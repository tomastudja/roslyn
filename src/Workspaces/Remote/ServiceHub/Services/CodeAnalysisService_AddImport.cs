﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.AddImport;
using Microsoft.CodeAnalysis.Packaging;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.SymbolSearch;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote
{
    internal partial class CodeAnalysisService : IRemoteAddImportFeatureService
    {
        public async Task<ImmutableArray<AddImportFixData>> GetFixesAsync(
            DocumentId documentId, TextSpan span, string diagnosticId, bool placeSystemNamespaceFirst,
            bool searchReferenceAssemblies, ImmutableArray<PackageSource> packageSources)
        {
            using (UserOperationBooster.Boost())
            {
                var solution = await GetSolutionAsync().ConfigureAwait(false);
                var document = solution.GetDocument(documentId);

                var service = document.GetLanguageService<IAddImportFeatureService>();

                var symbolSearchService = new SymbolSearchService(this);

                var result = await service.GetFixesAsync(
                    document, span, diagnosticId, placeSystemNamespaceFirst,
                    symbolSearchService, searchReferenceAssemblies,
                    packageSources, CancellationToken).ConfigureAwait(false);

                return result;
            }
        }

        /// <summary>
        /// Provides an implementation of the ISymbolSearchService on the remote side so that
        /// Add-Import can find results in nuget packages/reference assemblies.  This works
        /// by remoting *from* the OOP server back to the host, which can then forward this 
        /// appropriately to wherever the real ISymbolSearchService is running.  This is necessary
        /// because it's not guaranteed that the real ISymbolSearchService will be running in 
        /// the same process that is supplying the <see cref="CodeAnalysisService"/>.
        /// 
        /// Ideally we would not need to bounce back to the host for this.
        /// </summary>
        private class SymbolSearchService : ISymbolSearchService
        {
            private readonly CodeAnalysisService codeAnalysisService;

            public SymbolSearchService(CodeAnalysisService codeAnalysisService)
            {
                this.codeAnalysisService = codeAnalysisService;
            }

            public async Task<ImmutableArray<PackageWithTypeResult>> FindPackagesWithTypeAsync(
                string source, string name, int arity, CancellationToken cancellationToken)
            {
                var result = await codeAnalysisService.Rpc.InvokeAsync<ImmutableArray<PackageWithTypeResult>>(
                    nameof(FindPackagesWithTypeAsync), source, name, arity).ConfigureAwait(false);

                return result;
            }

            public async Task<ImmutableArray<PackageWithAssemblyResult>> FindPackagesWithAssemblyAsync(
                string source, string assemblyName, CancellationToken cancellationToken)
            {
                var result = await codeAnalysisService.Rpc.InvokeAsync<ImmutableArray<PackageWithAssemblyResult>>(
                    nameof(FindPackagesWithAssemblyAsync), source, assemblyName).ConfigureAwait(false);

                return result;
            }

            public async Task<ImmutableArray<ReferenceAssemblyWithTypeResult>> FindReferenceAssembliesWithTypeAsync(
                string name, int arity, CancellationToken cancellationToken)
            {
                var result = await codeAnalysisService.Rpc.InvokeAsync<ImmutableArray<ReferenceAssemblyWithTypeResult>>(
                    nameof(FindReferenceAssembliesWithTypeAsync), name, arity).ConfigureAwait(false);

                return result;
            }
        }
    }
}