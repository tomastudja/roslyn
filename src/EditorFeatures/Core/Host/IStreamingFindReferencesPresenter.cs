﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
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
        /// <param name="title">A title to display to the user in the presentation of the results.</param>
        /// <param name="supportsReferences">Whether or not showing references is supported.
        /// If true, then the presenter can group by definition, showing references underneath.
        /// It can also show messages about no references being found at the end of the search.
        /// If false, the presenter will not group by definitions, and will show the definition
        /// items in isolation.</param>
        /// <param name="cancellationToken">External cancellation token controlling whether finding shoudl be canceled
        /// or not.  This will be combined with a cancellation token owned by the <see cref="FindUsagesContext"/>.
        /// Callers should consider the cancellation token returned to be the source of truth for
        /// cancellation from that point on.</param>
        (FindUsagesContext context, CancellationToken cancellationToken) StartSearch(string title, bool supportsReferences, CancellationToken cancellationToken);

        /// <summary>
        /// Call this method to display the Containing Type, Containing Member, or Kind columns
        /// </summary>
        (FindUsagesContext context, CancellationToken cancellationToken) StartSearchWithCustomColumns(string title, bool supportsReferences, bool includeContainingTypeAndMemberColumns, bool includeKindColumn, CancellationToken cancellationToken);

        /// <summary>
        /// Clears all the items from the presenter.
        /// </summary>
        void ClearAll();
    }

    internal static class IStreamingFindUsagesPresenterExtensions
    {
        /// <summary>
        /// If there's only a single item, navigates to it.  Otherwise, presents all the
        /// items to the user.
        /// </summary>
        public static async Task<bool> TryNavigateToOrPresentItemsAsync(
            this IStreamingFindUsagesPresenter presenter,
            IThreadingContext threadingContext,
            Workspace workspace,
            string title,
            ImmutableArray<DefinitionItem> items,
            CancellationToken cancellationToken)
        {
            // Can only navigate or present items on UI thread.
            await threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Ignore any definitions that we can't navigate to.
            var definitions = items.WhereAsArray(d => d.CanNavigateTo(workspace, cancellationToken));

            // See if there's a third party external item we can navigate to.  If so, defer 
            // to that item and finish.
            var externalItems = definitions.WhereAsArray(d => d.IsExternal);
            foreach (var item in externalItems)
            {
                // If we're directly going to a location we need to activate the preview so
                // that focus follows to the new cursor position. This behavior is expected
                // because we are only going to navigate once successfully
                if (item.TryNavigateTo(workspace, showInPreviewTab: true, activateTab: true, cancellationToken))
                {
                    return true;
                }
            }

            var nonExternalItems = definitions.WhereAsArray(d => !d.IsExternal);
            if (nonExternalItems.Length == 0)
            {
                return false;
            }

            if (nonExternalItems.Length == 1 &&
                nonExternalItems[0].SourceSpans.Length <= 1)
            {
                // There was only one location to navigate to.  Just directly go to that location. If we're directly
                // going to a location we need to activate the preview so that focus follows to the new cursor position.

                return nonExternalItems[0].TryNavigateTo(workspace, showInPreviewTab: true, activateTab: true, cancellationToken);
            }

            if (presenter != null)
            {
                // We have multiple definitions, or we have definitions with multiple locations. Present this to the
                // user so they can decide where they want to go to.  If we cancel this will trigger the context to
                // cancel as well.
                var (context, combinedCancellationToken) = presenter.StartSearch(title, supportsReferences: false, cancellationToken);
                cancellationToken = combinedCancellationToken;
                try
                {
                    foreach (var definition in nonExternalItems)
                        await context.OnDefinitionFoundAsync(definition, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await context.OnCompletedAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return true;
        }
    }
}
