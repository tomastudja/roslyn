﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class ClassDeclarationSyntax
    {
        public ClassDeclarationSyntax Update(SyntaxList<AttributeListSyntax> attributeLists, SyntaxTokenList modifiers, SyntaxToken keyword, SyntaxToken identifier, TypeParameterListSyntax? typeParameterList, BaseListSyntax? baseList, SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses, SyntaxToken openBraceToken, SyntaxList<MemberDeclarationSyntax> members, SyntaxToken closeBraceToken, SyntaxToken semicolonToken)
        {
            return Update(attributeLists, modifiers, keyword, identifier, typeParameterList, ParameterList, baseList, constraintClauses, openBraceToken, members, closeBraceToken, semicolonToken);
        }

        protected override ParameterListSyntax? ParameterListCore => ParameterList;
    }
}

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    partial class ClassDeclarationSyntax
    {
        protected override ParameterListSyntax? ParameterListCore => ParameterList;
    }
}
