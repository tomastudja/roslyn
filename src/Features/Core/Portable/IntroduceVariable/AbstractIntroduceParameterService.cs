﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.AddParameter;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using static Microsoft.CodeAnalysis.CodeActions.CodeAction;

#nullable disable

namespace Microsoft.CodeAnalysis.IntroduceVariable
{
    internal abstract partial class AbstractIntroduceParameterService<TService, TExpressionSyntax, TMethodDeclarationSyntax> : CodeRefactoringProvider
        where TService : AbstractIntroduceParameterService<TService, TExpressionSyntax, TMethodDeclarationSyntax>
        where TExpressionSyntax : SyntaxNode
        where TMethodDeclarationSyntax : SyntaxNode
    {
        protected const string ExpressionAnnotationKind = nameof(ExpressionAnnotationKind);
        protected abstract Task<Solution> IntroduceParameterAsync(SemanticDocument document, TExpressionSyntax expression, bool allOccurrences, bool trampoline, CancellationToken cancellationToken);
        protected abstract bool ExpressionWithinParameterizedMethod(TExpressionSyntax expression);

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var (document, textSpan, cancellationToken) = context;
            if (document.Project.Solution.Workspace.Kind == WorkspaceKind.MiscellaneousFiles)
            {
                return;
            }

            var action = await IntroduceParameterAsync(document, textSpan, cancellationToken).ConfigureAwait(false);
            if (action != null)
            {
                context.RegisterRefactoring(action, textSpan);
            }
        }

        public async Task<CodeAction> IntroduceParameterAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var semanticDocument = await SemanticDocument.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();

            var expression = await document.TryGetRelevantNodeAsync<TExpressionSyntax>(textSpan, cancellationToken).ConfigureAwait(false);
            if (expression == null || CodeRefactoringHelpers.IsNodeUnderselected(expression, textSpan))
            {
                return null;
            }

            var expressionType = semanticDocument.SemanticModel.GetTypeInfo(expression, cancellationToken).Type;
            if (expressionType is IErrorTypeSymbol)
            {
                return null;
            }

            var containingType = expression.AncestorsAndSelf()
                .Select(n => semanticDocument.SemanticModel.GetDeclaredSymbol(n, cancellationToken))
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault();

            containingType ??= semanticDocument.SemanticModel.Compilation.ScriptClass;

            if (containingType == null || containingType.TypeKind == TypeKind.Interface)
            {
                return null;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (expression != null)
            {
                var (title, actions) = AddActions(semanticDocument, expression);

                if (actions.Length > 0)
                {
                    return new CodeActionWithNestedActions(title, actions, isInlinable: true);
                }
            }
            return null;

        }

        protected static Task<Solution> AddParameterToMethodHeaderAsync(SemanticDocument document, TExpressionSyntax expression, string parameterName, CancellationToken cancellationToken)
        {
            var invocationDocument = document.Document;
            var methodExpression = expression.FirstAncestorOrSelf<TMethodDeclarationSyntax>(node => node is TMethodDeclarationSyntax, true);
            var semanticModel = document.SemanticModel;
            var symbolInfo = (IMethodSymbol)semanticModel.GetDeclaredSymbol(methodExpression, cancellationToken);
            var syntaxFacts = invocationDocument.GetRequiredLanguageService<ISyntaxFactsService>();
            var parameterType = semanticModel.GetTypeInfo(expression, cancellationToken).Type ?? document.SemanticModel.Compilation.ObjectType;
            var refKind = syntaxFacts.GetRefKindOfArgument(expression);

            return AddParameterService.Instance.AddParameterAsync(
                invocationDocument,
                symbolInfo,
                parameterType,
                refKind,
                parameterName,
                null,
                false,
                cancellationToken,
                true);
        }

        protected static string GetNewParameterName(SemanticDocument document, TExpressionSyntax expression, CancellationToken cancellationToken)
        {
            var invocationDocument = document.Document;
            var semanticModel = document.SemanticModel;
            var semanticFacts = invocationDocument.GetRequiredLanguageService<ISemanticFactsService>();
            return semanticFacts.GenerateNameForExpression(
                    semanticModel, expression, capitalize: false, cancellationToken: cancellationToken);
        }

