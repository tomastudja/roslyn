﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class Utf8StringsLiteralsTests : CompilingTestBase
    {
        private static string HelpersSource => @"
class Helpers
{
    public static void Print(byte[] span)
    {
        System.Console.Write(""{"");
        foreach (var item in span)
        {
            System.Console.Write("" 0x{0:X}"", item);
        }
        System.Console.WriteLine("" }"");
    }

    public static void Print(Span<byte> span)
    {
        System.Console.Write(""{"");
        foreach (var item in span)
        {
            System.Console.Write("" 0x{0:X}"", item);
        }
        System.Console.WriteLine("" }"");
    }

    public static void Print(ReadOnlySpan<byte> span)
    {
        System.Console.Write(""{"");
        foreach (var item in span)
        {
            System.Console.Write("" 0x{0:X}"", item);
        }
        System.Console.WriteLine("" }"");
    }
}
";

        [ConditionalFact(typeof(CoreClrOnly))]
        public void ImplicitConversions_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
    }

    static byte[] Test1() => ""hello"";
    static Span<byte> Test2() => ""dog"";
    static ReadOnlySpan<byte> Test3() => ""cat"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (9,30): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //     static byte[] Test1() => "hello";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "byte[]").WithLocation(9, 30),
                // (10,34): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte>'
                //     static Span<byte> Test2() => "dog";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "System.Span<byte>").WithLocation(10, 34),
                // (11,42): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte>'
                //     static ReadOnlySpan<byte> Test3() => "cat";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(11, 42)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void ImplicitConversions_TupleLiteral_01()
        {
            var source = @"

class C
{
    static void Main()
    {
        (byte[] b, (byte[] d, string e) c) a = (""hello"", (""dog"", ""cat""));
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (7,49): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         (byte[] b, (byte[] d, string e) c) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "byte[]").WithLocation(7, 49),
                // (7,59): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         (byte[] b, (byte[] d, string e) c) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "byte[]").WithLocation(7, 59)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void ImplicitConversions_Deconstruction_01()
        {
            var source = @"
class C
{
    static void Main()
    {
        (byte[] a, (byte[] b, string c)) = (""hello"", (""dog"", ""cat""));
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (6,45): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         (byte[] a, (byte[] b, string c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "byte[]").WithLocation(6, 45),
                // (6,55): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         (byte[] a, (byte[] b, string c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "byte[]").WithLocation(6, 55)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void ExplicitConversions_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        var array = (byte[])""hello"";
        var span = (Span<byte>)""dog"";
        var readonlySpan = (ReadOnlySpan<byte>)""cat"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (7,21): error CS0030: Cannot convert type 'string' to 'byte[]'
                //         var array = (byte[])"hello";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(byte[])""hello""").WithArguments("string", "byte[]").WithLocation(7, 21),
                // (8,20): error CS0030: Cannot convert type 'string' to 'System.Span<byte>'
                //         var span = (Span<byte>)"dog";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(Span<byte>)""dog""").WithArguments("string", "System.Span<byte>").WithLocation(8, 20),
                // (9,28): error CS0030: Cannot convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         var readonlySpan = (ReadOnlySpan<byte>)"cat";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(ReadOnlySpan<byte>)""cat""").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(9, 28)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void ExplicitConversions_TupleLiteral_01()
        {
            var source = @"
class C
{
    static void Main()
    {
        var a = ((byte[] b, (byte[] d, string e) c))(""hello"", (""dog"", ""cat""));
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (6,54): error CS0030: Cannot convert type 'string' to 'byte[]'
                //         var a = ((byte[] b, (byte[] d, string e) c))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""hello""").WithArguments("string", "byte[]").WithLocation(6, 54),
                // (6,64): error CS0030: Cannot convert type 'string' to 'byte[]'
                //         var a = ((byte[] b, (byte[] d, string e) c))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""dog""").WithArguments("string", "byte[]").WithLocation(6, 64)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void InvalidContent_02()
        {
            var source = @"
class C
{
    static void Main()
    {
        _ = ""hello \uD801\uD802""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
            comp.VerifyEmitDiagnostics(
                // (6,13): error CS9100: The input string cannot be converted into the equivalent UTF8 byte representation. Unable to translate Unicode character \\uD801 at index 6 to specified code page.
                //         _ = "hello \uD801\uD802"u8;
                Diagnostic(ErrorCode.ERR_CannotBeConvertedToUTF8, @"""hello \uD801\uD802""u8").WithArguments(@"Unable to translate Unicode character \\uD801 at index 6 to specified code page.").WithLocation(6, 13)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void NoBehaviorChangeForConversionFromNullLiteral_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        System.Console.WriteLine(Test1() is null ? -1 : 0);
        System.Console.WriteLine(Test2().Length);
        System.Console.WriteLine(Test3().Length);
    }

    static byte[] Test1() => null;
    static Span<byte> Test2() => null;
    static ReadOnlySpan<byte> Test3() => null;
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            var verifier = CompileAndVerify(comp, expectedOutput: @"
-1
0
0
", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test1()", @"
{
  // Code size        2 (0x2)
  .maxstack  1
  IL_0000:  ldnull
  IL_0001:  ret
}
");

            verifier.VerifyIL("C.Test2()", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldnull
  IL_0001:  call       ""System.Span<byte> System.Span<byte>.op_Implicit(byte[])""
  IL_0006:  ret
}
");

            verifier.VerifyIL("C.Test3()", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldnull
  IL_0001:  call       ""System.ReadOnlySpan<byte> System.ReadOnlySpan<byte>.op_Implicit(byte[])""
  IL_0006:  ret
}
");

            comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularNext);

            CompileAndVerify(comp, expectedOutput: @"
-1
0
0
", verify: Verification.Fails).VerifyDiagnostics();

            comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);

            CompileAndVerify(comp, expectedOutput: @"
-1
0
0
", verify: Verification.Fails).VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void NoConversionFromType_01()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        string value = ""s"";
        byte[] array = value;
        Span<byte> span = value;
        ReadOnlySpan<byte> readonlySpan = value;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyEmitDiagnostics(
                // (9,24): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         byte[] array = value;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "value").WithArguments("string", "byte[]").WithLocation(9, 24),
                // (10,27): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte>'
                //         Span<byte> span = value;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "value").WithArguments("string", "System.Span<byte>").WithLocation(10, 27),
                // (11,43): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         ReadOnlySpan<byte> readonlySpan = value;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "value").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(11, 43)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void NoConversionFromType_02()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        string value = ""s"";
        var array = (byte[])value;
        var span = (Span<byte>)value;
        var readonlySpan = (ReadOnlySpan<byte>)value;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyEmitDiagnostics(
                // (9,21): error CS0030: Cannot convert type 'string' to 'byte[]'
                //         var array = (byte[])value;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(byte[])value").WithArguments("string", "byte[]").WithLocation(9, 21),
                // (10,20): error CS0030: Cannot convert type 'string' to 'System.Span<byte>'
                //         var span = (Span<byte>)value;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(Span<byte>)value").WithArguments("string", "System.Span<byte>").WithLocation(10, 20),
                // (11,28): error CS0030: Cannot convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         var readonlySpan = (ReadOnlySpan<byte>)value;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(ReadOnlySpan<byte>)value").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(11, 28)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void InvalidTargetType_01()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        const string s = ""s"";

        byte[,] array1 = s;
        Span<byte[]> span1 = s;
        ReadOnlySpan<byte[]> readonlySpan1 = s;

        int[] array2 = s;
        Span<int> span2 = s;
        ReadOnlySpan<int> readonlySpan2 = s;

        char[] array3 = s;
        Span<char> span3 = s;

        string[] array4 = s;
        Span<string> span4 = s;
        ReadOnlySpan<string> readonlySpan4 = s;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyEmitDiagnostics(
                // (10,26): error CS0029: Cannot implicitly convert type 'string' to 'byte[*,*]'
                //         byte[,] array1 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "byte[*,*]").WithLocation(10, 26),
                // (11,30): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte[]>'
                //         Span<byte[]> span1 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.Span<byte[]>").WithLocation(11, 30),
                // (12,46): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte[]>'
                //         ReadOnlySpan<byte[]> readonlySpan1 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.ReadOnlySpan<byte[]>").WithLocation(12, 46),
                // (14,24): error CS0029: Cannot implicitly convert type 'string' to 'int[]'
                //         int[] array2 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "int[]").WithLocation(14, 24),
                // (15,27): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<int>'
                //         Span<int> span2 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.Span<int>").WithLocation(15, 27),
                // (16,43): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<int>'
                //         ReadOnlySpan<int> readonlySpan2 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.ReadOnlySpan<int>").WithLocation(16, 43),
                // (18,25): error CS0029: Cannot implicitly convert type 'string' to 'char[]'
                //         char[] array3 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "char[]").WithLocation(18, 25),
                // (19,28): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<char>'
                //         Span<char> span3 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.Span<char>").WithLocation(19, 28),
                // (21,27): error CS0029: Cannot implicitly convert type 'string' to 'string[]'
                //         string[] array4 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "string[]").WithLocation(21, 27),
                // (22,30): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<string>'
                //         Span<string> span4 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.Span<string>").WithLocation(22, 30),
                // (23,46): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<string>'
                //         ReadOnlySpan<string> readonlySpan4 = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.ReadOnlySpan<string>").WithLocation(23, 46)
                );
        }

        [Fact]
        public void NotSZArray_01()
        {
            var il = @"
.class public auto ansi beforefieldinit Test
    extends System.Object
{
    .method public hidebysig specialname static 
        void set_P (
            uint8[1] 'value'
        ) cil managed 
    {
        .maxstack 8
        IL_0000: nop
        IL_0001: ret
    }

    .property uint8[1] P()
    {
        .set void Test::set_P(uint8[1])
    }
}
";

            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used

class C
{
    static void Main()
    {
        Test.P = ""s"";
    }
}
";

            var comp = CreateCompilationWithIL(source, il);
            var p = comp.GetMember<PropertySymbol>("Test.P");
            var type = (ArrayTypeSymbol)p.Type;
            Assert.Equal("System.Byte[*]", type.ToTestDisplayString());
            Assert.Equal(1, type.Rank);
            Assert.False(type.IsSZArray);
            comp.VerifyEmitDiagnostics(
                // (8,18): error CS0029: Cannot implicitly convert type 'string' to 'byte[*]'
                //         Test.P = "s";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""s""").WithArguments("string", "byte[*]").WithLocation(8, 18)
                );
        }

        [Fact]
        public void NotARefStruct_01()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        Span<byte> span = ""dog"";
    }
}

namespace System
{
    public readonly struct Span<T>
    {
        public Span(T[] arr)
        {
        }
    }
}
";

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,27): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte>'
                //         Span<byte> span = "dog";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "System.Span<byte>").WithLocation(8, 27)
                );
        }

        [Fact]
        public void NotARefStruct_02()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        ReadOnlySpan<byte> readonlySpan = ""cat"";
    }
}


namespace System
{
    public readonly struct ReadOnlySpan<T>
    {
        public ReadOnlySpan(T[] arr)
        {
        }
    }
}
";

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,43): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         ReadOnlySpan<byte> readonlySpan = "cat";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(8, 43)
                );
        }

        [Fact]
        public void NotARefStruct_03()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        Span<byte> span = ""dog"";
    }
}

