﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class AttributeTests_NullableContext : CSharpTestBase
    {
        [Fact]
        public void EmptyProject()
        {
            var source = @"";
            var comp = CreateCompilation(source);
            var expected =
@"";
            AssertNullableAttributes(comp, expected);
        }

        [Fact]
        public void ExplicitAttribute_FromSource()
        {
            var source =
@"#nullable enable
public class Program
{
    public object F(object arg) => arg;
}";
            var comp = CreateCompilation(new[] { NullableContextAttributeDefinition, source });
            var expected =
@"Program
    [NullableContext(1)] System.Object! F(System.Object! arg)
        System.Object! arg
";
            AssertNullableAttributes(comp, expected);
        }

        [Fact]
        public void ExplicitAttribute_FromMetadata()
        {
            var comp = CreateCompilation(NullableContextAttributeDefinition);
            comp.VerifyDiagnostics();
            var ref0 = comp.EmitToImageReference();

            var source =
@"#nullable enable
public class Program
{
    public object F(object arg) => arg;
}";
            comp = CreateCompilation(source, references: new[] { ref0 });
            var expected =
@"Program
    [NullableContext(1)] System.Object! F(System.Object! arg)
        System.Object! arg
";
            AssertNullableAttributes(comp, expected);
        }

        [Fact]
        public void ExplicitAttribute_MissingSingleByteConstructor()
        {
            var source1 =
@"namespace System.Runtime.CompilerServices
{
    public sealed class NullableContextAttribute : Attribute
    {
    }
}";
            var source2 =
@"public class Program
{
    public object F(object arg) => arg;
}";

            // C#7
            var comp = CreateCompilation(new[] { source1, source2 }, parseOptions: TestOptions.Regular7);
            comp.VerifyEmitDiagnostics();

            // C#8, nullable disabled
            comp = CreateCompilation(new[] { source1, source2 }, options: WithNonNullTypesFalse());
            comp.VerifyEmitDiagnostics();

            // C#8, nullable enabled
            comp = CreateCompilation(new[] { source1, source2 }, options: WithNonNullTypesTrue());
            comp.VerifyEmitDiagnostics(
                // (3,19): error CS0656: Missing compiler required member 'System.Runtime.CompilerServices.NullableContextAttribute..ctor'
                //     public object F(object arg) => arg;
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "F").WithArguments("System.Runtime.CompilerServices.NullableContextAttribute", ".ctor").WithLocation(3, 19));
        }

        [Fact]
        public void ExplicitAttribute_ReferencedInSource()
        {
            var sourceAttribute =
@"namespace System.Runtime.CompilerServices
{
    internal class NullableContextAttribute : System.Attribute
    {
        internal NullableContextAttribute(byte b) { }
    }
}";
            var source =
@"#pragma warning disable 169
using System.Runtime.CompilerServices;
[assembly: NullableContext(0)]
[module: NullableContext(0)]
[NullableContext(0)]
class Program
{
    [NullableContext(0)]object F;
    [NullableContext(0)]static object M1() => throw null;
    [return: NullableContext(0)]static object M2() => throw null;
    static void M3([NullableContext(0)]object arg) { }
}";

            // C#7
            var comp = CreateCompilation(new[] { sourceAttribute, source }, parseOptions: TestOptions.Regular7);
            verifyDiagnostics(comp);

            // C#8
            comp = CreateCompilation(new[] { sourceAttribute, source });
            verifyDiagnostics(comp);

            static void verifyDiagnostics(CSharpCompilation comp)
            {
                comp.VerifyDiagnostics(
                    // (4,10): error CS8335: Do not use 'System.Runtime.CompilerServices.NullableContextAttribute'. This is reserved for compiler usage.
                    // [module: NullableContext(0)]
                    Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "NullableContext(0)").WithArguments("System.Runtime.CompilerServices.NullableContextAttribute").WithLocation(4, 10),
                    // (5,2): error CS8335: Do not use 'System.Runtime.CompilerServices.NullableContextAttribute'. This is reserved for compiler usage.
                    // [NullableContext(0)]
                    Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "NullableContext(0)").WithArguments("System.Runtime.CompilerServices.NullableContextAttribute").WithLocation(5, 2),
                    // (9,6): error CS8335: Do not use 'System.Runtime.CompilerServices.NullableContextAttribute'. This is reserved for compiler usage.
                    //     [NullableContext(0)]static object M1() => throw null;
                    Diagnostic(ErrorCode.ERR_ExplicitReservedAttr, "NullableContext(0)").WithArguments("System.Runtime.CompilerServices.NullableContextAttribute").WithLocation(9, 6));
            }
        }

        [Fact]
        public void ExplicitAttribute_WithNullableContext()
        {
            var sourceAttribute =
@"#nullable enable
namespace System.Runtime.CompilerServices
{
    public sealed class NullableContextAttribute : Attribute
    {
        private object _f1;
        private object _f2;
        private object _f3;
        public NullableContextAttribute(byte b)
        {
        }
    }
}";
            var comp = CreateCompilation(sourceAttribute);
            var ref0 = comp.EmitToImageReference();
            var expected =
@"[NullableContext(1)] [Nullable(0)] System.Runtime.CompilerServices.NullableContextAttribute
    NullableContextAttribute(System.Byte b)
        System.Byte b
";
            AssertNullableAttributes(comp, expected);

            var source =
@"#nullable enable
public class Program
{
    private object _f1;
    private object _f2;
    private object _f3;
}";
            comp = CreateCompilation(source, references: new[] { ref0 });
            expected =
@"[NullableContext(1)] [Nullable(0)] Program
    Program()
";
            AssertNullableAttributes(comp, expected);
        }

        [Fact]
        public void AttributeField()
        {
            var source =
@"#nullable enable
using System;
using System.Linq;
public class A
{
    private object _f1;
    private object _f2;
    private object _f3;
}
public class B
{
    private object? _f1;
    private object? _f2;
    private object? _f3;
}
class Program
{
    static void Main()
    {
        Console.WriteLine(GetAttributeValue(typeof(A)));
        Console.WriteLine(GetAttributeValue(typeof(B)));
    }
    static byte GetAttributeValue(Type type)
    {
        var attribute = type.GetCustomAttributes(false).Single(a => a.GetType().Name == ""NullableContextAttribute"");
        var field = attribute.GetType().GetField(""Flag"");
        return (byte)field.GetValue(attribute);
    }
}";
            var expectedOutput =
@"1
2";
            var expectedAttributes =
@"[NullableContext(1)] [Nullable(0)] A
    A()
[NullableContext(2)] [Nullable(0)] B
    B()
";
            var comp = CreateCompilation(source, parseOptions: TestOptions.Regular8, options: TestOptions.DebugExe);
            CompileAndVerify(comp, expectedOutput: expectedOutput, symbolValidator: module => AssertNullableAttributes(module, expectedAttributes));
        }

        [Fact]
        public void MostCommonNullableValue()
        {
            Assert.Equal(null, getMostCommonValue());
            Assert.Equal(null, getMostCommonValue((byte?)null));
            Assert.Equal(null, getMostCommonValue(null, null));
            Assert.Equal((byte)0, getMostCommonValue(0));
            Assert.Equal((byte)1, getMostCommonValue(1));
            Assert.Equal((byte)2, getMostCommonValue(2));
#if !DEBUG
            Assert.Throws<InvalidOperationException>(() => getMostCommonValue(3));
#endif
            Assert.Equal((byte)0, getMostCommonValue(0, 0));
            Assert.Equal((byte)0, getMostCommonValue(0, 1));
            Assert.Equal((byte)0, getMostCommonValue(1, 0));
            Assert.Equal((byte)1, getMostCommonValue(1, 1));
            Assert.Equal((byte)1, getMostCommonValue(1, 2));
            Assert.Equal((byte)1, getMostCommonValue(2, 1));
            Assert.Equal((byte)2, getMostCommonValue(2, 2));
#if !DEBUG
            Assert.Throws<InvalidOperationException>(() => getMostCommonValue(2, 3));
#endif
            Assert.Equal((byte)0, getMostCommonValue(0, null));
            Assert.Equal((byte)0, getMostCommonValue(null, 0));
            Assert.Equal((byte)1, getMostCommonValue(1, null));
            Assert.Equal((byte)1, getMostCommonValue(null, 1));
            Assert.Equal((byte)0, getMostCommonValue(0, 1, 2, null));
            Assert.Equal((byte)0, getMostCommonValue(null, 2, 1, 0));
            Assert.Equal((byte)1, getMostCommonValue(1, 2, null));
            Assert.Equal((byte)1, getMostCommonValue(null, 2, 1));
            Assert.Equal((byte)2, getMostCommonValue(null, 2, null));
            Assert.Equal((byte)0, getMostCommonValue(0, 1, 0));
            Assert.Equal((byte)0, getMostCommonValue(1, 0, 0));
            Assert.Equal((byte)1, getMostCommonValue(1, 0, 1));
            Assert.Equal((byte)1, getMostCommonValue(1, 2, 1));
            Assert.Equal((byte)2, getMostCommonValue(2, 2, 1));

            static byte? getMostCommonValue(params byte?[] values)
            {
                var builder = new MostCommonNullableValueBuilder();
                foreach (var value in values)
                {
                    builder.AddValue(value);
                }
                return builder.MostCommonValue;
            }
        }

        [Fact]
        public void GetCommonNullableValue()
        {
            Assert.Equal(null, getCommonValue());
            Assert.Equal((byte)0, getCommonValue(0));
            Assert.Equal((byte)1, getCommonValue(1));
            Assert.Equal((byte)2, getCommonValue(2));
            Assert.Equal((byte)3, getCommonValue(3));
            Assert.Equal((byte)0, getCommonValue(0, 0));
            Assert.Equal(null, getCommonValue(0, 1));
            Assert.Equal(null, getCommonValue(1, 0));
            Assert.Equal((byte)1, getCommonValue(1, 1));
            Assert.Equal(null, getCommonValue(1, 2));
            Assert.Equal((byte)2, getCommonValue(2, 2));
            Assert.Equal(null, getCommonValue(0, 1, 0));
            Assert.Equal(null, getCommonValue(1, 0, 1));
            Assert.Equal(null, getCommonValue(2, 2, 1));
            Assert.Equal((byte)3, getCommonValue(3, 3, 3));

            static byte? getCommonValue(params byte[] values)
            {
                var builder = ArrayBuilder<byte>.GetInstance();
                builder.AddRange(values);
                var result = MostCommonNullableValueBuilder.GetCommonValue(builder);
                builder.Free();
                return result;
            }
        }

        private void AssertNullableAttributes(CSharpCompilation comp, string expected)
        {
            CompileAndVerify(comp, symbolValidator: module => AssertNullableAttributes(module, expected));
        }

        private static void AssertNullableAttributes(ModuleSymbol module, string expected)
        {
            var actual = NullableAttributesVisitor.GetString((PEModuleSymbol)module);
            AssertEx.AssertEqualToleratingWhitespaceDifferences(expected, actual);
        }
    }
}
