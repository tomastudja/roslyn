' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Operations
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics

    Partial Public Class IOperationTests
        Inherits SemanticModelTestBase
        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_DoWhileLoopsTest()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim ids As Integer() = New Integer() {6, 7, 8, 10}
        Dim sum As Integer = 0
        Dim i As Integer = 0
        Do'BIND:"Do"
            sum += ids(i)
            i += 1
        Loop While i < 4

        System.Console.WriteLine(sum)
    End Sub
End Class

    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IDoLoopOperation (DoLoopKind: DoWhileBottomLoop) (LoopKind.Do) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'Do'BIND:"Do ... While i < 4')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i < 4')
      Left: 
        ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 4) (Syntax: '4')
  IgnoredCondition: 
    null
  Body: 
    IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Do'BIND:"Do ... While i < 4')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'sum += ids(i)')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'sum += ids(i)')
            Left: 
              ILocalReferenceOperation: sum (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'sum')
            Right: 
              IArrayElementReferenceOperation (OperationKind.ArrayElementReference, IsExpression, Type: System.Int32) (Syntax: 'ids(i)')
                Array reference: 
                  ILocalReferenceOperation: ids (OperationKind.LocalReference, IsExpression, Type: System.Int32()) (Syntax: 'ids')
                Indices(1):
                    ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'i += 1')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            VerifyOperationTreeForTest(Of DoLoopBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_DoUntilLoopTest()
            Dim source = <![CDATA[
Class C
    Private X As Integer = 10
    Sub M(c As C)
        Do Until X < 0'BIND:"Do Until X < 0"
            X = X - 1
        Loop
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
IDoLoopOperation (DoLoopKind: DoUntilTopLoop) (LoopKind.Do) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'Do Until X  ... Loop')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'X < 0')
      Left: 
        IFieldReferenceOperation: C.X As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: 'X')
          Instance Receiver: 
            IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'X')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  IgnoredCondition: 
    null
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Do Until X  ... Loop')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'X = X - 1')
        Expression: 
          ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: 'X = X - 1')
            Left: 
              IFieldReferenceOperation: C.X As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: 'X')
                Instance Receiver: 
                  IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'X')
            Right: 
              IBinaryOperation (BinaryOperatorKind.Subtract, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Int32) (Syntax: 'X - 1')
                Left: 
                  IFieldReferenceOperation: C.X As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: 'X')
                    Instance Receiver: 
                      IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'X')
                Right: 
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            Dim expectedDiagnostics = String.Empty

            VerifyOperationTreeAndDiagnosticsForTest(Of DoLoopBlockSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileConditionTrue()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim index As Integer = 0
        Dim condition As Boolean = True
        While condition'BIND:"While condition"
            Dim value As Integer = System.Threading.Interlocked.Increment(index)
            If value > 10 Then
                condition = False
            End If
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While condi ... End While')
  Condition: 
    ILocalReferenceOperation: condition (OperationKind.LocalReference, IsExpression, Type: System.Boolean) (Syntax: 'condition')
  Body: 
    IBlockOperation (2 statements, 1 locals) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While condi ... End While')
      Locals: Local_1: value As System.Int32
      IVariableDeclarationsOperation (1 declarations) (OperationKind.VariableDeclarations, IsStatement, Type: null) (Syntax: 'Dim value A ... ment(index)')
        IVariableDeclarationOperation (1 variables) (OperationKind.VariableDeclaration, Type: null) (Syntax: 'value')
          Variables: Local_1: value As System.Int32
          Initializer: 
            IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null) (Syntax: '= System.Th ... ment(index)')
              IInvocationOperation (Function System.Threading.Interlocked.Increment(ByRef location As System.Int32) As System.Int32) (OperationKind.Invocation, IsExpression, Type: System.Int32) (Syntax: 'System.Thre ... ment(index)')
                Instance Receiver: 
                  null
                Arguments(1):
                    IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: location) (OperationKind.Argument, Type: null) (Syntax: 'index')
                      ILocalReferenceOperation: index (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'index')
                      InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                      OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IConditionalOperation (OperationKind.Conditional, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.GreaterThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'value > 10')
            Left: 
              ILocalReferenceOperation: value (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'value')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 10) (Syntax: '10')
        WhenTrue: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'condition = False')
              Expression: 
                ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Boolean) (Syntax: 'condition = False')
                  Left: 
                    ILocalReferenceOperation: condition (OperationKind.LocalReference, IsExpression, Type: System.Boolean) (Syntax: 'condition')
                  Right: 
                    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Boolean, Constant: False) (Syntax: 'False')
        WhenFalse: 
          null
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileLoopsTest()
            Dim source = <![CDATA[
Class Program
    Private Shared Function SumWhile() As Integer
        '
        ' Sum numbers 0 .. 4
        '
        Dim sum As Integer = 0
        Dim i As Integer = 0
        While i < 5'BIND:"While i < 5"
            sum += i
            i += 1
        End While
        Return sum
    End Function
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While i < 5 ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i < 5')
      Left: 
        ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 5) (Syntax: '5')
  Body: 
    IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While i < 5 ... End While')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'sum += i')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'sum += i')
            Left: 
              ILocalReferenceOperation: sum (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'sum')
            Right: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'i += 1')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithBreak()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim index As Integer = 0
        While True'BIND:"While True"
            Dim value As Integer = System.Threading.Interlocked.Increment(index)
            If value > 5 Then
                System.Console.WriteLine("While-loop break")
                Exit While
            End If
            System.Console.WriteLine("While-loop statement")
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While True' ... End While')
  Condition: 
    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'True')
  Body: 
    IBlockOperation (3 statements, 1 locals) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While True' ... End While')
      Locals: Local_1: value As System.Int32
      IVariableDeclarationsOperation (1 declarations) (OperationKind.VariableDeclarations, IsStatement, Type: null) (Syntax: 'Dim value A ... ment(index)')
        IVariableDeclarationOperation (1 variables) (OperationKind.VariableDeclaration, Type: null) (Syntax: 'value')
          Variables: Local_1: value As System.Int32
          Initializer: 
            IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null) (Syntax: '= System.Th ... ment(index)')
              IInvocationOperation (Function System.Threading.Interlocked.Increment(ByRef location As System.Int32) As System.Int32) (OperationKind.Invocation, IsExpression, Type: System.Int32) (Syntax: 'System.Thre ... ment(index)')
                Instance Receiver: 
                  null
                Arguments(1):
                    IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: location) (OperationKind.Argument, Type: null) (Syntax: 'index')
                      ILocalReferenceOperation: index (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'index')
                      InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                      OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IConditionalOperation (OperationKind.Conditional, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.GreaterThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'value > 5')
            Left: 
              ILocalReferenceOperation: value (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'value')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 5) (Syntax: '5')
        WhenTrue: 
          IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... oop break")')
              Expression: 
                IInvocationOperation (Sub System.Console.WriteLine(value As System.String)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... oop break")')
                  Instance Receiver: 
                    null
                  Arguments(1):
                      IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '"While-loop break"')
                        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.String, Constant: "While-loop break") (Syntax: '"While-loop break"')
                        InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                        OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
            IBranchOperation (BranchKind.Break, Label: exit) (OperationKind.Branch, IsStatement, Type: null) (Syntax: 'Exit While')
        WhenFalse: 
          null
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... statement")')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.String)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... statement")')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '"While-loop statement"')
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.String, Constant: "While-loop statement") (Syntax: '"While-loop statement"')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithThrow()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim index As Integer = 0
        While True'BIND:"While True"
            Dim value As Integer = System.Threading.Interlocked.Increment(index)
            If value > 100 Then
                Throw New System.Exception("An exception has occurred.")
            End If
            System.Console.WriteLine("While-loop statement")
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While True' ... End While')
  Condition: 
    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'True')
  Body: 
    IBlockOperation (3 statements, 1 locals) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While True' ... End While')
      Locals: Local_1: value As System.Int32
      IVariableDeclarationsOperation (1 declarations) (OperationKind.VariableDeclarations, IsStatement, Type: null) (Syntax: 'Dim value A ... ment(index)')
        IVariableDeclarationOperation (1 variables) (OperationKind.VariableDeclaration, Type: null) (Syntax: 'value')
          Variables: Local_1: value As System.Int32
          Initializer: 
            IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null) (Syntax: '= System.Th ... ment(index)')
              IInvocationOperation (Function System.Threading.Interlocked.Increment(ByRef location As System.Int32) As System.Int32) (OperationKind.Invocation, IsExpression, Type: System.Int32) (Syntax: 'System.Thre ... ment(index)')
                Instance Receiver: 
                  null
                Arguments(1):
                    IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: location) (OperationKind.Argument, Type: null) (Syntax: 'index')
                      ILocalReferenceOperation: index (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'index')
                      InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                      OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IConditionalOperation (OperationKind.Conditional, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.GreaterThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'value > 100')
            Left: 
              ILocalReferenceOperation: value (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'value')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 100) (Syntax: '100')
        WhenTrue: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'Throw New S ... occurred.")')
              Expression: 
                IThrowOperation (OperationKind.Throw, IsExpression, Type: System.Exception) (Syntax: 'Throw New S ... occurred.")')
                  IObjectCreationOperation (Constructor: Sub System.Exception..ctor(message As System.String)) (OperationKind.ObjectCreation, IsExpression, Type: System.Exception) (Syntax: 'New System. ... occurred.")')
                    Arguments(1):
                        IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: message) (OperationKind.Argument, Type: null) (Syntax: '"An excepti ...  occurred."')
                          ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.String, Constant: "An exception has occurred.") (Syntax: '"An excepti ...  occurred."')
                          InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                          OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                    Initializer: 
                      null
        WhenFalse: 
          null
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... statement")')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.String)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... statement")')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '"While-loop statement"')
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.String, Constant: "While-loop statement") (Syntax: '"While-loop statement"')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithAssignment()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim value As Integer = 4
        Dim i As Integer
        While (InlineAssignHelper(i, value)) >= 0'BIND:"While (InlineAssignHelper(i, value)) >= 0"
            System.Console.WriteLine("While {0} {1}", i, value)
            value -= 1
        End While
    End Sub
    Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
        target = value
        Return value
    End Function
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While (Inli ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.GreaterThanOrEqual, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: '(InlineAssi ... alue)) >= 0')
      Left: 
        IParenthesizedOperation (OperationKind.Parenthesized, IsExpression, Type: System.Int32) (Syntax: '(InlineAssi ... (i, value))')
          Operand: 
            IInvocationOperation (Function Program.InlineAssignHelper(Of System.Int32)(ByRef target As System.Int32, value As System.Int32) As System.Int32) (OperationKind.Invocation, IsExpression, Type: System.Int32) (Syntax: 'InlineAssig ... r(i, value)')
              Instance Receiver: 
                null
              Arguments(2):
                  IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: target) (OperationKind.Argument, Type: null) (Syntax: 'i')
                    ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                    InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                    OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'value')
                    ILocalReferenceOperation: value (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'value')
                    InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                    OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  Body: 
    IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While (Inli ... End While')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... , i, value)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(format As System.String, arg0 As System.Object, arg1 As System.Object)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... , i, value)')
            Instance Receiver: 
              null
            Arguments(3):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: format) (OperationKind.Argument, Type: null) (Syntax: '"While {0} {1}"')
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.String, Constant: "While {0} {1}") (Syntax: '"While {0} {1}"')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: arg0) (OperationKind.Argument, Type: null) (Syntax: 'i')
                  IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Object, IsImplicit) (Syntax: 'i')
                    Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                    Operand: 
                      ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: arg1) (OperationKind.Argument, Type: null) (Syntax: 'value')
                  IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Object, IsImplicit) (Syntax: 'value')
                    Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                    Operand: 
                      ILocalReferenceOperation: value (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'value')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'value -= 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Subtract, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'value -= 1')
            Left: 
              ILocalReferenceOperation: value (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'value')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileImplicit()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim number As Integer = 10
        While number'BIND:"While number"
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While numbe ... End While')
  Condition: 
    IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Boolean, IsImplicit) (Syntax: 'number')
      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Operand: 
        ILocalReferenceOperation: number (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'number')
  Body: 
    IBlockOperation (0 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While numbe ... End While')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithReturn()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        System.Console.WriteLine(GetFirstEvenNumber(33))
    End Sub
    Public Shared Function GetFirstEvenNumber(number As Integer) As Integer
        While True'BIND:"While True"
            If (number Mod 2) = 0 Then
                Return number
            End If

            number += 1
        End While
    End Function
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While True' ... End While')
  Condition: 
    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'True')
  Body: 
    IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While True' ... End While')
      IConditionalOperation (OperationKind.Conditional, IsStatement, Type: null) (Syntax: 'If (number  ... End If')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.Equals, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: '(number Mod 2) = 0')
            Left: 
              IParenthesizedOperation (OperationKind.Parenthesized, IsExpression, Type: System.Int32) (Syntax: '(number Mod 2)')
                Operand: 
                  IBinaryOperation (BinaryOperatorKind.Remainder, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Int32) (Syntax: 'number Mod 2')
                    Left: 
                      IParameterReferenceOperation: number (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'number')
                    Right: 
                      ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 2) (Syntax: '2')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
        WhenTrue: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'If (number  ... End If')
            IReturnOperation (OperationKind.Return, IsStatement, Type: null) (Syntax: 'Return number')
              ReturnedValue: 
                IParameterReferenceOperation: number (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'number')
        WhenFalse: 
          null
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'number += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'number += 1')
            Left: 
              IParameterReferenceOperation: number (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'number')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithGoto()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        System.Console.WriteLine(GetFirstEvenNumber(33))
    End Sub
    Public Shared Function GetFirstEvenNumber(number As Integer) As Integer
        While True'BIND:"While True"
            If (number Mod 2) = 0 Then
                GoTo Even
            End If
            number += 1
