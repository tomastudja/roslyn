﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;
using static Microsoft.CodeAnalysis.CodeActions.CodeAction;

namespace Microsoft.CodeAnalysis.IntroduceVariable
{
    internal abstract partial class AbstractIntroduceParameterService<
        TExpressionSyntax,
        TInvocationExpressionSyntax,
        TObjectCreationExpressionSyntax,
        TIdentifierNameSyntax> : CodeRefactoringProvider
        where TExpressionSyntax : SyntaxNode
        where TInvocationExpressionSyntax : TExpressionSyntax
        where TObjectCreationExpressionSyntax : TExpressionSyntax
        where TIdentifierNameSyntax : TExpressionSyntax
    {
        protected abstract SyntaxNode GenerateExpressionFromOptionalParameter(IParameterSymbol parameterSymbol);
        protected abstract SyntaxNode? GetLocalDeclarationFromDeclarator(SyntaxNode variableDecl);
        protected abstract SyntaxNode UpdateArgumentListSyntax(SyntaxNode argumentList, SeparatedSyntaxList<SyntaxNode> arguments);

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var (document, textSpan, cancellationToken) = context;
            if (document.Project.Solution.Workspace.Kind == WorkspaceKind.MiscellaneousFiles)
            {
                return;
            }

            var expression = await document.TryGetRelevantNodeAsync<TExpressionSyntax>(textSpan, cancellationToken).ConfigureAwait(false);
            if (expression == null || CodeRefactoringHelpers.IsNodeUnderselected(expression, textSpan))
            {
                return;
            }

            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var expressionType = semanticModel.GetTypeInfo(expression, cancellationToken).Type;
            if (expressionType is null or IErrorTypeSymbol)
            {
                return;
            }

            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();

            // Need to special case for expressions that are contained within a parameter
            // because it is technically "contained" within a method, but an expression in a parameter does not make
            // sense to introduce.
            var parameterNode = expression.FirstAncestorOrSelf<SyntaxNode>(node => syntaxFacts.IsParameter(node));
            if (parameterNode is not null)
            {
                return;
            }

            var generator = SyntaxGenerator.GetGenerator(document);
            var containingMethod = expression.FirstAncestorOrSelf<SyntaxNode>(node => generator.GetParameterListNode(node) is not null);

            if (containingMethod is null)
            {
                return;
            }

            var containingSymbol = semanticModel.GetDeclaredSymbol(containingMethod, cancellationToken);
            if (containingSymbol is not IMethodSymbol methodSymbol)
            {
                return;
            }

            // Code actions for trampoline and overloads will not be offered if the method is a constructor.
            // Code actions for overloads will not be offered if the method if the method is a local function.
            var methodKind = methodSymbol.MethodKind;
            if (methodKind is not (MethodKind.Ordinary or MethodKind.LocalFunction or MethodKind.Constructor))
            {
                return;
            }

            if (methodSymbol.Language.Equals("Visual Basic") && methodSymbol.Name.Equals("Finalize") && methodSymbol.IsOverride)
            {
                return;
            }

            var actions = await GetActionsAsync(document, expression, methodSymbol, containingMethod, cancellationToken).ConfigureAwait(false);

            if (actions is null)
            {
                return;
            }

            var singleLineExpression = syntaxFacts.ConvertToSingleLine(expression);
            var nodeString = singleLineExpression.ToString();

            context.RegisterRefactoring(new CodeActionWithNestedActions(
                string.Format(FeaturesResources.Introduce_parameter_for_0, nodeString), actions.Value.actions, isInlinable: false), textSpan);
            context.RegisterRefactoring(new CodeActionWithNestedActions(
                string.Format(FeaturesResources.Introduce_parameter_for_all_occurrences_of_0, nodeString), actions.Value.actionsAllOccurrences, isInlinable: false), textSpan);
        }

        /// <summary>
        /// Determines if the expression is something that should have code actions displayed for it.
        /// Depends upon the identifiers in the expression mapping back to parameters.
        /// Does not handle params parameters.
        /// </summary>
        private static async Task<(bool shouldDisplay, bool containsClassExpression)> ShouldExpressionDisplayCodeActionAsync(Document document, TExpressionSyntax expression, CancellationToken cancellationToken)
        {
            var variablesInExpression = expression.DescendantNodes();
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var containsClassSpecificStatement = false;
            var syntaxKinds = document.GetRequiredLanguageService<ISyntaxKindsService>();

            foreach (var variable in variablesInExpression)
            {
                var symbol = semanticModel.GetSymbolInfo(variable, cancellationToken).Symbol;
                if (symbol is IRangeVariableSymbol or ILocalSymbol)
                {
                    return (false, false);
                }

                if (symbol is IParameterSymbol parameter)
                {
                    if (parameter.IsParams)
                    {
                        return (false, false);
                    }
                }

                if (variable.RawKind == syntaxKinds.ThisExpression || variable.RawKind == syntaxKinds.BaseExpression)
                {
                    containsClassSpecificStatement = true;
                }
            }

            return (true, containsClassSpecificStatement);
        }

