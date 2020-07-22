﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics.Telemetry;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.CodeAnalysis.Workspaces.Diagnostics;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Diagnostics.EngineV2
{
    internal partial class DiagnosticIncrementalAnalyzer
    {
        public Task AnalyzeSyntaxAsync(Document document, InvocationReasons reasons, CancellationToken cancellationToken)
            => AnalyzeDocumentForKindAsync(document, AnalysisKind.Syntax, cancellationToken);

        public Task AnalyzeDocumentAsync(Document document, SyntaxNode bodyOpt, InvocationReasons reasons, CancellationToken cancellationToken)
            => AnalyzeDocumentForKindAsync(document, AnalysisKind.Semantic, cancellationToken);

        public Task AnalyzeNonSourceDocumentAsync(TextDocument textDocument, InvocationReasons reasons, CancellationToken cancellationToken)
            => AnalyzeDocumentForKindAsync(textDocument, AnalysisKind.Syntax, cancellationToken);

        private async Task AnalyzeDocumentForKindAsync(TextDocument document, AnalysisKind kind, CancellationToken cancellationToken)
        {
            try
            {
                if (!AnalysisEnabled(document))
                {
                    // to reduce allocations, here, we don't clear existing diagnostics since it is dealt by other entry point such as
                    // DocumentReset or DocumentClosed.
                    return;
                }

                var stateSets = _stateManager.GetOrUpdateStateSets(document.Project);
                var compilationWithAnalyzers = await GetOrCreateCompilationWithAnalyzersAsync(document.Project, stateSets, cancellationToken).ConfigureAwait(false);

                // We split the diagnostic computation for document into following steps:
                //  1. Try to get cached diagnostics for each analyzer, while computing the set of analyzers that do not have cached diagnostics.
                //  2. Execute all the non-cached analyzers with a single invocation into CompilationWithAnalyzers.
                //  3. Fetch computed diagnostics per-analyzer from the above invocation, and cache and raise diagnostic reported events.
                // In near future, the diagnostic computation invocation into CompilationWithAnalyzers will be moved to OOP.
                // This should help simplify and/or remove the IDE layer diagnostic caching in devenv process.

                // First attempt to fetch diagnostics from the cache, while computing the state sets for analyzers that are not cached.
                using var _ = ArrayBuilder<StateSet>.GetInstance(out var nonCachedStateSets);
                foreach (var stateSet in stateSets)
                {
                    var data = await TryGetCachedDocumentAnalysisDataAsync(document, stateSet, kind, cancellationToken).ConfigureAwait(false);
                    if (data.HasValue)
                    {
                        // We need to persist and raise diagnostics for suppressed analyzer.
                        PersistAndRaiseDiagnosticsIfNeeded(data.Value, stateSet);
                    }
                    else
                    {
                        nonCachedStateSets.Add(stateSet);
                    }
                }

                // Then, compute the diagnostics for non-cached state sets, and cache and raise diagnostic reported events for these diagnostics.
                if (nonCachedStateSets.Count > 0)
                {
                    var analysisScope = new DocumentAnalysisScope(document, span: null, nonCachedStateSets.SelectAsArray(s => s.Analyzer), kind);
                    var executor = new DocumentAnalysisExecutor(analysisScope, compilationWithAnalyzers, DiagnosticAnalyzerInfoCache);
                    foreach (var stateSet in nonCachedStateSets)
                    {
                        var computedData = await ComputeDocumentAnalysisDataAsync(executor, stateSet, cancellationToken).ConfigureAwait(false);
                        PersistAndRaiseDiagnosticsIfNeeded(computedData, stateSet);
                    }

                    var asyncToken = AnalyzerService.Listener.BeginAsyncOperation(nameof(AnalyzeDocumentForKindAsync));
                    var _2 = ReportAnalyzerPerformanceAsync(document, compilationWithAnalyzers, cancellationToken).CompletesAsyncOperation(asyncToken);
                }
            }
            catch (Exception e) when (FatalError.ReportUnlessCanceled(e))
            {
                throw ExceptionUtilities.Unreachable;
            }

            void PersistAndRaiseDiagnosticsIfNeeded(DocumentAnalysisData result, StateSet stateSet)
            {
                if (result.FromCache == true)
                {
                    RaiseDocumentDiagnosticsIfNeeded(document, stateSet, kind, result.Items);
                    return;
                }

                // no cancellation after this point.
                var state = stateSet.GetOrCreateActiveFileState(document.Id);
                state.Save(kind, result.ToPersistData());

                RaiseDocumentDiagnosticsIfNeeded(document, stateSet, kind, result.OldItems, result.Items);
            }
        }

        public async Task AnalyzeProjectAsync(Project project, bool semanticsChanged, InvocationReasons reasons, CancellationToken cancellationToken)
        {
            // Perf optimization. check whether we want to analyze this project or not.
            if (!FullAnalysisEnabled(project, forceAnalyzerRun: false))
            {
                return;
            }

            await AnalyzeProjectAsync(project, forceAnalyzerRun: false, cancellationToken).ConfigureAwait(false);
        }

        public Task ForceAnalyzeProjectAsync(Project project, CancellationToken cancellationToken)
            => AnalyzeProjectAsync(project, forceAnalyzerRun: true, cancellationToken);

        private async Task AnalyzeProjectAsync(Project project, bool forceAnalyzerRun, CancellationToken cancellationToken)
        {
            try
            {
                var stateSets = GetStateSetsForFullSolutionAnalysis(_stateManager.GetOrUpdateStateSets(project), project).ToList();
                var options = project.Solution.Options;

                // PERF: get analyzers that are not suppressed and marked as open file only
                // this is perf optimization. we cache these result since we know the result. (no diagnostics)
                var activeAnalyzers = stateSets
                                        .Select(s => s.Analyzer)
                                        .Where(a => !DiagnosticAnalyzerInfoCache.IsAnalyzerSuppressed(a, project) && !a.IsOpenFileOnly(options));

                // get driver only with active analyzers.
                var compilationWithAnalyzers = await AnalyzerHelper.CreateCompilationWithAnalyzersAsync(project, activeAnalyzers, includeSuppressedDiagnostics: true, cancellationToken).ConfigureAwait(false);

                var result = await GetProjectAnalysisDataAsync(compilationWithAnalyzers, project, stateSets, forceAnalyzerRun, cancellationToken).ConfigureAwait(false);
                if (result.OldResult == null)
                {
                    RaiseProjectDiagnosticsIfNeeded(project, stateSets, result.Result);
                    return;
                }

                // we might not have compilationWithAnalyzers even if project supports compilation if we are called with no analyzers. 
                var compilation = compilationWithAnalyzers?.Compilation ??
                    (project.SupportsCompilation ? await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) : null);

                // no cancellation after this point.
                // any analyzer that doesn't have result will be treated as returned empty set
                // which means we will remove those from error list
                foreach (var stateSet in stateSets)
                {
                    var state = stateSet.GetOrCreateProjectState(project.Id);

                    await state.SaveAsync(PersistentStorageService, project, result.GetResult(stateSet.Analyzer)).ConfigureAwait(false);
                }

                RaiseProjectDiagnosticsIfNeeded(project, stateSets, result.OldResult, result.Result);
            }
            catch (Exception e) when (FatalError.ReportUnlessCanceled(e))
            {
                throw ExceptionUtilities.Unreachable;
            }
        }

        public Task DocumentOpenAsync(Document document, CancellationToken cancellationToken)
            => TextDocumentOpenAsync(document, cancellationToken);

        public Task NonSourceDocumentOpenAsync(TextDocument document, CancellationToken cancellationToken)
            => TextDocumentOpenAsync(document, cancellationToken);

        private async Task TextDocumentOpenAsync(TextDocument document, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Diagnostics_DocumentOpen, GetOpenLogMessage, document, cancellationToken))
            {
                var stateSets = _stateManager.GetStateSets(document.Project);

                // let other component knows about this event
                ClearCompilationsWithAnalyzersCache();
                await _stateManager.OnDocumentOpenedAsync(stateSets, document).ConfigureAwait(false);
            }
        }

        public Task DocumentCloseAsync(Document document, CancellationToken cancellationToken)
            => TextDocumentCloseAsync(document, cancellationToken);

        public Task NonSourceDocumentCloseAsync(TextDocument document, CancellationToken cancellationToken)
            => TextDocumentCloseAsync(document, cancellationToken);

        private async Task TextDocumentCloseAsync(TextDocument document, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Diagnostics_DocumentClose, GetResetLogMessage, document, cancellationToken))
            {
                var stateSets = _stateManager.GetStateSets(document.Project);

                // let other components knows about this event
                ClearCompilationsWithAnalyzersCache();
                var documentHadDiagnostics = await _stateManager.OnDocumentClosedAsync(stateSets, document).ConfigureAwait(false);
                RaiseDiagnosticsRemovedIfRequiredForClosedOrResetDocument(document, stateSets, documentHadDiagnostics);
            }
        }

        public Task DocumentResetAsync(Document document, CancellationToken cancellationToken)
            => TextDocumentResetAsync(document, cancellationToken);

        public Task NonSourceDocumentResetAsync(TextDocument document, CancellationToken cancellationToken)
            => TextDocumentResetAsync(document, cancellationToken);

        private Task TextDocumentResetAsync(TextDocument document, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Diagnostics_DocumentReset, GetResetLogMessage, document, cancellationToken))
            {
                var stateSets = _stateManager.GetStateSets(document.Project);

                // let other components knows about this event
                ClearCompilationsWithAnalyzersCache();
                var documentHadDiagnostics = StateManager.OnDocumentReset(stateSets, document);
                RaiseDiagnosticsRemovedIfRequiredForClosedOrResetDocument(document, stateSets, documentHadDiagnostics);
            }

            return Task.CompletedTask;
        }

        private void RaiseDiagnosticsRemovedIfRequiredForClosedOrResetDocument(TextDocument document, IEnumerable<StateSet> stateSets, bool documentHadDiagnostics)
        {
            // if there was no diagnostic reported for this document OR Full solution analysis is enabled, nothing to clean up
            if (!documentHadDiagnostics ||
                FullAnalysisEnabled(document.Project, forceAnalyzerRun: false))
            {
                // this is Perf to reduce raising events unnecessarily.
                return;
            }

            var removeDiagnosticsOnDocumentClose = document.Project.Solution.Options.GetOption(ServiceFeatureOnOffOptions.RemoveDocumentDiagnosticsOnDocumentClose, document.Project.Language);

            if (!removeDiagnosticsOnDocumentClose)
            {
                return;
            }

            RaiseDiagnosticsRemovedForDocument(document.Id, stateSets);
        }

        public Task RemoveDocumentAsync(DocumentId documentId, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Diagnostics_RemoveDocument, GetRemoveLogMessage, documentId, CancellationToken.None))
            {
                var stateSets = _stateManager.GetStateSets(documentId.ProjectId);

                // let other components knows about this event
                ClearCompilationsWithAnalyzersCache();
                var changed = StateManager.OnDocumentRemoved(stateSets, documentId);

                // if there was no diagnostic reported for this document, nothing to clean up
                // this is Perf to reduce raising events unnecessarily.
                if (changed)
                {
                    RaiseDiagnosticsRemovedForDocument(documentId, stateSets);
                }
            }

            return Task.CompletedTask;
        }

        private void RaiseDiagnosticsRemovedForDocument(DocumentId documentId, IEnumerable<StateSet> stateSets)
        {
            // remove all diagnostics for the document
            AnalyzerService.RaiseBulkDiagnosticsUpdated(raiseEvents =>
            {
                foreach (var stateSet in stateSets)
                {
                    // clear all doucment diagnostics
                    RaiseDiagnosticsRemoved(documentId, solution: null, stateSet, AnalysisKind.Syntax, raiseEvents);
                    RaiseDiagnosticsRemoved(documentId, solution: null, stateSet, AnalysisKind.Semantic, raiseEvents);
                    RaiseDiagnosticsRemoved(documentId, solution: null, stateSet, AnalysisKind.NonLocal, raiseEvents);
                }
            });
        }

        public Task RemoveProjectAsync(ProjectId projectId, CancellationToken cancellation)
        {
            using (Logger.LogBlock(FunctionId.Diagnostics_RemoveProject, GetRemoveLogMessage, projectId, CancellationToken.None))
            {
                var stateSets = _stateManager.GetStateSets(projectId);

                // let other components knows about this event
                ClearCompilationsWithAnalyzersCache();
                var changed = _stateManager.OnProjectRemoved(stateSets, projectId);

                // if there was no diagnostic reported for this project, nothing to clean up
                // this is Perf to reduce raising events unnecessarily.
                if (changed)
                {
                    // remove all diagnostics for the project
                    AnalyzerService.RaiseBulkDiagnosticsUpdated(raiseEvents =>
                    {
                        foreach (var stateSet in stateSets)
                        {
                            // clear all project diagnostics
                            RaiseDiagnosticsRemoved(projectId, solution: null, stateSet, raiseEvents);
                        }
                    });
                }
            }

            return Task.CompletedTask;
        }

        public Task NewSolutionSnapshotAsync(Solution solution, CancellationToken cancellationToken)
        {
            // let other components knows about this event
            ClearCompilationsWithAnalyzersCache();

            return Task.CompletedTask;
        }

        private static bool AnalysisEnabled(TextDocument document)
        {
            if (document.Services.GetService<DocumentPropertiesService>()?.DiagnosticsLspClientName != null)
            {
                // This is a generated Razor document, and they want diagnostics, so let's report it
                return true;
            }

            // change it to check active file (or visible files), not open files if active file tracking is enabled.
            // otherwise, use open file.
            return document.IsOpen() && document.SupportsDiagnostics();
        }

        /// <summary>
        /// Return list of <see cref="StateSet"/> to be used for full solution analysis.
        /// </summary>
        private IEnumerable<StateSet> GetStateSetsForFullSolutionAnalysis(IEnumerable<StateSet> stateSets, Project project)
        {
            // If full analysis is off, remove state that is created from build.
            // this will make sure diagnostics from build (converted from build to live) will never be cleared
            // until next build.
            if (SolutionCrawlerOptions.GetBackgroundAnalysisScope(project) != BackgroundAnalysisScope.FullSolution)
            {
                stateSets = stateSets.Where(s => !s.FromBuild(project.Id));
            }

            // include all analyzers if option is on
            if (project.Solution.Workspace.Options.GetOption(InternalDiagnosticsOptions.ProcessHiddenDiagnostics))
            {
                return stateSets;
            }

            // Compute analyzer config special diagnostic options for computing effective severity.
            // Note that these options are not cached onto the project, so we compute it once upfront. 
            var analyzerConfigSpecialDiagnosticOptions = project.GetAnalyzerConfigSpecialDiagnosticOptions();

            // Include only analyzers we want to run for full solution analysis.
            // Analyzers not included here will never be saved because result is unknown.
            return stateSets.Where(s => IsCandidateForFullSolutionAnalysis(s.Analyzer, project, analyzerConfigSpecialDiagnosticOptions));
        }

        private bool IsCandidateForFullSolutionAnalysis(DiagnosticAnalyzer analyzer, Project project, ImmutableDictionary<string, ReportDiagnostic> analyzerConfigSpecialDiagnosticOptions)
        {
            // PERF: Don't query descriptors for compiler analyzer or file content load analyzer, always execute them.
            if (analyzer == FileContentLoadAnalyzer.Instance || analyzer.IsCompilerAnalyzer())
            {
                return true;
            }

            if (analyzer.IsBuiltInAnalyzer())
            {
                // always return true for builtin analyzer. we can't use
                // descriptor check since many builtin analyzer always return 
                // hidden descriptor regardless what descriptor it actually
                // return on runtime. they do this so that they can control
                // severity through option page rather than rule set editor.
                // this is special behavior only ide analyzer can do. we hope
                // once we support editorconfig fully, third party can use this
                // ability as well and we can remove this kind special treatment on builtin
                // analyzer.
                return true;
            }

            if (analyzer is DiagnosticSuppressor)
            {
                // Always execute diagnostic suppressors.
                return true;
            }

            // For most of analyzers, the number of diagnostic descriptors is small, so this should be cheap.
            var descriptors = DiagnosticAnalyzerInfoCache.GetDiagnosticDescriptors(analyzer);
            return descriptors.Any(d => d.GetEffectiveSeverity(project.CompilationOptions!, analyzerConfigSpecialDiagnosticOptions) != ReportDiagnostic.Hidden);
        }

        private void RaiseProjectDiagnosticsIfNeeded(
            Project project,
            IEnumerable<StateSet> stateSets,
            ImmutableDictionary<DiagnosticAnalyzer, DiagnosticAnalysisResult> result)
        {
            RaiseProjectDiagnosticsIfNeeded(project, stateSets, ImmutableDictionary<DiagnosticAnalyzer, DiagnosticAnalysisResult>.Empty, result);
        }

        private void RaiseProjectDiagnosticsIfNeeded(
            Project project,
            IEnumerable<StateSet> stateSets,
            ImmutableDictionary<DiagnosticAnalyzer, DiagnosticAnalysisResult> oldResult,
            ImmutableDictionary<DiagnosticAnalyzer, DiagnosticAnalysisResult> newResult)
        {
            if (oldResult.Count == 0 && newResult.Count == 0)
            {
                // there is nothing to update
                return;
            }

            AnalyzerService.RaiseBulkDiagnosticsUpdated(raiseEvents =>
            {
                foreach (var stateSet in stateSets)
                {
                    var analyzer = stateSet.Analyzer;

                    var oldAnalysisResult = GetResultOrEmpty(oldResult, analyzer, project.Id, VersionStamp.Default);
                    var newAnalysisResult = GetResultOrEmpty(newResult, analyzer, project.Id, VersionStamp.Default);

                    // Perf - 4 different cases.
                    // upper 3 cases can be removed and it will still work. but this is hot path so if we can bail out
                    // without any allocations, that's better.
                    if (oldAnalysisResult.IsEmpty && newAnalysisResult.IsEmpty)
                    {
                        // nothing to do
                        continue;
                    }

                    if (!oldAnalysisResult.IsEmpty && newAnalysisResult.IsEmpty)
                    {
                        RoslynDebug.Assert(oldAnalysisResult.DocumentIds != null);

                        // remove old diagnostics
                        RaiseProjectDiagnosticsRemoved(stateSet, oldAnalysisResult.ProjectId, oldAnalysisResult.DocumentIds, handleActiveFile: false, raiseEvents);
                        continue;
                    }

                    if (oldAnalysisResult.IsEmpty && !newAnalysisResult.IsEmpty)
                    {
                        // add new diagnostics
                        RaiseProjectDiagnosticsCreated(project, stateSet, oldAnalysisResult, newAnalysisResult, raiseEvents);
                        continue;
                    }

                    // both old and new has items in them. update existing items
                    RoslynDebug.Assert(oldAnalysisResult.DocumentIds != null);

                    // first remove ones no longer needed.
                    var documentsToRemove = oldAnalysisResult.DocumentIds.Except(newAnalysisResult.DocumentIds);
                    RaiseProjectDiagnosticsRemoved(stateSet, oldAnalysisResult.ProjectId, documentsToRemove, handleActiveFile: false, raiseEvents);

                    // next update or create new ones
                    RaiseProjectDiagnosticsCreated(project, stateSet, oldAnalysisResult, newAnalysisResult, raiseEvents);
                }
            });
        }

        private void RaiseDocumentDiagnosticsIfNeeded(TextDocument document, StateSet stateSet, AnalysisKind kind, ImmutableArray<DiagnosticData> items)
            => RaiseDocumentDiagnosticsIfNeeded(document, stateSet, kind, ImmutableArray<DiagnosticData>.Empty, items);

        private void RaiseDocumentDiagnosticsIfNeeded(
            TextDocument document, StateSet stateSet, AnalysisKind kind, ImmutableArray<DiagnosticData> oldItems, ImmutableArray<DiagnosticData> newItems)
        {
            RaiseDocumentDiagnosticsIfNeeded(document, stateSet, kind, oldItems, newItems, AnalyzerService.RaiseDiagnosticsUpdated, forceUpdate: false);
        }

        private void RaiseDocumentDiagnosticsIfNeeded(
            TextDocument document, StateSet stateSet, AnalysisKind kind,
            DiagnosticAnalysisResult oldResult, DiagnosticAnalysisResult newResult,
            Action<DiagnosticsUpdatedArgs> raiseEvents)
        {
            // if our old result is from build and we don't have actual data, don't try micro-optimize and always refresh diagnostics.
            // most of time, we don't actually load or hold the old data in memory from persistent storage due to perf reasons.
            //
            // we need this special behavior for errors from build since unlike live errors, we don't know whether errors
            // from build is for syntax, semantic or others. due to that, we blindly mark them as semantic errors (most common type of errors from build)
            //
            // that can sometime cause issues. for example, if the error turns out to be syntax error (live) then we at the end fail to de-dup.
            // but since this optimization saves us a lot of refresh between live errors analysis we want to disable this only in this condition.
            var forceUpdate = oldResult.FromBuild && oldResult.IsAggregatedForm;

            var oldItems = oldResult.GetDocumentDiagnostics(document.Id, kind);
            var newItems = newResult.GetDocumentDiagnostics(document.Id, kind);

            RaiseDocumentDiagnosticsIfNeeded(document, stateSet, kind, oldItems, newItems, raiseEvents, forceUpdate);
        }

        private void RaiseDocumentDiagnosticsIfNeeded(
            TextDocument document, StateSet stateSet, AnalysisKind kind,
            ImmutableArray<DiagnosticData> oldItems, ImmutableArray<DiagnosticData> newItems,
            Action<DiagnosticsUpdatedArgs> raiseEvents,
            bool forceUpdate)
        {
            if (!forceUpdate && oldItems.IsEmpty && newItems.IsEmpty)
            {
                // there is nothing to update
                return;
            }

            RaiseDiagnosticsCreated(document, stateSet, kind, newItems, raiseEvents);
        }

        private void RaiseProjectDiagnosticsCreated(Project project, StateSet stateSet, DiagnosticAnalysisResult oldAnalysisResult, DiagnosticAnalysisResult newAnalysisResult, Action<DiagnosticsUpdatedArgs> raiseEvents)
        {
            RoslynDebug.Assert(newAnalysisResult.DocumentIds != null);

            foreach (var documentId in newAnalysisResult.DocumentIds)
            {
                var document = project.GetTextDocument(documentId);
                if (document == null)
                {
                    // it can happen with build synchronization since, in build case, 
                    // we don't have actual snapshot (we have no idea what sources out of proc build has picked up)
                    // so we might be out of sync.
                    // example of such cases will be changing anything about solution while building is going on.
                    // it can be user explicit actions such as unloading project, deleting a file, but also it can be 
                    // something project system or roslyn workspace does such as populating workspace right after
                    // solution is loaded.
                    continue;
                }

                RaiseDocumentDiagnosticsIfNeeded(document, stateSet, AnalysisKind.NonLocal, oldAnalysisResult, newAnalysisResult, raiseEvents);

                // we don't raise events for active file. it will be taken cared by active file analysis
                if (stateSet.IsActiveFile(documentId))
                {
                    continue;
                }

                RaiseDocumentDiagnosticsIfNeeded(document, stateSet, AnalysisKind.Syntax, oldAnalysisResult, newAnalysisResult, raiseEvents);
                RaiseDocumentDiagnosticsIfNeeded(document, stateSet, AnalysisKind.Semantic, oldAnalysisResult, newAnalysisResult, raiseEvents);
            }

            RaiseDiagnosticsCreated(project, stateSet, newAnalysisResult.GetOtherDiagnostics(), raiseEvents);
        }

        private void RaiseProjectDiagnosticsRemoved(StateSet stateSet, ProjectId projectId, IEnumerable<DocumentId> documentIds, bool handleActiveFile, Action<DiagnosticsUpdatedArgs> raiseEvents)
        {
            foreach (var documentId in documentIds)
            {
                RaiseDiagnosticsRemoved(documentId, solution: null, stateSet, AnalysisKind.NonLocal, raiseEvents);

                // we don't raise events for active file. it will be taken care of by active file analysis
                if (!handleActiveFile && stateSet.IsActiveFile(documentId))
                {
                    continue;
                }

                RaiseDiagnosticsRemoved(documentId, solution: null, stateSet, AnalysisKind.Syntax, raiseEvents);
                RaiseDiagnosticsRemoved(documentId, solution: null, stateSet, AnalysisKind.Semantic, raiseEvents);
            }

            RaiseDiagnosticsRemoved(projectId, solution: null, stateSet, raiseEvents);
        }

        private async Task ReportAnalyzerPerformanceAsync(TextDocument document, CompilationWithAnalyzers? compilation, CancellationToken cancellationToken)
        {
            try
            {
                if (compilation == null)
                {
                    return;
                }

                var client = await RemoteHostClient.TryGetClientAsync(document.Project.Solution.Workspace, cancellationToken).ConfigureAwait(false);
                if (client == null)
                {
                    // no remote support
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                using var pooledObject = SharedPools.Default<Dictionary<DiagnosticAnalyzer, AnalyzerTelemetryInfo>>().GetPooledObject();

                var containsData = false;
                foreach (var analyzer in compilation.Analyzers)
                {
                    var telemetryInfo = await compilation.GetAnalyzerTelemetryInfoAsync(analyzer, cancellationToken).ConfigureAwait(false);
                    if (!containsData && telemetryInfo.ExecutionTime.Ticks > 0)
                    {
                        // this is unfortunate tweak due to how GetAnalyzerTelemetryInfoAsync works when analyzers are asked
                        // one by one rather than in bulk.
                        containsData = true;
                    }

                    pooledObject.Object.Add(analyzer, telemetryInfo);
                }

                if (!containsData)
                {
                    // looks like there is no new data from driver. skip reporting.
                    return;
                }

                await client.RunRemoteAsync(
                    WellKnownServiceHubService.CodeAnalysis,
                    nameof(IRemoteDiagnosticAnalyzerService.ReportAnalyzerPerformance),
                    solution: null,
                    new object[] { pooledObject.Object.ToAnalyzerPerformanceInfo(DiagnosticAnalyzerInfoCache), /* unit count */ 1 },
                    callbackTarget: null,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (FatalError.ReportWithoutCrashUnlessCanceled(ex))
            {
                // this is fire and forget method
            }
        }
    }
}