namespace System
{
    public class Span<T>
    {
        public Span(T[] arr)
        {
        }
    }
}
";

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,27): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte>'
                //         Span<byte> span = "dog";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "System.Span<byte>").WithLocation(8, 27)
                );
        }

        [Fact]
        public void NotARefStruct_04()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        ReadOnlySpan<byte> readonlySpan = ""cat"";
    }
}


namespace System
{
    public class ReadOnlySpan<T>
    {
        public ReadOnlySpan(T[] arr)
        {
        }
    }
}
";

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,43): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         ReadOnlySpan<byte> readonlySpan = "cat";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(8, 43)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        C1 x = ""hello"";
        C2 y = ""dog"";
        C3 z = ""cat"";
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,16): error CS0029: Cannot implicitly convert type 'string' to 'C1'
                //         C1 x = "hello";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(7, 16),
                // (8,16): error CS0029: Cannot implicitly convert type 'string' to 'C2'
                //         C2 y = "dog";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(8, 16),
                // (9,16): error CS0029: Cannot implicitly convert type 'string' to 'C3'
                //         C3 z = "cat";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(9, 16)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_TupleLiteral_01()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        (C1, (C2, C3)) a = (""hello"", (""dog"", ""cat""));
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (8,29): error CS0029: Cannot implicitly convert type 'string' to 'C1'
                //         (C1, (C2, C3)) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(8, 29),
                // (8,39): error CS0029: Cannot implicitly convert type 'string' to 'C2'
                //         (C1, (C2, C3)) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(8, 39),
                // (8,46): error CS0029: Cannot implicitly convert type 'string' to 'C3'
                //         (C1, (C2, C3)) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(8, 46)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_Deconstruction_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        (C1 a, (C2 b, C3 c)) = (""hello"", (""dog"", ""cat""));
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,33): error CS0029: Cannot implicitly convert type 'string' to 'C1'
                //         (C1 a, (C2 b, C3 c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(7, 33),
                // (7,43): error CS0029: Cannot implicitly convert type 'string' to 'C2'
                //         (C1 a, (C2 b, C3 c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(7, 43),
                // (7,50): error CS0029: Cannot implicitly convert type 'string' to 'C3'
                //         (C1 a, (C2 b, C3 c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(7, 50)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_02()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        var x = (C1)""hello"";
        var y = (C2)""dog"";
        var z = (C3)""cat"";
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        Helpers.Print(x);
        return new C1();
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        Helpers.Print(x);
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        Helpers.Print(x);
        return new C3();
    }
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (8,17): error CS0030: Cannot convert type 'string' to 'C1'
                //         var x = (C1)"hello";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C1)""hello""").WithArguments("string", "C1").WithLocation(8, 17),
                // (9,17): error CS0030: Cannot convert type 'string' to 'C2'
                //         var y = (C2)"dog";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C2)""dog""").WithArguments("string", "C2").WithLocation(9, 17),
                // (10,17): error CS0030: Cannot convert type 'string' to 'C3'
                //         var z = (C3)"cat";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C3)""cat""").WithArguments("string", "C3").WithLocation(10, 17)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_TupleLiteral_02()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        _ = ((C1, (C2, C3)))(""hello"", (""dog"", ""cat""));
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        Helpers.Print(x);
        return new C1();
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        Helpers.Print(x);
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        Helpers.Print(x);
        return new C3();
    }
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (8,30): error CS0030: Cannot convert type 'string' to 'C1'
                //         _ = ((C1, (C2, C3)))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(8, 30),
                // (8,40): error CS0030: Cannot convert type 'string' to 'C2'
                //         _ = ((C1, (C2, C3)))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(8, 40),
                // (8,47): error CS0030: Cannot convert type 'string' to 'C3'
                //         _ = ((C1, (C2, C3)))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(8, 47)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        var x = (C1)""hello"";
        var y = (C2)""dog"";
        var z = (C3)""cat"";
    }
}

