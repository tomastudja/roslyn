﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Shared.Options;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Highlighting
{
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(HighlightTag))]
    [ContentType(ContentTypeNames.CSharpContentType)]
    [ContentType(ContentTypeNames.VisualBasicContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal class HighlighterViewTaggerProvider : AsynchronousViewTaggerProvider<HighlightTag>
    {
        private readonly IHighlightingService _highlighterService;
        private readonly Lazy<IViewTaggerProvider> _asynchronousTaggerProvider;

        public override bool RemoveTagsThatIntersectEdits => true;
        public override bool IgnoreCaretMovementToExistingTag => true;
        public override SpanTrackingMode SpanTrackingMode => SpanTrackingMode.EdgeExclusive;
        public override IEnumerable<Option<bool>> Options => SpecializedCollections.SingletonEnumerable(InternalFeatureOnOffOptions.KeywordHighlight);

        [ImportingConstructor]
        public HighlighterViewTaggerProvider(
            IForegroundNotificationService notificationService,
            IHighlightingService highlighterService,
            [ImportMany] IEnumerable<Lazy<IAsynchronousOperationListener, FeatureMetadata>> asyncListeners)
            : base(new AggregateAsynchronousOperationListener(asyncListeners, FeatureAttribute.KeywordHighlighting), notificationService)
        {
            _highlighterService = highlighterService;
        }

        public override ITaggerEventSource CreateEventSource(ITextView textView, ITextBuffer subjectBuffer)
        {
            return TaggerEventSources.Compose(
                TaggerEventSources.OnTextChanged(subjectBuffer, TaggerDelay.OnIdle, reportChangedSpans: true),
                TaggerEventSources.OnCaretPositionChanged(textView, subjectBuffer, TaggerDelay.NearImmediate),
                TaggerEventSources.OnOptionChanged(subjectBuffer, FeatureOnOffOptions.KeywordHighlighting, TaggerDelay.NearImmediate));
        }

        public override ITagProducer<HighlightTag> CreateTagProducer()
        {
            return new HighlighterTagProducer(_highlighterService);
        }
    }
}