        /// <summary>
        /// Creates new code actions for each introduce parameter possibility.
        /// Does not create actions for overloads/trampoline if there are optional parameters or if the methodSymbol
        /// is a constructor.
        /// </summary>
        private async Task<(ImmutableArray<CodeAction> actions, ImmutableArray<CodeAction> actionsAllOccurrences)?> GetActionsAsync(Document document,
            TExpressionSyntax expression, IMethodSymbol methodSymbol, SyntaxNode containingMethod,
            CancellationToken cancellationToken)
        {
            var (shouldDisplay, containsClassExpression) = await ShouldExpressionDisplayCodeActionAsync(
                document, expression, cancellationToken).ConfigureAwait(false);
            if (!shouldDisplay)
            {
                return null;
            }

            using var actionsBuilder = TemporaryArray<CodeAction>.Empty;
            using var actionsBuilderAllOccurrences = TemporaryArray<CodeAction>.Empty;
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();
            var singleLineExpression = syntaxFacts.ConvertToSingleLine(expression);
            var nodeString = singleLineExpression.ToString();

            if (!containsClassExpression)
            {
                actionsBuilder.Add(CreateNewCodeAction(FeaturesResources.and_update_call_sites_directly, allOccurrences: false, SelectedCodeAction.Refactor));
                actionsBuilderAllOccurrences.Add(CreateNewCodeAction(FeaturesResources.and_update_call_sites_directly, allOccurrences: true, SelectedCodeAction.Refactor));
            }

            if (methodSymbol.MethodKind is not MethodKind.Constructor)
            {
                actionsBuilder.Add(CreateNewCodeAction(
                    FeaturesResources.into_extracted_method_to_invoke_at_call_sites, allOccurrences: false, SelectedCodeAction.Trampoline));
                actionsBuilderAllOccurrences.Add(CreateNewCodeAction(
                    FeaturesResources.into_extracted_method_to_invoke_at_call_sites, allOccurrences: true, SelectedCodeAction.Trampoline));

                if (methodSymbol.MethodKind is not MethodKind.LocalFunction)
                {
                    actionsBuilder.Add(CreateNewCodeAction(
                        string.Format(FeaturesResources.into_new_overload_of_0, nodeString), allOccurrences: false, SelectedCodeAction.Overload));
                    actionsBuilderAllOccurrences.Add(CreateNewCodeAction(
                        string.Format(FeaturesResources.into_new_overload_of_0, nodeString), allOccurrences: true, SelectedCodeAction.Overload));
                }
            }

            return (actionsBuilder.ToImmutableAndClear(), actionsBuilderAllOccurrences.ToImmutableAndClear());

            // Local function to create a code action with more ease
            MyCodeAction CreateNewCodeAction(string actionName, bool allOccurrences, SelectedCodeAction selectedCodeAction)
            {
                return new MyCodeAction(actionName, c => IntroduceParameterAsync(
                    document, expression, methodSymbol, containingMethod, allOccurrences, selectedCodeAction, c));
            }
        }

        /// <summary>
        /// Introduces a new parameter and refactors all the call sites based on the selected code action.
        /// </summary>
        private async Task<Solution> IntroduceParameterAsync(Document originalDocument, TExpressionSyntax expression,
            IMethodSymbol methodSymbol, SyntaxNode containingMethod, bool allOccurrences, SelectedCodeAction selectedCodeAction,
            CancellationToken cancellationToken)
        {
            var parameterName = await GetNewParameterNameAsync(originalDocument, expression, cancellationToken).ConfigureAwait(false);

            var methodCallSites = await FindCallSitesAsync(originalDocument, methodSymbol, cancellationToken).ConfigureAwait(false);

            var modifiedSolution = originalDocument.Project.Solution;
            var mappingDictionary = await MapExpressionToParametersAsync(originalDocument, expression, cancellationToken).ConfigureAwait(false);
            var syntaxFacts = originalDocument.GetRequiredLanguageService<ISyntaxFactsService>();

            foreach (var (project, projectCallSites) in methodCallSites.GroupBy(kvp => kvp.Key.Project))
            {
                var compilation = await project.GetRequiredCompilationAsync(cancellationToken).ConfigureAwait(false);
                foreach (var (document, invocations) in projectCallSites)
                {
                    var insertionIndex = GetInsertionIndex(compilation, methodSymbol, syntaxFacts, containingMethod);
                    if (selectedCodeAction is SelectedCodeAction.Trampoline or SelectedCodeAction.Overload)
                    {
                        var newRoot = await ModifyDocumentInvocationsTrampolineOverloadAndIntroduceParameterAsync(compilation,
                            document, originalDocument, invocations, mappingDictionary, methodSymbol, containingMethod,
                            insertionIndex, allOccurrences, parameterName, expression, selectedCodeAction, cancellationToken).ConfigureAwait(false);
                        modifiedSolution = modifiedSolution.WithDocumentSyntaxRoot(originalDocument.Id, newRoot);
                    }
                    else
                    {
                        var newRoot = await ModifyDocumentInvocationsAndIntroduceParameterAsync(compilation,
                            originalDocument, document, mappingDictionary, containingMethod,
                            expression, allOccurrences, parameterName, insertionIndex, invocations,
                            cancellationToken).ConfigureAwait(false);
                        modifiedSolution = modifiedSolution.WithDocumentSyntaxRoot(originalDocument.Id, newRoot);
                    }
                }
            }

            return modifiedSolution;
        }

