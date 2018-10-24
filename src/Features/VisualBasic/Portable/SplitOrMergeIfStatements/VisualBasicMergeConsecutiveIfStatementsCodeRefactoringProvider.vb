﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.SplitOrMergeIfStatements
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.SplitOrMergeIfStatements
    <ExportCodeRefactoringProvider(LanguageNames.VisualBasic, Name:=PredefinedCodeRefactoringProviderNames.MergeConsecutiveIfStatements), [Shared]>
    Friend NotInheritable Class VisualBasicMergeConsecutiveIfStatementsCodeRefactoringProvider
        Inherits AbstractMergeConsecutiveIfStatementsCodeRefactoringProvider(Of ExpressionSyntax)

        Protected Overrides Function IsApplicableSpan(node As SyntaxNode, span As TextSpan, ByRef ifStatementNode As SyntaxNode) As Boolean
            If TypeOf node Is IfStatementSyntax AndAlso TypeOf node.Parent Is MultiLineIfBlockSyntax Then
                Dim ifStatement = DirectCast(node, IfStatementSyntax)
                ' Cases:
                ' 1. Position is at a direct token child of an if statement with no selection (e.g. 'If' keyword, 'Then' keyword)
                ' 2. Selection around the 'If' keyword
                ' 3. Selection around the if statement - from 'If' keyword to 'Then' keyword
                If span.Length = 0 OrElse
                   span.IsAround(ifStatement.IfKeyword) OrElse
                   span.IsAround(ifStatement) Then
                    ifStatementNode = node.Parent
                    Return True
                End If
            End If

            If TypeOf node Is MultiLineIfBlockSyntax Then
                ' 4. Selection around the if block.
                If span.IsAround(node) Then
                    ifStatementNode = node
                    Return True
                End If
            End If

            If TypeOf node Is ElseIfStatementSyntax AndAlso TypeOf node.Parent Is ElseIfBlockSyntax Then
                Dim elseIfStatement = DirectCast(node, ElseIfStatementSyntax)
                ' 5. Position is at a direct token child of an else if statement with no selection (e.g. 'ElseIf' keyword, 'Then' keyword)
                ' 6. Selection around the 'ElseIf' keyword
                ' 7. Selection around the else if statement - from 'ElseIf' keyword to 'Then' keyword
                If span.Length = 0 OrElse
                   span.IsAround(elseIfStatement.ElseIfKeyword) OrElse
                   span.IsAround(elseIfStatement) Then
                    ifStatementNode = node.Parent
                    Return True
                End If
            End If

            If TypeOf node Is ElseIfBlockSyntax Then
                ' 8. Selection around the else if block.
                If span.IsAround(node) Then
                    ifStatementNode = node
                    Return True
                End If
            End If

            ifStatementNode = Nothing
            Return False
        End Function
    End Class
End Namespace
