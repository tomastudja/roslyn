﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports Microsoft.CodeAnalysis.CodeCleanup
Imports Microsoft.CodeAnalysis.CodeCleanup.Providers
Imports Microsoft.CodeAnalysis.Text

Namespace Microsoft.CodeAnalysis.VisualBasic.CodeCleanup
    Partial Friend Class VisualBasicCodeCleanerService
        Inherits AbstractCodeCleanerService

        Private Shared ReadOnly s_defaultProviders As ImmutableArray(Of ICodeCleanupProvider) = ImmutableArray.Create(Of ICodeCleanupProvider)(
            New AddMissingTokensCodeCleanupProvider(),
            New FixIncorrectTokensCodeCleanupProvider(),
            New ReduceTokensCodeCleanupProvider(),
            New NormalizeModifiersOrOperatorsCodeCleanupProvider(),
            New RemoveUnnecessaryLineContinuationCodeCleanupProvider(),
            New CaseCorrectionCodeCleanupProvider(),
            New SimplificationCodeCleanupProvider(),
            New FormatCodeCleanupProvider())

        Public Overrides Function GetDefaultProviders() As ImmutableArray(Of ICodeCleanupProvider)
            Return s_defaultProviders
        End Function

        Protected Overrides Function GetSpansToAvoid(root As SyntaxNode) As ImmutableArray(Of TextSpan)
            ' We don't want to touch nodes in the document that have syntax errors on them and which
            ' contain String literals.  It's quite possible that there is some string literal on a 
            ' previous line that was intended to be terminated on that line, but which wasn't.  The
            ' string may then have terminated on this line (because of the start of another literal)
            ' causing the literal contents to then be considered code.  We don't want to cleanup
            ' 'code' that the user intends to be the content of a string literal.

            Dim result = ArrayBuilder(Of TextSpan).GetInstance()

            ProcessNode(root, root, result)

            Return result.ToImmutableAndFree()
        End Function

        Private Sub ProcessNode(root As SyntaxNode, node As SyntaxNode, result As ArrayBuilder(Of TextSpan))
            If Not node.ContainsDiagnostics Then
                Return
            End If

            For Each child In node.ChildNodesAndTokens()
                If child.IsNode Then
                    ProcessNode(root, child.AsNode(), result)
                Else
                    ProcessToken(root, child.AsToken(), result)
                End If
            Next
        End Sub

        Private Sub ProcessToken(root As SyntaxNode, token As SyntaxToken, result As ArrayBuilder(Of TextSpan))
            If token.ContainsDiagnostics Then
                Dim parentMultiLineNode = If(GetMultiLineContainer(token.Parent), root)

                If ContainsStringLiteral(parentMultiLineNode) Then
                    result.Add(parentMultiLineNode.FullSpan)
                End If
            End If
        End Sub

        Private Function ContainsStringLiteral(node As SyntaxNode) As Boolean
            Return node.DescendantTokens().Any(Function(t) t.Kind() = SyntaxKind.StringLiteralToken)
        End Function

        Private Function GetMultiLineContainer(node As SyntaxNode) As SyntaxNode
            If node Is Nothing Then
                Return Nothing
            End If

            If Not VisualBasicSyntaxFactsService.Instance.IsOnSingleLine(node, fullSpan:=False) Then
                Return node
            End If

            Return GetMultiLineContainer(node.Parent)
        End Function
    End Class
End Namespace