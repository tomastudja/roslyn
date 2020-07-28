﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.DesignerAttribute;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Remote
{
    internal sealed partial class RemoteDesignerAttributeIncrementalAnalyzer : IncrementalAnalyzerBase
    {
        private const string DataKey = "DesignerAttributeData";

        /// <summary>
        /// Channel back to VS to inform it of the designer attributes we discover.
        /// </summary>
        private readonly RemoteEndPoint _endPoint;

        /// <summary>
        /// Keep track of the last information we reported.  We will avoid notifying the host if we recompute and these
        /// don't change.
        /// </summary>
        private readonly ConcurrentDictionary<DocumentId, (string? category, VersionStamp projectVersion)> _documentToLastReportedInformation =
            new ConcurrentDictionary<DocumentId, (string? category, VersionStamp projectVersion)>();

        public RemoteDesignerAttributeIncrementalAnalyzer(RemoteEndPoint endPoint)
        {
            _endPoint = endPoint;
        }

        public override async Task RemoveProjectAsync(ProjectId projectId, CancellationToken cancellationToken)
        {
            await _endPoint.InvokeAsync(
                nameof(IDesignerAttributeListener.OnProjectRemovedAsync),
                new object[] { projectId },
                cancellationToken).ConfigureAwait(false);

            foreach (var docId in _documentToLastReportedInformation.Keys.ToArray())
            {
                if (projectId == docId.ProjectId)
                    _documentToLastReportedInformation.TryRemove(docId, out _);
            }
        }

        public override Task RemoveDocumentAsync(DocumentId documentId, CancellationToken cancellationToken)
        {
            _documentToLastReportedInformation.TryRemove(documentId, out _);
            return Task.CompletedTask;
        }

        public override Task AnalyzeProjectAsync(Project project, bool semanticsChanged, InvocationReasons reasons, CancellationToken cancellationToken)
            => AnalyzeProjectAsync(project, specificDocument: null, cancellationToken);

        public override Task AnalyzeDocumentAsync(Document document, SyntaxNode? body, InvocationReasons reasons, CancellationToken cancellationToken)
        {
            // don't need to reanalyze file if just a method body was edited.  That can't
            // affect designer attributes.
            if (body != null)
                return Task.CompletedTask;

            // When we register our analyzer we will get called into for every document to
            // 'reanalyze' them all.  Ignore those as we would prefer to analyze the project
            // en-mass.
            if (reasons.Contains(PredefinedInvocationReasons.Reanalyze))
                return Task.CompletedTask;

            return AnalyzeProjectAsync(document.Project, document, cancellationToken);
        }

        private async Task AnalyzeProjectAsync(Project project, Document? specificDocument, CancellationToken cancellationToken)
        {
            if (!project.SupportsCompilation)
                return;

            // We need to reanalyze the project whenever it (or any of its dependencies) have
            // changed.  We need to know about dependencies since if a downstream project adds the
            // DesignerCategory attribute to a class, that can affect us when we examine the classes
            // in this project.
            var projectVersion = await project.GetDependentSemanticVersionAsync(cancellationToken).ConfigureAwait(false);

            // Now get all the values that actually changed and notify VS about them. We don't need
            // to tell it about the ones that didn't change since that will have no effect on the
            // user experience.
            var changedData = await ComputeLatestChangedInfoAsync(
                project, specificDocument, projectVersion, cancellationToken).ConfigureAwait(false);

            if (!changedData.IsEmpty)
            {
                await _endPoint.InvokeAsync(
                    nameof(IDesignerAttributeListener.ReportDesignerAttributeDataAsync),
                    new object[] { changedData.SelectAsArray(d => d.data).ToArray() },
                    cancellationToken).ConfigureAwait(false);
            }

            // Now, keep track of what we've reported to the host so we won't report unchanged files in the future.
            foreach (var (document, info) in changedData)
                _documentToLastReportedInformation[document.Id] = (info.Category, projectVersion);
        }

        private async Task<ImmutableArray<(Document document, DesignerAttributeData data)>> ComputeLatestChangedInfoAsync(
            Project project, Document? specificDocument, VersionStamp projectVersion, CancellationToken cancellationToken)
        {
            var compilation = await project.GetRequiredCompilationAsync(cancellationToken).ConfigureAwait(false);
            var designerCategoryType = compilation.DesignerCategoryAttributeType();

            using var _ = ArrayBuilder<Task<(Document document, DesignerAttributeData? data)>>.GetInstance(out var tasks);
            foreach (var document in project.Documents)
            {
                // If we're only analyzing a specific document, then skip the rest.
                if (specificDocument != null && document != specificDocument)
                    continue;

                tasks.Add(ComputeDesignerAttributeDataAsync(
                    projectVersion, designerCategoryType, document, cancellationToken));
            }

            var docsAndData = await Task.WhenAll(tasks).ConfigureAwait(false);
            return docsAndData.Where(d => d.data != null).SelectAsArray(d => (d.document, d.data!.Value));
        }

        private async Task<(Document document, DesignerAttributeData? data)> ComputeDesignerAttributeDataAsync(
            VersionStamp projectVersion, INamedTypeSymbol? designerCategoryType, Document document, CancellationToken cancellationToken)
        {
            try
            {
                // If we don't have a path for this document, we cant proceed with it.
                // We need that path to inform the project system which file we're referring to.
                if (document.FilePath == null)
                    return default;

                // If nothing has changed at the top level between the last time we analyzed this document and now, then
                // no need to analyze again.
                var hasExistingInfo = _documentToLastReportedInformation.TryGetValue(document.Id, out var existingInfo);
                if (hasExistingInfo && existingInfo.projectVersion == projectVersion)
                    return default;

                // We either haven't computed the designer info, or our data was out of date.  We need
                // So recompute here.  Figure out what the current category is, and if that's different
                // from what we previously stored.
                var category = await DesignerAttributeHelpers.ComputeDesignerAttributeCategoryAsync(
                    designerCategoryType, document, cancellationToken).ConfigureAwait(false);

                if (hasExistingInfo &&
                    existingInfo.category == category)
                {
                    // category hasn't changed.  no need to report this.
                    return default;
                }

                var data = new DesignerAttributeData
                {
                    Category = category,
                    DocumentId = document.Id,
                    FilePath = document.FilePath,
                };

                return (document, data);
            }
            catch (Exception e) when (FatalError.ReportWithoutCrashUnlessCanceled(e))
            {
                return default;
            }
        }
    }
}
