﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using static Microsoft.CodeAnalysis.CodeActions.CodeAction;

namespace Microsoft.CodeAnalysis.CSharp.Editor.Wrapping
{
    internal partial class CSharpParameterWrappingCodeRefactoringProvider
    {
        private class CodeActionComputer
        {
            private readonly Document _originalDocument;
            private readonly DocumentOptionSet _options;
            private readonly BaseParameterListSyntax _parameterList;

            private readonly bool _useTabs;
            private readonly int _tabSize;
            private readonly string _newLine;
            private readonly int _wrappingColumn;

            private SourceText _originalSourceText;
            private string _singleIndentionOpt;
            private string _afterOpenTokenIndentation;

            public CodeActionComputer(
                Document document, DocumentOptionSet options,
                BaseParameterListSyntax parameterList)
            {
                _originalDocument = document;
                _options = options;
                _parameterList = parameterList;

                _useTabs = options.GetOption(FormattingOptions.UseTabs);
                _tabSize = options.GetOption(FormattingOptions.TabSize);
                _newLine = options.GetOption(FormattingOptions.NewLine);
                _wrappingColumn = options.GetOption(FormattingOptions.PreferredWrappingColumn);
            }

            private static TextChange DeleteBetween(SyntaxNodeOrToken left, SyntaxNodeOrToken right)
                => UpdateBetween(left, right, "");

            private static TextChange UpdateBetween(SyntaxNodeOrToken left, SyntaxNodeOrToken right, string text)
                => new TextChange(TextSpan.FromBounds(left.Span.End, right.Span.Start), text);

            private void AddTextChangeBetweenOpenAndFirstItem(bool indentFirst, ArrayBuilder<TextChange> result)
            {
                result.Add(indentFirst
                    ? UpdateBetween(_parameterList.GetFirstToken(), _parameterList.Parameters[0], _newLine + _singleIndentionOpt)
                    : DeleteBetween(_parameterList.GetFirstToken(), _parameterList.Parameters[0]));
            }

            public async Task<ImmutableArray<CodeAction>> DoAsync(CancellationToken cancellationToken)
            {
                _originalSourceText = await _originalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                _afterOpenTokenIndentation = GetAfterOpenTokenIdentation(cancellationToken);
                _singleIndentionOpt = TryGetSingleIdentation(cancellationToken);

                return await GetTopLevelCodeActionsAsync(cancellationToken).ConfigureAwait(false);
            }

            private string GetAfterOpenTokenIdentation(CancellationToken cancellationToken)
            {
                var openToken = _parameterList.GetFirstToken();
                var afterOpenTokenOffset = _originalSourceText.GetOffset(openToken.Span.End);

                var indentString = afterOpenTokenOffset.CreateIndentationString(_useTabs, _tabSize);
                return indentString;
            }

            private string TryGetSingleIdentation(CancellationToken cancellationToken)
            {
                // Insert a newline after the open token of the parameter list.  Then ask the
                // ISynchronousIndentationService where it thinks that the next line should be indented.
                var openToken = _parameterList.GetFirstToken();

                var newSourceText = _originalSourceText.WithChanges(new TextChange(new TextSpan(openToken.Span.End, 0), _newLine));
                var newDocument = _originalDocument.WithText(newSourceText);

                var indentationService = newDocument.GetLanguageService<ISynchronousIndentationService>();
                var originalLineNumber = newSourceText.Lines.GetLineFromPosition(openToken.Span.Start).LineNumber;
                var desiredIndentation = indentationService.GetDesiredIndentation(
                    newDocument, originalLineNumber + 1, cancellationToken);

                if (desiredIndentation == null)
                {
                    return null;
                }

                var baseLine = newSourceText.Lines.GetLineFromPosition(desiredIndentation.Value.BasePosition);
                var baseOffsetInLine = desiredIndentation.Value.BasePosition - baseLine.Start;

                var indent = baseOffsetInLine + desiredIndentation.Value.Offset;

                var indentString = indent.CreateIndentationString(_useTabs, _tabSize);
                return indentString;
            }

            private string GetIndentationString(bool indentFirst, bool alignWithFirst)
            {
                if (indentFirst)
                {
                    return _singleIndentionOpt;
                }

                if (!alignWithFirst)
                {
                    return _singleIndentionOpt;
                }

                return _afterOpenTokenIndentation;
            }

