﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.Emit;

namespace Microsoft.CodeAnalysis.EditAndContinue
{
    internal readonly struct SemanticEditInfo
    {
        /// <summary>
        /// <see cref="SemanticEditKind.Insert"/> or <see cref="SemanticEditKind.Update"/>.
        /// </summary>
        public SemanticEditKind Kind { get; }

        /// <summary>
        /// If <see cref="Kind"/> is <see cref="SemanticEditKind.Insert"/> represents the inserted symbol in the new compilation.
        /// If <see cref="Kind"/> is <see cref="SemanticEditKind.Update"/> represents the updated symbol in both compilations.
        /// 
        /// We use <see cref="SymbolKey"/> to represent the symbol rather then <see cref="ISymbol"/>,
        /// since different semantic edits might hav ebeen calculated against different solution snapshot and thus symbols are not directly comparable.
        /// When the edits are processed we map the <see cref="SymbolKey"/> to the current compilation.
        /// </summary>
        public SymbolKey Symbol { get; }

        public Func<SyntaxNode, SyntaxNode?>? SyntaxMap { get; }

        /// <summary>
        /// Specified if the edit needs to be merged with other edits of the same <see cref="PartialType"/>.
        /// <see langword="null"/> for edits of non-partial types or their members and of a member of a partial type that do not require merging.
        /// 
        /// If specified, the <see cref="SyntaxMap"/> is incomplete: it only provides mapping of the changed members of a single partial type declaration.
        /// </summary>
        public SymbolKey? PartialType { get; }

        public SemanticEditInfo(
            SemanticEditKind kind,
            SymbolKey symbol,
            Func<SyntaxNode, SyntaxNode?>? syntaxMap,
            SymbolKey? partialType)
        {
            Kind = kind;
            Symbol = symbol;
            SyntaxMap = syntaxMap;
            PartialType = partialType;
        }
    }
}