class C1
{
    public static explicit operator C1(byte[] x)
    {
        Helpers.Print(x);
        return new C1();
    }
}

class C2
{
    public static explicit operator C2(Span<byte> x)
    {
        Helpers.Print(x);
        return new C2();
    }
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x)
    {
        Helpers.Print(x);
        return new C3();
    }
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (8,17): error CS0030: Cannot convert type 'string' to 'C1'
                //         var x = (C1)"hello";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C1)""hello""").WithArguments("string", "C1").WithLocation(8, 17),
                // (9,17): error CS0030: Cannot convert type 'string' to 'C2'
                //         var y = (C2)"dog";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C2)""dog""").WithArguments("string", "C2").WithLocation(9, 17),
                // (10,17): error CS0030: Cannot convert type 'string' to 'C3'
                //         var z = (C3)"cat";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C3)""cat""").WithArguments("string", "C3").WithLocation(10, 17)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_TupleLiteral_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        _ = ((C1, (C2, C3)))(""hello"", (""dog"", ""cat""));
    }
}

class C1
{
    public static explicit operator C1(byte[] x)
    {
        Helpers.Print(x);
        return new C1();
    }
}

class C2
{
    public static explicit operator C2(Span<byte> x)
    {
        Helpers.Print(x);
        return new C2();
    }
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x)
    {
        Helpers.Print(x);
        return new C3();
    }
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (8,30): error CS0030: Cannot convert type 'string' to 'C1'
                //         _ = ((C1, (C2, C3)))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(8, 30),
                // (8,40): error CS0030: Cannot convert type 'string' to 'C2'
                //         _ = ((C1, (C2, C3)))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(8, 40),
                // (8,47): error CS0030: Cannot convert type 'string' to 'C3'
                //         _ = ((C1, (C2, C3)))("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(8, 47)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_02()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        C1 x = ""hello"";
        C2 y = ""dog"";
        C3 z = ""cat"";
    }
}

class C1
{
    public static explicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static explicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,16): error CS0029: Cannot implicitly convert type 'string' to 'C1'
                //         C1 x = "hello";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(7, 16),
                // (8,16): error CS0029: Cannot implicitly convert type 'string' to 'C2'
                //         C2 y = "dog";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(8, 16),
                // (9,16): error CS0029: Cannot implicitly convert type 'string' to 'C3'
                //         C3 z = "cat";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(9, 16)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_TupleLiteral_02()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        (C1, (C2, C3)) a = (""hello"", (""dog"", ""cat""));
    }
}

class C1
{
    public static explicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static explicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (8,29): error CS0029: Cannot implicitly convert type 'string' to 'C1'
                //         (C1, (C2, C3)) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(8, 29),
                // (8,39): error CS0029: Cannot implicitly convert type 'string' to 'C2'
                //         (C1, (C2, C3)) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(8, 39),
                // (8,46): error CS0029: Cannot implicitly convert type 'string' to 'C3'
                //         (C1, (C2, C3)) a = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(8, 46)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_Deconstruction_02()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        (C1 a, (C2 b, C3 c)) = (""hello"", (""dog"", ""cat""));
    }
}

