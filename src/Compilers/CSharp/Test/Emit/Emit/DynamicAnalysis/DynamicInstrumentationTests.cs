﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.DynamicAnalysis.UnitTests
{
    public class DynamicInstrumentationTests : CSharpTestBase
    {
        const string InstrumentationHelperSource = @"
namespace Microsoft.CodeAnalysis.Runtime
{
    public static class Instrumentation
    {
        private static bool[][] _payloads;
        private static System.Guid _mvid;

        public static bool[] CreatePayload(System.Guid mvid, int methodIndex, ref bool[] payload, int payloadLength)
        {
            if (_mvid != mvid)
            {
                _payloads = new bool[100][];
                _mvid = mvid;
            }

            if (System.Threading.Interlocked.CompareExchange(ref payload, new bool[payloadLength], null) == null)
            {
                _payloads[methodIndex] = payload;
                return payload;
            }

            return _payloads[methodIndex];
        }

        public static void FlushPayload()
        {
            Console.WriteLine(""Flushing"");
            if (_payloads == null)
            {
                return;
            }
            for (int i = 0; i < _payloads.Length; i++)
            {
                bool[] payload = _payloads[i];
                if (payload != null)
                {
                    Console.WriteLine(i);
                    for (int j = 0; j < payload.Length; j++)
                    {
                        Console.WriteLine(payload[j]);
                        payload[j] = false;
                    }
                }
            }
        }
    }
}
";

        [Fact]
        public void HelpersInstrumentation()
        {
            string source = @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }
}
";
            string expectedOutput = @"Flushing
1
True
True
4
True
True
False
True
True
True
True
True
True
True
True
True
True
";
            string expectedCreatePayloadIL = @"{
  // Code size       66 (0x42)
  .maxstack  3
  IL_0000:  ldsfld     ""System.Guid Microsoft.CodeAnalysis.Runtime.Instrumentation._mvid""
  IL_0005:  ldarg.0
  IL_0006:  call       ""bool System.Guid.op_Inequality(System.Guid, System.Guid)""
  IL_000b:  brfalse.s  IL_001f
  IL_000d:  ldc.i4.s   100
  IL_000f:  newarr     ""bool[]""
  IL_0014:  stsfld     ""bool[][] Microsoft.CodeAnalysis.Runtime.Instrumentation._payloads""
  IL_0019:  ldarg.0
  IL_001a:  stsfld     ""System.Guid Microsoft.CodeAnalysis.Runtime.Instrumentation._mvid""
  IL_001f:  ldarg.2
  IL_0020:  ldarg.3
  IL_0021:  newarr     ""bool""
  IL_0026:  ldnull
  IL_0027:  call       ""bool[] System.Threading.Interlocked.CompareExchange<bool[]>(ref bool[], bool[], bool[])""
  IL_002c:  brtrue.s   IL_003a
  IL_002e:  ldsfld     ""bool[][] Microsoft.CodeAnalysis.Runtime.Instrumentation._payloads""
  IL_0033:  ldarg.1
  IL_0034:  ldarg.2
  IL_0035:  ldind.ref
  IL_0036:  stelem.ref
  IL_0037:  ldarg.2
  IL_0038:  ldind.ref
  IL_0039:  ret
  IL_003a:  ldsfld     ""bool[][] Microsoft.CodeAnalysis.Runtime.Instrumentation._payloads""
  IL_003f:  ldarg.1
  IL_0040:  ldelem.ref
  IL_0041:  ret
}";

            string expectedFlushPayloadIL = @"{
  // Code size      184 (0xb8)
  .maxstack  4
  .locals init (bool[] V_0,
                int V_1, //i
                bool[] V_2, //payload
                int V_3) //j
  IL_0000:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_0005:  ldtoken    ""void Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload()""
  IL_000a:  ldelem.ref
  IL_000b:  stloc.0
  IL_000c:  ldloc.0
  IL_000d:  brtrue.s   IL_0030
  IL_000f:  ldsfld     ""System.Guid <PrivateImplementationDetails>.MVID""
  IL_0014:  ldtoken    ""void Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload()""
  IL_0019:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_001e:  ldtoken    ""void Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload()""
  IL_0023:  ldelema    ""bool[]""
  IL_0028:  ldc.i4.s   13
  IL_002a:  call       ""bool[] Microsoft.CodeAnalysis.Runtime.Instrumentation.CreatePayload(System.Guid, int, ref bool[], int)""
  IL_002f:  stloc.0
  IL_0030:  ldloc.0
  IL_0031:  ldc.i4.0
  IL_0032:  ldc.i4.1
  IL_0033:  stelem.i1
  IL_0034:  ldloc.0
  IL_0035:  ldc.i4.1
  IL_0036:  ldc.i4.1
  IL_0037:  stelem.i1
  IL_0038:  ldstr      ""Flushing""
  IL_003d:  call       ""void System.Console.WriteLine(string)""
  IL_0042:  ldloc.0
  IL_0043:  ldc.i4.3
  IL_0044:  ldc.i4.1
  IL_0045:  stelem.i1
  IL_0046:  ldsfld     ""bool[][] Microsoft.CodeAnalysis.Runtime.Instrumentation._payloads""
  IL_004b:  brtrue.s   IL_0052
  IL_004d:  ldloc.0
  IL_004e:  ldc.i4.2
  IL_004f:  ldc.i4.1
  IL_0050:  stelem.i1
  IL_0051:  ret
  IL_0052:  ldloc.0
  IL_0053:  ldc.i4.4
  IL_0054:  ldc.i4.1
  IL_0055:  stelem.i1
  IL_0056:  ldc.i4.0
  IL_0057:  stloc.1
  IL_0058:  br.s       IL_00ad
  IL_005a:  ldloc.0
  IL_005b:  ldc.i4.6
  IL_005c:  ldc.i4.1
  IL_005d:  stelem.i1
  IL_005e:  ldsfld     ""bool[][] Microsoft.CodeAnalysis.Runtime.Instrumentation._payloads""
  IL_0063:  ldloc.1
  IL_0064:  ldelem.ref
  IL_0065:  stloc.2
  IL_0066:  ldloc.0
  IL_0067:  ldc.i4.s   12
  IL_0069:  ldc.i4.1
  IL_006a:  stelem.i1
  IL_006b:  ldloc.2
  IL_006c:  brfalse.s  IL_00a5
  IL_006e:  ldloc.0
  IL_006f:  ldc.i4.7
  IL_0070:  ldc.i4.1
  IL_0071:  stelem.i1
  IL_0072:  ldloc.1
  IL_0073:  call       ""void System.Console.WriteLine(int)""
  IL_0078:  ldloc.0
  IL_0079:  ldc.i4.8
  IL_007a:  ldc.i4.1
  IL_007b:  stelem.i1
  IL_007c:  ldc.i4.0
  IL_007d:  stloc.3
  IL_007e:  br.s       IL_009f
  IL_0080:  ldloc.0
  IL_0081:  ldc.i4.s   10
  IL_0083:  ldc.i4.1
  IL_0084:  stelem.i1
  IL_0085:  ldloc.2
  IL_0086:  ldloc.3
  IL_0087:  ldelem.u1
  IL_0088:  call       ""void System.Console.WriteLine(bool)""
  IL_008d:  ldloc.0
  IL_008e:  ldc.i4.s   11
  IL_0090:  ldc.i4.1
  IL_0091:  stelem.i1
  IL_0092:  ldloc.2
  IL_0093:  ldloc.3
  IL_0094:  ldc.i4.0
  IL_0095:  stelem.i1
  IL_0096:  ldloc.0
  IL_0097:  ldc.i4.s   9
  IL_0099:  ldc.i4.1
  IL_009a:  stelem.i1
  IL_009b:  ldloc.3
  IL_009c:  ldc.i4.1
  IL_009d:  add
  IL_009e:  stloc.3
  IL_009f:  ldloc.3
  IL_00a0:  ldloc.2
  IL_00a1:  ldlen
  IL_00a2:  conv.i4
  IL_00a3:  blt.s      IL_0080
  IL_00a5:  ldloc.0
  IL_00a6:  ldc.i4.5
  IL_00a7:  ldc.i4.1
  IL_00a8:  stelem.i1
  IL_00a9:  ldloc.1
  IL_00aa:  ldc.i4.1
  IL_00ab:  add
  IL_00ac:  stloc.1
  IL_00ad:  ldloc.1
  IL_00ae:  ldsfld     ""bool[][] Microsoft.CodeAnalysis.Runtime.Instrumentation._payloads""
  IL_00b3:  ldlen
  IL_00b4:  conv.i4
  IL_00b5:  blt.s      IL_005a
  IL_00b7:  ret
}";
            CompilationVerifier verifier = CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput);
            verifier.VerifyIL("Microsoft.CodeAnalysis.Runtime.Instrumentation.CreatePayload", expectedCreatePayloadIL);
            verifier.VerifyIL("Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload", expectedFlushPayloadIL);
        }

        [Fact]
        public void GotoCoverage()
        {
            string source = @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }

    static void TestMain()
    {
        Console.WriteLine(""foo"");
        goto bar;
        Console.Write(""you won't see me"");
        bar: Console.WriteLine(""bar"");
        Fred();
        return;
    }

    static void Wilma()
    {
        Betty(true);
        Barney(true);
        Barney(false);
        Betty(true);
    }

    static int Barney(bool b)
    {
        if (b)
            return 10;
        if (b)
            return 100;
        return 20;
    }

    static int Betty(bool b)
    {
        if (b)
            return 30;
        if (b)
            return 100;
        return 40;
    }

    static void Fred()
    {
        Wilma();
    }
}
";
            string expectedOutput = @"foo
