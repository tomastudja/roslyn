' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.Editor.Implementation.Outlining

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.Outlining
    Friend Class EventDeclarationOutliner
        Inherits AbstractSyntaxNodeOutliner(Of EventStatementSyntax)

        Protected Overrides Sub CollectOutliningSpans(eventDeclaration As EventStatementSyntax, spans As List(Of OutliningSpan), cancellationToken As CancellationToken)
            VisualBasicOutliningHelpers.CollectCommentsRegions(eventDeclaration, spans)

            Dim eventBlock = TryCast(eventDeclaration.Parent, EventBlockSyntax)
            If eventBlock IsNot Nothing AndAlso
               Not eventBlock.EndEventStatement.IsMissing Then
                spans.Add(VisualBasicOutliningHelpers.CreateRegionFromBlock(
                                eventBlock,
                                eventDeclaration.ConvertToSingleLine().ToString() & " " & Ellipsis,
                                autoCollapse:=True))

                VisualBasicOutliningHelpers.CollectCommentsRegions(eventBlock.EndEventStatement, spans)
            End If
        End Sub
    End Class
End Namespace
