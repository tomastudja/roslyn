﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Represents a chain of symbols that are imported to a particular position in a source file.  Symbols may be
    /// imported, but may not necessarily be available at that location (for example, an alias symbol hidden by another
    /// symbol).
    /// </summary>
    public interface IImportChain
    {
        /// <summary>
        /// Next item in the chain.  This generally represents the next scope in a file, or compilation that pulls in
        /// imported symbols.
        /// </summary>
        IImportChain? Parent { get; }

        /// <summary>
        /// Aliases defined at this level of the chain.  This corresponds to <c>using X = TypeOrNamespace;</c> in C# or
        /// <c>Imports X = TypeOrNamespace</c> in Visual Basic.
        /// </summary>
        ImmutableArray<IAliasSymbol> Aliases { get; }

        /// <summary>
        /// Types or namespaces imported at the list of the chain.  This corresponse to <c>using Namespace;</c> or
        /// <c>using static Type;</c> in C#, or <c>Imports TypeOrNamespace</c> in Visual Basic.
        /// </summary>
        ImmutableArray<INamespaceOrTypeSymbol> Imports { get; }
    }
}
