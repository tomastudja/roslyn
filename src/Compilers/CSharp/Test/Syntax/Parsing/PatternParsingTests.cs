﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
using Xunit;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class PatternParsingTexts : CSharpTestBase
    {
        [Fact]
        public void CasePatternVersusFeatureFlag()
        {
            var test = @"
class C 
{
    public static void Main(string[] args)
    {
        switch ((int) args[0][0])
        {
            case 1:
            case 2 when args.Length == 2:
            case 1<<2:
            case string s:
            default:
                break;
        }
        bool b = args[0] is string s;
    }
}
";
            CreateCompilationWithMscorlib(test, parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.CSharp6)).VerifyDiagnostics(
                // (9,13): error CS8059: Feature 'pattern matching' is not available in C# 6.  Please use language version 7 or greater.
                //             case 2 when args.Length == 2:
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "case 2 when args.Length == 2:").WithArguments("pattern matching", "7").WithLocation(9, 13),
                // (11,13): error CS8059: Feature 'pattern matching' is not available in C# 6.  Please use language version 7 or greater.
                //             case string s:
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "case string s:").WithArguments("pattern matching", "7").WithLocation(11, 13),
                // (15,18): error CS8059: Feature 'pattern matching' is not available in C# 6.  Please use language version 7 or greater.
                //         bool b = args[0] is string s;
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "args[0] is string s").WithArguments("pattern matching", "7").WithLocation(15, 18)
            );
        }

        [Fact]
        public void ThrowExpression_Good()
        {
            var test = @"using System;
class C
{
    public static void Sample(bool b, string s)
    {
        void NeverReturnsFunction() => throw new NullReferenceException();
        int x = b ? throw new NullReferenceException() : 1;
        x = b ? 2 : throw new NullReferenceException();
        s = s ?? throw new NullReferenceException();
        NeverReturnsFunction();
        throw new NullReferenceException() ?? throw new NullReferenceException() ?? throw null;
    }
    public static void NeverReturns() => throw new NullReferenceException();
}";
            CreateCompilationWithMscorlib(test).VerifyDiagnostics();
            CreateCompilationWithMscorlib(test, parseOptions: TestOptions.Regular6).VerifyDiagnostics(
                // (6,14): error CS8059: Feature 'local functions' is not available in C# 6.  Please use language version 7 or greater.
                //         void NeverReturnsFunction() => throw new NullReferenceException();
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "NeverReturnsFunction").WithArguments("local functions", "7").WithLocation(6, 14),
                // (6,40): error CS8059: Feature 'throw expression' is not available in C# 6.  Please use language version 7 or greater.
                //         void NeverReturnsFunction() => throw new NullReferenceException();
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "throw new NullReferenceException()").WithArguments("throw expression", "7").WithLocation(6, 40),
                // (7,21): error CS8059: Feature 'throw expression' is not available in C# 6.  Please use language version 7 or greater.
                //         int x = b ? throw new NullReferenceException() : 1;
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "throw new NullReferenceException()").WithArguments("throw expression", "7").WithLocation(7, 21),
                // (8,21): error CS8059: Feature 'throw expression' is not available in C# 6.  Please use language version 7 or greater.
                //         x = b ? 2 : throw new NullReferenceException();
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "throw new NullReferenceException()").WithArguments("throw expression", "7").WithLocation(8, 21),
                // (9,18): error CS8059: Feature 'throw expression' is not available in C# 6.  Please use language version 7 or greater.
                //         s = s ?? throw new NullReferenceException();
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "throw new NullReferenceException()").WithArguments("throw expression", "7").WithLocation(9, 18),
                // (11,47): error CS8059: Feature 'throw expression' is not available in C# 6.  Please use language version 7 or greater.
                //         throw new NullReferenceException() ?? throw new NullReferenceException() ?? throw null;
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "throw new NullReferenceException() ?? throw null").WithArguments("throw expression", "7").WithLocation(11, 47),
                // (11,85): error CS8059: Feature 'throw expression' is not available in C# 6.  Please use language version 7 or greater.
                //         throw new NullReferenceException() ?? throw new NullReferenceException() ?? throw null;
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "throw null").WithArguments("throw expression", "7").WithLocation(11, 85),
                // (13,42): error CS8059: Feature 'throw expression' is not available in C# 6.  Please use language version 7 or greater.
                //     public static void NeverReturns() => throw new NullReferenceException();
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "throw new NullReferenceException()").WithArguments("throw expression", "7").WithLocation(13, 42)
                );
        }

        [Fact]
        public void ThrowExpression_Bad()
        {
            var test = @"using System;
class C
{
    public static void Sample(bool b, string s)
    {
        // throw expression at wrong precedence
        s = s + throw new NullReferenceException();
        if (b || throw new NullReferenceException()) { }

        // throw expression where not permitted
        var z = from x in throw new NullReferenceException() select x;
        M(throw new NullReferenceException());
        throw throw null;
        (int, int) w = (1, throw null);
        return throw null;
    }
    static void M(string s) {}
}";
            CreateCompilationWithMscorlib(test).VerifyDiagnostics(
                // (7,17): error CS1525: Invalid expression term 'throw'
                //         s = s + throw new NullReferenceException();
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "throw new NullReferenceException()").WithArguments("throw").WithLocation(7, 17),
                // (8,18): error CS1525: Invalid expression term 'throw'
                //         if (b || throw new NullReferenceException()) { }
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "throw new NullReferenceException()").WithArguments("throw").WithLocation(8, 18),
                // (11,27): error CS8115: A throw expression is not allowed in this context.
                //         var z = from x in throw new NullReferenceException() select x;
                Diagnostic(ErrorCode.ERR_ThrowMisplaced, "throw").WithLocation(11, 27),
                // (12,11): error CS8115: A throw expression is not allowed in this context.
                //         M(throw new NullReferenceException());
                Diagnostic(ErrorCode.ERR_ThrowMisplaced, "throw").WithLocation(12, 11),
                // (13,15): error CS8115: A throw expression is not allowed in this context.
                //         throw throw null;
                Diagnostic(ErrorCode.ERR_ThrowMisplaced, "throw").WithLocation(13, 15),
                // (14,9): error CS0518: Predefined type 'System.ValueTuple`2' is not defined or imported
                //         (int, int) w = (1, throw null);
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "(int, int)").WithArguments("System.ValueTuple`2").WithLocation(14, 9),
                // (14,28): error CS8115: A throw expression is not allowed in this context.
                //         (int, int) w = (1, throw null);
                Diagnostic(ErrorCode.ERR_ThrowMisplaced, "throw").WithLocation(14, 28),
                // (14,24): error CS0518: Predefined type 'System.ValueTuple`2' is not defined or imported
                //         (int, int) w = (1, throw null);
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "(1, throw null)").WithArguments("System.ValueTuple`2").WithLocation(14, 24),
                // (15,16): error CS8115: A throw expression is not allowed in this context.
                //         return throw null;
                Diagnostic(ErrorCode.ERR_ThrowMisplaced, "throw").WithLocation(15, 16),
                // (14,9): warning CS0162: Unreachable code detected
                //         (int, int) w = (1, throw null);
                Diagnostic(ErrorCode.WRN_UnreachableCode, "(").WithLocation(14, 9)
                );
        }
    }
}
