﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.SourceGeneration
{
    internal interface ISyntaxHelper
    {
        bool IsCaseSensitive { get; }

        bool IsValidIdentifier(string name);

        bool IsCompilationUnit(SyntaxNode node);
        bool IsAnyNamespaceBlock(SyntaxNode node);

        bool IsAttributeList(SyntaxNode node, [NotNullWhen(true)] out SyntaxNode? attributeTarget);
        SeparatedSyntaxList<SyntaxNode> GetAttributesOfAttributeList(SyntaxNode node);

        bool IsAttribute(SyntaxNode node);
        SyntaxNode GetNameOfAttribute(SyntaxNode node);

        SyntaxToken GetUnqualifiedIdentifierOfName(SyntaxNode node);

        /// <summary>
        /// <paramref name="node"/> must be a compilation unit or namespace block.
        /// </summary>
        void AddAliases(SyntaxNode node, ArrayBuilder<(string aliasName, string symbolName)> aliases, bool global);
        void AddAliases(CompilationOptions options, ArrayBuilder<(string aliasName, string symbolName)> aliases);
    }

    internal abstract class AbstractSyntaxHelper : ISyntaxHelper
    {
        public abstract bool IsCaseSensitive { get; }

        public abstract bool IsValidIdentifier(string name);

        public abstract SyntaxToken GetUnqualifiedIdentifierOfName(SyntaxNode name);

        public abstract bool IsCompilationUnit(SyntaxNode node);
        public abstract bool IsAnyNamespaceBlock(SyntaxNode node);

        public abstract bool IsAttribute(SyntaxNode node);
        public abstract SyntaxNode GetNameOfAttribute(SyntaxNode node);

        public abstract bool IsAttributeList(SyntaxNode node, [NotNullWhen(true)] out SyntaxNode? attributeTarget);
        public abstract SeparatedSyntaxList<SyntaxNode> GetAttributesOfAttributeList(SyntaxNode node);

        public abstract void AddAliases(SyntaxNode node, ArrayBuilder<(string aliasName, string symbolName)> aliases, bool global);
        public abstract void AddAliases(CompilationOptions options, ArrayBuilder<(string aliasName, string symbolName)> aliases);
    }
}
