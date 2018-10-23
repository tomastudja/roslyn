﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.SplitOrMergeIfStatements
{
    internal abstract class AbstractSplitIfStatementCodeRefactoringProvider<TExpressionSyntax> : CodeRefactoringProvider
        where TExpressionSyntax : SyntaxNode
    {
        protected abstract int GetLogicalExpressionKind(IIfStatementSyntaxService ifSyntaxService);

        protected abstract bool IsConditionOfIfStatement(SyntaxNode expression, out SyntaxNode ifStatementNode);

        protected abstract CodeAction CreateCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument, string ifKeywordText);

        protected abstract Task<SyntaxNode> GetChangedRootAsync(
            Document document,
            SyntaxNode root,
            SyntaxNode currentIfStatement,
            TExpressionSyntax left,
            TExpressionSyntax right,
            CancellationToken cancellationToken);

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var token = root.FindToken(context.Span.Start);

            if (context.Span.Length > 0 &&
                context.Span != token.Span)
            {
                return;
            }

            var ifSyntaxService = context.Document.GetLanguageService<IIfStatementSyntaxService>();
            var syntaxFacts = context.Document.GetLanguageService<ISyntaxFactsService>();

            if (IsPartOfBinaryExpressionChain(token, GetLogicalExpressionKind(ifSyntaxService), out var rootExpression) &&
                IsConditionOfIfStatement(rootExpression, out _))
            {
                context.RegisterRefactoring(
                    CreateCodeAction(
                        c => RefactorAsync(context.Document, context.Span, c),
                        syntaxFacts.GetText(ifSyntaxService.IfKeywordKind)));
            }
        }

        private async Task<Document> RefactorAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var token = root.FindToken(span.Start);

            var ifSyntaxService = document.GetLanguageService<IIfStatementSyntaxService>();

            Contract.ThrowIfFalse(IsPartOfBinaryExpressionChain(token, GetLogicalExpressionKind(ifSyntaxService), out var rootExpression));
            Contract.ThrowIfFalse(IsConditionOfIfStatement(rootExpression, out var currentIfStatement));

            var (left, right) = SplitBinaryExpressionChain(token, rootExpression, document.GetLanguageService<ISyntaxFactsService>());

            var newRoot = await GetChangedRootAsync(document, root, currentIfStatement, left, right, cancellationToken).ConfigureAwait(false);
            return document.WithSyntaxRoot(newRoot);
        }

        private static bool IsPartOfBinaryExpressionChain(SyntaxToken token, int syntaxKind, out SyntaxNode rootExpression)
        {
            // Check whether the token is part of a binary expression, and if so,
            // return the topmost binary expression in the chain (e.g. `a && b && c`).

            SyntaxNodeOrToken current = token;

            while (current.Parent?.RawKind == syntaxKind)
            {
                current = current.Parent;
            }

            rootExpression = current.AsNode();
            return current.IsNode;
        }

        private static (TExpressionSyntax left, TExpressionSyntax right) SplitBinaryExpressionChain(
            SyntaxToken token, SyntaxNode rootExpression, ISyntaxFactsService syntaxFacts)
        {
            // We have a left-associative binary expression chain, e.g. `a && b && c && d`.
            // Let's say our token is the second `&&` token, between b and c. We'd like to split the chain at this point
            // and build new expressions for the left side and the right side of this token. This will
            // effectively change the associativity from `((a && b) && c) && d` to `(a && b) && (c && d)`.
            // The left side is in the proper shape already and we can build the right side by getting the
            // topmost expression and replacing our parent with our right side. In the example: `(a && b) && c` to `c`.

            syntaxFacts.GetPartsOfBinaryExpression(token.Parent, out var parentLeft, out _, out var parentRight);

            var left = (TExpressionSyntax)parentLeft;
            var right = (TExpressionSyntax)rootExpression.ReplaceNode(token.Parent, parentRight);

            return (left, right);
        }
    }
}
