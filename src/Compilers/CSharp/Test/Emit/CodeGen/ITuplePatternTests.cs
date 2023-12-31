﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.CodeGen
{
    [CompilerTrait(CompilerFeature.Patterns)]
    public class ITuplePatternTests : EmitMetadataTestBase
    {
        protected CSharpCompilation CreatePatternCompilation(string source, CSharpCompilationOptions options = null)
        {
            return CreateCompilation(new[] { source, _iTupleSource }, options: options ?? TestOptions.ReleaseExe, parseOptions: TestOptions.RegularWithRecursivePatterns);
        }

        private const string _iTupleSource = @"
namespace System.Runtime.CompilerServices
{
    public interface ITuple
    {
        int Length { get; }
        object this[int index] { get; }
    }
}
";

        [Fact]
        public void ITupleFromObject_01()
        {
            var source =
@"using System;
using System.Runtime.CompilerServices;
public class C : ITuple
{
    int ITuple.Length => 3;
    object ITuple.this[int i] => i + 3;
    public static void Main()
    {
        Console.WriteLine(M(new C()));
    }
    private static bool M(object t)
    {
        return t is (3, 4, 5);
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics();
            var expectedOutput = @"True";
            var compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("C.M",
@"{
  // Code size       97 (0x61)
  .maxstack  2
  .locals init (System.Runtime.CompilerServices.ITuple V_0,
                object V_1,
                object V_2,
                object V_3)
  IL_0000:  ldarg.0
  IL_0001:  isinst     ""System.Runtime.CompilerServices.ITuple""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brfalse.s  IL_005f
  IL_000a:  ldloc.0
  IL_000b:  callvirt   ""int System.Runtime.CompilerServices.ITuple.Length.get""
  IL_0010:  ldc.i4.3
  IL_0011:  bne.un.s   IL_005f
  IL_0013:  ldloc.0
  IL_0014:  ldc.i4.0
  IL_0015:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
  IL_001a:  stloc.1
  IL_001b:  ldloc.1
  IL_001c:  isinst     ""int""
  IL_0021:  brfalse.s  IL_005f
  IL_0023:  ldloc.1
  IL_0024:  unbox.any  ""int""
  IL_0029:  ldc.i4.3
  IL_002a:  bne.un.s   IL_005f
  IL_002c:  ldloc.0
  IL_002d:  ldc.i4.1
  IL_002e:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
  IL_0033:  stloc.2
  IL_0034:  ldloc.2
  IL_0035:  isinst     ""int""
  IL_003a:  brfalse.s  IL_005f
  IL_003c:  ldloc.2
  IL_003d:  unbox.any  ""int""
  IL_0042:  ldc.i4.4
  IL_0043:  bne.un.s   IL_005f
  IL_0045:  ldloc.0
  IL_0046:  ldc.i4.2
  IL_0047:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
  IL_004c:  stloc.3
  IL_004d:  ldloc.3
  IL_004e:  isinst     ""int""
  IL_0053:  brfalse.s  IL_005f
  IL_0055:  ldloc.3
  IL_0056:  unbox.any  ""int""
  IL_005b:  ldc.i4.5
  IL_005c:  ceq
  IL_005e:  ret
  IL_005f:  ldc.i4.0
  IL_0060:  ret
}");
        }

        [Fact]
        public void ITupleFromObject_02()
        {
            var source =
@"using System;
using System.Runtime.CompilerServices;
public class C : ITuple
{
    int ITuple.Length => 3;
    object ITuple.this[int i] => i + 3;
    public static void Main()
    {
        Console.WriteLine(M(new C()));
    }
    private static bool M(object t)
    {
        switch (t)
        {
            case (3, 4, 5): return true;
            case var _: return false;
        }
    }
}
";
            var compilation = CreatePatternCompilation(source);
            compilation.VerifyDiagnostics();
            var expectedOutput = @"True";
            var compVerifier = CompileAndVerify(compilation, expectedOutput: expectedOutput);
            compVerifier.VerifyIL("C.M",
@"{
  // Code size       98 (0x62)
  .maxstack  2
  .locals init (System.Runtime.CompilerServices.ITuple V_0,
                object V_1,
                object V_2,
                object V_3)
  IL_0000:  ldarg.0
  IL_0001:  isinst     ""System.Runtime.CompilerServices.ITuple""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brfalse.s  IL_0060
  IL_000a:  ldloc.0
  IL_000b:  callvirt   ""int System.Runtime.CompilerServices.ITuple.Length.get""
  IL_0010:  ldc.i4.3
  IL_0011:  bne.un.s   IL_0060
  IL_0013:  ldloc.0
  IL_0014:  ldc.i4.0
  IL_0015:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
  IL_001a:  stloc.1
  IL_001b:  ldloc.1
  IL_001c:  isinst     ""int""
  IL_0021:  brfalse.s  IL_0060
  IL_0023:  ldloc.1
  IL_0024:  unbox.any  ""int""
  IL_0029:  ldc.i4.3
  IL_002a:  bne.un.s   IL_0060
  IL_002c:  ldloc.0
  IL_002d:  ldc.i4.1
  IL_002e:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
  IL_0033:  stloc.2
  IL_0034:  ldloc.2
  IL_0035:  isinst     ""int""
  IL_003a:  brfalse.s  IL_0060
  IL_003c:  ldloc.2
  IL_003d:  unbox.any  ""int""
  IL_0042:  ldc.i4.4
  IL_0043:  bne.un.s   IL_0060
  IL_0045:  ldloc.0
  IL_0046:  ldc.i4.2
  IL_0047:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
  IL_004c:  stloc.3
  IL_004d:  ldloc.3
  IL_004e:  isinst     ""int""
  IL_0053:  brfalse.s  IL_0060
  IL_0055:  ldloc.3
  IL_0056:  unbox.any  ""int""
  IL_005b:  ldc.i4.5
  IL_005c:  bne.un.s   IL_0060
  IL_005e:  ldc.i4.1
  IL_005f:  ret
  IL_0060:  ldc.i4.0
  IL_0061:  ret
}");
        }

        [Fact, WorkItem(51801, "https://github.com/dotnet/roslyn/issues/51801")]
        public void IndexerOverrideLacksAccessor()
        {
            var source = @"
#nullable enable
using System.Runtime.CompilerServices;

class Base
{
  public virtual object this[int i] { get { return 1; } set { } }
}

class C : Base, ITuple
{
  public override object this[int i] { set { } }
  public int Length => 2;

  public string? Value { get; }

  public string M()
  {
    switch (this)
    {
      case (1, 1):
        return Value;
      default:
        return Value;
    }
  }
}
";
            var verifier = CompileAndVerify(CreatePatternCompilation(source, TestOptions.DebugDll));
            verifier.VerifyIL("C.M", @"
  {
      // Code size      110 (0x6e)
      .maxstack  2
      .locals init (C V_0,
                    int V_1,
                    object V_2,
                    int V_3,
                    object V_4,
                    int V_5,
                    C V_6,
                    string V_7)
      IL_0000:  nop
      IL_0001:  ldarg.0
      IL_0002:  stloc.s    V_6
      IL_0004:  ldloc.s    V_6
      IL_0006:  stloc.0
      IL_0007:  ldloc.0
      IL_0008:  isinst     ""System.Runtime.CompilerServices.ITuple""
      IL_000d:  brfalse.s  IL_0061
      IL_000f:  ldloc.0
      IL_0010:  callvirt   ""int System.Runtime.CompilerServices.ITuple.Length.get""
      IL_0015:  stloc.1
      IL_0016:  ldloc.1
      IL_0017:  ldc.i4.2
      IL_0018:  bne.un.s   IL_0061
      IL_001a:  ldloc.0
      IL_001b:  ldc.i4.0
      IL_001c:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
      IL_0021:  stloc.2
      IL_0022:  ldloc.2
      IL_0023:  isinst     ""int""
      IL_0028:  brfalse.s  IL_0061
      IL_002a:  ldloc.2
      IL_002b:  unbox.any  ""int""
      IL_0030:  stloc.3
      IL_0031:  ldloc.3
      IL_0032:  ldc.i4.1
      IL_0033:  bne.un.s   IL_0061
      IL_0035:  ldloc.0
      IL_0036:  ldc.i4.1
      IL_0037:  callvirt   ""object System.Runtime.CompilerServices.ITuple.this[int].get""
      IL_003c:  stloc.s    V_4
      IL_003e:  ldloc.s    V_4
      IL_0040:  isinst     ""int""
      IL_0045:  brfalse.s  IL_0061
      IL_0047:  ldloc.s    V_4
      IL_0049:  unbox.any  ""int""
      IL_004e:  stloc.s    V_5
      IL_0050:  ldloc.s    V_5
      IL_0052:  ldc.i4.1
      IL_0053:  beq.s      IL_0057
      IL_0055:  br.s       IL_0061
      IL_0057:  ldarg.0
      IL_0058:  call       ""string C.Value.get""
      IL_005d:  stloc.s    V_7
      IL_005f:  br.s       IL_006b
      IL_0061:  ldarg.0
      IL_0062:  call       ""string C.Value.get""
      IL_0067:  stloc.s    V_7
      IL_0069:  br.s       IL_006b
      IL_006b:  ldloc.s    V_7
      IL_006d:  ret
    }");
        }

    }
}
