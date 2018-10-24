﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.SplitOrMergeIfStatements
{
    internal abstract class AbstractMergeNestedIfStatementsCodeRefactoringProvider<TExpressionSyntax>
        : AbstractMergeIfStatementsCodeRefactoringProvider<TExpressionSyntax>
        where TExpressionSyntax : SyntaxNode
    {
        protected sealed override CodeAction CreateCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument, string ifKeywordText)
            => new MyCodeAction(createChangedDocument, ifKeywordText);

        protected sealed override async Task<bool> CanBeMergedAsync(
            Document document, SyntaxNode ifStatement, ISyntaxFactsService syntaxFacts, CancellationToken cancellationToken)
        {
            var ifSyntaxService = document.GetLanguageService<IIfStatementSyntaxService>();

            return IsFirstStatementOfIfStatement(syntaxFacts, ifSyntaxService, ifStatement, out var parentIfStatement) &&
                   await CanBeMergedWithOuterAsync(document, syntaxFacts, parentIfStatement, ifStatement, cancellationToken).ConfigureAwait(false);
        }

        protected sealed override SyntaxNode GetChangedRoot(Document document, SyntaxNode root, SyntaxNode ifStatement)
        {
            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();
            var ifSyntaxService = document.GetLanguageService<IIfStatementSyntaxService>();
            var generator = document.GetLanguageService<SyntaxGenerator>();

            Contract.ThrowIfFalse(IsFirstStatementOfIfStatement(syntaxFacts, ifSyntaxService, ifStatement, out var parentIfStatement));

            var newCondition = (TExpressionSyntax)generator.LogicalAndExpression(
                ifSyntaxService.GetConditionOfIfLikeStatement(parentIfStatement),
                ifSyntaxService.GetConditionOfIfLikeStatement(ifStatement));

            var newIfStatement = ifSyntaxService.WithStatementsOf(
                ifSyntaxService.WithCondition(parentIfStatement, newCondition),
                ifStatement);

            return root.ReplaceNode(parentIfStatement, newIfStatement.WithAdditionalAnnotations(Formatter.Annotation));
        }

        private bool IsFirstStatementOfIfStatement(
            ISyntaxFactsService syntaxFacts, IIfStatementSyntaxService ifSyntaxService, SyntaxNode statement, out SyntaxNode ifStatement)
        {
            // Check whether the statement is a first statement inside an if statement.
            // If it's inside a block, it has to be the first statement of the block.

            // This is a defensive check that should always succeed.
            if (syntaxFacts.IsStatementContainer(statement.Parent))
            {
                var statements = syntaxFacts.GetStatementContainerStatements(statement.Parent);
                if (statements.FirstOrDefault() == statement)
                {
                    var rootStatements = WalkUpBlocks(syntaxFacts, statements);
                    if (rootStatements.Count > 0 && ifSyntaxService.IsIfLikeStatement(rootStatements[0].Parent))
                    {
                        ifStatement = rootStatements[0].Parent;
                        return true;
                    }
                }
            }

            ifStatement = null;
            return false;
        }

        private async Task<bool> CanBeMergedWithOuterAsync(
            Document document,
            ISyntaxFactsService syntaxFacts,
            SyntaxNode outerIfStatement,
            SyntaxNode innerIfStatement,
            CancellationToken cancellationToken)
        {
            var ifSyntaxService = document.GetLanguageService<IIfStatementSyntaxService>();

            if (!ifSyntaxService.GetElseLikeClauses(outerIfStatement).SequenceEqual(
                    ifSyntaxService.GetElseLikeClauses(innerIfStatement), (a, b) => IsElseClauseEquivalent(syntaxFacts, ifSyntaxService, a, b)))
            {
                return false;
            }

            var statements = syntaxFacts.GetStatementContainerStatements(innerIfStatement.Parent);
            if (statements.Count == 1)
            {
                // There are no other statements below the inner if statement. Merging is OK.
                return true;
            }
            else
            {
                // There are statements below the inner if statement. We can merge if
                // 1. there are equivalent statements below the outer 'if', and
                // 2. control flow can't reach the end of these statements (otherwise, it would continue
                //    below the outer 'if' and run the same statements twice).
                // This will typically look like a single return, break, continue or a throw statement.

                // This is a defensive check that should always succeed.
                if (!syntaxFacts.IsStatementContainer(outerIfStatement.Parent))
                {
                    return false;
                }

                var outerStatements = syntaxFacts.GetStatementContainerStatements(outerIfStatement.Parent);
                var outerIfStatementIndex = outerStatements.IndexOf(outerIfStatement);

                var remainingStatements = statements.Skip(1);
                var remainingOuterStatements = outerStatements.Skip(outerIfStatementIndex + 1);

                if (!remainingStatements.SequenceEqual(remainingOuterStatements.Take(statements.Count - 1), syntaxFacts.AreEquivalent))
                {
                    return false;
                }

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                var controlFlow = semanticModel.AnalyzeControlFlow(statements.First(), statements.Last());

                return !controlFlow.EndPointIsReachable;
            }
        }

        private bool IsElseClauseEquivalent(
            ISyntaxFactsService syntaxFacts,
            IIfStatementSyntaxService ifSyntaxService,
            SyntaxNode elseClause1,
            SyntaxNode elseClause2)
        {
            // Compare Else/ElseIf clauses for equality.

            var isIfStatement = ifSyntaxService.IsIfLikeStatement(elseClause1);
            if (isIfStatement != ifSyntaxService.IsIfLikeStatement(elseClause2))
            {
                // If we have one Else and one ElseIf, they're not equal.
                return false;
            }

            if (isIfStatement)
            {
                // If we have two ElseIf blocks, their conditions have to match.
                var condition1 = ifSyntaxService.GetConditionOfIfLikeStatement(elseClause1);
                var condition2 = ifSyntaxService.GetConditionOfIfLikeStatement(elseClause2);

                if (!syntaxFacts.AreEquivalent(condition1, condition2))
                {
                    return false;
                }
            }

            var statements1 = WalkDownBlocks(syntaxFacts, syntaxFacts.GetStatementContainerStatements(elseClause1));
            var statements2 = WalkDownBlocks(syntaxFacts, syntaxFacts.GetStatementContainerStatements(elseClause2));

            return statements1.SequenceEqual(statements2, syntaxFacts.AreEquivalent);
        }

        private sealed class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument, string ifKeywordText)
                : base(string.Format(FeaturesResources.Merge_nested_0_statements, ifKeywordText), createChangedDocument)
            {
            }
        }
    }
}
