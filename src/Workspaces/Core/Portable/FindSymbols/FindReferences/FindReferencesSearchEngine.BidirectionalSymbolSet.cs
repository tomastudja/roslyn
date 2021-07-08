﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    internal partial class FindReferencesSearchEngine
    {
        /// <summary>
        /// Symbol set used when <see cref="FindReferencesSearchOptions.UnidirectionalHierarchyCascade"/> is <see
        /// langword="false"/>.
        /// </summary>
        private sealed class BidirectionalSymbolSet : SymbolSet
        {
            private readonly HashSet<ISymbol> _allSymbols = new();

            public BidirectionalSymbolSet(FindReferencesSearchEngine engine, HashSet<ISymbol> initialSymbols, HashSet<ISymbol> upSymbols)
                : base(engine)
            {
                _allSymbols.AddRange(initialSymbols);
                _allSymbols.AddRange(upSymbols);
            }

            public override ImmutableArray<ISymbol> GetAllSymbols()
                => _allSymbols.ToImmutableArray();

            public override async Task InheritanceCascadeAsync(Project project, CancellationToken cancellationToken)
            {
                // Start searching using the existing set of symbols found at the start (or anything found below that).
                var workQueue = new Stack<ISymbol>();
                PushAll(workQueue, _allSymbols);

                var projects = ImmutableHashSet.Create(project);

                while (workQueue.Count > 0)
                {
                    var current = workQueue.Pop();

                    // For each symbol we're examining try to walk both up and down from it to see if we discover any
                    // new symbols in this project.  As long as we keep finding symbols, we'll keep searching.
                    await AddDownSymbolsAsync(current, _allSymbols, workQueue, projects, cancellationToken).ConfigureAwait(false);
                    await AddUpSymbolsAsync(this.Engine, current, _allSymbols, workQueue, projects, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
