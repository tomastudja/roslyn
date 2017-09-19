﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    /// <summary>
    /// this place is dedicated to binding related error tests
    /// </summary>
    [CompilerTrait(CompilerFeature.ReadOnlyReferences)]
    public class SpanStackSafetyTests : CompilingTestBase
    {
        [Fact]
        public void TrivialBoxing()
        {
            var text = @"
using System;

class Program
{
    static void Main()
    {
        object x = new Span<int>();
        object y = new ReadOnlySpan<byte>();
        object z = new SpanLike<int>();
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (8,20): error CS0029: Cannot implicitly convert type 'System.Span<int>' to 'object'
                //         object x = new Span<int>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new Span<int>()").WithArguments("System.Span<int>", "object").WithLocation(8, 20),
                // (9,20): error CS0029: Cannot implicitly convert type 'System.ReadOnlySpan<byte>' to 'object'
                //         object y = new ReadOnlySpan<byte>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new ReadOnlySpan<byte>()").WithArguments("System.ReadOnlySpan<byte>", "object").WithLocation(9, 20),
                // (10,20): error CS0029: Cannot implicitly convert type 'System.SpanLike<int>' to 'object'
                //         object z = new SpanLike<int>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new SpanLike<int>()").WithArguments("System.SpanLike<int>", "object")
            );

            comp = CreateCompilationWithMscorlibAndSpanSrc(text);

            comp.VerifyDiagnostics(
                // (8,20): error CS0029: Cannot implicitly convert type 'System.Span<int>' to 'object'
                //         object x = new Span<int>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new Span<int>()").WithArguments("System.Span<int>", "object").WithLocation(8, 20),
                // (9,20): error CS0029: Cannot implicitly convert type 'System.ReadOnlySpan<byte>' to 'object'
                //         object y = new ReadOnlySpan<byte>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new ReadOnlySpan<byte>()").WithArguments("System.ReadOnlySpan<byte>", "object").WithLocation(9, 20),
                // (10,20): error CS0029: Cannot implicitly convert type 'System.SpanLike<int>' to 'object'
                //         object z = new SpanLike<int>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new SpanLike<int>()").WithArguments("System.SpanLike<int>", "object")
            );
        }

        [Fact]
        public void LambdaCapturing()
        {
            var text = @"
using System;

class Program
{
    // this should be ok
    public delegate Span<T> D1<T>(Span<T> arg);

    static void Main()
    {
        var x = new Span<int>();

        D1<int> d = (t)=>t;
        x = d(x);
        
        // error due to capture
        Func<int> f = () => x[1];
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            //PROTOTYPE(span): need a better error message for invalid span captures (should not say ref)
            comp.VerifyDiagnostics(
                // (17,29): error CS8175: Cannot use ref local 'x' inside an anonymous method, lambda expression, or query expression
                //         Func<int> f = () => x[1];
                Diagnostic(ErrorCode.ERR_AnonDelegateCantUseLocal, "x").WithArguments("x").WithLocation(17, 29)
            );
        }

        [Fact]
        public void GenericArgsAndConstraints()
        {
            var text = @"
using System;

class Program
{
    static void Main()
    {
        var x = new Span<int>();

        Func<Span<int>> d = ()=>x;
    }

    class C1<T> where T: Span<int>
    {
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            //PROTOTYPE(span): need a better error message for invalid span captures (should not say ref)
            comp.VerifyDiagnostics(
                // (13,26): error CS0701: 'Span<int>' is not a valid constraint. A type used as a constraint must be an interface, a non-sealed class or a type parameter.
                //     class C1<T> where T: Span<int>
                Diagnostic(ErrorCode.ERR_BadBoundType, "Span<int>").WithArguments("System.Span<int>").WithLocation(13, 26),
                // (10,14): error CS0306: The type 'Span<int>' may not be used as a type argument
                //         Func<Span<int>> d = ()=>x;
                Diagnostic(ErrorCode.ERR_BadTypeArgument, "Span<int>").WithArguments("System.Span<int>").WithLocation(10, 14),
                // (10,33): error CS8175: Cannot use ref local 'x' inside an anonymous method, lambda expression, or query expression
                //         Func<Span<int>> d = ()=>x;
                Diagnostic(ErrorCode.ERR_AnonDelegateCantUseLocal, "x").WithArguments("x").WithLocation(10, 33)
            );
        }

        [Fact]
        public void Arrays()
        {
            var text = @"
using System;

class Program
{
    static void Main()
    {
        var x = new Span<int>[1];

        var y = new SpanLike<int>[1,2];
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            //PROTOTYPE(span): make this bind-time diagnostic?
            comp.VerifyDiagnostics(
                // (8,21): error CS0611: Array elements cannot be of type 'Span<int>'
                //         var x = new Span<int>[1];
                Diagnostic(ErrorCode.ERR_ArrayElementCantBeRefAny, "Span<int>").WithArguments("System.Span<int>"),
                // (10,21): error CS0611: Array elements cannot be of type 'SpanLike<int>'
                //         var y = new SpanLike<int>[1,2];
                Diagnostic(ErrorCode.ERR_ArrayElementCantBeRefAny, "SpanLike<int>").WithArguments("System.SpanLike<int>").WithLocation(10, 21)
            );
        }

        [Fact]
        public void ByrefParam()
        {
            var text = @"
using System;

class Program
{
    static void Main()
    {
    }

    // OK
    static void M1(ref Span<string> ss)
    {
    }

    // OK
    static void M2(out SpanLike<string> ss)
    {
        ss = default;
    }

    // OK
    static void M3(ref readonly Span<string> ss)
    {
    }

    // OK
    static void M3l(ref readonly SpanLike<string> ss)
    {
    }

    // Not OK
    static ref Span<string> M4(ref Span<string> ss) { return ref ss; }

    // Not OK
    static ref readonly Span<string> M5(ref Span<string> ss) => ref ss;

    // Not OK
    // TypedReference baseline
    static ref TypedReference M1(ref TypedReference ss) => ref ss;
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (39,34): error CS1601: Cannot make reference to variable of type 'TypedReference'
                //     static ref TypedReference M1(ref TypedReference ss) => ref ss;
                Diagnostic(ErrorCode.ERR_MethodArgCantBeRefAny, "ref TypedReference ss").WithArguments("System.TypedReference").WithLocation(39, 34),
                // (39,12): error CS1599: Method or delegate cannot return type 'TypedReference'
                //     static ref TypedReference M1(ref TypedReference ss) => ref ss;
                Diagnostic(ErrorCode.ERR_MethodReturnCantBeRefAny, "ref TypedReference").WithArguments("System.TypedReference").WithLocation(39, 12),
                // (32,66): error CS8166: Cannot return a parameter by reference 'ss' because it is not a ref or out parameter
                //     static ref Span<string> M4(ref Span<string> ss) { return ref ss; }
                Diagnostic(ErrorCode.ERR_RefReturnParameter, "ss").WithArguments("ss").WithLocation(32, 66),
                // (35,69): error CS8166: Cannot return a parameter by reference 'ss' because it is not a ref or out parameter
                //     static ref readonly Span<string> M5(ref Span<string> ss) => ref ss;
                Diagnostic(ErrorCode.ERR_RefReturnParameter, "ss").WithArguments("ss").WithLocation(35, 69)
            );
        }

        [Fact]
        public void FieldsSpan()
        {
            var text = @"
using System;

public class Program
{
    static void Main()
    {
    }

    public static Span<byte> fs;
    public Span<int> fi; 

    public ref struct S1
    {
        public static Span<byte> fs1;
        public Span<int> fi1; 
    }

    public struct S2
    {
        public static Span<byte> fs2;
        public Span<int> fi2; 
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (22,16): error CS8519: Field or auto-implemented property cannot be of type 'Span<int>' unless it is an instance member of a ref struct.
                //         public Span<int> fi2; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<int>").WithArguments("System.Span<int>").WithLocation(22, 16),
                // (21,23): error CS8519: Field or auto-implemented property cannot be of type 'Span<byte>' unless it is an instance member of a ref struct.
                //         public static Span<byte> fs2;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<byte>").WithArguments("System.Span<byte>").WithLocation(21, 23),
                // (10,19): error CS8519: Field or auto-implemented property cannot be of type 'Span<byte>' unless it is an instance member of a ref struct.
                //     public static Span<byte> fs;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<byte>").WithArguments("System.Span<byte>").WithLocation(10, 19),
                // (11,12): error CS8519: Field or auto-implemented property cannot be of type 'Span<int>' unless it is an instance member of a ref struct.
                //     public Span<int> fi; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<int>").WithArguments("System.Span<int>").WithLocation(11, 12),
                // (15,23): error CS8519: Field or auto-implemented property cannot be of type 'Span<byte>' unless it is an instance member of a ref struct.
                //         public static Span<byte> fs1;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<byte>").WithArguments("System.Span<byte>").WithLocation(15, 23)
            );

            comp = CreateCompilationWithMscorlibAndSpanSrc(text);

            comp.VerifyDiagnostics(
                // (22,16): error CS8519: Field or auto-implemented property cannot be of type 'Span<int>' unless it is an instance member of a ref struct.
                //         public Span<int> fi2; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<int>").WithArguments("System.Span<int>").WithLocation(22, 16),
                // (21,23): error CS8519: Field or auto-implemented property cannot be of type 'Span<byte>' unless it is an instance member of a ref struct.
                //         public static Span<byte> fs2;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<byte>").WithArguments("System.Span<byte>").WithLocation(21, 23),
                // (10,19): error CS8519: Field or auto-implemented property cannot be of type 'Span<byte>' unless it is an instance member of a ref struct.
                //     public static Span<byte> fs;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<byte>").WithArguments("System.Span<byte>").WithLocation(10, 19),
                // (11,12): error CS8519: Field or auto-implemented property cannot be of type 'Span<int>' unless it is an instance member of a ref struct.
                //     public Span<int> fi; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<int>").WithArguments("System.Span<int>").WithLocation(11, 12),
                // (15,23): error CS8519: Field or auto-implemented property cannot be of type 'Span<byte>' unless it is an instance member of a ref struct.
                //         public static Span<byte> fs1;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<byte>").WithArguments("System.Span<byte>").WithLocation(15, 23)
            );
        }

        [Fact]
        public void FieldsSpanLike()
        {
            var text = @"
using System;

public class Program
{
    static void Main()
    {
    }

    public static SpanLike<byte> fs;
    public SpanLike<int> fi; 

    public ref struct S1
    {
        public static SpanLike<byte> fs1;
        public SpanLike<int> fi1; 
    }

    public struct S2
    {
        public static SpanLike<byte> fs2;
        public SpanLike<int> fi2; 
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (22,16): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<int>' unless it is an instance member of a ref struct.
                //         public SpanLike<int> fi2; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<int>").WithArguments("System.SpanLike<int>").WithLocation(22, 16),
                // (21,23): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<byte>' unless it is an instance member of a ref struct.
                //         public static SpanLike<byte> fs2;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<byte>").WithArguments("System.SpanLike<byte>").WithLocation(21, 23),
                // (10,19): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<byte>' unless it is an instance member of a ref struct.
                //     public static SpanLike<byte> fs;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<byte>").WithArguments("System.SpanLike<byte>").WithLocation(10, 19),
                // (11,12): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<int>' unless it is an instance member of a ref struct.
                //     public SpanLike<int> fi; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<int>").WithArguments("System.SpanLike<int>").WithLocation(11, 12),
                // (15,23): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<byte>' unless it is an instance member of a ref struct.
                //         public static SpanLike<byte> fs1;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<byte>").WithArguments("System.SpanLike<byte>").WithLocation(15, 23)
            );

            comp = CreateCompilationWithMscorlibAndSpanSrc(text);

            comp.VerifyDiagnostics(
                // (22,16): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<int>' unless it is an instance member of a ref struct.
                //         public SpanLike<int> fi2; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<int>").WithArguments("System.SpanLike<int>").WithLocation(22, 16),
                // (21,23): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<byte>' unless it is an instance member of a ref struct.
                //         public static SpanLike<byte> fs2;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<byte>").WithArguments("System.SpanLike<byte>").WithLocation(21, 23),
                // (10,19): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<byte>' unless it is an instance member of a ref struct.
                //     public static SpanLike<byte> fs;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<byte>").WithArguments("System.SpanLike<byte>").WithLocation(10, 19),
                // (11,12): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<int>' unless it is an instance member of a ref struct.
                //     public SpanLike<int> fi; 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<int>").WithArguments("System.SpanLike<int>").WithLocation(11, 12),
                // (15,23): error CS8519: Field or auto-implemented property cannot be of type 'SpanLike<byte>' unless it is an instance member of a ref struct.
                //         public static SpanLike<byte> fs1;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "SpanLike<byte>").WithArguments("System.SpanLike<byte>").WithLocation(15, 23)
            );
        }

        [WorkItem(20226, "https://github.com/dotnet/roslyn/issues/20226")]
        [Fact]
        public void InterfaceImpl()
        {
            var text = @"
using System;

public class Program
{
    static void Main(string[] args)
    {
        using (new S1())
        {

        }
    }

    public ref struct S1 : IDisposable
    {
        public void Dispose() { }
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (14,28): error CS8517: 'Program.S1': ref structs cannot implement interfaces
                //     public ref struct S1 : IDisposable
                Diagnostic(ErrorCode.ERR_RefStructInterfaceImpl, "IDisposable").WithArguments("Program.S1", "System.IDisposable").WithLocation(14, 28),
                // (8,16): error CS1674: 'Program.S1': type used in a using statement must be implicitly convertible to 'System.IDisposable'
                //         using (new S1())
                Diagnostic(ErrorCode.ERR_NoConvToIDisp, "new S1()").WithArguments("Program.S1").WithLocation(8, 16)
            );
        }

        [WorkItem(20226, "https://github.com/dotnet/roslyn/issues/20226")]
        [Fact]
        public void RefIteratorInAsync()
        {
            var text = @"
using System;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
    }

    static async Task<int> Test()
    {
        var obj = new C1();

        foreach (var i in obj)
        {
            await Task.Yield();
            System.Console.WriteLine(i);
        }

        return 123;
    }
}

class C1
{
    public S1 GetEnumerator()
    {
        return new S1();
    }

    public ref struct S1
    {
        public int Current => throw new NotImplementedException();

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }
    }
}

";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (15,9): error CS8518: foreach statement cannot operate on enumerators of type 'C1.S1' in async or iterator methods because 'C1.S1' is a ref struct.
                //         foreach (var i in obj)
                Diagnostic(ErrorCode.ERR_BadSpecialByRefIterator, "foreach").WithArguments("C1.S1").WithLocation(15, 9)
            );
        }

        [WorkItem(20226, "https://github.com/dotnet/roslyn/issues/20226")]
        [Fact]
        public void RefIteratorInIterator()
        {
            var text = @"
using System;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        // this is valid
        Action a = () =>
        {
            foreach (var i in new C1())
            {
            }
        };

        a();
    }

    static IEnumerable<int> Test()
    {
        // this is valid
        Action a = () =>
        {
            foreach (var i in new C1())
            {
            }
        };

        a();

        // this is an error
        foreach (var i in new C1())
        {
        }

        yield return 1;
    }
}

class C1
{
    public S1 GetEnumerator()
    {
        return new S1();
    }

    public ref struct S1
    {
        public int Current => throw new NotImplementedException();

        public bool MoveNext()
        {
            throw new NotImplementedException();
        }
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (33,9): error CS8518: foreach statement cannot operate on enumerators of type 'C1.S1' in async or iterator methods because 'C1.S1' is a ref struct.
                //         foreach (var i in new C1())
                Diagnostic(ErrorCode.ERR_BadSpecialByRefIterator, "foreach").WithArguments("C1.S1").WithLocation(33, 9)
            );
        }

        [Fact]
        public void Properties()
        {
            var text = @"
using System;

public class Program
{
    static void Main()
    {
    }

    // valid
    public static Span<byte> ps => default(Span<byte>);
    public Span<int> pi => default(Span<int>); 

    public Span<int> this[int i] => default(Span<int>); 

    // not valid
    public static Span<byte> aps {get;}
    public Span<int> api {get; set;} 
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (17,19): error CS8519: Field or auto-implemented property cannot be of type 'Span<byte>' unless it is an instance member of a ref struct.
                //     public static Span<byte> aps {get;}
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<byte>").WithArguments("System.Span<byte>").WithLocation(17, 19),
                // (18,12): error CS8519: Field or auto-implemented property cannot be of type 'Span<int>' unless it is an instance member of a ref struct.
                //     public Span<int> api {get; set;} 
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "Span<int>").WithArguments("System.Span<int>").WithLocation(18, 12)
            );
        }

        [Fact]
        public void Operators()
        {
            var text = @"
using System;

public class Program
{
    static void Main()
    {
    }

    // valid
    public static Span<byte> operator +(Span<byte> x, Program y) => default(Span<byte>);

    // invalid (baseline w/ TypedReference)
    public static TypedReference operator +(Span<int> x, Program y) => default(TypedReference);

}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (14,19): error CS1599: Method or delegate cannot return type 'TypedReference'
                //     public static TypedReference operator +(Span<int> x, Program y) => default(TypedReference);
                Diagnostic(ErrorCode.ERR_MethodReturnCantBeRefAny, "TypedReference").WithArguments("System.TypedReference").WithLocation(14, 19)
            );
        }

        [Fact]
        public void AsyncParams()
        {
            var text = @"
using System;
using System.Threading.Tasks;

public class Program
{
    static void Main()
    {
    }

    public static async Task<int> M1(Span<int> arg)
    {
        await Task.Yield();
        return 42;
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (11,48): error CS4012: Parameters or locals of type 'Span<int>' cannot be declared in async methods or lambda expressions.
                //     public static async Task<int> M1(Span<int> arg)
                Diagnostic(ErrorCode.ERR_BadSpecialByRefLocal, "arg").WithArguments("System.Span<int>").WithLocation(11, 48)
            );
        }

        [Fact]
        public void AsyncLocals()
        {
            var text = @"
using System;
using System.Threading.Tasks;

public class Program
{
    static void Main()
    {
    }

    public static async Task<int> M1()
    {
        Span<int> local = default(Span<int>);

        await Task.Yield();
        return 42;
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (13,9): error CS4012: Parameters or locals of type 'Span<int>' cannot be declared in async methods or lambda expressions.
                //         Span<int> local = default(Span<int>);
                Diagnostic(ErrorCode.ERR_BadSpecialByRefLocal, "Span<int>").WithArguments("System.Span<int>").WithLocation(13, 9),
                // (13,19): warning CS0219: The variable 'local' is assigned but its value is never used
                //         Span<int> local = default(Span<int>);
                Diagnostic(ErrorCode.WRN_UnreferencedVarAssg, "local").WithArguments("local").WithLocation(13, 19)
            );

            comp = CreateCompilationWithMscorlibAndSpan(text, TestOptions.DebugExe);

            comp.VerifyDiagnostics(
                // (13,9): error CS4012: Parameters or locals of type 'Span<int>' cannot be declared in async methods or lambda expressions.
                //         Span<int> local = default(Span<int>);
                Diagnostic(ErrorCode.ERR_BadSpecialByRefLocal, "Span<int>").WithArguments("System.Span<int>").WithLocation(13, 9),
                // (13,19): warning CS0219: The variable 'local' is assigned but its value is never used
                //         Span<int> local = default(Span<int>);
                Diagnostic(ErrorCode.WRN_UnreferencedVarAssg, "local").WithArguments("local").WithLocation(13, 19)
            );
        }

        [Fact]
        public void AsyncSpilling()
        {
            var text = @"
using System;
using System.Threading.Tasks;

public class Program
{
    static void Main()
    {
    }

    public static async Task<int> M1()
    {
        // this is ok
        TakesSpan(default(Span<int>), 123);

        // this is not ok
        TakesSpan(default(Span<int>), await I1());

        // this is ok
        TakesSpan(await I1(), default(Span<int>));

        return 42;
    }

    public static void TakesSpan(Span<int> s, int i)
    {
    }

    public static void TakesSpan(int i, Span<int> s)
    {
    }

    public static async Task<int> I1()
    {
        await Task.Yield();
        return 42;
    }
    
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            //PROTOTYPE(span): spilling diagnostics is very hard to detect early.
            //                 it would be uncommon too. Is it ok to do in Emit?
            comp.VerifyEmitDiagnostics(
                // (17,39): error CS4007: 'await' cannot be used in an expression containing the type 'System.Span<int>'
                //         TakesSpan(default(Span<int>), await I1());
                Diagnostic(ErrorCode.ERR_ByRefTypeAndAwait, "await I1()").WithArguments("System.Span<int>")
            );

            comp = CreateCompilationWithMscorlibAndSpan(text, TestOptions.DebugExe);

            comp.VerifyEmitDiagnostics(
                // (17,39): error CS4007: 'await' cannot be used in an expression containing the type 'System.Span<int>'
                //         TakesSpan(default(Span<int>), await I1());
                Diagnostic(ErrorCode.ERR_ByRefTypeAndAwait, "await I1()").WithArguments("System.Span<int>")
            );
        }

        [Fact]
        public void AsyncSpillTemp()
        {
            var text = @"
using System;
using System.Threading.Tasks;

public class Program
{
    static void Main()
    {
    }

    public static async Task<int> M1()
    {
        // this is not ok
        TakesSpan(s: default(Span<int>), i: await I1());

        return 42;
    }

    public static void TakesSpan(int i, Span<int> s)
    {
    }

    public static async Task<int> I1()
    {
        await Task.Yield();
        return 42;
    }
    
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            //PROTOTYPE(span): spilling diagnostics is very hard to detect early.
            //                 it would be uncommon too. Is it ok to do in Emit?
            comp.VerifyEmitDiagnostics(
                // (14,45): error CS4007: 'await' cannot be used in an expression containing the type 'System.Span<int>'
                //         TakesSpan(s: default(Span<int>), i: await I1());
                Diagnostic(ErrorCode.ERR_ByRefTypeAndAwait, "await I1()").WithArguments("System.Span<int>").WithLocation(14, 45)
            );

            comp = CreateCompilationWithMscorlibAndSpan(text, TestOptions.DebugExe);

            //PROTOTYPE(span): spilling diagnostics is very hard to detect early.
            //                 it would be uncommon too. Is it ok to do in Emit?
            comp.VerifyEmitDiagnostics(
                // (14,45): error CS4007: 'await' cannot be used in an expression containing the type 'System.Span<int>'
                //         TakesSpan(s: default(Span<int>), i: await I1());
                Diagnostic(ErrorCode.ERR_ByRefTypeAndAwait, "await I1()").WithArguments("System.Span<int>").WithLocation(14, 45)
            );
        }

        [Fact]
        public void BaseMethods()
        {
            var text = @"
using System;

public class Program
{
    static void Main()
    {
        // this is ok  (overriden)
        default(Span<int>).GetHashCode();

        // this is ok  (implicit boxing)
        default(Span<int>).GetType();

        // this is not ok  (implicit boxing)
        default(Span<int>).ToString();
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyDiagnostics(
                // (12,9): error CS0029: Cannot implicitly convert type 'System.Span<int>' to 'object'
                //         default(Span<int>).GetType();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "default(Span<int>)").WithArguments("System.Span<int>", "object").WithLocation(12, 9),
                // (15,9): error CS0029: Cannot implicitly convert type 'System.Span<int>' to 'System.ValueType'
                //         default(Span<int>).ToString();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "default(Span<int>)").WithArguments("System.Span<int>", "System.ValueType").WithLocation(15, 9)
            );
        }

        [Fact]
        public void MethodConversion()
        {
            var text = @"
using System;

public class Program
{
    static void Main()
    {
        //PROTOTYPE(span): we allow this. Is that because it would be a breaking change?
        Func<int> d0 = default(TypedReference).GetHashCode;

        // none of the following is ok, since we would need to capture the receiver.
        Func<int> d1 = default(Span<int>).GetHashCode;

        Func<Type> d2 = default(SpanLike<int>).GetType;

        Func<string> d3 = default(Span<int>).ToString;
    }
}
";

            CSharpCompilation comp = CreateCompilationWithMscorlibAndSpan(text);

            comp.VerifyEmitDiagnostics(
                // (12,43): error CS0123: No overload for 'GetHashCode' matches delegate 'Func<int>'
                //         Func<int> d1 = default(Span<int>).GetHashCode;
                Diagnostic(ErrorCode.ERR_MethDelegateMismatch, "GetHashCode").WithArguments("GetHashCode", "System.Func<int>").WithLocation(12, 43),
                // (14,48): error CS0123: No overload for 'GetType' matches delegate 'Func<Type>'
                //         Func<Type> d2 = default(SpanLike<int>).GetType;
                Diagnostic(ErrorCode.ERR_MethDelegateMismatch, "GetType").WithArguments("GetType", "System.Func<System.Type>").WithLocation(14, 48),
                // (16,46): error CS0123: No overload for 'ToString' matches delegate 'Func<string>'
                //         Func<string> d3 = default(Span<int>).ToString;
                Diagnostic(ErrorCode.ERR_MethDelegateMismatch, "ToString").WithArguments("ToString", "System.Func<string>").WithLocation(16, 46)
            );
        }

        //PROTOTYPE(span): Span and ReadOnlySpan should have ByRefLike attribute, eventually.
        //                 For now assume that any "System.Span" and "System.ReadOnlySpan" structs 
        //                 are ByRefLike
        [Fact]
        public void SpanDetect()
        {

            //span structs are not marked as "ref"
            string spanSourceNoRefs = @"
namespace System
{
    public struct Span<T> 
    {
        public ref T this[int i] => throw null;
        public override int GetHashCode() => 1;
    }

    public struct ReadOnlySpan<T>
    {
        public ref readonly T this[int i] => throw null;
        public override int GetHashCode() => 2;
    }

    public struct RegularStruct<T>
    {
    }

    // arity 0 - not a span
    public struct Span 
    {
    }

    // arity 2 - not a span
    public struct Span<T, U> 
    {
        public ref T this[int i] => throw null;
        public override int GetHashCode() => 1;
    }
}

// nested
public struct S1
{
    public struct Span<T> 
    {
        public ref T this[int i] => throw null;
        public override int GetHashCode() => 1;
    }
}

public struct Span<T> 
{
    public ref T this[int i] => throw null;
    public override int GetHashCode() => 1;
}

";
            var reference = CreateCompilation(
                spanSourceNoRefs,
                references: new List<MetadataReference>() { MscorlibRef_v4_0_30316_17626, SystemCoreRef, CSharpRef },
                options: TestOptions.ReleaseDll);

            reference.VerifyDiagnostics();

            var text = @"
using System;

class Program
{
    static void Main()
    {
        object x = new System.Span<int>();
        object y = new ReadOnlySpan<byte>();

        object z1 = new Span();
        object z2 = new Span<int, int>();
        object z3 = new S1.Span<int>();
        object z4 = new Span<int>();
    }
}
";
            var comp = CreateCompilation(
                text,
                references: new List<MetadataReference>() { MscorlibRef_v4_0_30316_17626, SystemCoreRef, CSharpRef, reference.EmitToImageReference() },
                options: TestOptions.ReleaseExe);

            comp.VerifyDiagnostics(
                // (8,20): error CS0029: Cannot implicitly convert type 'System.Span<int>' to 'object'
                //         object x = new System.Span<int>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new System.Span<int>()").WithArguments("System.Span<int>", "object").WithLocation(8, 20),
                // (9,20): error CS0029: Cannot implicitly convert type 'System.ReadOnlySpan<byte>' to 'object'
                //         object y = new ReadOnlySpan<byte>();
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "new ReadOnlySpan<byte>()").WithArguments("System.ReadOnlySpan<byte>", "object").WithLocation(9, 20)
            );
        }
    }
}
