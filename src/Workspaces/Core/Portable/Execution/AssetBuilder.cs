﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Execution
{
    internal struct AssetBuilder
    {
        private readonly Serializer _serializer;
        private readonly IChecksumTreeNode _checksumTree;

        public AssetBuilder(Solution solution) : this(new AssetOnlyTreeNode(solution))
        {
        }

        public AssetBuilder(IChecksumTreeNode checksumTree)
        {
            _checksumTree = checksumTree;
            _serializer = checksumTree.Serializer;
        }

        public Task<Asset> BuildAsync(SolutionState solutionState, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(solutionState, GetInfo(solutionState), WellKnownChecksumObjects.SolutionChecksumObjectInfo, CreateSolutionChecksumObjectInfoAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(ProjectState projectState, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(projectState, GetInfo(projectState), WellKnownChecksumObjects.ProjectChecksumObjectInfo, CreateProjectChecksumObjectInfoAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(TextDocumentState document, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(document, GetInfo(document), WellKnownChecksumObjects.DocumentChecksumObjectInfo, CreateDocumentChecksumObjectInfoAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(ProjectState projectState, CompilationOptions compilationOptions, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(compilationOptions, projectState, WellKnownChecksumObjects.CompilationOptions, CreateCompilationOptionsAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(ProjectState projectState, ParseOptions parseOptions, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(parseOptions, projectState, WellKnownChecksumObjects.ParseOptions, CreateParseOptionsAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(ProjectReference reference, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(reference, reference, WellKnownChecksumObjects.ProjectReference, CreateProjectReferenceAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(MetadataReference reference, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(reference, reference, WellKnownChecksumObjects.MetadataReference, CreateMetadataReferenceAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(AnalyzerReference reference, CancellationToken cancellationToken)
        {
            return _checksumTree.GetOrCreateAssetAsync(reference, reference, WellKnownChecksumObjects.AnalyzerReference, CreateAnalyzerReferenceAsync, cancellationToken);
        }

        public Task<Asset> BuildAsync(TextDocumentState state, SourceText unused, CancellationToken cancellationToken)
        {
            // TODO: currently this is a bit wierd not to hold onto source text.
            //       it would be nice if SourceText is changed like how recoverable syntax tree work.
            return _checksumTree.GetOrCreateAssetAsync(state, state, WellKnownChecksumObjects.SourceText, CreateSourceTextAsync, cancellationToken);
        }

        private Task<Asset> CreateSolutionChecksumObjectInfoAsync(SolutionChecksumObjectInfo info, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Asset>(new Asset<SolutionChecksumObjectInfo>(info, kind, _serializer.SerializeSolutionChecksumObjectInfo));
        }

        private Task<Asset> CreateProjectChecksumObjectInfoAsync(ProjectChecksumObjectInfo info, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Asset>(new Asset<ProjectChecksumObjectInfo>(info, kind, _serializer.SerializeProjectChecksumObjectInfo));
        }

        private Task<Asset> CreateDocumentChecksumObjectInfoAsync(DocumentChecksumObjectInfo info, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Asset>(new Asset<DocumentChecksumObjectInfo>(info, kind, _serializer.SerializeDocumentChecksumObjectInfo));
        }

        private Task<Asset> CreateCompilationOptionsAsync(ProjectState projectState, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Asset>(new LanguageSpecificAsset<CompilationOptions>(projectState.Language, projectState.CompilationOptions, kind, _serializer.SerializeCompilationOptions));
        }

        private Task<Asset> CreateParseOptionsAsync(ProjectState projectState, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Asset>(new LanguageSpecificAsset<ParseOptions>(projectState.Language, projectState.ParseOptions, kind, _serializer.SerializeParseOptions));
        }

        private Task<Asset> CreateProjectReferenceAsync(ProjectReference reference, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<Asset>(new Asset<ProjectReference>(reference, kind, _serializer.SerializeProjectReference));
        }

        private Task<Asset> CreateMetadataReferenceAsync(MetadataReference reference, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var checksum = _serializer.HostSerializationService.CreateChecksum(reference, cancellationToken);
            return Task.FromResult<Asset>(new MetadataReferenceAsset(_serializer, reference, checksum, kind));
        }

        private Task<Asset> CreateAnalyzerReferenceAsync(AnalyzerReference reference, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var checksum = _serializer.HostSerializationService.CreateChecksum(reference, cancellationToken);
            return Task.FromResult<Asset>(new AnalyzerReferenceAsset(_serializer, reference, checksum, kind));
        }

        private async Task<Asset> CreateSourceTextAsync(TextDocumentState state, string kind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = await state.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var checksum = new Checksum(text.GetChecksum());

            return new SourceTextAsset(_serializer, state, checksum, kind);
        }

        private SolutionChecksumObjectInfo GetInfo(SolutionState solutionState)
        {
            return new SolutionChecksumObjectInfo(solutionState.Id, solutionState.Version, solutionState.FilePath);
        }

        private ProjectChecksumObjectInfo GetInfo(ProjectState projectState)
        {
            return new ProjectChecksumObjectInfo(projectState.Id, projectState.Version, projectState.Name, projectState.AssemblyName, projectState.Language, projectState.FilePath, projectState.OutputFilePath);
        }

        private DocumentChecksumObjectInfo GetInfo(TextDocumentState documentState)
        {
            // we might just split it to TextDocument and Document.
            return new DocumentChecksumObjectInfo(documentState.Id, documentState.Name, documentState.Folders, GetSourceCodeKind(documentState), documentState.FilePath, IsGenerated(documentState));
        }

        private bool IsGenerated(TextDocumentState documentState)
        {
            var source = documentState as DocumentState;
            if (source != null)
            {
                return source.IsGenerated;
            }

            // no source
            return false;
        }

        private SourceCodeKind GetSourceCodeKind(TextDocumentState documentState)
        {
            var source = documentState as DocumentState;
            if (source != null)
            {
                return source.SourceCodeKind;
            }

            // no source
            return SourceCodeKind.Regular;
        }

        private sealed class AssetOnlyTreeNode : IChecksumTreeNode
        {
            public AssetOnlyTreeNode(Solution solution)
            {
                Serializer = new Serializer(solution.Workspace.Services);
            }

            public Serializer Serializer { get; }

            public Task<TResult> GetOrCreateAssetAsync<TKey, TValue, TResult>(
                TKey key, TValue value, string kind, Func<TValue, string, CancellationToken, Task<TResult>> valueGetterAsync, CancellationToken cancellationToken)
                where TKey : class
                where TResult : Asset
            {
                Contract.ThrowIfNull(key);
                return valueGetterAsync(value, kind, cancellationToken);
            }

            public Task<TResult> GetOrCreateChecksumObjectWithChildrenAsync<TKey, TValue, TResult>(
                TKey key, TValue value, string kind, Func<TKey, TValue, string, CancellationToken, Task<TResult>> valueGetterAsync, CancellationToken cancellationToken)
                where TKey : class
                where TResult : ChecksumObjectWithChildren
            {
                return Contract.FailWithReturn<Task<TResult>>("shouldn't be called");
            }

            public IChecksumTreeNode GetOrCreateSubTreeNode<TKey>(TKey key)
            {
                return Contract.FailWithReturn<IChecksumTreeNode>("shouldn't be called");
            }
        }
    }
}
