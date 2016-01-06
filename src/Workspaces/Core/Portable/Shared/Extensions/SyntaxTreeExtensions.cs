﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Shared.Extensions
{
    internal static class SyntaxTreeExtensions
    {
        public static bool IsScript(this SyntaxTree syntaxTree)
        {
            return syntaxTree.Options.Kind != SourceCodeKind.Regular;
        }

        /// <summary>
        /// Returns the identifier, keyword, contextual keyword or preprocessor keyword touching this
        /// position, or a token of Kind = None if the caret is not touching either.
        /// </summary>
        public static SyntaxToken GetTouchingWord(
            this SyntaxTree syntaxTree,
            int position,
            ISyntaxFactsService syntaxFacts,
            CancellationToken cancellationToken,
            bool findInsideTrivia = false)
        {
            return GetTouchingToken(syntaxTree, position, syntaxFacts.IsWord, cancellationToken, findInsideTrivia);
        }

        public static SyntaxToken GetTouchingToken(
            this SyntaxTree syntaxTree,
            int position,
            CancellationToken cancellationToken,
            bool findInsideTrivia = false)
        {
            return GetTouchingToken(syntaxTree, position, _ => true, cancellationToken, findInsideTrivia);
        }

        public static SyntaxToken GetTouchingToken(
            this SyntaxTree syntaxTree,
            int position,
            Predicate<SyntaxToken> predicate,
            CancellationToken cancellationToken,
            bool findInsideTrivia = false)
        {
            Contract.ThrowIfNull(syntaxTree);

            if (position >= syntaxTree.Length)
            {
                return default(SyntaxToken);
            }

            var token = syntaxTree.GetRoot(cancellationToken).FindToken(position, findInsideTrivia);

            if ((token.Span.Contains(position) || token.Span.End == position) && predicate(token))
            {
                return token;
            }

            token = token.GetPreviousToken();

            if (token.Span.End == position && predicate(token))
            {
                return token;
            }

            // SyntaxKind = None
            return default(SyntaxToken);
        }

        public static bool OverlapsHiddenPosition(this SyntaxTree tree, TextSpan span, CancellationToken cancellationToken)
        {
            if (tree == null)
            {
                return false;
            }

            var text = tree.GetText(cancellationToken);

            return text.OverlapsHiddenPosition(span, (position, cancellationToken2) =>
                {
                    // implements the ASP.Net IsHidden rule
                    var lineVisibility = tree.GetLineVisibility(position, cancellationToken2);
                    return lineVisibility == LineVisibility.Hidden || lineVisibility == LineVisibility.BeforeFirstLineDirective;
                },
                cancellationToken);
        }

        public static bool IsEntirelyHidden(this SyntaxTree tree, TextSpan span, CancellationToken cancellationToken)
        {
            if (!tree.HasHiddenRegions())
            {
                return false;
            }

            var text = tree.GetText(cancellationToken);
            var startLineNumber = text.Lines.IndexOf(span.Start);
            var endLineNumber = text.Lines.IndexOf(span.End);

            for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var linePosition = text.Lines[lineNumber].Start;
                if (!tree.IsHiddenPosition(linePosition, cancellationToken))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns <c>true</c> if the provided position is in a hidden region inaccessible to the user.
        /// </summary>
        public static bool IsHiddenPosition(this SyntaxTree tree, int position, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!tree.HasHiddenRegions())
            {
                return false;
            }

            var lineVisibility = tree.GetLineVisibility(position, cancellationToken);
            return lineVisibility == LineVisibility.Hidden || lineVisibility == LineVisibility.BeforeFirstLineDirective;
        }

        public static bool IsBeforeFirstToken(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var firstToken = syntaxTree.GetRoot(cancellationToken).GetFirstToken(includeZeroWidth: true, includeSkipped: true);

            return position <= firstToken.SpanStart;
        }

        public static SyntaxToken FindTokenOrEndToken(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            Contract.ThrowIfNull(syntaxTree);

            var root = syntaxTree.GetRoot(cancellationToken);
            var compilationUnit = root as ICompilationUnitSyntax;
            var result = root.FindToken(position, findInsideTrivia: true);
            if (result.RawKind != 0)
            {
                return result;
            }

            // Special cases.  See if we're actually at the end of a:
            // a) doc comment
            // b) pp directive
            // c) file

            var triviaList = compilationUnit.EndOfFileToken.LeadingTrivia;
            foreach (var trivia in triviaList.Reverse())
            {
                if (trivia.HasStructure)
                {
                    var token = trivia.GetStructure().GetLastToken(includeZeroWidth: true);
                    if (token.Span.End == position)
                    {
                        return token;
                    }
                }
            }

            if (position == root.FullSpan.End)
            {
                return compilationUnit.EndOfFileToken;
            }

            return default(SyntaxToken);
        }

        internal static SyntaxTrivia FindTriviaAndAdjustForEndOfFile(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken, bool findInsideTrivia = false)
        {
            var root = syntaxTree.GetRoot(cancellationToken);
            var compilationUnit = root as ICompilationUnitSyntax;
            var trivia = root.FindTrivia(position, findInsideTrivia);

            // If we ask right at the end of the file, we'll get back nothing.
            // We handle that case specially for now, though SyntaxTree.FindTrivia should
            // work at the end of a file.
            if (position == root.FullWidth())
            {
                var endOfFileToken = compilationUnit.EndOfFileToken;
                if (endOfFileToken.HasLeadingTrivia)
                {
                    trivia = endOfFileToken.LeadingTrivia.Last();
                }
                else
                {
                    var token = endOfFileToken.GetPreviousToken(includeSkipped: true);
                    if (token.HasTrailingTrivia)
                    {
                        trivia = token.TrailingTrivia.Last();
                    }
                }
            }

            return trivia;
        }
    }
}