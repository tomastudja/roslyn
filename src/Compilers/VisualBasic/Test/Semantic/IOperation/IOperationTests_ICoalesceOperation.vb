﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics

    Partial Public Class IOperationTests
        Inherits SemanticModelTestBase

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_01()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As Integer?, alternative as Integer, result As Integer) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Int32) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (Identity)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Int32) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Int32) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IInvocationOperation ( Function System.Nullable(Of System.Int32).GetValueOrDefault() As System.Int32) (OperationKind.Invocation, Type: System.Int32, IsImplicit) (Syntax: 'input')
              Instance Receiver: 
                IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')
              Arguments(0)

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Int32) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Int32, IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Int32, IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Int32, IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_02()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As Integer?, alternative as Long, result As Long) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Int64) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: True, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (WideningNumeric)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Int64) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Int64) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Int64, IsImplicit) (Syntax: 'input')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: True, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningNumeric)
              Operand: 
                IInvocationOperation ( Function System.Nullable(Of System.Int32).GetValueOrDefault() As System.Int32) (OperationKind.Invocation, Type: System.Int32, IsImplicit) (Syntax: 'input')
                  Instance Receiver: 
                    IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')
                  Arguments(0)

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Int64) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Int64, IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Int64, IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Int64, IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_03()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As Integer?, alternative as Long?, result As Long?) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Nullable(Of System.Int64)) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (WideningNullable)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int64)) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int64)) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Nullable(Of System.Int64), IsImplicit) (Syntax: 'input')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningNullable)
              Operand: 
                IInvocationOperation ( Function System.Nullable(Of System.Int32).GetValueOrDefault() As System.Int32) (OperationKind.Invocation, Type: System.Int32, IsImplicit) (Syntax: 'input')
                  Instance Receiver: 
                    IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')
                  Arguments(0)

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int64)) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Nullable(Of System.Int64), IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int64), IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int64), IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_04()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As String, alternative as Object, result As Object) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Object) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.String) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: True, IsUserDefined: False) (MethodSymbol: null)
    (WideningReference)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.String) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.String, IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Object, IsImplicit) (Syntax: 'input')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: True, IsUserDefined: False) (MethodSymbol: null)
                (WideningReference)
              Operand: 
                IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.String, IsImplicit) (Syntax: 'input')

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Object, IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_05()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As Integer?, alternative as Date, result As Object) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertTheseDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Object) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (WideningValue)
  WhenNull: 
    IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Object, IsImplicit) (Syntax: 'alternative')
      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Operand: 
        IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.DateTime) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Object, IsImplicit) (Syntax: 'input')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningValue)
              Operand: 
                IInvocationOperation ( Function System.Nullable(Of System.Int32).GetValueOrDefault() As System.Int32) (OperationKind.Invocation, Type: System.Int32, IsImplicit) (Syntax: 'input')
                  Instance Receiver: 
                    IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')
                  Arguments(0)

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Object, IsImplicit) (Syntax: 'alternative')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningValue)
              Operand: 
                IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.DateTime) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Object, IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_06()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As Integer?, alternative as Object, result As Object) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Object) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (WideningValue)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Object, IsImplicit) (Syntax: 'input')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningValue)
              Operand: 
                IInvocationOperation ( Function System.Nullable(Of System.Int32).GetValueOrDefault() As System.Int32) (OperationKind.Invocation, Type: System.Int32, IsImplicit) (Syntax: 'input')
                  Instance Receiver: 
                    IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')
                  Arguments(0)

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Object, IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_07()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(alternative as Object, result As Object) 'BIND:"Sub M1"
        result = If(Nothing, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Object) (Syntax: 'If(Nothing, alternative)')
  Expression: 
    ILiteralOperation (OperationKind.Literal, Type: null, Constant: null) (Syntax: 'Nothing')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (WideningNothingLiteral)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'Nothing')
          Value: 
            ILiteralOperation (OperationKind.Literal, Type: null, Constant: null) (Syntax: 'Nothing')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: null, Constant: null, IsImplicit) (Syntax: 'Nothing')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'Nothing')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Object, IsImplicit) (Syntax: 'Nothing')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningNothingLiteral)
              Operand: 
                IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: null, Constant: null, IsImplicit) (Syntax: 'Nothing')

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Object) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Object, IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Object, IsImplicit) (Syntax: 'If(Nothing, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_08()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(alternative as Integer, result As Integer) 'BIND:"Sub M1"
        result = If(Nothing, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Int32) (Syntax: 'If(Nothing, alternative)')
  Expression: 
    ILiteralOperation (OperationKind.Literal, Type: null, Constant: null) (Syntax: 'Nothing')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (WideningNothingLiteral)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Int32) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Int32) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'Nothing')
          Value: 
            ILiteralOperation (OperationKind.Literal, Type: null, Constant: null) (Syntax: 'Nothing')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: null, Constant: null, IsImplicit) (Syntax: 'Nothing')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'Nothing')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Int32, IsImplicit) (Syntax: 'Nothing')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningNothingLiteral)
              Operand: 
                IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: null, Constant: null, IsImplicit) (Syntax: 'Nothing')

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Int32) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Int32, IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Int32, IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Int32, IsImplicit) (Syntax: 'If(Nothing, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_09()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(alternative as Integer?, result As Integer?) 'BIND:"Sub M1"
        result = If(Nothing, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Nullable(Of System.Int32)) (Syntax: 'If(Nothing, alternative)')
  Expression: 
    ILiteralOperation (OperationKind.Literal, Type: null, Constant: null) (Syntax: 'Nothing')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (WideningNothingLiteral)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'Nothing')
          Value: 
            ILiteralOperation (OperationKind.Literal, Type: null, Constant: null) (Syntax: 'Nothing')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: null, Constant: null, IsImplicit) (Syntax: 'Nothing')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'Nothing')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'Nothing')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningNothingLiteral)
              Operand: 
                IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: null, Constant: null, IsImplicit) (Syntax: 'Nothing')

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'If(Nothing, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_10()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As Integer?, alternative as Byte?, result As Integer?) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Nullable(Of System.Int32)) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (Identity)
  WhenNull: 
    IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'alternative')
      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Operand: 
        IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Byte)) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'alternative')
              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                (WideningNullable)
              Operand: 
                IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Byte)) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_11()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input As Integer?, alternative as Integer?, result As Integer?) 'BIND:"Sub M1"
        result = If(input, alternative)
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim tree = compilation.SyntaxTrees.Single()
            Dim node = tree.GetRoot().DescendantNodes().OfType(Of BinaryConditionalExpressionSyntax)().Single()

            compilation.VerifyOperationTree(node, expectedOperationTree:=
            <![CDATA[
ICoalesceOperation (OperationKind.Coalesce, Type: System.Nullable(Of System.Int32)) (Syntax: 'If(input, alternative)')
  Expression: 
    IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')
  ValueConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
    (Identity)
  WhenNull: 
    IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'alternative')
]]>.Value)

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IParameterReferenceOperation: input (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input')
          Value: 
            IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input')

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative')
          Value: 
            IParameterReferenceOperation: alternative (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'alternative')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... lternative)')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result = If ... lternative)')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'If(input, alternative)')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[5]
