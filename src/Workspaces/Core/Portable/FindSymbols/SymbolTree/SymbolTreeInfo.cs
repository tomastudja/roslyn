﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    internal partial class SymbolTreeInfo
    {
        private readonly VersionStamp _version;

        /// <summary>
        /// To prevent lots of allocations, we concatenate all the names in all our
        /// Nodes into one long string.  Each Node then just points at the span in
        /// this string with the portion they care about.
        /// </summary>
        private readonly string _concatenatedNames;

        /// <summary>
        /// The list of nodes that represent symbols. The primary key into the sorting of this 
        /// list is the name. They are sorted case-insensitively with the <see cref="s_totalComparer" />.
        /// Finding case-sensitive matches can be found by binary searching for something that 
        /// matches insensitively, and then searching around that equivalence class for one that 
        /// matches.
        /// </summary>
        private readonly ImmutableArray<Node> _nodes;

        /// <summary>
        /// Inheritance information for the types in this assembly.  The mapping is between
        /// a type's simple name (like 'IDictionary') and the simple metadata names of types 
        /// that implement it or derive from it (like 'Dictionary').
        /// 
        /// Note: to save space, all names in this map are stored with simple ints.  These
        /// ints are the indices into _nodes that contain the nodes with the appropriate name.
        /// 
        /// This mapping is only produced for metadata assemblies.
        /// </summary>
        private readonly OrderPreservingMultiDictionary<int, int> _inheritanceMap;

        /// <summary>
        /// The task that produces the spell checker we use for fuzzy match queries.
        /// We use a task so that we can generate the <see cref="SymbolTreeInfo"/> 
        /// without having to wait for the spell checker construction to finish.
        /// 
        /// Features that don't need fuzzy matching don't want to incur the cost of 
        /// the creation of this value.  And the only feature which does want fuzzy
        /// matching (add-using) doesn't want to block waiting for the value to be
        /// created.
        /// </summary>
        private readonly Task<SpellChecker> _spellCheckerTask;

        private static readonly StringSliceComparer s_caseInsensitiveComparer =
            StringSliceComparer.OrdinalIgnoreCase;

        // We first sort in a case insensitive manner.  But, within items that match insensitively, 
        // we then sort in a case sensitive manner.  This helps for searching as we'll walk all 
        // the items of a specific casing at once.  This way features can cache values for that
        // casing and reuse them.  i.e. if we didn't do this we might get "Prop, prop, Prop, prop"
        // which might cause other features to continually recalculate if that string matches what
        // they're searching for.  However, with this sort of comparison we now get 
        // "prop, prop, Prop, Prop".  Features can take advantage of that by caching their previous
        // result and reusing it when they see they're getting the same string again.
        private static readonly Comparison<string> s_totalComparer = (s1, s2) =>
        {
            var diff = CaseInsensitiveComparison.Comparer.Compare(s1, s2);
            return diff != 0
                ? diff
                : StringComparer.Ordinal.Compare(s1, s2);
        };

        private SymbolTreeInfo(
            VersionStamp version,
            string concatenatedNames,
            Node[] sortedNodes,
            Task<SpellChecker> spellCheckerTask,
            OrderPreservingMultiDictionary<string, string> inheritanceMap)
            : this(version, concatenatedNames, sortedNodes, spellCheckerTask)
        {
            var indexBasedInheritanceMap = CreateIndexBasedInheritanceMap(inheritanceMap);
            _inheritanceMap = indexBasedInheritanceMap;
        }

        private SymbolTreeInfo(
            VersionStamp version,
            string concatenatedNames,
            Node[] sortedNodes,
            Task<SpellChecker> spellCheckerTask,
            OrderPreservingMultiDictionary<int, int> inheritanceMap)
            : this(version, concatenatedNames, sortedNodes, spellCheckerTask)
        {
            _inheritanceMap = inheritanceMap;
        }

        private SymbolTreeInfo(
            VersionStamp version,
            string concatenatedNames,
            Node[] sortedNodes, 
            Task<SpellChecker> spellCheckerTask)
        {
            _version = version;
            _concatenatedNames = concatenatedNames;
            _nodes = ImmutableArray.Create(sortedNodes);
            _spellCheckerTask = spellCheckerTask;
        }

        public Task<IEnumerable<ISymbol>> FindAsync(
            SearchQuery query, IAssemblySymbol assembly, SymbolFilter filter, CancellationToken cancellationToken)
        {
            return this.FindAsync(query, new AsyncLazy<IAssemblySymbol>(assembly), filter, cancellationToken);
        }

        public async Task<IEnumerable<ISymbol>> FindAsync(
            SearchQuery query, AsyncLazy<IAssemblySymbol> lazyAssembly, SymbolFilter filter, CancellationToken cancellationToken)
        {
            return SymbolFinder.FilterByCriteria(
                await FindAsyncWorker(query, lazyAssembly, cancellationToken).ConfigureAwait(false),
                filter);
        }

        private Task<IEnumerable<ISymbol>> FindAsyncWorker(
            SearchQuery query, AsyncLazy<IAssemblySymbol> lazyAssembly, CancellationToken cancellationToken)
        {
            // If the query has a specific string provided, then call into the SymbolTreeInfo
            // helpers optimized for lookup based on an exact name.
            switch (query.Kind)
            {
                case SearchKind.Exact:
                    return this.FindAsync(lazyAssembly, query.Name, ignoreCase: false, cancellationToken: cancellationToken);
                case SearchKind.ExactIgnoreCase:
                    return this.FindAsync(lazyAssembly, query.Name, ignoreCase: true, cancellationToken: cancellationToken);
                case SearchKind.Fuzzy:
                    return this.FuzzyFindAsync(lazyAssembly, query.Name, cancellationToken);
                case SearchKind.Custom:
                    // Otherwise, we'll have to do a slow linear search over all possible symbols.
                    return this.FindAsync(lazyAssembly, query.GetPredicate(), cancellationToken);
            }

            throw new InvalidOperationException();
        }

        /// <summary>
        /// Finds symbols in this assembly that match the provided name in a fuzzy manner.
        /// </summary>
        private async Task<IEnumerable<ISymbol>> FuzzyFindAsync(
            AsyncLazy<IAssemblySymbol> lazyAssembly, string name, CancellationToken cancellationToken)
        {
            if (_spellCheckerTask.Status != TaskStatus.RanToCompletion)
            {
                // Spell checker isn't ready.  Just return immediately.
                return SpecializedCollections.EmptyEnumerable<ISymbol>();
            }

            var spellChecker = _spellCheckerTask.Result;
            var similarNames = spellChecker.FindSimilarWords(name, substringsAreSimilar: false);
            var result = new List<ISymbol>();

            foreach (var similarName in similarNames)
            {
                var symbols = await FindAsync(lazyAssembly, similarName, ignoreCase: true, cancellationToken: cancellationToken).ConfigureAwait(false);
                result.AddRange(symbols);
            }

            return result;
        }

        /// <summary>
        /// Get all symbols that have a name matching the specified name.
        /// </summary>
        private async Task<IEnumerable<ISymbol>> FindAsync(
            AsyncLazy<IAssemblySymbol> lazyAssembly,
            string name,
            bool ignoreCase,
            CancellationToken cancellationToken)
        {
            var comparer = GetComparer(ignoreCase);
            var result = new List<ISymbol>();
            IAssemblySymbol assemblySymbol = null;

            foreach (var node in FindNodeIndices(name, comparer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                assemblySymbol = assemblySymbol ?? await lazyAssembly.GetValueAsync(cancellationToken).ConfigureAwait(false);

                result.AddRange(Bind(node, assemblySymbol.GlobalNamespace, cancellationToken));
            }

            return result;
        }

        /// <summary>
        /// Slow, linear scan of all the symbols in this assembly to look for matches.
        /// </summary>
        private async Task<IEnumerable<ISymbol>> FindAsync(
            AsyncLazy<IAssemblySymbol> lazyAssembly, Func<string, bool> predicate, CancellationToken cancellationToken)
        {
            var result = new List<ISymbol>();
            IAssemblySymbol assembly = null;
            for (int i = 0, n = _nodes.Length; i < n; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (predicate(GetName(_nodes[i])))
                {
                    assembly = assembly ?? await lazyAssembly.GetValueAsync(cancellationToken).ConfigureAwait(false);

                    result.AddRange(Bind(i, assembly.GlobalNamespace, cancellationToken));
                }
            }

            return result;
        }

        private static StringSliceComparer GetComparer(bool ignoreCase)
        {
            return ignoreCase
                ? StringSliceComparer.OrdinalIgnoreCase
                : StringSliceComparer.Ordinal;
        }

        /// <summary>
        /// Gets all the node indices with matching names per the <paramref name="comparer" />.
        /// </summary>
        private IEnumerable<int> FindNodeIndices(
            string name, StringSliceComparer comparer)
        {
            // find any node that matches case-insensitively
            var startingPosition = BinarySearch(name);
            var nameSlice = new StringSlice(name);

            if (startingPosition != -1)
            {
                // yield if this matches by the actual given comparer
                if (comparer.Equals(nameSlice, GetNameSlice(startingPosition)))
                {
                    yield return startingPosition;
                }

                int position = startingPosition;
                while (position > 0 && s_caseInsensitiveComparer.Equals(GetNameSlice(position - 1), nameSlice))
                {
                    position--;
                    if (comparer.Equals(GetNameSlice(position), nameSlice))
                    {
                        yield return position;
                    }
                }

                position = startingPosition;
                while (position + 1 < _nodes.Length && s_caseInsensitiveComparer.Equals(GetNameSlice(position + 1), nameSlice))
                {
                    position++;
                    if (comparer.Equals(GetNameSlice(position), nameSlice))
                    {
                        yield return position;
                    }
                }
            }
        }

        private StringSlice GetNameSlice(int nodeIndex)
        {
            return new StringSlice(_concatenatedNames, _nodes[nodeIndex].NameSpan);
        }

        /// <summary>
        /// Searches for a name in the ordered list that matches per the <see cref="s_caseInsensitiveComparer" />.
        /// </summary>
        private int BinarySearch(string name)
        {
            var nameSlice = new StringSlice(name);
            int max = _nodes.Length - 1;
            int min = 0;

            while (max >= min)
            {
                int mid = min + ((max - min) >> 1);

                var comparison = s_caseInsensitiveComparer.Compare(GetNameSlice(mid), nameSlice);
                if (comparison < 0)
                {
                    min = mid + 1;
                }
                else if (comparison > 0)
                {
                    max = mid - 1;
                }
                else
                {
                    return mid;
                }
            }

            return -1;
        }

        #region Construction

        // Cache the symbol tree infos for assembly symbols that share the same underlying metadata.
        // Generating symbol trees for metadata can be expensive (in large metadata cases).  And it's
        // common for us to have many threads to want to search the same metadata simultaneously.
        // As such, we want to only allow one thread to produce the tree for some piece of metadata
        // at a time.  
        //
        // AsyncLazy would normally be an ok choice here.  However, in the case where all clients
        // cancel their request, we don't want ot keep the AsyncLazy around.  It may capture a lot
        // of immutable state (like a Solution) that we don't want kept around indefinitely.  So we
        // only cache results (the symbol tree infos) if they successfully compute to completion.
        private static readonly ConditionalWeakTable<MetadataId, SemaphoreSlim> s_metadataIdToGate = new ConditionalWeakTable<MetadataId, SemaphoreSlim>();
        private static readonly ConditionalWeakTable<MetadataId, SymbolTreeInfo> s_metadataIdToInfo = new ConditionalWeakTable<MetadataId, SymbolTreeInfo>();

        private static readonly ConditionalWeakTable<MetadataId, SemaphoreSlim>.CreateValueCallback s_metadataIdToGateCallback =
            _ => new SemaphoreSlim(1);

        private static Task<SpellChecker> GetSpellCheckerTask(
            Solution solution, VersionStamp version, string filePath, 
            string concatenatedNames, Node[] sortedNodes)
        {
            // Create a new task to attempt to load or create the spell checker for this 
            // SymbolTreeInfo.  This way the SymbolTreeInfo will be ready immediately
            // for non-fuzzy searches, and soon afterwards it will be able to perform
            // fuzzy searches as well.
            return Task.Run(() => LoadOrCreateSpellCheckerAsync(solution, filePath,
                v => new SpellChecker(v, sortedNodes.Select(n => new StringSlice(concatenatedNames, n.NameSpan)))));
        }

        private static void SortNodes(
            ImmutableArray<BuilderNode> unsortedNodes,
            out string concatenatedNames,
            out Node[] sortedNodes)
        {
            // Generate index numbers from 0 to Count-1
            var tmp = new int[unsortedNodes.Length];
            for (int i = 0; i < tmp.Length; i++)
            {
                tmp[i] = i;
            }

            // Sort the index according to node elements
            Array.Sort<int>(tmp, (a, b) => CompareNodes(unsortedNodes[a], unsortedNodes[b], unsortedNodes));

            // Use the sort order to build the ranking table which will
            // be used as the map from original (unsorted) location to the
            // sorted location.
            var ranking = new int[unsortedNodes.Length];
            for (int i = 0; i < tmp.Length; i++)
            {
                ranking[tmp[i]] = i;
            }

            // No longer need the tmp array
            tmp = null;

            var result = new Node[unsortedNodes.Length];
            var concatenatedNamesBuilder = new StringBuilder();
            string lastName = null;

            // Copy nodes into the result array in the appropriate order and fixing
            // up parent indexes as we go.
            for (int i = 0; i < unsortedNodes.Length; i++)
            {
                var n = unsortedNodes[i];
                var currentName = n.Name;

                if (currentName != lastName)
                {
                    concatenatedNamesBuilder.Append(currentName);
                }

                result[ranking[i]] = new Node(
                    new TextSpan(concatenatedNamesBuilder.Length - currentName.Length, currentName.Length),
                    n.IsRoot ? n.ParentIndex : ranking[n.ParentIndex]);

                lastName = currentName;
            }

            sortedNodes = result;
            concatenatedNames = concatenatedNamesBuilder.ToString();
        }

        private static int CompareNodes(
            BuilderNode x, BuilderNode y, ImmutableArray<BuilderNode> nodeList)
        {
            var comp = s_totalComparer(x.Name, y.Name);
            if (comp == 0)
            {
                if (x.ParentIndex != y.ParentIndex)
                {
                    if (x.IsRoot)
                    {
                        return -1;
                    }
                    else if (y.IsRoot)
                    {
                        return 1;
                    }
                    else
                    {
                        return CompareNodes(nodeList[x.ParentIndex], nodeList[y.ParentIndex], nodeList);
                    }
                }
            }

            return comp;
        }

        #endregion

        #region Binding 

        // returns all the symbols in the container corresponding to the node
        private IEnumerable<ISymbol> Bind(
            int index, INamespaceOrTypeSymbol rootContainer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (var symbols = SharedPools.Default<List<ISymbol>>().GetPooledObject())
            {
                BindWorker(index, rootContainer, symbols.Object, cancellationToken);

                foreach (var symbol in symbols.Object)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return symbol;
                }
            }
        }

        // returns all the symbols in the container corresponding to the node
        private void BindWorker(
            int index, INamespaceOrTypeSymbol rootContainer, List<ISymbol> results, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var node = _nodes[index];
            if (node.IsRoot)
            {
                return;
            }

            if (_nodes[node.ParentIndex].IsRoot)
            {
                results.AddRange(rootContainer.GetMembers(GetName(node)));
            }
            else
            {
                using (var containerSymbols = SharedPools.Default<List<ISymbol>>().GetPooledObject())
                {
                    BindWorker(node.ParentIndex, rootContainer, containerSymbols.Object, cancellationToken);

                    foreach (var containerSymbol in containerSymbols.Object.OfType<INamespaceOrTypeSymbol>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        results.AddRange(containerSymbol.GetMembers(GetName(node)));
                    }
                }
            }
        }

        private string GetName(Node node)
        {
            return _concatenatedNames.Substring(node.NameSpan.Start, node.NameSpan.Length);
        }

        #endregion

        internal void AssertEquivalentTo(SymbolTreeInfo other)
        {
            Debug.Assert(_version.Equals(other._version));
            Debug.Assert(_nodes.Length == other._nodes.Length);

            for (int i = 0, n = _nodes.Length; i < n; i++)
            {
                _nodes[i].AssertEquivalentTo(other._nodes[i]);
            }

            Debug.Assert(_inheritanceMap.Keys.Count == other._inheritanceMap.Keys.Count);
            var orderedKeys1 = this._inheritanceMap.Keys.Order().ToList();
            var orderedKeys2 = other._inheritanceMap.Keys.Order().ToList();

            for (int i = 0; i < orderedKeys1.Count; i++)
            {
                var values1 = this._inheritanceMap[i];
                var values2 = other._inheritanceMap[i];

                Debug.Assert(values1.Length == values2.Length);
                for (int j = 0; j < values1.Length; j++)
                {
                    Debug.Assert(values1[j] == values2[j]);
                }
            }
        }

        private static SymbolTreeInfo CreateSymbolTreeInfo(
            Solution solution, VersionStamp version, 
            string filePath, ImmutableArray<BuilderNode> unsortedNodes,
            OrderPreservingMultiDictionary<string, string> inheritanceMap)
        {
            string concatenatedNames;
            Node[] sortedNodes;
            SortNodes(unsortedNodes, out concatenatedNames, out sortedNodes);
            var createSpellCheckerTask = GetSpellCheckerTask(
                solution, version, filePath, concatenatedNames, sortedNodes);

            return new SymbolTreeInfo(
                version, concatenatedNames, sortedNodes, createSpellCheckerTask, inheritanceMap);
        }

        private OrderPreservingMultiDictionary<int, int> CreateIndexBasedInheritanceMap(
            OrderPreservingMultiDictionary<string, string> inheritanceMap)
        {
            // All names in metadata will be case sensitive.  
            var comparer = GetComparer(ignoreCase: false);
            var result = new OrderPreservingMultiDictionary<int, int>();

            foreach (var kvp in inheritanceMap)
            {
                var baseName = kvp.Key;
                var baseNameIndex = BinarySearch(baseName);
                Debug.Assert(baseNameIndex >= 0);

                foreach (var derivedName in kvp.Value)
                {
                    foreach (var derivedNameIndex in FindNodeIndices(derivedName, comparer))
                    {
                        result.Add(baseNameIndex, derivedNameIndex);
                    }
                }
            }

            return result;
        }

        public IEnumerable<INamedTypeSymbol> GetDerivedMetadataTypes(
            string baseTypeName, Compilation compilation, CancellationToken cancellationToken)
        {
            var baseTypeNameIndex = BinarySearch(baseTypeName);
            var derivedTypeIndices = _inheritanceMap[baseTypeNameIndex];

            return from derivedTypeIndex in derivedTypeIndices
                   from symbol in Bind(derivedTypeIndex, compilation.GlobalNamespace, cancellationToken)
                   let namedType = symbol as INamedTypeSymbol
                   select namedType;
        }
    }
}