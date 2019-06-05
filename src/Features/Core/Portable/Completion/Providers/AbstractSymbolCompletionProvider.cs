﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Experiments;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Tags;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Completion.Providers
{
    internal abstract class AbstractSymbolCompletionProvider : CommonCompletionProvider
    {
        // PERF: Many CompletionProviders derive AbstractSymbolCompletionProvider and therefore
        // compute identical contexts. This actually shows up on the 2-core typing test.
        // Cache the most recent document/position/computed SyntaxContext to reduce repeat computation.
        private static readonly ConditionalWeakTable<Document, Task<SyntaxContext>> s_cachedDocuments = new ConditionalWeakTable<Document, Task<SyntaxContext>>();
        private static int s_cachedPosition;
        private static readonly object s_cacheGate = new object();

        private bool? _isTargetTypeCompletionFilterExperimentEnabled = null;

        protected AbstractSymbolCompletionProvider()
        {
        }

        protected abstract (string displayText, string suffix, string insertionText) GetDisplayAndSuffixAndInsertionText(
            ISymbol symbol, SyntaxContext context);

        protected virtual CompletionItemRules GetCompletionItemRules(IReadOnlyList<ISymbol> symbols)
            => CompletionItemRules.Default;

        /// <summary>
        /// Given a list of symbols, creates the list of completion items for them.
        /// </summary>
        private ImmutableArray<CompletionItem> CreateItems(
            ImmutableArray<ISymbol> symbols,
            SyntaxContext context,
            bool preselect,
            ImmutableArray<ITypeSymbol> inferredTypes)
        {
            if (IsTargetTypeCompletionFilterExperimentEnabled(context.Workspace))
            {
                var symbolGroups = from symbol in symbols
                                   let texts = GetDisplayAndSuffixAndInsertionText(symbol, context)
                                   group symbol by texts into g
                                   select g;

                var itemListBuilder = ImmutableArray.CreateBuilder<CompletionItem>();

                foreach (var symbolGroup in symbolGroups)
                {
                    var item = this.CreateItem(
                            symbolGroup.Key.displayText, symbolGroup.Key.suffix, symbolGroup.Key.insertionText, symbolGroup.ToList(), context,
                            invalidProjectMap: null, totalProjects: null, preselect: preselect);

                    foreach (var symbol in symbolGroup)
                    {
                        if (ShouldIncludeInTargetTypedCompletionList(symbol, inferredTypes, context.SemanticModel, context.Position))
                        {
                            item = item.AddTag(WellKnownTags.TargetTypeMatch);
                            break;
                        }
                    }

                    itemListBuilder.Add(item);
                }

                return itemListBuilder.ToImmutable();
            }
            else
            {
                var q = from symbol in symbols
                        let texts = GetDisplayAndSuffixAndInsertionText(symbol, context)
                        group symbol by texts into g
                        select this.CreateItem(
                            g.Key.displayText, g.Key.suffix, g.Key.insertionText, g.ToList(), context,
                            invalidProjectMap: null, totalProjects: null, preselect: preselect);

                return q.ToImmutableArray();
            }
        }

        private bool IsTargetTypeCompletionFilterExperimentEnabled(Workspace workspace)
        {
            if (!_isTargetTypeCompletionFilterExperimentEnabled.HasValue)
            {
                var experimentationService = workspace.Services.GetService<IExperimentationService>();
                _isTargetTypeCompletionFilterExperimentEnabled = experimentationService.IsExperimentEnabled(WellKnownExperimentNames.TargetTypedCompletionFilter);
            }

            return _isTargetTypeCompletionFilterExperimentEnabled == true;
        }

        private bool ShouldIncludeInTargetTypedCompletionList(ISymbol symbol, ImmutableArray<ITypeSymbol> inferredTypes, SemanticModel semanticModel, int position)
        {
            // When searching for identifiers of type C, exclude the symbol for the `C` type itself.
            if (symbol.Kind == SymbolKind.NamedType)
            {
                return false;
            }

            // Avoid offering members of object since they too commonly show up and are infrequently desired.
            if (symbol.ContainingType?.SpecialType == SpecialType.System_Object)
            {
                return false;
            }

            // Don't offer locals on the right-hand-side of their declaration: `int x = x`
            if (symbol.Kind == SymbolKind.Local)
            {
                var local = (ILocalSymbol)symbol;
                var declarationSyntax = symbol.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).SingleOrDefault();
                if (declarationSyntax != null && position < declarationSyntax.FullSpan.End)
                {
                    return false;
                }
            }

            var type = symbol.GetMemberType() ?? symbol.GetSymbolType();
            if (type == null)
            {
                return false;
            }

            foreach (var inferredType in inferredTypes)
            {
                if (semanticModel.Compilation.ClassifyCommonConversion(type.WithoutNullability(), inferredType.WithoutNullability()).IsImplicit)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Given a list of symbols, and a mapping from each symbol to its original SemanticModel, 
        /// creates the list of completion items for them.
        /// </summary>
        private ImmutableArray<CompletionItem> CreateItems(
            Dictionary<ISymbol, SyntaxContext> originatingContextMap,
            Dictionary<ISymbol, List<ProjectId>> invalidProjectMap,
            List<ProjectId> totalProjects,
            bool preselect,
            ImmutableArray<ITypeSymbol> inferredTypes)
        {
            if (IsTargetTypeCompletionFilterExperimentEnabled(originatingContextMap.First().Value.Workspace))
            {
                var symbols = originatingContextMap.Keys;
                var symbolGroups = from symbol in symbols
                                   let texts = GetDisplayAndSuffixAndInsertionText(symbol, originatingContextMap[symbol])
                                   group symbol by texts into g
                                   select g;

                var itemListBuilder = ImmutableArray.CreateBuilder<CompletionItem>();

                foreach (var symbolGroup in symbolGroups)
                {
                    var item = this.CreateItem(
                            symbolGroup.Key.displayText, symbolGroup.Key.suffix, symbolGroup.Key.insertionText, symbolGroup.ToList(),
                            originatingContextMap[symbolGroup.First()], invalidProjectMap, totalProjects, preselect);

                    item = AddTargetTypeMatchTagIfAppropriate(item, originatingContextMap, inferredTypes, symbolGroup);

                    itemListBuilder.Add(item);
                }

                return itemListBuilder.ToImmutable();
            }
            else
            {
                var symbols = originatingContextMap.Keys;
                var q = from symbol in symbols
                        let texts = GetDisplayAndSuffixAndInsertionText(symbol, originatingContextMap[symbol])
                        group symbol by texts into g
                        select this.CreateItem(
                            g.Key.displayText, g.Key.suffix, g.Key.insertionText, g.ToList(),
                            originatingContextMap[g.First()], invalidProjectMap, totalProjects, preselect);

                return q.ToImmutableArray();
            }
        }

        private CompletionItem AddTargetTypeMatchTagIfAppropriate(
            CompletionItem item,
            Dictionary<ISymbol, SyntaxContext> originatingContextMap,
            ImmutableArray<ITypeSymbol> inferredTypes,
            IGrouping<(string displayText, string suffix, string insertionText), ISymbol> symbolGroup)
        {
            foreach (var symbol in symbolGroup)
            {
                foreach (var syntaxContext in originatingContextMap.Values)
                {
                    if (ShouldIncludeInTargetTypedCompletionList(symbol, inferredTypes, syntaxContext.SemanticModel, syntaxContext.Position))
                    {
                        return item.AddTag(WellKnownTags.TargetTypeMatch);
                    }
                }
            }

            return item;
        }

        /// <summary>
        /// Given a Symbol, creates the completion item for it.
        /// </summary>
        private CompletionItem CreateItem(
            string displayText,
            string displayTextSuffix,
            string insertionText,
            List<ISymbol> symbols,
            SyntaxContext context,
            Dictionary<ISymbol, List<ProjectId>> invalidProjectMap,
            List<ProjectId> totalProjects,
            bool preselect)
        {
            Contract.ThrowIfNull(symbols);

            SupportedPlatformData supportedPlatformData = null;
            if (invalidProjectMap != null)
            {
                List<ProjectId> invalidProjects = null;
                foreach (var symbol in symbols)
                {
                    if (invalidProjectMap.TryGetValue(symbol, out invalidProjects))
                    {
                        break;
                    }
                }

                if (invalidProjects != null)
                {
                    supportedPlatformData = new SupportedPlatformData(invalidProjects, totalProjects, context.Workspace);
                }
            }

            return CreateItem(
                displayText, displayTextSuffix, insertionText, symbols,
                context, preselect, supportedPlatformData);
        }

        protected virtual CompletionItem CreateItem(
            string displayText, string displayTextSuffix, string insertionText,
            List<ISymbol> symbols, SyntaxContext context, bool preselect,
            SupportedPlatformData supportedPlatformData)
        {
            return SymbolCompletionItem.CreateWithSymbolId(
                displayText: displayText,
                displayTextSuffix: displayTextSuffix,
                insertionText: insertionText,
                filterText: GetFilterText(symbols[0], displayText, context),
                contextPosition: context.Position,
                symbols: symbols,
                supportedPlatforms: supportedPlatformData,
                rules: GetCompletionItemRules(symbols)
                        .WithMatchPriority(preselect ? MatchPriority.Preselect : MatchPriority.Default)
                        .WithSelectionBehavior(context.IsRightSideOfNumericType ? CompletionItemSelectionBehavior.SoftSelection : CompletionItemSelectionBehavior.Default));
        }

        protected virtual string GetFilterText(ISymbol symbol, string displayText, SyntaxContext context)
        {
            return (displayText == symbol.Name) ||
                (displayText.Length > 0 && displayText[0] == '@') ||
                (context.IsAttributeNameContext && symbol.IsAttribute())
                ? displayText
                : symbol.Name;
        }

        protected abstract Task<ImmutableArray<ISymbol>> GetSymbolsWorker(SyntaxContext context, int position, OptionSet options, CancellationToken cancellationToken);

        protected virtual Task<ImmutableArray<ISymbol>> GetPreselectedSymbolsWorker(SyntaxContext context, int position, OptionSet options, CancellationToken cancellationToken)
        {
            return SpecializedTasks.EmptyImmutableArray<ISymbol>();
        }

        protected override Task<CompletionDescription> GetDescriptionWorkerAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
            => SymbolCompletionItem.GetDescriptionAsync(item, document, cancellationToken);

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            try
            {
                var document = context.Document;
                var position = context.Position;
                var options = context.Options;
                var cancellationToken = context.CancellationToken;

                // If we were triggered by typing a character, then do a semantic check to make sure
                // we're still applicable.  If not, then return immediately.
                if (context.Trigger.Kind == CompletionTriggerKind.Insertion)
                {
                    var isSemanticTriggerCharacter = await IsSemanticTriggerCharacterAsync(document, position - 1, cancellationToken).ConfigureAwait(false);
                    if (!isSemanticTriggerCharacter)
                    {
                        return;
                    }
                }

                context.IsExclusive = IsExclusive();

                using (Logger.LogBlock(FunctionId.Completion_SymbolCompletionProvider_GetItemsWorker, cancellationToken))
                {
                    var syntaxContext = await GetOrCreateContext(document, position, cancellationToken).ConfigureAwait(false);

                    var regularItems = await GetItemsWorkerAsync(syntaxContext, document, position, options, preselect: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                    context.AddItems(regularItems);

                    var preselectedItems = await GetItemsWorkerAsync(syntaxContext, document, position, options, preselect: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                    context.AddItems(preselectedItems);
                }
            }
            catch (Exception e) when (FatalError.ReportWithoutCrashUnlessCanceled(e))
            {
                // nop
            }
        }

        private async Task<IEnumerable<CompletionItem>> GetItemsWorkerAsync(
            SyntaxContext context, Document document, int position, OptionSet options, bool preselect, CancellationToken cancellationToken)
        {
            var relatedDocumentIds = GetRelatedDocumentIds(document, position);

            options = GetUpdatedRecommendationOptions(options, document.Project.Language);

            var typeInferenceService = document.GetLanguageService<ITypeInferenceService>();
            var inferredTypes = typeInferenceService.InferTypes(context.SemanticModel, position, cancellationToken);

            if (relatedDocumentIds.IsEmpty)
            {
                var itemsForCurrentDocument = await GetSymbolsWorker(position, preselect, context, options, cancellationToken).ConfigureAwait(false);
                return CreateItems(itemsForCurrentDocument, context, preselect, inferredTypes);
            }

            var contextAndSymbolLists = await GetPerContextSymbols(document, position, options, new[] { document.Id }.Concat(relatedDocumentIds), preselect, cancellationToken).ConfigureAwait(false);
            var symbolToContextMap = UnionSymbols(contextAndSymbolLists);
            var missingSymbolsMap = FindSymbolsMissingInLinkedContexts(symbolToContextMap, contextAndSymbolLists);
            var totalProjects = contextAndSymbolLists.Select(t => t.documentId.ProjectId).ToList();

            return CreateItems(symbolToContextMap, missingSymbolsMap, totalProjects, preselect, inferredTypes);
        }

        private static ImmutableArray<DocumentId> GetRelatedDocumentIds(Document document, int position)
        {
            var relatedDocumentIds = document.GetLinkedDocumentIds();
            var relatedDocuments = relatedDocumentIds.Concat(document.Id).Select(document.Project.Solution.GetDocument);
            lock (s_cacheGate)
            {
                // Invalidate the cache if it's for a different position or a different set of Documents.
                // It's fairly likely that we'll only have to check the first document, unless someone
                // specially constructed a Solution with mismatched linked files.
                if (s_cachedPosition != position ||
                    !relatedDocuments.All((Document d) => s_cachedDocuments.TryGetValue(d, out var value)))
                {
                    s_cachedPosition = position;
                    foreach (var related in relatedDocuments)
                    {
                        s_cachedDocuments.Remove(document);
                    }
                }
            }

            return relatedDocumentIds;
        }

        protected virtual bool IsExclusive()
        {
            return false;
        }

        protected virtual Task<bool> IsSemanticTriggerCharacterAsync(Document document, int characterPosition, CancellationToken cancellationToken)
        {
            return SpecializedTasks.True;
        }

        private Task<ImmutableArray<ISymbol>> GetSymbolsWorker(int position, bool preselect, SyntaxContext context, OptionSet options, CancellationToken cancellationToken)
        {
            try
            {
                return preselect
                    ? GetPreselectedSymbolsWorker(context, position, options, cancellationToken)
                    : GetSymbolsWorker(context, position, options, cancellationToken);
            }
            catch (Exception e) when (FatalError.ReportUnlessCanceled(e))
            {
                throw ExceptionUtilities.Unreachable;
            }
        }

        private Dictionary<ISymbol, SyntaxContext> UnionSymbols(
            ImmutableArray<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<ISymbol> symbols)> linkedContextSymbolLists)
        {
            // To correctly map symbols back to their SyntaxContext, we do care about assembly identity.
            var result = new Dictionary<ISymbol, SyntaxContext>(LinkedFilesSymbolEquivalenceComparer.Instance);

            // We don't care about assembly identity when creating the union.
            foreach (var linkedContextSymbolList in linkedContextSymbolLists)
            {
                // We need to use the SemanticModel any particular symbol came from in order to generate its description correctly.
                // Therefore, when we add a symbol to set of union symbols, add a mapping from it to its SyntaxContext.
                foreach (var symbol in linkedContextSymbolList.symbols.GroupBy(s => new { s.Name, s.Kind }).Select(g => g.First()))
                {
                    if (!result.ContainsKey(symbol))
                    {
                        result.Add(symbol, linkedContextSymbolList.syntaxContext);
                    }
                }
            }

            return result;
        }

        protected async Task<ImmutableArray<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<ISymbol> symbols)>> GetPerContextSymbols(
            Document document, int position, OptionSet options, IEnumerable<DocumentId> relatedDocuments, bool preselect, CancellationToken cancellationToken)
        {
            var perContextSymbols = ArrayBuilder<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<ISymbol> symbols)>.GetInstance();
            foreach (var relatedDocumentId in relatedDocuments)
            {
                var relatedDocument = document.Project.Solution.GetDocument(relatedDocumentId);
                var context = await GetOrCreateContext(relatedDocument, position, cancellationToken).ConfigureAwait(false);

                if (IsCandidateProject(context, cancellationToken))
                {
                    var symbols = await GetSymbolsWorker(position, preselect, context, options, cancellationToken).ConfigureAwait(false);
                    perContextSymbols.Add((relatedDocument.Id, context, symbols));
                }
            }

            return perContextSymbols.ToImmutableAndFree();
        }

        private bool IsCandidateProject(SyntaxContext context, CancellationToken cancellationToken)
        {
            var syntaxFacts = context.GetLanguageService<ISyntaxFactsService>();
            return !syntaxFacts.IsInInactiveRegion(context.SyntaxTree, context.Position, cancellationToken);
        }

        protected OptionSet GetUpdatedRecommendationOptions(OptionSet options, string language)
        {
            var filterOutOfScopeLocals = options.GetOption(CompletionControllerOptions.FilterOutOfScopeLocals);
            var hideAdvancedMembers = options.GetOption(CompletionOptions.HideAdvancedMembers, language);

            return options
                .WithChangedOption(RecommendationOptions.FilterOutOfScopeLocals, language, filterOutOfScopeLocals)
                .WithChangedOption(RecommendationOptions.HideAdvancedMembers, language, hideAdvancedMembers);
        }

        protected abstract Task<SyntaxContext> CreateContext(Document document, int position, CancellationToken cancellationToken);

        private Task<SyntaxContext> GetOrCreateContext(Document document, int position, CancellationToken cancellationToken)
        {
            lock (s_cacheGate)
            {
                return s_cachedDocuments.GetValue(document, d => CreateContext(d, position, cancellationToken));
            }
        }

        /// <summary>
        /// Given a list of symbols, determine which are not recommended at the same position in linked documents.
        /// </summary>
        /// <param name="symbolToContext">The symbols recommended in the active context.</param>
        /// <param name="linkedContextSymbolLists">The symbols recommended in linked documents</param>
        /// <returns>The list of projects each recommended symbol did NOT appear in.</returns>
        protected Dictionary<ISymbol, List<ProjectId>> FindSymbolsMissingInLinkedContexts(
            Dictionary<ISymbol, SyntaxContext> symbolToContext,
            ImmutableArray<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<ISymbol> symbols)> linkedContextSymbolLists)
        {
            var missingSymbols = new Dictionary<ISymbol, List<ProjectId>>(LinkedFilesSymbolEquivalenceComparer.Instance);

            foreach (var linkedContextSymbolList in linkedContextSymbolLists)
            {
                var symbolsMissingInLinkedContext = symbolToContext.Keys.Except(linkedContextSymbolList.symbols, LinkedFilesSymbolEquivalenceComparer.Instance);
                foreach (var missingSymbol in symbolsMissingInLinkedContext)
                {
                    missingSymbols.GetOrAdd(missingSymbol, m => new List<ProjectId>()).Add(linkedContextSymbolList.documentId.ProjectId);
                }
            }

            return missingSymbols;
        }

        public override Task<TextChange?> GetTextChangeAsync(
            Document document, CompletionItem selectedItem, char? ch, CancellationToken cancellationToken)
        {
            return Task.FromResult<TextChange?>(new TextChange(
                selectedItem.Span, GetInsertionText(selectedItem, ch)));
        }

        private string GetInsertionText(CompletionItem item, char? ch)
        {
            return ch == null
                ? SymbolCompletionItem.GetInsertionText(item)
                : GetInsertionText(item, ch.Value);
        }

        /// <summary>
        /// Override this if you want to provide customized insertion based on the character typed.
        /// </summary>
        protected virtual string GetInsertionText(CompletionItem item, char ch)
        {
            return SymbolCompletionItem.GetInsertionText(item);
        }
    }
}
