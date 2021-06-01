﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.Implementation.Adornments;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Microsoft.CodeAnalysis.Editor.InlineErrors
{
    internal class InlineErrorTag : GraphicsTag
    {
        public readonly string ErrorType;
        private readonly DiagnosticData _diagnostic;

        public InlineErrorTag(string errorType, DiagnosticData diagnostic, IEditorFormatMap editorFormatMap)
            : base(editorFormatMap)
        {
            ErrorType = errorType;
            _diagnostic = diagnostic;
        }

        public override GraphicsResult GetGraphics(IWpfTextView view, Geometry bounds, TextFormattingRunProperties format)
        {
            var block = new TextBlock
            {
                FontFamily = format.Typeface.FontFamily,
                FontSize = 0.75 * format.FontRenderingEmSize,
                FontStyle = FontStyles.Normal,
                Foreground = format.ForegroundBrush,
                // Adds a little bit of padding to the left of the text relative to the border to make the text seem
                // more balanced in the border
                Padding = new Thickness(left: 2, top: 0, right: 2, bottom: 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var id = new Run(_diagnostic.Id);
            var link = new Hyperlink(id)
            {
                NavigateUri = new Uri(_diagnostic.HelpLink)
            };

            link.RequestNavigate += HandleRequestNavigate;
            block.Inlines.Add(link);
            block.Inlines.Add(": " + _diagnostic.Message);
            block.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            block.Arrange(new Rect(block.DesiredSize));

            var border = new Border
            {
                Background = format.BackgroundBrush,
                Child = block,
                CornerRadius = new CornerRadius(2),
                // Highlighting lines are 2px buffer.  So shift us up by one from the bottom so we feel centered between them.
                Margin = new Thickness(0, top: 0, 0, bottom: 2),
                Padding = new Thickness(2)
            };

            border.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            view.ViewportWidthChanged += ViewportWidthChangedHandler;
            // Need to set these properties to avoid unnecessary reformatting because some dependancy properties
            // affect layout
            TextOptions.SetTextFormattingMode(border, TextOptions.GetTextFormattingMode(view.VisualElement));
            TextOptions.SetTextHintingMode(border, TextOptions.GetTextHintingMode(view.VisualElement));
            TextOptions.SetTextRenderingMode(border, TextOptions.GetTextRenderingMode(view.VisualElement));

            Canvas.SetTop(border, bounds.Bounds.Bottom - border.DesiredSize.Height);
            Canvas.SetLeft(border, view.ViewportWidth - border.DesiredSize.Width);

            return new GraphicsResult(border,
                () => view.ViewportWidthChanged -= ViewportWidthChangedHandler);

            void ViewportWidthChangedHandler(object s, EventArgs e)
            {
                Canvas.SetLeft(border, view.ViewportWidth - border.DesiredSize.Width);
            }
        }

        private void HandleRequestNavigate(object sender, RoutedEventArgs e)
        {
            var link = (Hyperlink)sender;
            var uri = link.NavigateUri.ToString();
            Process.Start(uri);
            e.Handled = true;
        }

        public void UpdateColor(TextFormattingRunProperties format, UIElement adornment)
        {
            var border = (Border)adornment;
            border.Background = format.BackgroundBrush;
            var block = (TextBlock)border.Child;
            block.Foreground = format.ForegroundBrush;
        }

        public override GraphicsResult GetGraphics(IWpfTextView view, Geometry bounds)
        {
            throw new NotImplementedException();
        }

        protected override Color? GetColor(IWpfTextView view, IEditorFormatMap editorFormatMap)
        {
            if (ErrorType is PredefinedErrorTypeNames.SyntaxError)
            {
                return Colors.Red;
            }
            else if (ErrorType is PredefinedErrorTypeNames.Warning)
            {
                return Colors.Green;
            }
            else
            {
                return Colors.Purple;
            }
        }
    }
}
