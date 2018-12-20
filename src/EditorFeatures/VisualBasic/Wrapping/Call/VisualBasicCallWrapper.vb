﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.Editor.Wrapping.BinaryExpression
Imports Microsoft.CodeAnalysis.Editor.Wrapping.Call

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.Wrapping.Call
    Friend Class VisualBasicCallWrapper
        Inherits AbstractCallWrapper(Of
        ExpressionSyntax,
        NameSyntax,
        MemberAccessExpressionSyntax,
        InvocationExpressionSyntax,
        InvocationExpressionSyntax,
        ArgumentListSyntax)

        Public Sub New()
            MyBase.New(VisualBasicSyntaxFactsService.Instance)
        End Sub

        Public Overrides Function GetNewLineBeforeOperatorTrivia(newLine As SyntaxTriviaList) As SyntaxTriviaList
            Return newLine.InsertRange(0, {SyntaxFactory.WhitespaceTrivia(" "), SyntaxFactory.LineContinuationTrivia("_")})
        End Function
    End Class
End Namespace
