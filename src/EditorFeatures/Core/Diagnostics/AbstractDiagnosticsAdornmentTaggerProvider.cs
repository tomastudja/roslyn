﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.QuickInfo;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Workspaces;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.CodeAnalysis.Diagnostics
{
    internal abstract class AbstractDiagnosticsAdornmentTaggerProvider<TTag> :
        AbstractDiagnosticsTaggerProvider<TTag>
        where TTag : class, ITag
    {
        protected AbstractDiagnosticsAdornmentTaggerProvider(
            IThreadingContext threadingContext,
            IDiagnosticService diagnosticService,
            IDiagnosticAnalyzerService analyzerService,
            IGlobalOptionService globalOptions,
            ITextBufferVisibilityTracker? visibilityTracker,
            IAsynchronousOperationListenerProvider listenerProvider)
            : base(threadingContext, diagnosticService, analyzerService, globalOptions, visibilityTracker, listenerProvider.GetListener(FeatureAttribute.ErrorSquiggles))
        {
        }

        protected internal sealed override bool IsEnabled => true;

        protected internal sealed override ITagSpan<TTag>? CreateTagSpan(
            Workspace workspace, SnapshotSpan span, DiagnosticData data)
        {
            var errorTag = CreateTag(workspace, data);
            if (errorTag == null)
                return null;

            // Ensure the diagnostic has at least length 1.  Tags must have a non-empty length in order to actually show
            // up in the editor.
            var adjustedSpan = AdjustSnapshotSpan(span);
            if (adjustedSpan.Length == 0)
                return null;

            return new TagSpan<TTag>(adjustedSpan, errorTag);
        }

        protected static object CreateToolTipContent(Workspace workspace, DiagnosticData diagnostic)
        {
            Action? navigationAction = null;
            string? tooltip = null;
            if (workspace != null)
            {
                var helpLinkUri = diagnostic.GetValidHelpLinkUri();
                if (helpLinkUri != null)
                {
                    navigationAction = new QuickInfoHyperLink(workspace, helpLinkUri).NavigationAction;
                    tooltip = diagnostic.HelpLink;
                }
            }

            var diagnosticIdTextRun = navigationAction is null
                ? new ClassifiedTextRun(ClassificationTypeNames.Text, diagnostic.Id)
                : new ClassifiedTextRun(ClassificationTypeNames.Text, diagnostic.Id, navigationAction, tooltip);

            return new ContainerElement(
                ContainerElementStyle.Wrapped,
                new ClassifiedTextElement(
                    diagnosticIdTextRun,
                    new ClassifiedTextRun(ClassificationTypeNames.Punctuation, ":"),
                    new ClassifiedTextRun(ClassificationTypeNames.WhiteSpace, " "),
                    new ClassifiedTextRun(ClassificationTypeNames.Text, diagnostic.Message)));
        }

        // By default, tags must have at least length '1' so that they can be visible in the UI layer.
        protected virtual SnapshotSpan AdjustSnapshotSpan(SnapshotSpan span)
            => AdjustSnapshotSpan(span, minimumLength: 1, maximumLength: int.MaxValue);

        protected static SnapshotSpan AdjustSnapshotSpan(SnapshotSpan span, int minimumLength, int maximumLength)
        {
            var snapshot = span.Snapshot;

            // new length
            var length = Math.Min(Math.Max(span.Length, minimumLength), maximumLength);

            // make sure start + length is smaller than snapshot.Length and start is >= 0
            var start = Math.Max(0, Math.Min(span.Start, snapshot.Length - length));

            // make sure length is smaller than snapshot.Length which can happen if start == 0
            return new SnapshotSpan(snapshot, start, Math.Min(start + length, snapshot.Length) - start);
        }

        protected abstract TTag? CreateTag(Workspace workspace, DiagnosticData diagnostic);
    }
}
