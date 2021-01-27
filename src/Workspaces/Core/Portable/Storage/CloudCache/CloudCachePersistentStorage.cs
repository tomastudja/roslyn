﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.PersistentStorage;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.Storage
{
    internal class CloudCachePersistentStorage : AbstractPersistentStorage
    {
        private static readonly ObjectPool<byte[]> s_byteArrayPool = new(() => new byte[Checksum.HashSize]);
        private static readonly CloudCacheContainerKey s_solutionKey = new("Roslyn.Solution");

        private static readonly ConditionalWeakTable<ProjectState, ProjectCacheContainerKey> s_projectToContainerKey = new();

        private readonly ICloudCacheService _cacheService;

        private readonly ConditionalWeakTable<ProjectState, ProjectCacheContainerKey>.CreateValueCallback _projectToContainerKeyCallback;

        public CloudCachePersistentStorage(
            ICloudCacheService cacheService,
            string workingFolderPath,
            string relativePathBase,
            string databaseFilePath)
            : base(workingFolderPath, relativePathBase, databaseFilePath)
        {
            _cacheService = cacheService;
            _projectToContainerKeyCallback = ps => new(relativePathBase, ps.FilePath, ps.Name, ps.GetParseOptionsChecksum(CancellationToken.None));
        }

        public override void Dispose()
            => _cacheService.Dispose();

        private CloudCacheContainerKey? GetContainerKey(ProjectKey projectKey, Project? bulkLoadSnapshot)
        {
            return bulkLoadSnapshot != null
                ? s_projectToContainerKey.GetValue(bulkLoadSnapshot.State, _projectToContainerKeyCallback).ContainerKey
                : ProjectCacheContainerKey.CreateProjectContainerKey(this.SolutionFilePath, projectKey.FilePath, projectKey.Name, parseOptionsChecksum: null);
        }

        private CloudCacheContainerKey? GetContainerKey(DocumentKey documentKey, Document? bulkLoadSnapshot)
        {
            return bulkLoadSnapshot != null
                ? s_projectToContainerKey.GetValue(bulkLoadSnapshot.Project.State, _projectToContainerKeyCallback).GetValue(bulkLoadSnapshot.State)
                : ProjectCacheContainerKey.CreateDocumentContainerKey(this.SolutionFilePath, documentKey.Project.FilePath, documentKey.Project.Name, parseOptionsChecksum: null, documentKey.FilePath, documentKey.Name);
        }

        public override Task<bool> ChecksumMatchesAsync(string name, Checksum checksum, CancellationToken cancellationToken)
            => ChecksumMatchesAsync(name, checksum, s_solutionKey, cancellationToken);

        protected override Task<bool> ChecksumMatchesAsync(ProjectKey projectKey, Project? bulkLoadSnapshot, string name, Checksum checksum, CancellationToken cancellationToken)
            => ChecksumMatchesAsync(name, checksum, GetContainerKey(projectKey, bulkLoadSnapshot), cancellationToken);

        protected override Task<bool> ChecksumMatchesAsync(DocumentKey documentKey, Document? bulkLoadSnapshot, string name, Checksum checksum, CancellationToken cancellationToken)
            => ChecksumMatchesAsync(name, checksum, GetContainerKey(documentKey, bulkLoadSnapshot), cancellationToken);

        private async Task<bool> ChecksumMatchesAsync(string name, Checksum checksum, CloudCacheContainerKey? containerKey, CancellationToken cancellationToken)
        {
            if (containerKey == null)
                return false;

            using var bytes = s_byteArrayPool.GetPooledObject();
            checksum.WriteTo(bytes.Object);

            return await _cacheService.CheckExistsAsync(new CloudCacheItemKey(containerKey.Value, name, bytes.Object.AsMemory()), cancellationToken).ConfigureAwait(false);
        }

        public override Task<Stream?> ReadStreamAsync(string name, Checksum? checksum, CancellationToken cancellationToken)
            => ReadStreamAsync(name, checksum, s_solutionKey, cancellationToken);

        protected override Task<Stream?> ReadStreamAsync(ProjectKey projectKey, Project? bulkLoadSnapshot, string name, Checksum? checksum, CancellationToken cancellationToken)
            => ReadStreamAsync(name, checksum, GetContainerKey(projectKey, bulkLoadSnapshot), cancellationToken);

        protected override Task<Stream?> ReadStreamAsync(DocumentKey documentKey, Document? bulkLoadSnapshot, string name, Checksum? checksum, CancellationToken cancellationToken)
            => ReadStreamAsync(name, checksum, GetContainerKey(documentKey, bulkLoadSnapshot), cancellationToken);

        private async Task<Stream?> ReadStreamAsync(string name, Checksum? checksum, CloudCacheContainerKey? containerKey, CancellationToken cancellationToken)
        {
            if (containerKey == null)
                return null;

            if (checksum == null)
            {
                return await ReadStreamAsync(new CloudCacheItemKey(containerKey.Value, name), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var bytes = s_byteArrayPool.GetPooledObject();
                checksum.WriteTo(bytes.Object);

                return await ReadStreamAsync(new CloudCacheItemKey(containerKey.Value, name, bytes.Object.AsMemory()), cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<Stream?> ReadStreamAsync(CloudCacheItemKey key, CancellationToken cancellationToken)
        {
            var pipe = new Pipe();
            var result = await _cacheService.TryGetItemAsync(key, pipe.Writer, cancellationToken).ConfigureAwait(false);
            if (!result)
                return null;

            return pipe.Writer.AsStream();
        }

        public override Task<bool> WriteStreamAsync(string name, Stream stream, Checksum? checksum, CancellationToken cancellationToken)
            => WriteStreamAsync(name, stream, checksum, s_solutionKey, cancellationToken);

        public override Task<bool> WriteStreamAsync(Project project, string name, Stream stream, Checksum? checksum, CancellationToken cancellationToken)
            => WriteStreamAsync(name, stream, checksum, GetContainerKey((ProjectKey)project, project), cancellationToken);

        public override Task<bool> WriteStreamAsync(Document document, string name, Stream stream, Checksum? checksum, CancellationToken cancellationToken)
            => WriteStreamAsync(name, stream, checksum, GetContainerKey((DocumentKey)document, document), cancellationToken);

        private async Task<bool> WriteStreamAsync(string name, Stream stream, Checksum? checksum, CloudCacheContainerKey? containerKey, CancellationToken cancellationToken)
        {
            if (containerKey == null)
                return false;

            if (checksum == null)
            {
                return await WriteStreamAsync(new CloudCacheItemKey(containerKey.Value, name), stream, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var bytes = s_byteArrayPool.GetPooledObject();
                checksum.WriteTo(bytes.Object);

                return await WriteStreamAsync(new CloudCacheItemKey(containerKey.Value, name, bytes.Object.AsMemory()), stream, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<bool> WriteStreamAsync(CloudCacheItemKey key, Stream stream, CancellationToken cancellationToken)
        {
            // From: https://dev.azure.com/devdiv/DevDiv/_wiki/wikis/DevDiv.wiki/12664/VS-Cache-Service
            var pipe = new Pipe();
            var addItemTask = _cacheService.SetItemAsync(key, pipe.Reader, shareable: false, cancellationToken);
            try
            {
                await stream.CopyToAsync(pipe.Writer, cancellationToken).ConfigureAwait(false);
                await pipe.Writer.CompleteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await pipe.Writer.CompleteAsync(ex).ConfigureAwait(false);
                throw;
            }

            await addItemTask.ConfigureAwait(false);
            return true;
        }
    }
}
