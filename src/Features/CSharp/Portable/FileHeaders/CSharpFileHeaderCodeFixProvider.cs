﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Composition;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.FileHeaders;

namespace Microsoft.CodeAnalysis.CSharp.FileHeaders
{
    /// <summary>
    /// Implements a code fix for file header diagnostics.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpFileHeaderCodeFixProvider))]
    [Shared]
    internal class CSharpFileHeaderCodeFixProvider : AbstractFileHeaderCodeFixProvider
    {
        protected override int CommentTriviaKind => (int)SyntaxKind.SingleLineCommentTrivia;
        protected override int WhitespaceTriviaKind => (int)SyntaxKind.WhitespaceTrivia;
        protected override int EndOfLineTriviaKind => (int)SyntaxKind.EndOfLineTrivia;
        protected override string CommentPrefix => "//";

        protected override SyntaxTrivia EndOfLine(string text)
            => SyntaxFactory.EndOfLine(text);

        protected override SyntaxTriviaList ParseLeadingTrivia(string text)
            => SyntaxFactory.ParseLeadingTrivia(text);

        protected override FileHeader ParseFileHeader(SyntaxNode root)
            => FileHeaderHelpers.ParseFileHeader(root);
    }
}