bar
Flushing
1
True
True
True
2
True
True
True
False
True
True
True
3
True
True
True
True
True
4
True
True
True
False
True
True
5
True
True
True
False
False
False
6
True
True
9
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            string expectedBarneyIL = @"{
  // Code size       86 (0x56)
  .maxstack  4
  .locals init (bool[] V_0)
  IL_0000:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_0005:  ldtoken    ""int Program.Barney(bool)""
  IL_000a:  ldelem.ref
  IL_000b:  stloc.0
  IL_000c:  ldloc.0
  IL_000d:  brtrue.s   IL_002f
  IL_000f:  ldsfld     ""System.Guid <PrivateImplementationDetails>.MVID""
  IL_0014:  ldtoken    ""int Program.Barney(bool)""
  IL_0019:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_001e:  ldtoken    ""int Program.Barney(bool)""
  IL_0023:  ldelema    ""bool[]""
  IL_0028:  ldc.i4.6
  IL_0029:  call       ""bool[] Microsoft.CodeAnalysis.Runtime.Instrumentation.CreatePayload(System.Guid, int, ref bool[], int)""
  IL_002e:  stloc.0
  IL_002f:  ldloc.0
  IL_0030:  ldc.i4.0
  IL_0031:  ldc.i4.1
  IL_0032:  stelem.i1
  IL_0033:  ldloc.0
  IL_0034:  ldc.i4.2
  IL_0035:  ldc.i4.1
  IL_0036:  stelem.i1
  IL_0037:  ldarg.0
  IL_0038:  brfalse.s  IL_0041
  IL_003a:  ldloc.0
  IL_003b:  ldc.i4.1
  IL_003c:  ldc.i4.1
  IL_003d:  stelem.i1
  IL_003e:  ldc.i4.s   10
  IL_0040:  ret
  IL_0041:  ldloc.0
  IL_0042:  ldc.i4.4
  IL_0043:  ldc.i4.1
  IL_0044:  stelem.i1
  IL_0045:  ldarg.0
  IL_0046:  brfalse.s  IL_004f
  IL_0048:  ldloc.0
  IL_0049:  ldc.i4.3
  IL_004a:  ldc.i4.1
  IL_004b:  stelem.i1
  IL_004c:  ldc.i4.s   100
  IL_004e:  ret
  IL_004f:  ldloc.0
  IL_0050:  ldc.i4.5
  IL_0051:  ldc.i4.1
  IL_0052:  stelem.i1
  IL_0053:  ldc.i4.s   20
  IL_0055:  ret
}";

            string expectedPIDStaticConstructorIL = @"{
  // Code size       33 (0x21)
  .maxstack  2
  IL_0000:  ldtoken    Max Method Token Index
  IL_0005:  ldc.i4.1
  IL_0006:  add
  IL_0007:  newarr     ""bool[]""
  IL_000c:  stsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_0011:  ldstr      ##MVID##
  IL_0016:  call       ""System.Guid System.Guid.Parse(string)""
  IL_001b:  stsfld     ""System.Guid <PrivateImplementationDetails>.MVID""
  IL_0020:  ret
}";

            CompilationVerifier verifier = CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput);
            verifier.VerifyIL("Program.Barney", expectedBarneyIL);
            verifier.VerifyIL(".cctor", expectedPIDStaticConstructorIL);
        }

        [Fact]
        public void MethodsOfGenericTypesCoverage()
        {
            string source = @"
using System;

class MyBox<T> where T : class
{
    readonly T _value;

    public MyBox(T value)
    {
        _value = value;
    }

    public T GetValue()
    {
        if (_value == null)
        {
            return null;
        }

        return _value;
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }

    static void TestMain()
    {
        MyBox<object> x = new MyBox<object>(null);
        Console.WriteLine(x.GetValue() == null ? ""null"" : x.GetValue().ToString());
        MyBox<string> s = new MyBox<string>(""Hello"");
        Console.WriteLine(s.GetValue() == null ? ""null"" : s.GetValue());
    }
}
";
            // All instrumentation points in method 2 are True because they are covered by at least one specialization.
            //
            // This test verifies that the payloads of methods of generic types are in terms of method definitions and
            // not method references -- the indices for the methods would be different for references.
            string expectedOutput = @"null
Hello
Flushing
1
True
True
2
True
True
True
True
3
True
True
True
4
True
True
True
True
True
7
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            string expectedReleaseGetValueIL = @"{
  // Code size       93 (0x5d)
  .maxstack  4
  .locals init (bool[] V_0,
                T V_1)
  IL_0000:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_0005:  ldtoken    ""T MyBox<T>.GetValue()""
  IL_000a:  ldelem.ref
  IL_000b:  stloc.0
  IL_000c:  ldloc.0
  IL_000d:  brtrue.s   IL_002f
  IL_000f:  ldsfld     ""System.Guid <PrivateImplementationDetails>.MVID""
  IL_0014:  ldtoken    ""T MyBox<T>.GetValue()""
  IL_0019:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_001e:  ldtoken    ""T MyBox<T>.GetValue()""
  IL_0023:  ldelema    ""bool[]""
  IL_0028:  ldc.i4.4
  IL_0029:  call       ""bool[] Microsoft.CodeAnalysis.Runtime.Instrumentation.CreatePayload(System.Guid, int, ref bool[], int)""
  IL_002e:  stloc.0
  IL_002f:  ldloc.0
  IL_0030:  ldc.i4.0
  IL_0031:  ldc.i4.1
  IL_0032:  stelem.i1
  IL_0033:  ldloc.0
  IL_0034:  ldc.i4.2
  IL_0035:  ldc.i4.1
  IL_0036:  stelem.i1
  IL_0037:  ldarg.0
  IL_0038:  ldfld      ""T MyBox<T>._value""
  IL_003d:  box        ""T""
  IL_0042:  brtrue.s   IL_0052
  IL_0044:  ldloc.0
  IL_0045:  ldc.i4.1
  IL_0046:  ldc.i4.1
  IL_0047:  stelem.i1
  IL_0048:  ldloca.s   V_1
  IL_004a:  initobj    ""T""
  IL_0050:  ldloc.1
  IL_0051:  ret
  IL_0052:  ldloc.0
  IL_0053:  ldc.i4.3
  IL_0054:  ldc.i4.1
  IL_0055:  stelem.i1
  IL_0056:  ldarg.0
  IL_0057:  ldfld      ""T MyBox<T>._value""
  IL_005c:  ret
}";

            string expectedDebugGetValueIL = @"{
  // Code size      105 (0x69)
  .maxstack  4
  .locals init (bool[] V_0,
                bool V_1,
                T V_2,
                T V_3)
  IL_0000:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_0005:  ldtoken    ""T MyBox<T>.GetValue()""
  IL_000a:  ldelem.ref
  IL_000b:  stloc.0
  IL_000c:  ldloc.0
  IL_000d:  brtrue.s   IL_002f
  IL_000f:  ldsfld     ""System.Guid <PrivateImplementationDetails>.MVID""
  IL_0014:  ldtoken    ""T MyBox<T>.GetValue()""
  IL_0019:  ldsfld     ""bool[][] <PrivateImplementationDetails>.PayloadRoot0""
  IL_001e:  ldtoken    ""T MyBox<T>.GetValue()""
  IL_0023:  ldelema    ""bool[]""
  IL_0028:  ldc.i4.4
  IL_0029:  call       ""bool[] Microsoft.CodeAnalysis.Runtime.Instrumentation.CreatePayload(System.Guid, int, ref bool[], int)""
  IL_002e:  stloc.0
  IL_002f:  ldloc.0
  IL_0030:  ldc.i4.0
  IL_0031:  ldc.i4.1
  IL_0032:  stelem.i1
  IL_0033:  ldloc.0
  IL_0034:  ldc.i4.2
  IL_0035:  ldc.i4.1
  IL_0036:  stelem.i1
  IL_0037:  ldarg.0
  IL_0038:  ldfld      ""T MyBox<T>._value""
  IL_003d:  box        ""T""
  IL_0042:  ldnull
  IL_0043:  ceq
  IL_0045:  stloc.1
  IL_0046:  ldloc.1
  IL_0047:  brfalse.s  IL_005a
  IL_0049:  nop
  IL_004a:  ldloc.0
  IL_004b:  ldc.i4.1
  IL_004c:  ldc.i4.1
  IL_004d:  stelem.i1
  IL_004e:  ldloca.s   V_2
  IL_0050:  initobj    ""T""
  IL_0056:  ldloc.2
  IL_0057:  stloc.3
  IL_0058:  br.s       IL_0067
  IL_005a:  ldloc.0
  IL_005b:  ldc.i4.3
  IL_005c:  ldc.i4.1
  IL_005d:  stelem.i1
  IL_005e:  ldarg.0
  IL_005f:  ldfld      ""T MyBox<T>._value""
  IL_0064:  stloc.3
  IL_0065:  br.s       IL_0067
  IL_0067:  ldloc.3
  IL_0068:  ret
}";

            CompilationVerifier verifier = CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput, options: TestOptions.ReleaseExe);
            verifier.VerifyIL("MyBox<T>.GetValue", expectedReleaseGetValueIL);
            
            verifier = CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput, options: TestOptions.DebugExe);
            verifier.VerifyIL("MyBox<T>.GetValue", expectedDebugGetValueIL);
        }

        [Fact]
        public void NonStaticImplicitBlockMethodsCoverage()
        {
            string source = @"
using System;

public class Program
{
    public int Prop { get; }

    public int Prop2 { get; } = 25;

    public int Prop3 { get; set; }                                              // Methods 3 and 4

    public Program()                                                            // Method 5
    {
        Prop = 12;
        Prop3 = 12;
        Prop2 = Prop3;
    }

    public static void Main(string[] args)                                      // Method 6
    {
        new Program();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }
}
";
            string expectedOutput = @"Flushing
3
True
True
4
True
True
5
True
True
True
True
6
True
True
True
8
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput, options: TestOptions.ReleaseExe);
            CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput, options: TestOptions.DebugExe);
        }

        [Fact]
        public void ImplicitBlockMethodsCoverage()
        {
            string source = @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }
    
    static void TestMain()
    {
        int x = Count;
        x += Prop;
        Prop = x;
        x += Prop2;
        Lambda(x, (y) => y + 1);
    }

    static int Function(int x) => x;

    static int Count => Function(44);

    static int Prop { get; set; }

    static int Prop2 { get; set; } = 12;

    static int Lambda(int x, Func<int, int> l)
    {
        return l(x);
    }
}
";
            // There is no entry for method '8' since it's a Prop2_set which is never called.
            string expectedOutput = @"Flushing
