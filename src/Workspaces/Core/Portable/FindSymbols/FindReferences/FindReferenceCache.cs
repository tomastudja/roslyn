﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    internal static class FindReferenceCache
    {
        private static readonly ConditionalWeakTable<SemanticModel, Entry> s_cache = new();

        private static Entry GetEntry(SemanticModel model)
            => s_cache.GetValue(model, static _ => new());

        public static SymbolInfo GetSymbolInfo(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
        {
            var nodeCache = GetEntry(model).SymbolInfoCache;

            return nodeCache.GetOrAdd(node, static (n, arg) => arg.model.GetSymbolInfo(n, arg.cancellationToken), (model, cancellationToken));
        }

        public static IAliasSymbol? GetAliasInfo(
            ISemanticFactsService semanticFacts, SemanticModel model, SyntaxToken token, CancellationToken cancellationToken)
        {
            if (semanticFacts == null)
                return model.GetAliasInfo(token.GetRequiredParent(), cancellationToken);

            var entry = GetEntry(model);

            if (entry.AliasNameSet == null)
            {
                var set = semanticFacts.GetAliasNameSet(model, cancellationToken);
                Interlocked.CompareExchange(ref entry.AliasNameSet, set, null);
            }

            if (entry.AliasNameSet.Contains(token.ValueText))
                return model.GetAliasInfo(token.GetRequiredParent(), cancellationToken);

            return null;
        }

        public static async Task<ImmutableArray<SyntaxToken>> FindMatchingIdentifierTokensAsync(
            Document document,
            SemanticModel semanticModel,
            string identifier,
            CancellationToken cancellationToken)
        {
            // It's very costly to walk an entire tree.  So if the tree is simple and doesn't contain
            // any unicode escapes in it, then we do simple string matching to find the tokens.
            var info = await SyntaxTreeIndex.GetRequiredIndexAsync(document, cancellationToken).ConfigureAwait(false);

            // If this document doesn't even contain this identifier (escaped or non-escaped) we don't have to search it at all.
            if (!info.ProbablyContainsIdentifier(identifier))
                return ImmutableArray<SyntaxToken>.Empty;

            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();

            var root = await semanticModel.SyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            // If the identifier was escaped in the file then we'll have to do a more involved search that actually
            // walks the root and checks all identifier tokens.
            //
            // otherwise, we can use the text of the document to quickly find candidates and test those directly.
            var sourceText = !info.ProbablyContainsEscapedIdentifier(identifier)
                ? await document.GetTextAsync(cancellationToken).ConfigureAwait(false)
                : null;

            var normalizedIdentifier = syntaxFacts.IsCaseSensitive ? identifier : identifier.ToLowerInvariant();
            var entry = GetEntry(semanticModel);

            return entry.IdentifierCache.GetOrAdd(normalizedIdentifier,
                key => GetIdentifierTokensWithText(syntaxFacts, root, sourceText, key, cancellationToken));
        }

        /// <param name="sourceText">Text for the document being examined.  If not null, we are searching for an
        /// unescaped identifier, and we can just scan the text directly looking for hits.  If null, we're searching for
        /// escaped identifiers and we have to walk the root directly looking at every token</param>
        [PerformanceSensitive("https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1224834", AllowCaptures = false)]
        private static ImmutableArray<SyntaxToken> GetIdentifierTokensWithText(
            ISyntaxFactsService syntaxFacts,
            SyntaxNode root,
            SourceText? sourceText,
            string identifier,
            CancellationToken cancellationToken)
        {
            using var _ = ArrayBuilder<SyntaxToken>.GetInstance(out var result);

            if (sourceText == null)
            {
                // identifier is escaped.  Have to actually walk the entire tree to find matching tokens.
                Recurse(root);
            }
            else
            {
                // identifier is not escaped.  we can scan through the raw text of the file looking for matches.

                var index = 0;
                while ((index = sourceText.IndexOf(identifier, index, syntaxFacts.IsCaseSensitive)) >= 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var token = root.FindToken(index, findInsideTrivia: true);
                    var span = token.Span;
                    if (span.Start == index && span.Length == identifier.Length && IsMatch(token))
                        result.Add(token);

                    var nextIndex = index + identifier.Length;
                    nextIndex = Math.Max(nextIndex, token.SpanStart);
                    index = nextIndex;
                }
            }

            return result.ToImmutable();

            bool IsMatch(SyntaxToken token)
                => !token.IsMissing && syntaxFacts.IsIdentifier(token) && syntaxFacts.TextMatch(token.ValueText, identifier);

            void Recurse(SyntaxNode node)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var child in node.ChildNodesAndTokens())
                {
                    if (child.IsNode)
                    {
                        Recurse(child.AsNode()!);
                    }
                    else if (child.IsToken)
                    {
                        var token = child.AsToken();
                        if (IsMatch(token))
                            result.Add(token);

                        if (token.HasStructuredTrivia)
                        {
                            // structured trivia can only be leading trivia
                            foreach (var trivia in token.LeadingTrivia)
                            {
                                if (trivia.HasStructure)
                                    Recurse(trivia.GetStructure());
                            }
                        }
                    }
                }
            }
        }

        public static IEnumerable<SyntaxToken> GetConstructorInitializerTokens(
            ISyntaxFactsService syntaxFacts, SemanticModel model, SyntaxNode root, CancellationToken cancellationToken)
        {
            // this one will only get called when we know given document contains constructor initializer.
            // no reason to use text to check whether it exist first.
            var entry = GetEntry(model);

            if (entry.ConstructorInitializerCache.IsDefault)
            {
                var initializers = GetConstructorInitializerTokens(syntaxFacts, root, cancellationToken);
                ImmutableInterlocked.InterlockedInitialize(ref entry.ConstructorInitializerCache, initializers);
            }

            return entry.ConstructorInitializerCache;
        }

        private static ImmutableArray<SyntaxToken> GetConstructorInitializerTokens(
            ISyntaxFactsService syntaxFacts, SyntaxNode root, CancellationToken cancellationToken)
        {
            using var _ = ArrayBuilder<SyntaxToken>.GetInstance(out var initializers);
            foreach (var constructor in syntaxFacts.GetConstructors(root, cancellationToken))
            {
                foreach (var token in constructor.DescendantTokens(descendIntoTrivia: false))
                {
                    if (syntaxFacts.IsThisConstructorInitializer(token) || syntaxFacts.IsBaseConstructorInitializer(token))
                        initializers.Add(token);
                }
            }

            return initializers.ToImmutable();
        }

        private sealed class Entry
        {
            public ImmutableHashSet<string>? AliasNameSet;
            public ImmutableArray<SyntaxToken> ConstructorInitializerCache;

            public readonly ConcurrentDictionary<string, ImmutableArray<SyntaxToken>> IdentifierCache = new();
            public readonly ConcurrentDictionary<SyntaxNode, SymbolInfo> SymbolInfoCache = new();
        }
    }
}
