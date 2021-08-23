﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin.MarginGlyph;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.InheritanceMargin
{
    /// <summary>
    /// Manager controls all the glyphs of Inheritance Margin in <see cref="InheritanceMarginViewMargin"/>.
    /// </summary>
    internal class InheritanceGlyphManager : ForegroundThreadAffinitizedObject, IDisposable
    {
        private readonly IWpfTextView _textView;
        private readonly IThreadingContext _threadingContext;
        private readonly IStreamingFindUsagesPresenter _streamingFindUsagesPresenter;
        private readonly ClassificationTypeMap _classificationTypeMap;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IUIThreadOperationExecutor _operationExecutor;
        private readonly IEditorFormatMap _editorFormatMap;
        private Dictionary<InheritanceMarginGlyph, SnapshotSpan> _glyphToTaggedSpan;
        private readonly Canvas _glyphsContainer;

        // We want to our glyphs to have the same background color as the glyphs in GlyphMargin.
        private const string GlyphMarginName = "Indicator Margin";

        // The same width and height as the margin of indicator margin.
        private const double HeighAndWidthOfTheGlyph = 17;

        public InheritanceGlyphManager(
            IWpfTextView textView,
            IThreadingContext threadingContext,
            IStreamingFindUsagesPresenter streamingFindUsagesPresenter,
            ClassificationTypeMap classificationTypeMap,
            IClassificationFormatMap classificationFormatMap,
            IUIThreadOperationExecutor operationExecutor,
            IEditorFormatMap editorFormatMap,
            Canvas canvas) : base(threadingContext, assertIsForeground: true)
        {
            _textView = textView;
            _threadingContext = threadingContext;
            _streamingFindUsagesPresenter = streamingFindUsagesPresenter;
            _classificationTypeMap = classificationTypeMap;
            _classificationFormatMap = classificationFormatMap;
            _operationExecutor = operationExecutor;
            _editorFormatMap = editorFormatMap;
            _glyphsContainer = canvas;
            _editorFormatMap.FormatMappingChanged += FormatMappingChanged;

            _glyphToTaggedSpan = new Dictionary<InheritanceMarginGlyph, SnapshotSpan>();
            UpdateBackgroundColor();
        }

        public void AddGlyph(InheritanceMarginTag tag, SnapshotSpan span)
        {
            AssertIsForeground();
            var lines = _textView.TextViewLines;
            if (GetStartingLine(lines, span) is IWpfTextViewLine line)
            {
                var glyph = CreateNewGlyph(tag);
                glyph.Height = HeighAndWidthOfTheGlyph;
                glyph.Width = HeighAndWidthOfTheGlyph;
                SetTop(line, glyph);
                _glyphToTaggedSpan[glyph] = span;
                _glyphsContainer.Children.Add(glyph);
            }
        }

        public void RemoveGlyph(SnapshotSpan snapshotSpan)
        {
            AssertIsForeground();
            var marginsToRemove = _glyphToTaggedSpan
                .Where(kvp => snapshotSpan.IntersectsWith(kvp.Value))
                .ToImmutableArray();
            foreach (var (margin, span) in marginsToRemove)
            {
                _glyphsContainer.Children.Remove(margin);
                _glyphToTaggedSpan.Remove(margin);
            }
        }

        public void SetSnapshotAndUpdate(ITextSnapshot snapshot, IList<ITextViewLine> newOrReformattedLines, IList<ITextViewLine> translatedLines)
        {
            AssertIsForeground();
            if (_glyphToTaggedSpan.Count > 0)
            {
                // Go through all the existing visuals and invalidate or transform as appropriate.
                var glyphToTaggedSpanBuilder = new Dictionary<InheritanceMarginGlyph, SnapshotSpan>(_glyphToTaggedSpan.Count);

                foreach (var (glyph, span) in _glyphToTaggedSpan)
                {
                    var newSpan = span.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);
                    if (!_textView.TextViewLines.IntersectsBufferSpan(newSpan) || (GetStartingLine(newOrReformattedLines, span) != null))
                    {
                        //Either visual is no longer visible or it crosses a line
                        //that was reformatted.
                        _glyphsContainer.Children.Remove(glyph);
                    }
                    else
                    {
                        glyphToTaggedSpanBuilder[glyph] = newSpan;
                        var line = GetStartingLine(translatedLines, span);
                        if (line != null)
                        {
                            SetTop(line, glyph);
                        }
                    }
                }

                _glyphToTaggedSpan = glyphToTaggedSpanBuilder;
            }
        }

        private void SetTop(ITextViewLine line, InheritanceMarginGlyph glyph)
            => Canvas.SetTop(glyph, line.TextTop - _textView.ViewportTop);

        private static ITextViewLine? GetStartingLine(IList<ITextViewLine> lines, Span span)
        {
            if (lines.Count > 0)
            {
                var low = 0;
                var high = lines.Count;
                while (low < high)
                {
                    var middle = (low + high) / 2;
                    var middleLine = lines[middle];
                    if (span.Start < middleLine.Start)
                        high = middle;
                    else if (span.Start >= middleLine.EndIncludingLineBreak)
                        low = middle + 1;
                    else
                        return middleLine;
                }

                var lastLine = lines[lines.Count - 1];
                if ((lastLine.EndIncludingLineBreak == lastLine.Snapshot.Length) && (span.Start == lastLine.EndIncludingLineBreak))
                {
                    // As a special case, if the last line ends at the end of the buffer and the span starts at the end of the buffer
                    // as well, treat is as crossing the last line in the buffer.
                    return lastLine;
                }
            }

            return null;
        }

        private InheritanceMarginGlyph CreateNewGlyph(InheritanceMarginTag tag)
            => new InheritanceMarginGlyph(
                _threadingContext,
                _streamingFindUsagesPresenter,
                _classificationTypeMap,
                _classificationFormatMap,
                _operationExecutor,
                tag,
                _textView);

        private void FormatMappingChanged(object sender, FormatItemsEventArgs e)
            => UpdateBackgroundColor();

        private void UpdateBackgroundColor()
        {
            AssertIsForeground();
            var resourceDictionary = _editorFormatMap.GetProperties(GlyphMarginName);
            if (resourceDictionary.Contains(EditorFormatDefinition.BackgroundColorId))
            {
                var backgroundColor = (Color)resourceDictionary[EditorFormatDefinition.BackgroundColorId];
                // Set background color for all the glyphs
                ImageThemingUtilities.SetImageBackgroundColor(_glyphsContainer, backgroundColor);
            }
        }

        public void Dispose()
        {
            _editorFormatMap.FormatMappingChanged -= FormatMappingChanged;
        }
    }
}
