﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Completion.Providers
{
    internal static class ImportCompletionProviderHelper
    {
        private record class CacheEntry(Checksum Checksum, ImmutableArray<string> GlobalUsings);
        private static readonly ConcurrentDictionary<DocumentId, CacheEntry> _sdkGlobalUsingsCache = new();

        public static async Task<ImmutableArray<string>> GetImportedNamespacesAsync(SyntaxContext context, CancellationToken cancellationToken)
        {
            // The location is the containing node of the LeftToken, or the compilation unit itself if LeftToken
            // indicates the beginning of the document (i.e. no parent).
            var location = context.LeftToken.Parent ?? context.SyntaxTree.GetRoot(cancellationToken);
            var usingsFromCurrentDocument = context.SemanticModel.GetUsingNamespacesInScope(location).SelectAsArray(GetNamespaceName);

            // Since we don't have a compiler API to easily get all global usings yet, hardcode the the name of SDK auto-generated
            // global using file (which is a constant) for now as a temporary workaround.
            // https://github.com/dotnet/sdk/blob/main/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.GenerateGlobalUsings.targets
            var fileName = context.Document.Project.Name + ".GlobalUsings.g.cs";
            var globalUsingDocument = context.Document.Project.Documents.FirstOrDefault(d => d.Name.Equals(fileName));

            if (globalUsingDocument is null)
                return usingsFromCurrentDocument;

            ImmutableArray<string> globalUsings;
            if (_sdkGlobalUsingsCache.TryGetValue(globalUsingDocument.Id, out var cacheEntry))
            {
                // Just return whatever was cached last time. It'd be very rare for this file to change. To minimize impact on completion perf,
                // we'd tolerate temporarily staled results. A background task is created to refresh it if necessary.
                _ = GetGlobalUsingsAsync(globalUsingDocument, cacheEntry, CancellationToken.None);
                globalUsings = cacheEntry.GlobalUsings;
            }
            else
            {
                // cache miss, we have to calculate it now.
                globalUsings = await GetGlobalUsingsAsync(globalUsingDocument, cacheEntry: null, cancellationToken).ConfigureAwait(false);
            }

            return usingsFromCurrentDocument.Concat(globalUsings);

            static async Task<ImmutableArray<string>> GetGlobalUsingsAsync(Document document, CacheEntry? cacheEntry, CancellationToken cancellationToken)
            {
                // We only checksum off of the contents of the file.
                var checksum = await document.State.GetChecksumAsync(cancellationToken).ConfigureAwait(false);
                if (cacheEntry != null && checksum == cacheEntry.Checksum)
                    return cacheEntry.GlobalUsings;

                var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var model = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                var globalUsings = model.GetUsingNamespacesInScope(root).SelectAsArray(GetNamespaceName);

                _sdkGlobalUsingsCache[document.Id] = new(checksum, globalUsings);
                return globalUsings;
            }

            static string GetNamespaceName(INamespaceSymbol symbol)
                => symbol.ToDisplayString(SymbolDisplayFormats.NameFormat);
        }

    }
}
