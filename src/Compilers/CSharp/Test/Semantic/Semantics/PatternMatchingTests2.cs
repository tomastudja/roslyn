﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    [CompilerTrait(CompilerFeature.Patterns)]
    public class PatternMatchingTests2 : PatternMatchingTestBase
    {
        CSharpCompilation CreatePatternCompilation(string source)
        {
            return CreateCompilation(source, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularWithRecursivePatterns);
        }

        [Fact]
        public void Patterns2_00()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Console.WriteLine(1 is int {} x ? x : -1);
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"1");
        }

        [Fact]
        public void Patterns2_01()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Point p = new Point();
        Check(true, p is Point(3, 4) { Length: 5 } q1 && Check(p, q1));
        Check(false, p is Point(1, 4) { Length: 5 });
        Check(false, p is Point(3, 1) { Length: 5 });
        Check(false, p is Point(3, 4) { Length: 1 });
        Check(true, p is (3, 4) { Length: 5 } q2 && Check(p, q2));
        Check(false, p is (1, 4) { Length: 5 });
        Check(false, p is (3, 1) { Length: 5 });
        Check(false, p is (3, 4) { Length: 1 });
    }
    private static bool Check<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception($""expected: {expected}; actual: {actual}"");
        return true;
    }
}
public class Point
{
    public void Deconstruct(out int X, out int Y)
    {
        X = 3;
        Y = 4;
    }
    public int Length => 5;
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: "");
        }

        [Fact]
        public void Patterns2_02()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Point p = new Point();
        Check(true, p is Point(3, 4) { Length: 5 } q1 && Check(p, q1));
        Check(false, p is Point(1, 4) { Length: 5 });
        Check(false, p is Point(3, 1) { Length: 5 });
        Check(false, p is Point(3, 4) { Length: 1 });
        Check(true, p is (3, 4) { Length: 5 } q2 && Check(p, q2));
        Check(false, p is (1, 4) { Length: 5 });
        Check(false, p is (3, 1) { Length: 5 });
        Check(false, p is (3, 4) { Length: 1 });
    }
    private static bool Check<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception($""expected: {expected}; actual: {actual}"");
        return true;
    }
}
public class Point
{
    public int Length => 5;
}
public static class PointExtensions
{
    public static void Deconstruct(this Point p, out int X, out int Y)
    {
        X = 3;
        Y = 4;
    }
}
";
            // We use a compilation profile that provides System.Runtime.CompilerServices.ExtensionAttribute needed for this test
            var compilation = CreateCompilationWithMscorlib45(source, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularWithRecursivePatterns);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: "");
        }

        [Fact]
        public void Patterns2_03()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        var p = (x: 3, y: 4);
        Check(true, p is (3, 4) q1 && Check(p, q1));
        Check(false, p is (1, 4) { x: 3 });
        Check(false, p is (3, 1) { y: 4 });
        Check(false, p is (3, 4) { x: 1 });
        Check(true, p is (3, 4) { x: 3 } q2 && Check(p, q2));
        Check(false, p is (1, 4) { x: 3 });
        Check(false, p is (3, 1) { x: 3 });
        Check(false, p is (3, 4) { x: 1 });
    }
    private static bool Check<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception($""expected: {expected}; actual: {actual}"");
        return true;
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: "");
        }

        [Fact]
        public void Patterns2_DiscardPattern_01()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Point p = new Point();
        Check(true, p is Point(_, _) { Length: _ } q1 && Check(p, q1));
        Check(false, p is Point(1, _) { Length: _ });
        Check(false, p is Point(_, 1) { Length: _ });
        Check(false, p is Point(_, _) { Length: 1 });
        Check(true, p is (_, _) { Length: _ } q2 && Check(p, q2));
        Check(false, p is (1, _) { Length: _ });
        Check(false, p is (_, 1) { Length: _ });
        Check(false, p is (_, _) { Length: 1 });
    }
    private static bool Check<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception($""expected: {expected}; actual: {actual}"");
        return true;
    }
}
public class Point
{
    public void Deconstruct(out int X, out int Y)
    {
        X = 3;
        Y = 4;
    }
    public int Length => 5;
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: "");
        }

        [Fact]
        public void Patterns2_Switch01()
        {
            var sourceTemplate =
@"
class Program
{{
    public static void Main()
    {{
        var p = (true, false);
        switch (p)
        {{
            {0}
            {1}
            {2}
            case (_, _): // error - subsumed
                break;
        }}
    }}
}}";
            void testErrorCase(string s1, string s2, string s3)
            {
                var source = string.Format(sourceTemplate, s1, s2, s3);
                var compilation = CreatePatternCompilation(source);
                compilation.VerifyDiagnostics(
                    // (12,18): error CS8120: The switch case has already been handled by a previous case.
                    //             case (_, _): // error - subsumed
                    Diagnostic(ErrorCode.ERR_SwitchCaseSubsumed, "(_, _)").WithLocation(12, 18)
                    );
            }
            void testGoodCase(string s1, string s2)
            {
                var source = string.Format(sourceTemplate, s1, s2, string.Empty);
                var compilation = CreatePatternCompilation(source);
                compilation.VerifyDiagnostics(
                    );
            }
            var c1 = "case (true, _):";
            var c2 = "case (false, false):";
            var c3 = "case (_, true):";
            testErrorCase(c1, c2, c3);
            testErrorCase(c2, c3, c1);
            testErrorCase(c3, c1, c2);
            testErrorCase(c1, c3, c2);
            testErrorCase(c3, c2, c1);
            testErrorCase(c2, c1, c3);
            testGoodCase(c1, c2);
            testGoodCase(c1, c3);
            testGoodCase(c2, c3);
            testGoodCase(c2, c1);
            testGoodCase(c3, c1);
            testGoodCase(c3, c2);
        }

        [Fact]
        public void Patterns2_Switch02()
        {
            var source =
@"
class Program
{
    public static void Main()
    {
        Point p = new Point();
        switch (p)
        {
            case Point(3, 4) { Length: 5 }:
                System.Console.WriteLine(true);
                break;
            default:
                System.Console.WriteLine(false);
                break;
        }
    }
}
public class Point
{
    public void Deconstruct(out int X, out int Y)
    {
        X = 3;
        Y = 4;
    }
    public int Length => 5;
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: "True");
        }

        [Fact]
        public void DefaultPattern()
        {
            var source =
@"class Program
{
    public static void Main()
    {
        int i = 12;
        if (i is default) {} // error 1
        if (i is (default)) {} // error 2
        switch (i) { case default: break; } // warning 3
        switch (i) { case default when true: break; } // error 4
        switch ((1, 2)) { case (1, default): break; } // error 5
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (6,18): error CS8405: A default literal 'default' is not valid as a pattern. Use another literal (e.g. '0' or 'null') as appropriate. To match everything, use a discard pattern '_'.
                //         if (i is default) {} // error 1
                Diagnostic(ErrorCode.ERR_DefaultPattern, "default").WithLocation(6, 18),
                // (7,19): error CS8405: A default literal 'default' is not valid as a pattern. Use another literal (e.g. '0' or 'null') as appropriate. To match everything, use a discard pattern '_'.
                //         if (i is (default)) {} // error 2
                Diagnostic(ErrorCode.ERR_DefaultPattern, "default").WithLocation(7, 19),
                // (8,27): error CS8313: Did you mean to use the default switch label ('default:') rather than 'case default:'? If you really mean to use the default value, use another literal ('case 0:' or 'case null:') as appropriate.
                //         switch (i) { case default: break; } // warning 3
                Diagnostic(ErrorCode.ERR_DefaultInSwitch, "default").WithLocation(8, 27),
                // (9,27): error CS8405: A default literal 'default' is not valid as a pattern. Use another literal (e.g. '0' or 'null') as appropriate. To match everything, use a discard pattern '_'.
                //         switch (i) { case default when true: break; } // error 4
                Diagnostic(ErrorCode.ERR_DefaultPattern, "default").WithLocation(9, 27),
                // (10,36): error CS8405: A default literal 'default' is not valid as a pattern. Use another literal (e.g. '0' or 'null') as appropriate. To match everything, use a discard pattern '_'.
                //         switch ((1, 2)) { case (1, default): break; } // error 5
                Diagnostic(ErrorCode.ERR_DefaultPattern, "default").WithLocation(10, 36)
                );
        }

        [Fact]
        public void SwitchExpression_01()
        {
            // test appropriate language version or feature flag
            var source =
@"class Program
{
    public static void Main()
    {
        var r = 1 switch { _ => 0 };
    }
}";
            CreateCompilation(source, options: TestOptions.DebugExe).VerifyDiagnostics(
                // (5,17): error CS8058: Feature 'recursive patterns' is experimental and unsupported; use '/features:patterns2' to enable.
                //         var r = 1 switch ( _ => 0 );
                Diagnostic(ErrorCode.ERR_FeatureIsExperimental, "1 switch { _ => 0 }").WithArguments("recursive patterns", "patterns2").WithLocation(5, 17)
                );
        }

        [Fact]
        public void SwitchExpression_02()
        {
            // test switch expression's governing expression has no type
            // test switch expression's governing expression has type void
            var source =
@"class Program
{
    public static void Main()
    {
        var r1 = (1, null) switch { _ => 0 };
        var r2 = System.Console.Write(1) switch { _ => 0 };
    }
}";
            CreatePatternCompilation(source).VerifyDiagnostics(
                // (5,18): error CS8117: Invalid operand for pattern match; value required, but found '(int, <null>)'.
                //         var r1 = (1, null) switch ( _ => 0 );
                Diagnostic(ErrorCode.ERR_BadPatternExpression, "(1, null)").WithArguments("(int, <null>)").WithLocation(5, 18),
                // (6,18): error CS8117: Invalid operand for pattern match; value required, but found 'void'.
                //         var r2 = System.Console.Write(1) switch ( _ => 0 );
                Diagnostic(ErrorCode.ERR_BadPatternExpression, "System.Console.Write(1)").WithArguments("void").WithLocation(6, 18)
                );
        }

        [Fact]
        public void SwitchExpression_03()
        {
            // test that a ternary expression is not at an appropriate precedence
            // for the constant expression of a constant pattern in a switch expression arm.
            var source =
@"class Program
{
    public static void Main()
    {
        bool b = true;
        var r1 = b switch { true ? true : true => true, false => false };
        var r2 = b switch { (true ? true : true) => true, false => false };
    }
}";
            // PROTOTYPE(patterns2): This is admittedly poor syntax error recovery (for the line declaring r2),
            // but this test demonstrates that it is a syntax error.
            CreatePatternCompilation(source).VerifyDiagnostics(
                // (6,34): error CS1003: Syntax error, '=>' expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_SyntaxError, "?").WithArguments("=>", "?").WithLocation(6, 34),
                // (6,34): error CS1525: Invalid expression term '?'
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "?").WithArguments("?").WithLocation(6, 34),
                // (6,48): error CS1513: } expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_RbraceExpected, "=>").WithLocation(6, 48),
                // (6,48): error CS1003: Syntax error, ',' expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_SyntaxError, "=>").WithArguments(",", "=>").WithLocation(6, 48),
                // (6,51): error CS1002: ; expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "true").WithLocation(6, 51),
                // (6,55): error CS1002: ; expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_SemicolonExpected, ",").WithLocation(6, 55),
                // (6,55): error CS1513: } expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_RbraceExpected, ",").WithLocation(6, 55),
                // (6,63): error CS1002: ; expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "=>").WithLocation(6, 63),
                // (6,63): error CS1513: } expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_RbraceExpected, "=>").WithLocation(6, 63),
                // (6,72): error CS1002: ; expected
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "}").WithLocation(6, 72),
                // (6,73): error CS1597: Semicolon after method or accessor block is not valid
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.ERR_UnexpectedSemicolon, ";").WithLocation(6, 73),
                // (9,1): error CS1022: Type or namespace definition, or end-of-file expected
                // }
                Diagnostic(ErrorCode.ERR_EOFExpected, "}").WithLocation(9, 1),
                // (7,9): error CS0825: The contextual keyword 'var' may only appear within a local variable declaration or in script code
                //         var r2 = b switch { (true ? true : true) => true, false => false };
                Diagnostic(ErrorCode.ERR_TypeVarNotFound, "var").WithLocation(7, 9),
                // (7,18): error CS0103: The name 'b' does not exist in the current context
                //         var r2 = b switch { (true ? true : true) => true, false => false };
                Diagnostic(ErrorCode.ERR_NameNotInContext, "b").WithArguments("b").WithLocation(7, 18),
                // (7,20): warning CS8409: The switch expression does not handle all possible inputs (it is not exhaustive).
                //         var r2 = b switch { (true ? true : true) => true, false => false };
                Diagnostic(ErrorCode.WRN_SwitchExpressionNotExhaustive, "switch").WithLocation(7, 20),
                // (6,20): warning CS8409: The switch expression does not handle all possible inputs (it is not exhaustive).
                //         var r1 = b switch { true ? true : true => true, false => false };
                Diagnostic(ErrorCode.WRN_SwitchExpressionNotExhaustive, "switch").WithLocation(6, 20)
                );
        }

        [Fact]
        public void SwitchExpression_04()
        {
            // test that a ternary expression is permitted as a constant pattern in recursive contexts and the case expression.
            var source =
@"class Program
{
    public static void Main()
    {
        var b = (true, false);
        var r1 = b switch { (true ? true : true, _) => true, _ => false };
        var r2 = b is (true ? true : true, _);
        switch (b.Item1) { case true ? true : true: break; }
    }
}";
            CreatePatternCompilation(source).VerifyDiagnostics(
                );
        }

        [Fact]
        public void SwitchExpression_05()
        {
            // test throw expression in match arm.
            var source =
@"class Program
{
    public static void Main()
    {
        var x = 1 switch { 1 => 1, _ => throw null };
    }
}";
            CreatePatternCompilation(source).VerifyDiagnostics(
                );
        }

        [Fact]
        public void SwitchExpression_06()
        {
            // test common type vs delegate in match expression
            var source =
@"class Program
{
    public static void Main()
    {
        var x = 1 switch { 0 => M, 1 => new D(M), 2 => M };
        x();
    }
    public static void M() {}
    public delegate void D();
}";
            CreatePatternCompilation(source).VerifyDiagnostics(
                // (5,19): warning CS8409: The switch expression does not handle all possible inputs (it is not exhaustive).
                //         var x = 1 switch { 0 => M, 1 => new D(M), 2 => M };
                Diagnostic(ErrorCode.WRN_SwitchExpressionNotExhaustive, "switch").WithLocation(5, 19)
                );
        }

        [Fact]
        public void SwitchExpression_07()
        {
            // test flow analysis of the switch expression
            var source =
@"class Program
{
    public static void Main()
    {
        int q = 1;
        int u;
        var x = q switch { 0 => u=0, 1 => u=1, _ => u=2 };
        System.Console.WriteLine(u);
    }
}";
            CreatePatternCompilation(source).VerifyDiagnostics(
                );
        }

        [Fact]
        public void SwitchExpression_08()
        {
            // test flow analysis of the switch expression
            var source =
@"class Program
{
    public static void Main()
    {
        int q = 1;
        int u;
        var x = q switch { 0 => u=0, 1 => 1, _ => u=2 };
        System.Console.WriteLine(u);
    }
    static int M(int i) => i;
}";
            CreatePatternCompilation(source).VerifyDiagnostics(
                // (8,34): error CS0165: Use of unassigned local variable 'u'
                //         System.Console.WriteLine(u);
                Diagnostic(ErrorCode.ERR_UseDefViolation, "u").WithArguments("u").WithLocation(8, 34)
                );
        }

        [Fact]
        public void SwitchExpression_09()
        {
            // test flow analysis of the switch expression
            var source =
@"class Program
{
    public static void Main()
    {
        int q = 1;
        int u;
        var x = q switch { 0 => u=0, 1 => u=M(u), _ => u=2 };
        System.Console.WriteLine(u);
    }
    static int M(int i) => i;
}";
            CreatePatternCompilation(source).VerifyDiagnostics(
                // (7,47): error CS0165: Use of unassigned local variable 'u'
                //         var x = q switch { 0 => u=0, 1 => u=M(u), _ => u=2 };
                Diagnostic(ErrorCode.ERR_UseDefViolation, "u").WithArguments("u").WithLocation(7, 47)
                );
        }

        [Fact]
        public void SwitchExpression_10()
        {
            // test lazily inferring variables in the pattern
            // test lazily inferring variables in the when clause
            // test lazily inferring variables in the arrow expression
            var source =
@"class Program
{
    public static void Main()
    {
        int a = 1;
        var b = a switch { var x1 => x1 };
        var c = a switch { var x2 when x2 is var x3 => x3 };
        var d = a switch { var x4 => x4 is var x5 ? x5 : 1 };
    }
    static int M(int i) => i;
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,19): warning CS8409: The switch expression does not handle all possible inputs (it is not exhaustive).
                //         var c = a switch { var x2 when x2 is var x3 => x3 };
                Diagnostic(ErrorCode.WRN_SwitchExpressionNotExhaustive, "switch").WithLocation(7, 19)
                );
            var names = new[] { "x1", "x2", "x3", "x4", "x5" };
            var tree = compilation.SyntaxTrees[0];
            foreach (var designation in tree.GetRoot().DescendantNodes().OfType<SingleVariableDesignationSyntax>())
            {
                var model = compilation.GetSemanticModel(tree);
                var symbol = model.GetDeclaredSymbol(designation);
                Assert.Equal(SymbolKind.Local, symbol.Kind);
                Assert.Equal("int", ((LocalSymbol)symbol).Type.ToDisplayString());
            }
            foreach (var ident in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var model = compilation.GetSemanticModel(tree);
                var typeInfo = model.GetTypeInfo(ident);
                Assert.Equal("int", typeInfo.Type.ToDisplayString());
            }
        }

        [Fact]
        public void ShortDiscardInIsPattern()
        {
            // test that we forbid a short discard at the top level of an is-pattern expression
            var source =
@"class Program
{
    public static void Main()
    {
        int a = 1;
        if (a is _) { }
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (6,18): error CS0246: The type or namespace name '_' could not be found (are you missing a using directive or an assembly reference?)
                //         if (a is _) { }
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "_").WithArguments("_").WithLocation(6, 18)
                );
        }

        [Fact]
        public void Patterns2_04()
        {
            // Test that a single-element deconstruct pattern is an error if no further elements disambiguate.
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        var t = new System.ValueTuple<int>(1);
        if (t is (int x)) { }                           // error 1
        switch (t) { case (_): break; }                 // error 2
        var u = t switch { (int y) => y, _ => 2 };      // error 3
        if (t is (int z1) _) { }                        // error 4
        if (t is (Item1: int z2)) { }                   // error 5
        if (t is (int z3) { }) { }                      // error 6
        if (t is ValueTuple<int>(int z4)) { }           // ok
    }
    private static bool Check<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception($""expected: {expected}; actual: {actual}"");
        return true;
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (8,18): error CS8407: A single-element deconstruct pattern requires a type before the open parenthesis.
                //         if (t is (int x)) { }                           // error 1
                Diagnostic(ErrorCode.ERR_SingleElementPositionalPatternRequiresType, "(int x)").WithLocation(8, 18),
                // (9,27): error CS8407: A single-element deconstruct pattern requires a type before the open parenthesis.
                //         switch (t) { case (_): break; }                 // error 2
                Diagnostic(ErrorCode.ERR_SingleElementPositionalPatternRequiresType, "(_)").WithLocation(9, 27),
                // (10,28): error CS8407: A single-element deconstruct pattern requires a type before the open parenthesis.
                //         var u = t switch { (int y) => y, _ => 2 };      // error 3
                Diagnostic(ErrorCode.ERR_SingleElementPositionalPatternRequiresType, "(int y)").WithLocation(10, 28),
                // (11,18): error CS8407: A single-element deconstruct pattern requires a type before the open parenthesis.
                //         if (t is (int z1) _) { }                        // error 4
                Diagnostic(ErrorCode.ERR_SingleElementPositionalPatternRequiresType, "(int z1) _").WithLocation(11, 18),
                // (12,18): error CS8407: A single-element deconstruct pattern requires a type before the open parenthesis.
                //         if (t is (Item1: int z2)) { }                   // error 5
                Diagnostic(ErrorCode.ERR_SingleElementPositionalPatternRequiresType, "(Item1: int z2)").WithLocation(12, 18),
                // (13,18): error CS8407: A single-element deconstruct pattern requires a type before the open parenthesis.
                //         if (t is (int z3) { }) { }                      // error 6
                Diagnostic(ErrorCode.ERR_SingleElementPositionalPatternRequiresType, "(int z3) { }").WithLocation(13, 18),
                // (10,42): error CS8410: The pattern has already been handled by a previous arm of the switch expression.
                //         var u = t switch { (int y) => y, _ => 2 };      // error 3
                Diagnostic(ErrorCode.ERR_SwitchArmSubsumed, "_").WithLocation(10, 42)
                );
        }

        [Fact]
        public void Patterns2_05()
        {
            // Test parsing the var pattern
            // Test binding the var pattern
            // Test lowering the var pattern for the is-expression
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        var t = (1, 2);
        { Check(true, t is var (x, y) && x == 1 && y == 2); }
        { Check(false, t is var (x, y) && x == 1 && y == 3); }
    }
    private static void Check<T>(T expected, T actual)
    {
        if (!object.Equals(expected, actual)) throw new Exception($""Expected: '{expected}', Actual: '{actual}'"");
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"");
        }

        [Fact]
        public void Patterns2_06()
        {
            // Test that 'var' does not bind to a type
            var source =
@"
using System;
namespace N
{
    class Program
    {
        public static void Main()
        {
            var t = (1, 2);
            { Check(true, t is var (x, y) && x == 1 && y == 2); }  // error 1
            { Check(false, t is var (x, y) && x == 1 && y == 3); } // error 2
            { Check(true, t is var x); }                           // error 3
        }
        private static void Check<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual)) throw new Exception($""Expected: '{expected}', Actual: '{actual}'"");
        }
    }
    class var { }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (9,21): error CS0029: Cannot implicitly convert type '(int, int)' to 'N.var'
                //             var t = (1, 2);
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "(1, 2)").WithArguments("(int, int)", "N.var").WithLocation(9, 21),
                // (10,32): error CS8408: The syntax 'var' for a pattern is not permitted to bind to a type, but it binds to 'N.var' here.
                //             { Check(true, t is var (x, y) && x == 1 && y == 2); }  // error 1
                Diagnostic(ErrorCode.ERR_VarMayNotBindToType, "var").WithArguments("N.var").WithLocation(10, 32),
                // (10,32): error CS1061: 'var' does not contain a definition for 'Deconstruct' and no extension method 'Deconstruct' accepting a first argument of type 'var' could be found (are you missing a using directive or an assembly reference?)
                //             { Check(true, t is var (x, y) && x == 1 && y == 2); }  // error 1
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "var (x, y)").WithArguments("N.var", "Deconstruct").WithLocation(10, 32),
                // (10,32): error CS8129: No suitable Deconstruct instance or extension method was found for type 'var', with 2 out parameters and a void return type.
                //             { Check(true, t is var (x, y) && x == 1 && y == 2); }  // error 1
                Diagnostic(ErrorCode.ERR_MissingDeconstruct, "var (x, y)").WithArguments("N.var", "2").WithLocation(10, 32),
                // (11,33): error CS8408: The syntax 'var' for a pattern is not permitted to bind to a type, but it binds to 'N.var' here.
                //             { Check(false, t is var (x, y) && x == 1 && y == 3); } // error 2
                Diagnostic(ErrorCode.ERR_VarMayNotBindToType, "var").WithArguments("N.var").WithLocation(11, 33),
                // (11,33): error CS1061: 'var' does not contain a definition for 'Deconstruct' and no extension method 'Deconstruct' accepting a first argument of type 'var' could be found (are you missing a using directive or an assembly reference?)
                //             { Check(false, t is var (x, y) && x == 1 && y == 3); } // error 2
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "var (x, y)").WithArguments("N.var", "Deconstruct").WithLocation(11, 33),
                // (11,33): error CS8129: No suitable Deconstruct instance or extension method was found for type 'var', with 2 out parameters and a void return type.
                //             { Check(false, t is var (x, y) && x == 1 && y == 3); } // error 2
                Diagnostic(ErrorCode.ERR_MissingDeconstruct, "var (x, y)").WithArguments("N.var", "2").WithLocation(11, 33),
                // (12,32): error CS8408: The syntax 'var' for a pattern is not permitted to bind to a type, but it binds to 'N.var' here.
                //             { Check(true, t is var x); }                           // error 3
                Diagnostic(ErrorCode.ERR_VarMayNotBindToType, "var").WithArguments("N.var").WithLocation(12, 32)
                );
        }

        [Fact]
        public void Patterns2_10()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Console.Write(M((false, false)));
        Console.Write(M((false, true)));
        Console.Write(M((true, false)));
        Console.Write(M((true, true)));
    }
    private static int M((bool, bool) t)
    {
        switch (t)
        {
            case (false, false): return 0;
            case (false, _): return 1;
            case (_, false): return 2;
            case _: return 3;
        }
    }
}