Block[5] - Exit
    Predecessors (1)
        [4]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact()>
        Public Sub CoalesceOperation_12()
            Dim source =
<compilation>
    <file name="a.vb">
        <![CDATA[
Public Class C1
    Sub M1(input1 As Integer?, alternative1 as Integer?, input2 As Integer?, alternative2 as Integer?, result As Integer?) 'BIND:"Sub M1"
        result = If(If(input1, alternative1), If(input2, alternative2))
    End Sub
End Class
         ]]>
    </file>
</compilation>

            Dim compilation = CreateCompilationWithMscorlibAndVBRuntime(source, parseOptions:=TestOptions.RegularWithFlowAnalysisFeature)

            compilation.AssertNoDiagnostics()

            Dim expectedGraph =
            <![CDATA[
Block[0] - Entry
    Statements (0)
    Next Block[1]
Block[1] - Block
    Predecessors (1)
        [0]
    Statements (2)
        IFlowCaptureOperation: 0 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'result')
          Value: 
            IParameterReferenceOperation: result (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'result')

        IFlowCaptureOperation: 1 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input1')
          Value: 
            IParameterReferenceOperation: input1 (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input1')

    Jump if Null to Block[3]
        IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input1')

    Next Block[2]
Block[2] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input1')
          Value: 
            IFlowCaptureReferenceOperation: 1 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input1')

    Next Block[4]
Block[3] - Block
    Predecessors (1)
        [1]
    Statements (1)
        IFlowCaptureOperation: 2 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative1')
          Value: 
            IParameterReferenceOperation: alternative1 (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'alternative1')

    Next Block[4]
Block[4] - Block
    Predecessors (2)
        [2]
        [3]
    Statements (0)
    Jump if Null to Block[6]
        IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'If(input1, alternative1)')

    Next Block[5]
Block[5] - Block
    Predecessors (1)
        [4]
    Statements (1)
        IFlowCaptureOperation: 3 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'If(input1, alternative1)')
          Value: 
            IFlowCaptureReferenceOperation: 2 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'If(input1, alternative1)')

    Next Block[9]
Block[6] - Block
    Predecessors (1)
        [4]
    Statements (1)
        IFlowCaptureOperation: 4 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input2')
          Value: 
            IParameterReferenceOperation: input2 (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'input2')

    Jump if Null to Block[8]
        IFlowCaptureReferenceOperation: 4 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input2')

    Next Block[7]
Block[7] - Block
    Predecessors (1)
        [6]
    Statements (1)
        IFlowCaptureOperation: 3 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'input2')
          Value: 
            IFlowCaptureReferenceOperation: 4 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'input2')

    Next Block[9]
Block[8] - Block
    Predecessors (1)
        [6]
    Statements (1)
        IFlowCaptureOperation: 3 (OperationKind.FlowCapture, Type: null, IsImplicit) (Syntax: 'alternative2')
          Value: 
            IParameterReferenceOperation: alternative2 (OperationKind.ParameterReference, Type: System.Nullable(Of System.Int32)) (Syntax: 'alternative2')

    Next Block[9]
Block[9] - Block
    Predecessors (3)
        [5]
        [7]
        [8]
    Statements (3)
        IExpressionStatementOperation (OperationKind.ExpressionStatement, Type: null) (Syntax: 'result = If ... ernative2))')
          Expression: 
            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result = If ... ernative2))')
              Left: 
                IFlowCaptureReferenceOperation: 0 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'result')
              Right: 
                IFlowCaptureReferenceOperation: 3 (OperationKind.FlowCaptureReference, Type: System.Nullable(Of System.Int32), IsImplicit) (Syntax: 'If(If(input ... ernative2))')

        ILabeledOperation (Label: exit) (OperationKind.Labeled, Type: null, IsImplicit) (Syntax: 'End Sub')
          Statement: 
            null

        IReturnOperation (OperationKind.Return, Type: null, IsImplicit) (Syntax: 'End Sub')
          ReturnedValue: 
            null

    Next Block[10]
Block[10] - Exit
    Predecessors (1)
        [9]
    Statements (0)
]]>.Value

            VerifyFlowGraphForTest(Of MethodBlockSyntax)(compilation, expectedGraph)
        End Sub
    End Class
End Namespace

