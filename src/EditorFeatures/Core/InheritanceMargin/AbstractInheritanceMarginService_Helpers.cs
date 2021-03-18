﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.FindUsages;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.InheritanceMargin
{
    internal partial class AbstractInheritanceMarginService
    {
        private static async Task<InheritanceMemberItem> CreateInheritanceMemberInfoAsync(
            Solution solution,
            INamedTypeSymbol memberSymbol,
            int lineNumber,
            ImmutableArray<ISymbol> baseSymbols,
            ImmutableArray<ISymbol> derivedTypesSymbols,
            CancellationToken cancellationToken)
        {
            var baseSymbolItems = await baseSymbols
                .SelectAsArrayAsync(symbol => CreateInheritanceItemAsync(solution, symbol, InheritanceRelationship.Implementing, cancellationToken))
                .ConfigureAwait(false);

            var derivedTypeItems = await derivedTypesSymbols
                .SelectAsArrayAsync(symbol => CreateInheritanceItemAsync(solution, symbol, InheritanceRelationship.Implemented, cancellationToken))
                .ConfigureAwait(false);

            return new InheritanceMemberItem(
                lineNumber,
                memberSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                memberSymbol.GetGlyph(),
                baseSymbolItems.Concat(derivedTypeItems));
        }

        private static async ValueTask<InheritanceTargetItem> CreateInheritanceItemAsync(
            Solution solution,
            ISymbol targetSymbol,
            InheritanceRelationship inheritanceRelationshipWithOriginalMember,
            CancellationToken cancellationToken)
        {
            var definition = await targetSymbol.ToClassifiedDefinitionItemAsync(
                solution,
                isPrimary: true,
                includeHiddenLocations: false,
                FindReferencesSearchOptions.Default,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return new InheritanceTargetItem(
                inheritanceRelationshipWithOriginalMember,
                definition);
        }

        private static async Task<InheritanceMemberItem> CreateInheritanceMemberInfoForMemberAsync(
            Solution solution,
            ISymbol memberSymbol,
            int lineNumber,
            ImmutableArray<ISymbol> implementingMembers,
            ImmutableArray<ISymbol> implementedMembers,
            ImmutableArray<ISymbol> overridenMembers,
            ImmutableArray<ISymbol> overridingMembers,
            CancellationToken cancellationToken)
        {
            var implementingMemberItems = await implementingMembers
                .SelectAsArrayAsync(symbol => CreateInheritanceItemAsync(solution, symbol, InheritanceRelationship.Implementing, cancellationToken)).ConfigureAwait(false);
            var implementedMemberItems = await implementedMembers
                .SelectAsArrayAsync(symbol => CreateInheritanceItemAsync(solution, symbol, InheritanceRelationship.Implemented, cancellationToken)).ConfigureAwait(false);
            var overridenMemberItems = await overridenMembers
                .SelectAsArrayAsync(symbol => CreateInheritanceItemAsync(solution, symbol, InheritanceRelationship.Overridden, cancellationToken)).ConfigureAwait(false);
            var overridingMemberItems = await overridingMembers
                .SelectAsArrayAsync(symbol => CreateInheritanceItemAsync(solution, symbol, InheritanceRelationship.Overriding, cancellationToken)).ConfigureAwait(false);

            return new InheritanceMemberItem(
                lineNumber,
                memberSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                memberSymbol.GetGlyph(),
                implementingMemberItems.Concat(implementedMemberItems)
                    .Concat(overridenMemberItems)
                    .Concat(overridingMemberItems));
        }

        /// <summary>
        /// For the <param name="memberSymbol"/>, get all the implemented symbols.
        /// </summary>
        private static async Task<ImmutableArray<ISymbol>> GetImplementedSymbolsAsync(
            Solution solution,
            ISymbol memberSymbol,
            CancellationToken cancellationToken)
        {
            if (memberSymbol is INamedTypeSymbol { IsSealed: false } namedTypeSymbol)
            {
                var derivedTypes = await GetDerivedTypesAndImplementationsAsync(solution, namedTypeSymbol, cancellationToken).ConfigureAwait(false);
                return derivedTypes.CastArray<ISymbol>().ToImmutableArray();
            }

            if (memberSymbol is IMethodSymbol or IEventSymbol or IPropertySymbol
                 && memberSymbol.ContainingSymbol.IsInterfaceType())
            {
                return await SymbolFinder.FindMemberImplementationsArrayAsync(
                    memberSymbol,
                    solution,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return ImmutableArray<ISymbol>.Empty;
        }

        /// <summary>
        /// Get members overriding the <param name="memberSymbol"/>
        /// </summary>
        private static ImmutableArray<ISymbol> GetOverridingSymbols(ISymbol memberSymbol)
        {
            if (memberSymbol is INamedTypeSymbol)
            {
                return ImmutableArray<ISymbol>.Empty;
            }
            else
            {
                using var _ = ArrayBuilder<ISymbol>.GetInstance(out var builder);
                for (var overridenMember = memberSymbol.GetOverriddenMember();
                    overridenMember != null;
                    overridenMember = overridenMember.GetOverriddenMember())
                {
                    builder.Add(overridenMember.OriginalDefinition);
                }

                return builder.ToImmutableArray();
            }
        }

        /// <summary>
        /// Get the derived interfaces and derived classes for <param name="typeSymbol"/>.
        /// </summary>
        private static async Task<ImmutableArray<INamedTypeSymbol>> GetDerivedTypesAndImplementationsAsync(
            Solution solution,
            INamedTypeSymbol typeSymbol,
            CancellationToken cancellationToken)
        {
            if (typeSymbol.IsInterfaceType())
            {
                var allDerivedInterfaces = await SymbolFinder.FindDerivedInterfacesArrayAsync(
                    typeSymbol,
                    solution,
                    transitive: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var allImplementations = await SymbolFinder.FindImplementationsArrayAsync(
                    typeSymbol,
                    solution,
                    transitive: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                return allDerivedInterfaces.Concat(allImplementations);
            }
            else
            {
                return await SymbolFinder.FindDerivedClassesArrayAsync(
                    typeSymbol,
                    solution,
                    transitive: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
