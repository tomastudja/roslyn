﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Indentation;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CSharp.StringCopyPaste
{
    using static StringCopyPasteHelpers;
    using static SyntaxFactory;

    /// <summary>
    /// Implementation of <see cref="AbstractPasteProcessor"/> used when we know the original string literal expression
    /// we were copying text out of.  Because we know the original literal expression, we can determine what the
    /// characters being pasted meant in the original context and we can attempt to preserve that as closely as
    /// possible.
    /// </summary>
    internal class KnownSourcePasteProcessor : AbstractPasteProcessor
    {
        private readonly SnapshotSpan _selectionBeforePaste;
        private readonly StringCopyPasteData _copyPasteData;
        private readonly ITextBufferFactoryService3 _textBufferFactoryService;

        public KnownSourcePasteProcessor(
            string newLine,
            IndentationOptions indentationOptions,
            ITextSnapshot snapshotBeforePaste,
            ITextSnapshot snapshotAfterPaste,
            Document documentBeforePaste,
            Document documentAfterPaste,
            ExpressionSyntax stringExpressionBeforePaste,
            SnapshotSpan selectionBeforePaste,
            StringCopyPasteData copyPasteData,
            ITextBufferFactoryService3 textBufferFactoryService)
            : base(newLine, indentationOptions, snapshotBeforePaste, snapshotAfterPaste, documentBeforePaste, documentAfterPaste, stringExpressionBeforePaste)
        {
            _selectionBeforePaste = selectionBeforePaste;
            _copyPasteData = copyPasteData;
            _textBufferFactoryService = textBufferFactoryService;
        }

        public override ImmutableArray<TextChange> GetEdits(CancellationToken cancellationToken)
        {
            // For pastes into non-raw strings, we can just determine how the change should be escaped in-line at that
            // same location the paste originally happened at.  For raw-strings things get more complex as we have to
            // deal with things like indentation and potentially adding newlines to make things legal.

            // Smart Pasting into raw string not supported yet.  
            if (IsAnyRawStringExpression(StringExpressionBeforePaste))
                return default;
                // return GetEditsForRawString(cancellationToken);

            return GetEditsForNonRawString();
        }

        private ImmutableArray<TextChange> GetEditsForNonRawString()
        {
            using var _ = PooledStringBuilder.GetInstance(out var builder);

            if (StringExpressionBeforePaste is LiteralExpressionSyntax literal)
            {
                var isVerbatim = literal.Token.IsVerbatimStringLiteral();
                foreach (var content in _copyPasteData.Contents)
                {
                    if (content.IsText)
                    {
                        builder.Append(EscapeForNonRawStringLiteral(
                            isVerbatim, isInterpolated: false, trySkipExistingEscapes: false, content.TextValue));
                    }
                    else if (content.IsInterpolation)
                    {
                        // we're copying an interpolation from an interpolated string to a string literal. For example,
                        // we're pasting `{x + y}` into the middle of `"goobar"`.  One thing we could potentially do in
                        // the future is split the literal into `"goo" + $"{x + y}" + "bar"`, or just making the
                        // containing literal into an interpolation itself.  However, for now, we do the simple thing
                        // and just treat the interpolation as raw text that should just be escaped as appropriate into
                        // the destination.
                        builder.Append('{');
                        builder.Append(EscapeForNonRawStringLiteral(
                            isVerbatim, isInterpolated: false, trySkipExistingEscapes: false, content.InterpolationExpression));

                        if (content.InterpolationAlignmentClause != null)
                            builder.Append(content.InterpolationAlignmentClause);

                        if (content.InterpolationFormatClause != null)
                        {
                            builder.Append(':');
                            builder.Append(EscapeForNonRawStringLiteral(
                                isVerbatim, isInterpolated: false, trySkipExistingEscapes: false, content.InterpolationFormatClause));
                        }

                        builder.Append('}');
                    }
                    else
                    {
                        throw ExceptionUtilities.UnexpectedValue(content.Kind);
                    }
                }
            }
            else if (StringExpressionBeforePaste is InterpolatedStringExpressionSyntax interpolatedString)
            {
                var isVerbatim = interpolatedString.StringStartToken.Kind() is SyntaxKind.InterpolatedVerbatimStringStartToken;
                foreach (var content in _copyPasteData.Contents)
                {
                    if (content.IsText)
                    {
                        builder.Append(EscapeForNonRawStringLiteral(
                            isVerbatim, isInterpolated: true, trySkipExistingEscapes: false, content.TextValue));
                    }
                    else if (content.IsInterpolation)
                    {
                        // we're moving an interpolation from one interpolation to another.  This can just be copied
                        // wholesale *except* for the format literal portion (e.g. `{...:XXXX}` which may have to be updated
                        // for the destination type.
                        builder.Append('{');
                        builder.Append(content.InterpolationExpression);

                        if (content.InterpolationAlignmentClause != null)
                            builder.Append(content.InterpolationAlignmentClause);

                        if (content.InterpolationFormatClause != null)
                        {
                            builder.Append(':');
                            builder.Append(EscapeForNonRawStringLiteral(
                                isVerbatim, isInterpolated: true, trySkipExistingEscapes: false, content.InterpolationFormatClause));
                        }

                        builder.Append('}');
                    }
                    else
                    {
                        throw ExceptionUtilities.UnexpectedValue(content.Kind);
                    }
                }
            }
            else
            {
                throw ExceptionUtilities.Unreachable;
            }

            return ImmutableArray.Create(new TextChange(_selectionBeforePaste.Span.ToTextSpan(), builder.ToString()));
#if false
            using var _ = ArrayBuilder<TextChange>.GetInstance(out var edits);

            foreach (var change in Changes)
            {
                var wrappedChange = WrapChangeWithOriginalQuotes(change.NewText);
                var parsedChange = ParseExpression(wrappedChange);

                // If for some reason we can't actually successfully parse this copied text, then bail out.
                if (ContainsError(parsedChange))
                    return default;

                var modifiedText = TransformValueToDestinationKind(parsedChange);
                edits.Add(new TextChange(change.OldSpan.ToTextSpan(), modifiedText));
            }

            return edits.ToImmutable();

            string TransformValueToDestinationKind(ExpressionSyntax parsedChange)
            {
                // we have a matrix of every string source type to every string destination type.
                // 
                // Normal string
                // Interpolated string
                // Verbatim string
                // Verbatim interpolated string
                var pastingIntoVerbatimString = IsVerbatimStringExpression(StringExpressionBeforePaste);

                return (parsedChange, StringExpressionBeforePaste) switch
                {
                    (LiteralExpressionSyntax pastedText, LiteralExpressionSyntax) => TransformLiteralToLiteral(pastedText),
                    (LiteralExpressionSyntax pastedText, InterpolatedStringExpressionSyntax) => TransformLiteralToInterpolatedString(pastedText),
                    (InterpolatedStringExpressionSyntax pastedText, LiteralExpressionSyntax) => TransformInterpolatedStringToLiteral(pastedText),
                    (InterpolatedStringExpressionSyntax pastedText, InterpolatedStringExpressionSyntax) => TransformInterpolatedStringToInterpolatedString(pastedText),
                    _ => throw ExceptionUtilities.Unreachable,
                };

                string TransformLiteralToLiteral(LiteralExpressionSyntax pastedText)
                {
                    var textValue = pastedText.Token.ValueText;
                    return EscapeForNonRawStringLiteral(
                        isVerbatim: pastingIntoVerbatimString, isInterpolated: false, trySkipExistingEscapes: false, textValue);
                }

                string TransformLiteralToInterpolatedString(LiteralExpressionSyntax pastedText)
                {
                    var textValue = pastedText.Token.ValueText;
                    return EscapeForNonRawStringLiteral(
                        isVerbatim: pastingIntoVerbatimString, isInterpolated: true, trySkipExistingEscapes: false, textValue);
                }

                string TransformInterpolatedStringToLiteral(InterpolatedStringExpressionSyntax pastedText)
                {
                    using var _ = PooledStringBuilder.GetInstance(out var builder);
                    foreach (var content in pastedText.Contents)
                    {
                        if (content is InterpolatedStringTextSyntax stringText)
                        {
                            builder.Append(EscapeForNonRawStringLiteral(
                                pastingIntoVerbatimString, isInterpolated: true, trySkipExistingEscapes: false, stringText.TextToken.ValueText));
                        }
                        else if (content is InterpolationSyntax interpolation)
                        {
                            // we're copying an interpolation from an interpolated string to a string literal. For example,
                            // we're pasting `{x + y}` into the middle of `"goobar"`.  One thing we could potentially do in the
                            // future is split the literal into `"goo" + $"{x + y}" + "bar"`.  However, it's unclear if that
                            // would actually be desirable as `$"{x + x}"` may have no meaning in the destination location. So,
                            // for now, we do the simple thing and just treat the interpolation as raw text that should just be
                            // escaped as appropriate into the destination.
                            builder.Append(EscapeForNonRawStringLiteral(
                                pastingIntoVerbatimString, isInterpolated: false, trySkipExistingEscapes: false, interpolation.ToString()));
                        }
                    }

                    return builder.ToString();
                }

                string TransformInterpolatedStringToInterpolatedString(InterpolatedStringExpressionSyntax pastedText)
                {
                    using var _ = PooledStringBuilder.GetInstance(out var builder);
                    foreach (var content in pastedText.Contents)
                    {
                        if (content is InterpolatedStringTextSyntax stringText)
                        {
                            builder.Append(EscapeForNonRawStringLiteral(
                                pastingIntoVerbatimString, isInterpolated: false, trySkipExistingEscapes: false, stringText.TextToken.ValueText));
                        }
                        else if (content is InterpolationSyntax interpolation)
                        {
                            // we're moving an interpolation from one interpolation to another.  This can just be copied
                            // wholesale *except* for the format literal portion (e.g. `{...:XXXX}` which may have to be updated
                            // for the destination type.
                            if (interpolation.FormatClause is not null)
                            {
                                var oldToken = interpolation.FormatClause.FormatStringToken;
                                var newToken = Token(
                                    oldToken.LeadingTrivia, oldToken.Kind(),
                                    EscapeForNonRawStringLiteral(
                                        pastingIntoVerbatimString, isInterpolated: false, trySkipExistingEscapes: false, oldToken.ValueText),
                                    oldToken.ValueText, oldToken.TrailingTrivia);

                                interpolation = interpolation.ReplaceToken(oldToken, newToken);
                            }

                            builder.Append(interpolation.ToString());
                        }
                    }

                    return builder.ToString();
                }
            }

#endif
        }

#if false

        /// <summary>
        /// Takes a chunk of pasted text and reparses it as if it was surrounded by the original quotes it had in the
        /// string it came from.  With this we can determine how to interpret things like the escapes in their original
        /// context.  We can also figure out how to deal with copied interpolations.
        /// </summary>
        private string WrapChangeWithOriginalQuotes(string pastedText)
        {
            var textCopiedFrom = _snapshotCopiedFrom.AsText();
            var stringExpressionCopiedFromInfo = StringInfo.GetStringInfo(textCopiedFrom, _stringExpressionCopiedFrom);

            var startQuote = textCopiedFrom.ToString(stringExpressionCopiedFromInfo.StartDelimiterSpan);
            var endQuote = textCopiedFrom.ToString(stringExpressionCopiedFromInfo.EndDelimiterSpan);

            if (!IsAnyMultiLineRawStringExpression(_stringExpressionCopiedFrom))
                return $"{startQuote}{pastedText}{endQuote}";

            // With a raw string we have the issue that the contents may need to be indented properly in order for the
            // string to parsed successfully.  Because we're using the original start/end quote to wrap the text that
            // was pasted this normally is not an issue.  However, it can be a problem in the following case:
            //
            //      var source = """
            //              existing text
            //              [|copy
            //              this|]
            //              existing text
            //              """
            //
            // In this case, the first line of the text will not start with enough indentation and we will generate:
            //
            // """
            // copy
            //              this
            //              """
            //
            // To address this.  We ensure that if the content starts with spaces to not be a problem.
            var endLine = textCopiedFrom.Lines.GetLineFromPosition(_stringExpressionCopiedFrom.Span.End);
            var rawStringIndentation = endLine.GetLeadingWhitespace();

            var pastedTextWhitespace = pastedText.GetLeadingWhitespace();

            // First, if we don't have enough indentation whitespace in the string, but we do have a portion of the
            // necessary whitespace, then synthesize the remainder we need.
            if (pastedTextWhitespace.Length < rawStringIndentation.Length)
            {
                if (rawStringIndentation.EndsWith(pastedTextWhitespace))
                    return $"{startQuote}{rawStringIndentation[..^pastedTextWhitespace.Length]}{pastedText}{endQuote}";
            }
            else
            {
                // We have a lot of indentation whitespace.  Make sure it's legal though for this raw string.  If so,
                // nothing to do.
                if (pastedTextWhitespace.StartsWith(rawStringIndentation))
                    return $"{startQuote}{pastedText}{endQuote}";
            }

            // We have something with whitespace incompatible with the raw string indentation.  Just add the required
            // indentation we need to ensure this can parse.  Note: this is a heuristic, and it's possible we could
            // figure out something better here (for example copying just enough indentation whitespace to make things
            // successfully parse).
            return $"{startQuote}{rawStringIndentation}{pastedText}{endQuote}";
        }

#endif

#if false

        private ImmutableArray<TextChange> GetEditsForRawString(CancellationToken cancellationToken)
        {
            // Can't really figure anything out if the raw string is in error.
            if (NodeOrTokenContainsError(StringExpressionBeforePaste))
                return default;

            // If all we're going to do is insert whitespace, then don't make any adjustments to the text. We don't want
            // to end up inserting nothing and having the user very confused why their paste did nothing.
            if (AllWhitespace(SnapshotBeforePaste.Version.Changes))
                return default;

            using var _ = ArrayBuilder<TextChange>.GetInstance(out var edits);

            // After we've inserted the text we may now have made the raw string illegal (for example, having too many
            // quotes in it now).  Update the delimiters to make the content legal if necessary. if the content we're
            // going to add itself contains quotes, then figure out how many start/end quotes the final string literal
            // will need (which also gives us the number of quotes to add to the start/end).
            var quotesToAdd = GetQuotesToAddToRawString();
            var dollarSignsToAdd = GetDollarSignsToAddToRawString();

            // First, add any extra dollar signs needed.
            if (dollarSignsToAdd != null)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePaste.Span.Start, 0), dollarSignsToAdd));

            // Then any quotes to your starting delimiter
            if (quotesToAdd != null)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.ContentSpans.First().Start, 0), quotesToAdd));

            if (IsAnyMultiLineRawStringExpression(StringExpressionBeforePaste))
                AdjustWhitespaceAndAddTextChangesForMultiLineRawStringLiteral(edits);
            else
                AdjustWhitespaceAndAddTextChangesForSingleLineRawStringLiteral(edits, cancellationToken);

            // Then add any extra end quotes needed.
            if (quotesToAdd != null)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.EndDelimiterSpanWithoutSuffix.End, 0), quotesToAdd));

            foreach (var change in Changes)
            {
                var wrappedChange = WrapChangeWithOriginalQuotes(change.NewText);
                var parsedChange = ParseExpression(wrappedChange);

                // If for some reason we can't actually successfully parse this copied text, then bail out.
                if (ContainsError(parsedChange))
                    return default;

                var modifiedText = TransformValueToDestinationKind(parsedChange);
                edits.Add(new TextChange(change.OldSpan.ToTextSpan(), modifiedText));
            }

            return edits.ToImmutable();
        }

        private void AdjustWhitespaceAndAddTextChangesForSingleLineRawStringLiteral(ArrayBuilder<TextChange> edits, CancellationToken cancellationToken)
        {
            // When pasting into a single-line raw literal we will keep it a single line if we can.  If the content
            // we're pasting starts/ends with a quote, or contains a newline, then we have to convert to a multiline.
            //
            // Pasting any other content into a single-line raw literal is always legal and needs no extra work on our
            // part.

            var mustBeMultiLine = RawContentMustBeMultiLine(TextAfterPaste, TextContentsSpansAfterPaste);
            var indentationWhitespace = StringExpressionBeforePaste.GetFirstToken().GetPreferredIndentation(DocumentBeforePaste, IndentationOptions, cancellationToken);

            // A newline and the indentation to start with.
            if (mustBeMultiLine)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.StartDelimiterSpan.End, 0), NewLine + indentationWhitespace));



            // if the last change ended at the closing delimiter *and* ended with a newline, then we don't need to add a
            // final newline-space at the end because we will have already done that.
            if (mustBeMultiLine && !LastPastedLineAddedNewLine())
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.EndDelimiterSpan.Start, 0), NewLine + indentationWhitespace));
        }

        private void AdjustWhitespaceAndAddTextChangesForMultiLineRawStringLiteral(ArrayBuilder<TextChange> edits)
        {
            throw new NotImplementedException();
        }
