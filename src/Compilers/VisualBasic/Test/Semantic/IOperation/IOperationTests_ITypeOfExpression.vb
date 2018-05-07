﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics

    Partial Public Class IOperationTests
        Inherits SemanticModelTestBase

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub TestGetType()
            Dim source = <![CDATA[
Imports System
Class C
    Sub M(t As Type)
        t = GetType(Integer)'BIND:"GetType(Integer)"
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
ITypeOfOperation (OperationKind.TypeOf, Type: System.Type) (Syntax: 'GetType(Integer)')
  TypeOperand: System.Int32
]]>.Value

            Dim expectedDiagnostics = String.Empty

            VerifyOperationTreeAndDiagnosticsForTest(Of GetTypeExpressionSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub TestGetType_NonPrimitiveTypeArgument()
            Dim source = <![CDATA[
Imports System
Class C
    Sub M(t As Type)
        t = GetType(C)'BIND:"GetType(C)"
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
ITypeOfOperation (OperationKind.TypeOf, Type: System.Type) (Syntax: 'GetType(C)')
  TypeOperand: C
]]>.Value

            Dim expectedDiagnostics = String.Empty

            VerifyOperationTreeAndDiagnosticsForTest(Of GetTypeExpressionSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub TestGetType_ErrorTypeArgument()
            Dim source = <![CDATA[
Imports System
Class C
    Sub M(t As Type)
        t = GetType(UndefinedType)'BIND:"GetType(UndefinedType)"
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
ITypeOfOperation (OperationKind.TypeOf, Type: System.Type, IsInvalid) (Syntax: 'GetType(UndefinedType)')
  TypeOperand: UndefinedType
]]>.Value

            Dim expectedDiagnostics = <![CDATA[
BC30002: Type 'UndefinedType' is not defined.
        t = GetType(UndefinedType)'BIND:"GetType(UndefinedType)"
                    ~~~~~~~~~~~~~
]]>.Value

            VerifyOperationTreeAndDiagnosticsForTest(Of GetTypeExpressionSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub TestGetType_IdentifierArgument()
            Dim source = <![CDATA[
Imports System
Class C
    Sub M(t As Type)
        t = GetType(t)'BIND:"GetType(t)"
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
ITypeOfOperation (OperationKind.TypeOf, Type: System.Type, IsInvalid) (Syntax: 'GetType(t)')
  TypeOperand: t
]]>.Value

            Dim expectedDiagnostics = <![CDATA[
BC30002: Type 't' is not defined.
        t = GetType(t)'BIND:"GetType(t)"
                    ~
]]>.Value

            VerifyOperationTreeAndDiagnosticsForTest(Of GetTypeExpressionSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub TestGetType_ExpressionArgument()
            Dim source = <![CDATA[
Imports System
Class C
    Sub M(t As Type)
        t = GetType(M2())'BIND:"GetType(M2())"
    End Sub

    Function M2() As Type
        Return Nothing
    End Function
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
ITypeOfOperation (OperationKind.TypeOf, Type: System.Type, IsInvalid) (Syntax: 'GetType(M2())')
  TypeOperand: M2()
]]>.Value

            Dim expectedDiagnostics = <![CDATA[
BC30002: Type 'M2' is not defined.
        t = GetType(M2())'BIND:"GetType(M2())"
                    ~~
]]>.Value

            VerifyOperationTreeAndDiagnosticsForTest(Of GetTypeExpressionSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact>
        Public Sub TestGetType_MissingArgument()
            Dim source = <![CDATA[
Imports System
Class C
    Sub M(t As Type)
        t = GetType()'BIND:"GetType()"
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
ITypeOfOperation (OperationKind.TypeOf, Type: System.Type, IsInvalid) (Syntax: 'GetType()')
  TypeOperand: ?
]]>.Value

            Dim expectedDiagnostics = <![CDATA[
BC30182: Type expected.
        t = GetType()'BIND:"GetType()"
                    ~
]]>.Value

            VerifyOperationTreeAndDiagnosticsForTest(Of GetTypeExpressionSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation, CompilerFeature.Dataflow)>
        <Fact>
        Public Sub TypeOfFlow_01()
            Dim source = <![CDATA[
Imports System
Class C
    Public Sub M(t As Type)'BIND:"Public Sub M(t As Type)"
        t = GetType(Boolean)
    End Sub
End Class]]>.Value

            Dim expectedDiagnostics = String.Empty

            Dim expectedFlowGraph = <![CDATA[
Block[B0] - Entry
    Statements (0)
    Next (Regular) Block[B1]
Block[B1] - Block
    Predecessors: [B0]
    Statements (1)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 't = GetType(Boolean)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Type, IsImplicit) (Syntax: 't = GetType(Boolean)')
              Left: 
                IParameterReferenceOperation: t (OperationKind.ParameterReference, Type: System.Type) (Syntax: 't')
              Right: 
                ITypeOfOperation (OperationKind.TypeOf, Type: System.Type) (Syntax: 'GetType(Boolean)')
                  TypeOperand: System.Boolean

    Next (Regular) Block[B2]
Block[B2] - Exit
    Predecessors: [B1]
    Statements (0)
]]>.Value

            VerifyFlowGraphAndDiagnosticsForTest(Of MethodBlockSyntax)(source, expectedFlowGraph, expectedDiagnostics)
        End Sub
    End Class
End Namespace
