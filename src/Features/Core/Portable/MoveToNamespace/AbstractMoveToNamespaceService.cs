﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ChangeNamespace;
using Microsoft.CodeAnalysis.CodeRefactorings.MoveType;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.MoveToNamespace
{
    internal interface IMoveToNamespaceService : ILanguageService
    {
        Task<ImmutableArray<AbstractMoveToNamespaceCodeAction>> GetCodeActionsAsync(Document document, TextSpan span, CancellationToken cancellationToken);
        Task<MoveToNamespaceAnalysisResult> AnalyzeTypeAtPositionAsync(Document document, int position, CancellationToken cancellationToken);
        Task<MoveToNamespaceResult> MoveToNamespaceAsync(MoveToNamespaceAnalysisResult analysisResult, string targetNamespace, CancellationToken cancellationToken);
        MoveToNamespaceOptionsResult GetChangeNamespaceOptions(Document document, string defaultNamespace, ImmutableArray<string> namespaces);
        IMoveToNamespaceOptionsService OptionsService { get; }
    }

    internal abstract class AbstractMoveToNamespaceService<TNamespaceDeclarationSyntax, TNamedTypeDeclarationSyntax, TCompilationUnitSyntax>
        : IMoveToNamespaceService
        where TNamespaceDeclarationSyntax : SyntaxNode
        where TNamedTypeDeclarationSyntax : SyntaxNode
        where TCompilationUnitSyntax : SyntaxNode

    {
        protected abstract string GetNamespaceName(TNamespaceDeclarationSyntax namespaceSyntax);
        protected abstract string GetNamespaceName(TNamedTypeDeclarationSyntax namedTypeSyntax);
        protected abstract bool IsContainedInNamespaceDeclaration(TNamespaceDeclarationSyntax namespaceSyntax, int position);

        public IMoveToNamespaceOptionsService OptionsService { get; }

        protected AbstractMoveToNamespaceService(IMoveToNamespaceOptionsService moveToNamespaceOptionsService)
        {
            OptionsService = moveToNamespaceOptionsService;
        }

        public async Task<ImmutableArray<AbstractMoveToNamespaceCodeAction>> GetCodeActionsAsync(
            Document document,
            TextSpan span,
            CancellationToken cancellationToken)
        {
            // Code actions cannot be completed without the options needed
            // to fill in missing information.
            if (OptionsService != null)
            {
                var typeAnalysisResult = await AnalyzeTypeAtPositionAsync(document, span.Start, cancellationToken).ConfigureAwait(false);

                if (typeAnalysisResult.CanPerform)
                {
                    return ImmutableArray.Create(AbstractMoveToNamespaceCodeAction.Generate(this, typeAnalysisResult));
                }
            }

            return ImmutableArray<AbstractMoveToNamespaceCodeAction>.Empty;
        }

        public async Task<MoveToNamespaceAnalysisResult> AnalyzeTypeAtPositionAsync(
            Document document,
            int position,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);

            var token = root.FindToken(position);
            var node = token.Parent;

            var moveToNamespaceAnalysisResult = await TryAnalyzeNamespaceAsync(document, node, position, cancellationToken).ConfigureAwait(false);

            if (moveToNamespaceAnalysisResult != null)
            {
                return moveToNamespaceAnalysisResult;
            }

            moveToNamespaceAnalysisResult = await TryAnalyzeNamedTypeAsync(document, node, cancellationToken).ConfigureAwait(false);
            return moveToNamespaceAnalysisResult ?? MoveToNamespaceAnalysisResult.Invalid;
        }

        private async Task<MoveToNamespaceAnalysisResult> TryAnalyzeNamespaceAsync(
            Document document, SyntaxNode node, int position, CancellationToken cancellationToken)
        {
            var declarationSyntax = node.FirstAncestorOrSelf<TNamespaceDeclarationSyntax>();
            if (declarationSyntax == null || !IsContainedInNamespaceDeclaration(declarationSyntax, position))
            {
                return null;
            }

            if (ContainsNamespaceDeclaration(declarationSyntax) || GetNamespaceInSpineCount(declarationSyntax) > 1)
            {
                return MoveToNamespaceAnalysisResult.Invalid;
            }
            else
            {
                var namespaceName = GetNamespaceName(declarationSyntax);
                var namespaces = await GetNamespacesAsync(document, cancellationToken).ConfigureAwait(false);
                return new MoveToNamespaceAnalysisResult(document, declarationSyntax, namespaceName, namespaces.ToImmutableArray(), MoveToNamespaceAnalysisResult.ContainerType.Namespace);
            }
        }

        private async Task<MoveToNamespaceAnalysisResult> TryAnalyzeNamedTypeAsync(
            Document document, SyntaxNode node, CancellationToken cancellationToken)
        {
            var namespaceInSpineCount = GetNamespaceInSpineCount(node);

            // Multiple nested namespaces are currently not supported
            if (namespaceInSpineCount > 1 || ContainsMultipleTypesInSpine(node))
            {
                return MoveToNamespaceAnalysisResult.Invalid;
            }

            // Moving one of the many members declared in global namespace is not currently supported
            if (namespaceInSpineCount == 0)
            {
                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();

                if (syntaxFacts.GetMembersOfCompilationUnit(root).Count > 1)
                {
                    return MoveToNamespaceAnalysisResult.Invalid;
                }
            }

            if (node is TNamedTypeDeclarationSyntax namedTypeDeclarationSyntax)
            {
                var namespaceName = GetNamespaceName(namedTypeDeclarationSyntax);
                var namespaces = await GetNamespacesAsync(document, cancellationToken).ConfigureAwait(false);
                return new MoveToNamespaceAnalysisResult(document, namedTypeDeclarationSyntax, namespaceName, namespaces.ToImmutableArray(), MoveToNamespaceAnalysisResult.ContainerType.NamedType);
            }

            return null;
        }

        private bool ContainsNamespaceDeclaration(SyntaxNode node)
            => node.DescendantNodes().OfType<TNamespaceDeclarationSyntax>().Any();

        private static int GetNamespaceInSpineCount(SyntaxNode node)
            => node.AncestorsAndSelf().OfType<TNamespaceDeclarationSyntax>().Count();

        private static bool ContainsMultipleTypesInSpine(SyntaxNode node)
            => node.AncestorsAndSelf().OfType<TNamedTypeDeclarationSyntax>().Count() > 1;

        public Task<MoveToNamespaceResult> MoveToNamespaceAsync(
            MoveToNamespaceAnalysisResult analysisResult,
            string targetNamespace,
            CancellationToken cancellationToken)
        {
            if (!analysisResult.CanPerform)
            {
                return Task.FromResult(MoveToNamespaceResult.Failed);
            }

            switch (analysisResult.Container)
            {
                case MoveToNamespaceAnalysisResult.ContainerType.Namespace:
                    return MoveItemsInNamespaceAsync(analysisResult.Document, analysisResult.SyntaxNode, targetNamespace, cancellationToken);
                case MoveToNamespaceAnalysisResult.ContainerType.NamedType:
                    return MoveTypeToNamespaceAsync(analysisResult.Document, analysisResult.SyntaxNode, targetNamespace, cancellationToken);
                default:
                    throw new InvalidOperationException();
            }
        }

        private static async Task<ImmutableArray<ISymbol>> GetMemberSymbolsAsync(Document document, SyntaxNode container, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            switch (container)
            {
                case TNamespaceDeclarationSyntax namespaceNode:
                    var containerSymbol = (INamespaceSymbol)semanticModel.GetDeclaredSymbol(container);
                    return containerSymbol.GetMembers().SelectAsArray(m => (ISymbol)m);

                case TCompilationUnitSyntax compilationUnit:
                    var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();
                    var members = syntaxFacts.GetMembersOfCompilationUnit(compilationUnit);
                    Debug.Assert(members.Count == 1);
                    return members.SelectAsArray(member => semanticModel.GetDeclaredSymbol(member));

                default:
                    throw ExceptionUtilities.UnexpectedValue(container);
            }
        }

        private static async Task<MoveToNamespaceResult> MoveItemsInNamespaceAsync(
            Document document,
            SyntaxNode container,
            string targetNamespace,
            CancellationToken cancellationToken)
        {
            var memberSymbols = await GetMemberSymbolsAsync(document, container, cancellationToken).ConfigureAwait(false);
            var newNameOriginalSymbolMapping = memberSymbols
                .ToImmutableDictionary(symbol => GetNewSymbolName(symbol, targetNamespace), symbol => symbol);

            var changeNamespaceService = document.GetLanguageService<IChangeNamespaceService>();
            if (changeNamespaceService == null)
            {
                return MoveToNamespaceResult.Failed;
            }

            var originalSolution = document.Project.Solution;

            var changedSolution = await changeNamespaceService.ChangeNamespaceAsync(
                document,
                container,
                targetNamespace,
                cancellationToken).ConfigureAwait(false);

            return new MoveToNamespaceResult(originalSolution, changedSolution, document.Id, newNameOriginalSymbolMapping);
        }

        private static async Task<MoveToNamespaceResult> MoveTypeToNamespaceAsync(
            Document document,
            SyntaxNode container,
            string targetNamespace,
            CancellationToken cancellationToken)
        {
            var moveTypeService = document.GetLanguageService<IMoveTypeService>();
            if (moveTypeService == null)
            {
                return MoveToNamespaceResult.Failed;
            }

            // The move service expects a single position, not a full selection
            // See https://github.com/dotnet/roslyn/issues/34643
            var moveSpan = new TextSpan(container.FullSpan.Start, 0);

            var modifiedSolution = await moveTypeService.GetModifiedSolutionAsync(
                document,
                moveSpan,
                MoveTypeOperationKind.MoveTypeNamespaceScope,
                cancellationToken).ConfigureAwait(false);

            var modifiedDocument = modifiedSolution.GetDocument(document.Id);
            var syntaxRoot = await modifiedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var syntaxNode = syntaxRoot.GetAnnotatedNodes(AbstractMoveTypeService.NamespaceScopeMovedAnnotation).SingleOrDefault();
            if (syntaxNode == null)
            {
                // The type might be declared in global namespace
                syntaxNode = container.FirstAncestorOrSelf<TNamespaceDeclarationSyntax>() ?? syntaxRoot;
            }

            return await MoveItemsInNamespaceAsync(
                modifiedDocument,
                syntaxNode,
                targetNamespace,
                cancellationToken).ConfigureAwait(false);
        }

        private static string GetNewSymbolName(ISymbol symbol, string targetNamespace)
        {
            Debug.Assert(symbol != null && !string.IsNullOrEmpty(targetNamespace));

            var offset = symbol.ContainingNamespace.IsGlobalNamespace
                ? 0
                : symbol.ContainingNamespace.ToDisplayString().Length + 1;

            return $"{targetNamespace}.{symbol.ToDisplayString().Substring(offset)}";
        }

        private static readonly SymbolDisplayFormat QualifiedNamespaceFormat = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        protected static string GetQualifiedName(INamespaceSymbol namespaceSymbol)
            => namespaceSymbol.ToDisplayString(QualifiedNamespaceFormat);

        private static async Task<IEnumerable<string>> GetNamespacesAsync(Document document, CancellationToken cancellationToken)
        {
            var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

            return compilation.GlobalNamespace.GetAllNamespaces(cancellationToken)
                .Where(n => n.NamespaceKind == NamespaceKind.Module && n.ContainingAssembly == compilation.Assembly)
                .Select(GetQualifiedName);
        }

        public MoveToNamespaceOptionsResult GetChangeNamespaceOptions(
            Document document,
            string defaultNamespace,
            ImmutableArray<string> namespaces)
        {
            var syntaxFactsService = document.GetLanguageService<ISyntaxFactsService>();

            return OptionsService.GetChangeNamespaceOptions(
                defaultNamespace,
                namespaces,
                syntaxFactsService);
        }
    }
}
