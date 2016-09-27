// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Structure;

namespace Microsoft.CodeAnalysis.CSharp.Structure
{
    internal class EnumDeclarationStructureProvider : AbstractSyntaxNodeStructureProvider<EnumDeclarationSyntax>
    {
        protected override void CollectBlockSpans(
            EnumDeclarationSyntax enumDeclaration,
            ArrayBuilder<BlockSpan> spans,
            CancellationToken cancellationToken)
        {
            CSharpStructureHelpers.CollectCommentBlockSpans(enumDeclaration, spans);

            if (!enumDeclaration.OpenBraceToken.IsMissing &&
                !enumDeclaration.CloseBraceToken.IsMissing)
            {
                spans.Add(CSharpStructureHelpers.CreateBlockSpan(
                    enumDeclaration,
                    enumDeclaration.Identifier,
                    autoCollapse: false,
                    type: BlockTypes.Enum,
                    isCollapsible: true));
            }

            // add any leading comments before the end of the type block
            if (!enumDeclaration.CloseBraceToken.IsMissing)
            {
                var leadingTrivia = enumDeclaration.CloseBraceToken.LeadingTrivia;
                CSharpStructureHelpers.CollectCommentBlockSpans(leadingTrivia, spans);
            }
        }
    }
}