namespace System
{
    public struct ValueTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;

        public ValueTuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"0123");
        }

        [Fact]
        public void Patterns2_11()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Console.Write(M((false, false)));
        Console.Write(M((false, true)));
        Console.Write(M((true, false)));
        Console.Write(M((true, true)));
    }
    private static int M((bool, bool) t)
    {
        switch (t)
        {
            case (false, false): return 0;
            case (false, _): return 1;
            case (_, false): return 2;
            case (true, true): return 3;
            case _: return 4;
        }
    }
}

namespace System
{
    public struct ValueTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;

        public ValueTuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (20,18): error CS8120: The switch case has already been handled by a previous case.
                //             case _: return 4;
                Diagnostic(ErrorCode.ERR_SwitchCaseSubsumed, "_").WithLocation(20, 18)
                );
        }

        [Fact]
        public void Patterns2_12()
        {
            var source =
@"
using System;
class Program
{
    public static void Main()
    {
        Console.Write(M((false, false)));
        Console.Write(M((false, true)));
        Console.Write(M((true, false)));
        Console.Write(M((true, true)));
    }
    private static int M((bool, bool) t)
    {
        return t switch {
            (false, false) => 0,
            (false, _) => 1,
            (_, false) => 2,
            _ => 3
        };
    }
}

namespace System
{
    public struct ValueTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;

        public ValueTuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"0123");
        }

        [Fact]
        public void SwitchArmSubsumed()
        {
            var source =
@"public class X
{
    public static void Main()
    {
        string s = string.Empty;
        string s2 = s switch { null => null, string t => t, ""foo"" => null };
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (6,61): error CS8410: The pattern has already been handled by a previous arm of the switch expression.
                //         string s2 = s switch { null => null, string t => t, "foo" => null };
                Diagnostic(ErrorCode.ERR_SwitchArmSubsumed, @"""foo""").WithLocation(6, 61)
                );
        }

        [Fact]
        public void LongTuples()
        {
            var source =
@"using System;

public class X
{
    public static void Main()
    {
        var t = (1, 2, 3, 4, 5, 6, 7, 8, 9);
        {
            Console.WriteLine(t is (_, _, _, _, _, _, _, _, var t9) ? t9 : 100);
        }
        switch (t)
        {
            case (_, _, _, _, _, _, _, _, var t9):
                Console.WriteLine(t9);
                break;
        }
        // PROTOTYPE(patterns2): Lowering and code gen not yet supported for switch expression
        //Console.WriteLine(t switch { (_, _, _, _, _, _, _, _, var t9) => t9 });
    }
}";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"9
9");
        }

        [Fact]
        public void TypeCheckInPropertyPattern()
        {
            var source =
@"using System;

class Program2
{
    public static void Main()
    {
        object o = new Frog(1, 2);
        if (o is Frog(1, 2))
        {
            Console.Write(1);
        }
        if (o is Frog { A: 1, B: 2 })
        {
            Console.Write(2);
        }
        if (o is Frog(1, 2) { A: 1, B: 2, C: 3 })
        {
            Console.Write(3);
        }

        if (o is Frog(9, 2) { A: 1, B: 2, C: 3 }) {} else
        {
            Console.Write(4);
        }
        if (o is Frog(1, 9) { A: 1, B: 2, C: 3 }) {} else
        {
            Console.Write(5);
        }
        if (o is Frog(1, 2) { A: 9, B: 2, C: 3 }) {} else
        {
            Console.Write(6);
        }
        if (o is Frog(1, 2) { A: 1, B: 9, C: 3 }) {} else
        {
            Console.Write(7);
        }
        if (o is Frog(1, 2) { A: 1, B: 2, C: 9 }) {} else
        {
            Console.Write(8);
        }
    }
}

class Frog
{
    public object A, B;
    public object C => (int)A + (int)B;
    public Frog(object A, object B) => (this.A, this.B) = (A, B);
    public void Deconstruct(out object A, out object B) => (A, B) = (this.A, this.B);
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"12345678");
        }

        [Fact]
        public void OvereagerSubsumption()
        {
            var source =
@"using System;

class Program2
{
    public static int Main() => 0;
    public static void M(object o)
    {
        switch (o)
        {
            case (1, 2):
                break;
            case string s:
                break;
        }
    }
}
";
            var compilation = CreatePatternCompilation(source);
            // Two errors below instead of one due to https://github.com/dotnet/roslyn/issues/25533
            compilation.VerifyDiagnostics(
                // (10,18): error CS1061: 'object' does not contain a definition for 'Deconstruct' and no extension method 'Deconstruct' accepting a first argument of type 'object' could be found (are you missing a using directive or an assembly reference?)
                //             case (1, 2):
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "(1, 2)").WithArguments("object", "Deconstruct").WithLocation(10, 18),
                // (10,18): error CS8129: No suitable Deconstruct instance or extension method was found for type 'object', with 2 out parameters and a void return type.
                //             case (1, 2):
                Diagnostic(ErrorCode.ERR_MissingDeconstruct, "(1, 2)").WithArguments("object", "2").WithLocation(10, 18)
                );
        }

        [Fact]
        public void UnderscoreDeclaredAndDiscardPattern_01()
        {
            var source =
@"class Program0
{
    static int Main() => 0;
    private const int _ = 1;
    bool M1(object o) => o is _;                             // error: cannot use _ as a constant
    bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
}
class Program1
{
    class _ {}
    bool M1(object o) => o is _;                             // error: is type named _
    bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (11,31): error CS8413: The name '_' refers to the type 'Program1._', not the discard pattern. Use '@_' for the type, or 'var _' to discard.
                //     bool M1(object o) => o is _;                             // error: is type named _
                Diagnostic(ErrorCode.WRN_IsTypeNamedUnderscore, "_").WithArguments("Program1._").WithLocation(11, 31),
                // (5,31): error CS8412: A constant named '_' cannot be used as a pattern.
                //     bool M1(object o) => o is _;                             // error: cannot use _ as a constant
                Diagnostic(ErrorCode.ERR_ConstantPatternNamedUnderscore, "_").WithLocation(5, 31),
                // (12,48): error CS8411: The discard pattern '_' cannot be used where 'Program1._' is in scope.
                //     bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
                Diagnostic(ErrorCode.ERR_UnderscoreDeclaredAndDiscardPattern, "_").WithArguments("Program1._").WithLocation(12, 48),
                // (6,48): error CS8411: The discard pattern '_' cannot be used where 'Program0._' is in scope.
                //     bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
                Diagnostic(ErrorCode.ERR_UnderscoreDeclaredAndDiscardPattern, "_").WithArguments("Program0._").WithLocation(6, 48)
                );
        }

        [Fact]
        public void UnderscoreDeclaredAndDiscardPattern_02()
        {
            var source =
@"class Program0
{
    static int Main() => 0;
    private const int _ = 1;
}
class Program1 : Program0
{
    bool M2(object o) => o switch { 1 => true, _ => false }; // ok, private member not inherited
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
        }

        [Fact]
        public void UnderscoreDeclaredAndDiscardPattern_03()
        {
            var source =
@"class Program0
{
    static int Main() => 0;
    protected const int _ = 1;
}
class Program1 : Program0
{
    bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
}
class Program2
{
    bool _(object q) => true;
    bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (8,48): error CS8411: The discard pattern '_' cannot be used where 'Program0._' is in scope.
                //     bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
                Diagnostic(ErrorCode.ERR_UnderscoreDeclaredAndDiscardPattern, "_").WithArguments("Program0._").WithLocation(8, 48),
                // (13,48): error CS8411: The discard pattern '_' cannot be used where 'Program2._(object)' is in scope.
                //     bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
                Diagnostic(ErrorCode.ERR_UnderscoreDeclaredAndDiscardPattern, "_").WithArguments("Program2._(object)").WithLocation(13, 48)
                );
        }

        [Fact]
        public void UnderscoreDeclaredAndDiscardPattern_04()
        {
            var source =
@"using _ = System.Int32;
class Program
{
    static int Main() => 0;
    bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (5,48): error CS8411: The discard pattern '_' cannot be used where '_' is in scope.
                //     bool M2(object o) => o switch { 1 => true, _ => false }; // error: _ in scope
                Diagnostic(ErrorCode.ERR_UnderscoreDeclaredAndDiscardPattern, "_").WithArguments("_").WithLocation(5, 48)
                );
        }

        [Fact]
        public void EscapingUnderscoreDeclaredAndDiscardPattern_04()
        {
            var source =
@"class Program0
{
    static int Main() => 0;
    private const int _ = 2;
    bool M1(object o) => o is @_;
    int M2(object o) => o switch { 1 => 1, @_ => 2, var _ => 3 };
}
class Program1
{
    class _ {}
    bool M1(object o) => o is @_;
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
        }

        [Fact]
        public void ErroneousSwitchArmDefiniteAssignment()
        {
            // When a switch expression arm is erroneous, ensure that the expression is treated as unreachable (e.g. for definite assignment purposes).
            var source =
@"class Program2
{
    public static int Main() => 0;
    public static void M(string s)
    {
        int i;
        int j = s switch { ""frog"" => 1, 0 => i, _ => 2 };
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,41): error CS0029: Cannot implicitly convert type 'int' to 'string'
                //         int j = s switch { "frog" => 1, 0 => i, _ => 2 };
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "0").WithArguments("int", "string").WithLocation(7, 41)
                );
        }

        [Fact, WorkItem(9154, "https://github.com/dotnet/roslyn/issues/9154")]
        public void ErroneousIsPatternDefiniteAssignment()
        {
            var source =
@"class Program2
{
    public static int Main() => 0;
    void Dummy(object o) {}
    void Test5()
    {
        Dummy((System.Func<object, object, bool>) ((o1, o2) => o1 is int x5 && 
                                                               o2 is int x5 && 
                                                               x5 > 0));
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (8,74): error CS0128: A local variable or function named 'x5' is already defined in this scope
                //                                                                o2 is int x5 && 
                Diagnostic(ErrorCode.ERR_LocalDuplicate, "x5").WithArguments("x5").WithLocation(8, 74)
                );
        }

        [Fact, WorkItem(25591, "https://github.com/dotnet/roslyn/issues/25591")]
        public void TupleSubsumptionError()
        {
            var source =
@"class Program2
{
    public static void Main()
    {
        M(new Fox());
        M(new Cat());
        M(new Program2());
    }
    static void M(object o)
    {
        switch ((o, 0))
        {
            case (Fox fox, _):
                System.Console.Write(""Fox "");
                break;
            case (Cat cat, _):
                System.Console.Write(""Cat"");
                break;
        }
    }
}
class Fox {}
class Cat {}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"Fox Cat");
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns01()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (a: 1, b: 2)
        {
            case (c: 2, d: 3): // error: c and d not defined
                break;
        }
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,19): error CS8416: The name 'c' does not identify tuple element 0.
                //             case (c: 2, d: 3): // error: c and d not defined
                Diagnostic(ErrorCode.ERR_TupleElementNameMismatch, "c").WithArguments("c", "0").WithLocation(7, 19),
                // (7,25): error CS8416: The name 'd' does not identify tuple element 1.
                //             case (c: 2, d: 3): // error: c and d not defined
                Diagnostic(ErrorCode.ERR_TupleElementNameMismatch, "d").WithArguments("d", "1").WithLocation(7, 25)
                );
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns02()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (a: 1, b: 2)
        {
            case (a: 2, a: 3):
                break;
        }
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,25): error CS8416: The name 'a' does not identify tuple element 1.
                //             case (a: 2, a: 3):
                Diagnostic(ErrorCode.ERR_TupleElementNameMismatch, "a").WithArguments("a", "1").WithLocation(7, 25)
                );
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns03()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (a: 1, b: 2)
        {
            case (a: 2, Item2: 3):
                System.Console.WriteLine(666);
                break;
            case (a: 1, Item2: 2):
                System.Console.WriteLine(111);
                break;
        }
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"111");
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns04()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (new T(a: 1, b: 2))
        {
            case (c: 2, d: 3):
                break;
        }
    }
}
class T
{
    public int A;
    public int B;
    public T(int a, int b) => (A, B) = (a, b);
    public void Deconstruct(out int a, out int b) => (a, b) = (A, B);
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,19): error CS8417: The name 'c' does not match the corresponding 'Deconstruct' parameter 'a'.
                //             case (c: 2, d: 3):
                Diagnostic(ErrorCode.ERR_DeconstructParameterNameMismatch, "c").WithArguments("c", "a").WithLocation(7, 19),
                // (7,25): error CS8417: The name 'd' does not match the corresponding 'Deconstruct' parameter 'b'.
                //             case (c: 2, d: 3):
                Diagnostic(ErrorCode.ERR_DeconstructParameterNameMismatch, "d").WithArguments("d", "b").WithLocation(7, 25)
                );
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns05()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (new T(a: 1, b: 2))
        {
            case (c: 2, d: 3):
                break;
        }
    }
}
class T
{
    public int A;
    public int B;
    public T(int a, int b) => (A, B) = (a, b);
}
static class Extensions
{
    public static void Deconstruct(this T t, out int a, out int b) => (a, b) = (t.A, t.B);
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,19): error CS8417: The name 'c' does not match the corresponding 'Deconstruct' parameter 'a'.
                //             case (c: 2, d: 3):
                Diagnostic(ErrorCode.ERR_DeconstructParameterNameMismatch, "c").WithArguments("c", "a").WithLocation(7, 19),
                // (7,25): error CS8417: The name 'd' does not match the corresponding 'Deconstruct' parameter 'b'.
                //             case (c: 2, d: 3):
                Diagnostic(ErrorCode.ERR_DeconstructParameterNameMismatch, "d").WithArguments("d", "b").WithLocation(7, 25)
                );
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns06()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (new T(a: 1, b: 2))
        {
            case (a: 2, a: 3):
                break;
        }
    }
}
class T
{
    public int A;
    public int B;
    public T(int a, int b) => (A, B) = (a, b);
    public void Deconstruct(out int a, out int b) => (a, b) = (A, B);
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,25): error CS8417: The name 'a' does not match the corresponding 'Deconstruct' parameter 'b'.
                //             case (a: 2, a: 3):
                Diagnostic(ErrorCode.ERR_DeconstructParameterNameMismatch, "a").WithArguments("a", "b").WithLocation(7, 25)
                );
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns07()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (new T(a: 1, b: 2))
        {
            case (a: 2, a: 3):
                break;
        }
    }
}
class T
{
    public int A;
    public int B;
    public T(int a, int b) => (A, B) = (a, b);
}
static class Extensions
{
    public static void Deconstruct(this T t, out int a, out int b) => (a, b) = (t.A, t.B);
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                // (7,25): error CS8417: The name 'a' does not match the corresponding 'Deconstruct' parameter 'b'.
                //             case (a: 2, a: 3):
                Diagnostic(ErrorCode.ERR_DeconstructParameterNameMismatch, "a").WithArguments("a", "b").WithLocation(7, 25)
                );
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns08()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (new T(a: 1, b: 2))
        {
            case (a: 2, b: 3):
                System.Console.WriteLine(666);
                break;
            case (a: 1, b: 2):
                System.Console.WriteLine(111);
                break;
        }
    }
}
class T
{
    public int A;
    public int B;
    public T(int a, int b) => (A, B) = (a, b);
    public void Deconstruct(out int a, out int b) => (a, b) = (A, B);
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"111");
        }

        [Fact, WorkItem(25934, "https://github.com/dotnet/roslyn/issues/25934")]
        public void NamesInPositionalPatterns09()
        {
            var source =
@"class Program
{
    static void Main()
    {
        switch (new T(a: 1, b: 2))
        {
            case (a: 2, b: 3):
                System.Console.WriteLine(666);
                break;
            case (a: 1, b: 2):
                System.Console.WriteLine(111);
                break;
        }
    }
}
class T
{
    public int A;
    public int B;
    public T(int a, int b) => (A, B) = (a, b);
}
static class Extensions
{
    public static void Deconstruct(this T t, out int a, out int b) => (a, b) = (t.A, t.B);
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics(
                );
            var comp = CompileAndVerify(compilation, expectedOutput: @"111");
        }

        // PROTOTYPE(patterns2): Need to have tests that exercise:
        // PROTOTYPE(patterns2): Building the decision tree for the var-pattern
        // PROTOTYPE(patterns2): Definite assignment for the var-pattern
        // PROTOTYPE(patterns2): Variable finder for the var-pattern
        // PROTOTYPE(patterns2): Scope binder contains an approprate scope for the var-pattern
        // PROTOTYPE(patterns2): Lazily binding types for variables declared in the var-pattern
        // PROTOTYPE(patterns2): Error when there is a type or constant named var in scope where the var pattern is used
    }
}
