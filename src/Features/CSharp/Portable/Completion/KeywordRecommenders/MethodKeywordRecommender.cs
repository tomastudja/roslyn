﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Completion.KeywordRecommenders
{
    internal class MethodKeywordRecommender : AbstractSyntacticSingleKeywordRecommender
    {
        public MethodKeywordRecommender()
            : base(SyntaxKind.MethodKeyword)
        {
        }

        protected override bool IsValidContext(int position, CSharpSyntaxContext context, CancellationToken cancellationToken)
        {
            if (context.IsMemberAttributeContext(SyntaxKindSet.ClassInterfaceStructRecordTypeDeclarations, cancellationToken))
            {
                return true;
            }

            var token = context.TargetToken;

            if (token.Kind() == SyntaxKind.OpenBracketToken &&
                token.Parent.IsKind(SyntaxKind.AttributeList) &&
                token.Parent.FirstAncestorOrSelf<SyntaxNode>(n => n is
                    PropertyDeclarationSyntax or
                    EventDeclarationSyntax or
                    TypeDeclarationSyntax(kind: SyntaxKind.ClassDeclaration or SyntaxKind.StructDeclaration) { ParameterList: not null }) is not null)
            {
                return true;
            }

            return false;
        }
    }
}
