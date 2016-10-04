﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Structure

    Friend Class DoLoopBlockStructureProvider
        Inherits AbstractSyntaxNodeStructureProvider(Of DoLoopBlockSyntax)

        Protected Overrides Sub CollectBlockSpans(node As DoLoopBlockSyntax,
                                                  spans As ArrayBuilder(Of BlockSpan),
                                                  cancellationToken As CancellationToken)
            spans.AddIfNotNull(CreateRegionFromBlock(
                               node, node.LoopStatement, autoCollapse:=False,
                               type:=BlockTypes.Loop, isCollapsible:=True))
        End Sub
    End Class

End Namespace