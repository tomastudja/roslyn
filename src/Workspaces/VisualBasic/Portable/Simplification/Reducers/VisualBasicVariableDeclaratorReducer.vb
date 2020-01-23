﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Simplification
    Partial Friend Class VisualBasicVariableDeclaratorReducer
        Inherits AbstractVisualBasicReducer

        Private Shared ReadOnly s_pool As ObjectPool(Of IReductionRewriter) =
            New ObjectPool(Of IReductionRewriter)(Function() New Rewriter(s_pool))

        Public Sub New()
            MyBase.New(s_pool)
        End Sub

        Private Shared ReadOnly s_simplifyVariableDeclarator As Func(Of VariableDeclaratorSyntax, SemanticModel, OptionSet, CancellationToken, SyntaxNode) = AddressOf SimplifyVariableDeclarator

        Private Overloads Shared Function SimplifyVariableDeclarator(
            node As VariableDeclaratorSyntax,
            semanticModel As SemanticModel,
            optionSet As OptionSet,
            cancellationToken As CancellationToken
        ) As SyntaxNode
            Dim replacementNode As SyntaxNode = Nothing
            Dim issueSpan As TextSpan

            If Not node.TryReduceVariableDeclaratorWithoutType(semanticModel, replacementNode, issueSpan) Then
                Return node
            End If

            replacementNode = node.CopyAnnotationsTo(replacementNode).WithAdditionalAnnotations(Formatter.Annotation)
            Return replacementNode.WithoutAnnotations(Simplifier.Annotation)
        End Function
    End Class
End Namespace