1
True
True
True
2
True
True
True
True
True
True
3
True
True
4
True
True
5
True
True
6
True
True
7
True
True
9
True
True
13
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput, options: TestOptions.ReleaseExe);
            CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput, options: TestOptions.DebugExe);
        }

        [Fact]
        public void MultipleDeclarationsCoverage()
        {
            string source = @"
using System;

public class Program
{
    public static void Main(string[] args)                                      // Method 1
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }

    static void TestMain()                                                      // Method 2
    {
        int x;
        int a, b;
        DoubleDeclaration(5);
        DoubleForDeclaration(5);
    }

    static int DoubleDeclaration(int x)                                         // Method 3
    {
        int c = x;
        int a, b;
        int f;

        a = b = f = c;
        int d = a, e = b;
        return d + e + f;
    }

    static int DoubleForDeclaration(int x)                                      // Method 4
    {
        for(int a = x, b = x; a + b < 10; a++)
        {
            Console.WriteLine(""Cannot get here."");
            x++;
        }

        return x;
    }
}
";
            string expectedOutput = @"Flushing
1
True
True
True
2
True
True
True
3
True
True
True
True
True
True
4
True
True
True
False
False
False
True
7
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            CompilationVerifier verifier = CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput);
        }

        [Fact]
        public void UsingAndFixedCoverage()
        {
            string source = @"
using System;
using System.IO;

public class Program
{
    public static void Main(string[] args)                                          // Method 1
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }

    static void TestMain()                                                          // Method 2
    {
        using (var memoryStream = new MemoryStream())
        {
            ;
        }

        using (MemoryStream s1 = new MemoryStream(), s2 = new MemoryStream())
        {
            ;
        }

        var otherStream = new MemoryStream();
        using (otherStream)
        {
            ;
        }

        unsafe
        {
            double[] a = { 1, 2, 3 };
            fixed(double* p = a)
            {
                ;
            }
            fixed(double* q = a, r = a)
            {
                ;
            }
        }
    }
}
";
            string expectedOutput = @"Flushing
