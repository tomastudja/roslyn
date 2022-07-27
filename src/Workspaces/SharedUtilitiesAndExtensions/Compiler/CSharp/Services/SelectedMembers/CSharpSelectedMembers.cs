﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.LanguageServices;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.LanguageServices
{
    internal class CSharpSelectedMembers : AbstractSelectedMembers<
        MemberDeclarationSyntax,
        FieldDeclarationSyntax,
        PropertyDeclarationSyntax,
        TypeDeclarationSyntax,
        VariableDeclaratorSyntax>
    {
        public static readonly CSharpSelectedMembers Instance = new();

        private CSharpSelectedMembers()
        {
        }

        protected override ImmutableArray<(SyntaxNode declarator, SyntaxToken identifier)> GetDeclaratorsAndIdentifiers(MemberDeclarationSyntax member)
        {
            return member switch
            {
                FieldDeclarationSyntax fieldDeclaration => fieldDeclaration.Declaration.Variables.SelectAsArray(
                    v => (declaration: (SyntaxNode)v, identifier: v.Identifier)),
                EventFieldDeclarationSyntax eventFieldDeclaration => eventFieldDeclaration.Declaration.Variables.SelectAsArray(
                    v => (declaration: (SyntaxNode)v, identifier: v.Identifier)),
                _ => ImmutableArray.Create((declaration: (SyntaxNode)member, identifier: member.GetNameToken())),
            };
        }

        protected override SyntaxList<MemberDeclarationSyntax> GetMembers(TypeDeclarationSyntax containingType)
            => containingType.Members;
    }
}
