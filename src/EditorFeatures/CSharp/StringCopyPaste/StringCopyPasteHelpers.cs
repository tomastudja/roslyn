﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CSharp.StringCopyPaste
{
    internal static class StringCopyPasteHelpers
    {
        public static bool HasNewLine(TextLine line)
            => line.Span.End != line.SpanIncludingLineBreak.End;

        /// <summary>
        /// True if the string literal contains an error diagnostic that indicates a parsing problem with it. For
        /// interpolated strings, this only includes the text sections, and not any interpolation holes in the literal.
        /// </summary>
        public static bool ContainsError(ExpressionSyntax stringExpression)
        {
            if (stringExpression is LiteralExpressionSyntax)
                return NodeOrTokenContainsError(stringExpression);

            if (stringExpression is InterpolatedStringExpressionSyntax interpolatedString)
            {
                using var _ = PooledHashSet<Diagnostic>.GetInstance(out var errors);
                foreach (var diagnostic in interpolatedString.GetDiagnostics())
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Error)
                        errors.Add(diagnostic);
                }

                // we don't care about errors in holes.  Only errors in the content portions of the string.
                for (int i = 0, n = interpolatedString.Contents.Count; i < n && errors.Count > 0; i++)
                {
                    if (interpolatedString.Contents[i] is InterpolatedStringTextSyntax text)
                    {
                        foreach (var diagnostic in text.GetDiagnostics())
                            errors.Remove(diagnostic);
                    }
                }

                return errors.Count > 0;
            }

            throw ExceptionUtilities.UnexpectedValue(stringExpression);
        }

        public static bool NodeOrTokenContainsError(SyntaxNodeOrToken nodeOrToken)
        {
            foreach (var diagnostic in nodeOrToken.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        public static bool AllWhitespace(INormalizedTextChangeCollection changes)
        {
            foreach (var change in changes)
            {
                if (!AllWhitespace(change.NewText))
                    return false;
            }

            return true;
        }

        private static bool AllWhitespace(string text)
        {
            foreach (var ch in text)
            {
                if (!SyntaxFacts.IsWhitespace(ch))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Given a TextLine, returns the index (in the SourceText) of the first character of it that is not a
        /// Whitespace character.  The LineBreak parts of the line are not considered here.  If the line is empty/blank
        /// (again, not counting LineBreak characters) then -1 is returned.
        /// </summary>
        public static int GetFirstNonWhitespaceIndex(SourceText text, TextLine line)
        {
            for (int i = line.Start, n = line.End; i < n; i++)
            {
                if (!SyntaxFacts.IsWhitespace(text[i]))
                    return i;
            }

            return -1;
        }

        public static bool ContainsControlCharacter(INormalizedTextChangeCollection changes)
        {
            foreach (var change in changes)
            {
                if (ContainsControlCharacter(change.NewText))
                    return true;
            }

            return false;
        }

        public static bool ContainsControlCharacter(string newText)
        {
            foreach (var c in newText)
            {
                if (char.IsControl(c))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes all characters matching <see cref="SyntaxFacts.IsWhitespace(char)"/> from the start of <paramref
        /// name="value"/>.
        /// </summary>
        public static (string whitespace, string contents) ExtractWhitespace(string value)
        {
            var start = 0;
            while (start < value.Length && SyntaxFacts.IsWhitespace(value[start]))
                start++;

            return (value[..start], value[start..]);
        }

        public static bool IsAnyRawStringExpression(ExpressionSyntax expression)
            => expression is LiteralExpressionSyntax literal
                ? IsRawStringLiteral(literal)
                : IsRawStringLiteral((InterpolatedStringExpressionSyntax)expression);

        public static bool IsRawStringLiteral(InterpolatedStringExpressionSyntax interpolatedString)
            => interpolatedString.StringStartToken.Kind() is SyntaxKind.InterpolatedSingleLineRawStringStartToken or SyntaxKind.InterpolatedMultiLineRawStringStartToken;

        public static bool IsRawStringLiteral(LiteralExpressionSyntax literal)
            => literal.Token.Kind() is SyntaxKind.SingleLineRawStringLiteralToken or SyntaxKind.MultiLineRawStringLiteralToken;

        /// <summary>
        /// Given a string literal or interpolated string, returns the subspans of those expressions that are actual
        /// text content spans.  For a string literal, this is the span between the quotes.  For an interpolated string
        /// this is the text regions between the holes.  Note that for interpolated strings the content spans may be
        /// empty (for example, between two adjacent holes).  We still want to know about those empty spans so that if a
        /// paste happens into that empty region that we still escape properly.
        /// </summary>
        public static ImmutableArray<TextSpan> GetTextContentSpans(
            SourceText text, ExpressionSyntax stringExpression, out int delimiterQuoteCount)
        {
            if (stringExpression is LiteralExpressionSyntax literal)
            {
                return ImmutableArray.Create(GetTextContentSpan(text, literal, out delimiterQuoteCount));
            }
            else if (stringExpression is InterpolatedStringExpressionSyntax interpolatedString)
            {
                return GetTextContentSpans(text, interpolatedString, out delimiterQuoteCount);
            }
            else
            {
                throw ExceptionUtilities.UnexpectedValue(stringExpression);
            }
        }

        private static ImmutableArray<TextSpan> GetTextContentSpans(
            SourceText text, InterpolatedStringExpressionSyntax interpolatedString, out int delimiterQuoteCount)
        {
            // Interpolated string.  Normal, verbatim, or raw.
            //
            // Skip past the leading and trailing delimiters.
            var start = interpolatedString.SpanStart;
            while (start < text.Length && text[start] is '@' or '$')
                start++;

            var position = start;
            while (start < interpolatedString.StringStartToken.Span.End && text[start] == '"')
                start++;
            delimiterQuoteCount = start - position;

            var end = interpolatedString.Span.End;

            end = SkipU8Suffix(text, start, end);
            while (end > interpolatedString.StringEndToken.Span.Start && text[end - 1] == '"')
                end--;

            using var result = TemporaryArray<TextSpan>.Empty;
            var currentPosition = start;
            for (var i = 0; i < interpolatedString.Contents.Count; i++)
            {
                var content = interpolatedString.Contents[i];
                if (content is InterpolationSyntax)
                {
                    result.Add(TextSpan.FromBounds(currentPosition, content.SpanStart));
                    currentPosition = content.Span.End;
                }
            }

            // Then, once through the body, add a final span from the end of the last interpolation to the end delimiter.
            result.Add(TextSpan.FromBounds(currentPosition, end));
            return result.ToImmutableAndClear();
        }

        private static int SkipU8Suffix(SourceText text, int start, int end)
        {
            if (end > start && text[end - 1] == '8')
                end--;
            if (end > start && text[end - 1] is 'u' or 'U')
                end--;
            return end;
        }

        private static TextSpan GetTextContentSpan(SourceText text, LiteralExpressionSyntax literal, out int delimiterQuoteCount)
        {
            // simple string literal (normal, verbatim or raw).
            //
            // Skip past the leading and trailing delimiters and add the span in between.
            if (IsRawStringLiteral(literal))
            {
                var start = literal.SpanStart;
                while (start < text.Length && text[start] == '"')
                    start++;
                delimiterQuoteCount = start - literal.SpanStart;

                var end = literal.Span.End;

                end = SkipU8Suffix(text, start, end);
                while (end > start && text[end - 1] == '"')
                    end--;

                return TextSpan.FromBounds(start, end);
            }
            else
            {
                var start = literal.SpanStart;
                if (start < text.Length && text[start] == '@')
                    start++;

                var position = start;
                if (start < text.Length && text[start] == '"')
                    start++;
                delimiterQuoteCount = start - position;

                var end = literal.Span.End;

                end = SkipU8Suffix(text, start, end);
                if (end > start && text[end - 1] == '"')
                    end--;

                return TextSpan.FromBounds(start, end);
            }
        }

        /// <summary>
        /// Given a section of a document, finds the longest sequence of quote (<c>"</c>) characters in it.  Used to
        /// determine if a raw string literal needs to grow its delimiters to ensure that the quote sequence will no
        /// longer be a problem.
        /// </summary>
        public static int GetLongestQuoteSequence(SourceText text, TextSpan span)
        {
            var longestCount = 0;
            for (int currentIndex = span.Start, contentEnd = span.End; currentIndex < contentEnd;)
            {
                if (text[currentIndex] == '"')
                {
                    var endQuoteIndex = currentIndex;
                    while (endQuoteIndex < contentEnd && text[endQuoteIndex] == '"')
                        endQuoteIndex++;

                    longestCount = Math.Max(longestCount, endQuoteIndex - currentIndex);
                    currentIndex = endQuoteIndex;
                }
                else
                {
                    currentIndex++;
                }
            }

            return longestCount;
        }

        /// <summary>
        /// Given a set of selections, finds the innermost string-literal/interpolation that they are all contained in.
        /// If no such literal/interpolation exists, this returns null. 
        /// </summary>
        public static ExpressionSyntax? FindCommonContainingStringExpression(
            SyntaxNode root, NormalizedSnapshotSpanCollection selectionsBeforePaste)
        {
            ExpressionSyntax? expression = null;
            foreach (var snapshotSpan in selectionsBeforePaste)
            {
                var container = FindContainingSupportedStringExpression(root, snapshotSpan.Start.Position);
                if (container == null)
                    return null;

                expression ??= container;
                if (expression != container)
                    return null;
            }

            return expression;
        }

        public static ExpressionSyntax? FindContainingSupportedStringExpression(SyntaxNode root, int position)
        {
            var node = root.FindToken(position).Parent;
            for (var current = node; current != null; current = current.Parent)
            {
                if (current is LiteralExpressionSyntax literalExpression)
                    return IsSupportedStringExpression(literalExpression) ? literalExpression : null;

                if (current is InterpolatedStringExpressionSyntax interpolatedString)
                    return IsSupportedStringExpression(interpolatedString) ? interpolatedString : null;
            }

            return null;
        }

        public static bool IsSupportedStringExpression(ExpressionSyntax expression)
        {
            // When new string forms are added, support for them can be introduced here.  However, by checking the exact
            // types of strings supported, downstream code can know exactly what forms they should be looking for and
            // that nothing else may flow down to them.

            if (expression is LiteralExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.StringLiteralExpression,
                    Token.RawKind: (int)SyntaxKind.StringLiteralToken or
                                   (int)SyntaxKind.SingleLineRawStringLiteralToken or
                                   (int)SyntaxKind.MultiLineRawStringLiteralToken,
                })
            {
                return true;
            }

            if (expression is InterpolatedStringExpressionSyntax
                {
                    StringStartToken.RawKind: (int)SyntaxKind.InterpolatedStringStartToken or
                                              (int)SyntaxKind.InterpolatedVerbatimStringStartToken or
                                              (int)SyntaxKind.InterpolatedSingleLineRawStringStartToken or
                                              (int)SyntaxKind.InterpolatedMultiLineRawStringStartToken,
                })
            {
                return true;
            }

            return false;
        }

        public static string EscapeForNonRawStringLiteral(bool isVerbatim, string value)
        {
            // Verbatim strings are trivial.  They just need to escape `"` to `""`.
            if (isVerbatim)
                return value.Replace("\"", "\"\"");

            // Standard strings have a much larger set of cases to consider.
            using var _ = PooledStringBuilder.GetInstance(out var builder);

            // taken from object-display
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Surrogate)
                {
                    var category = CharUnicodeInfo.GetUnicodeCategory(value, i);
                    if (category == UnicodeCategory.Surrogate)
                    {
                        // an unpaired surrogate
                        builder.Append("\\u" + ((int)c).ToString("x4"));
                    }
                    else if (NeedsEscaping(category))
                    {
                        // a surrogate pair that needs to be escaped
                        var unicode = char.ConvertToUtf32(value, i);
                        builder.Append("\\U" + unicode.ToString("x8"));
                        i++; // skip the already-encoded second surrogate of the pair
                    }
                    else
                    {
                        // copy a printable surrogate pair directly
                        builder.Append(c);
                        builder.Append(value[++i]);
                    }
                }
                else if (TryReplaceChar(c, out var replaceWith))
                {
                    builder.Append(replaceWith);
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();

            static bool TryReplaceChar(char c, [NotNullWhen(true)] out string? replaceWith)
            {
                replaceWith = null;
                switch (c)
                {
                    case '\\':
                        replaceWith = "\\\\";
                        break;
                    case '\0':
                        replaceWith = "\\0";
                        break;
                    case '\a':
                        replaceWith = "\\a";
                        break;
                    case '\b':
                        replaceWith = "\\b";
                        break;
                    case '\f':
                        replaceWith = "\\f";
                        break;
                    case '\n':
                        replaceWith = "\\n";
                        break;
                    case '\r':
                        replaceWith = "\\r";
                        break;
                    case '\t':
                        replaceWith = "\\t";
                        break;
                    case '\v':
                        replaceWith = "\\v";
                        break;
                    case '"':
                        replaceWith = "\\\"";
                        break;
                }

                if (replaceWith != null)
                    return true;

                if (NeedsEscaping(CharUnicodeInfo.GetUnicodeCategory(c)))
                {
                    replaceWith = "\\u" + ((int)c).ToString("x4");
                    return true;
                }

                return false;
            }

            static bool NeedsEscaping(UnicodeCategory category)
            {
                switch (category)
                {
                    case UnicodeCategory.Control:
                    case UnicodeCategory.OtherNotAssigned:
                    case UnicodeCategory.ParagraphSeparator:
                    case UnicodeCategory.LineSeparator:
                    case UnicodeCategory.Surrogate:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Given a set of source text lines, determines what common whitespace prefix each line has.  Note that this
        /// does *not* include the first line as it's super common for someone to copy a set of lines while only
        /// starting the selection at the start of the content on the first line.  This also does not include empty
        /// lines as they're also very common, but are clearly not a way of indicating indentation indent for the normal
        /// lines.
        /// </summary>
        public static string? GetCommonIndentationPrefix(SourceText text)
        {
            string? commonIndentPrefix = null;

            for (int i = 1, n = text.Lines.Count; i < n; i++)
            {
                var line = text.Lines[i];
                var nonWhitespaceIndex = GetFirstNonWhitespaceIndex(text, line);
                if (nonWhitespaceIndex >= 0)
                    commonIndentPrefix = GetCommonIndentationPrefix(commonIndentPrefix, text, TextSpan.FromBounds(line.Start, nonWhitespaceIndex));
            }

            return commonIndentPrefix;
        }

        private static string? GetCommonIndentationPrefix(string? commonIndentPrefix, SourceText text, TextSpan lineWhitespaceSpan)
        {
            // first line with indentation whitespace we're seeing.  Just keep track of that.
            if (commonIndentPrefix == null)
                return text.ToString(lineWhitespaceSpan);

            // we have indentation whitespace from a previous line.  Figure out the max commonality between it and the
            // line we're currently looking at.
            var commonPrefixLength = 0;
            for (var n = Math.Min(commonIndentPrefix.Length, lineWhitespaceSpan.Length); commonPrefixLength < n; commonPrefixLength++)
            {
                if (commonIndentPrefix[commonPrefixLength] != text[lineWhitespaceSpan.Start + commonPrefixLength])
                    break;
            }

            return commonIndentPrefix[..commonPrefixLength];
        }

        public static TextSpan MapSpan(TextSpan span, ITextSnapshot from, ITextSnapshot to)
            => from.CreateTrackingSpan(span.ToSpan(), SpanTrackingMode.EdgeInclusive).GetSpan(to).Span.ToTextSpan();

        public static bool RawContentMustBeMultiLine(SourceText text, ImmutableArray<TextSpan> spans)
        {
            Contract.ThrowIfTrue(spans.Length == 0);

            // Empty raw string must be multiline.
            if (spans.Length == 1 && spans[0].IsEmpty)
                return true;

            // Or if it starts/ends with a quote 
            if (spans.First().Length > 0 && text[spans.First().Start] == '"')
                return true;

            if (spans.Last().Length > 0 && text[spans.Last().End - 1] == '"')
                return true;

            // or contains a newline
            foreach (var span in spans)
            {
                for (var i = span.Start; i < span.End; i++)
                {
                    if (SyntaxFacts.IsNewLine(text[i]))
                        return true;
                }
            }

            return false;
        }
    }
}