            private async Task<CodeAction> CreateCodeActionAsync(
                HashSet<string> seenDocuments, ImmutableArray<TextChange> edits,
                string parentTitle, string title, CancellationToken cancellationToken)
            {
                if (edits.Length == 0)
                {
                    return null;
                }

                var finalEdits = ArrayBuilder<TextChange>.GetInstance();

                try
                {
                    foreach (var edit in edits)
                    {
                        var text = _originalSourceText.ToString(edit.Span);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // editing some piece of non-whitespace trivia.  We don't support this.
                            return null;
                        }

                        // Make sure we're not about to make an edit that just changes the code to what
                        // is already there.
                        if (text != edit.NewText)
                        {
                            finalEdits.Add(edit);
                        }
                    }

                    if (finalEdits.Count == 0)
                    {
                        return null;
                    }

                    var newSourceText = _originalSourceText.WithChanges(finalEdits);
                    var newDocument = _originalDocument.WithText(newSourceText);

                    var newRoot = await newDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                    var newOpenToken = newRoot.FindToken(_parameterList.SpanStart);
                    var newParameterList = newOpenToken.Parent;

                    var formattedDocument = await Formatter.FormatAsync(
                        newDocument, newParameterList.Span, cancellationToken: cancellationToken).ConfigureAwait(false);

                    // make sure we've actually made a textual change.
                    var finalSourceText = await formattedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);

                    var originalText = _originalSourceText.ToString();
                    var finalText = finalSourceText.ToString();

                    if (!seenDocuments.Add(finalText) ||
                        originalText == finalText)
                    {
                        return null;
                    }

                    return new WrapItemsAction(title, parentTitle, _ => Task.FromResult(formattedDocument));
                }
                finally
                {
                    finalEdits.Free();
                }
            }

            private async Task<ImmutableArray<CodeAction>> GetTopLevelCodeActionsAsync(CancellationToken cancellationToken)
            {
                var codeActions = ArrayBuilder<CodeAction>.GetInstance();
                var seenDocuments = new HashSet<string>();

                codeActions.AddIfNotNull(await GetWrapEveryTopLevelCodeActionAsync(
                    seenDocuments, cancellationToken).ConfigureAwait(false));

                codeActions.AddIfNotNull(await GetUnwrapAllTopLevelCodeActionsAsync(
                    seenDocuments, cancellationToken).ConfigureAwait(false));

                codeActions.AddIfNotNull(await GetWrapLongTopLevelCodeActionAsync(
                    seenDocuments, cancellationToken).ConfigureAwait(false));

                return SortActionsByMRU(codeActions.ToImmutableAndFree());
            }

            #region unwrap all

            private async Task<CodeAction> GetUnwrapAllTopLevelCodeActionsAsync(
                HashSet<string> seenDocuments, CancellationToken cancellationToken)
            {
                var unwrapActions = ArrayBuilder<CodeAction>.GetInstance();

                var parentTitle = string.Format(FeaturesResources.Unwrap_0, FeaturesResources.parameter_list);

                // 1. Unwrap:
                //      MethodName(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
                //
                // 2. Unwrap with indent:
                //      MethodName(
                //          int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
                unwrapActions.AddIfNotNull(await GetUnwrapAllCodeActionAsync(seenDocuments, parentTitle, indentFirst: false, cancellationToken).ConfigureAwait(false));
                unwrapActions.AddIfNotNull(await GetUnwrapAllCodeActionAsync(seenDocuments, parentTitle, indentFirst: true, cancellationToken).ConfigureAwait(false));

                var sorted = SortActionsByMRU(unwrapActions.ToImmutableAndFree());
                if (sorted.Length == 0)
                {
                    return null;
                }

                return sorted.Length == 1
                    ? sorted[0]
                    : new CodeActionWithNestedActions(parentTitle, sorted, isInlinable: true);
            }

            private Task<CodeAction> GetUnwrapAllCodeActionAsync(
                HashSet<string> seenDocuments, string parentTitle,
                bool indentFirst, CancellationToken cancellationToken)
            {
                if (indentFirst && _singleIndentionOpt == null)
                {
                    return null;
                }

                var edits = GetUnwrapAllEdits(indentFirst);
                var title = indentFirst
                    ? string.Format(FeaturesResources.Unwrap_and_indent_all_0, FeaturesResources.parameters)
                    : string.Format(FeaturesResources.Unwrap_all_0, FeaturesResources.parameters);
                
                return CreateCodeActionAsync(seenDocuments, edits, parentTitle, title, cancellationToken);
            }

