﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Options;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.BraceMatching
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [TagType(typeof(BraceHighlightTag))]
    internal class BraceHighlightingViewTaggerProvider : AsynchronousViewTaggerProvider<BraceHighlightTag>
    {
        private readonly IBraceMatchingService _braceMatcherService;

        public override bool RemoveTagsThatIntersectEdits => true;
        public override SpanTrackingMode SpanTrackingMode => SpanTrackingMode.EdgeExclusive;
        public override IEnumerable<Option<bool>> Options => SpecializedCollections.SingletonEnumerable(InternalFeatureOnOffOptions.BraceMatching);

        [ImportingConstructor]
        public BraceHighlightingViewTaggerProvider(
            IBraceMatchingService braceMatcherService,
            IForegroundNotificationService notificationService,
            [ImportMany] IEnumerable<Lazy<IAsynchronousOperationListener, FeatureMetadata>> asyncListeners)
                : base(new AggregateAsynchronousOperationListener(asyncListeners, FeatureAttribute.BraceHighlighting), notificationService)
        {
            _braceMatcherService = braceMatcherService;
        }

        public override ITaggerEventSource CreateEventSource(ITextView textView, ITextBuffer subjectBuffer)
        {
            return TaggerEventSources.Compose(
                TaggerEventSources.OnTextChanged(subjectBuffer, TaggerDelay.NearImmediate),
                TaggerEventSources.OnCaretPositionChanged(textView, subjectBuffer, TaggerDelay.NearImmediate));
        }

        public override Task ProduceTagsAsync(DocumentSnapshotSpan documentSnapshotSpan, int? caretPosition, Action<ITagSpan<BraceHighlightTag>> addTag, CancellationToken cancellationToken)
        {
            var document = documentSnapshotSpan.Document;
            if (!caretPosition.HasValue || document == null)
            {
                return SpecializedTasks.EmptyTask;
            }

            return ProduceTagsAsync(document, documentSnapshotSpan.SnapshotSpan.Snapshot, caretPosition.Value, addTag, cancellationToken);
        }

        internal async Task ProduceTagsAsync(
            Document document,
            ITextSnapshot snapshot,
            int position,
            Action<ITagSpan<BraceHighlightTag>> addTag,
            CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Tagger_BraceHighlighting_TagProducer_ProduceTags, cancellationToken))
            {
                await ProduceTagsForBracesAsync(document, snapshot, position, addTag, rightBrace: false, cancellationToken: cancellationToken).ConfigureAwait(false);
                await ProduceTagsForBracesAsync(document, snapshot, position - 1, addTag, rightBrace: true, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ProduceTagsForBracesAsync(
            Document document,
            ITextSnapshot snapshot,
            int position,
            Action<ITagSpan<BraceHighlightTag>> addTag,
            bool rightBrace,
            CancellationToken cancellationToken)
        {
            if (position >= 0 && position < snapshot.Length)
            {
                var braces = await _braceMatcherService.GetMatchingBracesAsync(document, position, cancellationToken).ConfigureAwait(false);
                if (braces.HasValue)
                {
                    if ((!rightBrace && braces.Value.LeftSpan.Start == position) ||
                        (rightBrace && braces.Value.RightSpan.Start == position))
                    {
                        addTag(snapshot.GetTagSpan(braces.Value.LeftSpan.ToSpan(), BraceHighlightTag.StartTag));
                        addTag(snapshot.GetTagSpan(braces.Value.RightSpan.ToSpan(), BraceHighlightTag.EndTag));
                    }
                }
            }
        }
    }
}