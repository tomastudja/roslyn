﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Microsoft.CodeAnalysis.Editor.Wpf;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.InheritanceMargin;
using Microsoft.VisualStudio.Imaging.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph
{
    /// <summary>
    /// View model used to show the MenuItem for inheritance target.
    /// </summary>
    internal class TargetMenuItemViewModel : InheritanceMenuItemViewModel
    {
        /// <summary>
        /// The margin for the default case.
        /// </summary>
        private static Thickness s_defaultMargin = new Thickness(4, 1, 4, 1);

        /// <summary>
        /// The margin used when this target item needs to be indented. Its left margin is 20 because the width of the image
        /// moniker is 16.
        /// </summary>
        private static Thickness s_indentMargin = new Thickness(20, 1, 4, 1);

        /// <summary>
        /// DefinitionItem used for navigation.
        /// </summary>
        public DefinitionItem DefinitionItem { get; }

        /// <summary>
        /// Margin for the image moniker.
        /// </summary>
        public Thickness Margin { get; }

        // Internal for testing purpose
        internal TargetMenuItemViewModel(
            string displayContent,
            ImageMoniker imageMoniker,
            string automationName,
            DefinitionItem definitionItem,
            Thickness margin) : base(displayContent, imageMoniker, automationName)
        {
            DefinitionItem = definitionItem;
            Margin = margin;
        }

        public static TargetMenuItemViewModel Create(InheritanceTargetItem target, bool indent)
        {
            var displayContent = target.DisplayName;
            var imageMoniker = target.Glyph.GetImageMoniker();
            return new TargetMenuItemViewModel(
                displayContent,
                imageMoniker,
                displayContent,
                target.DefinitionItem,
                indent ? s_indentMargin : s_defaultMargin);
        }
    }
}