            private ImmutableArray<TextChange> GetUnwrapAllEdits(bool indentFirst)
            {
                var result = ArrayBuilder<TextChange>.GetInstance();

                AddTextChangeBetweenOpenAndFirstItem(indentFirst, result);

                foreach (var comma in _parameterList.Parameters.GetSeparators())
                {
                    result.Add(DeleteBetween(comma.GetPreviousToken(), comma));
                    result.Add(DeleteBetween(comma, comma.GetNextToken()));
                }

                result.Add(DeleteBetween(_parameterList.Parameters.Last(), _parameterList.GetLastToken()));
                return result.ToImmutableAndFree();
            }

            #endregion

            #region wrap long line

            private async Task<CodeAction> GetWrapLongTopLevelCodeActionAsync(
                HashSet<string> seenDocuments, CancellationToken cancellationToken)
            {
                var parentTitle = string.Format(FeaturesResources.Wrap_long_0, FeaturesResources.parameter_list);
                var codeActions = ArrayBuilder<CodeAction>.GetInstance();

                // Wrap at long length, indent all parameters:
                //      MethodName(
                //          int a, int b, int c, int d, int e,
                //          int f, int g, int h, int i, int j)
                codeActions.AddIfNotNull(await GetWrapLongLineCodeActionAsync(
                    seenDocuments, parentTitle, indentFirst: true, alignWithFirst: true, cancellationToken).ConfigureAwait(false));

                // Wrap at long length, indent wrapped parameters:
                //      MethodName(int a, int b, int c, 
                //          int d, int e, int f, int g,
                //          int h, int i, int j)
                codeActions.AddIfNotNull(await GetWrapLongLineCodeActionAsync(
                    seenDocuments, parentTitle, indentFirst: false, alignWithFirst: false, cancellationToken).ConfigureAwait(false));

                // Wrap at long length, align with first parameter:
                //      MethodName(int a, int b, int c,
                //                 int d, int e, int f,
                //                 int g, int h, int i,
                //                 int j)
                codeActions.AddIfNotNull(await GetWrapLongLineCodeActionAsync(
                    seenDocuments, parentTitle, indentFirst: false, alignWithFirst: true, cancellationToken).ConfigureAwait(false));

                var sorted = SortActionsByMRU(codeActions.ToImmutableAndFree());
                if (sorted.Length == 0)
                {
                    return null;
                }

                return new CodeActionWithNestedActions(parentTitle, sorted, isInlinable: false);
            }

            private Task<CodeAction> GetWrapLongLineCodeActionAsync(
                HashSet<string> seenDocuments, string parentTitle, 
                bool indentFirst, bool alignWithFirst, CancellationToken cancellationToken)
            {
                var indentation = GetIndentationString(indentFirst, alignWithFirst);
                if (indentation == null)
                {
                    return null;
                }

                var edits = GetWrapLongLinesEdits(indentFirst, indentation);
                var title = GetNestedCodeActionTitle(indentFirst, alignWithFirst);

                return CreateCodeActionAsync(seenDocuments, edits, parentTitle, title, cancellationToken);
            }

            private ImmutableArray<TextChange> GetWrapLongLinesEdits(bool indentFirst, string indentation)
            {
                var result = ArrayBuilder<TextChange>.GetInstance();

                AddTextChangeBetweenOpenAndFirstItem(indentFirst, result);

                var currentOffset = indentation.Length;
                var parametersAndSeparators = _parameterList.Parameters.GetWithSeparators();
                var firstItemOnLine = true;

                for (var i = 0; i < parametersAndSeparators.Count; i += 2)
                {
                    var parameter = parametersAndSeparators[i].AsNode();

                    if (i < parametersAndSeparators.Count - 1)
                    {
                        // Get rid of any spaces between the list item and the following comma
                        var comma = parametersAndSeparators[i + 1].AsToken();
                        result.Add(DeleteBetween(parameter, comma));

                        result.Add(DeleteBetween(comma, parametersAndSeparators[i + 2]));

                        if (firstItemOnLine || currentOffset < _wrappingColumn)
                        {
                            // Either this was the first item on the line (and thus we always want
                            // to emit it), or this item didn't take us past the wrapping limit.
                            // All we need to do here is remove the space between this comma and the 
                            // next item.
                            firstItemOnLine = false;
                        }
                        else
                        {
                            // not the first item on the line and this item makes us go past the
                            // wrapping limit.  We want to wrap before this item.
                            result.Add(UpdateBetween(parametersAndSeparators[i - 1], parameter, _newLine + indentation));
                            currentOffset = indentation.Length;
                            firstItemOnLine = true;
                        }

                        // Determine the offset after this parameter this ensures we always place at least one parameter before wrapping.
                        currentOffset += parameter.Span.Length + comma.Span.Length;
                    }
                }

                // last parameter.  Delete whatever is between it and the close token of the list.
                result.Add(DeleteBetween(_parameterList.Parameters.Last(), _parameterList.GetLastToken()));

                return result.ToImmutableAndFree();
            }