        /// <summary>
        /// Gets the parameter name, if the expression's grandparent is a variable declarator then it just gets the
        /// local declarations name. Otherwise, it generates a name based on the context of the expression.
        /// </summary>
        private async Task<string> GetNewParameterNameAsync(Document document, TExpressionSyntax expression, CancellationToken cancellationToken)
        {
            if (ShouldRemoveVariableDeclaratorContainingExpression(document, expression, out var varDeclName, out _))
            {
                return varDeclName;
            }

            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var semanticFacts = document.GetRequiredLanguageService<ISemanticFactsService>();
            return semanticFacts.GenerateNameForExpression(semanticModel, expression, capitalize: false, cancellationToken);
        }

        /// <summary>
        /// Determines if the expression's grandparent is a variable declarator and if so,
        /// returns the name
        /// </summary>
        protected bool ShouldRemoveVariableDeclaratorContainingExpression(
            Document document, TExpressionSyntax expression, [NotNullWhen(true)] out string? varDeclName, [NotNullWhen(true)] out SyntaxNode? localDeclaration)
        {
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();
            var declarator = expression?.Parent?.Parent;

            localDeclaration = null;

            if (!syntaxFacts.IsVariableDeclarator(declarator))
            {
                varDeclName = null;
                return false;
            }

            localDeclaration = GetLocalDeclarationFromDeclarator(declarator);
            if (localDeclaration is null)
            {
                varDeclName = null;
                return false;
            }

            // TODO: handle in the future
            if (syntaxFacts.GetVariablesOfLocalDeclarationStatement(localDeclaration).Count > 1)
            {
                varDeclName = null;
                localDeclaration = null;
                return false;
            }

            varDeclName = syntaxFacts.GetIdentifierOfVariableDeclarator(declarator).ValueText;
            return true;
        }

        /// <summary>
        /// Locates all the call sites of the method that introduced the parameter
        /// </summary>
        protected static async Task<Dictionary<Document, List<SyntaxNode>>> FindCallSitesAsync(
            Document document, IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            var methodCallSites = new Dictionary<Document, List<SyntaxNode>>();
            var progress = new StreamingProgressCollector();
            await SymbolFinder.FindReferencesAsync(
                methodSymbol, document.Project.Solution, progress,
                documents: null, FindReferencesSearchOptions.Default, cancellationToken).ConfigureAwait(false);
            var referencedSymbols = progress.GetReferencedSymbols();

            // Ordering by descending to sort invocations by starting span to account for nested invocations
            var referencedLocations = referencedSymbols.SelectMany(referencedSymbol => referencedSymbol.Locations)
                .Distinct().Where(reference => !reference.IsImplicit)
                .OrderByDescending(reference => reference.Location.SourceSpan.Start);

            // Adding the original document to ensure that it will be seen again when processing the call sites
            // in order to update the original expression and containing method.
            methodCallSites.Add(document, new List<SyntaxNode>());
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();

            foreach (var refLocation in referencedLocations)
            {
                // Does not support cross-language references currently
                if (refLocation.Document.Project.Language == document.Project.Language)
                {
                    var reference = refLocation.Location.FindNode(cancellationToken).GetRequiredParent();
                    var originalReference = reference;
                    if (reference is not (TObjectCreationExpressionSyntax or TInvocationExpressionSyntax))
                    {
                        reference = reference.GetRequiredParent();
                    }

                    // Only adding items that are of type InvocationExpressionSyntax or TObjectCreationExpressionSyntax
                    var invocationOrCreation = reference as TObjectCreationExpressionSyntax ?? (SyntaxNode?)(reference as TInvocationExpressionSyntax);
                    if (invocationOrCreation is null)
                    {
                        continue;
                    }

                    if (!methodCallSites.TryGetValue(refLocation.Document, out var list))
                    {
                        list = new List<SyntaxNode>();
                        methodCallSites.Add(refLocation.Document, list);
                    }

                    list.Add(invocationOrCreation);
                }
            }

            return methodCallSites;
        }

        /// <summary>
        /// If the parameter is optional and the invocation does not specify the parameter, then
        /// a named argument needs to be introduced.
        /// </summary>
        private static SeparatedSyntaxList<SyntaxNode> AddArgumentToArgumentList(
            SeparatedSyntaxList<SyntaxNode> invocationArguments, SyntaxGenerator generator,
            SyntaxNode newArgumentExpression, string parameterName, int insertionIndex, bool named)
        {
            var argument = named
                ? generator.Argument(parameterName, RefKind.None, newArgumentExpression)
                : generator.Argument(newArgumentExpression);
            return invocationArguments.Insert(insertionIndex, argument);
        }

