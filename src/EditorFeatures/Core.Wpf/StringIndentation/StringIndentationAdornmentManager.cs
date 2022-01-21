﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Windows.Shapes;
using Microsoft.CodeAnalysis.Editor.Implementation.Adornments;
using Microsoft.CodeAnalysis.Editor.Implementation.StringIndentation;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.StringIndentation
{
    internal partial class StringIndentationAdornmentManager : AbstractAdornmentManager<StringIndentationTag>
    {
        public StringIndentationAdornmentManager(
            IThreadingContext threadingContext,
            IWpfTextView textView,
            IViewTagAggregatorFactoryService tagAggregatorFactoryService,
            IAsynchronousOperationListener asyncListener,
            string adornmentLayerName)
            : base(threadingContext, textView, tagAggregatorFactoryService, asyncListener, adornmentLayerName)
        {
        }

        protected override void AddAdornmentsToAdornmentLayer_CallOnlyOnUIThread(NormalizedSnapshotSpanCollection changedSpanCollection)
        {
            // this method should only run on UI thread as we do WPF here.
            Contract.ThrowIfFalse(TextView.VisualElement.Dispatcher.CheckAccess());

            var viewSnapshot = TextView.TextSnapshot;
            var viewLines = TextView.TextViewLines;

            foreach (var changedSpan in changedSpanCollection)
            {
                if (!viewLines.IntersectsBufferSpan(changedSpan))
                    continue;

                var tagSpans = TagAggregator.GetTags(changedSpan);
                foreach (var tagMappingSpan in tagSpans)
                {
                    if (!ShouldDrawTag(changedSpan, tagMappingSpan, out _))
                        continue;

                    if (!TryMapToSingleSnapshotSpan(tagMappingSpan.Span, TextView.TextSnapshot, out var span))
                        continue;

                    if (!TryMapHoleSpans(tagMappingSpan.Tag.OrderedHoleSpans, out var orderedHoleSpans))
                        continue;

                    if (VisibleBlock.CreateVisibleBlock(span, orderedHoleSpans, TextView) is not VisibleBlock block)
                        continue;

                    var tag = tagMappingSpan.Tag;
                    var brush = tag.GetBrush(TextView);

                    foreach (var (start, end) in block.YSegments)
                    {
                        var line = new Line
                        {
                            SnapsToDevicePixels = true,
                            StrokeThickness = 1.0,
                            X1 = block.X,
                            X2 = block.X,
                            Y1 = start,
                            Y2 = end,
                            Stroke = brush,
                        };

                        AdornmentLayer.AddAdornment(
                            behavior: AdornmentPositioningBehavior.TextRelative,
                            visualSpan: span,
                            tag: block,
                            adornment: line,
                            removedCallback: delegate { });
                    }
                }
            }
        }

        private bool TryMapHoleSpans(
            ImmutableArray<SnapshotSpan> spans,
            out ImmutableArray<SnapshotSpan> result)
        {
            using var _ = ArrayBuilder<SnapshotSpan>.GetInstance(out var builder);
            foreach (var span in spans)
            {
                var mapped = MapUpToView(TextView, span);
                if (mapped == null)
                {
                    result = default;
                    return false;
                }

                builder.Add(mapped.Value);
            }

            result = builder.ToImmutable();
            return true;
        }

        private static SnapshotSpan? MapUpToView(ITextView textView, SnapshotSpan span)
        {
            // Must be called from the UI thread.
            var start = textView.GetPositionInView(span.Start);
            var end = textView.GetPositionInView(span.End);

            if (start == null || end == null || end < start)
                return null;

            return new SnapshotSpan(start.Value, end.Value);
        }
    }
}