Even:
            Return number
        End While
    End Function
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While True' ... End While')
  Condition: 
    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'True')
  Body: 
    IBlockOperation (4 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While True' ... End While')
      IConditionalOperation (OperationKind.Conditional, IsStatement, Type: null) (Syntax: 'If (number  ... End If')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.Equals, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: '(number Mod 2) = 0')
            Left: 
              IParenthesizedOperation (OperationKind.Parenthesized, IsExpression, Type: System.Int32) (Syntax: '(number Mod 2)')
                Operand: 
                  IBinaryOperation (BinaryOperatorKind.Remainder, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Int32) (Syntax: 'number Mod 2')
                    Left: 
                      IParameterReferenceOperation: number (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'number')
                    Right: 
                      ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 2) (Syntax: '2')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
        WhenTrue: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'If (number  ... End If')
            IBranchOperation (BranchKind.GoTo, Label: Even) (OperationKind.Branch, IsStatement, Type: null) (Syntax: 'GoTo Even')
        WhenFalse: 
          null
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'number += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'number += 1')
            Left: 
              IParameterReferenceOperation: number (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'number')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
      ILabeledOperation (Label: Even) (OperationKind.Labeled, IsStatement, Type: null) (Syntax: 'Even:')
        Statement: 
          null
      IReturnOperation (OperationKind.Return, IsStatement, Type: null) (Syntax: 'Return number')
        ReturnedValue: 
          IParameterReferenceOperation: number (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'number')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileMissingCondition()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim index As Integer = 0
        Dim condition As Boolean = True
        While 'BIND:"While "
            Dim value As Integer = System.Threading.Interlocked.Increment(index)
            If value > 100 Then
                condition = False
            End If
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null, IsInvalid) (Syntax: 'While 'BIND ... End While')
  Condition: 
    IInvalidOperation (OperationKind.Invalid, IsExpression, Type: null, IsInvalid) (Syntax: '')
      Children(0)
  Body: 
    IBlockOperation (2 statements, 1 locals) (OperationKind.Block, IsStatement, Type: null, IsInvalid) (Syntax: 'While 'BIND ... End While')
      Locals: Local_1: value As System.Int32
      IVariableDeclarationsOperation (1 declarations) (OperationKind.VariableDeclarations, IsStatement, Type: null) (Syntax: 'Dim value A ... ment(index)')
        IVariableDeclarationOperation (1 variables) (OperationKind.VariableDeclaration, Type: null) (Syntax: 'value')
          Variables: Local_1: value As System.Int32
          Initializer: 
            IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null) (Syntax: '= System.Th ... ment(index)')
              IInvocationOperation (Function System.Threading.Interlocked.Increment(ByRef location As System.Int32) As System.Int32) (OperationKind.Invocation, IsExpression, Type: System.Int32) (Syntax: 'System.Thre ... ment(index)')
                Instance Receiver: 
                  null
                Arguments(1):
                    IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: location) (OperationKind.Argument, Type: null) (Syntax: 'index')
                      ILocalReferenceOperation: index (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'index')
                      InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                      OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IConditionalOperation (OperationKind.Conditional, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.GreaterThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'value > 100')
            Left: 
              ILocalReferenceOperation: value (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'value')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 100) (Syntax: '100')
        WhenTrue: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'If value >  ... End If')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'condition = False')
              Expression: 
                ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Boolean) (Syntax: 'condition = False')
                  Left: 
                    ILocalReferenceOperation: condition (OperationKind.LocalReference, IsExpression, Type: System.Boolean) (Syntax: 'condition')
                  Right: 
                    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Boolean, Constant: False) (Syntax: 'False')
        WhenFalse: 
          null
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileMissingStatement()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main()
        Dim index As Integer = 0
        Dim condition As Boolean = True
        While (condition)'BIND:"While (condition)"
        End While
    End Sub
