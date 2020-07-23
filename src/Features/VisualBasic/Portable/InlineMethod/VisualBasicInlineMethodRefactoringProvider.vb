﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Composition
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.InlineMethod
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.InlineMethod
    <ExportCodeRefactoringProvider(LanguageNames.VisualBasic, Name:=PredefinedCodeRefactoringProviderNames.InlineMethod), [Shared]>
    <Export(GetType(VisualBasicInlineMethodRefactoringProvider))>
    Friend NotInheritable Class VisualBasicInlineMethodRefactoringProvider
        Inherits AbstractInlineMethodRefactoringProvider

        <ImportingConstructor>
        <Obsolete(MefConstruction.ImportingConstructorMessage, True)>
        Public Sub New()
        End Sub

        Protected Overrides Async Function GetInvocationExpressionSyntaxNodeAsync(context As CodeRefactoringContext) As Task(Of SyntaxNode)
            Dim syntaxNode = Await context.TryGetRelevantNodeAsync(Of InvocationExpressionSyntax).ConfigureAwait(False)
            Return syntaxNode
        End Function

        Protected Overrides Function IsMethodContainsOneStatement(calleeMethodDeclarationSyntaxNode As SyntaxNode) As Boolean
            Dim methodStatementSyntaxNode = TryCast(calleeMethodDeclarationSyntaxNode, MethodStatementSyntax)
            If methodStatementSyntaxNode IsNot Nothing Then
                Dim methodBlock = TryCast(methodStatementSyntaxNode.Parent, MethodBlockSyntax)
                If methodBlock IsNot Nothing Then
                    Dim statements = methodBlock.Statements
                    If statements.Count = 1 Then
                        Dim singleStatement = statements(0)

                        Return statements.Count = 1 _
                            AndAlso (TypeOf singleStatement Is ReturnStatementSyntax _
                                OrElse TypeOf singleStatement Is ExpressionStatementSyntax _
                                OrElse TypeOf singleStatement Is ThrowStatementSyntax)
                    End If
                End If
            End If

            Return Nothing
        End Function

        Protected Overrides Function ExtractExpressionFromMethodDeclaration(calleeMethodDeclarationSyntaxNode As SyntaxNode) As SyntaxNode
            Dim methodStatementSyntaxNode = TryCast(calleeMethodDeclarationSyntaxNode, MethodStatementSyntax)
            Dim inlineSyntaxNode As SyntaxNode = Nothing
            If methodStatementSyntaxNode IsNot Nothing Then
                Dim methodBlock = TryCast(methodStatementSyntaxNode.Parent, MethodBlockSyntax)
                If methodBlock IsNot Nothing Then
                    Dim statements = methodBlock.Statements
                    If statements.Count = 1 Then
                        Dim singleStatement = statements(0)
                        Dim returnStatement = TryCast(singleStatement, ReturnStatementSyntax)
                        If returnStatement IsNot Nothing Then
                            inlineSyntaxNode = returnStatement.Expression
                        End If

                        Dim expressionStatement = TryCast(singleStatement, ExpressionStatementSyntax)
                        If expressionStatement IsNot Nothing Then
                            inlineSyntaxNode = expressionStatement.Expression
                        End If

                        Dim throwStatement = TryCast(singleStatement, ThrowStatementSyntax)
                        If throwStatement IsNot Nothing Then
                            inlineSyntaxNode = throwStatement.Expression
                        End If
                    End If
                End If
            End If

            If inlineSyntaxNode Is Nothing Then
                inlineSyntaxNode = SyntaxFactory.EmptyStatement()
            End If

            Return inlineSyntaxNode
        End Function

        Protected Overrides Function ReplaceParametersAndGenericsInMethodDeclaration(methodDeclarationSyntaxNode As SyntaxNode, methodInvocationSyntaxNode As SyntaxNode, methodSymbol As IMethodSymbol, semanticModel As SemanticModel) As SyntaxNode
            Return methodDeclarationSyntaxNode
        End Function

        Protected Overrides Function TryGetVariableDeclarationsForOutParameters(methodInvovation As SyntaxNode, semanticModel As SemanticModel, ByRef variableDeclarations As ImmutableArray(Of SyntaxNode)) As Boolean
            variableDeclarations = Nothing
            Return False
        End Function
    End Class
End Namespace
