﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.ConvertForToForEach
{
    internal abstract class AbstractConvertForToForEachCodeRefactoringProvider<
        TStatementSyntax,
        TForStatementSyntax,
        TExpressionSyntax,
        TMemberAccessExpressionSyntax,
        TTypeNode,
        TVariableDeclaratorSyntax> : CodeRefactoringProvider
        where TStatementSyntax : SyntaxNode
        where TForStatementSyntax : TStatementSyntax
        where TExpressionSyntax : SyntaxNode
        where TMemberAccessExpressionSyntax : SyntaxNode
        where TTypeNode : SyntaxNode
        where TVariableDeclaratorSyntax : SyntaxNode
    {
        protected abstract string GetTitle();

        protected abstract SyntaxList<TStatementSyntax> GetBodyStatements(TForStatementSyntax forStatement);
        protected abstract bool IsValidVariableDeclarator(TVariableDeclaratorSyntax firstVariable);

        protected abstract bool TryGetForStatementComponents(
            TForStatementSyntax forStatement,
            out SyntaxToken iterationVariable, out TExpressionSyntax initializer,
            out TMemberAccessExpressionSyntax memberAccess, out TExpressionSyntax stepValueExpressionOpt,
            CancellationToken cancellationToken);

        protected abstract SyntaxNode ConvertForNode(
            TForStatementSyntax currentFor, TTypeNode typeNode, SyntaxToken foreachIdentifier,
            TExpressionSyntax collectionExpression, ITypeSymbol iterationVariableType, OptionSet options);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var cancellationToken = context.CancellationToken;
            var document = context.Document;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var token = root.FindToken(context.Span.Start);

            // position has to be inside the 'for' span, or if there is a selection, it must
            // match the 'for' span exactly.
            if (context.Span.IsEmpty && !token.Span.IntersectsWith(context.Span.Start))
            {
                return;
            }

            if (!context.Span.IsEmpty && context.Span != token.Span)
            {
                return;
            }

            var forStatement = token.Parent.GetAncestorOrThis<TForStatementSyntax>();
            if (forStatement == null)
            {
                return;
            }

            if (forStatement.GetFirstToken() != token)
            {
                return;
            }

            if (!TryGetForStatementComponents(forStatement,
                    out var iterationVariable, out var initializer,
                    out var memberAccess, out var stepValueExpressionOpt, cancellationToken))
            {
                return;
            }

            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();
            syntaxFacts.GetPartsOfMemberAccessExpression(memberAccess,
                out var collectionExpressionNode, out var memberAccessNameNode);

            var collectionExpression = (TExpressionSyntax)collectionExpressionNode;
            syntaxFacts.GetNameAndArityOfSimpleName(memberAccessNameNode, out var memberAccessName, out _);
            if (memberAccessName != nameof(Array.Length) && memberAccessName != nameof(IList.Count))
            {
                return;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            // If the for-variable is an identifier, then make sure it's declaring a variable
            // at the for-statement, and not referencing some previously declared symbol.  i.e
            // VB allows:
            //
            //      dim i as integer
            //      for i = 0 to ...
            //
            // We can't convert this as it would change important semantics.
            // NOTE: we could potentially update this if we saw that the variable was not used
            // after the for-loop.  But, for now, we'll just be conservative and assume this means
            // the user wanted the 'i' for some other purpose and we should keep things as is.
            var iterationSymbol = semanticModel.GetSymbolInfo(iterationVariable.Parent, cancellationToken).GetAnySymbol();
            if (iterationSymbol != null)
            {
                if (iterationSymbol.Locations.Length != 1 ||
                    !iterationSymbol.Locations[0].IsInSource ||
                    iterationVariable != iterationSymbol.Locations[0].FindToken(cancellationToken))
                {
                    // was a reference to some other variable.
                    return;
                }
            }

            // Make sure we're starting at 0.
            var initializerValue = semanticModel.GetConstantValue(initializer, cancellationToken);
            if (!(initializerValue.HasValue && initializerValue.Value is 0))
            {
                return;
            }

            // Make sure we're incrementing by 1.
            if (stepValueExpressionOpt != null)
            {
                var stepValue = semanticModel.GetConstantValue(stepValueExpressionOpt);
                if (!(stepValue.HasValue && stepValue.Value is 1))
                {
                    return;
                }
            }

            var collectionType = semanticModel.GetTypeInfo(collectionExpression, cancellationToken);
            if (collectionType.Type == null && collectionType.Type.TypeKind == TypeKind.Error)
            {
                return;
            }

            var containingType = semanticModel.GetEnclosingNamedType(context.Span.Start, cancellationToken);
            if (containingType == null)
            {
                return;
            }

            var ienumerableType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T);
            var ienumeratorType = semanticModel.Compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerator_T);

            // make sure the collection can be iterated.
            if (!TryGetIterationElementType(
                    containingType, collectionType.Type,
                    ienumerableType, ienumeratorType,
                    out var iterationType))
            {
                return;
            }

            // If the user uses the iteration variable for any other reason, we can't convert this.
            var bodyStatements = GetBodyStatements(forStatement);
            foreach (var statement in bodyStatements)
            {
                if (iterationVariableIsUsedForMoreThanCollectionIndex(statement))
                {
                    return;
                }
            }

            // Looks good.  We can convert this.
            context.RegisterRefactoring(new MyCodeAction(GetTitle(),
                c => ConvertForToForEachAsync(
                    document, forStatement, iterationVariable, collectionExpression,
                    containingType, collectionType.Type, iterationType, c)));

            // local functions
            bool iterationVariableIsUsedForMoreThanCollectionIndex(SyntaxNode current)
            {
                if (syntaxFacts.IsIdentifierName(current))
                {
                    syntaxFacts.GetNameAndArityOfSimpleName(current, out var name, out _);
                    if (name == iterationVariable.ValueText)
                    {
                        // found a reference.  make sure it's only used inside something like
                        // list[i]

                        if (!syntaxFacts.IsSimpleArgument(current.Parent) ||
                            !syntaxFacts.IsElementAccessExpression(current.Parent.Parent.Parent))
                        {
                            // used in something other than accessing into a collection.
                            // can't convert this for-loop.
                            return true;
                        }

                        var expr = syntaxFacts.GetExpressionOfElementAccessExpression(current.Parent.Parent.Parent);
                        if (!syntaxFacts.AreEquivalent(expr, collectionExpression))
                        {
                            // was indexing into something other than the collection.
                            // can't convert this for-loop.
                            return true;
                        }

                        // this usage of the for-variable is fine.
                    }
                }

                foreach (var child in current.ChildNodesAndTokens())
                {
                    if (child.IsNode)
                    {
                        if (iterationVariableIsUsedForMoreThanCollectionIndex(child.AsNode()))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private IEnumerable<TSymbol> TryFindMembersInThisOrBaseTypes<TSymbol>(
            INamedTypeSymbol containingType, ITypeSymbol type, string memberName) where TSymbol : class, ISymbol
        {
            var methods = type.GetAccessibleMembersInThisAndBaseTypes<TSymbol>(containingType);
            return methods.Where(m => m.Name == memberName);
        }

        private TSymbol TryFindMemberInThisOrBaseTypes<TSymbol>(
            INamedTypeSymbol containingType, ITypeSymbol type, string memberName) where TSymbol : class, ISymbol
        {
            return TryFindMembersInThisOrBaseTypes<TSymbol>(containingType, type, memberName).FirstOrDefault();
        }

        private bool TryGetIterationElementType(
            INamedTypeSymbol containingType, ITypeSymbol collectionType, 
            INamedTypeSymbol ienumerableType, INamedTypeSymbol ienumeratorType,
            out ITypeSymbol iterationType)
        {
            if (collectionType is IArrayTypeSymbol arrayType)
            {
                iterationType = arrayType.ElementType;
                return true;
            }

            // Check in the class/struct hierarchy first.
            var getEnumeratorMethod = TryFindMemberInThisOrBaseTypes<IMethodSymbol>(
                containingType, collectionType, WellKnownMemberNames.GetEnumeratorMethodName);
            if (getEnumeratorMethod != null)
            {
                return TryGetIterationElementTypeFromGetEnumerator(
                    containingType, getEnumeratorMethod, ienumeratorType, out iterationType);
            }

            // couldn't find .GetEnumerator on the class/struct.  Check the interface hierarchy.
            var instantiatedIEnumerableType = collectionType.GetAllInterfacesIncludingThis().FirstOrDefault(
                t => Equals(t.OriginalDefinition, ienumerableType));

            if (instantiatedIEnumerableType != null)
            {
                iterationType = instantiatedIEnumerableType.TypeArguments[0];
                return true;
            }

            iterationType = default;
            return false;
        }

        private bool TryGetIterationElementTypeFromGetEnumerator(
            INamedTypeSymbol containingType, IMethodSymbol getEnumeratorMethod, 
            INamedTypeSymbol ienumeratorType, out ITypeSymbol iterationType)
        {
            var getEnumeratorReturnType = getEnumeratorMethod.ReturnType;

            // Check in the class/struct hierarchy first.
            var currentProperty = TryFindMemberInThisOrBaseTypes<IPropertySymbol>(
                containingType, getEnumeratorReturnType, WellKnownMemberNames.CurrentPropertyName);
            if (currentProperty != null)
            {
                iterationType = currentProperty.Type;
                return true;
            }

            // couldn't find .Current on the class/struct.  Check the interface hierarchy.
            var instantiatedIEnumeratorType = getEnumeratorReturnType.GetAllInterfacesIncludingThis().FirstOrDefault(
                t => Equals(t.OriginalDefinition, ienumeratorType));

            if (instantiatedIEnumeratorType != null)
            {
                iterationType = instantiatedIEnumeratorType.TypeArguments[0];
                return true;
            }

            iterationType = default;
            return false;
        }

        private async Task<Document> ConvertForToForEachAsync(
            Document document, TForStatementSyntax forStatement,
            SyntaxToken iterationVariable, TExpressionSyntax collectionExpression,
            INamedTypeSymbol containingType, ITypeSymbol collectionType,
            ITypeSymbol iterationType, CancellationToken cancellationToken)
        {
            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();
            var semanticFacts = document.GetLanguageService<ISemanticFactsService>();
            var generator = SyntaxGenerator.GetGenerator(document);

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = new SyntaxEditor(root, generator);

            // create a dummy "list[i]" expression.  We'll use this to find all places to replace
            // in the current for statement.
            var indexExpression = generator.ElementAccessExpression(
                collectionExpression, generator.IdentifierName(iterationVariable));

            // See if the first statement in the for loop is of the form:
            //      var x = list[i]   or
            //
            // If so, we'll use those as the iteration variables for the new foreach statement.
            var bodyStatements = GetBodyStatements(forStatement);
            var (typeNode, foreachIdentifier, declarationStatement) = tryDeconstructInitialDeclaration();

            if (typeNode == null)
            {
                // user didn't provide an explicit type.  Check if the index-type of the collection
                // is different from than .Current type of the enumerator.  If so, add an explicit
                // type so that the foreach will coerce the types accordingly.
                var indexerType = GetIndexerType(containingType, collectionType);
                if (!Equals(indexerType, iterationType))
                {
                    typeNode = (TTypeNode)generator.TypeExpression(
                        indexerType ?? semanticModel.Compilation.GetSpecialType(SpecialType.System_Object));
                }
            }

            // If we couldn't find an appropriate existing variable to use as the foreach
            // variable, then generate one automatically.
            if (foreachIdentifier.RawKind == 0)
            {
                foreachIdentifier = semanticFacts.GenerateUniqueName(
                    semanticModel, forStatement, containerOpt: null, baseName: "v", cancellationToken);
                foreachIdentifier = foreachIdentifier.WithAdditionalAnnotations(RenameAnnotation.Create());
            }

            // Create the expression we'll use to replace all matches in the for-body.
            var foreachIdentifierReference = foreachIdentifier.WithoutAnnotations(RenameAnnotation.Kind).WithoutTrivia();

            // Walk the for statement, replacing any matches we find.
            findAndReplaceMatches(forStatement);

            // Finally, remove the declaration statement if we found one.  Move all its leading
            // trivia to the next statement.
            if (declarationStatement != null)
            {
                editor.RemoveNode(declarationStatement,
                    SyntaxGenerator.DefaultRemoveOptions | SyntaxRemoveOptions.KeepLeadingTrivia);
            }

            var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
            editor.ReplaceNode(
                forStatement,
                (currentFor, _) => this.ConvertForNode(
                    (TForStatementSyntax)currentFor, typeNode, foreachIdentifier,
                    collectionExpression, iterationType, options));

            return document.WithSyntaxRoot(editor.GetChangedRoot());

            // local functions
            (TTypeNode, SyntaxToken, TStatementSyntax) tryDeconstructInitialDeclaration()
            {
                if (bodyStatements.Count >= 1)
                {
                    var firstStatement = bodyStatements[0];
                    if (syntaxFacts.IsLocalDeclarationStatement(firstStatement))
                    {
                        var variables = syntaxFacts.GetVariablesOfLocalDeclarationStatement(firstStatement);
                        if (variables.Count == 1)
                        {
                            var firstVariable = (TVariableDeclaratorSyntax)variables[0];
                            if (IsValidVariableDeclarator(firstVariable))
                            {
                                var firstVariableInitializer = syntaxFacts.GetValueOfEqualsValueClause(
                                    syntaxFacts.GetInitializerOfVariableDeclarator(firstVariable));
                                if (syntaxFacts.AreEquivalent(firstVariableInitializer, indexExpression))
                                {
                                    var type = (TTypeNode)syntaxFacts.GetTypeOfVariableDeclarator(firstVariable)?.WithoutLeadingTrivia();
                                    var identifier = syntaxFacts.GetIdentifierOfVariableDeclarator(firstVariable);
                                    var statement = firstStatement;
                                    return (type, identifier, statement);
                                }
                            }
                        }
                    }
                }

                return default;
            }

            void findAndReplaceMatches(SyntaxNode current)
            {
                if (syntaxFacts.AreEquivalent(current, indexExpression))
                {
                    // Found a match.  replace with iteration variable.
                    var replacementToken = foreachIdentifierReference;

                    if (semanticFacts.IsWrittenTo(semanticModel, current, cancellationToken))
                    {
                        replacementToken = replacementToken.WithAdditionalAnnotations(
                            WarningAnnotation.Create(FeaturesResources.Warning_colon_Collection_was_modified_during_iteration));
                    }

                    if (crossesFunctionBoundary(current))
                    {
                        replacementToken = replacementToken.WithAdditionalAnnotations(
                            WarningAnnotation.Create(FeaturesResources.Warning_colon_Iteration_variable_crossed_function_boundary));
                    }

                    var replacement = generator.IdentifierName(replacementToken).WithTriviaFrom(current);

                    editor.ReplaceNode(current, replacement);
                }

                foreach (var child in current.ChildNodesAndTokens())
                {
                    if (child.IsNode)
                    {
                        findAndReplaceMatches(child.AsNode());
                    }
                }
            }

            bool crossesFunctionBoundary(SyntaxNode node)
            {
                var containingFunction = node.AncestorsAndSelf().FirstOrDefault(
                    n => syntaxFacts.IsLocalFunctionStatement(n) || syntaxFacts.IsAnonymousFunction(n));

                if (containingFunction == null)
                {
                    return false;
                }

                return containingFunction.AncestorsAndSelf().Contains(forStatement);
            }
        }

        private static ITypeSymbol GetIndexerType(INamedTypeSymbol containingType, ITypeSymbol collectionType)
        {
            if (collectionType is IArrayTypeSymbol arrayType)
            {
                return arrayType.ElementType;
            }

            var indexer =
                collectionType.GetAccessibleMembersInThisAndBaseTypes<IPropertySymbol>(containingType)
                              .Where(IsViableIndexer)
                              .FirstOrDefault();

            if (indexer?.Type != null)
            {
                return indexer.Type;
            }

            if (collectionType.IsInterfaceType())
            {
                var interfaces = collectionType.GetAllInterfacesIncludingThis();
                indexer = interfaces.SelectMany(i => i.GetMembers().OfType<IPropertySymbol>().Where(IsViableIndexer))
                                    .FirstOrDefault();

                return indexer?.Type;
            }

            return null;
        }

        private static bool IsViableIndexer(IPropertySymbol property)
            => property.IsIndexer &&
               property.Parameters.Length == 1 &&
               property.Parameters[0].Type?.SpecialType == SpecialType.System_Int32;

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument) 
                : base(title, createChangedDocument, title)
            {
            }
        }
    }
}
