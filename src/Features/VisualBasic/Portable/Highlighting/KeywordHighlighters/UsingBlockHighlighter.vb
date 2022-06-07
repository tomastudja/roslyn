﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Composition
Imports System.Threading
Imports Microsoft.CodeAnalysis.Highlighting
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.KeywordHighlighting
    <ExportHighlighter(LanguageNames.VisualBasic), [Shared]>
    Friend Class UsingBlockHighlighter
        Inherits AbstractKeywordHighlighter(Of SyntaxNode)

        <ImportingConstructor>
        <Obsolete(MefConstruction.ImportingConstructorMessage, True)>
        Public Sub New()
        End Sub

        Protected Overloads Overrides Sub AddHighlights(node As SyntaxNode, highlights As List(Of TextSpan), cancellationToken As CancellationToken)
            Dim usingBlock = node.GetAncestor(Of UsingBlockSyntax)()
            If usingBlock Is Nothing Then
                Return
            End If

            With usingBlock
                highlights.Add(.UsingStatement.UsingKeyword.Span)
                highlights.Add(.EndUsingStatement.Span)
            End With
        End Sub
    End Class
End Namespace