#endif

#if false

        private ImmutableArray<TextChange> GetEditsForRawString(CancellationToken cancellationToken)
        {

            // Can't really figure anything out if the raw string is in error.
            if (NodeOrTokenContainsError(StringExpressionBeforePaste))
                return default;

            // If all we're going to do is insert whitespace, then don't make any adjustments to the text. We don't want
            // to end up inserting nothing and having the user very confused why their paste did nothing.
            if (AllWhitespace(SnapshotBeforePaste.Version.Changes))
                return default;

            // Pasting into a raw string is complicated.  The code we are copying may have something like `"\"\"\""`
            // (where the middle \"\"\" is what is being copied).  That means we're actually going to be inserting 3
            // quotes into the raw string.  But we need to see that that's what's been inserted so that we can then
            // count and figure out how many total quotes there are in a row to determine what to do with the start/end
            // delimiter of the raw string.
            //
            // So, we do this work in two passes.  First, we take the pasted text, figure out what the actual
            // interpreted characters were that it was adding (e.g. `\"\"\"` -> `"""`).  Then, from that, we can figure
            // out what changes need to happen to happen to the raw literal itself, along with the actual content
            // changes to make.

            var internalContentChanges = GetRawStringContentChanges(cancellationToken);
            var clonedBuffer = _textBufferFactoryService.CreateTextBuffer(SnapshotBeforePaste.TextImage, SnapshotBeforePaste.ContentType);
            var clonedSnapshotBeforeFakePaste = clonedBuffer.CurrentSnapshot;
            var edit = clonedBuffer.CreateEdit();
            foreach (var change in internalContentChanges)
                edit.Replace(change.Span.ToSpan(), change.NewText);
            edit.Apply();

            var clonedSnapshotAfterFakePaste = clonedBuffer.CurrentSnapshot;
            var clonedTextAfterFakePaste = clonedSnapshotAfterFakePaste.AsText();
            var contentSpansAfterFakePaste = this.StringExpressionBeforePasteInfo.ContentSpans.SelectAsArray(
                ts => MapSpan(ts, clonedSnapshotBeforeFakePaste, clonedSnapshotAfterFakePaste));

            // After we've inserted the text we may now have made the raw string illegal (for example, having too many
            // quotes in it now).  Update the delimiters to make the content legal if necessary.
            // if the content we're going to add itself contains quotes, then figure out how many start/end quotes the
            // final string literal will need (which also gives us the number of quotes to add to the start/end).

            var quotesToAdd = GetQuotesToAddToRawString(clonedTextAfterFakePaste, contentSpansAfterFakePaste);
            var dollarSignsToAdd = GetDollarSignsToAddToRawString(clonedTextAfterFakePaste, contentSpansAfterFakePaste);
            var mustBeMultiLine =
                !IsAnyMultiLineRawStringExpression(StringExpressionBeforePaste) &&
                RawContentMustBeMultiLine(clonedTextAfterFakePaste, contentSpansAfterFakePaste);
            var indentationWhitespace = StringExpressionBeforePaste.GetFirstToken().GetPreferredIndentation(DocumentBeforePaste, IndentationOptions, cancellationToken);

            using var _ = ArrayBuilder<TextChange>.GetInstance(out var edits);

            // First, add any extra dollar signs needed.
            if (dollarSignsToAdd != null)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePaste.Span.Start, 0), dollarSignsToAdd));

            // Then any quotes to the starting delimiter
            if (quotesToAdd != null)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.ContentSpans.First().Start, 0), quotesToAdd));

            // A newline and the indentation to start with if we're converting to multi-line
            if (mustBeMultiLine)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.StartDelimiterSpan.End, 0), NewLine + indentationWhitespace));

            // Then add the actual changes in the content.
            edits.AddRange(internalContentChanges);

            // if the last change ended at the closing delimiter *and* ended with a newline, then we don't need to add a
            // final newline-space at the end because we will have already done that.
            if (mustBeMultiLine && !LastPastedLineAddedNewLine())
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.EndDelimiterSpan.Start, 0), NewLine + indentationWhitespace));

            // Then add any extra end quotes needed.
            if (quotesToAdd != null)
                edits.Add(new TextChange(new TextSpan(StringExpressionBeforePasteInfo.EndDelimiterSpanWithoutSuffix.End, 0), quotesToAdd));

            return edits.ToImmutable();
        }

        private ImmutableArray<TextChange> GetRawStringContentChanges(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

#endif
    }
}
