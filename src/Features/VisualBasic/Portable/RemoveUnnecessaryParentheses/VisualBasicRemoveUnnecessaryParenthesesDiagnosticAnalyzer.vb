﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.RemoveUnnecessaryParentheses
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.RemoveUnnecessaryParentheses
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Friend Class VisualBasicRemoveUnnecessaryParenthesesDiagnosticAnalyzer
        Inherits AbstractRemoveUnnecessaryParenthesesDiagnosticAnalyzer(Of SyntaxKind, ParenthesizedExpressionSyntax)

        Protected Overrides Function GetSyntaxNodeKind() As SyntaxKind
            Return SyntaxKind.ParenthesizedExpression
        End Function

        Protected Overrides Function CanRemoveParentheses(
                parenthesizedExpression As ParenthesizedExpressionSyntax, semanticModel As SemanticModel,
                ByRef precedenceKind As PrecedenceKind, ByRef clarifiesPrecedence As Boolean) As Boolean

            Return CanRemoveParenthesesHelper(parenthesizedExpression, semanticModel, precedenceKind, clarifiesPrecedence)
        End Function

        Public Shared Function CanRemoveParenthesesHelper(
                parenthesizedExpression As ParenthesizedExpressionSyntax, semanticModel As SemanticModel,
                ByRef precedenceKind As PrecedenceKind, ByRef clarifiesPrecedence As Boolean) As Boolean
            Dim result = parenthesizedExpression.CanRemoveParentheses(semanticModel)
            If Not result Then
                precedenceKind = Nothing
                clarifiesPrecedence = False
                Return False
            End If

            Dim innerExpression = parenthesizedExpression.Expression
            Dim innerExpressionPrecedence = innerExpression.GetOperatorPrecedence()
            Dim innerExpressionIsSimple = innerExpressionPrecedence = OperatorPrecedence.PrecedenceNone

            Dim parentBinary = TryCast(parenthesizedExpression.Parent, BinaryExpressionSyntax)
            Dim parentAssignment = TryCast(parenthesizedExpression.Parent, AssignmentStatementSyntax)

            If parentBinary IsNot Nothing Then
                precedenceKind = GetPrecedenceKind(parentBinary)
                clarifiesPrecedence = Not innerExpressionIsSimple AndAlso
                                      parentBinary.GetOperatorPrecedence() <> innerExpressionPrecedence
                Return True
            ElseIf parentAssignment IsNot Nothing Then
                ' if we have:  a = (b.length)  this can be removed, and precedence is not clarified. however:
                ' if we have:  a *= (b + c)    this can be removed, but the parens were clarifying precedence
                precedenceKind = PrecedenceKind.Assignment
                clarifiesPrecedence = Not innerExpressionIsSimple
                Return True
            End If

            precedenceKind = PrecedenceKind.Other
            clarifiesPrecedence = False
            Return True
        End Function

        Public Shared Function GetPrecedenceKind(binaryLike As SyntaxNode) As PrecedenceKind
            Dim binary = TryCast(binaryLike, BinaryExpressionSyntax)
            If binary Is Nothing Then
                Debug.Assert(TypeOf binaryLike Is AssignmentStatementSyntax)
                Return PrecedenceKind.Assignment
            End If

            Dim precedence = binary.GetOperatorPrecedence()
            Select Case precedence
                Case OperatorPrecedence.PrecedenceXor,
                     OperatorPrecedence.PrecedenceOr,
                     OperatorPrecedence.PrecedenceAnd
                    Return PrecedenceKind.Logical

                Case OperatorPrecedence.PrecedenceRelational
                    Return PrecedenceKind.Relational

                Case OperatorPrecedence.PrecedenceShift
                    Return PrecedenceKind.Shift

                Case OperatorPrecedence.PrecedenceConcatenate,
                     OperatorPrecedence.PrecedenceAdd,
                     OperatorPrecedence.PrecedenceModulus,
                     OperatorPrecedence.PrecedenceIntegerDivide,
                     OperatorPrecedence.PrecedenceMultiply,
                     OperatorPrecedence.PrecedenceExponentiate
                    Return PrecedenceKind.Arithmetic
            End Select

            Throw ExceptionUtilities.UnexpectedValue(precedence)
        End Function
    End Class
End Namespace
