// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public partial class IOperationTests : SemanticModelTestBase
    {
        [CompilerTrait(CompilerFeature.IOperation)]
        [Fact]
        public void TestTypeOfExpression()
        {
            string source = @"
using System;

class C
{
    void M(Type t)
    {
        t = /*<bind>*/typeof(int)/*</bind>*/;
    }
}
";
            string expectedOperationTree = @"
ITypeOfExpression (OperationKind.TypeOfExpression, Type: System.Type) (Syntax: 'typeof(int)')
  TypeOperand: System.Int32
";
            var expectedDiagnostics = DiagnosticDescription.None;

            VerifyOperationTreeAndDiagnosticsForTest<TypeOfExpressionSyntax>(source, expectedOperationTree, expectedDiagnostics);
        }

        [CompilerTrait(CompilerFeature.IOperation)]
        [Fact]
        public void TestTypeOfExpression_NonPrimitiveTypeArgument()
        {
            string source = @"
using System;

class C
{
    void M(Type t)
    {
        t = /*<bind>*/typeof(C)/*</bind>*/;
    }
}
";
            string expectedOperationTree = @"
ITypeOfExpression (OperationKind.TypeOfExpression, Type: System.Type) (Syntax: 'typeof(C)')
  TypeOperand: C
";
            var expectedDiagnostics = DiagnosticDescription.None;

            VerifyOperationTreeAndDiagnosticsForTest<TypeOfExpressionSyntax>(source, expectedOperationTree, expectedDiagnostics);
        }

        [CompilerTrait(CompilerFeature.IOperation)]
        [Fact]
        public void TestTypeOfExpression_ErrorTypeArgument()
        {
            string source = @"
using System;

class C
{
    void M(Type t)
    {
        t = /*<bind>*/typeof(UndefinedType)/*</bind>*/;
    }
}
";
            string expectedOperationTree = @"
ITypeOfExpression (OperationKind.TypeOfExpression, Type: System.Type, IsInvalid) (Syntax: 'typeof(UndefinedType)')
  TypeOperand: UndefinedType
";
            var expectedDiagnostics = new DiagnosticDescription[] {
                // CS0246: The type or namespace name 'UndefinedType' could not be found (are you missing a using directive or an assembly reference?)
                //         t = /*<bind>*/typeof(UndefinedType)/*</bind>*/;
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UndefinedType").WithArguments("UndefinedType").WithLocation(8, 30)
            };

            VerifyOperationTreeAndDiagnosticsForTest<TypeOfExpressionSyntax>(source, expectedOperationTree, expectedDiagnostics);
        }

        [CompilerTrait(CompilerFeature.IOperation)]
        [Fact]
        public void TestTypeOfExpression_IdentifierArgument()
        {
            string source = @"
using System;

class C
{
    void M(Type t)
    {
        t = /*<bind>*/typeof(t)/*</bind>*/;
    }
}
";
            string expectedOperationTree = @"
ITypeOfExpression (OperationKind.TypeOfExpression, Type: System.Type, IsInvalid) (Syntax: 'typeof(t)')
  TypeOperand: t
";
            var expectedDiagnostics = new DiagnosticDescription[] {
                // CS0118: 't' is a variable but is used like a type
                //         t = /*<bind>*/typeof(t)/*</bind>*/;
                Diagnostic(ErrorCode.ERR_BadSKknown, "t").WithArguments("t", "variable", "type").WithLocation(8, 30)
            };

            VerifyOperationTreeAndDiagnosticsForTest<TypeOfExpressionSyntax>(source, expectedOperationTree, expectedDiagnostics);
        }

        [CompilerTrait(CompilerFeature.IOperation)]
        [Fact]
        public void TestTypeOfExpression_ExpressionArgument()
        {
            string source = @"
using System;

class C
{
    void M(Type t)
    {
        t = /*<bind>*/typeof(M2()/*</bind>*/);
    }

    Type M2() => null;
}
";
            string expectedOperationTree = @"
IInvalidExpression (OperationKind.InvalidExpression, Type: ?, IsInvalid) (Syntax: 'typeof(M2()')
  Children(1):
      ITypeOfExpression (OperationKind.TypeOfExpression, Type: System.Type, IsInvalid) (Syntax: 'typeof(M2')
        TypeOperand: M2
";
            var expectedDiagnostics = new DiagnosticDescription[] {
                // CS1026: ) expected
                //         t = /*<bind>*/typeof(M2()/*</bind>*/);
                Diagnostic(ErrorCode.ERR_CloseParenExpected, "(").WithLocation(8, 32),
                // CS1002: ; expected
                //         t = /*<bind>*/typeof(M2()/*</bind>*/);
                Diagnostic(ErrorCode.ERR_SemicolonExpected, ")").WithLocation(8, 45),
                // CS1513: } expected
                //         t = /*<bind>*/typeof(M2()/*</bind>*/);
                Diagnostic(ErrorCode.ERR_RbraceExpected, ")").WithLocation(8, 45),
                // CS0246: The type or namespace name 'M2' could not be found (are you missing a using directive or an assembly reference?)
                //         t = /*<bind>*/typeof(M2()/*</bind>*/);
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "M2").WithArguments("M2").WithLocation(8, 30)
            };

            VerifyOperationTreeAndDiagnosticsForTest<InvocationExpressionSyntax>(source, expectedOperationTree, expectedDiagnostics);
        }

        [CompilerTrait(CompilerFeature.IOperation)]
        [Fact]
        public void TestTypeOfExpression_MissingArgument()
        {
            string source = @"
using System;

class C
{
    void M(Type t)
    {
        t = /*<bind>*/typeof()/*</bind>*/;
    }
}
";
            string expectedOperationTree = @"
ITypeOfExpression (OperationKind.TypeOfExpression, Type: System.Type, IsInvalid) (Syntax: 'typeof()')
  TypeOperand: ?
";
            var expectedDiagnostics = new DiagnosticDescription[] {
                // CS1031: Type expected
                //         t = /*<bind>*/typeof()/*</bind>*/;
                Diagnostic(ErrorCode.ERR_TypeExpected, ")").WithLocation(8, 30)
            };

            VerifyOperationTreeAndDiagnosticsForTest<TypeOfExpressionSyntax>(source, expectedOperationTree, expectedDiagnostics);
        }
    }
}