            #endregion

            #region wrap every

            private async Task<CodeAction> GetWrapEveryTopLevelCodeActionAsync(HashSet<string> seenDocuments, CancellationToken cancellationToken)
            {
                var parentTitle = string.Format(FeaturesResources.Wrap_every_0, FeaturesResources.parameter);

                var codeActions = ArrayBuilder<CodeAction>.GetInstance();

                // Wrap each parameter, indent all parameters
                //      MethodName(
                //          int a,
                //          int b,
                //          ...
                //          int j)
                codeActions.AddIfNotNull(await GetWrapEveryNestedCodeActionAsync(
                    seenDocuments, parentTitle, indentFirst: true, alignWithFirst: true, cancellationToken).ConfigureAwait(false));

                // Wrap each parameter. indent wrapped parameters:
                //      MethodName(int a,
                //          int b,
                //          ...
                //          int j)
                codeActions.AddIfNotNull(await GetWrapEveryNestedCodeActionAsync(
                    seenDocuments, parentTitle, indentFirst: false, alignWithFirst: false, cancellationToken).ConfigureAwait(false));

                // Wrap each parameter, align with first parameter
                //      MethodName(int a,
                //                 int b,
                //                 ...
                //                 int j);
                codeActions.AddIfNotNull(await GetWrapEveryNestedCodeActionAsync(
                    seenDocuments, parentTitle, indentFirst: false, alignWithFirst: true, cancellationToken).ConfigureAwait(false));

                var sorted = SortActionsByMRU(codeActions.ToImmutableAndFree());
                if (sorted.Length == 0)
                {
                    return null;
                }

                return new CodeActionWithNestedActions(parentTitle, sorted, isInlinable: false);
            }

            private Task<CodeAction> GetWrapEveryNestedCodeActionAsync(
                HashSet<string> seenDocuments, string parentTitle, 
                bool indentFirst, bool alignWithFirst, CancellationToken cancellationToken)
            {
                var indentation = GetIndentationString(indentFirst, alignWithFirst);
                if (indentation == null)
                {
                    return null;
                }

                var edits = GetWrapEachEdits(indentFirst, indentation);
                var title = GetNestedCodeActionTitle(indentFirst, alignWithFirst);

                return CreateCodeActionAsync(
                    seenDocuments, edits, parentTitle, title, cancellationToken);
            }

            private static string GetNestedCodeActionTitle(bool indentFirst, bool alignWithFirst)
            {
                return indentFirst
                    ? string.Format(FeaturesResources.Indent_all_0, FeaturesResources.parameters)
                    : alignWithFirst
                        ? string.Format(FeaturesResources.Align_wrapped_0, FeaturesResources.parameters)
                        : string.Format(FeaturesResources.Indent_wrapped_0, FeaturesResources.parameters);
            }

            private ImmutableArray<TextChange> GetWrapEachEdits(bool indentFirst, string indentation)
            {
                var result = ArrayBuilder<TextChange>.GetInstance();

                AddTextChangeBetweenOpenAndFirstItem(indentFirst, result);

                var parametersAndSeparators = _parameterList.Parameters.GetWithSeparators();

                for (var i = 0; i < parametersAndSeparators.Count; i += 2)
                {
                    var parameter = parametersAndSeparators[i].AsNode();
                    if (i < parametersAndSeparators.Count - 1)
                    {
                        // intermediary parameter
                        var comma = parametersAndSeparators[i + 1].AsToken();
                        result.Add(DeleteBetween(parameter, comma));

                        // Always wrap between this comma and the next item.
                        result.Add(UpdateBetween(comma, parametersAndSeparators[i + 2], _newLine + indentation));
                    }
                }

                // last parameter.  Delete whatever is between it and the close token of the list.
                result.Add(DeleteBetween(_parameterList.Parameters.Last(), _parameterList.GetLastToken()));

                return result.ToImmutableAndFree();
            }

            #endregion
        }
    }
}
