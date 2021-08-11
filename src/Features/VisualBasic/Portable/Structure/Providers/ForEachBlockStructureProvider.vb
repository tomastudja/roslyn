﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.[Shared].Collections
Imports Microsoft.CodeAnalysis.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Structure
    Friend Class ForEachBlockStructureProvider
        Inherits AbstractSyntaxNodeStructureProvider(Of ForEachBlockSyntax)

        Protected Overrides Sub CollectBlockSpans(previousToken As SyntaxToken,
                                                  node As ForEachBlockSyntax,
                                                  ByRef spans As TemporaryArray(Of BlockSpan),
                                                  optionProvider As BlockStructureOptionProvider,
                                                  cancellationToken As CancellationToken)
            spans.AddIfNotNull(CreateBlockSpanFromBlock(
                               node, node.ForOrForEachStatement, autoCollapse:=False,
                               type:=BlockTypes.Loop, isCollapsible:=True))
        End Sub
    End Class
End Namespace
