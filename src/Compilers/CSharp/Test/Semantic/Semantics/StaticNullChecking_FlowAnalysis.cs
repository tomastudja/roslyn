﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Nullable
{
    public class StaticNullChecking_FlowAnalysis : CSharpTestBase
    {
        [Fact]
        public void ImplicitlyTypedArrayCreation_01()
        {
            var source =
@"class C
{
    static void F(object x, object? y)
    {
        var a = new[] { x, x };
        a.ToString();
        a[0].ToString();
        var b = new[] { x, y };
        b.ToString();
        b[0].ToString();
        var c = new[] { b };
        c[0].ToString();
        c[0][0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (10,9): warning CS8602: Possible dereference of a null reference.
                //         b[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b[0]").WithLocation(10, 9),
                // (13,9): warning CS8602: Possible dereference of a null reference.
                //         c[0][0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "c[0][0]").WithLocation(13, 9));
        }

        [Fact]
        public void ImplicitlyTypedArrayCreation_02()
        {
            var source =
@"class C
{
    static void F(object x, object? y)
    {
        var a = new[] { x };
        a[0].ToString();
        var b = new[] { y };
        b[0].ToString();
        var c = new[] { a, b };
        c[0][0].ToString();
        var d = new[] { a, b! };
        d[0][0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (8,9): warning CS8602: Possible dereference of a null reference.
                //         b[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b[0]").WithLocation(8, 9),
                // (10,9): warning CS8602: Possible dereference of a null reference.
                //         c[0][0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "c[0][0]").WithLocation(10, 9));
        }

        [Fact]
        public void ImplicitlyTypedArrayCreation_03()
        {
            var source =
@"class C
{
    static void F(object x, object? y)
    {
        (new[] { x, x })[1].ToString();
        (new[] { y, x })[1].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (6,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { y, x })[1].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { y, x })[1]").WithLocation(6, 9));
        }

        [Fact]
        public void ImplicitlyTypedArrayCreation_04()
        {
            var source =
@"class C
{
    static void F()
    {
        object? o = new object();
        var a = new[] { o };
        a[0].ToString();
        var b = new[] { a };
        b[0][0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ImplicitlyTypedArrayCreation_05()
        {
            var source =
@"class C
{
    static void F(int n)
    {
        object? o = new object();
        while (n-- > 0)
        {
            var a = new[] { o };
            a[0].ToString();
            var b = new[] { a };
            b[0][0].ToString();
            o = null;
        }
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (9,13): warning CS8602: Possible dereference of a null reference.
                //             a[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "a[0]").WithLocation(9, 13),
                // (11,13): warning CS8602: Possible dereference of a null reference.
                //             b[0][0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b[0][0]").WithLocation(11, 13));
        }

        [Fact]
        public void ImplicitlyTypedArrayCreation_06()
        {
            var source =
@"class C
{
    static void F(string s)
    {
        var a = new[] { new object(), (string)null };
        a[0].ToString();
        var b = new[] { (object)null, s };
        b[0].ToString();
        var c = new[] { s, (object)null };
        c[0].ToString();
        var d = new[] { (string)null, new object() };
        d[0].ToString();
        var e = new[] { new object(), (string)null! };
        e[0].ToString();
        var f = new[] { (object)null!, s };
        f[0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,39): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         var a = new[] { new object(), (string)null };
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(string)null").WithLocation(5, 39),
                // (6,9): warning CS8602: Possible dereference of a null reference.
                //         a[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "a[0]").WithLocation(6, 9),
                // (7,25): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         var b = new[] { (object)null, s };
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(object)null").WithLocation(7, 25),
                // (8,9): warning CS8602: Possible dereference of a null reference.
                //         b[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b[0]").WithLocation(8, 9),
                // (9,28): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         var c = new[] { s, (object)null };
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(object)null").WithLocation(9, 28),
                // (10,9): warning CS8602: Possible dereference of a null reference.
                //         c[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "c[0]").WithLocation(10, 9),
                // (11,25): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         var d = new[] { (string)null, new object() };
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(string)null").WithLocation(11, 25),
                // (12,9): warning CS8602: Possible dereference of a null reference.
                //         d[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "d[0]").WithLocation(12, 9));
        }

        [Fact]
        public void ImplicitlyTypedArrayCreation_07()
        {
            var source =
@"#pragma warning disable 0649
class A<T>
{
    internal T F;
}
class B1 : A<object?> { }
class B2 : A<object> { }
class C
{
    static void F()
    {
        var a = new[] { new A<object>(), new B1() };
        a[0].F.ToString();
        var b = new[] { new A<object?>(), new B2() };
        b[0].F.ToString();
        var c = new[] { new B1(), new A<object>() };
        c[0].F.ToString();
        var d = new[] { new B2(), new A<object?>() };
        d[0].F.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (2,7): warning CS8618: Non-nullable field 'F' is uninitialized.
                // class A<T>
                Diagnostic(ErrorCode.WRN_UninitializedNonNullableField, "A").WithArguments("field", "F").WithLocation(2, 7),
                // (12,42): warning CS8619: Nullability of reference types in value of type 'B1' doesn't match target type 'A<object>'.
                //         var a = new[] { new A<object>(), new B1() };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new B1()").WithArguments("B1", "A<object>").WithLocation(12, 42),
                // (14,43): warning CS8619: Nullability of reference types in value of type 'B2' doesn't match target type 'A<object?>'.
                //         var b = new[] { new A<object?>(), new B2() };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new B2()").WithArguments("B2", "A<object?>").WithLocation(14, 43),
                // (15,9): warning CS8602: Possible dereference of a null reference.
                //         b[0].F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b[0].F").WithLocation(15, 9),
                // (16,25): warning CS8619: Nullability of reference types in value of type 'B1' doesn't match target type 'A<object>'.
                //         var c = new[] { new B1(), new A<object>() };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new B1()").WithArguments("B1", "A<object>").WithLocation(16, 25),
                // (18,25): warning CS8619: Nullability of reference types in value of type 'B2' doesn't match target type 'A<object?>'.
                //         var d = new[] { new B2(), new A<object?>() };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new B2()").WithArguments("B2", "A<object?>").WithLocation(18, 25),
                // (19,9): warning CS8602: Possible dereference of a null reference.
                //         d[0].F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "d[0].F").WithLocation(19, 9));
        }

        // PROTOTYPE(NullableReferenceType): The array element type should be nullable,
        // even though there is no best type when considering nullability for C<object>? and
        // C<object?>. In short, should report WRN_NullReferenceReceiver for `c[0].ToString()`
        [Fact]
        public void ImplicitlyTypedArrayCreation_08()
        {
            var source =
@"class C<T> { }
class C
{
    static void F(C<object>? a, C<object?> b)
    {
        if (a == null)
        {
            var c = new[] { a, b };
            c[0].ToString();
            var d = new[] { b, a };
            d[0].ToString();
        }
        else
        {
            var c = new[] { a, b };
            c[0].ToString();
            var d = new[] { b, a };
            d[0].ToString();
        }
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (8,32): warning CS8619: Nullability of reference types in value of type 'C<object?>' doesn't match target type 'C<object>'.
                //             var c = new[] { a, b };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "b").WithArguments("C<object?>", "C<object>").WithLocation(8, 32),
                // (10,32): warning CS8619: Nullability of reference types in value of type 'C<object>' doesn't match target type 'C<object?>'.
                //             var d = new[] { b, a };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "a").WithArguments("C<object>", "C<object?>").WithLocation(10, 32),
                // (15,32): warning CS8619: Nullability of reference types in value of type 'C<object?>' doesn't match target type 'C<object>'.
                //             var c = new[] { a, b };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "b").WithArguments("C<object?>", "C<object>").WithLocation(15, 32),
                // (17,32): warning CS8619: Nullability of reference types in value of type 'C<object>' doesn't match target type 'C<object?>'.
                //             var d = new[] { b, a };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "a").WithArguments("C<object>", "C<object?>").WithLocation(17, 32));
        }

        [Fact]
        public void ImplicitlyTypedArrayCreation_09()
        {
            var source =
@"class C
{
    static void F(C x, Unknown? y)
    {
        var a = new[] { x, y };
        a[0].ToString();
        var b = new[] { y, x };
        b[0].ToString();
    }
    static void G(C? x, Unknown y)
    {
        var a = new[] { x, y };
        a[0].ToString();
        var b = new[] { y, x };
        b[0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (10,25): error CS0246: The type or namespace name 'Unknown' could not be found (are you missing a using directive or an assembly reference?)
                //     static void G(C? x, Unknown y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Unknown").WithArguments("Unknown").WithLocation(10, 25),
                // (3,24): error CS0246: The type or namespace name 'Unknown' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F(C x, Unknown? y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Unknown").WithArguments("Unknown").WithLocation(3, 24),
                // (6,9): warning CS8602: Possible dereference of a null reference.
                //         a[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "a[0]").WithLocation(6, 9));
        }

        [Fact]
        public void ExplicitlyTypedArrayCreation()
        {
            var source =
@"class C
{
    static void F(object x, object? y)
    {
        var a = new object[] { x, y };
        a[0].ToString();
        var b = new object?[] { x, y };
        b[0].ToString();
        var c = new object[] { x, y! };
        c[0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,35): warning CS8601: Possible null reference assignment.
                //         var a = new object[] { x, y };
                Diagnostic(ErrorCode.WRN_NullReferenceAssignment, "y").WithLocation(5, 35),
                // (8,9): warning CS8602: Possible dereference of a null reference.
                //         b[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b[0]").WithLocation(8, 9));
        }

        [Fact]
        public void ConditionalOperator_01()
        {
            var source =
@"class C
{
    static void F(bool b, object x, object? y)
    {
        var z = b ? x : y;
        z.ToString();
        var w = b ? y : x;
        w.ToString();
        var v = true ? y : x;
        v.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (6,9): warning CS8602: Possible dereference of a null reference.
                //         z.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "z").WithLocation(6, 9),
                // (8,9): warning CS8602: Possible dereference of a null reference.
                //         w.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "w").WithLocation(8, 9),
                // (10,9): warning CS8602: Possible dereference of a null reference.
                //         v.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "v").WithLocation(10, 9));
        }

        [Fact]
        public void ConditionalOperator_02()
        {
            var source =
@"class C
{
    static void F(bool b, object x, object? y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
        if (y != null) (b ? x : y).ToString();
        if (y != null) (b ? y : x).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x : y").WithLocation(5, 10),
                // (6,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y : x").WithLocation(6, 10));
        }

        [Fact]
        public void ConditionalOperator_03()
        {
            var source =
@"class C
{
    static void F(object x, object? y)
    {
        (false ? x : y).ToString();
        (false ? y : x).ToString();
        (true ? x : y).ToString();
        (true ? y : x).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,10): warning CS8602: Possible dereference of a null reference.
                //         (false ? x : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "false ? x : y").WithLocation(5, 10),
                // (8,10): warning CS8602: Possible dereference of a null reference.
                //         (true ? y : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "true ? y : x").WithLocation(8, 10));
        }

        [Fact]
        public void ConditionalOperator_04()
        {
            var source =
@"class C
{
    static void F(bool b, object x, string? y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
    static void G(bool b, object? x, string y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x : y").WithLocation(5, 10),
                // (6,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y : x").WithLocation(6, 10),
                // (10,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x : y").WithLocation(10, 10),
                // (11,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y : x").WithLocation(11, 10));
        }

        [Fact]
        public void ConditionalOperator_05()
        {
            var source =
@"#pragma warning disable 0649
class A<T>
{
    internal T F;
    internal T P { get; }
}
class B1 : A<object?> { }
class B2 : A<object> { }
class C
{
    static void F(bool b, A<object> x, B1 y)
    {
        (b ? x : y)/*T:A<object!>!*/.F.ToString();
        (b ? y : x).P.ToString();
    }
    static void G(bool b, A<object?> x, B2 y)
    {
        (b ? x : y)/*T:A<object?>!*/.F.ToString();
        (b ? y : x).P.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (2,7): warning CS8618: Non-nullable property 'P' is uninitialized.
                // class A<T>
                Diagnostic(ErrorCode.WRN_UninitializedNonNullableField, "A").WithArguments("property", "P").WithLocation(2, 7),
                // (2,7): warning CS8618: Non-nullable field 'F' is uninitialized.
                // class A<T>
                Diagnostic(ErrorCode.WRN_UninitializedNonNullableField, "A").WithArguments("field", "F").WithLocation(2, 7),
                // (13,10): warning CS8626: No best nullability for operands of conditional expression 'A<object>' and 'B1'.
                //         (b ? x : y)/*T:A<object!>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "b ? x : y").WithArguments("A<object>", "B1").WithLocation(13, 10),
                // (14,10): warning CS8626: No best nullability for operands of conditional expression 'B1' and 'A<object>'.
                //         (b ? y : x).P.ToString();
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "b ? y : x").WithArguments("B1", "A<object>").WithLocation(14, 10),
                // (18,10): warning CS8626: No best nullability for operands of conditional expression 'A<object?>' and 'B2'.
                //         (b ? x : y)/*T:A<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "b ? x : y").WithArguments("A<object?>", "B2").WithLocation(18, 10),
                // (18,9): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : y)/*T:A<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(b ? x : y)/*T:A<object?>!*/.F").WithLocation(18, 9),
                // (19,10): warning CS8626: No best nullability for operands of conditional expression 'B2' and 'A<object?>'.
                //         (b ? y : x).P.ToString();
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "b ? y : x").WithArguments("B2", "A<object?>").WithLocation(19, 10),
                // (19,9): warning CS8602: Possible dereference of a null reference.
                //         (b ? y : x).P.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(b ? y : x).P").WithLocation(19, 9));
        }

        [Fact]
        public void ConditionalOperator_06()
        {
            var source =
@"class C
{
    static void F(bool b, object x, string? y)
    {
        (b ? null : x).ToString();
        (b ? null : y).ToString();
        (b ? x: null).ToString();
        (b ? y: null).ToString();
        (b ? null: null).ToString();
        (b ? default : x).ToString();
        (b ? default : y).ToString();
        (b ? x: default).ToString();
        (b ? y: default).ToString();
        (b ? default: default).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (9,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between '<null>' and '<null>'
                //         (b ? null: null).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? null: null").WithArguments("<null>", "<null>").WithLocation(9, 10),
                // (14,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'default' and 'default'
                //         (b ? default: default).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? default: default").WithArguments("default", "default").WithLocation(14, 10),
                // (5,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? null : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? null : x").WithLocation(5, 10),
                // (6,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? null : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? null : y").WithLocation(6, 10),
                // (7,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x: null).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x: null").WithLocation(7, 10),
                // (8,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y: null).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y: null").WithLocation(8, 10),
                // (9,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? null: null).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? null: null").WithLocation(9, 10),
                // (10,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? default : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? default : x").WithLocation(10, 10),
                // (11,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? default : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? default : y").WithLocation(11, 10),
                // (12,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x: default).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x: default").WithLocation(12, 10),
                // (13,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y: default).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y: default").WithLocation(13, 10),
                // (14,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? default: default).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? default: default").WithLocation(14, 10));
        }

        [Fact]
        public void ConditionalOperator_07()
        {
            var source =
@"class C
{
    static void F(bool b, Unknown x, Unknown? y)
    {
        (b ? null : x).ToString();
        (b ? null : y).ToString();
        (b ? x: null).ToString();
        (b ? y: null).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (3,27): error CS0246: The type or namespace name 'Unknown' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F(bool b, Unknown x, Unknown? y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Unknown").WithArguments("Unknown").WithLocation(3, 27),
                // (3,38): error CS0246: The type or namespace name 'Unknown' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F(bool b, Unknown x, Unknown? y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Unknown").WithArguments("Unknown").WithLocation(3, 38),
                // (5,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? null : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? null : x").WithLocation(5, 10),
                // (6,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? null : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? null : y").WithLocation(6, 10),
                // (7,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x: null).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x: null").WithLocation(7, 10),
                // (8,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y: null).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y: null").WithLocation(8, 10));
        }

        [Fact]
        public void ConditionalOperator_08()
        {
            var source =
@"class C
{
    static void F1(bool b, UnknownA x, UnknownB y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
    static void F2(bool b, UnknownA? x, UnknownB y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
    static void F3(bool b, UnknownA? x, UnknownB? y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (3,28): error CS0246: The type or namespace name 'UnknownA' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F1(bool b, UnknownA x, UnknownB y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownA").WithArguments("UnknownA").WithLocation(3, 28),
                // (3,40): error CS0246: The type or namespace name 'UnknownB' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F1(bool b, UnknownA x, UnknownB y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownB").WithArguments("UnknownB").WithLocation(3, 40),
                // (8,28): error CS0246: The type or namespace name 'UnknownA' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F2(bool b, UnknownA? x, UnknownB y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownA").WithArguments("UnknownA").WithLocation(8, 28),
                // (8,41): error CS0246: The type or namespace name 'UnknownB' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F2(bool b, UnknownA? x, UnknownB y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownB").WithArguments("UnknownB").WithLocation(8, 41),
                // (13,28): error CS0246: The type or namespace name 'UnknownA' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F3(bool b, UnknownA? x, UnknownB? y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownA").WithArguments("UnknownA").WithLocation(13, 28),
                // (13,41): error CS0246: The type or namespace name 'UnknownB' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F3(bool b, UnknownA? x, UnknownB? y)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownB").WithArguments("UnknownB").WithLocation(13, 41),
                // (10,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x : y").WithLocation(10, 10),
                // (11,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y : x").WithLocation(11, 10),
                // (15,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x : y").WithLocation(15, 10),
                // (16,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y : x").WithLocation(16, 10));
        }

        [Fact]
        public void ConditionalOperator_09()
        {
            var source =
@"struct A { }
struct B { }
class C
{
    static void F1(bool b, A x, B y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
    static void F2(bool b, A x, C y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
    static void F3(bool b, B x, C? y)
    {
        (b ? x : y).ToString();
        (b ? y : x).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (7,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'A' and 'B'
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? x : y").WithArguments("A", "B").WithLocation(7, 10),
                // (8,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'B' and 'A'
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? y : x").WithArguments("B", "A").WithLocation(8, 10),
                // (12,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'A' and 'C'
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? x : y").WithArguments("A", "C").WithLocation(12, 10),
                // (13,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'C' and 'A'
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? y : x").WithArguments("C", "A").WithLocation(13, 10),
                // (17,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'B' and 'C'
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? x : y").WithArguments("B", "C").WithLocation(17, 10),
                // (18,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'C' and 'B'
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? y : x").WithArguments("C", "B").WithLocation(18, 10),
                // (17,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x : y").WithLocation(17, 10),
                // (18,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? y : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? y : x").WithLocation(18, 10));
        }

        [Fact]
        public void ConditionalOperator_10()
        {
            var source =
@"using System;
class C
{
    static void F(bool b, object? x, object y)
    {
        (b ? x : throw new Exception()).ToString();
        (b ? y : throw new Exception()).ToString();
        (b ? throw new Exception() : x).ToString();
        (b ? throw new Exception() : y).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (6,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? x : throw new Exception()).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? x : throw new Exception()").WithLocation(6, 10),
                // (8,10): warning CS8602: Possible dereference of a null reference.
                //         (b ? throw new Exception() : x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "b ? throw new Exception() : x").WithLocation(8, 10));
        }

        [Fact]
        public void ConditionalOperator_11()
        {
            var source =
@"class C
{
    static void F(bool b, object x)
    {
        (b ? x : throw null).ToString();
        (b ? throw null : x).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void ConditionalOperator_12()
        {
            var source =
@"using System;
class C
{
    static void F(bool b)
    {
        (b ? throw new Exception() : throw new Exception()).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (6,10): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between '<throw expression>' and '<throw expression>'
                //         (b ? throw new Exception() : throw new Exception()).ToString();
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? throw new Exception() : throw new Exception()").WithArguments("<throw expression>", "<throw expression>").WithLocation(6, 10));
        }

        [Fact]
        public void ConditionalOperator_13()
        {
            var source =
@"class C
{
    static bool F(object? x)
    {
        return true;
    }
    static void F1(bool c, bool b1, bool b2, object v1)
    {
        object x1;
        object y1;
        object? z1 = null;
        object? w1 = null;
        if (c ? b1 && F(x1 = v1) && F(z1 = v1) : b2 && F(y1 = v1) && F(w1 = v1))
        {
            x1.ToString(); // unassigned (if)
            y1.ToString(); // unassigned (if)
            z1.ToString(); // may be null (if)
            w1.ToString(); // may be null (if)
        }
        else
        {
            x1.ToString(); // unassigned (no error) (else)
            y1.ToString(); // unassigned (no error) (else)
            z1.ToString(); // may be null (else)
            w1.ToString(); // may be null (else)
        }
    }
    static void F2(bool b1, bool b2, object v2)
    {
        object x2;
        object y2;
        object? z2 = null;
        object? w2 = null;
        if (true ? b1 && F(x2 = v2) && F(z2 = v2) : b2 && F(y2 = v2) && F(w2 = v2))
        {
            x2.ToString(); // ok (if)
            y2.ToString(); // unassigned (if)
            z2.ToString(); // ok (if)
            w2.ToString(); // may be null (if)
        }
        else
        {
            x2.ToString(); // unassigned (else)
            y2.ToString(); // unassigned (no error) (else)
            z2.ToString(); // may be null (else)
            w2.ToString(); // may be null (else)
        }
    }
    static void F3(bool b1, bool b2, object v3)
    {
        object x3;
        object y3;
        object? z3 = null;
        object? w3 = null;
        if (false ? b1 && F(x3 = v3) && F(z3 = v3) : b2 && F(y3 = v3) && F(w3 = v3))
        {
            x3.ToString(); // unassigned (if)
            y3.ToString(); // ok (if)
            z3.ToString(); // may be null (if)
            w3.ToString(); // ok (if)
        }
        else
        {
            x3.ToString(); // unassigned (no error) (else)
            y3.ToString(); // unassigned (else)
            z3.ToString(); // may be null (else)
            w3.ToString(); // may be null (else)
        }
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (15,13): error CS0165: Use of unassigned local variable 'x1'
                //             x1.ToString(); // unassigned (if)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "x1").WithArguments("x1").WithLocation(15, 13),
                // (16,13): error CS0165: Use of unassigned local variable 'y1'
                //             y1.ToString(); // unassigned (if)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "y1").WithArguments("y1").WithLocation(16, 13),
                // (17,13): warning CS8602: Possible dereference of a null reference.
                //             z1.ToString(); // may be null (if)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "z1").WithLocation(17, 13),
                // (18,13): warning CS8602: Possible dereference of a null reference.
                //             w1.ToString(); // may be null (if)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "w1").WithLocation(18, 13),
                // (24,13): warning CS8602: Possible dereference of a null reference.
                //             z1.ToString(); // may be null (else)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "z1").WithLocation(24, 13),
                // (25,13): warning CS8602: Possible dereference of a null reference.
                //             w1.ToString(); // may be null (else)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "w1").WithLocation(25, 13),
                // (37,13): error CS0165: Use of unassigned local variable 'y2'
                //             y2.ToString(); // unassigned (if)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "y2").WithArguments("y2").WithLocation(37, 13),
                // (43,13): error CS0165: Use of unassigned local variable 'x2'
                //             x2.ToString(); // unassigned (else)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "x2").WithArguments("x2").WithLocation(43, 13),
                // (39,13): warning CS8602: Possible dereference of a null reference.
                //             w2.ToString(); // may be null (if)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "w2").WithLocation(39, 13),
                // (45,13): warning CS8602: Possible dereference of a null reference.
                //             z2.ToString(); // may be null (else)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "z2").WithLocation(45, 13),
                // (46,13): warning CS8602: Possible dereference of a null reference.
                //             w2.ToString(); // may be null (else)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "w2").WithLocation(46, 13),
                // (57,13): error CS0165: Use of unassigned local variable 'x3'
                //             x3.ToString(); // unassigned (if)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "x3").WithArguments("x3").WithLocation(57, 13),
                // (65,13): error CS0165: Use of unassigned local variable 'y3'
                //             y3.ToString(); // unassigned (else)
                Diagnostic(ErrorCode.ERR_UseDefViolation, "y3").WithArguments("y3").WithLocation(65, 13),
                // (59,13): warning CS8602: Possible dereference of a null reference.
                //             z3.ToString(); // may be null (if)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "z3").WithLocation(59, 13),
                // (66,13): warning CS8602: Possible dereference of a null reference.
                //             z3.ToString(); // may be null (else)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "z3").WithLocation(66, 13),
                // (67,13): warning CS8602: Possible dereference of a null reference.
                //             w3.ToString(); // may be null (else)
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "w3").WithLocation(67, 13));
        }

        [Fact]
        public void NullCoalescingOperator_01()
        {
            var source =
@"class C
{
    static void F(object? x, object? y)
    {
        var z = x ?? y;
        z.ToString();
        if (y == null) return;
        var w = x ?? y;
        w.ToString();
        var v = null ?? x;
        v.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (6,9): warning CS8602: Possible dereference of a null reference.
                //         z.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "z").WithLocation(6, 9),
                // (11,9): warning CS8602: Possible dereference of a null reference.
                //         v.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "v").WithLocation(11, 9));
        }

        [Fact]
        public void NullCoalescingOperator_02()
        {
            var source =
@"class C
{
    static void F(object? x, object? y)
    {
        (x ?? y).ToString();
        if (y != null) (x ?? y).ToString();
        if (y != null) (y ?? x).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,10): warning CS8602: Possible dereference of a null reference.
                //         (x ?? y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x ?? y").WithLocation(5, 10),
                // (7,25): hidden CS8607: Expression is probably never null.
                //         if (y != null) (y ?? x).ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y").WithLocation(7, 25));
        }

        [Fact]
        public void NullCoalescingOperator_03()
        {
            var source =
@"class C
{
    static void F(object x, object? y)
    {
        (null ?? null).ToString();
        (null ?? x).ToString();
        (null ?? y).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,10): error CS0019: Operator '??' cannot be applied to operands of type '<null>' and '<null>'
                //         (null ?? null).ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "null ?? null").WithArguments("??", "<null>", "<null>").WithLocation(5, 10),
                // (7,10): warning CS8602: Possible dereference of a null reference.
                //         (null ?? y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "null ?? y").WithLocation(7, 10));
        }

        [Fact]
        public void NullCoalescingOperator_04()
        {
            var source =
@"class C
{
    static void F(string x, string? y)
    {
        ("""" ?? x).ToString();
        ("""" ?? y).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void NullCoalescingOperator_05()
        {
            var source0 =
@"public class A { }
public class B { }
public class UnknownNull
{
    public A A;
    public B B;
}";
            var comp0 = CreateStandardCompilation(source0, parseOptions: TestOptions.Regular7);
            comp0.VerifyDiagnostics();
            var ref0 = comp0.EmitToImageReference();

            var source1 =
@"public class MaybeNull
{
    public A? A;
    public B? B;
}
public class NotNull
{
    public A A = new A();
    public B B = new B();
}";
            var comp1 = CreateStandardCompilation(source1, references: new[] { ref0 }, parseOptions: TestOptions.Regular8);
            comp1.VerifyDiagnostics();
            var ref1 = comp1.EmitToImageReference();

            var source =
@"class C
{
    static void F1(UnknownNull x1, UnknownNull y1)
    {
        (x1.A ?? y1.B)/*T:*/.ToString();
    }
    static void F2(UnknownNull x2, MaybeNull y2)
    {
        (x2.A ?? y2.B)/*T:?*/.ToString();
    }
    static void F3(MaybeNull x3, UnknownNull y3)
    {
        (x3.A ?? y3.B)/*T:*/.ToString();
    }
    static void F4(MaybeNull x4, MaybeNull y4)
    {
        (x4.A ?? y4.B)/*T:?*/.ToString();
    }
    static void F5(UnknownNull x5, NotNull y5)
    {
        (x5.A ?? y5.B)/*T:!*/.ToString();
    }
    static void F6(NotNull x6, UnknownNull y6)
    {
        (x6.A ?? y6.B)/*T:!*/.ToString();
    }
    static void F7(MaybeNull x7, NotNull y7)
    {
        (x7.A ?? y7.B)/*T:!*/.ToString();
    }
    static void F8(NotNull x8, MaybeNull y8)
    {
        (x8.A ?? y8.B)/*T:!*/.ToString();
    }
    static void F9(NotNull x9, NotNull y9)
    {
        (x9.A ?? y9.B)/*T:!*/.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, references: new[] { ref0, ref1 }, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (5,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x1.A ?? y1.B)/*T:*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x1.A ?? y1.B").WithArguments("??", "A", "B").WithLocation(5, 10),
                // (9,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x2.A ?? y2.B)/*T:?*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x2.A ?? y2.B").WithArguments("??", "A", "B").WithLocation(9, 10),
                // (9,10): warning CS8602: Possible dereference of a null reference.
                //         (x2.A ?? y2.B)/*T:?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x2.A ?? y2.B").WithLocation(9, 10),
                // (13,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x3.A ?? y3.B)/*T:*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x3.A ?? y3.B").WithArguments("??", "A", "B").WithLocation(13, 10),
                // (17,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x4.A ?? y4.B)/*T:?*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x4.A ?? y4.B").WithArguments("??", "A", "B").WithLocation(17, 10),
                // (17,10): warning CS8602: Possible dereference of a null reference.
                //         (x4.A ?? y4.B)/*T:?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x4.A ?? y4.B").WithLocation(17, 10),
                // (21,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x5.A ?? y5.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x5.A ?? y5.B").WithArguments("??", "A", "B").WithLocation(21, 10),
                // (25,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x6.A ?? y6.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x6.A ?? y6.B").WithArguments("??", "A", "B").WithLocation(25, 10),
                // (25,10): hidden CS8607: Expression is probably never null.
                //         (x6.A ?? y6.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "x6.A").WithLocation(25, 10),
                // (29,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x7.A ?? y7.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x7.A ?? y7.B").WithArguments("??", "A", "B").WithLocation(29, 10),
                // (33,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x8.A ?? y8.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x8.A ?? y8.B").WithArguments("??", "A", "B").WithLocation(33, 10),
                // (33,10): hidden CS8607: Expression is probably never null.
                //         (x8.A ?? y8.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "x8.A").WithLocation(33, 10),
                // (37,10): error CS0019: Operator '??' cannot be applied to operands of type 'A' and 'B'
                //         (x9.A ?? y9.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.ERR_BadBinaryOps, "x9.A ?? y9.B").WithArguments("??", "A", "B").WithLocation(37, 10),
                // (37,10): hidden CS8607: Expression is probably never null.
                //         (x9.A ?? y9.B)/*T:!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "x9.A").WithLocation(37, 10));
        }

        [Fact]
        public void NullCoalescingOperator_06()
        {
            var source =
@"class C
{
    static void F1(C x1, Unknown? y1)
    {
        (x1 ?? y1)/*T:!*/.ToString();
        (y1 ?? x1)/*T:!*/.ToString();
        (null ?? y1)/*T:Unknown?*/.ToString();
        (y1 ?? null)/*T:?*/.ToString();
    }
    static void F2(C? x2, Unknown y2)
    {
        (x2 ?? y2)/*T:!*/.ToString();
        (y2 ?? x2)/*T:!*/.ToString();
        (null ?? y2)/*T:Unknown!*/.ToString();
        (y2 ?? null)/*T:!*/.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (3,26): error CS0246: The type or namespace name 'Unknown' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F1(C x1, Unknown? y1)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Unknown").WithArguments("Unknown").WithLocation(3, 26),
                // (10,27): error CS0246: The type or namespace name 'Unknown' could not be found (are you missing a using directive or an assembly reference?)
                //     static void F2(C? x2, Unknown y2)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Unknown").WithArguments("Unknown").WithLocation(10, 27),
                // (5,10): hidden CS8607: Expression is probably never null.
                //         (x1 ?? y1)/*T:!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "x1").WithLocation(5, 10),
                // (7,10): warning CS8602: Possible dereference of a null reference.
                //         (null ?? y1)/*T:Unknown?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "null ?? y1").WithLocation(7, 10),
                // (8,10): warning CS8602: Possible dereference of a null reference.
                //         (y1 ?? null)/*T:?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "y1 ?? null").WithLocation(8, 10),
                // (13,10): hidden CS8607: Expression is probably never null.
                //         (y2 ?? x2)/*T:!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y2").WithLocation(13, 10),
                // (15,10): hidden CS8607: Expression is probably never null.
                //         (y2 ?? null)/*T:!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y2").WithLocation(15, 10));
        }

        [Fact]
        public void NullCoalescingOperator_07()
        {
            var source =
@"class C
{
    static void F(object? o, object[]? a, object?[]? b)
    {
        if (o == null)
        {
            var c = new[] { o };
            (a ?? c)[0].ToString();
            (b ?? c)[0].ToString();
        }
        else
        {
            var c = new[] { o };
            (a ?? c)[0].ToString();
            (b ?? c)[0].ToString();
        }
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (8,13): warning CS8602: Possible dereference of a null reference.
                //             (a ?? c)[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(a ?? c)[0]").WithLocation(8, 13),
                // (9,13): warning CS8602: Possible dereference of a null reference.
                //             (b ?? c)[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(b ?? c)[0]").WithLocation(9, 13),
                // (15,13): warning CS8602: Possible dereference of a null reference.
                //             (b ?? c)[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(b ?? c)[0]").WithLocation(15, 13));
        }

        [Fact]
        public void NullCoalescingOperator_08()
        {
            var source =
@"interface I<T> { }
class C
{
    static object? F((I<object>, I<object?>)? x, (I<object?>, I<object>)? y)
    {
        return x ?? y;
    }
    static object F((I<object>, I<object?>)? x, (I<object?>, I<object>) y)
    {
        return x ?? y;
    }
}";
            var comp = CreateStandardCompilation(source, references: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (6,21): warning CS8619: Nullability of reference types in value of type '(I<object?>, I<object>)?' doesn't match target type '(I<object>, I<object?>)?'.
                //         return x ?? y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("(I<object?>, I<object>)?", "(I<object>, I<object?>)?").WithLocation(6, 21),
                // (10,21): warning CS8619: Nullability of reference types in value of type '(I<object?>, I<object>)' doesn't match target type '(I<object>, I<object?>)'.
                //         return x ?? y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("(I<object?>, I<object>)", "(I<object>, I<object?>)").WithLocation(10, 21));
        }

        [Fact]
        public void AnonymousObjectCreation_01()
        {
            var source =
@"class C
{
    static void F(object? o)
    {
        (new { P = o }).P.ToString();
        if (o == null) return;
        (new { Q = o }).Q.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,9): warning CS8602: Possible dereference of a null reference.
                //         (new { P = o }).P.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new { P = o }).P").WithLocation(5, 9));
        }

        // PROTOTYPE(NullableReferenceType): NullableWalker.VisitAnonymousObjectCreationExpression
        // should support initializers with inferred nullability.
        [Fact(Skip = "TODO")]
        public void AnonymousObjectCreation_02()
        {
            var source =
@"class C
{
    static void F(object? o)
    {
        (new { P = new[] { o }}).P[0].ToString();
        if (o == null) return;
        (new { Q = new[] { o }}).Q[0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,9): warning CS8602: Possible dereference of a null reference.
                //         (new { P = new[] { o }}).P[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new { P = new[] { o }}).P[0]").WithLocation(5, 9));
        }

        [Fact]
        public void ObjectInitializer_01()
        {
            var source =
@"class C
{
    C(object o) { }
    static void F(object? x)
    {
        var y = new C(x);
        if (x != null) y = new C(x);
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (6,23): warning CS8604: Possible null reference argument for parameter 'o' in 'C.C(object o)'.
                //         var y = new C(x);
                Diagnostic(ErrorCode.WRN_NullReferenceArgument, "x").WithArguments("o", "C.C(object o)").WithLocation(6, 23));
        }

        [Fact]
        public void ObjectInitializer_02()
        {
            var source =
@"class A
{
    internal B F = new B();
}
class B
{
    internal object? G;
}
class C
{
    static void Main()
    {
        var o = new A() { F = { G = new object() } };
        o.F.G.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        // PROTOTYPE(NullableReferenceTypes): Track nullability through deconstruction assignment.
        [Fact(Skip = "TODO")]
        public void DeconstructionTypeInference()
        {
            var source =
@"class C
{
    static void F((object? a, object? b) t)
    {
        if (t.b == null) return;
        object? x;
        object? y;
        (x, y) = t;
        x.ToString();
        y.ToString();
    }
    static void F(object? a, object? b)
    {
        if (b == null) return;
        object? x;
        object? y;
        (x, y) = (a, b);
        x.ToString();
        y.ToString();
    }
}";
            var comp = CreateStandardCompilation(
                source,
                references: new[] { ValueTupleRef, SystemRuntimeFacadeRef },
                parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (9,9): warning CS8602: Possible dereference of a null reference.
                //         x.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x").WithLocation(9, 9),
                // (18,9): warning CS8602: Possible dereference of a null reference.
                //         x.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x").WithLocation(18, 9));
        }

        [Fact]
        public void DynamicInvocation()
        {
            var source =
@"class C
{
    static void F(object x, object y)
    {
    }
    static void G(object? x, dynamic y)
    {
        F(x, y);
        if (x != null) F(y, x);
    }
}";
            var comp = CreateCompilationWithMscorlibAndSystemCore(source, parseOptions: TestOptions.Regular8);
            // PROTOTYPE(NullableReferenceTypes): We should be able to report warnings
            // when all applicable methods agree on the nullability of particular parameters.
            // (For instance, x in F(x, y) above.)
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DynamicObjectCreation_01()
        {
            var source =
@"class C
{
    C(object x, object y)
    {
    }
    static void G(object? x, dynamic y)
    {
        var o = new C(x, y);
        if (x != null) o = new C(y, x);
    }
}";
            var comp = CreateCompilationWithMscorlibAndSystemCore(source, parseOptions: TestOptions.Regular8);
            // PROTOTYPE(NullableReferenceTypes): We should be able to report warnings
            // when all applicable methods agree on the nullability of particular parameters.
            // (For instance, x in F(x, y) above.)
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void DynamicObjectCreation_02()
        {
            var source =
@"class C
{
    C(object f)
    {
        F = f;
    }
    object? F;
    object? G;
    static void M(dynamic d)
    {
        var o = new C(d) { G = new object() };
        o.G.ToString();
    }
}";
            var comp = CreateCompilationWithMscorlibAndSystemCore(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void TypeInference_01()
        {
            var source =
@"class C<T>
{
    internal T F;
}
class C
{
    static C<T> Create<T>(T t)
    {
        return new C<T>();
    }
    static void F(object? x)
    {
        if (x == null)
        {
            Create(x).F = null;
            var y = Create(x);
            y.F = null;
        }
        else
        {
            Create(x).F = null; // warn
            var y = Create(x);
            y.F = null; // warn
        }
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (1,7): warning CS8618: Non-nullable field 'F' is uninitialized.
                // class C<T>
                Diagnostic(ErrorCode.WRN_UninitializedNonNullableField, "C").WithArguments("field", "F").WithLocation(1, 7),
                // (21,27): warning CS8625: Cannot convert null literal to non-nullable reference or unconstrained type parameter.
                //             Create(x).F = null; // warn
                Diagnostic(ErrorCode.WRN_NullAsNonNullable, "null").WithLocation(21, 27),
                // (23,19): warning CS8625: Cannot convert null literal to non-nullable reference or unconstrained type parameter.
                //             y.F = null; // warn
                Diagnostic(ErrorCode.WRN_NullAsNonNullable, "null").WithLocation(23, 19));
        }

        [Fact]
        public void TypeInference_ArgumentOrder()
        {
            var source =
@"interface I<T>
{
     T P { get; }
}
class C
{
    static T F<T, U>(I<T> x, I<U> y) => x.P;
    static void M(I<object?> x, I<string> y)
    {
        F(y: y, x: x).ToString();
    }
}";
            var comp = CreateCompilationWithMscorlibAndSystemCore(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (10,9): warning CS8602: Possible dereference of a null reference.
                //         F(y: y, x: x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "F(y: y, x: x)").WithLocation(10, 9));
        }

        [Fact]
        public void LambdaReturnValue_01()
        {
            var source =
@"using System;
class C
{
    static void F(Func<object> f)
    {
    }
    static void G(string x, object? y)
    {
        F(() => { if ((object)x == y) return x; return y; });
        F(() => { if (y == null) return x; return y; });
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (9,56): warning CS8603: Possible null reference return.
                //         F(() => { if ((object)x == y) return x; return y; });
                Diagnostic(ErrorCode.WRN_NullReferenceReturn, "y").WithLocation(9, 56));
        }

        [Fact]
        public void LambdaReturnValue_02()
        {
            var source =
@"using System;
class C
{
    static void F(Func<object> f)
    {
    }
    static void G(bool b, object x, string? y)
    {
        F(() => { if (b) return x; return y; });
        F(() => { if (b) return y; return x; });
    }
    static void H(bool b, object? x, string y)
    {
        F(() => { if (b) return x; return y; });
        F(() => { if (b) return y; return x; });
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (9,43): warning CS8603: Possible null reference return.
                //         F(() => { if (b) return x; return y; });
                Diagnostic(ErrorCode.WRN_NullReferenceReturn, "y").WithLocation(9, 43),
                // (10,33): warning CS8603: Possible null reference return.
                //         F(() => { if (b) return y; return x; });
                Diagnostic(ErrorCode.WRN_NullReferenceReturn, "y").WithLocation(10, 33),
                // (14,33): warning CS8603: Possible null reference return.
                //         F(() => { if (b) return x; return y; });
                Diagnostic(ErrorCode.WRN_NullReferenceReturn, "x").WithLocation(14, 33),
                // (15,43): warning CS8603: Possible null reference return.
                //         F(() => { if (b) return y; return x; });
                Diagnostic(ErrorCode.WRN_NullReferenceReturn, "x").WithLocation(15, 43));
        }

        // PROTOTYPE(NullableReferenceTypes): Infer lambda return type nullability.
        [Fact(Skip = "TODO")]
        public void LambdaReturnValue_03()
        {
            var source =
@"using System;
class C
{
    static T F<T>(Func<T> f)
    {
        return default(T);
    }
    static void G(bool b, object x, string? y)
    {
        F(() => { if (b) return x; return y; }).ToString();
    }
    static void H(bool b, object? x, string y)
    {
        F(() => { if (b) return x; return y; }).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (10,9): warning CS8602: Possible dereference of a null reference.
                //         F(() => { if (b) return x; return y; }).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "F(() => { if (b) return x; return y; })").WithLocation(10, 9),
                // (14,9): warning CS8602: Possible dereference of a null reference.
                //         F(() => { if (b) return x; return y; }).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "F(() => { if (b) return x; return y; })").WithLocation(14, 9));
        }

        // PROTOTYPE(NullableReferenceTypes): Infer lambda return type nullability.
        [Fact(Skip = "TODO")]
        public void LambdaReturnValue_04()
        {
            var source =
@"using System;
class C
{
    static T F<T>(Func<T> f)
    {
        return default(T);
    }
    static void G(object? o)
    {
        F(() => o).ToString();
        if (o != null) F(() => o).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (10,9): warning CS8602: Possible dereference of a null reference.
                //         F(() => o).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "F(() => o)").WithLocation(10, 9));
        }

        // PROTOTYPE(NullableReferenceTypes): Infer lambda return type nullability.
        [Fact(Skip = "TODO")]
        public void LambdaReturnValue_05()
        {
            var source =
@"using System;
class C
{
    static T F<T>(Func<object?, T> f)
    {
        return default(T);
    }
    static void G()
    {
        F(o => { if (o == null) throw new ArgumentException(); return o; }).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void IdentityConversion_NullCoalescingOperator_01()
        {
            var source =
@"interface I<T> { }
interface IIn<in T> { }
interface IOut<out T> { }
class C
{
    static void F1(I<object>? x1, I<object?> y1)
    {
        I<object> z1 = x1 ?? y1;
        I<object?> w1 = y1 ?? x1;
    }
    static void F2(IIn<object>? x2, IIn<object?> y2)
    {
        IIn<object> z2 = x2 ?? y2;
        IIn<object?> w2 = y2 ?? x2;
    }
    static void F3(IOut<object>? x3, IOut<object?> y3)
    {
        IOut<object> z3 = x3 ?? y3;
        IOut<object?> w3 = y3 ?? x3;
    }
    static void F4(IIn<object>? x4, IIn<object> y4)
    {
        IIn<object> z4;
        z4 = ((IIn<object?>)x4) ?? y4;
        z4 = x4 ?? (IIn<object?>)y4;
    }
    static void F5(IIn<object?>? x5, IIn<object?> y5)
    {
        IIn<object> z5;
        z5 = ((IIn<object>)x5) ?? y5;
        z5 = x5 ?? (IIn<object>)y5;
    }
    static void F6(IOut<object?>? x6, IOut<object?> y6)
    {
        IOut<object?> z6;
        z6 = ((IOut<object>)x6) ?? y6;
        z6 = x6 ?? (IOut<object>)y6;
    }
    static void F7(IOut<object>? x7, IOut<object> y7)
    {
        IOut<object?> z7;
        z7 = ((IOut<object?>)x7) ?? y7;
        z7 = x7 ?? (IOut<object?>)y7;
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (8,30): warning CS8619: Nullability of reference types in value of type 'I<object?>' doesn't match target type 'I<object>'.
                //         I<object> z1 = x1 ?? y1;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y1").WithArguments("I<object?>", "I<object>").WithLocation(8, 30),
                // (9,25): hidden CS8607: Expression is probably never null.
                //         I<object?> w1 = y1 ?? x1;
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y1").WithLocation(9, 25),
                // (9,31): warning CS8619: Nullability of reference types in value of type 'I<object>' doesn't match target type 'I<object?>'.
                //         I<object?> w1 = y1 ?? x1;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x1").WithArguments("I<object>", "I<object?>").WithLocation(9, 31),
                // (14,27): hidden CS8607: Expression is probably never null.
                //         IIn<object?> w2 = y2 ?? x2;
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y2").WithLocation(14, 27),
                // (14,27): warning CS8619: Nullability of reference types in value of type 'IIn<object>' doesn't match target type 'IIn<object?>'.
                //         IIn<object?> w2 = y2 ?? x2;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y2 ?? x2").WithArguments("IIn<object>", "IIn<object?>").WithLocation(14, 27),
                // (18,27): warning CS8619: Nullability of reference types in value of type 'IOut<object?>' doesn't match target type 'IOut<object>'.
                //         IOut<object> z3 = x3 ?? y3;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x3 ?? y3").WithArguments("IOut<object?>", "IOut<object>").WithLocation(18, 27),
                // (19,28): hidden CS8607: Expression is probably never null.
                //         IOut<object?> w3 = y3 ?? x3;
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y3").WithLocation(19, 28),
                // (24,15): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         z4 = ((IIn<object?>)x4) ?? y4;
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(IIn<object?>)x4").WithLocation(24, 15),
                // (30,15): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         z5 = ((IIn<object>)x5) ?? y5;
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(IIn<object>)x5").WithLocation(30, 15),
                // (36,15): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         z6 = ((IOut<object>)x6) ?? y6;
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(IOut<object>)x6").WithLocation(36, 15),
                // (42,15): warning CS8600: Converting null literal or possible null value to non-nullable type.
                //         z7 = ((IOut<object?>)x7) ?? y7;
                Diagnostic(ErrorCode.WRN_ConvertingNullableToNonNullable, "(IOut<object?>)x7").WithLocation(42, 15));
        }

        [Fact]
        public void IdentityConversion_NullCoalescingOperator_02()
        {
            var source =
@"interface IIn<in T> { }
interface IOut<out T> { }
class C
{
    static IIn<T>? FIn<T>(T x)
    {
        return null;
    }
    static IOut<T>? FOut<T>(T x)
    {
        return null;
    }
    static void FIn(IIn<object?>? x)
    {
    }
    static T FOut<T>(IOut<T>? x)
    {
        throw new System.Exception();
    }
    static void F1(IIn<object>? x1, IIn<object?>? y1)
    {
        FIn((x1 ?? y1)/*T:IIn<object!>?*/);
        FIn((y1 ?? x1)/*T:IIn<object!>?*/);
    }
    static void F2(IOut<object>? x2, IOut<object?>? y2)
    {
        FOut((x2 ?? y2)/*T:IOut<object?>?*/).ToString();
        FOut((y2 ?? x2)/*T:IOut<object?>?*/).ToString();
    }
    static void F3(object? x3, object? y3)
    {
        FIn((FIn(x3) ?? FIn(y3))/*T:IIn<object?>?*/); // A
        if (x3 == null) return;
        FIn((FIn(x3) ?? FIn(y3))/*T:IIn<object!>?*/); // B
        FIn((FIn(y3) ?? FIn(x3))/*T:IIn<object!>?*/); // C
        if (y3 == null) return;
        FIn((FIn(x3) ?? FIn(y3))/*T:IIn<object!>?*/); // D
    }
    static void F4(object? x4, object? y4)
    {
        FOut((FOut(x4) ?? FOut(y4))/*T:IOut<object?>?*/).ToString(); // A
        if (x4 == null) return;
        FOut((FOut(x4) ?? FOut(y4))/*T:IOut<object?>?*/).ToString(); // B
        FOut((FOut(y4) ?? FOut(x4))/*T:IOut<object?>?*/).ToString(); // C
        if (y4 == null) return;
        FOut((FOut(x4) ?? FOut(y4))/*T:IOut<object!>?*/).ToString(); // D
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (22,14): warning CS8620: Nullability of reference types in argument of type 'IIn<object>' doesn't match target type 'IIn<object?>' for parameter 'x' in 'void C.FIn(IIn<object?>? x)'.
                //         FIn((x1 ?? y1)/*T:IIn<object!>?*/);
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInArgument, "x1 ?? y1").WithArguments("IIn<object>", "IIn<object?>", "x", "void C.FIn(IIn<object?>? x)").WithLocation(22, 14),
                // (23,14): warning CS8620: Nullability of reference types in argument of type 'IIn<object>' doesn't match target type 'IIn<object?>' for parameter 'x' in 'void C.FIn(IIn<object?>? x)'.
                //         FIn((y1 ?? x1)/*T:IIn<object!>?*/);
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInArgument, "y1 ?? x1").WithArguments("IIn<object>", "IIn<object?>", "x", "void C.FIn(IIn<object?>? x)").WithLocation(23, 14),
                // (27,9): warning CS8602: Possible dereference of a null reference.
                //         FOut((x2 ?? y2)/*T:IOut<object?>?*/).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "FOut((x2 ?? y2)/*T:IOut<object?>?*/)").WithLocation(27, 9),
                // (28,9): warning CS8602: Possible dereference of a null reference.
                //         FOut((y2 ?? x2)/*T:IOut<object?>?*/).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "FOut((y2 ?? x2)/*T:IOut<object?>?*/)").WithLocation(28, 9),
                // (34,14): warning CS8620: Nullability of reference types in argument of type 'IIn<object>' doesn't match target type 'IIn<object?>' for parameter 'x' in 'void C.FIn(IIn<object?>? x)'.
                //         FIn((FIn(x3) ?? FIn(y3))/*T:IIn<object!>?*/); // B
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInArgument, "FIn(x3) ?? FIn(y3)").WithArguments("IIn<object>", "IIn<object?>", "x", "void C.FIn(IIn<object?>? x)").WithLocation(34, 14),
                // (35,14): warning CS8620: Nullability of reference types in argument of type 'IIn<object>' doesn't match target type 'IIn<object?>' for parameter 'x' in 'void C.FIn(IIn<object?>? x)'.
                //         FIn((FIn(y3) ?? FIn(x3))/*T:IIn<object!>?*/); // C
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInArgument, "FIn(y3) ?? FIn(x3)").WithArguments("IIn<object>", "IIn<object?>", "x", "void C.FIn(IIn<object?>? x)").WithLocation(35, 14),
                // (37,14): warning CS8620: Nullability of reference types in argument of type 'IIn<object>' doesn't match target type 'IIn<object?>' for parameter 'x' in 'void C.FIn(IIn<object?>? x)'.
                //         FIn((FIn(x3) ?? FIn(y3))/*T:IIn<object!>?*/); // D
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInArgument, "FIn(x3) ?? FIn(y3)").WithArguments("IIn<object>", "IIn<object?>", "x", "void C.FIn(IIn<object?>? x)").WithLocation(37, 14),
                // (41,9): warning CS8602: Possible dereference of a null reference.
                //         FOut((FOut(x4) ?? FOut(y4))/*T:IOut<object?>?*/).ToString(); // A
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "FOut((FOut(x4) ?? FOut(y4))/*T:IOut<object?>?*/)").WithLocation(41, 9),
                // (43,9): warning CS8602: Possible dereference of a null reference.
                //         FOut((FOut(x4) ?? FOut(y4))/*T:IOut<object?>?*/).ToString(); // B
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "FOut((FOut(x4) ?? FOut(y4))/*T:IOut<object?>?*/)").WithLocation(43, 9),
                // (44,9): warning CS8602: Possible dereference of a null reference.
                //         FOut((FOut(y4) ?? FOut(x4))/*T:IOut<object?>?*/).ToString(); // C
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "FOut((FOut(y4) ?? FOut(x4))/*T:IOut<object?>?*/)").WithLocation(44, 9));
        }

        [Fact]
        public void IdentityConversion_NullCoalescingOperator_03()
        {
            var source =
@"class C
{
    static void F((object?, object?)? x, (object, object) y)
    {
        (x ?? y).Item1.ToString();
    }
    static void G((object, object)? x, (object?, object?) y)
    {
        (x ?? y).Item1.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, references: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,9): warning CS8602: Possible dereference of a null reference.
                //         (x ?? y).Item1.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(x ?? y).Item1").WithLocation(5, 9),
                // (9,9): warning CS8602: Possible dereference of a null reference.
                //         (x ?? y).Item1.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(x ?? y).Item1").WithLocation(9, 9));
        }

        [Fact]
        public void IdentityConversion_NullCoalescingOperator_04()
        {
            var source =
@"#pragma warning disable 0649
struct A<T>
{
    public static implicit operator B<T>(A<T> a) => default;
}
struct B<T>
{
    internal T F;
}
class C
{
    static void F1(A<object>? x1, B<object?> y1)
    {
        (x1 ?? y1)/*T:B<object?>!*/.F.ToString();
    }
    static void F2(A<object?>? x2, B<object> y2)
    {
        (x2 ?? y2)/*T:B<object!>!*/.F.ToString();
    }
    static void F3(A<object> x3, B<object?>? y3)
    {
        (y3 ?? x3)/*T:B<object?>!*/.F.ToString();
    }
    static void F4(A<object?> x4, B<object>? y4)
    {
        (y4 ?? x4)/*T:B<object!>!*/.F.ToString();
    }
    static void F5(A<object>? x5, B<object?>? y5)
    {
        (x5 ?? y5)/*T:B<object?>?*/.Value.F.ToString();
        (y5 ?? x5)/*T:B<object?>?*/.Value.F.ToString();
    }
    static void F6(A<object?>? x6, B<object>? y6)
    {
        (x6 ?? y6)/*T:B<object!>?*/.Value.F.ToString();
        (y6 ?? x6)/*T:B<object!>?*/.Value.F.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, references: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (14,10): warning CS8619: Nullability of reference types in value of type 'A<object>' doesn't match target type 'B<object?>'.
                //         (x1 ?? y1)/*T:B<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x1").WithArguments("A<object>", "B<object?>").WithLocation(14, 10),
                // (14,9): warning CS8602: Possible dereference of a null reference.
                //         (x1 ?? y1)/*T:B<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(x1 ?? y1)/*T:B<object?>!*/.F").WithLocation(14, 9),
                // (18,10): warning CS8619: Nullability of reference types in value of type 'A<object?>' doesn't match target type 'B<object>'.
                //         (x2 ?? y2)/*T:B<object!>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x2").WithArguments("A<object?>", "B<object>").WithLocation(18, 10),
                // (22,16): warning CS8619: Nullability of reference types in value of type 'B<object>' doesn't match target type 'B<object?>'.
                //         (y3 ?? x3)/*T:B<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x3").WithArguments("B<object>", "B<object?>").WithLocation(22, 16),
                // (22,9): warning CS8602: Possible dereference of a null reference.
                //         (y3 ?? x3)/*T:B<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(y3 ?? x3)/*T:B<object?>!*/.F").WithLocation(22, 9),
                // (26,16): warning CS8619: Nullability of reference types in value of type 'B<object?>' doesn't match target type 'B<object>'.
                //         (y4 ?? x4)/*T:B<object!>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x4").WithArguments("B<object?>", "B<object>").WithLocation(26, 16),
                // (30,10): warning CS8619: Nullability of reference types in value of type 'A<object>' doesn't match target type 'B<object?>?'.
                //         (x5 ?? y5)/*T:B<object?>?*/.Value.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x5").WithArguments("A<object>", "B<object?>?").WithLocation(30, 10),
                // (30,9): warning CS8602: Possible dereference of a null reference.
                //         (x5 ?? y5)/*T:B<object?>?*/.Value.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(x5 ?? y5)/*T:B<object?>?*/.Value.F").WithLocation(30, 9),
                // (31,16): warning CS8619: Nullability of reference types in value of type 'B<object>?' doesn't match target type 'B<object?>?'.
                //         (y5 ?? x5)/*T:B<object?>?*/.Value.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x5").WithArguments("B<object>?", "B<object?>?").WithLocation(31, 16),
                // (31,9): warning CS8602: Possible dereference of a null reference.
                //         (y5 ?? x5)/*T:B<object?>?*/.Value.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(y5 ?? x5)/*T:B<object?>?*/.Value.F").WithLocation(31, 9),
                // (35,10): warning CS8619: Nullability of reference types in value of type 'A<object?>' doesn't match target type 'B<object>?'.
                //         (x6 ?? y6)/*T:B<object!>?*/.Value.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x6").WithArguments("A<object?>", "B<object>?").WithLocation(35, 10),
                // (36,16): warning CS8619: Nullability of reference types in value of type 'B<object?>?' doesn't match target type 'B<object>?'.
                //         (y6 ?? x6)/*T:B<object!>?*/.Value.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x6").WithArguments("B<object?>?", "B<object>?").WithLocation(36, 16));
        }

        // PROTOTYPE(NullableReferenceTypes): Conversions: Conversion
        // (VisitConversion ignores nullability of operand in conversion from A<T> to B<T>.)
        [Fact]
        public void IdentityConversion_NullCoalescingOperator_05()
        {
            var source =
@"#pragma warning disable 0649
struct A<T>
{
    public static implicit operator B<T>(A<T> a) => new B<T>();
}
class B<T>
{
    internal T F;
}
class C
{
    static void F1(A<object>? x1, B<object?> y1)
    {
        (x1 ?? y1)/*T:B<object?>!*/.F.ToString();
    }
    static void F2(A<object?>? x2, B<object> y2)
    {
        (x2 ?? y2)/*T:B<object!>!*/.F.ToString();
    }
    static void F3(A<object> x3, B<object?>? y3)
    {
        (y3 ?? x3)/*T:B<object?>*/.F.ToString();
    }
    static void F4(A<object?> x4, B<object>? y4)
    {
        (y4 ?? x4)/*T:B<object!>*/.F.ToString();
    }
    static void F5(A<object>? x5, B<object?>? y5)
    {
        (x5 ?? y5)/*T:B<object?>?*/.F.ToString();
        (y5 ?? x5)/*T:B<object?>*/.F.ToString();
    }
    static void F6(A<object?>? x6, B<object>? y6)
    {
        (x6 ?? y6)/*T:B<object!>?*/.F.ToString();
        (y6 ?? x6)/*T:B<object!>*/.F.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, references: new[] { ValueTupleRef, SystemRuntimeFacadeRef }, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (6,7): warning CS8618: Non-nullable field 'F' is uninitialized.
                // class B<T>
                Diagnostic(ErrorCode.WRN_UninitializedNonNullableField, "B").WithArguments("field", "F").WithLocation(6, 7),
                // (14,10): warning CS8619: Nullability of reference types in value of type 'A<object>' doesn't match target type 'B<object?>'.
                //         (x1 ?? y1)/*T:B<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x1").WithArguments("A<object>", "B<object?>").WithLocation(14, 10),
                // (14,9): warning CS8602: Possible dereference of a null reference.
                //         (x1 ?? y1)/*T:B<object?>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(x1 ?? y1)/*T:B<object?>!*/.F").WithLocation(14, 9),
                // (18,10): warning CS8619: Nullability of reference types in value of type 'A<object?>' doesn't match target type 'B<object>'.
                //         (x2 ?? y2)/*T:B<object!>!*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x2").WithArguments("A<object?>", "B<object>").WithLocation(18, 10),
                // (22,16): warning CS8619: Nullability of reference types in value of type 'B<object>' doesn't match target type 'B<object?>'.
                //         (y3 ?? x3)/*T:B<object?>*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x3").WithArguments("B<object>", "B<object?>").WithLocation(22, 16),
                // (22,9): warning CS8602: Possible dereference of a null reference.
                //         (y3 ?? x3)/*T:B<object?>*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(y3 ?? x3)/*T:B<object?>*/.F").WithLocation(22, 9),
                // (26,16): warning CS8619: Nullability of reference types in value of type 'B<object?>' doesn't match target type 'B<object>'.
                //         (y4 ?? x4)/*T:B<object!>*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x4").WithArguments("B<object?>", "B<object>").WithLocation(26, 16),
                // (30,10): warning CS8619: Nullability of reference types in value of type 'A<object>' doesn't match target type 'B<object?>'.
                //         (x5 ?? y5)/*T:B<object?>?*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x5").WithArguments("A<object>", "B<object?>").WithLocation(30, 10),
                // (30,10): warning CS8602: Possible dereference of a null reference.
                //         (x5 ?? y5)/*T:B<object?>?*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x5 ?? y5").WithLocation(30, 10),
                // (30,9): warning CS8602: Possible dereference of a null reference.
                //         (x5 ?? y5)/*T:B<object?>?*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(x5 ?? y5)/*T:B<object?>?*/.F").WithLocation(30, 9),
                // (31,16): warning CS8619: Nullability of reference types in value of type 'B<object>' doesn't match target type 'B<object?>'.
                //         (y5 ?? x5)/*T:B<object?>*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x5").WithArguments("B<object>", "B<object?>").WithLocation(31, 16),
                // (31,9): warning CS8602: Possible dereference of a null reference.
                //         (y5 ?? x5)/*T:B<object?>*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(y5 ?? x5)/*T:B<object?>*/.F").WithLocation(31, 9),
                // (35,10): warning CS8619: Nullability of reference types in value of type 'A<object?>' doesn't match target type 'B<object>'.
                //         (x6 ?? y6)/*T:B<object!>?*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x6").WithArguments("A<object?>", "B<object>").WithLocation(35, 10),
                // (35,10): warning CS8602: Possible dereference of a null reference.
                //         (x6 ?? y6)/*T:B<object!>?*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x6 ?? y6").WithLocation(35, 10),
                // (36,16): warning CS8619: Nullability of reference types in value of type 'B<object?>' doesn't match target type 'B<object>'.
                //         (y6 ?? x6)/*T:B<object!>*/.F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x6").WithArguments("B<object?>", "B<object>").WithLocation(36, 16));
        }

        [Fact]
        public void IdentityConversion_NullCoalescingOperator_06()
        {
            var source =
@"class C
{
    static void F1(object? x, dynamic? y, dynamic z)
    {
        (x ?? y).ToString();
        (x ?? z).ToString(); // ok
        (y ?? x).ToString();
        (y ?? z).ToString(); // ok
        (z ?? x).ToString();
        (z ?? y).ToString();
    }
}";
            var comp = CreateCompilationWithMscorlibAndSystemCore(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (5,10): warning CS8602: Possible dereference of a null reference.
                //         (x ?? y).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x ?? y").WithLocation(5, 10),
                // (7,10): warning CS8602: Possible dereference of a null reference.
                //         (y ?? x).ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "y ?? x").WithLocation(7, 10),
                // (9,10): hidden CS8607: Expression is probably never null.
                //         (z ?? x).ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "z").WithLocation(9, 10),
                // (10,10): hidden CS8607: Expression is probably never null.
                //         (z ?? y).ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "z").WithLocation(10, 10));
        }

        [Fact]
        public void ImplicitConversion_NullCoalescingOperator_01()
        {
            var source0 =
@"public class UnknownNull
{
    public object Object;
    public string String;
}";
            var comp0 = CreateStandardCompilation(source0, parseOptions: TestOptions.Regular7);
            comp0.VerifyDiagnostics();
            var ref0 = comp0.EmitToImageReference();

            var source1 =
@"public class MaybeNull
{
    public object? Object;
    public string? String;
}
public class NotNull
{
    public object Object = new object();
    public string String = string.Empty;
}";
            var comp1 = CreateStandardCompilation(source1, parseOptions: TestOptions.Regular8);
            comp1.VerifyDiagnostics();
            var ref1 = comp1.EmitToImageReference();

            var source =
@"class C
{
    static void F1(UnknownNull x1, UnknownNull y1)
    {
        (x1.Object ?? y1.String)/*T:object*/.ToString();
        (y1.String ?? x1.Object)/*T:object*/.ToString();
    }
    static void F2(UnknownNull x2, MaybeNull y2)
    {
        (x2.Object ?? y2.String)/*T:object?*/.ToString();
        (y2.String ?? x2.Object)/*T:object*/.ToString();
    }
    static void F3(MaybeNull x3, UnknownNull y3)
    {
        (x3.Object ?? y3.String)/*T:object*/.ToString();
        (y3.String ?? x3.Object)/*T:object?*/.ToString();
    }
    static void F4(MaybeNull x4, MaybeNull y4)
    {
        (x4.Object ?? y4.String)/*T:object?*/.ToString();
        (y4.String ?? x4.Object)/*T:object?*/.ToString();
    }
    static void F5(UnknownNull x5, NotNull y5)
    {
        (x5.Object ?? y5.String)/*T:object!*/.ToString();
        (y5.String ?? x5.Object)/*T:object!*/.ToString();
    }
    static void F6(NotNull x6, UnknownNull y6)
    {
        (x6.Object ?? y6.String)/*T:object!*/.ToString();
        (y6.String ?? x6.Object)/*T:object!*/.ToString();
    }
    static void F7(MaybeNull x7, NotNull y7)
    {
        (x7.Object ?? y7.String)/*T:object!*/.ToString();
        (y7.String ?? x7.Object)/*T:object!*/.ToString();
    }
    static void F8(NotNull x8, MaybeNull y8)
    {
        (x8.Object ?? y8.String)/*T:object!*/.ToString();
        (y8.String ?? x8.Object)/*T:object!*/.ToString();
    }
    static void F9(NotNull x9, NotNull y9)
    {
        (x9.Object ?? y9.String)/*T:object!*/.ToString();
        (y9.String ?? x9.Object)/*T:object!*/.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, references: new[] { ref0, ref1 }, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (10,10): warning CS8602: Possible dereference of a null reference.
                //         (x2.Object ?? y2.String)/*T:object?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x2.Object ?? y2.String").WithLocation(10, 10),
                // (16,10): warning CS8602: Possible dereference of a null reference.
                //         (y3.String ?? x3.Object)/*T:object?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "y3.String ?? x3.Object").WithLocation(16, 10),
                // (20,10): warning CS8602: Possible dereference of a null reference.
                //         (x4.Object ?? y4.String)/*T:object?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x4.Object ?? y4.String").WithLocation(20, 10),
                // (21,10): warning CS8602: Possible dereference of a null reference.
                //         (y4.String ?? x4.Object)/*T:object?*/.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "y4.String ?? x4.Object").WithLocation(21, 10),
                // (26,10): hidden CS8607: Expression is probably never null.
                //         (y5.String ?? x5.Object)/*T:object*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y5.String").WithLocation(26, 10),
                // (30,10): hidden CS8607: Expression is probably never null.
                //         (x6.Object ?? y6.String)/*T:object*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "x6.Object").WithLocation(30, 10),
                // (36,10): hidden CS8607: Expression is probably never null.
                //         (y7.String ?? x7.Object)/*T:object!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y7.String").WithLocation(36, 10),
                // (40,10): hidden CS8607: Expression is probably never null.
                //         (x8.Object ?? y8.String)/*T:object*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "x8.Object").WithLocation(40, 10),
                // (45,10): hidden CS8607: Expression is probably never null.
                //         (x9.Object ?? y9.String)/*T:object!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "x9.Object").WithLocation(45, 10),
                // (46,10): hidden CS8607: Expression is probably never null.
                //         (y9.String ?? x9.Object)/*T:object!*/.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y9.String").WithLocation(46, 10));
        }

        // PROTOTYPE(NullableReferenceTypes): Conversions: Conversion
        // (VisitConversion does not report nullability mismatch for the conversion in node.Right.)
        [Fact]
        public void ImplicitConversion_NullCoalescingOperator_02()
        {
            var source =
@"#pragma warning disable 0649
class A<T>
{
    internal T F;
}
class B<T> : A<T> { }
class C
{
    static void F(A<object>? x, B<object?> y)
    {
        (x ?? y).F.ToString();
        (y ?? x).F.ToString();
    }
    static void G(A<object?> z, B<object>? w)
    {
        (z ?? w).F.ToString();
        (w ?? z).F.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (2,7): warning CS8618: Non-nullable field 'F' is uninitialized.
                // class A<T>
                Diagnostic(ErrorCode.WRN_UninitializedNonNullableField, "A").WithArguments("field", "F").WithLocation(2, 7),
                // (11,15): warning CS8619: Nullability of reference types in value of type 'B<object?>' doesn't match target type 'A<object>'.
                //         (x ?? y).F.ToString();
                //Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("B<object?>", "A<object>").WithLocation(11, 15),
                // (12,10): hidden CS8607: Expression is probably never null.
                //         (y ?? x).F.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y").WithLocation(12, 10),
                // (12,10): warning CS8619: Nullability of reference types in value of type 'B<object?>' doesn't match target type 'A<object>'.
                //         (y ?? x).F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("B<object?>", "A<object>").WithLocation(12, 10),
                // (16,10): hidden CS8607: Expression is probably never null.
                //         (z ?? w).F.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "z").WithLocation(16, 10),
                // (16,15): warning CS8619: Nullability of reference types in value of type 'B<object>' doesn't match target type 'A<object?>'.
                //         (z ?? w).F.ToString();
                //Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "w").WithArguments("B<object>", "A<object?>").WithLocation(16, 15),
                // (16,9): warning CS8602: Possible dereference of a null reference.
                //         (z ?? w).F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(z ?? w).F").WithLocation(16, 9),
                // (17,10): warning CS8619: Nullability of reference types in value of type 'B<object>' doesn't match target type 'A<object?>'.
                //         (w ?? z).F.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "w").WithArguments("B<object>", "A<object?>").WithLocation(17, 10),
                // (17,9): warning CS8602: Possible dereference of a null reference.
                //         (w ?? z).F.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(w ?? z).F").WithLocation(17, 9));
        }

        // PROTOTYPE(NullableReferenceTypes): Conversions: Conversion
        // (VisitConversion does not report nullability mismatch for the conversion in node.Right.)
        [Fact]
        public void ImplicitConversion_NullCoalescingOperator_03()
        {
            var source =
@"interface IIn<in T>
{
    void F(T x, T y);
}
class C
{
    static void F(IIn<object>? x, IIn<string?> y)
    {
        (x ?? y)/*T:IIn<string?>!*/.F(string.Empty, null);
        (y ?? x)/*T:IIn<string?>!*/.F(string.Empty, null);
    }
    static void G(IIn<object?> z, IIn<string>? w)
    {
        (z ?? w)/*T:IIn<string!>!*/.F(string.Empty, null);
        (w ?? z)/*T:IIn<string!>!*/.F(string.Empty, null);
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (9,10): warning CS8619: Nullability of reference types in value of type 'IIn<object>' doesn't match target type 'IIn<string?>'.
                //         (x ?? y)/*T:IIn<string?>!*/.F(string.Empty, null);
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x").WithArguments("IIn<object>", "IIn<string?>").WithLocation(9, 10),
                // (10,10): hidden CS8607: Expression is probably never null.
                //         (y ?? x)/*T:IIn<string?>!*/.F(string.Empty, null);
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y").WithLocation(10, 10),
                // (10,15): warning CS8619: Nullability of reference types in value of type 'IIn<object>' doesn't match target type 'IIn<string?>'.
                //         (y ?? x)/*T:IIn<string?>!*/.F(string.Empty, null);
                //Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x").WithArguments("IIn<object>", "IIn<string?>").WithLocation(10, 15),
                // (14,10): hidden CS8607: Expression is probably never null.
                //         (z ?? w)/*T:IIn<string!>!*/.F(string.Empty, null);
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "z").WithLocation(14, 10),
                // (14,53): warning CS8600: Cannot convert null to non-nullable reference.
                //         (z ?? w)/*T:IIn<string!>!*/.F(string.Empty, null);
                Diagnostic(ErrorCode.WRN_NullAsNonNullable, "null").WithLocation(14, 53),
                // (15,53): warning CS8600: Cannot convert null to non-nullable reference.
                //         (w ?? z)/*T:IIn<string!>!*/.F(string.Empty, null);
                Diagnostic(ErrorCode.WRN_NullAsNonNullable, "null").WithLocation(15, 53));
        }

        // PROTOTYPE(NullableReferenceTypes): Conversions: Conversion
        // (VisitConversion does not report nullability mismatch for the conversion in node.Right.)
        [Fact]
        public void ImplicitConversion_NullCoalescingOperator_04()
        {
            var source =
@"interface IOut<out T>
{
    T P { get; }
}
class C
{
    static void F(IOut<object>? x, IOut<string?> y)
    {
        (x ?? y)/*T:IOut<object!>!*/.P.ToString();
        (y ?? x)/*T:IOut<object!>!*/.P.ToString();
    }
    static void G(IOut<object?> z, IOut<string>? w)
    {
        (z ?? w)/*T:IOut<object?>!*/.P.ToString();
        (w ?? z)/*T:IOut<object?>!*/.P.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyTypes();
            comp.VerifyDiagnostics(
                // (9,15): warning CS8619: Nullability of reference types in value of type 'IOut<string?>' doesn't match target type 'IOut<object>'.
                //         (x ?? y)/*T:IOut<object!>!*/.P.ToString();
                //Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("IOut<string?>", "IOut<object>").WithLocation(9, 15),
                // (10,10): hidden CS8607: Expression is probably never null.
                //         (y ?? x)/*T:IOut<object!>!*/.P.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "y").WithLocation(10, 10),
                // (10,10): warning CS8619: Nullability of reference types in value of type 'IOut<string?>' doesn't match target type 'IOut<object>'.
                //         (y ?? x)/*T:IOut<object!>!*/.P.ToString();
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("IOut<string?>", "IOut<object>").WithLocation(10, 10),
                // (14,10): hidden CS8607: Expression is probably never null.
                //         (z ?? w)/*T:IOut<object?>!*/.P.ToString();
                Diagnostic(ErrorCode.HDN_ExpressionIsProbablyNeverNull, "z").WithLocation(14, 10),
                // (14,9): warning CS8602: Possible dereference of a null reference.
                //         (z ?? w)/*T:IOut<object?>!*/.P.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(z ?? w)/*T:IOut<object?>!*/.P").WithLocation(14, 9),
                // (15,9): warning CS8602: Possible dereference of a null reference.
                //         (w ?? z)/*T:IOut<object?>!*/.P.ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(w ?? z)/*T:IOut<object?>!*/.P").WithLocation(15, 9));
        }

        [Fact]
        public void IdentityConversion_ArrayInitializer()
        {
            var source =
@"interface I<T> { }
interface IIn<in T> { }
interface IOut<out T> { }
class C
{
    static void F(I<object> x, I<object?> y, I<object>? z, I<object?>? w)
    {
        (new[] { x, y })[0].ToString(); // A1
        (new[] { x, z })[0].ToString(); // A2
        (new[] { x, w })[0].ToString(); // A3
        (new[] { y, z })[0].ToString(); // A4
        (new[] { y, w })[0].ToString(); // A5
        (new[] { w, z })[0].ToString(); // A6
    }
    static void F(IIn<object> x, IIn<object?> y, IIn<object>? z, IIn<object?>? w)
    {
        (new[] { x, y })[0].ToString(); // B1
        (new[] { x, z })[0].ToString(); // B2
        (new[] { x, w })[0].ToString(); // B3
        (new[] { y, z })[0].ToString(); // B4
        (new[] { y, w })[0].ToString(); // B5
        (new[] { w, z })[0].ToString(); // B6
    }
    static void F(IOut<object> x, IOut<object?> y, IOut<object>? z, IOut<object?>? w)
    {
        (new[] { x, y })[0].ToString(); // C1
        (new[] { x, z })[0].ToString(); // C2
        (new[] { x, w })[0].ToString(); // C3
        (new[] { y, z })[0].ToString(); // C4
        (new[] { y, w })[0].ToString(); // C5
        (new[] { w, z })[0].ToString(); // C6
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (8,21): warning CS8619: Nullability of reference types in value of type 'I<object?>' doesn't match target type 'I<object>'.
                //         (new[] { x, y })[0].ToString(); // A1
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("I<object?>", "I<object>").WithLocation(8, 21),
                // (9,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { x, z })[0].ToString(); // A2
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { x, z })[0]").WithLocation(9, 9),
                // (10,21): warning CS8619: Nullability of reference types in value of type 'I<object?>' doesn't match target type 'I<object>'.
                //         (new[] { x, w })[0].ToString(); // A3
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "w").WithArguments("I<object?>", "I<object>").WithLocation(10, 21),
                // (11,21): warning CS8619: Nullability of reference types in value of type 'I<object>' doesn't match target type 'I<object?>'.
                //         (new[] { y, z })[0].ToString(); // A4
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "z").WithArguments("I<object>", "I<object?>").WithLocation(11, 21),
                // (12,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { y, w })[0].ToString(); // A5
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { y, w })[0]").WithLocation(12, 9),
                // (13,21): warning CS8619: Nullability of reference types in value of type 'I<object>' doesn't match target type 'I<object?>'.
                //         (new[] { w, z })[0].ToString(); // A6
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "z").WithArguments("I<object>", "I<object?>").WithLocation(13, 21),
                // (18,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { x, z })[0].ToString(); // B2
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { x, z })[0]").WithLocation(18, 9),
                // (19,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { x, w })[0].ToString(); // B3
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { x, w })[0]").WithLocation(19, 9),
                // (20,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { y, z })[0].ToString(); // B4
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { y, z })[0]").WithLocation(20, 9),
                // (21,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { y, w })[0].ToString(); // B5
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { y, w })[0]").WithLocation(21, 9),
                // (22,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { w, z })[0].ToString(); // B6
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { w, z })[0]").WithLocation(22, 9),
                // (27,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { x, z })[0].ToString(); // C2
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { x, z })[0]").WithLocation(27, 9),
                // (28,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { x, w })[0].ToString(); // C3
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { x, w })[0]").WithLocation(28, 9),
                // (29,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { y, z })[0].ToString(); // C4
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { y, z })[0]").WithLocation(29, 9),
                // (30,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { y, w })[0].ToString(); // C5
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { y, w })[0]").WithLocation(30, 9),
                // (31,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { w, z })[0].ToString(); // C6
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { w, z })[0]").WithLocation(31, 9));
        }

        // PROTOTYPE(NullableReferenceTypes): Update this method to use types from unannotated assemblies
        // rather than `x!`, particularly because `x!` should result in IsNullable=false rather than IsNullable=null.
        // PROTOTYPE(NullableReferenceTypes): Should report the same warnings (or no warnings) for { x, x! } and { x!, x }.
        [Fact(Skip = "TODO")]
        public void IdentityConversion_ArrayInitializer_IsNullableNull()
        {
            var source =
@"#pragma warning disable 0649
class A<T>
{
    internal T F;
}
class B
{
    static void F(object? x, object y)
    {
        (new[] { x, x! })[0].ToString();
        (new[] { x!, x })[0].ToString();
        (new[] { y, y! })[0].ToString();
        (new[] { y!, y })[0].ToString();
    }
    static void F(A<object?> z, A<object> w)
    {
        (new[] { z, z! })[0].F.ToString();
        (new[] { z!, z })[0].F.ToString();
        (new[] { w, w! })[0].F.ToString();
        (new[] { w!, w })[0].F.ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        // PROTOTYPE(NullableReferenceTypes): Update this method to use types from unannotated assemblies
        // rather than `x!`, particularly because `x!` should result in IsNullable=false rather than IsNullable=null.
        // PROTOTYPE(NullableReferenceTypes): Should report the same warnings (or no warnings) for (x, x!) and (x!, x).
        [Fact(Skip = "TODO")]
        public void IdentityConversion_TypeInference_IsNullableNull()
        {
            var source =
@"class A<T>
{
}
class B
{
    static T F1<T>(T x, T y)
    {
        return x;
    }
    static void G1(object? x, object y)
    {
        F1(x, x!).ToString();
        F1(x!, x).ToString();
        F1(y, y!).ToString();
        F1(y!, y).ToString();
    }
    static T F2<T>(A<T> x, A<T> y)
    {
        throw new System.Exception();
    }
    static void G(A<object?> z, A<object> w)
    {
        F2(z, z!).ToString();
        F2(z!, z).ToString();
        F2(w, w!).ToString();
        F2(w!, w).ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics();
        }

        // PROTOTYPE(NullableReferenceTypes): Conversions: Other
        [Fact(Skip = "TODO")]
        public void IdentityConversion_ArrayInitializer_ExplicitType()
        {
            var source =
@"interface I<T> { }
interface IIn<in T> { }
interface IOut<out T> { }
class C
{
    static void F(I<object>? x, I<object?>? y)
    {
        I<object?>?[] a = new[] { x };
        I<object?>[] b = new[] { y };
        I<object>?[] c = new[] { y };
        I<object>[] d = new[] { x };
    }
    static void F(IIn<object>? x, IIn<object?>? y)
    {
        IIn<object?>?[] a = new[] { x };
        IIn<object?>[] b = new[] { y };
        IIn<object>?[] c = new[] { y };
        IIn<object>[] d = new[] { x };
    }
    static void F(IOut<object>? x, IOut<object?>? y)
    {
        IOut<object?>?[] a = new[] { x };
        IOut<object?>[] b = new[] { y };
        IOut<object>?[] c = new[] { y };
        IOut<object>[] d = new[] { x };
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (8,27): warning CS8619: Nullability of reference types in value of type 'I<object>?[]' doesn't match target type 'I<object?>?[]'.
                //         I<object?>?[] a = new[] { x };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { x }").WithArguments("I<object>?[]", "I<object?>?[]").WithLocation(8, 27),
                // (9,26): warning CS8619: Nullability of reference types in value of type 'I<object?>?[]' doesn't match target type 'I<object?>[]'.
                //         I<object?>[] b = new[] { y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { y }").WithArguments("I<object?>?[]", "I<object?>[]").WithLocation(9, 26),
                // (10,26): warning CS8619: Nullability of reference types in value of type 'I<object?>?[]' doesn't match target type 'I<object>?[]'.
                //         I<object>?[] c = new[] { y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { y }").WithArguments("I<object?>?[]", "I<object>?[]").WithLocation(10, 26),
                // (11,25): warning CS8619: Nullability of reference types in value of type 'I<object>?[]' doesn't match target type 'I<object>[]'.
                //         I<object>[] d = new[] { x };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { x }").WithArguments("I<object>?[]", "I<object>[]").WithLocation(11, 25),
                // (15,29): warning CS8619: Nullability of reference types in value of type 'IIn<object>?[]' doesn't match target type 'IIn<object?>?[]'.
                //         IIn<object?>?[] a = new[] { x };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { x }").WithArguments("IIn<object>?[]", "IIn<object?>?[]").WithLocation(15, 29),
                // (16,28): warning CS8619: Nullability of reference types in value of type 'IIn<object?>?[]' doesn't match target type 'IIn<object?>[]'.
                //         IIn<object?>[] b = new[] { y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { y }").WithArguments("IIn<object?>?[]", "IIn<object?>[]").WithLocation(16, 28),
                // (18,27): warning CS8619: Nullability of reference types in value of type 'IIn<object>?[]' doesn't match target type 'IIn<object>[]'.
                //         IIn<object>[] d = new[] { x };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { x }").WithArguments("IIn<object>?[]", "IIn<object>[]").WithLocation(18, 27),
                // (23,29): warning CS8619: Nullability of reference types in value of type 'IOut<object?>?[]' doesn't match target type 'IOut<object?>[]'.
                //         IOut<object?>[] b = new[] { y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { y }").WithArguments("IOut<object?>?[]", "IOut<object?>[]").WithLocation(23, 29),
                // (24,29): warning CS8619: Nullability of reference types in value of type 'IOut<object?>?[]' doesn't match target type 'IOut<object>?[]'.
                //         IOut<object>?[] c = new[] { y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { y }").WithArguments("IOut<object?>?[]", "IOut<object>?[]").WithLocation(24, 29),
                // (25,28): warning CS8619: Nullability of reference types in value of type 'IOut<object>?[]' doesn't match target type 'IOut<object>[]'.
                //         IOut<object>[] d = new[] { x };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "new[] { x }").WithArguments("IOut<object>?[]", "IOut<object>[]").WithLocation(25, 28));
        }

        [Fact]
        public void ImplicitConversion_ArrayInitializer_ExplicitType_01()
        {
            var source =
@"class A<T> { }
class B<T> : A<T> { }
class C
{
    static void F(A<object> x, B<object?> y)
    {
        var z = new A<object>[] { x, y };
        var w = new A<object?>[] { x, y };
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (7,38): warning CS8619: Nullability of reference types in value of type 'B<object?>' doesn't match target type 'A<object>'.
                //         var z = new A<object>[] { x, y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("B<object?>", "A<object>").WithLocation(7, 38),
                // (8,36): warning CS8619: Nullability of reference types in value of type 'A<object>' doesn't match target type 'A<object?>'.
                //         var w = new A<object?>[] { x, y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x").WithArguments("A<object>", "A<object?>").WithLocation(8, 36));
        }

        [Fact]
        public void ImplicitConversion_ArrayInitializer_ExplicitType_02()
        {
            var source =
@"interface IIn<in T> { }
interface IOut<out T> { }
class C
{
    static void F(IIn<object> x, IIn<object?> y)
    {
        var a = new IIn<string?>[] { x };
        var b = new IIn<string>[] { y };
    }
    static void F(IOut<string> x, IOut<string?> y)
    {
        var a = new IOut<object?>[] { x };
        var b = new IOut<object>[] { y };
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (7,38): warning CS8619: Nullability of reference types in value of type 'IIn<object>' doesn't match target type 'IIn<string?>'.
                //         var a = new IIn<string?>[] { x };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "x").WithArguments("IIn<object>", "IIn<string?>").WithLocation(7, 38),
                // (13,38): warning CS8619: Nullability of reference types in value of type 'IOut<string?>' doesn't match target type 'IOut<object>'.
                //         var b = new IOut<object>[] { y };
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "y").WithArguments("IOut<string?>", "IOut<object>").WithLocation(13, 38));
        }

        [Fact]
        public void MultipleConversions_ArrayInitializer()
        {
            var source =
@"class A
{
    public static implicit operator C(A a) => new C();
}
class B : A
{
}
class C
{
    static void F(B x, C? y)
    {
        (new[] { x, y })[0].ToString();
        (new[] { y, x })[0].ToString();
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (12,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { x, y })[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { x, y })[0]").WithLocation(12, 9),
                // (13,9): warning CS8602: Possible dereference of a null reference.
                //         (new[] { y, x })[0].ToString();
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "(new[] { y, x })[0]").WithLocation(13, 9));
        }

        [Fact]
        public void IdentityConversion_ConditionalOperator()
        {
            var source =
@"interface I<T> { }
interface IIn<in T> { }
interface IOut<out T> { }
class C
{
    static void F(bool c, I<object> x, I<object?> y)
    {
        I<object> a;
        a = c ? x : y;
        a = false ? x : y;
        a = true ? x : y;
        I<object?> b;
        b = c ? x : y;
        b = false ? x : y;
        b = true ? x : y;
    }
    static void F(bool c, IIn<object> x, IIn<object?> y)
    {
        IIn<object> a;
        a = c ? x : y;
        a = false ? x : y;
        a = true ? x : y;
        IIn<object?> b;
        b = c ? x : y;
        b = false ? x : y;
        b = true ? x : y;
    }
    static void F(bool c, IOut<object> x, IOut<object?> y)
    {
        IOut<object> a;
        a = c ? x : y;
        a = false ? x : y;
        a = true ? x : y;
        IOut<object?> b;
        b = c ? x : y;
        b = false ? x : y;
        b = true ? x : y;
    }
}";
            var comp = CreateStandardCompilation(source, parseOptions: TestOptions.Regular8);
            comp.VerifyDiagnostics(
                // (9,13): warning CS8626: No best nullability for operands of conditional expression 'I<object>' and 'I<object?>'.
                //         a = c ? x : y;
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "c ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(9, 13),
                // (10,13): warning CS8626: No best nullability for operands of conditional expression 'I<object>' and 'I<object?>'.
                //         a = false ? x : y;
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "false ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(10, 13),
                // (11,13): warning CS8626: No best nullability for operands of conditional expression 'I<object>' and 'I<object?>'.
                //         a = true ? x : y;
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "true ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(11, 13),
                // (13,13): warning CS8626: No best nullability for operands of conditional expression 'I<object>' and 'I<object?>'.
                //         b = c ? x : y;
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "c ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(13, 13),
                // (13,13): warning CS8619: Nullability of reference types in value of type 'I<object>' doesn't match target type 'I<object?>'.
                //         b = c ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "c ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(13, 13),
                // (14,13): warning CS8626: No best nullability for operands of conditional expression 'I<object>' and 'I<object?>'.
                //         b = false ? x : y;
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "false ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(14, 13),
                // (14,13): warning CS8619: Nullability of reference types in value of type 'I<object>' doesn't match target type 'I<object?>'.
                //         b = false ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "false ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(14, 13),
                // (15,13): warning CS8626: No best nullability for operands of conditional expression 'I<object>' and 'I<object?>'.
                //         b = true ? x : y;
                Diagnostic(ErrorCode.WRN_NoBestNullabilityConditionalExpression, "true ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(15, 13),
                // (15,13): warning CS8619: Nullability of reference types in value of type 'I<object>' doesn't match target type 'I<object?>'.
                //         b = true ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "true ? x : y").WithArguments("I<object>", "I<object?>").WithLocation(15, 13),
                // (24,13): warning CS8619: Nullability of reference types in value of type 'IIn<object>' doesn't match target type 'IIn<object?>'.
                //         b = c ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "c ? x : y").WithArguments("IIn<object>", "IIn<object?>").WithLocation(24, 13),
                // (25,13): warning CS8619: Nullability of reference types in value of type 'IIn<object>' doesn't match target type 'IIn<object?>'.
                //         b = false ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "false ? x : y").WithArguments("IIn<object>", "IIn<object?>").WithLocation(25, 13),
                // (26,13): warning CS8619: Nullability of reference types in value of type 'IIn<object>' doesn't match target type 'IIn<object?>'.
                //         b = true ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "true ? x : y").WithArguments("IIn<object>", "IIn<object?>").WithLocation(26, 13),
                // (31,13): warning CS8619: Nullability of reference types in value of type 'IOut<object?>' doesn't match target type 'IOut<object>'.
                //         a = c ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "c ? x : y").WithArguments("IOut<object?>", "IOut<object>").WithLocation(31, 13),
                // (32,13): warning CS8619: Nullability of reference types in value of type 'IOut<object?>' doesn't match target type 'IOut<object>'.
                //         a = false ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "false ? x : y").WithArguments("IOut<object?>", "IOut<object>").WithLocation(32, 13),
                // (33,13): warning CS8619: Nullability of reference types in value of type 'IOut<object?>' doesn't match target type 'IOut<object>'.
                //         a = true ? x : y;
                Diagnostic(ErrorCode.WRN_NullabilityMismatchInAssignment, "true ? x : y").WithArguments("IOut<object?>", "IOut<object>").WithLocation(33, 13));
        }
    }
}