1
True
True
True
2
True
True
True
True
True
True
True
True
True
True
True
True
True
True
True
5
True
True
False
True
True
True
True
True
True
True
True
True
True
";
           
            CompilationVerifier verifier = CompileAndVerify(source + InstrumentationHelperSource, options: TestOptions.UnsafeDebugExe, expectedOutput: expectedOutput);
        }

        [Fact]
        public void ManyStatementsCoverage()                                    // Method 3
        {
            string source = @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }

    static void TestMain()
    {
        VariousStatements(2);
        Empty();
    }

    static void VariousStatements(int z)
    {
        int x = z + 10;
        switch (z)
        {
            case 1:
                break;
            case 2:
                break;
            case 3:
                break;
            default:
                break;
        }

        if (x > 10)
        {
            x++;
        }
        else
        {
            x--;
        }

        for (int y = 0; y < 50; y++)
        {
            if (y < 30)
            {
                x++;
                continue;
            }
            else
                break;
        }

        int[] a = new int[] { 1, 2, 3, 4 };
        foreach (int i in a)
        {
            x++;
        }

        while (x < 100)
        {
            x++;
        }

        try
        {
            x++;
            if (x > 10)
            {
                throw new System.Exception();
            }
            x++;
        }
        catch (System.Exception e)
        {
            x++;
        }
        finally
        {
            x++;
        }

        lock (new object())
        {
            ;
        }

        Console.WriteLine(x);

        try
        {
            using ((System.IDisposable)new object())
            {
                ;
            }
        }
        catch (System.Exception e)
        {
        }

        // Include an infinite loop to make sure that a compiler optimization doesn't eliminate the instrumentation.
        while (true)
        {
            return;
        }
    }

    static void Empty()                                 // Method 4
    {
    }
}
";
            string expectedOutput = @"103
