' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics
    Partial Public Class IOperationTests
        Inherits SemanticModelTestBase

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub ILabelStatement_SimpleLabelTest()
            Dim source = <![CDATA[
Option Strict On
Imports System

Public Class C1
    Public Sub M1()'BIND:"Public Sub M1()"
        GoTo Label
Label:  Console.WriteLine("Hello World!")
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
IBlockOperation (5 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Public Sub  ... End Sub')
  IBranchOperation (BranchKind.GoTo, Label: Label) (OperationKind.Branch, IsStatement, Type: null) (Syntax: 'GoTo Label')
  ILabeledOperation (Label: Label) (OperationKind.Labeled, IsStatement, Type: null) (Syntax: 'Label:')
    Statement: 
      null
  IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'Console.Wri ... lo World!")')
    Expression: 
      IInvocationOperation (Sub System.Console.WriteLine(value As System.String)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'Console.Wri ... lo World!")')
        Instance Receiver: 
          null
        Arguments(1):
            IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '"Hello World!"')
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.String, Constant: "Hello World!") (Syntax: '"Hello World!"')
              InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
              OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
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