class C1
{
    public static explicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static explicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,33): error CS0029: Cannot implicitly convert type 'string' to 'C1'
                //         (C1 a, (C2 b, C3 c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""hello""").WithArguments("string", "C1").WithLocation(7, 33),
                // (7,43): error CS0029: Cannot implicitly convert type 'string' to 'C2'
                //         (C1 a, (C2 b, C3 c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""").WithArguments("string", "C2").WithLocation(7, 43),
                // (7,50): error CS0029: Cannot implicitly convert type 'string' to 'C3'
                //         (C1 a, (C2 b, C3 c)) = ("hello", ("dog", "cat"));
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""").WithArguments("string", "C3").WithLocation(7, 50)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void ExpressionTree_02()
        {
            var source = @"
using System;
using System.Linq.Expressions;
class C
{
    static void Main()
    {
        Expression<Func<C1>> x = () => (C1)""hello"";
        Expression<Func<C2>> y = () => (C2)""dog"";
        Expression<Func<C3>> z = () => (C3)""cat"";

        System.Console.WriteLine(x);
        System.Console.WriteLine(y);
        System.Console.WriteLine(z);
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (8,40): error CS0030: Cannot convert type 'string' to 'C1'
                //         Expression<Func<C1>> x = () => (C1)"hello";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C1)""hello""").WithArguments("string", "C1").WithLocation(8, 40),
                // (9,40): error CS0030: Cannot convert type 'string' to 'C2'
                //         Expression<Func<C2>> y = () => (C2)"dog";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C2)""dog""").WithArguments("string", "C2").WithLocation(9, 40),
                // (10,40): error CS0030: Cannot convert type 'string' to 'C3'
                //         Expression<Func<C3>> z = () => (C3)"cat";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C3)""cat""").WithArguments("string", "C3").WithLocation(10, 40)
                );
        }

        [Fact]
        public void ExpressionTree_03()
        {
            var source = @"
using System;
using System.Linq.Expressions;
class C
{
    static void Main()
    {
        Expression<Func<byte[]>> x = () => ""hello""u8;
        System.Console.WriteLine(x);
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (8,44): error CS9101: An expression tree may not contain UTF8 string conversion or literal.
                //         Expression<Func<byte[]>> x = () => "hello"u8;
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsUTF8StringLiterals, @"""hello""u8").WithLocation(8, 44)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.Write(Test(""s""));
        System.Console.Write(Test(""s""u8));
    }

    static string Test(ReadOnlySpan<char> a) => ""ReadOnlySpan"";
    static string Test(byte[] a) => ""array"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"ReadOnlySpanarray").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_02()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.Write(Test(""s""));
        System.Console.Write(Test(""s""u8));
    }

    static string Test(byte[] a) => ""array"";
    static string Test(ReadOnlySpan<char> a) => ""ReadOnlySpan"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"ReadOnlySpanarray").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_03()
        {
            var source = @"
class C
{
    static void Main()
    {
        System.Console.WriteLine(Test(""s""));
    }

    static string Test(C1 a) => ""string"";
    static string Test(C2 a) => ""array"";
}

class C1
{
    public static implicit operator C1(string x)
    {
        return new C1();
    }
}

class C2
{
    public static implicit operator C2(byte[] x)
    {
        return new C2();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"string").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_04()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine(Test(""s"", (int)1));
    }

    static string Test(ReadOnlySpan<char> a, long x) => ""ReadOnlySpan"";
    static string Test(byte[] a, int x) => ""array"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"ReadOnlySpan").VerifyDiagnostics();

            comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);
            comp.VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_05()
        {
            var source = @"
using System;

class C
{
    static void Main()
    {
        Test(""s"");
    }

    static void Test(C1 a) {}
}

class C1
{
    public static implicit operator C1(string x)
    {
        System.Console.WriteLine(""string"");
        return new C1();
    }

    public static implicit operator C1(ReadOnlySpan<byte> x)
    {
        System.Console.WriteLine(""ReadOnlySpan<byte>"");
        return new C1();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"string").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_10()
        {
            var source = @"
using System;

class Program
{
    static void Main()
    {
        var p = new Program();
        Console.WriteLine(p.M(""""));
    }

    public string M(byte[] b) => ""byte[]"";
}

static class E
{
    public static string M(this object o, string s) => ""string"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            CompileAndVerify(comp, expectedOutput: @"string").
                VerifyDiagnostics();

            comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NullableAnalysis_03()
        {
            var source = @"
#nullable enable

class C
{
    static void Main()
    {
        _ = ""hello""u8.Length;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics();
        }

        [ConditionalTheory(typeof(CoreClrOnly))]
        [InlineData("u8")]
        [InlineData("U8")]
        public void UTF8StringLiteral_01(string suffix)
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        Helpers.Print(Test1());
        Helpers.Print(Test2());
        Helpers.Print(Test3());
    }

    static byte[] Test1() => ""hello""" + suffix + @";
    static Span<byte> Test2() => ""dog""" + suffix + @";
    static ReadOnlySpan<byte> Test3() => ""cat""" + suffix + @";
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            var verifier = CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test1()", @"
{
  // Code size       18 (0x12)
  .maxstack  3
  IL_0000:  ldc.i4.5
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=5 <PrivateImplementationDetails>.2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  ret
}
");

            verifier.VerifyIL("C.Test2()", @"
{
  // Code size       23 (0x17)
  .maxstack  3
  IL_0000:  ldc.i4.3
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.CD6357EFDD966DE8C0CB2F876CC89EC74CE35F0968E11743987084BD42FB8944""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  call       ""System.Span<byte> System.Span<byte>.op_Implicit(byte[])""
  IL_0016:  ret
}
");

            verifier.VerifyIL("C.Test3()", @"
{
  // Code size       12 (0xc)
  .maxstack  2
  IL_0000:  ldsflda    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.77AF778B51ABD4A3C51C5DDD97204A9C3AE614EBCCB75A606C3B6865AED6744E""
  IL_0005:  ldc.i4.3
  IL_0006:  newobj     ""System.ReadOnlySpan<byte>..ctor(void*, int)""
  IL_000b:  ret
}
");

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularNext);

            CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);
            comp.VerifyDiagnostics(
                // (13,30): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static byte[] Test1() => "hello"u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""hello""" + suffix).WithArguments("UTF-8 string literals").WithLocation(13, 30),
                // (14,34): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static Span<byte> Test2() => "dog"u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""dog""" + suffix).WithArguments("UTF-8 string literals").WithLocation(14, 34),
                // (15,42): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static ReadOnlySpan<byte> Test3() => "cat"u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""cat""" + suffix).WithArguments("UTF-8 string literals").WithLocation(15, 42)
                );
        }

        [ConditionalTheory(typeof(CoreClrOnly))]
        [InlineData("u8")]
        [InlineData("U8")]
        public void UTF8StringLiteral_02(string suffix)
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        Helpers.Print(Test1());
        Helpers.Print(Test2());
        Helpers.Print(Test3());
    }

    static byte[] Test1() => @""hello""" + suffix + @";
    static Span<byte> Test2() => @""dog""" + suffix + @";
    static ReadOnlySpan<byte> Test3() => @""cat""" + suffix + @";
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            var verifier = CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test1()", @"
{
  // Code size       18 (0x12)
  .maxstack  3
  IL_0000:  ldc.i4.5
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=5 <PrivateImplementationDetails>.2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  ret
}
");

            verifier.VerifyIL("C.Test2()", @"
{
  // Code size       23 (0x17)
  .maxstack  3
  IL_0000:  ldc.i4.3
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.CD6357EFDD966DE8C0CB2F876CC89EC74CE35F0968E11743987084BD42FB8944""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  call       ""System.Span<byte> System.Span<byte>.op_Implicit(byte[])""
  IL_0016:  ret
}
");

            verifier.VerifyIL("C.Test3()", @"
{
  // Code size       12 (0xc)
  .maxstack  2
  IL_0000:  ldsflda    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.77AF778B51ABD4A3C51C5DDD97204A9C3AE614EBCCB75A606C3B6865AED6744E""
  IL_0005:  ldc.i4.3
  IL_0006:  newobj     ""System.ReadOnlySpan<byte>..ctor(void*, int)""
  IL_000b:  ret
}
");

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularNext);

            CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);
            comp.VerifyDiagnostics(
                // (13,30): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static byte[] Test1() => "hello"u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"@""hello""" + suffix).WithArguments("UTF-8 string literals").WithLocation(13, 30),
                // (14,34): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static Span<byte> Test2() => "dog"u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"@""dog""" + suffix).WithArguments("UTF-8 string literals").WithLocation(14, 34),
                // (15,42): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static ReadOnlySpan<byte> Test3() => "cat"u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"@""cat""" + suffix).WithArguments("UTF-8 string literals").WithLocation(15, 42)
                );
        }

        [ConditionalTheory(typeof(CoreClrOnly))]
        [InlineData("u8")]
        [InlineData("U8")]
        public void UTF8StringLiteral_03(string suffix)
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        Helpers.Print(Test1());
        Helpers.Print(Test2());
        Helpers.Print(Test3());
    }

    static byte[] Test1() => """"""hello""""""" + suffix + @";
    static Span<byte> Test2() => """"""dog""""""" + suffix + @";
    static ReadOnlySpan<byte> Test3() => """"""cat""""""" + suffix + @";
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            var verifier = CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test1()", @"
{
  // Code size       18 (0x12)
  .maxstack  3
  IL_0000:  ldc.i4.5
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=5 <PrivateImplementationDetails>.2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  ret
}
");

            verifier.VerifyIL("C.Test2()", @"
{
  // Code size       23 (0x17)
  .maxstack  3
  IL_0000:  ldc.i4.3
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.CD6357EFDD966DE8C0CB2F876CC89EC74CE35F0968E11743987084BD42FB8944""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  call       ""System.Span<byte> System.Span<byte>.op_Implicit(byte[])""
  IL_0016:  ret
}
");

            verifier.VerifyIL("C.Test3()", @"
{
  // Code size       12 (0xc)
  .maxstack  2
  IL_0000:  ldsflda    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.77AF778B51ABD4A3C51C5DDD97204A9C3AE614EBCCB75A606C3B6865AED6744E""
  IL_0005:  ldc.i4.3
  IL_0006:  newobj     ""System.ReadOnlySpan<byte>..ctor(void*, int)""
  IL_000b:  ret
}
");

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularNext);

            CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);
            comp.VerifyDiagnostics(
                // (13,30): error CS8652: The feature 'raw string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static byte[] Test1() => """hello"""u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""hello""""""" + suffix).WithArguments("raw string literals").WithLocation(13, 30),
                // (13,30): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static byte[] Test1() => """hello"""u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""hello""""""" + suffix).WithArguments("UTF-8 string literals").WithLocation(13, 30),
                // (14,34): error CS8652: The feature 'raw string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static Span<byte> Test2() => """dog"""u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""dog""""""" + suffix).WithArguments("raw string literals").WithLocation(14, 34),
                // (14,34): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static Span<byte> Test2() => """dog"""u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""dog""""""" + suffix).WithArguments("UTF-8 string literals").WithLocation(14, 34),
                // (15,42): error CS8652: The feature 'raw string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static ReadOnlySpan<byte> Test3() => """cat"""u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""cat""""""" + suffix).WithArguments("raw string literals").WithLocation(15, 42),
                // (15,42): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static ReadOnlySpan<byte> Test3() => """cat"""u8;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""cat""""""" + suffix).WithArguments("UTF-8 string literals").WithLocation(15, 42)
                );
        }

        [ConditionalTheory(typeof(CoreClrOnly))]
        [InlineData("u8")]
        [InlineData("U8")]
        public void UTF8StringLiteral_04(string suffix)
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        Helpers.Print(Test1());
        Helpers.Print(Test2());
        Helpers.Print(Test3());
    }

    static byte[] Test1() => """"""
  hello
  """"""" + suffix + @";
    static Span<byte> Test2() => """"""
  dog
  """"""" + suffix + @";
    static ReadOnlySpan<byte> Test3() => """"""
  cat
  """"""" + suffix + @";
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            var verifier = CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test1()", @"
{
  // Code size       18 (0x12)
  .maxstack  3
  IL_0000:  ldc.i4.5
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=5 <PrivateImplementationDetails>.2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  ret
}
");

            verifier.VerifyIL("C.Test2()", @"
{
  // Code size       23 (0x17)
  .maxstack  3
  IL_0000:  ldc.i4.3
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.CD6357EFDD966DE8C0CB2F876CC89EC74CE35F0968E11743987084BD42FB8944""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  call       ""System.Span<byte> System.Span<byte>.op_Implicit(byte[])""
  IL_0016:  ret
}
");

            verifier.VerifyIL("C.Test3()", @"
{
  // Code size       12 (0xc)
  .maxstack  2
  IL_0000:  ldsflda    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.77AF778B51ABD4A3C51C5DDD97204A9C3AE614EBCCB75A606C3B6865AED6744E""
  IL_0005:  ldc.i4.3
  IL_0006:  newobj     ""System.ReadOnlySpan<byte>..ctor(void*, int)""
  IL_000b:  ret
}
");

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.RegularNext);

            CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);
            comp.VerifyDiagnostics(
                // (13,30): error CS8652: The feature 'raw string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static byte[] Test1() => """
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""
  hello
  """"""" + suffix).WithArguments("raw string literals").WithLocation(13, 30),
                // (13,30): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static byte[] Test1() => """
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""
  hello
  """"""" + suffix).WithArguments("UTF-8 string literals").WithLocation(13, 30),
                // (16,34): error CS8652: The feature 'raw string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static Span<byte> Test2() => """
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""
  dog
  """"""" + suffix).WithArguments("raw string literals").WithLocation(16, 34),
                // (16,34): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static Span<byte> Test2() => """
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""
  dog
  """"""" + suffix).WithArguments("UTF-8 string literals").WithLocation(16, 34),
                // (19,42): error CS8652: The feature 'raw string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static ReadOnlySpan<byte> Test3() => """
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""
  cat
  """"""" + suffix).WithArguments("raw string literals").WithLocation(19, 42),
                // (19,42): error CS8652: The feature 'UTF-8 string literals' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static ReadOnlySpan<byte> Test3() => """
                Diagnostic(ErrorCode.ERR_FeatureInPreview, @"""""""
  cat
  """"""" + suffix).WithArguments("UTF-8 string literals").WithLocation(19, 42)
                );
        }

        [Fact]
        public void MissingType_01()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        _ = ""hello""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.MakeTypeMissing(SpecialType.System_Byte);
            comp.VerifyEmitDiagnostics(
                // (7,13): error CS0518: Predefined type 'System.Byte' is not defined or imported
                //         _ = "hello"u8;
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, @"""hello""u8").WithArguments("System.Byte").WithLocation(7, 13)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void MissingHelpers_07()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine();
        Helpers.Print(Test1());
        Helpers.Print(Test2());
        Helpers.Print(Test3());
    }

    static byte[] Test1() => ""hello""u8;
    static Span<byte> Test2() => ""dog""u8;
    static ReadOnlySpan<byte> Test3() => ""cat""u8;
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.MakeMemberMissing(WellKnownMember.System_Span_T__ctor_Array);
            comp.MakeMemberMissing(WellKnownMember.System_Span_T__ctor_Pointer);
            comp.MakeMemberMissing(WellKnownMember.System_ReadOnlySpan_T__ctor_Array);

            var verifier = CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
{ 0x64 0x6F 0x67 }
{ 0x63 0x61 0x74 }
", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test1()", @"
{
  // Code size       18 (0x12)
  .maxstack  3
  IL_0000:  ldc.i4.5
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=5 <PrivateImplementationDetails>.2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  ret
}
");

            verifier.VerifyIL("C.Test2()", @"
{
  // Code size       23 (0x17)
  .maxstack  3
  IL_0000:  ldc.i4.3
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.CD6357EFDD966DE8C0CB2F876CC89EC74CE35F0968E11743987084BD42FB8944""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  call       ""System.Span<byte> System.Span<byte>.op_Implicit(byte[])""
  IL_0016:  ret
}
");

            verifier.VerifyIL("C.Test3()", @"
{
  // Code size       12 (0xc)
  .maxstack  2
  IL_0000:  ldsflda    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.77AF778B51ABD4A3C51C5DDD97204A9C3AE614EBCCB75A606C3B6865AED6744E""
  IL_0005:  ldc.i4.3
  IL_0006:  newobj     ""System.ReadOnlySpan<byte>..ctor(void*, int)""
  IL_000b:  ret
}
");
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void MissingHelpers_08()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        Helpers.Print(Test2());
    }

    static Span<byte> Test2() => ""dog""u8;
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.MakeMemberMissing(WellKnownMember.System_Span_T__ctor_Pointer);
            var verifier = CompileAndVerify(comp, expectedOutput: "{ 0x64 0x6F 0x67 }", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test2()", @"
{
  // Code size       23 (0x17)
  .maxstack  3
  IL_0000:  ldc.i4.3
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.CD6357EFDD966DE8C0CB2F876CC89EC74CE35F0968E11743987084BD42FB8944""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  call       ""System.Span<byte> System.Span<byte>.op_Implicit(byte[])""
  IL_0016:  ret
}
");
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void MissingHelpers_09()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        Helpers.Print(Test3());
    }

    static ReadOnlySpan<byte> Test3() => ""cat""u8;
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.MakeMemberMissing(WellKnownMember.System_ReadOnlySpan_T__ctor_Pointer);
            var verifier = CompileAndVerify(comp, expectedOutput: "{ 0x63 0x61 0x74 }", verify: Verification.Fails).VerifyDiagnostics();

            verifier.VerifyIL("C.Test3()", @"
{
  // Code size       23 (0x17)
  .maxstack  3
  IL_0000:  ldc.i4.3
  IL_0001:  newarr     ""byte""
  IL_0006:  dup
  IL_0007:  ldtoken    ""<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.77AF778B51ABD4A3C51C5DDD97204A9C3AE614EBCCB75A606C3B6865AED6744E""
  IL_000c:  call       ""void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)""
  IL_0011:  call       ""System.ReadOnlySpan<byte> System.ReadOnlySpan<byte>.op_Implicit(byte[])""
  IL_0016:  ret
}
");
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_11()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine(Test(""s""u8));
    }

    static string Test(ReadOnlySpan<byte> a) => ""ReadOnlySpan"";
    static string Test(byte[] a) => ""array"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            CompileAndVerify(comp, expectedOutput: @"array").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_12()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine(Test(""s""u8));
    }

    static string Test(Span<byte> a) => ""Span"";
    static string Test(byte[] a) => ""array"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            CompileAndVerify(comp, expectedOutput: @"array").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_13()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine(Test(""s""u8));
    }

    static string Test(ReadOnlySpan<byte> a) => ""ReadOnlySpan"";
    static string Test(Span<byte> a) => ""Span"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            CompileAndVerify(comp, expectedOutput: @"Span").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_14()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        System.Console.WriteLine(Test(""s""u8));
    }

    static string Test(ReadOnlySpan<byte> a) => ""ReadOnlySpan"";
    static string Test(byte[] a) => ""array"";
    static string Test(Span<byte> a) => ""Span"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            CompileAndVerify(comp, expectedOutput: @"array").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_03()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        C1 x = ""hello""u8;
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        Helpers.Print(x);
        return new C1();
    }
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_04()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        var x = (C1)""hello""u8;
    }
}

class C1
{
    public static implicit operator C1(byte[] x)
    {
        Helpers.Print(x);
        return new C1();
    }
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_05()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        C2 y = ""dog""u8;
        C3 z = ""cat""u8;
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,16): error CS0029: Cannot implicitly convert type 'byte[]' to 'C2'
                //         C2 y = "dog"u8;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""u8").WithArguments("byte[]", "C2").WithLocation(7, 16),
                // (8,16): error CS0029: Cannot implicitly convert type 'byte[]' to 'C3'
                //         C3 z = "cat"u8;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""u8").WithArguments("byte[]", "C3").WithLocation(8, 16)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedImplicitConversions_06()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        var y = (C2)""dog""u8;
        var z = (C3)""cat""u8;
    }
}

class C2
{
    public static implicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static implicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,17): error CS0030: Cannot convert type 'byte[]' to 'C2'
                //         var y = (C2)"dog"u8;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C2)""dog""u8").WithArguments("byte[]", "C2").WithLocation(7, 17),
                // (8,17): error CS0030: Cannot convert type 'byte[]' to 'C3'
                //         var z = (C3)"cat"u8;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C3)""cat""u8").WithArguments("byte[]", "C3").WithLocation(8, 17)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_03()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        C1 x = ""hello""u8;
        C2 y = ""dog""u8;
        C3 z = ""cat""u8;
    }
}

class C1
{
    public static explicit operator C1(byte[] x)
    {
        return new C1();
    }
}

class C2
{
    public static explicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (7,16): error CS0266: Cannot implicitly convert type 'byte[]' to 'C1'. An explicit conversion exists (are you missing a cast?)
                //         C1 x = "hello"u8;
                Diagnostic(ErrorCode.ERR_NoImplicitConvCast, @"""hello""u8").WithArguments("byte[]", "C1").WithLocation(7, 16),
                // (8,16): error CS0029: Cannot implicitly convert type 'byte[]' to 'C2'
                //         C2 y = "dog"u8;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""dog""u8").WithArguments("byte[]", "C2").WithLocation(8, 16),
                // (9,16): error CS0029: Cannot implicitly convert type 'byte[]' to 'C3'
                //         C3 z = "cat"u8;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""cat""u8").WithArguments("byte[]", "C3").WithLocation(9, 16)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_04()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        var x = (C1)""hello""u8;
    }
}

