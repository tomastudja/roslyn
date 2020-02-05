﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.GenerateEqualsAndGetHashCodeFromMembers
{
    /// <summary>
    /// Service that can be used to generate <see cref="object.Equals(object)"/> and
    /// <see cref="object.GetHashCode"/> overloads for use from other IDE features.
    /// </summary>
    internal interface IGenerateEqualsAndGetHashCodeService : ILanguageService
    {
        /// <summary>
        /// Formats only the members in the provided document that were generated by this interface.
        /// </summary>
        Task<Document> FormatDocumentAsync(Document document, CancellationToken cancellationToken);

        /// <summary>
        /// Generates an override of <see cref="object.Equals(object)"/> that works by comparing the
        /// provided <paramref name="members"/>.
        /// </summary>
        Task<IMethodSymbol> GenerateEqualsMethodAsync(Document document, INamedTypeSymbol namedType, ImmutableArray<ISymbol> members, string localNameOpt, CancellationToken cancellationToken);

        /// <summary>
        /// Generates an override of <see cref="object.Equals(object)"/> that works by delegating to
        /// <see cref="IEquatable{T}.Equals(T)"/>.
        /// </summary>
        Task<IMethodSymbol> GenerateEqualsMethodThroughIEquatableEqualsAsync(Document document, INamedTypeSymbol namedType, CancellationToken cancellationToken);

        /// <summary>
        /// Generates an implementation of <see cref="IEquatable{T}.Equals"/> that works by
        /// comparing the provided <paramref name="members"/>.
        /// </summary>
        Task<IMethodSymbol> GenerateIEquatableEqualsMethodAsync(Document document, INamedTypeSymbol namedType, ImmutableArray<ISymbol> members, INamedTypeSymbol constructedEquatableType, CancellationToken cancellationToken);

        /// <summary>
        /// Generates an override of <see cref="object.GetHashCode"/> that computes a reasonable
        /// hash based on the provided <paramref name="members"/>.  The generated function will
        /// defer to HashCode.Combine if it exists.  Otherwise, it will determine if it should
        /// generate code directly in-line to compute the hash, or defer to something like
        /// <see cref="ValueTuple.GetHashCode"/> to provide a reasonable alternative.
        /// </summary>
        Task<IMethodSymbol> GenerateGetHashCodeMethodAsync(Document document, INamedTypeSymbol namedType, ImmutableArray<ISymbol> members, CancellationToken cancellationToken);
    }
}
