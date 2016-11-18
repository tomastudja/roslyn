﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindUsages;

namespace Microsoft.CodeAnalysis.Editor.Host
{
    /// <summary>
    /// API for hosts to provide if they can present FindUsages results in a streaming manner.
    /// i.e. if they support showing results as they are found instead of after all of the results
    /// are found.
    /// </summary>
    internal interface IStreamingFindUsagesPresenter
    {
        /// <summary>
        /// Tells the presenter that a search is starting.  The returned <see cref="FindUsagesContext"/>
        /// is used to push information about the search into.  i.e. when a reference is found
        /// <see cref="FindUsagesContext.OnReferenceFoundAsync"/> should be called.  When the
        /// search completes <see cref="FindUsagesContext.OnCompletedAsync"/> should be called. 
        /// etc. etc.
        /// </summary>
        FindUsagesContext StartSearch(string title);
    }

    internal static class IStreamingFindUsagesPresenterExtensions
    {
        public static async Task NavigateToOrPresentItemsAsync(
            this IStreamingFindUsagesPresenter presenter,
            string title, ImmutableArray<DefinitionItem> items)
        {
            // Ignore any definitions that we can't navigate to.
            var definitions = items.WhereAsArray(d => d.CanNavigateTo());

            // See if there's a third party external item we can navigate to.  If so, defer 
            // to that item and finish.
            var externalItems = definitions.WhereAsArray(d => d.IsExternal);
            foreach (var item in externalItems)
            {
                if (item.TryNavigateTo())
                {
                    return;
                }
            }

            var nonExternalItems = definitions.WhereAsArray(d => !d.IsExternal);
            if (nonExternalItems.Length == 0)
            {
                return;
            }

            if (nonExternalItems.Length == 1 &&
                nonExternalItems[0].SourceSpans.Length <= 1)
            {
                // There was only one location to navigate to.  Just directly go to that location.
                nonExternalItems[0].TryNavigateTo();
                return;
            }

            // We have multiple definitions, or we have definitions with multiple locations.
            // Present this to the user so they can decide where they want to go to.

            var context = presenter.StartSearch(title);
            foreach (var definition in nonExternalItems)
            {
                await context.OnDefinitionFoundAsync(definition).ConfigureAwait(false);
            }

            // Note: we don't need to put this in a finally.  The only time we might not hit
            // this is if cancellation or another error gets thrown.  In the former case,
            // that means that a new search has started.  We don't care about telling the
            // context it has completed.  In the latter case somethign wrong has happened
            // and we don't want to run any more code code in this particular context.
            await context.OnCompletedAsync().ConfigureAwait(false);
        }
    }
}