End Class

    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While (cond ... End While')
  Condition: 
    IParenthesizedOperation (OperationKind.Parenthesized, IsExpression, Type: System.Boolean) (Syntax: '(condition)')
      Operand: 
        ILocalReferenceOperation: condition (OperationKind.LocalReference, IsExpression, Type: System.Boolean) (Syntax: 'condition')
  Body: 
    IBlockOperation (0 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While (cond ... End While')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithContinue()
            Dim source = <![CDATA[
Class ContinueTest
    Private Shared Sub Main()
        Dim i As Integer = 0
        While i <= 10'BIND:"While i <= 10"
            i += 1
            If i < 9 Then
                Continue While
            End If
            System.Console.WriteLine(i)
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While i <=  ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThanOrEqual, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i <= 10')
      Left: 
        ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 10) (Syntax: '10')
  Body: 
    IBlockOperation (3 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While i <=  ... End While')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'i += 1')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
      IConditionalOperation (OperationKind.Conditional, IsStatement, Type: null) (Syntax: 'If i < 9 Th ... End If')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i < 9')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 9) (Syntax: '9')
        WhenTrue: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'If i < 9 Th ... End If')
            IBranchOperation (BranchKind.Continue, Label: continue) (OperationKind.Branch, IsStatement, Type: null) (Syntax: 'Continue While')
        WhenFalse: 
          null
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... riteLine(i)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... riteLine(i)')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'i')
                  ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileConditionInvocationExpression()
            Dim source = <![CDATA[
Class ContinueTest
    Private Shared Sub Main()
        Dim i As Integer = 0
        While IsTrue(i)'BIND:"While IsTrue(i)"
            i += 1
            System.Console.WriteLine(i)
        End While
    End Sub
    Private Shared Function IsTrue(i As Integer) As Boolean
        If i < 9 Then
            Return True
        Else
            Return False
        End If
    End Function
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While IsTru ... End While')
  Condition: 
    IInvocationOperation (Function ContinueTest.IsTrue(i As System.Int32) As System.Boolean) (OperationKind.Invocation, IsExpression, Type: System.Boolean) (Syntax: 'IsTrue(i)')
      Instance Receiver: 
        null
      Arguments(1):
          IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: i) (OperationKind.Argument, Type: null) (Syntax: 'i')
            ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
            OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
  Body: 
    IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While IsTru ... End While')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'i += 1')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... riteLine(i)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... riteLine(i)')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'i')
                  ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileNested()
            Dim source = <![CDATA[
Class Test
    Private Shared Sub Main()
        Dim i As Integer = 0
        While i < 10'BIND:"While i < 10"
            i += 1
            Dim j As Integer = 0
            While j < 10
                j += 1
                System.Console.WriteLine(j)
            End While
            System.Console.WriteLine(i)
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While i < 1 ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i < 10')
      Left: 
        ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 10) (Syntax: '10')
  Body: 
    IBlockOperation (4 statements, 1 locals) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While i < 1 ... End While')
      Locals: Local_1: j As System.Int32
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'i += 1')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
      IVariableDeclarationsOperation (1 declarations) (OperationKind.VariableDeclarations, IsStatement, Type: null) (Syntax: 'Dim j As Integer = 0')
        IVariableDeclarationOperation (1 variables) (OperationKind.VariableDeclaration, Type: null) (Syntax: 'j')
          Variables: Local_1: j As System.Int32
          Initializer: 
            IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null) (Syntax: '= 0')
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
      IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While j < 1 ... End While')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'j < 10')
            Left: 
              ILocalReferenceOperation: j (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'j')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 10) (Syntax: '10')
        Body: 
          IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While j < 1 ... End While')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'j += 1')
              Expression: 
                ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'j += 1')
                  Left: 
                    ILocalReferenceOperation: j (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'j')
                  Right: 
                    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... riteLine(j)')
              Expression: 
                IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... riteLine(j)')
                  Instance Receiver: 
                    null
                  Arguments(1):
                      IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'j')
                        ILocalReferenceOperation: j (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'j')
                        InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                        OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... riteLine(i)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... riteLine(i)')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'i')
                  ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileChangeOuterInnerValue()
            Dim source = <![CDATA[
Class Test
    Private Shared Sub Main()
        Dim i As Integer = 0
        While i < 10'BIND:"While i < 10"
            i += 1
            Dim j As Integer = 0
            While j < 10
                j += 1
                i = i + j
                System.Console.WriteLine(j)
            End While
            System.Console.WriteLine(i)
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While i < 1 ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i < 10')
      Left: 
        ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 10) (Syntax: '10')
  Body: 
    IBlockOperation (4 statements, 1 locals) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While i < 1 ... End While')
      Locals: Local_1: j As System.Int32
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'i += 1')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
      IVariableDeclarationsOperation (1 declarations) (OperationKind.VariableDeclarations, IsStatement, Type: null) (Syntax: 'Dim j As Integer = 0')
        IVariableDeclarationOperation (1 variables) (OperationKind.VariableDeclaration, Type: null) (Syntax: 'j')
          Variables: Local_1: j As System.Int32
          Initializer: 
            IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null) (Syntax: '= 0')
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
      IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While j < 1 ... End While')
        Condition: 
          IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'j < 10')
            Left: 
              ILocalReferenceOperation: j (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'j')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 10) (Syntax: '10')
        Body: 
          IBlockOperation (3 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While j < 1 ... End While')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'j += 1')
              Expression: 
                ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'j += 1')
                  Left: 
                    ILocalReferenceOperation: j (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'j')
                  Right: 
                    ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i = i + j')
              Expression: 
                ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: 'i = i + j')
                  Left: 
                    ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                  Right: 
                    IBinaryOperation (BinaryOperatorKind.Add, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Int32) (Syntax: 'i + j')
                      Left: 
                        ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                      Right: 
                        ILocalReferenceOperation: j (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'j')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... riteLine(j)')
              Expression: 
                IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... riteLine(j)')
                  Instance Receiver: 
                    null
                  Arguments(1):
                      IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'j')
                        ILocalReferenceOperation: j (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'j')
                        InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                        OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... riteLine(i)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... riteLine(i)')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'i')
                  ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileIncrementInCondition()
            Dim source = <![CDATA[
Class Program
    Private Shared Sub Main(args As String())
        Dim i As Integer = 0
        While System.Threading.Interlocked.Increment(i) < 5'BIND:"While System.Threading.Interlocked.Increment(i) < 5"
            System.Console.WriteLine(i)
        End While
    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While Syste ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'System.Thre ... ment(i) < 5')
      Left: 
        IInvocationOperation (Function System.Threading.Interlocked.Increment(ByRef location As System.Int32) As System.Int32) (OperationKind.Invocation, IsExpression, Type: System.Int32) (Syntax: 'System.Thre ... ncrement(i)')
          Instance Receiver: 
            null
          Arguments(1):
              IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: location) (OperationKind.Argument, Type: null) (Syntax: 'i')
                ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 5) (Syntax: '5')
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While Syste ... End While')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'System.Cons ... riteLine(i)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'System.Cons ... riteLine(i)')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: 'i')
                  ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileInfiniteLoop()
            Dim source = <![CDATA[
Class C
    Private Shared Sub Main(args As String())
        Dim i As Integer = 1
        While i > 0'BIND:"While i > 0"
            i += 1
        End While
    End Sub
End Class

    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While i > 0 ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.GreaterThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i > 0')
      Left: 
        ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While i > 0 ... End While')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i += 1')
        Expression: 
          ICompoundAssignmentOperation (BinaryOperatorKind.Add, Checked) (OperationKind.CompoundAssignment, IsExpression, Type: System.Int32) (Syntax: 'i += 1')
            Left: 
              ILocalReferenceOperation: i (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileConstantCheck()
            Dim source = <![CDATA[
Class Program
    Private Function foo() As Boolean
        Const b As Boolean = True
        While b = b'BIND:"While b = b"
            Return b
        End While
    End Function

End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While b = b ... End While')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.Equals, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'b = b')
      Left: 
        ILocalReferenceOperation: b (OperationKind.LocalReference, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'b')
      Right: 
        ILocalReferenceOperation: b (OperationKind.LocalReference, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'b')
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While b = b ... End While')
      IReturnOperation (OperationKind.Return, IsStatement, Type: null) (Syntax: 'Return b')
        ReturnedValue: 
          ILocalReferenceOperation: b (OperationKind.LocalReference, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'b')
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithTryCatch()
            Dim source = <![CDATA[
Public Class TryCatchFinally
    Public Sub TryMethod()
        Dim x As SByte = 111, y As SByte
        While System.Math.Max(System.Threading.Interlocked.Decrement(x), x + 1) > 0'BIND:"While System.Math.Max(System.Threading.Interlocked.Decrement(x), x + 1) > 0"
            Try
                y = CSByte(x / 2)
            Finally
                Throw New System.Exception()
            End Try
        End While

    End Sub
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null, IsInvalid) (Syntax: 'While Syste ... End While')
  Condition: 
    IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Boolean, IsInvalid, IsImplicit) (Syntax: 'System.Math ...  x + 1) > 0')
      Conversion: CommonConversion (Exists: False, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Operand: 
        IBinaryOperation (BinaryOperatorKind.GreaterThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: ?, IsInvalid) (Syntax: 'System.Math ...  x + 1) > 0')
          Left: 
            IInvalidOperation (OperationKind.Invalid, IsExpression, Type: ?, IsInvalid) (Syntax: 'System.Math ... (x), x + 1)')
              Children(3):
                  IOperation:  (OperationKind.None, Type: null) (Syntax: 'System.Math.Max')
                    Children(1):
                        IOperation:  (OperationKind.None, Type: null) (Syntax: 'System.Math')
                  IInvalidOperation (OperationKind.Invalid, IsExpression, Type: ?, IsInvalid) (Syntax: 'System.Thre ... ecrement(x)')
                    Children(2):
                        IOperation:  (OperationKind.None, Type: null, IsInvalid) (Syntax: 'System.Thre ... d.Decrement')
                          Children(1):
                              IOperation:  (OperationKind.None, Type: null) (Syntax: 'System.Thre ... Interlocked')
                        ILocalReferenceOperation: x (OperationKind.LocalReference, IsExpression, Type: System.SByte) (Syntax: 'x')
                  IBinaryOperation (BinaryOperatorKind.Add, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Int32) (Syntax: 'x + 1')
                    Left: 
                      IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Int32, IsImplicit) (Syntax: 'x')
                        Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: True, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                        Operand: 
                          ILocalReferenceOperation: x (OperationKind.LocalReference, IsExpression, Type: System.SByte) (Syntax: 'x')
                    Right: 
                      ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
          Right: 
            ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null, IsInvalid) (Syntax: 'While Syste ... End While')
      ITryOperation (OperationKind.Try, IsStatement, Type: null) (Syntax: 'Try ... End Try')
        Body: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Try ... End Try')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'y = CSByte(x / 2)')
              Expression: 
                ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.SByte) (Syntax: 'y = CSByte(x / 2)')
                  Left: 
                    ILocalReferenceOperation: y (OperationKind.LocalReference, IsExpression, Type: System.SByte) (Syntax: 'y')
                  Right: 
                    IConversionOperation (Explicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.SByte) (Syntax: 'CSByte(x / 2)')
                      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: True, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                      Operand: 
                        IBinaryOperation (BinaryOperatorKind.Divide, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Double) (Syntax: 'x / 2')
                          Left: 
                            IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Double, IsImplicit) (Syntax: 'x')
                              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: True, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                              Operand: 
                                ILocalReferenceOperation: x (OperationKind.LocalReference, IsExpression, Type: System.SByte) (Syntax: 'x')
                          Right: 
                            IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Double, Constant: 2, IsImplicit) (Syntax: '2')
                              Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: True, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                              Operand: 
                                ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 2) (Syntax: '2')
        Catch clauses(0)
        Finally: 
          IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Finally ... Exception()')
            IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'Throw New S ... Exception()')
              Expression: 
                IThrowOperation (OperationKind.Throw, IsExpression, Type: System.Exception) (Syntax: 'Throw New S ... Exception()')
                  IObjectCreationOperation (Constructor: Sub System.Exception..ctor()) (OperationKind.ObjectCreation, IsExpression, Type: System.Exception) (Syntax: 'New System.Exception()')
                    Arguments(0)
                    Initializer: 
                      null
]]>.Value

            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_DoWhileFuncCall()
            Dim source = <![CDATA[
Imports System

Class C
    Sub F()
        Do While G()'BIND:"Do While G()"
            Console.WriteLine(1)
        Loop
    End Sub

    Function G() As Boolean
        Return False
    End Function
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IDoLoopOperation (DoLoopKind: DoWhileTopLoop) (LoopKind.Do) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'Do While G( ... Loop')
  Condition: 
    IInvocationOperation ( Function C.G() As System.Boolean) (OperationKind.Invocation, IsExpression, Type: System.Boolean) (Syntax: 'G()')
      Instance Receiver: 
        IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'G')
      Arguments(0)
  IgnoredCondition: 
    null
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Do While G( ... Loop')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'Console.WriteLine(1)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'Console.WriteLine(1)')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '1')
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of DoLoopBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_DoLoopWhileStatement()
            Dim source = <![CDATA[
Imports System

Class C
    Sub F()
        Do'BIND:"Do"
            Console.WriteLine(1)
        Loop While G()
    End Sub

    Function G() As Boolean
        Return False
    End Function
End Class
    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IDoLoopOperation (DoLoopKind: DoWhileBottomLoop) (LoopKind.Do) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'Do'BIND:"Do ... p While G()')
  Condition: 
    IInvocationOperation ( Function C.G() As System.Boolean) (OperationKind.Invocation, IsExpression, Type: System.Boolean) (Syntax: 'G()')
      Instance Receiver: 
        IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'G')
      Arguments(0)
  IgnoredCondition: 
    null
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Do'BIND:"Do ... p While G()')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'Console.WriteLine(1)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(value As System.Int32)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'Console.WriteLine(1)')
            Instance Receiver: 
              null
            Arguments(1):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '1')
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
]]>.Value

            VerifyOperationTreeForTest(Of DoLoopBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_DoLoopUntilStatement()
            Dim source = <![CDATA[
Class C
    Private X As Integer = 10
    Sub M(c As C)
        Do'BIND:"Do"
            X = X - 1
        Loop Until X < 0
    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
IDoLoopOperation (DoLoopKind: DoUntilBottomLoop) (LoopKind.Do) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'Do'BIND:"Do ... Until X < 0')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.LessThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'X < 0')
      Left: 
        IFieldReferenceOperation: C.X As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: 'X')
          Instance Receiver: 
            IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'X')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  IgnoredCondition: 
    null
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'Do'BIND:"Do ... Until X < 0')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'X = X - 1')
        Expression: 
          ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: 'X = X - 1')
            Left: 
              IFieldReferenceOperation: C.X As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: 'X')
                Instance Receiver: 
                  IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'X')
            Right: 
              IBinaryOperation (BinaryOperatorKind.Subtract, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Int32) (Syntax: 'X - 1')
                Left: 
                  IFieldReferenceOperation: C.X As System.Int32 (OperationKind.FieldReference, IsExpression, Type: System.Int32) (Syntax: 'X')
                    Instance Receiver: 
                      IInstanceReferenceOperation (OperationKind.InstanceReference, IsExpression, Type: C, IsImplicit) (Syntax: 'X')
                Right: 
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            Dim expectedDiagnostics = String.Empty

            VerifyOperationTreeAndDiagnosticsForTest(Of DoLoopBlockSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_WhileWithNot()
            Dim source = <![CDATA[
Imports System
Module M1
    Sub Main()
        Dim x As Integer
        Dim breakLoop As Boolean
        x = 1
        breakLoop = False
        While Not breakLoop'BIND:"While Not breakLoop"
            Console.WriteLine("Iterate {0}", x)
            breakLoop = True
        End While
    End Sub
End Module

    ]]>.Value

            Dim expectedOperationTree = <![CDATA[
IWhileLoopOperation (LoopKind.While) (OperationKind.Loop, IsStatement, Type: null) (Syntax: 'While Not b ... End While')
  Condition: 
    IUnaryOperation (UnaryOperatorKind.Not, Checked) (OperationKind.UnaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'Not breakLoop')
      Operand: 
        ILocalReferenceOperation: breakLoop (OperationKind.LocalReference, IsExpression, Type: System.Boolean) (Syntax: 'breakLoop')
  Body: 
    IBlockOperation (2 statements) (OperationKind.Block, IsStatement, Type: null) (Syntax: 'While Not b ... End While')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'Console.Wri ... te {0}", x)')
        Expression: 
          IInvocationOperation (Sub System.Console.WriteLine(format As System.String, arg0 As System.Object)) (OperationKind.Invocation, IsExpression, Type: System.Void) (Syntax: 'Console.Wri ... te {0}", x)')
            Instance Receiver: 
              null
            Arguments(2):
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: format) (OperationKind.Argument, Type: null) (Syntax: '"Iterate {0}"')
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.String, Constant: "Iterate {0}") (Syntax: '"Iterate {0}"')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: arg0) (OperationKind.Argument, Type: null) (Syntax: 'x')
                  IConversionOperation (Implicit, TryCast: False, Unchecked) (OperationKind.Conversion, IsExpression, Type: System.Object, IsImplicit) (Syntax: 'x')
                    Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                    Operand: 
                      ILocalReferenceOperation: x (OperationKind.LocalReference, IsExpression, Type: System.Int32) (Syntax: 'x')
                  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'breakLoop = True')
        Expression: 
          ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Boolean) (Syntax: 'breakLoop = True')
            Left: 
              ILocalReferenceOperation: breakLoop (OperationKind.LocalReference, IsExpression, Type: System.Boolean) (Syntax: 'breakLoop')
            Right: 
              ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Boolean, Constant: True) (Syntax: 'True')
]]>.Value
            VerifyOperationTreeForTest(Of WhileBlockSyntax)(source, expectedOperationTree)
        End Sub

        <CompilerTrait(CompilerFeature.IOperation)>
        <Fact(), WorkItem(17602, "https://github.com/dotnet/roslyn/issues/17602")>
        Public Sub IWhileUntilLoopStatement_DoWhileWithTopAndBottomCondition()
            Dim source = <![CDATA[
Class C
    Sub M(i As Integer)
        Do While i > 0'BIND:"Do While i > 0"
            i = i + 1
        Loop Until i <= 0

    End Sub
End Class]]>.Value

            Dim expectedOperationTree = <![CDATA[
IDoLoopOperation (DoLoopKind: None) (LoopKind.Do) (OperationKind.Loop, IsStatement, Type: null, IsInvalid) (Syntax: 'Do While i  ... ntil i <= 0')
  Condition: 
    IBinaryOperation (BinaryOperatorKind.GreaterThan, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i > 0')
      Left: 
        IParameterReferenceOperation: i (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  IgnoredCondition: 
    IBinaryOperation (BinaryOperatorKind.LessThanOrEqual, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Boolean) (Syntax: 'i <= 0')
      Left: 
        IParameterReferenceOperation: i (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'i')
      Right: 
        ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 0) (Syntax: '0')
  Body: 
    IBlockOperation (1 statements) (OperationKind.Block, IsStatement, Type: null, IsInvalid) (Syntax: 'Do While i  ... ntil i <= 0')
      IExpressionStatementOperation (OperationKind.ExpressionStatement, IsStatement, Type: null) (Syntax: 'i = i + 1')
        Expression: 
          ISimpleAssignmentOperation (OperationKind.SimpleAssignment, IsExpression, Type: System.Int32) (Syntax: 'i = i + 1')
            Left: 
              IParameterReferenceOperation: i (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'i')
            Right: 
              IBinaryOperation (BinaryOperatorKind.Add, Checked) (OperationKind.BinaryOperator, IsExpression, Type: System.Int32) (Syntax: 'i + 1')
                Left: 
                  IParameterReferenceOperation: i (OperationKind.ParameterReference, IsExpression, Type: System.Int32) (Syntax: 'i')
                Right: 
                  ILiteralOperation (OperationKind.Literal, IsExpression, Type: System.Int32, Constant: 1) (Syntax: '1')
]]>.Value

            Dim expectedDiagnostics = <![CDATA[
BC30238: 'Loop' cannot have a condition if matching 'Do' has one.
        Loop Until i <= 0
             ~~~~~
]]>.Value

            VerifyOperationTreeAndDiagnosticsForTest(Of DoLoopBlockSyntax)(source, expectedOperationTree, expectedDiagnostics)
        End Sub
    End Class
End Namespace