        private static bool ShouldArgumentBeNamed(Compilation compilation, ISemanticFactsService semanticFacts,
            SemanticModel semanticModel, SeparatedSyntaxList<SyntaxNode> invocationArguments, int methodInsertionIndex,
            CancellationToken cancellationToken)
        {
            var invocationInsertIndex = 0;
            foreach (var invocationArgument in invocationArguments)
            {
                var argumentParameter = semanticFacts.FindParameterForArgument(semanticModel, invocationArgument, cancellationToken);
                if (argumentParameter is not null && ShouldIndexBeIncremented(compilation, argumentParameter))
                {
                    invocationInsertIndex++;
                }
                else
                {
                    break;
                }
            }

            return invocationInsertIndex < methodInsertionIndex;
        }

        /// <summary>
        /// Goes through the parameters of the original method to get the location that the parameter
        /// and argument should be introduced.
        /// </summary>
        private static int GetInsertionIndex(Compilation compilation, IMethodSymbol methodSymbol,
            ISyntaxFactsService syntaxFacts, SyntaxNode methodDeclaration)
        {
            var parameterList = syntaxFacts.GetParameterList(methodDeclaration);
            Contract.ThrowIfNull(parameterList);
            var insertionIndex = 0;

            foreach (var parameterSymbol in methodSymbol.Parameters)
            {
                // Want to skip optional parameters, params parameters, and CancellationToken since they should be at
                // the end of the list.
                if (ShouldIndexBeIncremented(compilation, parameterSymbol))
                {
                    insertionIndex++;
                }
            }

            return insertionIndex;
        }

        private static bool ShouldIndexBeIncremented(Compilation compilation, IParameterSymbol parameter)
            => !parameter.HasExplicitDefaultValue && !parameter.IsParams &&
                        !parameter.Type.Equals(compilation.GetTypeByMetadataName(typeof(CancellationToken)?.FullName!));

        /// <summary>
        /// Gets the matches of the expression and replaces them with the identifier.
        /// Special case for the original matching expression, if its parent is a LocalDeclarationStatement then it can
        /// be removed because assigning the local dec variable to a parameter is repetitive. Does not need a rename
        /// annotation since the user has already named the local declaration.
        /// Otherwise, it needs to have a rename annotation added to it because the new parameter gets a randomly
        /// generated name that the user can immediately change.
        /// </summary>
        public async Task UpdateExpressionInOriginalFunctionAsync(Document document,
            TExpressionSyntax expression, SyntaxNode scope, string parameterName, SyntaxEditor editor,
            bool allOccurrences, CancellationToken cancellationToken)
        {
            var generator = editor.Generator;
            var matches = await FindMatchesAsync(document, expression, scope, allOccurrences, cancellationToken).ConfigureAwait(false);
            var replacement = (TIdentifierNameSyntax)generator.IdentifierName(parameterName);

            foreach (var match in matches)
            {
                // Special case the removal of the originating expression to either remove the local declaration
                // or to add a rename annotation.
                if (!match.Equals(expression))
                {
                    editor.ReplaceNode(match, replacement);
                }
                else
                {
                    if (ShouldRemoveVariableDeclaratorContainingExpression(document, expression, out _, out var localDeclaration))
                    {
                        editor.RemoveNode(localDeclaration);
                    }
                    else
                    {
                        // Found the initially selected expression. Replace it with the new name we choose, but also annotate
                        // that name with the RenameAnnotation so a rename session is started where the user can pick their
                        // own preferred name.
                        replacement = (TIdentifierNameSyntax)generator.IdentifierName(generator.Identifier(parameterName)
                            .WithAdditionalAnnotations(RenameAnnotation.Create()));
                        editor.ReplaceNode(match, replacement);
                    }
                }
            }
        }

