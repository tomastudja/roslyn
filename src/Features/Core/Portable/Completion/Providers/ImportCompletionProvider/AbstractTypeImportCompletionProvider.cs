﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion.Log;
using Microsoft.CodeAnalysis.Completion.Providers.ImportCompletion;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Completion.Providers
{
    internal abstract class AbstractTypeImportCompletionProvider : AbstractImportCompletionProvider
    {
        private static readonly SymbolDisplayFormat s_typeNameDisplayFormat = new(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters |
                             SymbolDisplayGenericsOptions.IncludeVariance);

        protected override bool ShouldProvideCompletion(CompletionContext completionContext, SyntaxContext syntaxContext)
            => syntaxContext.IsTypeContext;

        protected override void LogCommit()
            => CompletionProvidersLogger.LogCommitOfTypeImportCompletionItem();

        protected abstract ImmutableArray<SyntaxNode> GetAliasDeclarationNodes(SyntaxNode node);

        protected override async Task AddCompletionItemsAsync(CompletionContext completionContext, SyntaxContext syntaxContext, HashSet<string> namespacesInScope, bool isExpandedCompletion, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Completion_TypeImportCompletionProvider_GetCompletionItemsAsync, cancellationToken))
            {
                var telemetryCounter = new TelemetryCounter();
                var typeImportCompletionService = completionContext.Document.GetRequiredLanguageService<ITypeImportCompletionService>();

                var itemsFromAllAssemblies = await typeImportCompletionService.GetAllTopLevelTypesAsync(
                    completionContext.Document.Project,
                    syntaxContext,
                    forceCacheCreation: isExpandedCompletion,
                    cancellationToken).ConfigureAwait(false);

                if (itemsFromAllAssemblies == null)
                {
                    telemetryCounter.CacheMiss = true;
                }
                else
                {
                    var aliasTargetNamespaceToTypeNameMap = GetAliasTypeDictionary(completionContext.Document, syntaxContext, cancellationToken);
                    foreach (var items in itemsFromAllAssemblies)
                    {
                        AddItems(items, completionContext, namespacesInScope, aliasTargetNamespaceToTypeNameMap, telemetryCounter);
                    }
                }

                telemetryCounter.Report();
            }
        }

        /// <summary>
        /// Get a multi-Dictionary stores the information about the target of all alias Symbol in the syntax tree.
        /// Multiple aliases might live under same namespace.
        /// Key is the namespace of the symbol, value is the name of the symbol.
        /// </summary>
        private MultiDictionary<string, string> GetAliasTypeDictionary(
            Document document,
            SyntaxContext syntaxContext,
            CancellationToken cancellationToken)
        {
            var syntaxFactsService = document.GetRequiredLanguageService<ISyntaxFactsService>();
            var dictionary = new MultiDictionary<string, string>(syntaxFactsService.StringComparer);

            var nodeToCheck = syntaxContext.LeftToken.Parent;
            if (nodeToCheck == null)
            {
                return dictionary;
            }

            // In case the caret is at the beginning of the file, take the root node.
            var aliasDeclarations = GetAliasDeclarationNodes(nodeToCheck);
            foreach (var aliasNode in aliasDeclarations)
            {
                var symbol = syntaxContext.SemanticModel.GetDeclaredSymbol(aliasNode, cancellationToken);
                if (symbol is IAliasSymbol {Target: ITypeSymbol target} && target.TypeKind != TypeKind.Error)
                {
                    var namespaceOfTarget = target.ContainingNamespace.ToDisplayString(SymbolDisplayFormats.NameFormat);
                    var typeNameOfTarget = target.ToDisplayString(s_typeNameDisplayFormat);
                    dictionary.Add(namespaceOfTarget, typeNameOfTarget);
                }
            }

            return dictionary;
        }

        private static void AddItems(
            ImmutableArray<CompletionItem> items,
            CompletionContext completionContext,
            HashSet<string> namespacesInScope,
            MultiDictionary<string, string> aliasTargetNamespaceToTypeNameMap,
            TelemetryCounter counter)
        {
            counter.ReferenceCount++;
            foreach (var item in items)
            {
                if (ShouldAddItem(item, namespacesInScope, aliasTargetNamespaceToTypeNameMap))
                {
                    // We can return cached item directly, item's span will be fixed by completion service.
                    // On the other hand, because of this (i.e. mutating the  span of cached item for each run),
                    // the provider can not be used as a service by components that might be run in parallel
                    // with completion, which would be a race.
                    completionContext.AddItem(item);
                    counter.ItemsCount++;
                }
            }

            static bool ShouldAddItem(
                CompletionItem item,
                HashSet<string> namespacesInScope,
                MultiDictionary<string, string> aliasTargetNamespaceToTypeNameMap)
            {
                var containingNamespace = ImportCompletionItem.GetContainingNamespace(item);
                // 1. if the namespace of the item is in scoop. Don't add the item
                if (namespacesInScope.Contains(containingNamespace))
                {
                    return false;
                }

                // 2. If the item might be an alias target. First check if its namespace is in the map.
                if (aliasTargetNamespaceToTypeNameMap.ContainsKey(containingNamespace))
                {
                    // Then check its type name.
                    // It is done in this way because we don't want to get type name for all the items
                    var typeName = ImportCompletionItem.GetTypeName(item);
                    if (aliasTargetNamespaceToTypeNameMap[containingNamespace].Contains(typeName))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private class TelemetryCounter
        {
            protected int Tick { get; }
            public int ItemsCount { get; set; }
            public int ReferenceCount { get; set; }
            public bool CacheMiss { get; set; }

            public TelemetryCounter()
                => Tick = Environment.TickCount;

            public void Report()
            {
                if (CacheMiss)
                {
                    CompletionProvidersLogger.LogTypeImportCompletionCacheMiss();
                }
                else
                {
                    var delta = Environment.TickCount - Tick;
                    CompletionProvidersLogger.LogTypeImportCompletionTicksDataPoint(delta);
                    CompletionProvidersLogger.LogTypeImportCompletionItemCountDataPoint(ItemsCount);
                    CompletionProvidersLogger.LogTypeImportCompletionReferenceCountDataPoint(ReferenceCount);
                }
            }
        }
    }
}
