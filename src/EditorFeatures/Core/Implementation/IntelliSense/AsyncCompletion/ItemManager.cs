﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Roslyn.Utilities;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using RoslynCompletionList = Microsoft.CodeAnalysis.Completion.CompletionList;
using VSCompletionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.AsyncCompletion
{
    internal sealed partial class ItemManager : IAsyncCompletionItemManager
    {
        public const string AggressiveDefaultsMatchingOptionName = "AggressiveDefaultsMatchingOption";
        private const string CombinedSortedList = nameof(CombinedSortedList);

        private readonly RecentItemsManager _recentItemsManager;
        private readonly IGlobalOptionService _globalOptions;

        internal ItemManager(RecentItemsManager recentItemsManager, IGlobalOptionService globalOptions)
        {
            _recentItemsManager = recentItemsManager;
            _globalOptions = globalOptions;
        }

        public Task<ImmutableArray<VSCompletionItem>> SortCompletionListAsync(
            IAsyncCompletionSession session,
            AsyncCompletionSessionInitialDataSnapshot data,
            CancellationToken cancellationToken)
        {
            // This method is called exactly once, so use the opportunity to set a baseline for telemetry.
            if (session.TextView.Properties.TryGetProperty(CompletionSource.TargetTypeFilterExperimentEnabled, out bool isTargetTypeFilterEnabled) && isTargetTypeFilterEnabled)
            {
                AsyncCompletionLogger.LogSessionHasTargetTypeFilterEnabled();
                if (data.InitialList.Any(i => i.Filters.Any(f => f.DisplayText == FeaturesResources.Target_type_matches)))
                    AsyncCompletionLogger.LogSessionContainsTargetTypeFilter();
            }

            // Sort by default comparer of Roslyn CompletionItem
            var sortedItems = data.InitialList.OrderBy(GetOrAddRoslynCompletionItem).ToImmutableArray();
            return Task.FromResult(sortedItems);
        }

        public async Task<FilteredCompletionModel?> UpdateCompletionListAsync(
            IAsyncCompletionSession session,
            AsyncCompletionSessionDataSnapshot data,
            CancellationToken cancellationToken)
        {
            // As explained in more details in the comments for `CompletionSource.GetCompletionContextAsync`, expanded items might
            // not be provided upon initial trigger of completion to reduce typing delays, even if they are supposed to be included by default.
            // While we do not expect to run in to this scenario very often, we'd still want to minimize the impact on user experience of this feature
            // as best as we could when it does occur. So the solution we came up with is this: if we decided to not include expanded items (because the
            // computation is running too long,) we will let it run in the background as long as the completion session is still active. Then whenever
            // any user input that would cause the completion list to refresh, we will check the state of this background task and add expanded items as part
            // of the update if they are available.
            // There is a `CompletionContext.IsIncomplete` flag, which is only supported in LSP mode at the moment. Therefore we opt to handle the checking
            // and combining the items in Roslyn until the `IsIncomplete` flag is fully supported in classic mode.

            if (session.Properties.TryGetProperty<Task<(CompletionContext, RoslynCompletionList)>>(CompletionSource.ExpandedItemsTask, out var task)
                && task.Status == TaskStatus.RanToCompletion)
            {
                // Make sure the task is removed when Adding expanded items,
                // so duplicated items won't be added in subsequent list updates.
                session.Properties.RemoveProperty(CompletionSource.ExpandedItemsTask);

                var (expandedContext, expandedList) = await task.ConfigureAwait(false);
                if (expandedContext.Items.Length > 0)
                {
                    var _ = ArrayBuilder<VSCompletionItem>.GetInstance(expandedContext.Items.Length + data.InitialSortedList.Length, out var itemsBuilder);
                    itemsBuilder.AddRange(data.InitialSortedList);
                    itemsBuilder.AddRange(expandedContext.Items);
                    var combinedSortedList = itemsBuilder.OrderBy(GetOrAddRoslynCompletionItem).ToImmutableArray();

                    // Add expanded items into a combined list, and save it to be used for future updates during the same session.
                    session.Properties[CombinedSortedList] = combinedSortedList;
                    var combinedFilterStates = FilterSet.CombineFilterStates(expandedContext.Filters, data.SelectedFilters);

                    data = new(combinedSortedList, data.Snapshot, data.Trigger, data.InitialTrigger, combinedFilterStates,
                        data.IsSoftSelected, data.DisplaySuggestionItem, data.Defaults);
                }

                AsyncCompletionLogger.LogSessionWithDelayedImportCompletionIncludedInUpdate();
            }
            else if (session.Properties.TryGetProperty<ImmutableArray<VSCompletionItem>>(CombinedSortedList, out var combinedSortedList))
            {
                // Always use the previously saved combined list if available.
                data = new(combinedSortedList, data.Snapshot, data.Trigger, data.InitialTrigger, data.SelectedFilters,
                    data.IsSoftSelected, data.DisplaySuggestionItem, data.Defaults);
            }

            var updater = new CompletionListUpdater(session, data, _recentItemsManager, _globalOptions);
            return updater.UpdateCompletionList(cancellationToken);
        }

        private static RoslynCompletionItem GetOrAddRoslynCompletionItem(VSCompletionItem vsItem)
        {
            if (!vsItem.Properties.TryGetProperty(CompletionSource.RoslynItem, out RoslynCompletionItem roslynItem))
            {
                roslynItem = RoslynCompletionItem.Create(
                    displayText: vsItem.DisplayText,
                    filterText: vsItem.FilterText,
                    sortText: vsItem.SortText,
                    displayTextSuffix: vsItem.Suffix);

                vsItem.Properties.AddProperty(CompletionSource.RoslynItem, roslynItem);
            }

            return roslynItem;
        }
    }
}
