﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.DocumentOutline
{
    internal partial class DocumentOutlineViewModel
    {
        private readonly record struct UIData(ExpansionOption ExpansionOption, SnapshotPoint? CaretPoint);

        /// <summary>
        /// Queue to batch up work to do to highlight the currently selected symbol node, expand/collapse nodes,
        /// then update the UI.
        /// </summary>
        private readonly AsyncBatchingWorkQueue<UIData> _updateModelQueue;

        public void EnqueueModelUpdateTask(ExpansionOption expansionOption, SnapshotPoint? caretPoint)
        {
            _updateModelQueue.AddWork(new UIData(expansionOption, caretPoint), cancelExistingWork: true);
        }

        private async ValueTask UpdateModelAsync(ImmutableSegmentedList<UIData> data, CancellationToken cancellationToken)
        {
            var (expansion, caretPoint) = data.Last();
            var model = await _filterAndSortQueue.WaitUntilCurrentBatchCompletesAsync().ConfigureAwait(false);
            if (model is null)
            {
                return;
            }

            var documentSymbolUIItems = DocumentOutlineHelper.GetDocumentSymbolItemViewModels(model.DocumentSymbolData);

            DocumentSymbolItemViewModel? symbolToSelect = null;
            if (caretPoint.HasValue)
            {
                symbolToSelect = DocumentOutlineHelper.GetDocumentNodeToSelect(documentSymbolUIItems, model.OriginalSnapshot, caretPoint.Value);
            }

            // Expand/collapse nodes based on the given Expansion Option.
            if (expansion is not ExpansionOption.NoChange && DocumentSymbolUIItems.Any())
            {
                DocumentOutlineHelper.SetIsExpanded(documentSymbolUIItems, DocumentSymbolUIItems, expansion);
            }

            // Highlight the selected node if it exists, otherwise unselect all nodes (required so that the view does not select a node by default).
            if (symbolToSelect is not null)
            {
                // Expand all ancestors first to ensure the selected node will be visible.
                DocumentOutlineHelper.ExpandAncestors(documentSymbolUIItems, symbolToSelect.RangeSpan);
                symbolToSelect.IsSelected = true;
            }
            else
            {
                // On Document Outline Control initialization, SymbolTree.ItemsSource is null
                if (DocumentSymbolUIItems.Any())
                    DocumentOutlineHelper.UnselectAll(DocumentSymbolUIItems);
            }

            DocumentSymbolUIItems = new ObservableCollection<DocumentSymbolItemViewModel>(documentSymbolUIItems);
        }
    }
}
