﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Collections;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols.SymbolTree
{
    [ExportWorkspaceService(typeof(SymbolTreeInfoCacheService)), Shared]
    internal sealed partial class SymbolTreeInfoCacheService : IWorkspaceService
    {
        private readonly ConcurrentDictionary<ProjectId, SymbolTreeInfo> _projectIdToInfo = new();
        private readonly ConcurrentDictionary<MetadataId, MetadataInfo> _metadataIdToInfo = new();

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public SymbolTreeInfoCacheService()
        {
        }

        public async ValueTask<SymbolTreeInfo?> TryGetMetadataSymbolTreeInfoAsync(
            Solution solution,
            PortableExecutableReference reference,
            CancellationToken cancellationToken)
        {
            var metadataId = SymbolTreeInfo.GetMetadataIdNoThrow(reference);
            if (metadataId == null)
                return null;

            var checksum = SymbolTreeInfo.GetMetadataChecksum(solution, reference, cancellationToken);

            // See if the last value produced matches what the caller is asking for.  If so, return that.
            if (_metadataIdToInfo.TryGetValue(metadataId, out var metadataInfo) &&
                metadataInfo.SymbolTreeInfo.Checksum == checksum)
            {
                return metadataInfo.SymbolTreeInfo;
            }

            // If we didn't have it in our cache, see if we can load it from disk.
            // Note: pass 'loadOnly' so we only attempt to load from disk, not to actually
            // try to create the metadata.
            var info = await SymbolTreeInfo.GetInfoForMetadataReferenceAsync(
                solution, reference, checksum, loadOnly: true, cancellationToken).ConfigureAwait(false);
            return info;
        }

        public async Task<SymbolTreeInfo?> TryGetSourceSymbolTreeInfoAsync(
            Project project, CancellationToken cancellationToken)
        {
            // See if the last value produced matches what the caller is asking for.  If so, return that.
            var checksum = await SymbolTreeInfo.GetSourceSymbolsChecksumAsync(project, cancellationToken).ConfigureAwait(false);
            if (_projectIdToInfo.TryGetValue(project.Id, out var projectInfo) &&
                projectInfo.Checksum == checksum)
            {
                return projectInfo;
            }

            // If we didn't have it in our cache, see if we can load it from disk.
            // Note: pass 'loadOnly' so we only attempt to load from disk, not to actually
            // try to create the index.
            var info = await SymbolTreeInfo.GetInfoForSourceAssemblyAsync(
                project, checksum, loadOnly: true, cancellationToken).ConfigureAwait(false);
            return info;
        }

        public async Task AnalyzeDocumentAsync(Document document, bool isMethodBodyEdit, CancellationToken cancellationToken)
        {
            // This was a method body edit.  We can reuse the existing SymbolTreeInfo if we have one.  We can't just
            // bail out here as the change in the document means we'll have a new checksum.  We need to get that new
            // checksum so that our cached information is valid.
            if (isMethodBodyEdit &&
                _projectIdToInfo.TryGetValue(document.Project.Id, out var cachedInfo))
            {
                var checksum = await SymbolTreeInfo.GetSourceSymbolsChecksumAsync(
                    document.Project, cancellationToken).ConfigureAwait(false);

                var newInfo = cachedInfo.WithChecksum(checksum);
                _projectIdToInfo[document.Project.Id] = newInfo;
                return;
            }

            await AnalyzeProjectAsync(document.Project, cancellationToken).ConfigureAwait(false);
        }

        public async Task AnalyzeProjectAsync(Project project, CancellationToken cancellationToken)
        {
            Debug.Assert(project.SupportsCompilation);

            // Produce the indices for the source and metadata symbols in parallel.
            using var _ = ArrayBuilder<Task>.GetInstance(out var tasks);

            tasks.Add(Task.Run(() => this.UpdateSourceSymbolTreeInfoAsync(project, cancellationToken), cancellationToken));
            tasks.Add(Task.Run(() => this.UpdateReferencesAsync(project, cancellationToken), cancellationToken));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task UpdateSourceSymbolTreeInfoAsync(Project project, CancellationToken cancellationToken)
        {
            var checksum = await SymbolTreeInfo.GetSourceSymbolsChecksumAsync(project, cancellationToken).ConfigureAwait(false);
            if (!_projectIdToInfo.TryGetValue(project.Id, out var projectInfo) ||
                projectInfo.Checksum != checksum)
            {
                projectInfo = await SymbolTreeInfo.GetInfoForSourceAssemblyAsync(
                    project, checksum, loadOnly: false, cancellationToken).ConfigureAwait(false);

                Contract.ThrowIfNull(projectInfo);
                Contract.ThrowIfTrue(projectInfo.Checksum != checksum, "If we computed a SymbolTreeInfo, then its checksum much match our checksum.");

                // Mark that we're up to date with this project.  Future calls with the same 
                // semantic version can bail out immediately.
                _projectIdToInfo[project.Id] = projectInfo;
            }
        }

        private async Task UpdateReferencesAsync(Project project, CancellationToken cancellationToken)
        {
            // Process all metadata references. If it remote workspace, do this in parallel.
            using var pendingTasks = new TemporaryArray<Task>();

            foreach (var reference in project.MetadataReferences)
            {
                if (reference is not PortableExecutableReference portableExecutableReference)
                    continue;

                if (cancellationToken.IsCancellationRequested)
                {
                    // Break out of this loop to make sure other pending operations process cancellation before
                    // returning.
                    break;
                }

                var updateTask = UpdateReferenceAsync(_metadataIdToInfo, project, portableExecutableReference, cancellationToken);
                if (updateTask.Status != TaskStatus.RanToCompletion)
                    pendingTasks.Add(updateTask);
            }

            if (pendingTasks.Count > 0)
            {
                // If any update operations did not complete synchronously (including any cancelled operations),
                // wait for them to complete now.
                await Task.WhenAll(pendingTasks.ToImmutableAndClear()).ConfigureAwait(false);
            }

            // ⚠ This local function must be 'async' to ensure exceptions are captured in the resulting task and
            // not thrown directly to the caller.
            static async Task UpdateReferenceAsync(
                ConcurrentDictionary<MetadataId, SymbolTreeInfoCacheService.MetadataInfo> metadataIdToInfo,
                Project project,
                PortableExecutableReference reference,
                CancellationToken cancellationToken)
            {
                var metadataId = SymbolTreeInfo.GetMetadataIdNoThrow(reference);
                if (metadataId == null)
                    return;

                // 🐉 PERF: GetMetadataChecksum indirectly uses a ConditionalWeakTable. This call is intentionally
                // placed before the first 'await' of this asynchronous method to ensure it executes in the
                // synchronous portion of the caller. https://dev.azure.com/devdiv/DevDiv/_workitems/edit/1270250
                var checksum = SymbolTreeInfo.GetMetadataChecksum(project.Solution, reference, cancellationToken);
                if (!metadataIdToInfo.TryGetValue(metadataId, out var metadataInfo) ||
                    metadataInfo.SymbolTreeInfo.Checksum != checksum)
                {
                    var info = await SymbolTreeInfo.GetInfoForMetadataReferenceAsync(
                        project.Solution, reference, checksum, loadOnly: false, cancellationToken: cancellationToken).ConfigureAwait(false);

                    Contract.ThrowIfNull(info);
                    Contract.ThrowIfTrue(info.Checksum != checksum, "If we computed a SymbolTreeInfo, then its checksum much match our checksum.");

                    // Note, getting the info may fail (for example, bogus metadata).  That's ok.  
                    // We still want to cache that result so that don't try to continuously produce
                    // this info over and over again.
                    metadataInfo = new SymbolTreeInfoCacheService.MetadataInfo(info, metadataInfo.ReferencingProjects ?? new HashSet<ProjectId>());
                    metadataIdToInfo[metadataId] = metadataInfo;
                }

                // Keep track that this dll is referenced by this project.
                lock (metadataInfo.ReferencingProjects)
                {
                    metadataInfo.ReferencingProjects.Add(project.Id);
                }
            }
        }

        public void RemoveProject(ProjectId projectId)
        {
            _projectIdToInfo.TryRemove(projectId, out _);
            RemoveMetadataReferences(projectId);
        }

        private void RemoveMetadataReferences(ProjectId projectId)
        {
            foreach (var (id, info) in _metadataIdToInfo.ToArray())
            {
                lock (info.ReferencingProjects)
                {
                    info.ReferencingProjects.Remove(projectId);

                    // If this metadata dll isn't referenced by any project.  We can just dump it.
                    if (info.ReferencingProjects.Count == 0)
                        _metadataIdToInfo.TryRemove(id, out _);
                }
            }
        }
    }
}
