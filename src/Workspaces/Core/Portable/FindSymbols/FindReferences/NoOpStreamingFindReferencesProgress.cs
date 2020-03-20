﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Shared.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    /// <summary>
    /// A does-nothing version of the <see cref="IStreamingFindReferencesProgress"/>. Useful for
    /// clients that have no need to report progress as they work.
    /// </summary>
    internal class NoOpStreamingFindReferencesProgress : IStreamingFindReferencesProgress
    {
        public static readonly IStreamingFindReferencesProgress Instance =
            new NoOpStreamingFindReferencesProgress();

        public IStreamingProgressTracker ProgressTracker { get; }

        private NoOpStreamingFindReferencesProgress()
        {
            this.ProgressTracker = new NoOpProgressTracker();
        }

        public Task ReportProgressAsync(int current, int maximum) => Task.CompletedTask;

        public Task OnCompletedAsync() => Task.CompletedTask;
        public Task OnStartedAsync() => Task.CompletedTask;
        public Task OnDefinitionFoundAsync(SymbolAndProjectId symbol) => Task.CompletedTask;
        public Task OnReferenceFoundAsync(SymbolAndProjectId symbol, ReferenceLocation location) => Task.CompletedTask;
        public Task OnFindInDocumentStartedAsync(Document document) => Task.CompletedTask;
        public Task OnFindInDocumentCompletedAsync(Document document) => Task.CompletedTask;

        private class NoOpProgressTracker : IStreamingProgressTracker
        {
            public Task AddItemsAsync(int count) => Task.CompletedTask;
            public Task ItemCompletedAsync() => Task.CompletedTask;
        }
    }
}
