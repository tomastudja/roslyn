﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion.Log;
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
    internal abstract class AbstractSymbolCompletionProvider : LSPCompletionProvider
    {
        // PERF: Many CompletionProviders derive AbstractSymbolCompletionProvider and therefore
        // compute identical contexts. This actually shows up on the 2-core typing test.
        // Cache the most recent document/position/computed SyntaxContext to reduce repeat computation.
        private static readonly ConditionalWeakTable<Document, Tuple<int, AsyncLazy<SyntaxContext>>> s_cachedDocuments = new();

        private bool? _isTargetTypeCompletionFilterExperimentEnabled = null;

        protected AbstractSymbolCompletionProvider()
        {
        }

        protected abstract Task<SyntaxContext> CreateContextAsync(Document document, int position, CancellationToken cancellationToken);
        protected abstract Task<ImmutableArray<(ISymbol symbol, bool preselect)>> GetSymbolsAsync(CompletionContext? completionContext, SyntaxContext syntaxContext, int position, OptionSet options, CancellationToken cancellationToken);
        protected abstract (string displayText, string suffix, string insertionText) GetDisplayAndSuffixAndInsertionText(ISymbol symbol, SyntaxContext context);

        protected virtual CompletionItemRules GetCompletionItemRules(ImmutableArray<(ISymbol symbol, bool preselect)> symbols)
            => CompletionItemRules.Default;

        protected bool IsTargetTypeCompletionFilterExperimentEnabled(Workspace workspace)
        {
            if (!_isTargetTypeCompletionFilterExperimentEnabled.HasValue)
            {
                var experimentationService = workspace.Services.GetRequiredService<IExperimentationService>();
                _isTargetTypeCompletionFilterExperimentEnabled = experimentationService.IsExperimentEnabled(WellKnownExperimentNames.TargetTypedCompletionFilter);
            }

            return _isTargetTypeCompletionFilterExperimentEnabled == true;
        }

        /// <param name="typeConvertibilityCache">A cache to use for repeated lookups. This should be created with <see cref="SymbolEqualityComparer.Default"/>
        /// because we ignore nullability.</param>
        private static bool ShouldIncludeInTargetTypedCompletionList(
            ISymbol symbol,
            ImmutableArray<ITypeSymbol> inferredTypes,
            SemanticModel semanticModel,
            int position,
            Dictionary<ITypeSymbol, bool> typeConvertibilityCache)
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

            if (typeConvertibilityCache.TryGetValue(type, out var isConvertible))
            {
                return isConvertible;
            }

            typeConvertibilityCache[type] = CompletionUtilities.IsTypeImplicitlyConvertible(semanticModel.Compilation, type, inferredTypes);
            return typeConvertibilityCache[type];
        }

        /// <summary>
        /// Given a list of symbols, and a mapping from each symbol to its original SemanticModel, 
        /// creates the list of completion items for them.
        /// </summary>
        private ImmutableArray<CompletionItem> CreateItems(
            CompletionContext completionContext,
            ImmutableArray<(ISymbol symbol, bool preselect)> symbols,
            Func<(ISymbol symbol, bool preselect), SyntaxContext> contextLookup,
            Dictionary<ISymbol, List<ProjectId>>? invalidProjectMap,
            List<ProjectId>? totalProjects,
            TelemetryCounter telemetryCounter)
        {
            // We might get symbol w/o name but CanBeReferencedByName is still set to true, 
            // need to filter them out.
            // https://github.com/dotnet/roslyn/issues/47690
            var symbolGroups = from symbol in symbols
                               let texts = GetDisplayAndSuffixAndInsertionText(symbol.symbol, contextLookup(symbol))
                               where !string.IsNullOrWhiteSpace(texts.displayText)
                               group symbol by texts into g
                               select g;

            var itemListBuilder = ImmutableArray.CreateBuilder<CompletionItem>();
            var typeConvertibilityCache = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);

            foreach (var symbolGroup in symbolGroups)
            {
                var includeItemInTargetTypedCompletion = false;
                var arbitraryFirstContext = contextLookup(symbolGroup.First());
                var symbolList = symbolGroup.ToImmutableArray();

                if (IsTargetTypeCompletionFilterExperimentEnabled(arbitraryFirstContext.Workspace))
                {
                    var tick = Environment.TickCount;

                    includeItemInTargetTypedCompletion = TryFindFirstSymbolMatchesTargetTypes(contextLookup, symbolList, typeConvertibilityCache, out var index);
                    if (includeItemInTargetTypedCompletion && index > 0)
                    {
                        // This would ensure a symbol matches target types to be used for description if there's any,
                        // assuming the default implementation of GetDescriptionWorkerAsync is used.
                        var firstMatch = symbolList[index];
                        symbolList = symbolList.RemoveAt(index);
                        symbolList = symbolList.Insert(0, firstMatch);
                    }

                    telemetryCounter.AddTick(Environment.TickCount - tick);
                }

                var item = CreateItem(
                    completionContext, symbolGroup.Key.displayText, symbolGroup.Key.suffix, symbolGroup.Key.insertionText,
                    symbolList, arbitraryFirstContext, invalidProjectMap, totalProjects);

                if (includeItemInTargetTypedCompletion)
                {
                    item = item.AddTag(WellKnownTags.TargetTypeMatch);
                }

                itemListBuilder.Add(item);
            }

            return itemListBuilder.ToImmutable();
        }

        protected static bool TryFindFirstSymbolMatchesTargetTypes(
            Func<(ISymbol symbol, bool preselect), SyntaxContext> contextLookup,
            ImmutableArray<(ISymbol symbol, bool preselect)> symbolList,
            Dictionary<ITypeSymbol, bool> typeConvertibilityCache,
            out int index)
        {
            for (index = 0; index < symbolList.Length; ++index)
            {
                var symbol = symbolList[index];
                var syntaxContext = contextLookup(symbol);
                if (ShouldIncludeInTargetTypedCompletionList(symbol.symbol, syntaxContext.InferredTypes, syntaxContext.SemanticModel, syntaxContext.Position, typeConvertibilityCache))
                    break;
            }

            return index < symbolList.Length;
        }

        /// <summary>
        /// Given a Symbol, creates the completion item for it.
        /// </summary>
        private CompletionItem CreateItem(
            CompletionContext completionContext,
            string displayText,
            string displayTextSuffix,
            string insertionText,
            ImmutableArray<(ISymbol symbol, bool preselect)> symbols,
            SyntaxContext context,
            Dictionary<ISymbol, List<ProjectId>>? invalidProjectMap,
            List<ProjectId>? totalProjects)
        {
            Contract.ThrowIfNull(symbols);

            SupportedPlatformData? supportedPlatformData = null;
            if (invalidProjectMap != null)
            {
                List<ProjectId>? invalidProjects = null;
                foreach (var symbol in symbols)
                {
                    if (invalidProjectMap.TryGetValue(symbol.symbol, out invalidProjects))
                        break;
                }

                if (invalidProjects != null)
                    supportedPlatformData = new SupportedPlatformData(invalidProjects, totalProjects, context.Workspace);
            }

            return CreateItem(
                completionContext, displayText, displayTextSuffix, insertionText, symbols, context, supportedPlatformData);
        }

        protected virtual CompletionItem CreateItem(
            CompletionContext completionContext,
            string displayText,
            string displayTextSuffix,
            string insertionText,
            ImmutableArray<(ISymbol symbol, bool preselect)> symbols,
            SyntaxContext context,
            SupportedPlatformData? supportedPlatformData)
        {
            var preselect = symbols.Any(t => t.preselect);
            return SymbolCompletionItem.CreateWithSymbolId(
                displayText: displayText,
                displayTextSuffix: displayTextSuffix,
                insertionText: insertionText,
                filterText: GetFilterText(symbols[0].symbol, displayText, context),
                contextPosition: context.Position,
                symbols: symbols.SelectAsArray(t => t.symbol),
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

        protected override Task<CompletionDescription> GetDescriptionWorkerAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
            => SymbolCompletionItem.GetDescriptionAsync(item, document, cancellationToken);

        public override async Task ProvideCompletionsAsync(CompletionContext completionContext)
        {
            try
            {
                var document = completionContext.Document;
                var position = completionContext.Position;
                var options = completionContext.Options;
                var cancellationToken = completionContext.CancellationToken;

                // If we were triggered by typing a character, then do a semantic check to make sure
                // we're still applicable.  If not, then return immediately.
                if (completionContext.Trigger.Kind == CompletionTriggerKind.Insertion)
                {
                    var isSemanticTriggerCharacter = await IsSemanticTriggerCharacterAsync(document, position - 1, cancellationToken).ConfigureAwait(false);
                    if (!isSemanticTriggerCharacter)
                    {
                        return;
                    }
                }

                completionContext.IsExclusive = IsExclusive();

                using (Logger.LogBlock(FunctionId.Completion_SymbolCompletionProvider_GetItemsWorker, cancellationToken))
                using (var telemetryCounter = new TelemetryCounter(ShouldCollectTelemetryForTargetTypeCompletion && IsTargetTypeCompletionFilterExperimentEnabled(document.Project.Solution.Workspace)))
                {
                    var syntaxContext = await GetOrCreateContextAsync(document, position, cancellationToken).ConfigureAwait(false);
                    var regularItems = await GetItemsAsync(completionContext, syntaxContext, document, position, options, telemetryCounter, cancellationToken).ConfigureAwait(false);

                    completionContext.AddItems(regularItems);
                }
            }
            catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e))
            {
                // nop
            }
        }

        private async Task<ImmutableArray<CompletionItem>> GetItemsAsync(
            CompletionContext completionContext,
            SyntaxContext syntaxContext,
            Document document,
            int position,
            OptionSet options,
            TelemetryCounter telemetryCounter,
            CancellationToken cancellationToken)
        {
            var relatedDocumentIds = document.GetLinkedDocumentIds();
            options = GetUpdatedRecommendationOptions(options, document.Project.Language);

            if (relatedDocumentIds.IsEmpty)
            {
                var itemsForCurrentDocument = await GetSymbolsAsync(completionContext, syntaxContext, position, options, cancellationToken).ConfigureAwait(false);
                return CreateItems(completionContext, itemsForCurrentDocument, _ => syntaxContext, invalidProjectMap: null, totalProjects: null, telemetryCounter);
            }

            var contextAndSymbolLists = await GetPerContextSymbolsAsync(completionContext, document, position, options, new[] { document.Id }.Concat(relatedDocumentIds), cancellationToken).ConfigureAwait(false);
            var symbolToContextMap = UnionSymbols(contextAndSymbolLists);
            var missingSymbolsMap = FindSymbolsMissingInLinkedContexts(symbolToContextMap, contextAndSymbolLists);
            var totalProjects = contextAndSymbolLists.Select(t => t.documentId.ProjectId).ToList();

            return CreateItems(
                completionContext, symbolToContextMap.Keys.ToImmutableArray(), symbol => symbolToContextMap[symbol], missingSymbolsMap, totalProjects, telemetryCounter);
        }

        protected virtual bool IsExclusive()
            => false;

        protected virtual Task<bool> IsSemanticTriggerCharacterAsync(Document document, int characterPosition, CancellationToken cancellationToken)
            => SpecializedTasks.True;

        private static Dictionary<(ISymbol symbol, bool preselect), SyntaxContext> UnionSymbols(
            ImmutableArray<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<(ISymbol symbol, bool preselect)> symbols)> linkedContextSymbolLists)
        {
            // To correctly map symbols back to their SyntaxContext, we do care about assembly identity.
            var result = new Dictionary<(ISymbol symbol, bool preselect), SyntaxContext>(CompletionLinkedFilesSymbolEquivalenceComparer.Instance);

            // We don't care about assembly identity when creating the union.
            foreach (var (documentId, syntaxContext, symbols) in linkedContextSymbolLists)
            {
                // We need to use the SemanticModel any particular symbol came from in order to generate its description correctly.
                // Therefore, when we add a symbol to set of union symbols, add a mapping from it to its SyntaxContext.
                foreach (var symbol in symbols.GroupBy(s => new { s.symbol.Name, s.symbol.Kind }).Select(g => g.First()))
                {
                    if (!result.ContainsKey(symbol))
                        result.Add(symbol, syntaxContext);
                }
            }

            return result;
        }

        private async Task<ImmutableArray<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<(ISymbol symbol, bool preselect)> symbols)>> GetPerContextSymbolsAsync(
            CompletionContext completionContext, Document document, int position, OptionSet options, IEnumerable<DocumentId> relatedDocuments, CancellationToken cancellationToken)
        {
            using var _ = ArrayBuilder<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<(ISymbol symbol, bool preselect)> symbols)>.GetInstance(out var perContextSymbols);
            foreach (var relatedDocumentId in relatedDocuments)
            {
                var relatedDocument = document.Project.Solution.GetRequiredDocument(relatedDocumentId);
                var syntaxContext = await GetOrCreateContextAsync(relatedDocument, position, cancellationToken).ConfigureAwait(false);
                var symbols = await TryGetSymbolsForContextAsync(completionContext, syntaxContext, options, cancellationToken).ConfigureAwait(false);

                // Only consider contexts that are in active region
                if (symbols.HasValue)
                    perContextSymbols.Add((relatedDocument.Id, syntaxContext, symbols.Value));
            }

            return perContextSymbols.ToImmutable();
        }

        /// <summary>
        /// If current context is in active region, returns available symbols. Otherwise, returns null.
        /// </summary>
        protected async Task<ImmutableArray<(ISymbol symbol, bool preselect)>?> TryGetSymbolsForContextAsync(
            CompletionContext? completionContext, SyntaxContext syntaxContext, OptionSet options, CancellationToken cancellationToken)
        {
            var syntaxFacts = syntaxContext.GetLanguageService<ISyntaxFactsService>();
            return syntaxFacts.IsInInactiveRegion(syntaxContext.SyntaxTree, syntaxContext.Position, cancellationToken)
                ? null
                : await GetSymbolsAsync(completionContext, syntaxContext, syntaxContext.Position, options, cancellationToken).ConfigureAwait(false);
        }

        protected static OptionSet GetUpdatedRecommendationOptions(OptionSet options, string language)
        {
            var filterOutOfScopeLocals = options.GetOption(CompletionControllerOptions.FilterOutOfScopeLocals);
            var hideAdvancedMembers = options.GetOption(CompletionOptions.HideAdvancedMembers, language);

            return options
                .WithChangedOption(RecommendationOptions.FilterOutOfScopeLocals, language, filterOutOfScopeLocals)
                .WithChangedOption(RecommendationOptions.HideAdvancedMembers, language, hideAdvancedMembers);
        }

        private Task<SyntaxContext> GetOrCreateContextAsync(Document document, int position, CancellationToken cancellationToken)
        {
            lock (s_cachedDocuments)
            {
                var (cachedPosition, cachedLazyContext) = s_cachedDocuments.GetValue(
                    document, d => Tuple.Create(position, new AsyncLazy<SyntaxContext>(ct => CreateContextAsync(d, position, ct), cacheResult: true)));

                if (cachedPosition == position)
                {
                    return cachedLazyContext.GetValueAsync(cancellationToken);
                }

                var lazyContext = new AsyncLazy<SyntaxContext>(ct => CreateContextAsync(document, position, ct), cacheResult: true);
                s_cachedDocuments.Remove(document);
                s_cachedDocuments.Add(document, Tuple.Create(position, lazyContext));
                return lazyContext.GetValueAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Given a list of symbols, determine which are not recommended at the same position in linked documents.
        /// </summary>
        /// <param name="symbolToContext">The symbols recommended in the active context.</param>
        /// <param name="linkedContextSymbolLists">The symbols recommended in linked documents</param>
        /// <returns>The list of projects each recommended symbol did NOT appear in.</returns>
        protected static Dictionary<ISymbol, List<ProjectId>> FindSymbolsMissingInLinkedContexts(
            Dictionary<(ISymbol symbol, bool preselect), SyntaxContext> symbolToContext,
            ImmutableArray<(DocumentId documentId, SyntaxContext syntaxContext, ImmutableArray<(ISymbol symbol, bool preselect)> symbols)> linkedContextSymbolLists)
        {
            var missingSymbols = new Dictionary<ISymbol, List<ProjectId>>(LinkedFilesSymbolEquivalenceComparer.Instance);

            foreach (var (documentId, syntaxContext, symbols) in linkedContextSymbolLists)
            {
                var symbolsMissingInLinkedContext = symbolToContext.Keys.Except(symbols, CompletionLinkedFilesSymbolEquivalenceComparer.Instance);
                foreach (var (symbol, _) in symbolsMissingInLinkedContext)
                    missingSymbols.GetOrAdd(symbol, m => new List<ProjectId>()).Add(documentId.ProjectId);
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
            => SymbolCompletionItem.GetInsertionText(item);

        // This is used to decide which provider we'd collect target type completion telemetry from.
        protected virtual bool ShouldCollectTelemetryForTargetTypeCompletion => false;

        private class TelemetryCounter : IDisposable
        {
            private readonly bool _shouldReport;
            private int _tick;

            public TelemetryCounter(bool shouldReport)
                => _shouldReport = shouldReport;

            public void AddTick(int tick)
                => _tick += tick;

            public void Dispose()
            {
                if (_shouldReport)
                {
                    CompletionProvidersLogger.LogTargetTypeCompletionTicksDataPoint(_tick);
                }
            }
        }

        internal sealed class CompletionLinkedFilesSymbolEquivalenceComparer : IEqualityComparer<(ISymbol symbol, bool preselect)>
        {
            public static readonly CompletionLinkedFilesSymbolEquivalenceComparer Instance = new();

            public bool Equals((ISymbol symbol, bool preselect) x, (ISymbol symbol, bool preselect) y)
                => LinkedFilesSymbolEquivalenceComparer.Instance.Equals(x.symbol, y.symbol);

            public int GetHashCode((ISymbol symbol, bool preselect) obj)
                => LinkedFilesSymbolEquivalenceComparer.Instance.GetHashCode(obj.symbol);
        }
    }
}
