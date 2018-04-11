﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    public class PatternTests : EmitMetadataTestBase
    {
        [Fact, WorkItem(18811, "https://github.com/dotnet/roslyn/issues/18811")]
        public void MissingNullable_01()
        {
            var source = @"namespace System {
    public class Object { }
    public abstract class ValueType { }
    public struct Void { }
    public struct Boolean { }
    public struct Int32 { }
}
static class C {
    public static bool M() => ((object)123) is int i;
}
";
            var compilation = CreateEmptyCompilation(source, options: TestOptions.ReleaseDll);
            compilation.GetDiagnostics().Verify();
            compilation.GetEmitDiagnostics().Verify(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1)
                );
        }

        [Fact, WorkItem(18811, "https://github.com/dotnet/roslyn/issues/18811")]
        public void MissingNullable_02()
        {
            var source = @"namespace System {
    public class Object { }
    public abstract class ValueType { }
    public struct Void { }
    public struct Boolean { }
    public struct Int32 { }
    public struct Nullable<T> where T : struct { }
}
static class C {
    public static bool M() => ((object)123) is int i;
}
";
            var compilation = CreateEmptyCompilation(source, options: TestOptions.UnsafeReleaseDll);
            compilation.GetDiagnostics().Verify();
            compilation.GetEmitDiagnostics().Verify(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion)
                );
        }

        [Fact]
        public void MissingNullable_03()
        {
            var source = @"namespace System {
    public class Object { }
    public abstract class ValueType { }
    public struct Void { }
    public struct Boolean { }
    public struct Int32 { }
    public struct Nullable<T> where T : struct { }
}
static class C {
    static void M1(int? x)
    {
        switch (x)
        {
            case int i: break;
        }
    }
    static bool M2(int? x) => x is int i;
}
";
            var compilation = CreateEmptyCompilation(source, options: TestOptions.UnsafeReleaseDll);
            compilation.GetDiagnostics().Verify();
            compilation.GetEmitDiagnostics().Verify(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // (14,18): error CS0656: Missing compiler required member 'System.Nullable`1.get_HasValue'
                //             case int i: break;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "int i").WithArguments("System.Nullable`1", "get_HasValue").WithLocation(14, 18),
                // (14,18): error CS0656: Missing compiler required member 'System.Nullable`1.GetValueOrDefault'
                //             case int i: break;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "int i").WithArguments("System.Nullable`1", "GetValueOrDefault").WithLocation(14, 18),
                // (12,17): error CS0656: Missing compiler required member 'System.Nullable`1.get_Value'
                //         switch (x)
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "x").WithArguments("System.Nullable`1", "get_Value").WithLocation(12, 17),
                // (17,36): error CS0656: Missing compiler required member 'System.Nullable`1.get_HasValue'
                //     static bool M2(int? x) => x is int i;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "int i").WithArguments("System.Nullable`1", "get_HasValue").WithLocation(17, 36),
                // (17,36): error CS0656: Missing compiler required member 'System.Nullable`1.GetValueOrDefault'
                //     static bool M2(int? x) => x is int i;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "int i").WithArguments("System.Nullable`1", "GetValueOrDefault").WithLocation(17, 36),
                // (17,36): error CS0656: Missing compiler required member 'System.Nullable`1.get_Value'
                //     static bool M2(int? x) => x is int i;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "int i").WithArguments("System.Nullable`1", "get_Value").WithLocation(17, 36)
                );
        }

        [Fact]
        public void MissingNullable_04()
        {
            var source = @"namespace System {
    public class Object { }
    public abstract class ValueType { }
    public struct Void { }
    public struct Boolean { }
    public struct Int32 { }
    public struct Nullable<T> where T : struct { public T GetValueOrDefault() => default(T); }
}
static class C {
    static void M1(int? x)
    {
        switch (x)
        {
            case int i: break;
        }
    }
    static bool M2(int? x) => x is int i;
}
";
            var compilation = CreateEmptyCompilation(source, options: TestOptions.UnsafeReleaseDll);
            compilation.GetDiagnostics().Verify();
            compilation.GetEmitDiagnostics().Verify(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // (14,18): error CS0656: Missing compiler required member 'System.Nullable`1.get_HasValue'
                //             case int i: break;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "int i").WithArguments("System.Nullable`1", "get_HasValue").WithLocation(14, 18),
                // (17,36): error CS0656: Missing compiler required member 'System.Nullable`1.get_HasValue'
                //     static bool M2(int? x) => x is int i;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "int i").WithArguments("System.Nullable`1", "get_HasValue").WithLocation(17, 36)
                );
        }

        [Fact, WorkItem(17266, "https://github.com/dotnet/roslyn/issues/17266")]
        public void DoubleEvaluation01()
        {
            var source =
@"using System;
public class C
{
    public static void Main()
    {
        if (TryGet() is int index)
        {
            Console.WriteLine(index);
        }
    }

    public static int? TryGet()
    {
        Console.WriteLine(""eval"");
        return null;
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.DebugExe);
            compilation.VerifyDiagnostics();
            var expectedOutput = @"eval";
            var compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("C.Main",
@"{
  // Code size       42 (0x2a)
  .maxstack  1
  .locals init (int V_0, //index
                bool V_1,
                int? V_2)
  IL_0000:  nop
  IL_0001:  call       ""int? C.TryGet()""
  IL_0006:  stloc.2
  IL_0007:  ldloca.s   V_2
  IL_0009:  call       ""bool int?.HasValue.get""
  IL_000e:  brfalse.s  IL_001b
  IL_0010:  ldloca.s   V_2
  IL_0012:  call       ""int int?.GetValueOrDefault()""
  IL_0017:  stloc.0
  IL_0018:  ldc.i4.1
  IL_0019:  br.s       IL_001c
  IL_001b:  ldc.i4.0
  IL_001c:  stloc.1
  IL_001d:  ldloc.1
  IL_001e:  brfalse.s  IL_0029
  IL_0020:  nop
  IL_0021:  ldloc.0
  IL_0022:  call       ""void System.Console.WriteLine(int)""
  IL_0027:  nop
  IL_0028:  nop
  IL_0029:  ret
}");

            compilation = CreateCompilation(source, options: TestOptions.ReleaseExe);
            compilation.VerifyDiagnostics();
            compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("C.Main",
@"{
  // Code size       30 (0x1e)
  .maxstack  1
  .locals init (int V_0, //index
                int? V_1)
  IL_0000:  call       ""int? C.TryGet()""
  IL_0005:  stloc.1
  IL_0006:  ldloca.s   V_1
  IL_0008:  call       ""bool int?.HasValue.get""
  IL_000d:  brfalse.s  IL_001d
  IL_000f:  ldloca.s   V_1
  IL_0011:  call       ""int int?.GetValueOrDefault()""
  IL_0016:  stloc.0
  IL_0017:  ldloc.0
  IL_0018:  call       ""void System.Console.WriteLine(int)""
  IL_001d:  ret
}");
        }

        [Fact, WorkItem(19122, "https://github.com/dotnet/roslyn/issues/19122")]
        public void PatternCrash_01()
        {
            var source = @"using System;
using System.Collections.Generic;
using System.Linq;

public class Class2 : IDisposable
{
    public Class2(bool parameter = false)
    {
    }

    public void Dispose()
    {
    }
}

class X<T>
{
    IdentityAccessor<T> idAccessor = new IdentityAccessor<T>();
    void Y<U>() where U : T
    {
        // BUG: The following line is the problem
        if (GetT().FirstOrDefault(p => idAccessor.GetId(p) == Guid.Empty) is U u)
        {
        }
    }

    IEnumerable<T> GetT()
    {
        yield return default(T);
    }
}
class IdentityAccessor<T>
{
    public Guid GetId(T t)
    {
        return Guid.Empty;
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.DebugDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("X<T>.Y<U>",
@"{
  // Code size       61 (0x3d)
  .maxstack  3
  .locals init (U V_0, //u
                bool V_1,
                T V_2)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  call       ""System.Collections.Generic.IEnumerable<T> X<T>.GetT()""
  IL_0007:  ldarg.0
  IL_0008:  ldftn      ""bool X<T>.<Y>b__1_0<U>(T)""
  IL_000e:  newobj     ""System.Func<T, bool>..ctor(object, System.IntPtr)""
  IL_0013:  call       ""T System.Linq.Enumerable.FirstOrDefault<T>(System.Collections.Generic.IEnumerable<T>, System.Func<T, bool>)""
  IL_0018:  stloc.2
  IL_0019:  ldloc.2
  IL_001a:  box        ""T""
  IL_001f:  isinst     ""U""
  IL_0024:  brfalse.s  IL_0035
  IL_0026:  ldloc.2
  IL_0027:  box        ""T""
  IL_002c:  unbox.any  ""U""
  IL_0031:  stloc.0
  IL_0032:  ldc.i4.1
  IL_0033:  br.s       IL_0036
  IL_0035:  ldc.i4.0
  IL_0036:  stloc.1
  IL_0037:  ldloc.1
  IL_0038:  brfalse.s  IL_003c
  IL_003a:  nop
  IL_003b:  nop
  IL_003c:  ret
}");
        }

        [Fact, WorkItem(24522, "https://github.com/dotnet/roslyn/issues/24522")]
        public void IgnoreDeclaredConversion_01()
        {
            var source =
@"class Base<T>
{
    public static implicit operator Derived(Base<T> obj)
    {
        return new Derived();
    }
}

class Derived : Base<object>
{
}

class Program
{
    static void Main(string[] args)
    {
        Base<object> x = new Derived();
        System.Console.WriteLine(x is Derived);
        System.Console.WriteLine(x is Derived y);
        switch (x)
        {
            case Derived z: System.Console.WriteLine(true); break;
        }
        System.Console.WriteLine(null != (x as Derived));
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.DebugExe, references: new[] { LinqAssemblyRef });
            compilation.VerifyDiagnostics();
            var expectedOutput =
@"True
True
True
True";
            var compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("Program.Main",
@"{
  // Code size       82 (0x52)
  .maxstack  2
  .locals init (Base<object> V_0, //x
                Derived V_1, //y
                Derived V_2, //z
                Base<object> V_3)
  IL_0000:  nop
  IL_0001:  newobj     ""Derived..ctor()""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  isinst     ""Derived""
  IL_000d:  ldnull
  IL_000e:  cgt.un
  IL_0010:  call       ""void System.Console.WriteLine(bool)""
  IL_0015:  nop
  IL_0016:  ldloc.0
  IL_0017:  isinst     ""Derived""
  IL_001c:  stloc.1
  IL_001d:  ldloc.1
  IL_001e:  ldnull
  IL_001f:  cgt.un
  IL_0021:  call       ""void System.Console.WriteLine(bool)""
  IL_0026:  nop
  IL_0027:  ldloc.0
  IL_0028:  stloc.3
  IL_0029:  ldloc.3
  IL_002a:  stloc.0
  IL_002b:  ldloc.0
  IL_002c:  isinst     ""Derived""
  IL_0031:  stloc.2
  IL_0032:  ldloc.2
  IL_0033:  brtrue.s   IL_0037
  IL_0035:  br.s       IL_0042
  IL_0037:  br.s       IL_0039
  IL_0039:  ldc.i4.1
  IL_003a:  call       ""void System.Console.WriteLine(bool)""
  IL_003f:  nop
  IL_0040:  br.s       IL_0042
  IL_0042:  ldloc.0
  IL_0043:  isinst     ""Derived""
  IL_0048:  ldnull
  IL_0049:  cgt.un
  IL_004b:  call       ""void System.Console.WriteLine(bool)""
  IL_0050:  nop
  IL_0051:  ret
}");
        }

        [Fact]
        public void DoublePattern01()
        {
            var source =
@"using System;
class Program
{
    static bool P1(double d) => d is double.NaN;
    static bool P2(float f) => f is float.NaN;
    static bool P3(double d) => d is 3.14d;
    static bool P4(float f) => f is 3.14f;
    static bool P5(object o)
    {
        switch (o)
        {
            case double.NaN: return true;
            case float.NaN: return true;
            case 3.14d: return true;
            case 3.14f: return true;
            default: return false;
        }
    }
    public static void Main(string[] args)
    {
        Console.Write(P1(double.NaN));
        Console.Write(P1(1.0));
        Console.Write(P2(float.NaN));
        Console.Write(P2(1.0f));
        Console.Write(P3(3.14));
        Console.Write(P3(double.NaN));
        Console.Write(P4(3.14f));
        Console.Write(P4(float.NaN));
        Console.Write(P5(double.NaN));
        Console.Write(P5(0.0d));
        Console.Write(P5(float.NaN));
        Console.Write(P5(0.0f));
        Console.Write(P5(3.14d));
        Console.Write(P5(125));
        Console.Write(P5(3.14f));
        Console.Write(P5(1.0f));
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.DebugExe);
            compilation.VerifyDiagnostics();
            var expectedOutput = @"TrueFalseTrueFalseTrueFalseTrueFalseTrueFalseTrueFalseTrueFalseTrueFalse";
            var compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("Program.P1",
@"{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""bool double.IsNaN(double)""
  IL_0006:  ret
}");
            compVerifier.VerifyIL("Program.P2",
@"{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""bool float.IsNaN(float)""
  IL_0006:  ret
}");
            compVerifier.VerifyIL("Program.P3",
@"{
  // Code size       13 (0xd)
  .maxstack  2
  IL_0000:  ldc.r8     3.14
  IL_0009:  ldarg.0
  IL_000a:  ceq
  IL_000c:  ret
}");
            compVerifier.VerifyIL("Program.P4",
@"{
  // Code size        9 (0x9)
  .maxstack  2
  IL_0000:  ldc.r4     3.14
  IL_0005:  ldarg.0
  IL_0006:  ceq
  IL_0008:  ret
}");
            compVerifier.VerifyIL("Program.P5",
@"{
  // Code size       98 (0x62)
  .maxstack  2
  .locals init (double V_0,
                float V_1,
                object V_2,
                bool V_3)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.2
  IL_0003:  ldloc.2
  IL_0004:  starg.s    V_0
  IL_0006:  ldarg.0
  IL_0007:  isinst     ""double""
  IL_000c:  brfalse.s  IL_002b
  IL_000e:  ldarg.0
  IL_000f:  unbox.any  ""double""
  IL_0014:  stloc.0
  IL_0015:  ldloc.0
  IL_0016:  call       ""bool double.IsNaN(double)""
  IL_001b:  brtrue.s   IL_004c
  IL_001d:  ldc.r8     3.14
  IL_0026:  ldloc.0
  IL_0027:  beq.s      IL_0054
  IL_0029:  br.s       IL_005c
  IL_002b:  ldarg.0
  IL_002c:  isinst     ""float""
  IL_0031:  brfalse.s  IL_005c
  IL_0033:  ldarg.0
  IL_0034:  unbox.any  ""float""
  IL_0039:  stloc.1
  IL_003a:  ldloc.1
  IL_003b:  call       ""bool float.IsNaN(float)""
  IL_0040:  brtrue.s   IL_0050
  IL_0042:  ldc.r4     3.14
  IL_0047:  ldloc.1
  IL_0048:  beq.s      IL_0058
  IL_004a:  br.s       IL_005c
  IL_004c:  ldc.i4.1
  IL_004d:  stloc.3
  IL_004e:  br.s       IL_0060
  IL_0050:  ldc.i4.1
  IL_0051:  stloc.3
  IL_0052:  br.s       IL_0060
  IL_0054:  ldc.i4.1
  IL_0055:  stloc.3
  IL_0056:  br.s       IL_0060
  IL_0058:  ldc.i4.1
  IL_0059:  stloc.3
  IL_005a:  br.s       IL_0060
  IL_005c:  ldc.i4.0
  IL_005d:  stloc.3
  IL_005e:  br.s       IL_0060
  IL_0060:  ldloc.3
  IL_0061:  ret
}");
        }

        [Fact]
        public void DecimalEquality()
        {
            // demonstrate that pattern-matching against a decimal constant is
            // at least as efficient as simply using ==
            var source =
@"using System;
public class C
{
    public static void Main()
    {
        Console.Write(M1(1.0m));
        Console.Write(M2(1.0m));
        Console.Write(M1(2.0m));
        Console.Write(M2(2.0m));
    }

    static int M1(decimal d)
    {
        if (M(d) is 1.0m) return 1;
        return 0;
    }

    static int M2(decimal d)
    {
        if (M(d) == 1.0m) return 1;
        return 0;
    }

    public static decimal M(decimal d)
    {
        return d;
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.DebugExe);
            compilation.VerifyDiagnostics();
            var expectedOutput = @"1100";
            var compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("C.M1",
@"{
  // Code size       39 (0x27)
  .maxstack  5
  .locals init (bool V_0,
                decimal V_1,
                int V_2)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  call       ""decimal C.M(decimal)""
  IL_0007:  stloc.1
  IL_0008:  ldc.i4.s   10
  IL_000a:  ldc.i4.0
  IL_000b:  ldc.i4.0
  IL_000c:  ldc.i4.0
  IL_000d:  ldc.i4.1
  IL_000e:  newobj     ""decimal..ctor(int, int, int, bool, byte)""
  IL_0013:  ldloc.1
  IL_0014:  call       ""bool decimal.op_Equality(decimal, decimal)""
  IL_0019:  stloc.0
  IL_001a:  ldloc.0
  IL_001b:  brfalse.s  IL_0021
  IL_001d:  ldc.i4.1
  IL_001e:  stloc.2
  IL_001f:  br.s       IL_0025
  IL_0021:  ldc.i4.0
  IL_0022:  stloc.2
  IL_0023:  br.s       IL_0025
  IL_0025:  ldloc.2
  IL_0026:  ret
}");
            compVerifier.VerifyIL("C.M2",
@"{
  // Code size       37 (0x25)
  .maxstack  6
  .locals init (bool V_0,
                int V_1)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  call       ""decimal C.M(decimal)""
  IL_0007:  ldc.i4.s   10
  IL_0009:  ldc.i4.0
  IL_000a:  ldc.i4.0
  IL_000b:  ldc.i4.0
  IL_000c:  ldc.i4.1
  IL_000d:  newobj     ""decimal..ctor(int, int, int, bool, byte)""
  IL_0012:  call       ""bool decimal.op_Equality(decimal, decimal)""
  IL_0017:  stloc.0
  IL_0018:  ldloc.0
  IL_0019:  brfalse.s  IL_001f
  IL_001b:  ldc.i4.1
  IL_001c:  stloc.1
  IL_001d:  br.s       IL_0023
  IL_001f:  ldc.i4.0
  IL_0020:  stloc.1
  IL_0021:  br.s       IL_0023
  IL_0023:  ldloc.1
  IL_0024:  ret
}");

            compilation = CreateCompilation(source, options: TestOptions.ReleaseExe);
            compilation.VerifyDiagnostics();
            compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("C.M1",
@"{
  // Code size       30 (0x1e)
  .maxstack  5
  .locals init (decimal V_0)
  IL_0000:  ldarg.0
  IL_0001:  call       ""decimal C.M(decimal)""
  IL_0006:  stloc.0
  IL_0007:  ldc.i4.s   10
  IL_0009:  ldc.i4.0
  IL_000a:  ldc.i4.0
  IL_000b:  ldc.i4.0
  IL_000c:  ldc.i4.1
  IL_000d:  newobj     ""decimal..ctor(int, int, int, bool, byte)""
  IL_0012:  ldloc.0
  IL_0013:  call       ""bool decimal.op_Equality(decimal, decimal)""
  IL_0018:  brfalse.s  IL_001c
  IL_001a:  ldc.i4.1
  IL_001b:  ret
  IL_001c:  ldc.i4.0
  IL_001d:  ret
}");
            compVerifier.VerifyIL("C.M2",
@"{
  // Code size       28 (0x1c)
  .maxstack  6
  IL_0000:  ldarg.0
  IL_0001:  call       ""decimal C.M(decimal)""
  IL_0006:  ldc.i4.s   10
  IL_0008:  ldc.i4.0
  IL_0009:  ldc.i4.0
  IL_000a:  ldc.i4.0
  IL_000b:  ldc.i4.1
  IL_000c:  newobj     ""decimal..ctor(int, int, int, bool, byte)""
  IL_0011:  call       ""bool decimal.op_Equality(decimal, decimal)""
  IL_0016:  brfalse.s  IL_001a
  IL_0018:  ldc.i4.1
  IL_0019:  ret
  IL_001a:  ldc.i4.0
  IL_001b:  ret
}");
        }

        [Fact, WorkItem(16878, "https://github.com/dotnet/roslyn/issues/16878")]
        public void RedundantNullCheck()
        {
            var source =
@"public class C
{
    static int M1(bool? b1, bool b2)
    {
        switch (b1)
        {
            case null:
                return 1;
            case var _ when b2:
                return 2;
            case true:
                return 3;
            case false:
                return 4;
        }
    }

    static int M2(object o, bool b)
    {
        switch (o)
        {
            case string a when b:
                return 1;
            case string a:
                return 2;
        }
        return 3;
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("C.M1",
@"{
  // Code size       35 (0x23)
  .maxstack  1
  .locals init (bool? V_0)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  call       ""bool bool?.HasValue.get""
  IL_0009:  brfalse.s  IL_0018
  IL_000b:  br.s       IL_001a
  IL_000d:  ldloca.s   V_0
  IL_000f:  call       ""bool bool?.GetValueOrDefault()""
  IL_0014:  brtrue.s   IL_001f
  IL_0016:  br.s       IL_0021
  IL_0018:  ldc.i4.1
  IL_0019:  ret
  IL_001a:  ldarg.1
  IL_001b:  brfalse.s  IL_000d
  IL_001d:  ldc.i4.2
  IL_001e:  ret
  IL_001f:  ldc.i4.3
  IL_0020:  ret
  IL_0021:  ldc.i4.4
  IL_0022:  ret
}");
            compVerifier.VerifyIL("C.M2",
@"{
  // Code size       19 (0x13)
  .maxstack  1
  .locals init (string V_0)
  IL_0000:  ldarg.0
  IL_0001:  isinst     ""string""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brfalse.s  IL_0011
  IL_000a:  ldarg.1
  IL_000b:  brfalse.s  IL_000f
  IL_000d:  ldc.i4.1
  IL_000e:  ret
  IL_000f:  ldc.i4.2
  IL_0010:  ret
  IL_0011:  ldc.i4.3
  IL_0012:  ret
}");
        }

        [Fact, WorkItem(12813, "https://github.com/dotnet/roslyn/issues/12813")]
        public void NoBoxingOnIntegerConstantPattern()
        {
            var source =
@"public class C
{
    static bool M1(int x)
    {
        return x is 42;
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("C.M1",
@"{
  // Code size        6 (0x6)
  .maxstack  2
  IL_0000:  ldc.i4.s   42
  IL_0002:  ldarg.0
  IL_0003:  ceq
  IL_0005:  ret
}");
        }

        [Fact, WorkItem(22654, "https://github.com/dotnet/roslyn/issues/22654")]
        public void NoRedundantTypeCheck()
        {
            var source =
@"using System;
public class C
{
    public void SwitchBasedPatternMatching(object o)
    {
        switch (o)
        {
            case int n when n == 1:
                Console.WriteLine(""1""); break;
            case string s:
                Console.WriteLine(""s""); break;
            case int n when n == 2:
                Console.WriteLine(""2""); break;
        }
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("C.SwitchBasedPatternMatching",
@"{
  // Code size       71 (0x47)
  .maxstack  2
  .locals init (int V_0,
                string V_1,
                object V_2)
  IL_0000:  ldarg.1
  IL_0001:  stloc.2
  IL_0002:  ldloc.2
  IL_0003:  isinst     ""int""
  IL_0008:  brfalse.s  IL_0013
  IL_000a:  ldloc.2
  IL_000b:  unbox.any  ""int""
  IL_0010:  stloc.0
  IL_0011:  br.s       IL_001e
  IL_0013:  ldloc.2
  IL_0014:  isinst     ""string""
  IL_0019:  stloc.1
  IL_001a:  ldloc.1
  IL_001b:  brtrue.s   IL_002d
  IL_001d:  ret
  IL_001e:  ldloc.0
  IL_001f:  ldc.i4.1
  IL_0020:  bne.un.s   IL_0038
  IL_0022:  ldstr      ""1""
  IL_0027:  call       ""void System.Console.WriteLine(string)""
  IL_002c:  ret
  IL_002d:  ldstr      ""s""
  IL_0032:  call       ""void System.Console.WriteLine(string)""
  IL_0037:  ret
  IL_0038:  ldloc.0
  IL_0039:  ldc.i4.2
  IL_003a:  bne.un.s   IL_0046
  IL_003c:  ldstr      ""2""
  IL_0041:  call       ""void System.Console.WriteLine(string)""
  IL_0046:  ret
}");
        }

        [Fact, WorkItem(15437, "https://github.com/dotnet/roslyn/issues/15437")]
        public void IsTypeDiscard()
        {
            var source =
@"public class C
{
    public bool IsString(object o)
    {
        return o is string _;
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("C.IsString",
@"{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldarg.1
  IL_0001:  isinst     ""string""
  IL_0006:  ldnull
  IL_0007:  cgt.un
  IL_0009:  ret
}");
        }

        [Fact, WorkItem(19150, "https://github.com/dotnet/roslyn/issues/19150")]
        public void RedundantHasValue()
        {
            var source =
@"using System;
public class C
{
    public static void M(int? x)
    {
        switch (x)
        {
            case int i:
                Console.Write(i);
                break;
            case null:
                Console.Write(""null"");
                break;
        }
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("C.M",
@"{
  // Code size       33 (0x21)
  .maxstack  1
  IL_0000:  ldarga.s   V_0
  IL_0002:  call       ""bool int?.HasValue.get""
  IL_0007:  brfalse.s  IL_0016
  IL_0009:  ldarga.s   V_0
  IL_000b:  call       ""int int?.GetValueOrDefault()""
  IL_0010:  call       ""void System.Console.Write(int)""
  IL_0015:  ret
  IL_0016:  ldstr      ""null""
  IL_001b:  call       ""void System.Console.Write(string)""
  IL_0020:  ret
}");
        }

        [Fact, WorkItem(19153, "https://github.com/dotnet/roslyn/issues/19153")]
        public void RedundantBox()
        {
            var source = @"using System;
public class C
{
    public static void M<T, U>(U x) where T : U
    {
        // when T is not known to be a reference type, there is an unboxing conversion from
        // a type parameter U to T, provided T depends on U.
        switch (x)
        {
            case T i:
                Console.Write(i);
                break;
        }
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("C.M<T, U>(U)",
@"{
  // Code size       35 (0x23)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  box        ""U""
  IL_0006:  isinst     ""T""
  IL_000b:  brfalse.s  IL_0022
  IL_000d:  ldarg.0
  IL_000e:  box        ""U""
  IL_0013:  unbox.any  ""T""
  IL_0018:  box        ""T""
  IL_001d:  call       ""void System.Console.Write(object)""
  IL_0022:  ret
}");
        }

        [Fact, WorkItem(20641, "https://github.com/dotnet/roslyn/issues/20641")]
        public void PatternsVsAs01()
        {
            var source = @"using System.Collections;
using System.Collections.Generic;

class Program
{
    static void Main() { }

    internal static bool TryGetCount1<T>(IEnumerable<T> source, out int count)
    {
        ICollection nonGeneric = source as ICollection;
        if (nonGeneric != null)
        {
            count = nonGeneric.Count;
            return true;
        }

        ICollection<T> generic = source as ICollection<T>;
        if (generic != null)
        {
            count = generic.Count;
            return true;
        }

        count = -1;
        return false;
    }

    internal static bool TryGetCount2<T>(IEnumerable<T> source, out int count)
    {
        switch (source)
        {
            case ICollection nonGeneric:
                count = nonGeneric.Count;
                return true;

            case ICollection<T> generic:
                count = generic.Count;
                return true;

            default:
                count = -1;
                return false;
        }
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("Program.TryGetCount1<T>",
@"{
  // Code size       45 (0x2d)
  .maxstack  2
  .locals init (System.Collections.ICollection V_0, //nonGeneric
                System.Collections.Generic.ICollection<T> V_1) //generic
  IL_0000:  ldarg.0
  IL_0001:  isinst     ""System.Collections.ICollection""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brfalse.s  IL_0014
  IL_000a:  ldarg.1
  IL_000b:  ldloc.0
  IL_000c:  callvirt   ""int System.Collections.ICollection.Count.get""
  IL_0011:  stind.i4
  IL_0012:  ldc.i4.1
  IL_0013:  ret
  IL_0014:  ldarg.0
  IL_0015:  isinst     ""System.Collections.Generic.ICollection<T>""
  IL_001a:  stloc.1
  IL_001b:  ldloc.1
  IL_001c:  brfalse.s  IL_0028
  IL_001e:  ldarg.1
  IL_001f:  ldloc.1
  IL_0020:  callvirt   ""int System.Collections.Generic.ICollection<T>.Count.get""
  IL_0025:  stind.i4
  IL_0026:  ldc.i4.1
  IL_0027:  ret
  IL_0028:  ldarg.1
  IL_0029:  ldc.i4.m1
  IL_002a:  stind.i4
  IL_002b:  ldc.i4.0
  IL_002c:  ret
}");
            compVerifier.VerifyIL("Program.TryGetCount2<T>",
@"{
  // Code size       47 (0x2f)
  .maxstack  2
  .locals init (System.Collections.ICollection V_0, //nonGeneric
                System.Collections.Generic.ICollection<T> V_1) //generic
  IL_0000:  ldarg.0
  IL_0001:  isinst     ""System.Collections.ICollection""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brtrue.s   IL_0016
  IL_000a:  ldarg.0
  IL_000b:  isinst     ""System.Collections.Generic.ICollection<T>""
  IL_0010:  stloc.1
  IL_0011:  ldloc.1
  IL_0012:  brtrue.s   IL_0020
  IL_0014:  br.s       IL_002a
  IL_0016:  ldarg.1
  IL_0017:  ldloc.0
  IL_0018:  callvirt   ""int System.Collections.ICollection.Count.get""
  IL_001d:  stind.i4
  IL_001e:  ldc.i4.1
  IL_001f:  ret
  IL_0020:  ldarg.1
  IL_0021:  ldloc.1
  IL_0022:  callvirt   ""int System.Collections.Generic.ICollection<T>.Count.get""
  IL_0027:  stind.i4
  IL_0028:  ldc.i4.1
  IL_0029:  ret
  IL_002a:  ldarg.1
  IL_002b:  ldc.i4.m1
  IL_002c:  stind.i4
  IL_002d:  ldc.i4.0
  IL_002e:  ret
}");
        }

        [Fact, WorkItem(20641, "https://github.com/dotnet/roslyn/issues/20641")]
        public void PatternsVsAs02()
        {
            var source = @"using System.Collections;
class Program
{
    static void Main() { }

    internal static bool IsEmpty1(IEnumerable source)
    {
        var c = source as ICollection;
        return c != null && c.Count > 0;
    }

    internal static bool IsEmpty2(IEnumerable source)
    {
        return source is ICollection c && c.Count > 0;
    }

}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("Program.IsEmpty1",
@"{
  // Code size       22 (0x16)
  .maxstack  2
  .locals init (System.Collections.ICollection V_0) //c
  IL_0000:  ldarg.0
  IL_0001:  isinst     ""System.Collections.ICollection""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brfalse.s  IL_0014
  IL_000a:  ldloc.0
  IL_000b:  callvirt   ""int System.Collections.ICollection.Count.get""
  IL_0010:  ldc.i4.0
  IL_0011:  cgt
  IL_0013:  ret
  IL_0014:  ldc.i4.0
  IL_0015:  ret
}");
            compVerifier.VerifyIL("Program.IsEmpty2",
@"{
  // Code size       22 (0x16)
  .maxstack  2
  .locals init (System.Collections.ICollection V_0) //c
  IL_0000:  ldarg.0
  IL_0001:  isinst     ""System.Collections.ICollection""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brfalse.s  IL_0014
  IL_000a:  ldloc.0
  IL_000b:  callvirt   ""int System.Collections.ICollection.Count.get""
  IL_0010:  ldc.i4.0
  IL_0011:  cgt
  IL_0013:  ret
  IL_0014:  ldc.i4.0
  IL_0015:  ret
}");
        }

        [Fact]
        [WorkItem(24550, "https://github.com/dotnet/roslyn/issues/24550")]
        [WorkItem(1284, "https://github.com/dotnet/csharplang/issues/1284")]
        public void ConstantPatternVsUnconstrainedTypeParameter01()
        {
            var source =
@"using System;
class Program
{
    static void Main()
    {
        Console.WriteLine(Test1<object>(null));
        Console.WriteLine(Test1<int>(1));
        Console.WriteLine(Test1<int?>(null));
        Console.WriteLine(Test1<int?>(1));

        Console.WriteLine(Test2<object>(0));
        Console.WriteLine(Test2<int>(1));
        Console.WriteLine(Test2<int?>(0));
        Console.WriteLine(Test2<string>(""frog""));

        Console.WriteLine(Test3<object>(""frog""));
        Console.WriteLine(Test3<int>(1));
        Console.WriteLine(Test3<string>(""frog""));
        Console.WriteLine(Test3<int?>(1));
    }

    public static bool Test1<T>(T t)
    {
        return t is null;
    }
    public static bool Test2<T>(T t)
    {
        return t is 0;
    }
    public static bool Test3<T>(T t)
    {
        return t is ""frog"";
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseExe);
            compilation.VerifyDiagnostics();
            var expectedOutput =
@"True
False
True
False
True
False
True
False
True
False
True
False
";
            var compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("Program.Test1<T>(T)",
@"{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  box        ""T""
  IL_0006:  ldnull
  IL_0007:  ceq
  IL_0009:  ret
}");
            compVerifier.VerifyIL("Program.Test2<T>(T)",
@"{
  // Code size       32 (0x20)
  .maxstack  2
  .locals init (int V_0)
  IL_0000:  ldarg.0
  IL_0001:  box        ""T""
  IL_0006:  isinst     ""int""
  IL_000b:  brfalse.s  IL_001e
  IL_000d:  ldarg.0
  IL_000e:  box        ""T""
  IL_0013:  unbox.any  ""int""
  IL_0018:  stloc.0
  IL_0019:  ldloc.0
  IL_001a:  ldc.i4.0
  IL_001b:  ceq
  IL_001d:  ret
  IL_001e:  ldc.i4.0
  IL_001f:  ret
}");
            compVerifier.VerifyIL("Program.Test3<T>(T)",
@"{
  // Code size       29 (0x1d)
  .maxstack  2
  .locals init (string V_0)
  IL_0000:  ldarg.0
  IL_0001:  box        ""T""
  IL_0006:  isinst     ""string""
  IL_000b:  stloc.0
  IL_000c:  ldloc.0
  IL_000d:  brfalse.s  IL_001b
  IL_000f:  ldstr      ""frog""
  IL_0014:  ldloc.0
  IL_0015:  call       ""bool string.op_Equality(string, string)""
  IL_001a:  ret
  IL_001b:  ldc.i4.0
  IL_001c:  ret
}");
        }

        [Fact]
        [WorkItem(24550, "https://github.com/dotnet/roslyn/issues/24550")]
        [WorkItem(1284, "https://github.com/dotnet/csharplang/issues/1284")]
        public void ConstantPatternVsUnconstrainedTypeParameter02()
        {
            var source =
@"class C<T>
{
    internal struct S { }
    static bool Test1(S s)
    {
        return s is null;
    }
    static bool Test2(S s)
    {
        return s is 1;
    }
}";
            var compilation = CreateCompilation(source, options: TestOptions.ReleaseDll);
            compilation.VerifyDiagnostics();
            var compVerifier = CompileAndVerify(compilation);
            compVerifier.VerifyIL("C<T>.Test1(C<T>.S)",
@"");
            compVerifier.VerifyIL("C<T>.Test2(C<T>.S)",
@"{
  // Code size       32 (0x20)
  .maxstack  2
  .locals init (int V_0)
  IL_0000:  ldarg.0
  IL_0001:  box        ""C<T>.S""
  IL_0006:  isinst     ""int""
  IL_000b:  brfalse.s  IL_001e
  IL_000d:  ldarg.0
  IL_000e:  box        ""C<T>.S""
  IL_0013:  unbox.any  ""int""
  IL_0018:  stloc.0
  IL_0019:  ldc.i4.1
  IL_001a:  ldloc.0
  IL_001b:  ceq
  IL_001d:  ret
  IL_001e:  ldc.i4.0
  IL_001f:  ret
}");
        }
    }
}
