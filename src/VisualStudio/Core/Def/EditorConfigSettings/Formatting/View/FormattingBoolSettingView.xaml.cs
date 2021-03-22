﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis.Editor.EditorConfigSettings.Data;

namespace Microsoft.VisualStudio.LanguageServices.EditorConfigSettings.Formatting.View
{
    /// <summary>
    /// Interaction logic for WhitespaceValueSettingControl.xaml
    /// </summary>
    internal partial class FormattingBoolSettingView : UserControl
    {
        private readonly FormattingSetting _setting;

        public FormattingBoolSettingView(FormattingSetting setting)
        {
            InitializeComponent();
            _setting = setting;

            if (setting.GetValue() is bool value)
            {
                RootCheckBox.IsChecked = value;
            }
        }

        private void CheckBoxChanged(object sender, RoutedEventArgs e)
        {
            _setting.SetValue(RootCheckBox.IsChecked == true);
        }
    }
}
