﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Syntax
{
    public partial class MethodDeclarationSyntax
    {
        public int Arity
        {
            get
            {
                return this.TypeParameterList == null ? 0 : this.TypeParameterList.Parameters.Count;
            }
        }
    }
}

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class SyntaxFactory
    {
        public static MethodDeclarationSyntax MethodDeclaration(
            SyntaxList<AttributeListSyntax> attributeLists,
            SyntaxTokenList modifiers,
            TypeSyntax returnType,
            ExplicitInterfaceSpecifierSyntax explicitInterfaceSpecifier,
            SyntaxToken identifier,
            TypeParameterListSyntax typeParameterList,
            ParameterListSyntax parameterList,
            SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses,
            BlockSyntax body,
            SyntaxToken semicolonToken)
        {
            return SyntaxFactory.MethodDeclaration(
                attributeLists,
                modifiers,
                returnType,
                explicitInterfaceSpecifier,
                identifier,
                typeParameterList,
                parameterList,
                constraintClauses,
                body,
                null,
                semicolonToken);
        }
    }
}