class C1
{
    public static explicit operator C1(byte[] x)
    {
        Helpers.Print(x);
        return new C1();
    }
}
";
            var comp = CreateCompilation(source + HelpersSource, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"
{ 0x68 0x65 0x6C 0x6C 0x6F }
").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void UserDefinedExplicitConversions_05()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {
        var y = (C2)""dog""u8;
        var z = (C3)""cat""u8;
    }
}

class C2
{
    public static explicit operator C2(Span<byte> x)
    {
        return new C2();
    }
}

class C3
{
    public static explicit operator C3(ReadOnlySpan<byte> x)
    {
        return new C3();
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);
            comp.VerifyDiagnostics(
                // (7,17): error CS0030: Cannot convert type 'byte[]' to 'C2'
                //         var y = (C2)"dog"u8;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C2)""dog""u8").WithArguments("byte[]", "C2").WithLocation(7, 17),
                // (8,17): error CS0030: Cannot convert type 'byte[]' to 'C3'
                //         var z = (C3)"cat"u8;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(C3)""cat""u8").WithArguments("byte[]", "C3").WithLocation(8, 17)
                );
        }

        [Theory]
        [InlineData(@"""hello""")]
        [InlineData(@"@""hello""")]
        [InlineData(@"""""""hello""""""")]
        [InlineData(@"""""""
  hello
  """"""")]
        public void NaturalType_01(string literal)
        {
            var source = @"
class C
{
    static void Main()
    {
        System.Console.WriteLine((" + literal + @"u8).GetType());
    }
}
";
            var comp = CreateCompilation(source, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"System.Byte[]").VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void OverloadResolution_15()
        {
            var source = @"
class C
{
    static void Main()
    {
        System.Console.WriteLine(Test((""s"", 1)));
    }

    static string Test((object, int) a) => ""object"";
    static string Test((byte[], int) a) => ""array"";
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            CompileAndVerify(comp, expectedOutput: @"object").VerifyDiagnostics();

            comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe, parseOptions: TestOptions.Regular10);
            comp.VerifyDiagnostics();
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void NoConversionFromNonStringNull_01()
        {
            var source = @"
#pragma warning disable CS0219 // The variable is assigned but its value is never used
using System;
class C
{
    static void Main()
    {
        const object nullValue = null;
        byte[] array = nullValue;
        Span<byte> span = nullValue;
        ReadOnlySpan<byte> readonlySpan = nullValue;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyEmitDiagnostics(
                // (9,24): error CS0266: Cannot implicitly convert type 'object' to 'byte[]'. An explicit conversion exists (are you missing a cast?)
                //         byte[] array = nullValue;
                Diagnostic(ErrorCode.ERR_NoImplicitConvCast, "nullValue").WithArguments("object", "byte[]").WithLocation(9, 24),
                // (10,27): error CS0266: Cannot implicitly convert type 'object' to 'System.Span<byte>'. An explicit conversion exists (are you missing a cast?)
                //         Span<byte> span = nullValue;
                Diagnostic(ErrorCode.ERR_NoImplicitConvCast, "nullValue").WithArguments("object", "System.Span<byte>").WithLocation(10, 27),
                // (11,43): error CS0266: Cannot implicitly convert type 'object' to 'System.ReadOnlySpan<byte>'. An explicit conversion exists (are you missing a cast?)
                //         ReadOnlySpan<byte> readonlySpan = nullValue;
                Diagnostic(ErrorCode.ERR_NoImplicitConvCast, "nullValue").WithArguments("object", "System.ReadOnlySpan<byte>").WithLocation(11, 43)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void VariableIsNotUsed()
        {
            var source = @"
using System;
class C
{
    static void Main()
    {


        var x1 = ""123""U8;
        Span<byte> x2 = ""124""U8;
        ReadOnlySpan<byte> x3 = ""125""U8;














        var x4 = (byte[])""123""U8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugExe);

            comp.VerifyEmitDiagnostics(
                // (9,13): warning CS0219: The variable 'x1' is assigned but its value is never used
                //         var x1 = "123"U8;
                Diagnostic(ErrorCode.WRN_UnreferencedVarAssg, "x1").WithArguments("x1").WithLocation(9, 13),
                // (26,13): warning CS0219: The variable 'x4' is assigned but its value is never used
                //         var x4 = (byte[])"123"U8;
                Diagnostic(ErrorCode.WRN_UnreferencedVarAssg, "x4").WithArguments("x4").WithLocation(26, 13)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void DefaultParameterValues_01()
        {
            var source = @"
using System;
class C
{






    static void M05(byte[] x = ""05""u8){}
    static void M06(Span<byte> x = ""06""u8){}
    static void M07(ReadOnlySpan<byte> x = ""07""U8){}
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (11,32): error CS1736: Default parameter value for 'x' must be a compile-time constant
                //     static void M05(byte[] x = "05"u8){}
                Diagnostic(ErrorCode.ERR_DefaultValueMustBeConstant, @"""05""u8").WithArguments("x").WithLocation(11, 32),
                // (12,36): error CS1736: Default parameter value for 'x' must be a compile-time constant
                //     static void M06(Span<byte> x = "06"u8){}
                Diagnostic(ErrorCode.ERR_DefaultValueMustBeConstant, @"""06""u8").WithArguments("x").WithLocation(12, 36),
                // (13,44): error CS1736: Default parameter value for 'x' must be a compile-time constant
                //     static void M07(ReadOnlySpan<byte> x = "07"U8){}
                Diagnostic(ErrorCode.ERR_DefaultValueMustBeConstant, @"""07""U8").WithArguments("x").WithLocation(13, 44)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void DefaultParameterValues_02()
        {
            var source = @"
using System;
class C
{






    static void M05(byte[] x = (byte[])""05""u8){}
    static void M06(Span<byte> x = (Span<byte>)""06""u8){}
    static void M07(ReadOnlySpan<byte> x = (ReadOnlySpan<byte>)""07""U8){}
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (11,32): error CS1736: Default parameter value for 'x' must be a compile-time constant
                //     static void M05(byte[] x = (byte[])"05"u8){}
                Diagnostic(ErrorCode.ERR_DefaultValueMustBeConstant, @"(byte[])""05""u8").WithArguments("x").WithLocation(11, 32),
                // (12,36): error CS1736: Default parameter value for 'x' must be a compile-time constant
                //     static void M06(Span<byte> x = (Span<byte>)"06"u8){}
                Diagnostic(ErrorCode.ERR_DefaultValueMustBeConstant, @"(Span<byte>)""06""u8").WithArguments("x").WithLocation(12, 36),
                // (13,44): error CS1736: Default parameter value for 'x' must be a compile-time constant
                //     static void M07(ReadOnlySpan<byte> x = (ReadOnlySpan<byte>)"07"U8){}
                Diagnostic(ErrorCode.ERR_DefaultValueMustBeConstant, @"(ReadOnlySpan<byte>)""07""U8").WithArguments("x").WithLocation(13, 44)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void CallerMemberName_01()
        {
            var source = @"
using System;
class C
{
    void Test1([System.Runtime.CompilerServices.CallerMemberName] byte[] x = default)
    {
    }
    void Test2([System.Runtime.CompilerServices.CallerMemberName] Span<byte> x = default)
    {
    }
    void Test3([System.Runtime.CompilerServices.CallerMemberName] ReadOnlySpan<byte> x = default)
    {
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (5,17): error CS4019: CallerMemberNameAttribute cannot be applied because there are no standard conversions from type 'string' to type 'byte[]'
                //     void Test1([System.Runtime.CompilerServices.CallerMemberName] byte[] x = default)
                Diagnostic(ErrorCode.ERR_NoConversionForCallerMemberNameParam, "System.Runtime.CompilerServices.CallerMemberName").WithArguments("string", "byte[]").WithLocation(5, 17),
                // (8,17): error CS4019: CallerMemberNameAttribute cannot be applied because there are no standard conversions from type 'string' to type 'Span<byte>'
                //     void Test2([System.Runtime.CompilerServices.CallerMemberName] Span<byte> x = default)
                Diagnostic(ErrorCode.ERR_NoConversionForCallerMemberNameParam, "System.Runtime.CompilerServices.CallerMemberName").WithArguments("string", "System.Span<byte>").WithLocation(8, 17),
                // (11,17): error CS4019: CallerMemberNameAttribute cannot be applied because there are no standard conversions from type 'string' to type 'ReadOnlySpan<byte>'
                //     void Test3([System.Runtime.CompilerServices.CallerMemberName] ReadOnlySpan<byte> x = default)
                Diagnostic(ErrorCode.ERR_NoConversionForCallerMemberNameParam, "System.Runtime.CompilerServices.CallerMemberName").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(11, 17)
                );
        }

        [Fact]
        public void AttributeArgument_01()
        {
            var source = @"

class C
{








    [UTF8(""3""U8)]
    static void M03(){}







    [UTF8((byte[])""3""U8)]
    static void M06(){}
}

public class UTF8Attribute : System.Attribute
{
    public UTF8Attribute(byte[] x) {}
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (13,11): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
                //     [UTF8("3"U8)]
                Diagnostic(ErrorCode.ERR_BadAttributeArgument, @"""3""U8").WithLocation(13, 11),
                // (22,11): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
                //     [UTF8((byte[])"3"U8)]
                Diagnostic(ErrorCode.ERR_BadAttributeArgument, @"(byte[])""3""U8").WithLocation(22, 11)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void AttributeArgument_02()
        {
            var source = @"
using System;
class C
{








    [UTF8(""3""U8)]
    static void M03(){}







    [UTF8((Span<byte>)""3""U8)]
    static void M06(){}
}

public class UTF8Attribute : System.Attribute
{
    public UTF8Attribute(Span<byte> x) {}
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (13,6): error CS0181: Attribute constructor parameter 'x' has type 'Span<byte>', which is not a valid attribute parameter type
                //     [UTF8("3"U8)]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "UTF8").WithArguments("x", "System.Span<byte>").WithLocation(13, 6),
                // (22,6): error CS0181: Attribute constructor parameter 'x' has type 'Span<byte>', which is not a valid attribute parameter type
                //     [UTF8((Span<byte>)"3"U8)]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "UTF8").WithArguments("x", "System.Span<byte>").WithLocation(22, 6)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void AttributeArgument_03()
        {
            var source = @"
using System;
class C
{








    [UTF8(""3""U8)]
    static void M03(){}







    [UTF8((ReadOnlySpan<byte>)""3""U8)]
    static void M06(){}
}

public class UTF8Attribute : System.Attribute
{
    public UTF8Attribute(ReadOnlySpan<byte> x) {}
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (13,6): error CS0181: Attribute constructor parameter 'x' has type 'ReadOnlySpan<byte>', which is not a valid attribute parameter type
                //     [UTF8("3"U8)]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "UTF8").WithArguments("x", "System.ReadOnlySpan<byte>").WithLocation(13, 6),
                // (22,6): error CS0181: Attribute constructor parameter 'x' has type 'ReadOnlySpan<byte>', which is not a valid attribute parameter type
                //     [UTF8((ReadOnlySpan<byte>)"3"U8)]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "UTF8").WithArguments("x", "System.ReadOnlySpan<byte>").WithLocation(22, 6)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void NoConversionFromNonConstant_01()
        {
            var source = @"
using System;
class C
{
    void Test(string s)
    {
        byte[] x = s;
        Span<byte> y = s;
        ReadOnlySpan<byte> z = s;
        x = (byte[])s;
        y = (Span<byte>)s;
        z = (ReadOnlySpan<byte>)s;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (7,20): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         byte[] x = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "byte[]").WithLocation(7, 20),
                // (8,24): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte>'
                //         Span<byte> y = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.Span<byte>").WithLocation(8, 24),
                // (9,32): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         ReadOnlySpan<byte> z = s;
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(9, 32),
                // (10,13): error CS0030: Cannot convert type 'string' to 'byte[]'
                //         x = (byte[])s;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(byte[])s").WithArguments("string", "byte[]").WithLocation(10, 13),
                // (11,13): error CS0030: Cannot convert type 'string' to 'System.Span<byte>'
                //         y = (Span<byte>)s;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(Span<byte>)s").WithArguments("string", "System.Span<byte>").WithLocation(11, 13),
                // (12,13): error CS0030: Cannot convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         z = (ReadOnlySpan<byte>)s;
                Diagnostic(ErrorCode.ERR_NoExplicitConv, "(ReadOnlySpan<byte>)s").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(12, 13)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void NoConversionFromNonConstant_02()
        {
            var source = @"
using System;
class C
{
    void Test(string s)
    {
        byte[] x = $""1{s}"";
        Span<byte> y = $""2{s}"";
        ReadOnlySpan<byte> z = $""3{s}"";
        x = (byte[])$""4{s}"";
        y = (Span<byte>)$""5{s}"";
        z = (ReadOnlySpan<byte>)$""6{s}"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (7,20): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         byte[] x = $"1{s}";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"$""1{s}""").WithArguments("string", "byte[]").WithLocation(7, 20),
                // (8,24): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte>'
                //         Span<byte> y = $"2{s}";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"$""2{s}""").WithArguments("string", "System.Span<byte>").WithLocation(8, 24),
                // (9,32): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         ReadOnlySpan<byte> z = $"3{s}";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"$""3{s}""").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(9, 32),
                // (10,13): error CS0030: Cannot convert type 'string' to 'byte[]'
                //         x = (byte[])$"4{s}";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(byte[])$""4{s}""").WithArguments("string", "byte[]").WithLocation(10, 13),
                // (11,13): error CS0030: Cannot convert type 'string' to 'System.Span<byte>'
                //         y = (Span<byte>)$"5{s}";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(Span<byte>)$""5{s}""").WithArguments("string", "System.Span<byte>").WithLocation(11, 13),
                // (12,13): error CS0030: Cannot convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         z = (ReadOnlySpan<byte>)$"6{s}";
                Diagnostic(ErrorCode.ERR_NoExplicitConv, @"(ReadOnlySpan<byte>)$""6{s}""").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(12, 13)
                );
        }

        [Fact]
        public void PatternMatching_01()
        {
            var source = @"

class C
{
    void Test(byte[] s)
    {
        _ = s is ""1"";
        _ = s is ""2""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (7,18): error CS0029: Cannot implicitly convert type 'string' to 'byte[]'
                //         _ = s is "1";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""1""").WithArguments("string", "byte[]").WithLocation(7, 18),
                // (8,18): error CS0150: A constant value is expected
                //         _ = s is "2"u8;
                Diagnostic(ErrorCode.ERR_ConstantExpected, @"""2""u8").WithLocation(8, 18)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void PatternMatching_02()
        {
            var source = @"
using System;
class C
{
    void Test(Span<byte> s)
    {
        _ = s is ""1"";
        _ = s is ""2""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (7,18): error CS0029: Cannot implicitly convert type 'string' to 'System.Span<byte>'
                //         _ = s is "1";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""1""").WithArguments("string", "System.Span<byte>").WithLocation(7, 18),
                // (8,18): error CS0150: A constant value is expected
                //         _ = s is "2"u8;
                Diagnostic(ErrorCode.ERR_ConstantExpected, @"""2""u8").WithLocation(8, 18)
                );
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void PatternMatching_03()
        {
            var source = @"
using System;
class C
{
    void Test(ReadOnlySpan<byte> s)
    {
        _ = s is ""1"";
        _ = s is ""2""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);

            comp.VerifyEmitDiagnostics(
                // (7,18): error CS0029: Cannot implicitly convert type 'string' to 'System.ReadOnlySpan<byte>'
                //         _ = s is "1";
                Diagnostic(ErrorCode.ERR_NoImplicitConv, @"""1""").WithArguments("string", "System.ReadOnlySpan<byte>").WithLocation(7, 18),
                // (8,18): error CS0150: A constant value is expected
                //         _ = s is "2"u8;
                Diagnostic(ErrorCode.ERR_ConstantExpected, @"""2""u8").WithLocation(8, 18)
                );
        }

        [Fact]
        public void SemanticModel_01()
        {
            var source = @"
using System;
class C
{
    byte[] Test()
    {
        return ""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.String", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Byte[]", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.False(model.GetConversion(node).Exists);
        }

        [Fact]
        public void SemanticModel_02()
        {
            var source = @"
using System;
class C
{
    byte[] Test()
    {
        return (byte[])""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.Byte[]", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Byte[]", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_03()
        {
            var source = @"
using System;
class C
{
    Span<byte> Test()
    {
        return ""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.String", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Span<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.False(model.GetConversion(node).Exists);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_04()
        {
            var source = @"
using System;
class C
{
    Span<byte> Test()
    {
        return (Span<byte>)""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.Span<System.Byte>", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Span<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_05()
        {
            var source = @"
using System;
class C
{
    ReadOnlySpan<byte> Test()
    {
        return ""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.String", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.ReadOnlySpan<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.False(model.GetConversion(node).Exists);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_06()
        {
            var source = @"
using System;
class C
{
    ReadOnlySpan<byte> Test()
    {
        return (ReadOnlySpan<byte>)""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.ReadOnlySpan<System.Byte>", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.ReadOnlySpan<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }

        [Fact]
        public void SemanticModel_07()
        {
            var source = @"
using System;
class C
{
    byte[] Test()
    {
        return ""1""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.Byte[]", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Byte[]", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }

        [Fact]
        public void SemanticModel_08()
        {
            var source = @"
using System;
class C
{
    byte[] Test()
    {
        return (byte[])""1""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Null(symbolInfo.Symbol);

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.Byte[]", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Byte[]", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_09()
        {
            var source = @"
using System;
class C
{
    Span<byte> Test()
    {
        return ""1""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Equal("System.Span<System.Byte> System.Span<System.Byte>.op_Implicit(System.Byte[]? array)", symbolInfo.Symbol.ToTestDisplayString());

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.Byte[]", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Span<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsUserDefined);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_10()
        {
            var source = @"
using System;
class C
{
    Span<byte> Test()
    {
        return (Span<byte>)""1""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Equal("System.Span<System.Byte> System.Span<System.Byte>.op_Implicit(System.Byte[]? array)", symbolInfo.Symbol.ToTestDisplayString());

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.Span<System.Byte>", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.Span<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_11()
        {
            var source = @"
using System;
class C
{
    ReadOnlySpan<byte> Test()
    {
        return ""1""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Equal("System.ReadOnlySpan<System.Byte> System.ReadOnlySpan<System.Byte>.op_Implicit(System.Byte[]? array)", symbolInfo.Symbol.ToTestDisplayString());

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.Byte[]", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.ReadOnlySpan<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsUserDefined);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_12()
        {
            var source = @"
using System;
class C
{
    ReadOnlySpan<byte> Test()
    {
        return (ReadOnlySpan<byte>)""1""u8;
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Equal("System.ReadOnlySpan<System.Byte> System.ReadOnlySpan<System.Byte>.op_Implicit(System.Byte[]? array)", symbolInfo.Symbol.ToTestDisplayString());

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.ReadOnlySpan<System.Byte>", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.ReadOnlySpan<System.Byte>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_13()
        {
            var source = @"
using System;
class C
{
    ReadOnlySpan<char> Test()
    {
        return ""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Equal("System.ReadOnlySpan<System.Char> System.String.op_Implicit(System.String? value)", symbolInfo.Symbol.ToTestDisplayString());

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.String", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.ReadOnlySpan<System.Char>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsUserDefined);
        }

        [ConditionalFact(typeof(CoreClrOnly))]
        public void SemanticModel_14()
        {
            var source = @"
using System;
class C
{
    ReadOnlySpan<char> Test()
    {
        return (ReadOnlySpan<char>)""1"";
    }
}
";
            var comp = CreateCompilation(source, targetFramework: TargetFramework.NetCoreApp, options: TestOptions.DebugDll);
            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);
            var node = tree.GetCompilationUnitRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression;

            var symbolInfo = model.GetSymbolInfo(node);
            Assert.Equal("System.ReadOnlySpan<System.Char> System.String.op_Implicit(System.String? value)", symbolInfo.Symbol.ToTestDisplayString());

            var typeInfo = model.GetTypeInfo(node);
            Assert.Equal("System.ReadOnlySpan<System.Char>", typeInfo.Type.ToTestDisplayString());
            Assert.Equal("System.ReadOnlySpan<System.Char>", typeInfo.ConvertedType.ToTestDisplayString());

            Assert.True(model.GetConversion(node).IsIdentity);
        }
    }
}
