﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Serialization;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Remote
{
    internal class ChecksumSynchronizer
    {
        // make sure there is always only 1 bulk synchronization
        private static readonly SemaphoreSlim s_gate = new SemaphoreSlim(initialCount: 1);

        private readonly AssetProvider _assetProvider;

        public ChecksumSynchronizer(AssetProvider assetProvider)
            => _assetProvider = assetProvider;

        public async ValueTask SynchronizeAssetsAsync(HashSet<Checksum> checksums, CancellationToken cancellationToken)
        {
            using (await s_gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                await SynchronizeAssets_NoLockAsync(checksums, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask SynchronizeSolutionAssetsAsync(Checksum solutionChecksum, CancellationToken cancellationToken)
        {
            using (await s_gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                // this will make 4 round trip to data source (VS) to get all assets that belong to the given solution checksum

                // first, get solution checksum object for the given solution checksum
                var solutionChecksumObject = await _assetProvider.GetAssetAsync<SolutionStateChecksums>(solutionChecksum, cancellationToken).ConfigureAwait(false);

                // second, get direct children of the solution
                {
                    using var pooledObject = SharedPools.Default<HashSet<Checksum>>().GetPooledObject();
                    var checksums = pooledObject.Object;

                    solutionChecksumObject.AddAllTo(checksums);
                    checksums.Remove(solutionChecksumObject.Checksum);
                    await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);
                }

                // third and last get direct children for all projects and documents in the solution 
                await SynchronizeProjectAssets_NoLockAsync(solutionChecksumObject.Projects, cancellationToken).ConfigureAwait(false);
            }
        }

        public async ValueTask SynchronizeProjectAssetsAsync(HashSet<Checksum> projectChecksums, CancellationToken cancellationToken)
        {
            using (await s_gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
            {
                await SynchronizeProjectAssets_NoLockAsync(projectChecksums, cancellationToken).ConfigureAwait(false);
            }
        }

        private async ValueTask SynchronizeProjectAssets_NoLockAsync(IReadOnlyCollection<Checksum> projectChecksums, CancellationToken cancellationToken)
        {
            // get children of project checksum objects at once
            await SynchronizeProjectsAsync(projectChecksums, cancellationToken).ConfigureAwait(false);

            // get children of document checksum objects at once
            using var pooledObject = SharedPools.Default<HashSet<Checksum>>().GetPooledObject();
            var checksums = pooledObject.Object;

            foreach (var projectChecksum in projectChecksums)
            {
                var projectChecksumObject = await _assetProvider.GetAssetAsync<ProjectStateChecksums>(projectChecksum, cancellationToken).ConfigureAwait(false);

                await CollectChecksumChildrenAsync(checksums, projectChecksumObject.Documents, cancellationToken).ConfigureAwait(false);
                await CollectChecksumChildrenAsync(checksums, projectChecksumObject.AdditionalDocuments, cancellationToken).ConfigureAwait(false);
                await CollectChecksumChildrenAsync(checksums, projectChecksumObject.AnalyzerConfigDocuments, cancellationToken).ConfigureAwait(false);
            }

            await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask SynchronizeProjectsAsync(IReadOnlyCollection<Checksum> projectChecksums, CancellationToken cancellationToken)
        {
            // get children of project checksum objects at once
            using var pooledObject = SharedPools.Default<HashSet<Checksum>>().GetPooledObject();
            var checksums = pooledObject.Object;

            await CollectProjectStateChecksumsAsync(checksums, projectChecksums, cancellationToken).ConfigureAwait(false);
            await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask SynchronizeAssets_NoLockAsync(HashSet<Checksum> checksums, CancellationToken cancellationToken)
        {
            // get children of solution checksum object at once
            await _assetProvider.SynchronizeAssetsAsync(checksums, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask CollectChecksumChildrenAsync(HashSet<Checksum> set, IReadOnlyCollection<Checksum> checksums, CancellationToken cancellationToken)
        {
            foreach (var checksum in checksums)
            {
                var checksumObject = await _assetProvider.GetAssetAsync<DocumentStateChecksums>(checksum, cancellationToken).ConfigureAwait(false);
                AddIfNeeded(set, checksumObject.Info);
                AddIfNeeded(set, checksumObject.Text);
            }
        }

        private async ValueTask CollectProjectStateChecksumsAsync(HashSet<Checksum> set, IReadOnlyCollection<Checksum> checksums, CancellationToken cancellationToken)
        {
            foreach (var checksum in checksums)
            {
                var checksumObject = await _assetProvider.GetAssetAsync<ProjectStateChecksums>(checksum, cancellationToken).ConfigureAwait(false);
                AddIfNeeded(set, checksumObject.Info);
                AddIfNeeded(set, checksumObject.CompilationOptions);
                AddIfNeeded(set, checksumObject.ParseOptions);
                AddIfNeeded(set, checksumObject.Documents);
                AddIfNeeded(set, checksumObject.ProjectReferences);
                AddIfNeeded(set, checksumObject.MetadataReferences);
                AddIfNeeded(set, checksumObject.AnalyzerReferences);
                AddIfNeeded(set, checksumObject.AdditionalDocuments);
                AddIfNeeded(set, checksumObject.AnalyzerConfigDocuments);
            }
        }

        private void AddIfNeeded(HashSet<Checksum> checksums, ChecksumCollection checksumCollection)
        {
            foreach (var checksum in checksumCollection)
                AddIfNeeded(checksums, checksum);
        }

        private void AddIfNeeded(HashSet<Checksum> checksums, Checksum checksum)
        {
            if (checksum != Checksum.Null && !_assetProvider.EnsureCacheEntryIfExists(checksum))
                checksums.Add(checksum);
        }
    }
}