Flushing
1
True
True
True
2
True
True
True
3
True
True
False
True
False
False
True
True
False
True
True
True
True
True
True
True
True
True
True
True
True
True
True
True
False
True
True
True
True
True
False
True
True
True
4
True
7
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput);
        }

        [Fact]
        public void LambdaCoverage()
        {
            string source = @"
using System;

public class Program
{
    public static void Main(string[] args)                                  // Method 1
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();      // Method 2
    }

    static void TestMain()
    {
        int y = 5;
        Func<int, int> tester = (x) =>
        {
            while (x > 10)
            {
                return y;
            }

            return x;
        };

        y = 75;
        if (tester(20) > 50)
            Console.WriteLine(""OK"");
        else
            Console.WriteLine(""Bad"");
    }
}
";
            string expectedOutput = @"OK
Flushing
1
True
True
True
2
True
True
True
True
False
True
True
True
False
True
5
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput);
        }

        [Fact]
        public void AsyncCoverage()
        {
            string source = @"
using System;
using System.Threading.Tasks;

public class Program
{
    public static void Main(string[] args)
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }

    static void TestMain()
    {
        Console.WriteLine(Outer(""Goo"").Result);
    }

    async static Task<string> Outer(string s)
    {
        string s1 = await First(s);
        string s2 = await Second(s);

        return s1 + s2;
    }

    async static Task<string> First(string s)
    {
        string result = await Second(s) + ""Glue"";
        if (result.Length > 2)
            return result;
        else
            return ""Too short"";
    }

    async static Task<string> Second(string s)
    {
        string doubled = """";
        if (s.Length > 2)
            doubled = s + s;
        else
            doubled = ""HuhHuh"";
        return await Task.Factory.StartNew(() => doubled);
    }
}
";
            string expectedOutput = @"GooGooGlueGooGoo
Flushing
1
True
True
True
2
True
True
3
True
True
True
True
4
True
True
True
False
True
5
True
True
True
False
True
True
8
True
True
False
True
True
True
True
True
True
True
True
True
True
";

            CompileAndVerify(source + InstrumentationHelperSource, expectedOutput: expectedOutput);
        }

        [Fact]
        public void MissingMethodNeededForAnalysis()
        {
            string source = @"
namespace System
{
    public class Object { }  
    public struct Int32 { }  
    public struct Boolean { }  
    public class String { }  
    public class Exception { }  
    public class ValueType { }  
    public class Enum { }  
    public struct Void { }  
    public class Guid { }
}

public class Console
{
    public static void WriteLine(string s) { }
    public static void WriteLine(int i) { }
    public static void WriteLine(bool b) { }
}

public class Program
{
    public static void Main(string[] args)
    {
        TestMain();
        Microsoft.CodeAnalysis.Runtime.Instrumentation.FlushPayload();
    }

    static int TestMain()
    {
        return 3;
    }
}
";

            ImmutableArray<Diagnostic> diagnostics = CreateCompilation(source + InstrumentationHelperSource).GetEmitDiagnostics(EmitOptions.Default.WithInstrument("Test.Flag"));
            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Code == (int)ErrorCode.ERR_MissingPredefinedMember &&
                    diagnostic.Arguments[0].Equals("System.Guid") && diagnostic.Arguments[1].Equals("Parse"))
                {
                    return;
                }
            }

            Assert.True(false);
        }

        private CompilationVerifier CompileAndVerify(string source, string expectedOutput = null, CompilationOptions options = null)
        {
            return base.CompileAndVerify(source, expectedOutput: expectedOutput, additionalRefs: s_refs, options: (options ?? TestOptions.ReleaseExe).WithDeterministic(true), emitOptions: EmitOptions.Default.WithInstrument("Test.Flag"));
        }

        private static readonly MetadataReference[] s_refs = new[] { MscorlibRef_v4_0_30316_17626, SystemRef_v4_0_30319_17929, SystemCoreRef_v4_0_30319_17929 };
    }
}