        /// <summary>
        /// For the trampoline case, it goes through the invocations and adds an argument which is a 
        /// call to the extracted method.
        /// Introduces a new method overload or new trampoline method.
        /// Updates the original method site with a newly introduced parameter.
        /// 
        /// ****Trampoline Example:****
        /// public void M(int x, int y)
        /// {
        ///     int f = [|x * y|];
        ///     Console.WriteLine(f);
        /// }
        /// 
        /// public void InvokeMethod()
        /// {
        ///     M(5, 6);
        /// }
        /// 
        /// ---------------------------------------------------->
        /// 
        /// public int GetF(int x, int y) // Generated method
        /// {
        ///     return x * y;
        /// }
        /// 
        /// public void M(int x, int y, int f)
        /// {
        ///     Console.WriteLine(f);
        /// }
        /// 
        /// public void InvokeMethod()
        /// {
        ///     M(5, 6, GetF(5, 6)); //Fills in with call to generated method
        /// }
        /// 
        /// -----------------------------------------------------------------------
        /// ****Overload Example:****
        /// public void M(int x, int y)
        /// {
        ///     int f = [|x * y|];
        ///     Console.WriteLine(f);
        /// }
        /// 
        /// public void InvokeMethod()
        /// {
        ///     M(5, 6);
        /// }
        /// 
        /// ---------------------------------------------------->
        /// 
        /// public void M(int x, int y) // Generated overload
        /// {
        ///     M(x, y, x * y)
        /// }
        /// 
        /// public void M(int x, int y, int f)
        /// {
        ///     Console.WriteLine(f);
        /// }
        /// 
        /// public void InvokeMethod()
        /// {
        ///     M(5, 6);
        /// }
        /// </summary>
        private async Task<SyntaxNode> ModifyDocumentInvocationsTrampolineOverloadAndIntroduceParameterAsync(Compilation compilation,
            Document currentDocument, Document originalDocument,
            List<SyntaxNode> invocations, Dictionary<TIdentifierNameSyntax, IParameterSymbol> mappingDictionary,
            IMethodSymbol methodSymbol, SyntaxNode containingMethod, int insertionIndex,
            bool allOccurrences, string parameterName, TExpressionSyntax expression, SelectedCodeAction selectedCodeAction,
            CancellationToken cancellationToken)
        {
            var generator = SyntaxGenerator.GetGenerator(currentDocument);
            var semanticFacts = currentDocument.GetRequiredLanguageService<ISemanticFactsService>();
            var invocationSemanticModel = await currentDocument.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var root = await currentDocument.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var editor = new SyntaxEditor(root, generator);
            var syntaxFacts = currentDocument.GetRequiredLanguageService<ISyntaxFactsService>();

            // Creating a new method name by concatenating the parameter name that has been upper-cased.
            var newMethodIdentifier = "Get" + parameterName.ToPascalCase();
            var validParameters = methodSymbol.Parameters.Intersect(mappingDictionary.Values).ToImmutableArray();

            if (selectedCodeAction is SelectedCodeAction.Trampoline)
            {
                var parameterToArgumentMap = new Dictionary<IParameterSymbol, int>();
                foreach (var invocation in invocations)
                {
                    var argumentListSyntax = syntaxFacts.GetArgumentListOfInvocationExpression(invocation);
                    editor.ReplaceNode(argumentListSyntax, (currentArgumentListSyntax, _) =>
                    {
                        return GenerateNewArgumentListSyntaxForTrampoline(compilation, syntaxFacts, semanticFacts, invocationSemanticModel, generator,
                            parameterToArgumentMap, currentArgumentListSyntax,
                            argumentListSyntax, invocation, validParameters, parameterName, newMethodIdentifier, insertionIndex, cancellationToken);
                    });
                }
            }

            // If you are at the original document, then also introduce the new method and introduce the parameter.
            if (currentDocument.Id == originalDocument.Id)
            {
                if (selectedCodeAction is SelectedCodeAction.Trampoline)
                {
                    var newMethodNode = await ExtractMethodAsync(originalDocument, expression, methodSymbol, validParameters, newMethodIdentifier,
                        generator, cancellationToken).ConfigureAwait(false);
                    editor.InsertBefore(containingMethod, newMethodNode);
                }
                else if (selectedCodeAction is SelectedCodeAction.Overload)
                {
                    var newMethodNode = await GenerateNewMethodOverloadAsync(originalDocument, expression, methodSymbol, insertionIndex,
                        generator, cancellationToken).ConfigureAwait(false);
                    editor.InsertBefore(containingMethod, newMethodNode);
                }

                await UpdateExpressionInOriginalFunctionAsync(originalDocument, expression, containingMethod,
                    parameterName, editor, allOccurrences, cancellationToken).ConfigureAwait(false);
                var parameterType = await GetTypeOfExpressionAsync(originalDocument, expression, cancellationToken).ConfigureAwait(false);
                var parameter = generator.ParameterDeclaration(parameterName, generator.TypeExpression(parameterType));
                editor.InsertParameter(containingMethod, insertionIndex, parameter);
            }

            return editor.GetChangedRoot();

            // Adds an argument which is an invocation of the newly created method to the callsites
            // of the method invocations where a parameter was added.
            // Example:
            // public void M(int x, int y)
            // {
            //     int f = [|x * y|];
            //     Console.WriteLine(f);
            // }
            // 
            // public void InvokeMethod()
            // {
            //     M(5, 6);
            // }
            //
            // ---------------------------------------------------->
            // 
            // public int GetF(int x, int y)
            // {
            //     return x * y;
            // }
            // 
            // public void M(int x, int y)
            // {
            //     int f = x * y;
            //     Console.WriteLine(f);
            // }
            //
            // public void InvokeMethod()
            // {
            //     M(5, 6, GetF(5, 6)); // This is the generated invocation which is a new argument at the call site
            // }
            SyntaxNode GenerateNewArgumentListSyntaxForTrampoline(Compilation compilation, ISyntaxFactsService syntaxFacts, ISemanticFactsService semanticFacts,
                SemanticModel invocationSemanticModel, SyntaxGenerator generator, Dictionary<IParameterSymbol, int> parameterToArgumentMap,
                SyntaxNode currentArgumentListSyntax, SyntaxNode argumentListSyntax,
                SyntaxNode invocation, ImmutableArray<IParameterSymbol> validParameters, string parameterName, string newMethodIdentifier,
                int insertionIndex, CancellationToken cancellationToken)
            {
                var invocationArguments = syntaxFacts.GetArgumentsOfArgumentList(argumentListSyntax);
                parameterToArgumentMap.Clear();
                MapParameterToArgumentsAtInvocation(semanticFacts, parameterToArgumentMap, invocationArguments, invocationSemanticModel, cancellationToken);
                var currentInvocationArguments = syntaxFacts.GetArgumentsOfArgumentList(currentArgumentListSyntax);
                var requiredArguments = new List<SyntaxNode>();

                foreach (var parameterSymbol in validParameters)
                {
                    if (parameterToArgumentMap.TryGetValue(parameterSymbol, out var index))
                    {
                        requiredArguments.Add(currentInvocationArguments[index]);
                    }
                }

                var conditionalRoot = syntaxFacts.GetRootConditionalAccessExpression(invocation);
                var named = ShouldArgumentBeNamed(compilation, semanticFacts, invocationSemanticModel, invocationArguments, insertionIndex, cancellationToken);
                var newMethodInvocation = GenerateNewMethodInvocation(syntaxFacts, generator, invocation, requiredArguments, newMethodIdentifier);

                SeparatedSyntaxList<SyntaxNode> allArguments;
                if (conditionalRoot is null)
                {
                    allArguments = AddArgumentToArgumentList(currentInvocationArguments, generator, newMethodInvocation, parameterName, insertionIndex, named);
                }
                else
                {
                    var expressionsWithConditionalAccessors = conditionalRoot.ReplaceNode(invocation, newMethodInvocation);
                    allArguments = AddArgumentToArgumentList(currentInvocationArguments, generator, expressionsWithConditionalAccessors, parameterName, insertionIndex, named);
                }

                return UpdateArgumentListSyntax(currentArgumentListSyntax, allArguments);
            }
        }

