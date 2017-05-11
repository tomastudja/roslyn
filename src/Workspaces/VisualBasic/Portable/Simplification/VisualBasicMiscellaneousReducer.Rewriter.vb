﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Simplification
    Partial Friend Class VisualBasicMiscellaneousReducer
        Private Class Rewriter
            Inherits AbstractExpressionRewriter

            Public Sub New(optionSet As OptionSet, cancellationToken As CancellationToken)
                MyBase.New(optionSet, cancellationToken)
            End Sub

            Public Overrides Function VisitInvocationExpression(node As InvocationExpressionSyntax) As SyntaxNode
                CancellationToken.ThrowIfCancellationRequested()

                Return SimplifyExpression(
                    node,
                    newNode:=MyBase.VisitInvocationExpression(node),
                    simplifier:=s_simplifyInvocationExpression)
            End Function

            Public Overrides Function VisitObjectCreationExpression(node As ObjectCreationExpressionSyntax) As SyntaxNode
                CancellationToken.ThrowIfCancellationRequested()

                Return SimplifyExpression(
                    node,
                    newNode:=MyBase.VisitObjectCreationExpression(node),
                    simplifier:=s_simplifyObjectCreationExpression)
            End Function

            Public Overrides Function VisitParameter(node As ParameterSyntax) As SyntaxNode
                CancellationToken.ThrowIfCancellationRequested()

                Return SimplifyNode(
                    node,
                    newNode:=MyBase.VisitParameter(node),
                    parentNode:=node.Parent,
                    simplifyFunc:=s_simplifyParameter)
            End Function
        End Class
    End Class
End Namespace