﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Highlighting
{
    /// <summary>
    /// A derivation of <see cref="ProducerPopulatedTagSource{TTag}" /> that only recomputes highlighting if you moved out
    /// of the existing set of highlights.
    /// </summary>
    internal sealed class HighlightingTagSource : ProducerPopulatedTagSource<HighlightTag>
    {
        public HighlightingTagSource(
            ITextView textView,
            ITextBuffer subjectBuffer,
            IAsynchronousTaggerDataSource<HighlightTag> dataSource,
            IAsynchronousOperationListener asyncListener,
            IForegroundNotificationService notificationService)
            : base(textView, subjectBuffer, dataSource, asyncListener, notificationService)
        {
        }

        protected override void RecalculateTagsOnChanged(TaggerEventArgs e)
        {
            if (e.Kind == PredefinedChangedEventKinds.CaretPositionChanged)
            {
                var caret = GetCaretPoint();

                if (caret.HasValue)
                {
                    // If it changed position and we're still in a tag, there's nothing more to do
                    var currentTags = GetTagIntervalTreeForBuffer(caret.Value.Snapshot.TextBuffer);
                    if (currentTags != null && currentTags.GetIntersectingSpans(new SnapshotSpan(caret.Value, 0)).Any())
                    {
                        return;
                    }
                }
            }

            base.RecalculateTagsOnChanged(e);
        }
    }
}