        private static SyntaxNode GenerateNewMethodInvocation(ISyntaxFactsService syntaxFacts, SyntaxGenerator generator, SyntaxNode invocation, List<SyntaxNode> arguments, string newMethodIdentifier)
        {
            var methodName = generator.IdentifierName(newMethodIdentifier);
            var fullExpression = syntaxFacts.GetExpressionOfInvocationExpression(invocation);
            if (syntaxFacts.IsAnyMemberAccessExpression(fullExpression))
            {
                var identifierExpression = syntaxFacts.GetExpressionOfMemberAccessExpression(fullExpression);
                if (identifierExpression is not null)
                {
                    methodName = generator.MemberAccessExpression(identifierExpression, newMethodIdentifier);
                }
                else
                {
                    // In the VB case sometimes the the member access expression's expression is nothing
                    // so I generate a member binding expression instead.
                    methodName = generator.MemberAccessExpression(expression: null, newMethodIdentifier);
                }
            }
            else if (syntaxFacts.IsMemberBindingExpression(fullExpression))
            {
                methodName = generator.MemberBindingExpression(generator.IdentifierName(newMethodIdentifier));
            }

            return generator.InvocationExpression(methodName, arguments);
        }

        /// <summary>
        /// Generates a method declaration containing a return expression of the highlighted expression.
        /// Example:
        /// public void M(int x, int y)
        /// {
        ///     int f = [|x * y|];
        /// }
        /// 
        /// ---------------------------------------------------->
        /// 
        /// public int GetF(int x, int y)
        /// {
        ///     return x * y;
        /// }
        /// 
        /// public void M(int x, int y)
        /// {
        ///     int f = x * y;
        /// }
        /// </summary>
        private static async Task<SyntaxNode> ExtractMethodAsync(Document document, TExpressionSyntax expression,
            IMethodSymbol methodSymbol, ImmutableArray<IParameterSymbol> validParameters, string newMethodIdentifier, SyntaxGenerator generator, CancellationToken cancellationToken)
        {
            // Remove trivia so the expression is in a single line and does not affect the spacing of the following line
            var returnStatement = generator.ReturnStatement(expression.WithoutTrivia());
            var typeSymbol = await GetTypeOfExpressionAsync(document, expression, cancellationToken).ConfigureAwait(false);
            var newMethodDeclaration = await CreateMethodDeclarationAsync(document, methodSymbol, expression, returnStatement,
                validParameters, newMethodIdentifier, typeSymbol, isTrampoline: true, cancellationToken).ConfigureAwait(false);
            return newMethodDeclaration;
        }

        /// <summary>
        /// Generates a method declaration containing a call to the method that introduced the parameter.
        /// Example:
        /// 
        /// ***This is an intermediary step in which the original function has not be updated yet
        /// public void M(int x, int y)
        /// {
        ///     int f = [|x * y|];
        /// }
        /// 
        /// ---------------------------------------------------->
        /// 
        /// public void M(int x, int y) // Generated overload
        /// {
        ///     M(x, y, x * y);
        /// }
        /// 
        /// public void M(int x, int y) // Original function (which will be mutated in a later step)
        /// {
        ///     int f = x * y;
        /// }
        /// </summary>
        private static async Task<SyntaxNode> GenerateNewMethodOverloadAsync(Document document, TExpressionSyntax expression,
            IMethodSymbol methodSymbol, int insertionIndex, SyntaxGenerator generator, CancellationToken cancellationToken)
        {
            // Need the parameters from the original function as arguments for the invocation
            var arguments = generator.CreateArguments(methodSymbol.Parameters);

            // Remove trivia so the expression is in a single line and does not affect the spacing of the following line
            arguments = arguments.Insert(insertionIndex, generator.Argument(expression.WithoutTrivia()));
            var memberName = methodSymbol.IsGenericMethod
                ? generator.GenericName(methodSymbol.Name, methodSymbol.TypeArguments)
                : generator.IdentifierName(methodSymbol.Name);
            var invocation = generator.InvocationExpression(memberName, arguments);

            var newStatement = methodSymbol.ReturnsVoid
               ? generator.ExpressionStatement(invocation)
               : generator.ReturnStatement(invocation);

            var newMethodDeclaration = await CreateMethodDeclarationAsync(document, methodSymbol, expression, newStatement,
                validParameters: null, newMethodIdentifier: null, typeSymbol: null, isTrampoline: false, cancellationToken).ConfigureAwait(false);
            return newMethodDeclaration;
        }

