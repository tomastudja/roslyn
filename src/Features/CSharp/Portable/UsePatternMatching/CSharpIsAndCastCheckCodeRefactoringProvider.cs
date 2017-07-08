﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.UsePatternMatching
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp), Shared]
    internal class CSharpIsAndCastCheckCodeRefactoringProvider : CodeRefactoringProvider
    {
        private const string CS0165 = nameof(CS0165); // Use of unassigned local variable 's'
        private static readonly SyntaxAnnotation s_referenceAnnotation = new SyntaxAnnotation();

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            if (!context.Span.IsEmpty)
            {
                return;
            }

            var document = context.Document;
            var cancellationToken = context.CancellationToken;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = root.FindNode(context.Span);
            var isExpression = node.FirstAncestorOrSelf<BinaryExpressionSyntax>(b => b.IsKind(SyntaxKind.IsExpression));
            if (isExpression == null)
            {
                return;
            }

            // See if this is an 'is' expression that would be handled by the analyzer.  If so
            // we don't need to do anything.
            if (CSharpIsAndCastCheckDiagnosticAnalyzer.Instance.TryGetPatternPieces(
                    isExpression, out _, out _, out _, out _))
            {
                return;
            }

            var container = GetContainer(isExpression);
            if (container == null)
            {
                return;
            }

            var expr = isExpression.Left.WalkDownParentheses();
            var type = (TypeSyntax)isExpression.Right;

            var typeSymbol = semanticModel.GetTypeInfo(type, cancellationToken).Type;
            if (typeSymbol == null || typeSymbol.IsNullable())
            {
                // not legal to write "(x is int? y)"
                return;
            }

            // First, find all the potential cast locations we can replace with a reference to
            // our new local.
            var matches = new HashSet<CastExpressionSyntax>();
            AddMatches(container, expr, type, matches);

            if (matches.Count == 0)
            {
                return;
            }

            // Find a reasonable name for the local we're going to make.  It should ideally 
            // relate to the type the user is casting to, and it should not collisde with anything
            // in scope.
            var reservedNames = semanticModel.LookupSymbols(isExpression.SpanStart)
                                             .Concat(GetExistingSymbols(semanticModel, container, cancellationToken))
                                             .Select(s => s.Name)
                                             .ToSet();

            var localName = NameGenerator.EnsureUniqueness(
                ICodeDefinitionFactoryExtensions.GetLocalName(typeSymbol),
                reservedNames).EscapeIdentifier();

            // Now, go and actually try to make the change.  This will allow us to see all the
            // locations that we updated to see if that caused an error.
            var tempMatches = new HashSet<CastExpressionSyntax>();
            foreach (var castExpression in matches.ToArray())
            {
                tempMatches.Add(castExpression);
                var updatedDocument = await ReplaceMatchesAsync(
                    document, root, isExpression, localName, tempMatches, cancellationToken).ConfigureAwait(false);
                tempMatches.Clear();

                var causesError = await ReplacementCausesErrorAsync(updatedDocument, cancellationToken).ConfigureAwait(false);
                if (causesError)
                {
                    matches.Remove(castExpression);
                }
            }

            if (matches.Count == 0)
            {
                return;
            }

            context.RegisterRefactoring(new MyCodeAction(
                c => ReplaceMatchesAsync(
                    document, root, isExpression, localName, matches, c)));
        }

        private static IEnumerable<ISymbol> GetExistingSymbols(
            SemanticModel semanticModel, SyntaxNode container, CancellationToken cancellationToken)
        {
            // Ignore an annonymous type property or tuple field.  It's ok if they have a name that 
            // matches the name of the local we're introducing.
            return semanticModel.GetAllDeclaredSymbols(container, cancellationToken)
                                .Where(s => !s.IsAnonymousTypeProperty() && !s.IsTupleField());
        }

        private async Task<bool> ReplacementCausesErrorAsync(
            Document updatedDocument, CancellationToken cancellationToken)
        {
            var semanticModel = await updatedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var root = await updatedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var currentNode = root.GetAnnotatedNodes(s_referenceAnnotation).Single();
            var diagnostics = semanticModel.GetDiagnostics(currentNode.Span, cancellationToken);

            return diagnostics.Any(d => d.Id == CS0165);
        }

        private Task<Document> ReplaceMatchesAsync(
            Document document, SyntaxNode root, BinaryExpressionSyntax isExpression,
            string localName, HashSet<CastExpressionSyntax> matches,
            CancellationToken cancellationToken)
        {
            var editor = new SyntaxEditor(root, document.Project.Solution.Workspace);

            // now, replace "x is Y" with "x is Y y" and put a rename-annotation on 'y' so that
            // the user can actually name the variable whatever they want.
            var newLocalName = SyntaxFactory.Identifier(localName)
                                            .WithAdditionalAnnotations(RenameAnnotation.Create());
            var isPattern = SyntaxFactory.IsPatternExpression(
                isExpression.Left, isExpression.OperatorToken,
                SyntaxFactory.DeclarationPattern((TypeSyntax)isExpression.Right.WithTrailingTrivia(SyntaxFactory.Space),
                    SyntaxFactory.SingleVariableDesignation(newLocalName))).WithTriviaFrom(isExpression);

            editor.ReplaceNode(isExpression, isPattern);

            // Now, go through all the "(Y)x" casts and replace them with just "y".
            var localReference = SyntaxFactory.IdentifierName(localName);
            foreach (var castExpression in matches)
            {
                var castRoot = castExpression.WalkUpParentheses();

                editor.ReplaceNode(
                    castRoot,
                    localReference.WithTriviaFrom(castRoot)
                                  .WithAdditionalAnnotations(s_referenceAnnotation));
            }

            var changedRoot = editor.GetChangedRoot();
            var newDocument = document.WithSyntaxRoot(changedRoot);
            return Task.FromResult(newDocument);
        }

        private SyntaxNode GetContainer(BinaryExpressionSyntax isExpression)
        {
            for (SyntaxNode current = isExpression; current != null; current = current.Parent)
            {
                switch (current)
                {
                    case StatementSyntax statement:
                        return statement;
                    case LambdaExpressionSyntax lambda:
                        return lambda.Body;
                    case ArrowExpressionClauseSyntax arrowExpression:
                        return arrowExpression.Expression;
                    case EqualsValueClauseSyntax equalsValue:
                        return equalsValue.Value;
                }
            }

            return null;
        }

        private void AddMatches(
            SyntaxNode node, ExpressionSyntax expr, TypeSyntax type, HashSet<CastExpressionSyntax> matches)
        {
            // Don't bother recursing down nodes that are before the type in the is-expressoin.
            if (node.Span.End >= type.Span.End)
            {
                if (node.IsKind(SyntaxKind.CastExpression))
                {
                    var castExpression = (CastExpressionSyntax)node;
                    if (SyntaxFactory.AreEquivalent(castExpression.Type, type) &&
                        SyntaxFactory.AreEquivalent(castExpression.Expression.WalkDownParentheses(), expr))
                    {
                        matches.Add(castExpression);
                    }
                }

                foreach (var child in node.ChildNodesAndTokens())
                {
                    if (child.IsNode)
                    {
                        AddMatches(child.AsNode(), expr, type, matches);
                    }
                }
            }
        }

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument) 
                : base(FeaturesResources.Use_pattern_matching, createChangedDocument)
            {
            }

            internal override CodeActionPriority Priority => CodeActionPriority.Low;
        }
    }
}
