﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeFixes
{
    internal static class FixAllContextExtensions
    {
        public static IProgressTracker GetProgressTracker(this FixAllContext context)
        {
#if CODE_STYLE
            return NoOpProgressTracker.Instance;
#else
            return context.ProgressTracker;
#endif
        }
    }

    internal static class FixAllContextHelper
    {
        public static async Task<ImmutableDictionary<Document, ImmutableArray<Diagnostic>>> GetDocumentDiagnosticsToFixAsync(FixAllContext fixAllContext)
        {
            var cancellationToken = fixAllContext.CancellationToken;

            var allDiagnostics = ImmutableArray<Diagnostic>.Empty;
            var projectsToFix = ImmutableArray<Project>.Empty;

            var document = fixAllContext.Document;
            var project = fixAllContext.Project;

            var progressTracker = fixAllContext.GetProgressTracker();

            switch (fixAllContext.Scope)
            {
                case FixAllScope.Document:
                    if (document != null && !await document.IsGeneratedCodeAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var documentDiagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(document).ConfigureAwait(false);
                        return ImmutableDictionary<Document, ImmutableArray<Diagnostic>>.Empty.SetItem(document, documentDiagnostics);
                    }

                    break;

                case FixAllScope.Project:
                    projectsToFix = ImmutableArray.Create(project);
                    allDiagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                    break;

                case FixAllScope.Solution:
                    projectsToFix = project.Solution.Projects
                        .Where(p => p.Language == project.Language)
                        .ToImmutableArray();

                    // Update the progress dialog with the count of projects to actually fix. We'll update the progress
                    // bar as we get all the documents in AddDocumentDiagnosticsAsync.

                    progressTracker.AddItems(projectsToFix.Length);

                    var diagnostics = new ConcurrentDictionary<ProjectId, ImmutableArray<Diagnostic>>();
                    using (var _ = ArrayBuilder<Task>.GetInstance(projectsToFix.Length, out var tasks))
                    {
                        foreach (var projectToFix in projectsToFix)
                            tasks.Add(Task.Run(async () => await AddDocumentDiagnosticsAsync(diagnostics, projectToFix).ConfigureAwait(false), cancellationToken));

                        await Task.WhenAll(tasks).ConfigureAwait(false);
                        allDiagnostics = allDiagnostics.AddRange(diagnostics.SelectMany(i => i.Value));
                    }
                    break;
            }

            if (allDiagnostics.IsEmpty)
            {
                return ImmutableDictionary<Document, ImmutableArray<Diagnostic>>.Empty;
            }

            return await GetDocumentDiagnosticsToFixAsync(
                allDiagnostics, projectsToFix, fixAllContext.CancellationToken).ConfigureAwait(false);

            async Task AddDocumentDiagnosticsAsync(ConcurrentDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnostics, Project projectToFix)
            {
                try
                {
                    var projectDiagnostics = await fixAllContext.GetAllDiagnosticsAsync(projectToFix).ConfigureAwait(false);
                    diagnostics.TryAdd(projectToFix.Id, projectDiagnostics);
                }
                finally
                {
                    progressTracker.ItemCompleted();
                }
            }
        }

        private static async Task<ImmutableDictionary<Document, ImmutableArray<Diagnostic>>> GetDocumentDiagnosticsToFixAsync(
            ImmutableArray<Diagnostic> diagnostics,
            ImmutableArray<Project> projects,
            CancellationToken cancellationToken)
        {
            var treeToDocumentMap = await GetTreeToDocumentMapAsync(projects, cancellationToken).ConfigureAwait(false);

            var builder = ImmutableDictionary.CreateBuilder<Document, ImmutableArray<Diagnostic>>();
            foreach (var (document, diagnosticsForDocument) in diagnostics.GroupBy(d => GetReportedDocument(d, treeToDocumentMap)))
            {
                if (document is null)
                    continue;

                cancellationToken.ThrowIfCancellationRequested();
                if (!await document.IsGeneratedCodeAsync(cancellationToken).ConfigureAwait(false))
                {
                    builder.Add(document, diagnosticsForDocument.ToImmutableArray());
                }
            }

            return builder.ToImmutable();
        }

        private static async Task<ImmutableDictionary<SyntaxTree, Document>> GetTreeToDocumentMapAsync(ImmutableArray<Project> projects, CancellationToken cancellationToken)
        {
            var builder = ImmutableDictionary.CreateBuilder<SyntaxTree, Document>();
            foreach (var project in projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var document in project.Documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tree = await document.GetRequiredSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
                    builder.Add(tree, document);
                }
            }

            return builder.ToImmutable();
        }

        private static Document? GetReportedDocument(Diagnostic diagnostic, ImmutableDictionary<SyntaxTree, Document> treeToDocumentsMap)
        {
            var tree = diagnostic.Location.SourceTree;
            if (tree != null)
            {
                if (treeToDocumentsMap.TryGetValue(tree, out var document))
                {
                    return document;
                }
            }

            return null;
        }

        public static string GetDefaultFixAllTitle(FixAllContext fixAllContext)
            => GetDefaultFixAllTitle(fixAllContext.Scope, fixAllContext.DiagnosticIds, fixAllContext.Document, fixAllContext.Project);

        public static string GetDefaultFixAllTitle(
            FixAllScope fixAllScope,
            ImmutableHashSet<string> diagnosticIds,
            Document? triggerDocument,
            Project triggerProject)
        {
            var diagnosticId = string.Join(",", diagnosticIds);

            switch (fixAllScope)
            {
                case FixAllScope.Custom:
                    return string.Format(WorkspaceExtensionsResources.Fix_all_0, diagnosticId);

                case FixAllScope.Document:
                    return string.Format(WorkspaceExtensionsResources.Fix_all_0_in_1, diagnosticId, triggerDocument!.Name);

                case FixAllScope.Project:
                    return string.Format(WorkspaceExtensionsResources.Fix_all_0_in_1, diagnosticId, triggerProject.Name);

                case FixAllScope.Solution:
                    return string.Format(WorkspaceExtensionsResources.Fix_all_0_in_Solution, diagnosticId);

                default:
                    throw ExceptionUtilities.UnexpectedValue(fixAllScope);
            }
        }

        public static Task<Solution> FixAllInSolutionAsync(
            FixAllContext fixAllContext,
            Func<FixAllContext, Document, ImmutableArray<Diagnostic>, Task<Document?>> computeNewDocumentAsync)
        {
            var solution = fixAllContext.Solution;
            var dependencyGraph = solution.GetProjectDependencyGraph();

            // Walk through each project in topological order, determining and applying the diagnostics for each
            // project.  We do this in topological order so that the compilations for successive projects are readily
            // available as we just computed them for dependent projects.  If we were to do it out of order, we might
            // start with a project that has a ton of dependencies, and we'd spend an inordinate amount of time just
            // building the compilations for it before we could proceed.
            //
            // By processing one project at a time, we can also let go of a project once done with it, allowing us to
            // reclaim lots of the memory so we don't overload the system while processing a large solution.
            var sortedProjectIds = dependencyGraph.GetTopologicallySortedProjects().ToImmutableArray();
            return FixAllInSolutionAsync(fixAllContext, sortedProjectIds, computeNewDocumentAsync);
        }

        public static async Task<Solution> FixAllInSolutionAsync(
            FixAllContext fixAllContext,
            ImmutableArray<ProjectId> projectIds,
            Func<FixAllContext, Document, ImmutableArray<Diagnostic>, Task<Document?>> computeNewDocumentAsync)
        {
            var progressTracker = fixAllContext.GetProgressTracker();
            progressTracker.Description = GetDefaultFixAllTitle(fixAllContext);

            var solution = fixAllContext.Solution;
            progressTracker.AddItems(projectIds.Length);

            using var _ = PooledDictionary<DocumentId, SourceText>.GetInstance(out var docIdToNewText);

            var currentSolution = solution;
            foreach (var projectId in projectIds)
            {
                try
                {
                    var project = solution.GetRequiredProject(projectId);
                    await AddDocumentFixesAsync(fixAllContext, computeNewDocumentAsync, project, docIdToNewText).ConfigureAwait(false);
                    foreach (var (docId, newText) in docIdToNewText)
                        currentSolution = currentSolution.WithDocumentText(docId, newText);
                }
                finally
                {
                    progressTracker.ItemCompleted();
                }
            }

            return currentSolution;
        }

        private static async Task AddDocumentFixesAsync(
            FixAllContext fixAllContext,
            Func<FixAllContext, Document, ImmutableArray<Diagnostic>, Task<Document?>> computeNewDocumentAsync,
            Project project,
            PooledDictionary<DocumentId, SourceText> docIdToNewText)
        {
            var progressTracker = fixAllContext.GetProgressTracker();

            var solution = fixAllContext.Solution;

            // First, get all the diagnostics for this project.
            progressTracker.Description = string.Format(WorkspaceExtensionsResources.Computing_diagnostics_for_0, project.Name);
            var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
            if (diagnostics.IsDefaultOrEmpty)
                return;

            // Then, once we've got the diagnostics, compute and apply the fixes for all in parallel to all the
            // affected documents in this project.
            progressTracker.Description = string.Format(WorkspaceExtensionsResources.Applying_fixes_to_0, project.Name);

            using var _ = ArrayBuilder<Task<(DocumentId, SourceText)>>.GetInstance(out var tasks);
            foreach (var group in diagnostics.Where(d => d.Location.IsInSource).GroupBy(d => d.Location.SourceTree))
            {
                var tree = group.Key;
                Contract.ThrowIfNull(tree);
                var document = solution.GetRequiredDocument(tree);
                var documentDiagnostics = group.ToImmutableArray();
                if (documentDiagnostics.IsDefaultOrEmpty)
                    continue;

                tasks.Add(Task.Run(async () =>
                {
                    var newDocument = await computeNewDocumentAsync(fixAllContext, document, documentDiagnostics).ConfigureAwait(false);
                    if (newDocument == null || newDocument == document)
                        return default;

                    // Convert new documents to text so we can release any expensive trees that may have been created.
                    return (document.Id, await newDocument.GetTextAsync(fixAllContext.CancellationToken).ConfigureAwait(false));
                }));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var task in tasks)
            {
                var (docId, newText) = await task.ConfigureAwait(false);
                if (docId != null)
                    docIdToNewText[docId] = newText;
            }
        }
    }
}