        private static async Task<SyntaxNode> CreateMethodDeclarationAsync(Document document, IMethodSymbol methodSymbol,
            TExpressionSyntax expression, SyntaxNode newStatement, ImmutableArray<IParameterSymbol>? validParameters,
            string? newMethodIdentifier, ITypeSymbol? typeSymbol, bool isTrampoline, CancellationToken cancellationToken)
        {
            var codeGenerationService = document.GetRequiredLanguageService<ICodeGenerationService>();
            var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            var newMethod = isTrampoline
                ? CodeGenerationSymbolFactory.CreateMethodSymbol(methodSymbol, name: newMethodIdentifier, parameters: validParameters, statements: ImmutableArray.Create(newStatement), returnType: typeSymbol)
                : CodeGenerationSymbolFactory.CreateMethodSymbol(methodSymbol, statements: ImmutableArray.Create(newStatement), containingType: methodSymbol.ContainingType);
            var newMethodDeclaration = codeGenerationService.CreateMethodDeclaration(newMethod, options: new CodeGenerationOptions(options: options, parseOptions: expression.SyntaxTree.Options));
            return newMethodDeclaration;
        }

        /// <summary>
        /// This method goes through all the invocation sites and adds a new argument with the expression to be added.
        /// It also introduces a parameter at the original method site.
        /// 
        /// Example:
        /// public void M(int x, int y)
        /// {
        ///     int f = [|x * y|];
        /// }
        /// 
        /// public void InvokeMethod()
        /// {
        ///     M(5, 6);
        /// }
        /// 
        /// ---------------------------------------------------->
        /// 
        /// public void M(int x, int y, int f) // parameter gets introduced
        /// {
        /// }
        /// 
        /// public void InvokeMethod()
        /// {
        ///     M(5, 6, 5 * 6); // argument gets added to callsite
        /// }
        /// </summary>
        private async Task<SyntaxNode> ModifyDocumentInvocationsAndIntroduceParameterAsync(Compilation compilation,
            Document originalDocument, Document document, Dictionary<TIdentifierNameSyntax, IParameterSymbol> mappingDictionary,
            SyntaxNode containingMethod, TExpressionSyntax expression,
            bool allOccurrences, string parameterName, int insertionIndex,
            List<SyntaxNode> invocations, CancellationToken cancellationToken)
        {
            var semanticFacts = document.GetRequiredLanguageService<ISemanticFactsService>();
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();
            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var generator = SyntaxGenerator.GetGenerator(document);
            var editor = new SyntaxEditor(root, generator);
            var invocationSemanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var parameterToArgumentMap = new Dictionary<IParameterSymbol, int>();
            foreach (var invocation in invocations)
            {
                var expressionEditor = new SyntaxEditor(expression, generator);

                var argumentListSyntax = invocation is TObjectCreationExpressionSyntax
                    ? syntaxFacts.GetArgumentListOfObjectCreationExpression(invocation)
                    : syntaxFacts.GetArgumentListOfInvocationExpression(invocation);

                var invocationArguments = syntaxFacts.GetArgumentsOfArgumentList(argumentListSyntax);
                parameterToArgumentMap.Clear();
                MapParameterToArgumentsAtInvocation(semanticFacts, parameterToArgumentMap, invocationArguments, invocationSemanticModel, cancellationToken);

                if (argumentListSyntax is not null)
                {
                    editor.ReplaceNode(argumentListSyntax, (currentArgumentListSyntax, _) =>
                    {
                        var updatedInvocationArguments = syntaxFacts.GetArgumentsOfArgumentList(currentArgumentListSyntax);
                        var updatedExpression = CreateNewArgumentExpression(expressionEditor,
                            syntaxFacts, mappingDictionary, parameterToArgumentMap, updatedInvocationArguments);
                        var named = ShouldArgumentBeNamed(compilation, semanticFacts, invocationSemanticModel, invocationArguments, insertionIndex, cancellationToken);
                        var allArguments = AddArgumentToArgumentList(updatedInvocationArguments, generator,
                            updatedExpression.WithAdditionalAnnotations(Formatter.Annotation), parameterName, insertionIndex, named);
                        return UpdateArgumentListSyntax(currentArgumentListSyntax, allArguments);
                    });
                }
            }

            // If you are at the original document, then also introduce the new method and introduce the parameter.
            if (document.Id == originalDocument.Id)
            {
                await UpdateExpressionInOriginalFunctionAsync(originalDocument, expression, containingMethod,
                    parameterName, editor, allOccurrences, cancellationToken).ConfigureAwait(false);
                var parameterType = await GetTypeOfExpressionAsync(document, expression, cancellationToken).ConfigureAwait(false);
                var parameter = generator.ParameterDeclaration(name: parameterName, type:
                    generator.TypeExpression(parameterType));
                editor.InsertParameter(containingMethod, insertionIndex, parameter);
            }

            return editor.GetChangedRoot();
        }