        protected static IMethodSymbol GetMethodSymbolFromExpression(SemanticDocument annotatedSemanticDocument, TExpressionSyntax annotatedExpression, CancellationToken cancellationToken)
        {
            var methodExpression = annotatedExpression.FirstAncestorOrSelf<TMethodDeclarationSyntax>(node => node is TMethodDeclarationSyntax, true);
            return (IMethodSymbol)annotatedSemanticDocument.SemanticModel.GetDeclaredSymbol(methodExpression, cancellationToken);
        }

        protected static async Task<SemanticDocument> GetAnnotatedSemanticDocumentAsync(SemanticDocument document, SyntaxAnnotation annotatedExpression,
            TExpressionSyntax expression, CancellationToken cancellationToken)
        {
            var expressionWithAnnotation = expression.WithAdditionalAnnotations(annotatedExpression);
            var newDocument = document.Document.WithSyntaxRoot(document.Root.ReplaceNode(expression, expressionWithAnnotation));
            return await SemanticDocument.CreateAsync(newDocument, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Rewrites the expression with the new parameter
        /// </summary>
        /// <param name="document"> The document after changing the method header </param>
        /// <param name="newExpression"> The annotated expression </param>
        /// <param name="parameterName"> The parameter name that was added to the method header </param>
        /// <param name="allOccurrences"> Checks if we want to change all occurrences of the expression or just the original expression </param>
        /// <returns></returns>
        protected Document ConvertExpressionWithNewParameter(
            SemanticDocument document,
            TExpressionSyntax newExpression,
            string parameterName,
            bool allOccurrences,
            CancellationToken cancellationToken)
        {
            var generator = SyntaxGenerator.GetGenerator(document.Document);
            var parameterNameSyntax = (TExpressionSyntax)generator.IdentifierName(parameterName);
            var block = (TMethodDeclarationSyntax)newExpression.Ancestors().FirstOrDefault(s => s is TMethodDeclarationSyntax);
            SyntaxNode scope = block;

            var matches = FindMatches(document, newExpression, document, scope, allOccurrences, cancellationToken);
            SyntaxNode innermostCommonBlock = null;

            if (matches.Count > 1)
            {
                innermostCommonBlock = matches.FindInnermostCommonNode();
            }
            else
            {
                innermostCommonBlock = matches.Single().Parent;
            }

            var newExpressionCopy = newExpression;

            var newInnerMostBlock = Rewrite(
                document, newExpression, parameterNameSyntax, document, innermostCommonBlock, allOccurrences, cancellationToken);
            var newRoot = document.Root.ReplaceNode(innermostCommonBlock, newInnerMostBlock);
            return document.Document.WithSyntaxRoot(newRoot);
        }

        private (string title, ImmutableArray<CodeAction> actions) AddActions(SemanticDocument semanticDocument, TExpressionSyntax expression)
        {
            var actionsBuilder = new ArrayBuilder<CodeAction>();
            if (ExpressionWithinParameterizedMethod(expression))
            {
                actionsBuilder.Add(new IntroduceParameterCodeAction(semanticDocument, (TService)this, expression, false, false));
                actionsBuilder.Add(new IntroduceParameterCodeAction(semanticDocument, (TService)this, expression, false, true));

                actionsBuilder.Add(new IntroduceParameterCodeAction(semanticDocument, (TService)this, expression, true, false));
                actionsBuilder.Add(new IntroduceParameterCodeAction(semanticDocument, (TService)this, expression, true, true));
            }

            return (FeaturesResources.Introduce_parameter, actionsBuilder.ToImmutable());
        }

        protected static ISet<TExpressionSyntax> FindMatches(
           SemanticDocument originalDocument,
           TExpressionSyntax expressionInOriginal,
           SemanticDocument currentDocument,
           SyntaxNode withinNodeInCurrent,
           bool allOccurrences,
           CancellationToken cancellationToken)
        {
            var syntaxFacts = currentDocument.Project.LanguageServices.GetService<ISyntaxFactsService>();
            var originalSemanticModel = originalDocument.SemanticModel;
            var currentSemanticModel = currentDocument.SemanticModel;

            var result = new HashSet<TExpressionSyntax>();
            var matches = from nodeInCurrent in withinNodeInCurrent.DescendantNodesAndSelf().OfType<TExpressionSyntax>()
                          where NodeMatchesExpression(originalSemanticModel, currentSemanticModel, expressionInOriginal, nodeInCurrent, allOccurrences, cancellationToken)
                          select nodeInCurrent;
            result.AddRange(matches.OfType<TExpressionSyntax>());

            return result;
        }

        private static bool NodeMatchesExpression(
            SemanticModel originalSemanticModel,
            SemanticModel currentSemanticModel,
            TExpressionSyntax expressionInOriginal,
            TExpressionSyntax nodeInCurrent,
            bool allOccurrences,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nodeInCurrent == expressionInOriginal)
            {
                return true;
            }

            if (allOccurrences)
            {
                // Original expression and current node being semantically equivalent isn't enough when the original expression 
                // is a member access via instance reference (either implicit or explicit), the check only ensures that the expression
                // and current node are both backed by the same member symbol. So in this case, in addition to SemanticEquivalence check, 
                // we also check if expression and current node are both instance member access.
                //
                // For example, even though the first `c` binds to a field and we are introducing a local for it,
                // we don't want other references to that field to be replaced as well (i.e. the second `c` in the expression).
                //
                //  class C
                //  {
                //      C c;
                //      void Test()
                //      {
                //          var x = [|c|].c;
                //      }
                //  }

                if (SemanticEquivalence.AreEquivalent(
                    originalSemanticModel, currentSemanticModel, expressionInOriginal, nodeInCurrent))
                {
                    var originalOperation = originalSemanticModel.GetOperation(expressionInOriginal, cancellationToken);
                    if (IsInstanceMemberReference(originalOperation))
                    {
                        var currentOperation = currentSemanticModel.GetOperation(nodeInCurrent, cancellationToken);
                        return IsInstanceMemberReference(currentOperation);
                    }

                    return true;
                }
            }

            return false;
            static bool IsInstanceMemberReference(IOperation operation)
                => operation is IMemberReferenceOperation memberReferenceOperation &&
                    memberReferenceOperation.Instance?.Kind == OperationKind.InstanceReference;
        }

        protected TNode Rewrite<TNode>(
            SemanticDocument originalDocument,
            TExpressionSyntax expressionInOriginal,
            TExpressionSyntax variableName,
            SemanticDocument currentDocument,
            TNode withinNodeInCurrent,
            bool allOccurrences,
            CancellationToken cancellationToken)
            where TNode : SyntaxNode
        {
            var generator = SyntaxGenerator.GetGenerator(originalDocument.Document);
            var matches = FindMatches(originalDocument, expressionInOriginal, currentDocument, withinNodeInCurrent, allOccurrences, cancellationToken);

            // Parenthesize the variable, and go and replace anything we find with it.
            // NOTE: we do not want elastic trivia as we want to just replace the existing code 
            // as is, while preserving the trivia there.  We do not want to update it.
            var replacement = generator.AddParentheses(variableName, includeElasticTrivia: false)
                                         .WithAdditionalAnnotations(Formatter.Annotation);

            return RewriteCore(withinNodeInCurrent, replacement, matches);
        }

        protected abstract TNode RewriteCore<TNode>(
            TNode node,
            SyntaxNode replacementNode,
            ISet<TExpressionSyntax> matches)
            where TNode : SyntaxNode;

        private class IntroduceParameterCodeAction : AbstractIntroduceParameterCodeAction
        {
            internal IntroduceParameterCodeAction(
                SemanticDocument document,
                TService service,
                TExpressionSyntax expression,
                bool allOccurrences,
                bool trampoline)
                : base(document, service, expression, allOccurrences, trampoline)
            { }
        }
    }
}
