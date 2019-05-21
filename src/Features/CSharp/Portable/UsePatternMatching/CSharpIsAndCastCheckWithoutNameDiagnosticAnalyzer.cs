﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.UsePatternMatching
{
    /// <summary>
    /// DiagnosticAnalyzer that looks for is-tests and cast-expressions, and offers to convert them
    /// to use patterns.  i.e. if the user has <c>obj is TestFile &amp;&amp; ((TestFile)obj).Name == "Test"</c>
    /// it will offer to convert that <c>obj is TestFile file &amp;&amp; file.Name == "Test"</c>.
    /// 
    /// Complements <see cref="CSharpIsAndCastCheckDiagnosticAnalyzer"/> (which does the same,
    /// but only for code cases where the user has provided an appropriate variable name in
    /// code that can be used).
    /// </summary>
    //
    // disabled for preview 1 due to some perf issue. 
    // we will re-enable it once the issue is addressed.
    // https://devdiv.visualstudio.com/DevDiv/_workitems?id=504089&_a=edit&triage=true 
    // [DiagnosticAnalyzer(LanguageNames.CSharp), Shared]
    internal class CSharpIsAndCastCheckWithoutNameDiagnosticAnalyzer : AbstractBuiltInCodeStyleDiagnosticAnalyzer
    {
        private const string CS0165 = nameof(CS0165); // Use of unassigned local variable 's'
        private static readonly SyntaxAnnotation s_referenceAnnotation = new SyntaxAnnotation();

        public static readonly CSharpIsAndCastCheckWithoutNameDiagnosticAnalyzer Instance = new CSharpIsAndCastCheckWithoutNameDiagnosticAnalyzer();

        public CSharpIsAndCastCheckWithoutNameDiagnosticAnalyzer()
            : base(IDEDiagnosticIds.InlineIsTypeWithoutNameCheckDiagnosticsId,
                   new LocalizableResourceString(
                       nameof(FeaturesResources.Use_pattern_matching), FeaturesResources.ResourceManager, typeof(FeaturesResources)))
        {
        }

        public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
            => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

        protected override void InitializeWorker(AnalysisContext context)
            => context.RegisterSyntaxNodeAction(SyntaxNodeAction, SyntaxKind.IsExpression);

        private void SyntaxNodeAction(SyntaxNodeAnalysisContext context)
        {
            var cancellationToken = context.CancellationToken;
            var semanticModel = context.SemanticModel;
            var syntaxTree = semanticModel.SyntaxTree;

            // "x is Type y" is only available in C# 7.0 and above.  Don't offer this refactoring
            // in projects targetting a lesser version.
            if (((CSharpParseOptions)syntaxTree.Options).LanguageVersion < LanguageVersion.CSharp7)
            {
                return;
            }

            var root = syntaxTree.GetRoot(cancellationToken);
            var isExpression = (BinaryExpressionSyntax)context.Node;

            var workspace = (context.Options as WorkspaceAnalyzerOptions)?.Services.Workspace;
            if (workspace == null)
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

            var (matches, _) = AnalyzeExpression(workspace, semanticModel, isExpression, cancellationToken);
            if (matches == null || matches.Count == 0)
            {
                return;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    this.Descriptor, isExpression.GetLocation()));
        }

        public (HashSet<CastExpressionSyntax>, string localName) AnalyzeExpression(
            Workspace workspace,
            SemanticModel semanticModel,
            BinaryExpressionSyntax isExpression,
            CancellationToken cancellationToken)
        {
            var container = GetContainer(isExpression);
            if (container == null)
            {
                return default;
            }

            var expr = isExpression.Left.WalkDownParentheses();
            var type = (TypeSyntax)isExpression.Right;

            var typeSymbol = semanticModel.GetTypeInfo(type, cancellationToken).Type;
            if (typeSymbol == null || typeSymbol.IsNullable())
            {
                // not legal to write "(x is int? y)"
                return default;
            }

            // First, find all the potential cast locations we can replace with a reference to
            // our new local.
            var matches = new HashSet<CastExpressionSyntax>();
            AddMatches(container, expr, type, matches);

            if (matches.Count == 0)
            {
                return default;
            }

            // Find a reasonable name for the local we're going to make.  It should ideally 
            // relate to the type the user is casting to, and it should not collisde with anything
            // in scope.
            var reservedNames = semanticModel.LookupSymbols(isExpression.SpanStart)
                                             .Concat(semanticModel.GetExistingSymbols(container, cancellationToken))
                                             .Select(s => s.Name)
                                             .ToSet();

            var localName = NameGenerator.EnsureUniqueness(
                SyntaxGeneratorExtensions.GetLocalName(typeSymbol),
                reservedNames).EscapeIdentifier();

            // Now, go and actually try to make the change.  This will allow us to see all the
            // locations that we updated to see if that caused an error.
            var tempMatches = new HashSet<CastExpressionSyntax>();
            foreach (var castExpression in matches.ToArray())
            {
                tempMatches.Add(castExpression);
                var updatedSemanticModel = ReplaceMatches(
                    workspace, semanticModel, isExpression, localName, tempMatches, cancellationToken);
                tempMatches.Clear();

                var causesError = ReplacementCausesError(updatedSemanticModel, cancellationToken);
                if (causesError)
                {
                    matches.Remove(castExpression);
                }
            }

            return (matches, localName);
        }

        private bool ReplacementCausesError(
            SemanticModel updatedSemanticModel, CancellationToken cancellationToken)
        {
            var root = updatedSemanticModel.SyntaxTree.GetRoot(cancellationToken);

            var currentNode = root.GetAnnotatedNodes(s_referenceAnnotation).Single();
            var diagnostics = updatedSemanticModel.GetDiagnostics(currentNode.Span, cancellationToken);

            return diagnostics.Any(d => d.Id == CS0165);
        }

        public SemanticModel ReplaceMatches(
            Workspace workspace, SemanticModel semanticModel, BinaryExpressionSyntax isExpression,
            string localName, HashSet<CastExpressionSyntax> matches,
            CancellationToken cancellationToken)
        {
            var root = semanticModel.SyntaxTree.GetRoot(cancellationToken);
            var editor = new SyntaxEditor(root, workspace);

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
            var updatedSyntaxTree = semanticModel.SyntaxTree.WithRootAndOptions(
                changedRoot, semanticModel.SyntaxTree.Options);

            var updatedCompilation = semanticModel.Compilation.ReplaceSyntaxTree(
                semanticModel.SyntaxTree, updatedSyntaxTree);
            return updatedCompilation.GetSemanticModel(updatedSyntaxTree);
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
    }
}
