﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Microsoft.VisualStudio.LanguageServices.StackTraceExplorer
{
    /// <summary>
    /// Interaction logic for CallstackExplorerRoot.xaml
    /// </summary>
    internal partial class StackTraceExplorerRoot : UserControl
    {
        private readonly StackTraceExplorerRootViewModel _viewModel;

        public StackTraceExplorerRoot(StackTraceExplorerRootViewModel viewModel)
        {
            DataContext = _viewModel = viewModel;

            InitializeComponent();
            DataObject.AddPastingHandler(this, OnPaste);
        }

        private void CommandBinding_OnPaste(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
            => OnPaste();

        internal void OnClear()
        {
            _viewModel.SelectedTab?.Content.OnClear();
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
            => OnPaste();

        public void OnPaste()
        {
            _viewModel.OnPaste();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (sender is StackTraceExplorerTab tab)
            {
                tab.CloseClick.Execute(null);
            }
        }
    }
}
