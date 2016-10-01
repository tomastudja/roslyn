﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Structure;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Structure
{
    internal class SwitchStatementStructureProvider : AbstractSyntaxNodeStructureProvider<SwitchStatementSyntax>
    {
        protected override void CollectBlockSpans(
            SwitchStatementSyntax node,
            ArrayBuilder<BlockSpan> spans,
            CancellationToken cancellationToken)
        {
            spans.Add(new BlockSpan(isCollapsible: false,
                textSpan: TextSpan.FromBounds(node.CloseParenToken.Span.End, node.CloseBraceToken.Span.End),
                hintSpan: node.Span,
                type: BlockTypes.Switch));
        }
    }
}