        private static async Task<ITypeSymbol> GetTypeOfExpressionAsync(Document document, TExpressionSyntax expression, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var typeSymbol = semanticModel.GetTypeInfo(expression, cancellationToken).ConvertedType ?? semanticModel.Compilation.ObjectType;
            return typeSymbol;
        }
        /// <summary>
        /// This method iterates through the variables in the expression and maps the variables back to the parameter
        /// it is associated with. It then maps the parameter back to the argument at the invocation site and gets the
        /// index to retrieve the updated arguments at the invocation.
        /// </summary>
        private TExpressionSyntax CreateNewArgumentExpression(SyntaxEditor editor, ISyntaxFactsService syntaxFacts,
            Dictionary<TIdentifierNameSyntax, IParameterSymbol> mappingDictionary,
            Dictionary<IParameterSymbol, int> parameterToArgumentMap,
            SeparatedSyntaxList<SyntaxNode> updatedInvocationArguments)
        {
            foreach (var (variable, mappedParameter) in mappingDictionary)
            {
                var parameterMapped = parameterToArgumentMap.TryGetValue(mappedParameter, out var index);
                if (parameterMapped)
                {
                    var updatedInvocationArgument = updatedInvocationArguments[index];
                    var argumentExpression = syntaxFacts.GetExpressionOfArgument(updatedInvocationArgument);
                    var parenthesizedArgumentExpression = editor.Generator.AddParentheses(argumentExpression, includeElasticTrivia: false);
                    editor.ReplaceNode(variable, parenthesizedArgumentExpression);
                }
                else if (mappedParameter.HasExplicitDefaultValue)
                {
                    var generatedExpression = GenerateExpressionFromOptionalParameter(mappedParameter);
                    var parenthesizedGeneratedExpression = editor.Generator.AddParentheses(generatedExpression, includeElasticTrivia: false);
                    editor.ReplaceNode(variable, parenthesizedGeneratedExpression);
                }
            }

            return (TExpressionSyntax)editor.GetChangedRoot();
        }

        private static void MapParameterToArgumentsAtInvocation(
            ISemanticFactsService semanticFacts, Dictionary<IParameterSymbol, int> mapping, SeparatedSyntaxList<SyntaxNode> arguments,
            SemanticModel invocationSemanticModel, CancellationToken cancellationToken)
        {
            for (var i = 0; i < arguments.Count; i++)
            {
                var argumentParameter = semanticFacts.FindParameterForArgument(invocationSemanticModel, arguments[i], cancellationToken);
                if (argumentParameter is not null)
                {
                    mapping[argumentParameter] = i;
                }
            }
        }

        /// <summary>
        /// Ties the identifiers within the expression back to their associated parameter.
        /// </summary>
        public static async Task<Dictionary<TIdentifierNameSyntax, IParameterSymbol>> MapExpressionToParametersAsync(
            Document document, TExpressionSyntax expression, CancellationToken cancellationToken)
        {
            var nameToParameterDict = new Dictionary<TIdentifierNameSyntax, IParameterSymbol>();
            var variablesInExpression = expression.DescendantNodes().OfType<TIdentifierNameSyntax>();
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            foreach (var variable in variablesInExpression)
            {
                var symbol = semanticModel.GetSymbolInfo(variable, cancellationToken).Symbol;
                if (symbol is IParameterSymbol parameterSymbol)
                {
                    nameToParameterDict.Add(variable, parameterSymbol);
                }
            }

            return nameToParameterDict;
        }

        /// <summary>
        /// Finds the matches of the expression within the same block.
        /// </summary>
        protected static async Task<IEnumerable<TExpressionSyntax>> FindMatchesAsync(Document document,
            TExpressionSyntax expression, SyntaxNode withinNode,
            bool allOccurrences, CancellationToken cancellationToken)
        {
            if (!allOccurrences)
            {
                return SpecializedCollections.SingletonEnumerable(expression);
            }

            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();
            var originalSemanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var matches = from nodeInCurrent in withinNode.DescendantNodesAndSelf().OfType<TExpressionSyntax>()
                          where NodeMatchesExpression(originalSemanticModel, expression, nodeInCurrent, cancellationToken)
                          select nodeInCurrent;
            return matches;
        }

        private static bool NodeMatchesExpression(SemanticModel originalSemanticModel,
            TExpressionSyntax expression, TExpressionSyntax currentNode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (currentNode == expression)
            {
                return true;
            }

            return SemanticEquivalence.AreEquivalent(
                originalSemanticModel, originalSemanticModel, expression, currentNode);
        }

        private class MyCodeAction : SolutionChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Solution>> createChangedSolution)
                : base(title, createChangedSolution)
            {
            }
        }

        private enum SelectedCodeAction
        {
            Refactor,
            Trampoline,
            Overload
        }
    }
}
