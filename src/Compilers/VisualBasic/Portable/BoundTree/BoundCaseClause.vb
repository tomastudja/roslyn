﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class BoundCaseClause

#If DEBUG Then
        Protected Shared Sub ValidateValueAndCondition(valueOpt As BoundExpression, conditionOpt As BoundExpression, operatorKind As BinaryOperatorKind)
            Debug.Assert((valueOpt Is Nothing) Xor (conditionOpt Is Nothing))

            If conditionOpt IsNot Nothing Then
                Select Case conditionOpt.Kind
                    Case BoundKind.BinaryOperator
                        Dim binaryOp As BoundBinaryOperator = DirectCast(conditionOpt, BoundBinaryOperator)
                        Debug.Assert((binaryOp.OperatorKind And BinaryOperatorKind.OpMask) = operatorKind)

                    Case BoundKind.UserDefinedBinaryOperator
                        Dim binaryOp As BoundUserDefinedBinaryOperator = DirectCast(conditionOpt, BoundUserDefinedBinaryOperator)
                        Debug.Assert((binaryOp.OperatorKind And BinaryOperatorKind.OpMask) = operatorKind)

                    Case Else
                        ExceptionUtilities.UnexpectedValue(conditionOpt.Kind) ' This is going to assert
                End Select
            End If

        End Sub
#End If

    End Class
End Namespace
