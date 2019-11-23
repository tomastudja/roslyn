﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Wrapping.SeparatedSyntaxList;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Wrapping.InitializerExpression
{
    internal abstract partial class AbstractInitializerExpressionWrapper<TListSyntax, TListItemSyntax>
    {
        private class InitializerExpressionCodeActionComputer : AbstractCodeActionComputer<AbstractInitializerExpressionWrapper<TListSyntax, TListItemSyntax>>
        {
            private readonly TListSyntax _listSyntax;
            private readonly SeparatedSyntaxList<TListItemSyntax> _listItems;
            private readonly SyntaxTrivia _afterOpenTokenIndentationTrivia;
            private readonly SyntaxTrivia _singleIndentationTrivia;
            private readonly SyntaxTrivia _braceIndentationTrivia;
            private readonly bool _doWrapInitializerOpenBrace;

            public InitializerExpressionCodeActionComputer(
                AbstractInitializerExpressionWrapper<TListSyntax, TListItemSyntax> service,
                Document document, SourceText sourceText, DocumentOptionSet options,
                TListSyntax listSyntax, SeparatedSyntaxList<TListItemSyntax> listItems,
                bool doWrapInitializerOpenBrace, CancellationToken cancellationToken)
                : base(service, document, sourceText, options, cancellationToken)
            {
                _listSyntax = listSyntax;
                _listItems = listItems;

                var generator = SyntaxGenerator.GetGenerator(OriginalDocument);

                _afterOpenTokenIndentationTrivia = generator.Whitespace(GetAfterOpenTokenIdentation());
                _singleIndentationTrivia = generator.Whitespace(GetSingleIdentation());
                _braceIndentationTrivia = generator.Whitespace(GetBraceTokenIndentation());

                _doWrapInitializerOpenBrace = doWrapInitializerOpenBrace;
            }

            private string GetAfterOpenTokenIdentation()
            {
                var openToken = _listSyntax.GetFirstToken();
                var afterOpenTokenOffset = OriginalSourceText.GetOffset(openToken.Span.End);

                var indentString = afterOpenTokenOffset.CreateIndentationString(UseTabs, TabSize);
                return indentString;
            }

            private string GetBraceTokenIndentation()
            {
                var previousToken = _listSyntax.GetFirstToken().GetPreviousToken();
                return GetSmartIndentationAfter(previousToken);
            }

            private string GetSingleIdentation()
            {
                // Insert a newline after the open token of the list.  Then ask the
                // ISynchronousIndentationService where it thinks that the next line should be
                // indented.
                var openToken = _listSyntax.GetFirstToken();

                return GetSmartIndentationAfter(openToken);
            }

            protected override async Task<ImmutableArray<WrappingGroup>> ComputeWrappingGroupsAsync()
            {
                var result = ArrayBuilder<WrappingGroup>.GetInstance();
                await AddWrappingGroups(result).ConfigureAwait(false);
                return result.ToImmutableAndFree();
            }

            private async Task AddWrappingGroups(ArrayBuilder<WrappingGroup> result)
            {
                result.Add(await GetWrapEveryGroupAsync().ConfigureAwait(false));
                result.Add(await GetUnwrapGroupAsync().ConfigureAwait(false));
                result.Add(await GetWrapLongGroupAsync().ConfigureAwait(false));
            }

            private async Task<WrappingGroup> GetWrapEveryGroupAsync()
            {
                var parentTitle = Wrapper.Wrap_every_item;

                var codeActions = ArrayBuilder<WrapItemsAction>.GetInstance();

                codeActions.Add(await GetWrapEveryNestedCodeActionAsync(
                    parentTitle, WrappingStyle.WrapFirst_IndentRest).ConfigureAwait(false));

                // See comment in GetWrapLongTopLevelCodeActionAsync for explanation of why we're
                // not inlinable.
                return new WrappingGroup(isInlinable: false, codeActions.ToImmutableAndFree());
            }

            private async Task<WrapItemsAction> GetWrapEveryNestedCodeActionAsync(
                string parentTitle, WrappingStyle wrappingStyle)
            {
                var indentationTrivia = GetIndentationTrivia(wrappingStyle);

                var edits = GetWrapEachEdits(wrappingStyle, indentationTrivia);

                return await TryCreateCodeActionAsync(edits, parentTitle, Wrapper.Indent_all_items).ConfigureAwait(false);
            }

            private SyntaxTrivia GetIndentationTrivia(WrappingStyle wrappingStyle)
            {
                return wrappingStyle == WrappingStyle.UnwrapFirst_AlignRest
                    ? _afterOpenTokenIndentationTrivia
                    : _singleIndentationTrivia;
            }

            private ImmutableArray<Edit> GetWrapEachEdits(
                WrappingStyle wrappingStyle, SyntaxTrivia indentationTrivia)
            {
                var result = ArrayBuilder<Edit>.GetInstance();

                if (_doWrapInitializerOpenBrace)
                {
                    result.Add(Edit.UpdateBetween(_listSyntax.GetFirstToken().GetPreviousToken(), NewLineTrivia, _braceIndentationTrivia, _listSyntax.GetFirstToken()));
                }

                AddTextChangeBetweenOpenAndFirstItem(wrappingStyle, result);

                var itemsAndSeparators = _listItems.GetWithSeparators();

                for (var i = 0; i < itemsAndSeparators.Count; i += 2)
                {
                    var item = itemsAndSeparators[i].AsNode();
                    if (i < itemsAndSeparators.Count - 1)
                    {
                        // intermediary item
                        var comma = itemsAndSeparators[i + 1].AsToken();
                        result.Add(Edit.DeleteBetween(item, comma));

                        // Always wrap between this comma and the next item.
                        result.Add(Edit.UpdateBetween(
                            comma, NewLineTrivia, indentationTrivia, itemsAndSeparators[i + 2]));
                    }
                }

                result.Add(Edit.UpdateBetween(_listItems.Last(), NewLineTrivia, _braceIndentationTrivia, _listSyntax.GetLastToken()));

                return result.ToImmutableAndFree();
            }

            private void AddTextChangeBetweenOpenAndFirstItem(
                WrappingStyle wrappingStyle, ArrayBuilder<Edit> result)
            {
                result.Add(wrappingStyle == WrappingStyle.WrapFirst_IndentRest
                    ? Edit.UpdateBetween(_listSyntax.GetFirstToken(), NewLineTrivia, _singleIndentationTrivia, _listItems[0])
                    : Edit.DeleteBetween(_listSyntax.GetFirstToken(), _listItems[0]));
            }

            private async Task<WrappingGroup> GetUnwrapGroupAsync()
            {
                var unwrapActions = ArrayBuilder<WrapItemsAction>.GetInstance();

                var parentTitle = Wrapper.Unwrap_list;
                unwrapActions.Add(await GetUnwrapAllCodeActionAsync(parentTitle, WrappingStyle.UnwrapFirst_IndentRest).ConfigureAwait(false));

                // The 'unwrap' title strings are unique and do not collide with any other code
                // actions we're computing.  So they can be inlined if possible.
                return new WrappingGroup(isInlinable: true, unwrapActions.ToImmutableAndFree());
            }

            private async Task<WrapItemsAction> GetUnwrapAllCodeActionAsync(string parentTitle, WrappingStyle wrappingStyle)
            {
                var edits = GetUnwrapAllEdits(wrappingStyle);
                var title = Wrapper.Unwrap_all_items;

                return await TryCreateCodeActionAsync(edits, parentTitle, title).ConfigureAwait(false);
            }

            private ImmutableArray<Edit> GetUnwrapAllEdits(WrappingStyle wrappingStyle)
            {
                var result = ArrayBuilder<Edit>.GetInstance();

                if (_doWrapInitializerOpenBrace)
                {
                    result.Add(Edit.DeleteBetween(_listSyntax.GetFirstToken().GetPreviousToken(), _listSyntax.GetFirstToken()));
                }

                AddTextChangeBetweenOpenAndFirstItem(wrappingStyle, result);

                foreach (var comma in _listItems.GetSeparators())
                {
                    result.Add(Edit.DeleteBetween(comma.GetPreviousToken(), comma));
                    result.Add(Edit.DeleteBetween(comma, comma.GetNextToken()));
                }

                result.Add(Edit.DeleteBetween(_listItems.Last(), _listSyntax.GetLastToken()));
                return result.ToImmutableAndFree();
            }

            private async Task<WrappingGroup> GetWrapLongGroupAsync()
            {
                var parentTitle = Wrapper.Wrap_long_list;
                var codeActions = ArrayBuilder<WrapItemsAction>.GetInstance();

                codeActions.Add(await GetWrapLongLineCodeActionAsync(
                    parentTitle, WrappingStyle.WrapFirst_IndentRest).ConfigureAwait(false));

                return new WrappingGroup(isInlinable: false, codeActions.ToImmutableAndFree());
            }

            private async Task<WrapItemsAction> GetWrapLongLineCodeActionAsync(
                string parentTitle, WrappingStyle wrappingStyle)
            {
                var indentationTrivia = GetIndentationTrivia(wrappingStyle);

                var edits = GetWrapLongLinesEdits(wrappingStyle, indentationTrivia);

                return await TryCreateCodeActionAsync(edits, parentTitle, Wrapper.Indent_all_items).ConfigureAwait(false);
            }

            private ImmutableArray<Edit> GetWrapLongLinesEdits(
                WrappingStyle wrappingStyle, SyntaxTrivia indentationTrivia)
            {
                var result = ArrayBuilder<Edit>.GetInstance();

                if (_doWrapInitializerOpenBrace)
                {
                    result.Add(Edit.UpdateBetween(_listSyntax.GetFirstToken().GetPreviousToken(), NewLineTrivia, _braceIndentationTrivia, _listSyntax.GetFirstToken()));
                }

                AddTextChangeBetweenOpenAndFirstItem(wrappingStyle, result);

                var currentOffset = wrappingStyle == WrappingStyle.WrapFirst_IndentRest
                    ? indentationTrivia.FullWidth()
                    : _afterOpenTokenIndentationTrivia.FullWidth();
                var itemsAndSeparators = _listItems.GetWithSeparators();

                for (var i = 0; i < itemsAndSeparators.Count; i += 2)
                {
                    var item = itemsAndSeparators[i].AsNode();

                    // Figure out where we'd be after this item.
                    currentOffset += item.Span.Length;

                    if (i > 0)
                    {
                        if (currentOffset < WrappingColumn)
                        {
                            // this item would not make us go pass our preferred wrapping column. So
                            // keep it on this line, making sure there's a space between the previous
                            // comma and us.
                            result.Add(Edit.UpdateBetween(itemsAndSeparators[i - 1], SingleWhitespaceTrivia, NoTrivia, item));
                            currentOffset += " ".Length;
                        }
                        else
                        {
                            // not the first item on the line and this item makes us go past the wrapping
                            // limit.  We want to wrap before this item.
                            result.Add(Edit.UpdateBetween(itemsAndSeparators[i - 1], NewLineTrivia, indentationTrivia, item));
                            currentOffset = indentationTrivia.FullWidth() + item.Span.Length;
                        }
                    }

                    //// Get rid of any spaces between the list item and the following token (a
                    //// comma or close token).
                    var nextToken = item.GetLastToken().GetNextToken();

                    currentOffset += nextToken.Span.Length;
                }

                result.Add(Edit.UpdateBetween(_listItems.Last(), NewLineTrivia, _braceIndentationTrivia, _listSyntax.GetLastToken()));

                return result.ToImmutableAndFree();
            }
        }
    }
}
