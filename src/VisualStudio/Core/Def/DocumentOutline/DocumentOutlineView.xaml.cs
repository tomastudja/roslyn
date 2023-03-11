﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices.Implementation;
using Microsoft.VisualStudio.LanguageServices.Utilities;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.LanguageServices.DocumentOutline
{
    /// <summary>
    /// Interaction logic for DocumentOutlineView.xaml
    /// All operations happen on the UI thread for visual studio
    /// </summary>
    internal sealed partial class DocumentOutlineView : UserControl, IDisposable
    {
        private readonly IThreadingContext _threadingContext;
        private readonly VsCodeWindowViewTracker _viewTracker;
        private readonly DocumentOutlineViewModel _viewModel;

        public DocumentOutlineView(
            IThreadingContext threadingContext,
            VsCodeWindowViewTracker viewTracker,
            DocumentOutlineViewModel viewModel)
        {
            _threadingContext = threadingContext;
            _viewTracker = viewTracker;
            _viewModel = viewModel;

            DataContext = _viewModel;
            InitializeComponent();
            UpdateSort(SortOption.Location); // Set default sort for top-level items

            viewTracker.CaretMovedOrActiveViewChanged += ViewTracker_CaretMovedOrActiveViewChanged;
        }

        public void Dispose()
        {
            _viewTracker.CaretMovedOrActiveViewChanged -= ViewTracker_CaretMovedOrActiveViewChanged;
            _viewModel.Dispose();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => _viewModel.SearchText = SearchBox.Text;

        private void ExpandAll(object sender, RoutedEventArgs e)
            => _viewModel.ExpandOrCollapseAll(true);

        private void CollapseAll(object sender, RoutedEventArgs e)
            => _viewModel.ExpandOrCollapseAll(false);

        private void SortByName(object sender, EventArgs e)
            => UpdateSort(SortOption.Name);

        private void SortByOrder(object sender, EventArgs e)
            => UpdateSort(SortOption.Location);

        private void SortByType(object sender, EventArgs e)
            => UpdateSort(SortOption.Type);

        private void UpdateSort(SortOption sortOption)
        {
            _threadingContext.ThrowIfNotOnUIThread();

            // Log which sort option was used
            Logger.Log(sortOption switch
            {
                SortOption.Name => FunctionId.DocumentOutline_SortByName,
                SortOption.Location => FunctionId.DocumentOutline_SortByOrder,
                SortOption.Type => FunctionId.DocumentOutline_SortByType,
                _ => throw new NotImplementedException(),
            }, logLevel: LogLevel.Information);

            // "DocumentSymbolItems" is the key name we specified for our CollectionViewSource in the XAML file
            var collectionView = ((CollectionViewSource)FindResource("DocumentSymbolItems")).View;

            // Defer changes until all the properties have been set
            using (var _ = collectionView.DeferRefresh())
            {
                // Update top-level sorting options for our tree view
                collectionView.SortDescriptions.UpdateSortDescription(sortOption);

                // Set the sort option property to begin live-sorting
                _viewModel.SortOption = sortOption;
            }

            // Queue a refresh now that everything is set.
            collectionView.Refresh();
        }

        /// <summary>
        /// When a symbol node in the window is selected via the keyboard, move the caret to its position in the latest active text view.
        /// </summary>
        private void SymbolTreeItem_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            _threadingContext.ThrowIfNotOnUIThread();

            // This is a user-initiated navigation
            if (!_viewModel.IsNavigating && e.OriginalSource is TreeViewItem { DataContext: DocumentSymbolDataViewModel symbolModel })
            {
                // let the view model know that we are initiating navigation.
                _viewModel.IsNavigating = true;
                try
                {
                    var textView = _viewTracker.GetActiveView();
                    Assumes.NotNull(textView);
                    textView.TryMoveCaretToAndEnsureVisible(
                        symbolModel.Data.SelectionRangeSpan.TranslateTo(textView.TextSnapshot, SpanTrackingMode.EdgeInclusive).Start);
                }
                finally
                {
                    _viewModel.IsNavigating = false;
                }
            }
        }

        /// <summary>
        /// On caret position change, highlight the corresponding symbol node in the window and update the view.
        /// </summary>
        private void ViewTracker_CaretMovedOrActiveViewChanged(object sender, EventArgs e)
        {
            _threadingContext.ThrowIfNotOnUIThread();
            _viewModel.ExpandAndSelectItemAtCaretPosition();
        }
    }
}
