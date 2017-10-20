' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics

    Partial Public Class IOperationTests
        Inherits SemanticModelTestBase

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub WithStatement_Basic()
            Dim source = <![CDATA[
Class C
    Public I, J As Integer
End Class

Class D
    Private Sub M(c As C)
        With c'BIND:"With c"
            .I = 0
            .J = 0
        End With

    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWithOperation (OperationKind.None, IsStatement, Type: null) (Syntax: 'With c'BIND ... End With')
  Value: 
    IParameterReferenceOperation: c (OperationKind.ParameterReference, IsExpression, Type: C) (Syntax: 'c')
  Body: 
    IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'With c'BIND ... End With')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: '.I = 0')
        Expression: 
          ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: '.I = 0')
            Left: 
              IFieldReferenceOperation: C.I As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: '.I')
                Instance Receiver: 
                  IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'c')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: '.J = 0')
        Expression: 
          ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: '.J = 0')
            Left: 
              IFieldReferenceOperation: C.J As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: '.J')
                Instance Receiver: 
                  IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'c')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
]]>.Value

            Dim expectedDiagnostics = String.Empty

            VerifyOperationTreeAndDiagnosticsForTest(Of WithBlockSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub WithStatement_Parent()
            Dim source = <![CDATA[
Class C
    Public I, J As Integer
End Class

Class D
    Private Sub M(c As C)'BIND:"Private Sub M(c As C)"
        With c
            .I = 0
            .J = 0
        End With

    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
IBlockOperation (3 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Private Sub ... End Sub')
  IWithOperation (OperationKind.None, IsStatement, Type: null) (Syntax: 'With c ... End With')
    Value: 
      IParameterReferenceOperation: c (OperationKind.ParameterReference, IsExpression, Type: C) (Syntax: 'c')
    Body: 
      IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'With c ... End With')
        IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: '.I = 0')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: '.I = 0')
              Left: 
                IFieldReferenceOperation: C.I As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: '.I')
                  Instance Receiver: 
                    IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'c')
              Right: 
                ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
        IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: '.J = 0')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: '.J = 0')
              Left: 
                IFieldReferenceOperation: C.J As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: '.J')
                  Instance Receiver: 
                    IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'c')
              Right: 
                ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  ILabeledOperation (Label: exit) (OperationKind.Labeled, IsStatement, Type: null) (Syntax: 'End Sub')
    Statement: 
      null
  IReturnOperation (OperationKind.Return, IsStatement, Type: null) (Syntax: 'End Sub')
    ReturnedValue: 
      null
]]>.Value

            Dim expectedDiagnostics = String.Empty

            VerifyOperationTreeAndDiagnosticsForTest(Of MethodBlockSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub
    End Class
End Namespace
