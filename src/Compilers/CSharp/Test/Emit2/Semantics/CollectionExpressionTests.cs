﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Retargeting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class CollectionExpressionTests : CSharpTestBase
    {
        private static string IncludeExpectedOutput(string expectedOutput) => ExecutionConditionUtil.IsMonoOrCoreClr ? expectedOutput : null;

        private const string s_collectionExtensions = """
            using System;
            using System.Collections;
            using System.Linq;
            using System.Text;
            static partial class CollectionExtensions
            {
                private static void Append(StringBuilder builder, bool isFirst, object value)
                {
                    if (!isFirst) builder.Append(", ");
                    if (value is IEnumerable e && value is not string)
                    {
                        AppendCollection(builder, e);
                    }
                    else
                    {
                        builder.Append(value is null ? "null" : value.ToString());
                    }
                }
                private static void AppendCollection(StringBuilder builder, IEnumerable e)
                {
                    builder.Append("[");
                    bool isFirst = true;
                    foreach (var i in e)
                    {
                        Append(builder, isFirst, i);
                        isFirst = false;
                    }
                    builder.Append("]");
                }
                internal static void Report(this object o, bool includeType = false)
                {
                    var builder = new StringBuilder();
                    Append(builder, isFirst: true, o);
                    if (includeType) Console.Write("({0}) ", GetTypeName(o.GetType()));
                    Console.Write(builder.ToString());
                    Console.Write(", ");
                }
                internal static string GetTypeName(this Type type)
                {
                    if (type.IsArray)
                    {
                        return GetTypeName(type.GetElementType()) + "[]";
                    }
                    string typeName = type.Name;
                    int index = typeName.LastIndexOf('`');
                    if (index >= 0)
                    {
                        typeName = typeName.Substring(0, index);
                    }
                    if (!type.IsGenericParameter)
                    {
                        if (type.DeclaringType is { } declaringType)
                        {
                            typeName = Concat(GetTypeName(declaringType), typeName);
                        }
                        else
                        {
                            typeName = Concat(type.Namespace, typeName);
                        }
                    }
                    if (!type.IsGenericType)
                    {
                        return typeName;
                    }
                    var typeArgs = type.GetGenericArguments();
                    return $"{typeName}<{string.Join(", ", typeArgs.Select(GetTypeName))}>";
                }
                private static string Concat(string container, string name)
                {
                    return string.IsNullOrEmpty(container) ? name : container + "." + name;
                }
            }
            """;
        private const string s_collectionExtensionsWithSpan = s_collectionExtensions +
            """
            static partial class CollectionExtensions
            {
                internal static void Report<T>(this in Span<T> s)
                {
                    Report((ReadOnlySpan<T>)s);
                }
                internal static void Report<T>(this in ReadOnlySpan<T> s)
                {
                    var builder = new StringBuilder();
                    builder.Append("[");
                    bool isFirst = true;
                    foreach (var i in s)
                    {
                        Append(builder, isFirst, i);
                        isFirst = false;
                    }
                    builder.Append("]");
                    Console.Write(builder.ToString());
                    Console.Write(", ");
                }
            }
            """;

        [Theory]
        [InlineData(LanguageVersion.CSharp11)]
        [InlineData(LanguageVersion.Preview)]
        public void LanguageVersionDiagnostics(LanguageVersion languageVersion)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        object[] x = [];
                        List<object> y = [1, 2, 3];
                        List<object[]> z = [[]];
                    }
                }
                """;
            var comp = CreateCompilation(source, parseOptions: TestOptions.Regular.WithLanguageVersion(languageVersion));
            if (languageVersion == LanguageVersion.CSharp11)
            {
                comp.VerifyEmitDiagnostics(
                    // (6,22): error CS9058: Feature 'collection expressions' is not available in C# 11.0. Please use language version 12.0 or greater.
                    //         object[] x = [];
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion11, "[").WithArguments("collection expressions", "12.0").WithLocation(6, 22),
                    // (7,26): error CS9058: Feature 'collection expressions' is not available in C# 11.0. Please use language version 12.0 or greater.
                    //         List<object> y = [1, 2, 3];
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion11, "[").WithArguments("collection expressions", "12.0").WithLocation(7, 26),
                    // (8,28): error CS9058: Feature 'collection expressions' is not available in C# 11.0. Please use language version 12.0 or greater.
                    //         List<object[]> z = [[]];
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion11, "[").WithArguments("collection expressions", "12.0").WithLocation(8, 28),
                    // (8,29): error CS9058: Feature 'collection expressions' is not available in C# 11.0. Please use language version 12.0 or greater.
                    //         List<object[]> z = [[]];
                    Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion11, "[").WithArguments("collection expressions", "12.0").WithLocation(8, 29));
            }
            else
            {
                comp.VerifyEmitDiagnostics();
            }
        }

        [Fact]
        public void NaturalType_01()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object x = [];
                        dynamic y = [];
                        var z = [];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,20): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object x = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("object").WithLocation(5, 20),
                // (6,21): error CS9174: Cannot initialize type 'dynamic' with a collection expression because the type is not constructible.
                //         dynamic y = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("dynamic").WithLocation(6, 21),
                // (7,17): error CS9176: There is no target type for the collection expression.
                //         var z = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 17));

            var tree = comp.SyntaxTrees[0];
            var model = comp.GetSemanticModel(tree);
            var collections = tree.GetRoot().DescendantNodes().OfType<CollectionExpressionSyntax>().ToArray();
            Assert.Equal(3, collections.Length);
            VerifyTypes(model, collections[0], expectedType: null, expectedConvertedType: "System.Object", ConversionKind.NoConversion);
            VerifyTypes(model, collections[1], expectedType: null, expectedConvertedType: "dynamic", ConversionKind.NoConversion);
            VerifyTypes(model, collections[2], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
        }

        [Fact]
        public void NaturalType_02()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object x = [1];
                        dynamic y = [2];
                        var z = [3];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,20): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object x = [1];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1]").WithArguments("object").WithLocation(5, 20),
                // (6,21): error CS9174: Cannot initialize type 'dynamic' with a collection expression because the type is not constructible.
                //         dynamic y = [2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[2]").WithArguments("dynamic").WithLocation(6, 21),
                // (7,17): error CS9176: There is no target type for the collection expression.
                //         var z = [3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[3]").WithLocation(7, 17));

            var tree = comp.SyntaxTrees[0];
            var model = comp.GetSemanticModel(tree);
            var collections = tree.GetRoot().DescendantNodes().OfType<CollectionExpressionSyntax>().ToArray();
            Assert.Equal(3, collections.Length);
            VerifyTypes(model, collections[0], expectedType: null, expectedConvertedType: "System.Object", ConversionKind.NoConversion);
            VerifyTypes(model, collections[1], expectedType: null, expectedConvertedType: "dynamic", ConversionKind.NoConversion);
            VerifyTypes(model, collections[2], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
        }

        [Fact]
        public void NaturalType_03()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object x = [1, ""];
                        dynamic y = [2, ""];
                        var z = [3, ""];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,20): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object x = [1, ""];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, @"[1, """"]").WithArguments("object").WithLocation(5, 20),
                // (6,21): error CS9174: Cannot initialize type 'dynamic' with a collection expression because the type is not constructible.
                //         dynamic y = [2, ""];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, @"[2, """"]").WithArguments("dynamic").WithLocation(6, 21),
                // (7,17): error CS9176: There is no target type for the collection expression.
                //         var z = [3, ""];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, @"[3, """"]").WithLocation(7, 17));
        }

        [Fact]
        public void NaturalType_04()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object x = [null];
                        dynamic y = [null];
                        var z = [null];
                        int?[] w = [null];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,20): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object x = [null];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[null]").WithArguments("object").WithLocation(5, 20),
                // (6,21): error CS9174: Cannot initialize type 'dynamic' with a collection expression because the type is not constructible.
                //         dynamic y = [null];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[null]").WithArguments("dynamic").WithLocation(6, 21),
                // (7,17): error CS9176: There is no target type for the collection expression.
                //         var z = [null];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[null]").WithLocation(7, 17));
        }

        [Fact]
        public void NaturalType_05()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        var x = [1, 2, null];
                        object y = [1, 2, null];
                        dynamic z = [1, 2, null];
                        int?[] w = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,17): error CS9176: There is no target type for the collection expression.
                //         var x = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[1, 2, null]").WithLocation(5, 17),
                // (6,20): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2, null]").WithArguments("object").WithLocation(6, 20),
                // (7,21): error CS9174: Cannot initialize type 'dynamic' with a collection expression because the type is not constructible.
                //         dynamic z = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2, null]").WithArguments("dynamic").WithLocation(7, 21));
        }

        [Fact]
        public void NaturalType_06()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object[] x = [[]];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,23): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object[] x = [[]];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("object").WithLocation(5, 23));
        }

        [Fact]
        public void NaturalType_07()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object[] y = [[2]];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,23): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object[] y = [[2]];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[2]").WithArguments("object").WithLocation(5, 23));
        }

        [Fact]
        public void NaturalType_08()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        var z = [[3]];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,17): error CS9176: There is no target type for the collection expression.
                //         var z = [[3]];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[[3]]").WithLocation(5, 17));
        }

        [Fact]
        public void NaturalType_09()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        F([]);
                        F([[]]);
                    }
                    static T F<T>(T t) => t;
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,9): error CS0411: The type arguments for method 'Program.F<T>(T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T)").WithLocation(5, 9),
                // (6,9): error CS0411: The type arguments for method 'Program.F<T>(T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([[]]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T)").WithLocation(6, 9));
        }

        [Fact]
        public void NaturalType_10()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        F([1, 2]).Report();
                        F([[3, 4]]).Report();
                    }
                    static T F<T>(T t) => t;
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics(
                // 0.cs(5,9): error CS0411: The type arguments for method 'Program.F<T>(T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([1, 2]).Report();
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T)").WithLocation(5, 9),
                // 0.cs(6,9): error CS0411: The type arguments for method 'Program.F<T>(T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([[3, 4]]).Report();
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T)").WithLocation(6, 9));
        }

        [Fact]
        public void NaturalType_11()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        var d1 = () => [];
                        Func<int[]> d2 = () => [];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,18): error CS8917: The delegate type could not be inferred.
                //         var d1 = () => [];
                Diagnostic(ErrorCode.ERR_CannotInferDelegateType, "() => []").WithLocation(6, 18));
        }

        [Fact]
        public void InterfaceType_01()
        {
            string source = """
                using System.Collections;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable a = [1];
                        ICollection b = [2];
                        IList c = [3];
                        a.Report(includeType: true);
                        b.Report(includeType: true);
                        c.Report(includeType: true);
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics(
                // 0.cs(6,25): error CS9174: Cannot initialize type 'IEnumerable' with a collection expression because the type is not constructible.
                //         IEnumerable a = [1];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1]").WithArguments("System.Collections.IEnumerable").WithLocation(6, 25),
                // 0.cs(7,25): error CS9174: Cannot initialize type 'ICollection' with a collection expression because the type is not constructible.
                //         ICollection b = [2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[2]").WithArguments("System.Collections.ICollection").WithLocation(7, 25),
                // 0.cs(8,19): error CS9174: Cannot initialize type 'IList' with a collection expression because the type is not constructible.
                //         IList c = [3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[3]").WithArguments("System.Collections.IList").WithLocation(8, 19));
        }

        [Fact]
        public void InterfaceType_02()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> a = [];
                        ICollection<int> b = [];
                        IList<int> c = [];
                        IReadOnlyCollection<int> d = [];
                        IReadOnlyList<int> e = [];
                        a.Report(includeType: true);
                        b.Report(includeType: true);
                        c.Report(includeType: true);
                        d.Report(includeType: true);
                        e.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(System.Int32[]) [], (System.Collections.Generic.List<System.Int32>) [], (System.Collections.Generic.List<System.Int32>) [], (System.Int32[]) [], (System.Int32[]) [], ");
        }

        [Fact]
        public void InterfaceType_03()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> a = [1];
                        ICollection<int> b = [2];
                        IList<int> c = [3];
                        IReadOnlyCollection<int> d = [4];
                        IReadOnlyList<int> e = [5];
                        a.Report(includeType: true);
                        b.Report(includeType: true);
                        c.Report(includeType: true);
                        d.Report(includeType: true);
                        e.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(<>z__ReadOnlyArray<System.Int32>) [1], (System.Collections.Generic.List<System.Int32>) [2], (System.Collections.Generic.List<System.Int32>) [3], (<>z__ReadOnlyArray<System.Int32>) [4], (<>z__ReadOnlyArray<System.Int32>) [5], ");
        }

        [Fact]
        public void NaturalType_23()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        var x = [null, 1];
                        object y = [null, 2];
                        int?[] z = [null, 3];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,17): error CS9176: There is no target type for the collection expression.
                //         var x = [null, 1];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[null, 1]").WithLocation(5, 17),
                // (6,20): error CS9174: Cannot initialize type 'object' with a collection expression because the type is not constructible.
                //         object y = [null, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[null, 2]").WithArguments("object").WithLocation(6, 20));
        }

        [Fact]
        public void NaturalType_24()
        {
            string source = """
                class Program
                {
                    static void F1(int i)
                    {
                        (string, int)[] x1 = [(null, default)];
                        string[] y1 = [i switch {  _ => default }];
                        string[] z1 = [i == 0 ? null : default];
                    }
                    static void F2(int i)
                    /*<bind>*/
                    {
                        var x2 = [(null, default)];
                        var y2 = [i switch { _ => default }];
                        var z2 = [i == 0 ? null : default];
                    }
                    /*</bind>*/
                }
                """;

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (12,18): error CS9176: There is no target type for the collection expression.
                //         var x2 = [(null, default)];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[(null, default)]").WithLocation(12, 18),
                // (13,18): error CS9176: There is no target type for the collection expression.
                //         var y2 = [i switch { _ => default }];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[i switch { _ => default }]").WithLocation(13, 18),
                // (13,21): error CS8506: No best type was found for the switch expression.
                //         var y2 = [i switch { _ => default }];
                Diagnostic(ErrorCode.ERR_SwitchExpressionNoBestType, "switch").WithLocation(13, 21),
                // (14,18): error CS9176: There is no target type for the collection expression.
                //         var z2 = [i == 0 ? null : default];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[i == 0 ? null : default]").WithLocation(14, 18),
                // (14,19): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between '<null>' and 'default'
                //         var z2 = [i == 0 ? null : default];
                Diagnostic(ErrorCode.ERR_InvalidQM, "i == 0 ? null : default").WithArguments("<null>", "default").WithLocation(14, 19));

            VerifyOperationTreeForTest<BlockSyntax>(comp,
                """
                IBlockOperation (3 statements, 3 locals) (OperationKind.Block, Type: null, IsInvalid) (Syntax: '{ ... }')
                  Locals: Local_1: ? x2
                    Local_2: ? y2
                    Local_3: ? z2
                  IVariableDeclarationGroupOperation (1 declarations) (OperationKind.VariableDeclarationGroup, Type: null, IsInvalid) (Syntax: 'var x2 = [( ...  default)];')
                    IVariableDeclarationOperation (1 declarators) (OperationKind.VariableDeclaration, Type: null, IsInvalid) (Syntax: 'var x2 = [( ... , default)]')
                      Declarators:
                          IVariableDeclaratorOperation (Symbol: ? x2) (OperationKind.VariableDeclarator, Type: null, IsInvalid) (Syntax: 'x2 = [(null, default)]')
                            Initializer:
                              IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null, IsInvalid) (Syntax: '= [(null, default)]')
                                IOperation:  (OperationKind.None, Type: ?, IsInvalid) (Syntax: '[(null, default)]')
                                  Children(1):
                                      ITupleOperation (OperationKind.Tuple, Type: null, IsInvalid) (Syntax: '(null, default)')
                                        NaturalType: null
                                        Elements(2):
                                            ILiteralOperation (OperationKind.Literal, Type: null, Constant: null, IsInvalid) (Syntax: 'null')
                                            IDefaultValueOperation (OperationKind.DefaultValue, Type: ?, IsInvalid) (Syntax: 'default')
                      Initializer:
                        null
                  IVariableDeclarationGroupOperation (1 declarations) (OperationKind.VariableDeclarationGroup, Type: null, IsInvalid) (Syntax: 'var y2 = [i ... default }];')
                    IVariableDeclarationOperation (1 declarators) (OperationKind.VariableDeclaration, Type: null, IsInvalid) (Syntax: 'var y2 = [i ...  default }]')
                      Declarators:
                          IVariableDeclaratorOperation (Symbol: ? y2) (OperationKind.VariableDeclarator, Type: null, IsInvalid) (Syntax: 'y2 = [i swi ...  default }]')
                            Initializer:
                              IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null, IsInvalid) (Syntax: '= [i switch ...  default }]')
                                IOperation:  (OperationKind.None, Type: ?, IsInvalid) (Syntax: '[i switch { ...  default }]')
                                  Children(1):
                                      ISwitchExpressionOperation (1 arms, IsExhaustive: True) (OperationKind.SwitchExpression, Type: ?, IsInvalid) (Syntax: 'i switch {  ... > default }')
                                        Value:
                                          IParameterReferenceOperation: i (OperationKind.ParameterReference, Type: System.Int32, IsInvalid) (Syntax: 'i')
                                        Arms(1):
                                            ISwitchExpressionArmOperation (0 locals) (OperationKind.SwitchExpressionArm, Type: null, IsInvalid) (Syntax: '_ => default')
                                              Pattern:
                                                IDiscardPatternOperation (OperationKind.DiscardPattern, Type: null, IsInvalid) (Syntax: '_') (InputType: System.Int32, NarrowedType: System.Int32)
                                              Value:
                                                IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: ?, IsInvalid, IsImplicit) (Syntax: 'default')
                                                  Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                                                  Operand:
                                                    IDefaultValueOperation (OperationKind.DefaultValue, Type: ?, IsInvalid) (Syntax: 'default')
                      Initializer:
                        null
                  IVariableDeclarationGroupOperation (1 declarations) (OperationKind.VariableDeclarationGroup, Type: null, IsInvalid) (Syntax: 'var z2 = [i ... : default];')
                    IVariableDeclarationOperation (1 declarators) (OperationKind.VariableDeclaration, Type: null, IsInvalid) (Syntax: 'var z2 = [i ...  : default]')
                      Declarators:
                          IVariableDeclaratorOperation (Symbol: ? z2) (OperationKind.VariableDeclarator, Type: null, IsInvalid) (Syntax: 'z2 = [i ==  ...  : default]')
                            Initializer:
                              IVariableInitializerOperation (OperationKind.VariableInitializer, Type: null, IsInvalid) (Syntax: '= [i == 0 ? ...  : default]')
                                IOperation:  (OperationKind.None, Type: ?, IsInvalid) (Syntax: '[i == 0 ? n ...  : default]')
                                  Children(1):
                                      IConditionalOperation (OperationKind.Conditional, Type: ?, IsInvalid) (Syntax: 'i == 0 ? null : default')
                                        Condition:
                                          IBinaryOperation (BinaryOperatorKind.Equals) (OperationKind.Binary, Type: System.Boolean, IsInvalid) (Syntax: 'i == 0')
                                            Left:
                                              IParameterReferenceOperation: i (OperationKind.ParameterReference, Type: System.Int32, IsInvalid) (Syntax: 'i')
                                            Right:
                                              ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 0, IsInvalid) (Syntax: '0')
                                        WhenTrue:
                                          IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: ?, Constant: null, IsInvalid, IsImplicit) (Syntax: 'null')
                                            Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: True, IsUserDefined: False) (MethodSymbol: null)
                                            Operand:
                                              ILiteralOperation (OperationKind.Literal, Type: null, Constant: null, IsInvalid) (Syntax: 'null')
                                        WhenFalse:
                                          IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: ?, IsInvalid, IsImplicit) (Syntax: 'default')
                                            Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                                            Operand:
                                              IDefaultValueOperation (OperationKind.DefaultValue, Type: ?, IsInvalid) (Syntax: 'default')
                      Initializer:
                        null
                """);
        }

        [Fact]
        public void TargetType_01()
        {
            string source = """
                class Program
                {
                    static void F(bool b, object o)
                    {
                        int[] a1 = b ? [1] : [];
                        int[] a2 = b? [] : [2];
                        object[] a3 = b ? [3] : [o];
                        object[] a4 = b ? [o] : [4];
                        int?[] a5 = b ? [5] : [null];
                        int?[] a6 = b ? [null] : [6];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();
        }

        [Fact]
        public void TargetType_02()
        {
            string source = """
                using System;
                class Program
                {
                    static void F(bool b, object o)
                    {
                        Func<int[]> f1 = () => { if (b) return [1]; return []; };
                        Func<int[]> f2 = () => { if (b) return []; return [2]; };
                        Func<object[]> f3 = () => { if (b) return [3]; return [o]; };
                        Func<object[]> f4 = () => { if (b) return [o]; return [4]; };
                        Func<int?[]> f5 = () => { if (b) return [5]; return [null]; };
                        Func<int?[]> f6 = () => { if (b) return [null]; return [6]; };
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();
        }

        // Overload resolution should choose array over interface.
        [Fact]
        public void OverloadResolution_01()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static IEnumerable<int> F1(IEnumerable<int> arg) => arg;
                    static int[] F1(int[] arg) => arg;
                    static int[] F2(int[] arg) => arg;
                    static IEnumerable<int> F2(IEnumerable<int> arg) => arg;
                    static void Main()
                    {
                        var x = F1([]);
                        var y = F2([1, 2]);
                        x.Report(includeType: true);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [], (System.Int32[]) [1, 2], ");
        }

        // Overload resolution should choose collection initializer type over interface.
        [Fact]
        public void OverloadResolution_02()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static IEnumerable<int> F1(IEnumerable<int> arg) => arg;
                    static List<int> F1(List<int> arg) => arg;
                    static List<int> F2(List<int> arg) => arg;
                    static IEnumerable<int> F2(IEnumerable<int> arg) => arg;
                    static void Main()
                    {
                        var x = F1([]);
                        var y = F2([1, 2]);
                        x.Report(includeType: true);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Collections.Generic.List<System.Int32>) [], (System.Collections.Generic.List<System.Int32>) [1, 2], ");
        }

        [Fact]
        public void OverloadResolution_03()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static List<int> F(List<int> arg) => arg;
                    static int[] F(int[] arg) => arg;
                    static void Main()
                    {
                        var x = F([]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F(List<int>)' and 'Program.F(int[])'
                //         var x = F([]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F").WithArguments("Program.F(System.Collections.Generic.List<int>)", "Program.F(int[])").WithLocation(8, 17));
        }

        [Fact]
        public void OverloadResolution_04()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static List<int> F1(List<int> arg) => arg;
                    static int[] F1(int[] arg) => arg;
                    static int[] F2(int[] arg) => arg;
                    static List<int> F2(List<int> arg) => arg;
                    static void Main()
                    {
                        var x = F1([1]);
                        var y = F2([2]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (10,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F1(List<int>)' and 'Program.F1(int[])'
                //         var x = F1([1]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F1").WithArguments("Program.F1(System.Collections.Generic.List<int>)", "Program.F1(int[])").WithLocation(10, 17),
                // (11,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F2(int[])' and 'Program.F2(List<int>)'
                //         var y = F2([2]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F2").WithArguments("Program.F2(int[])", "Program.F2(System.Collections.Generic.List<int>)").WithLocation(11, 17));
        }

        [Fact]
        public void OverloadResolution_05()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static List<int> F1(List<int> arg) => arg;
                    static List<long?> F1(List<long?> arg) => arg;
                    static List<long?> F2(List<long?> arg) => arg;
                    static List<int> F2(List<int> arg) => arg;
                    static void Main()
                    {
                        var x = F1([1]);
                        var y = F2([2]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (10,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F1(List<int>)' and 'Program.F1(List<long?>)'
                //         var x = F1([1]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F1").WithArguments("Program.F1(System.Collections.Generic.List<int>)", "Program.F1(System.Collections.Generic.List<long?>)").WithLocation(10, 17),
                // (11,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F2(List<long?>)' and 'Program.F2(List<int>)'
                //         var y = F2([2]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F2").WithArguments("Program.F2(System.Collections.Generic.List<long?>)", "Program.F2(System.Collections.Generic.List<int>)").WithLocation(11, 17));
        }

        [Fact]
        public void OverloadResolution_06()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static List<int?> F1(List<int?> arg) => arg;
                    static List<long> F1(List<long> arg) => arg;
                    static List<long> F2(List<long> arg) => arg;
                    static List<int?> F2(List<int?> arg) => arg;
                    static void Main()
                    {
                        var x = F1([1]);
                        var y = F2([2]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // 0.cs(10,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F1(List<int?>)' and 'Program.F1(List<long>)'
                //         var x = F1([1]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F1").WithArguments("Program.F1(System.Collections.Generic.List<int?>)", "Program.F1(System.Collections.Generic.List<long>)").WithLocation(10, 17),
                // 0.cs(11,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F2(List<long>)' and 'Program.F2(List<int?>)'
                //         var y = F2([2]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F2").WithArguments("Program.F2(System.Collections.Generic.List<long>)", "Program.F2(System.Collections.Generic.List<int?>)").WithLocation(11, 17));
        }

        [Fact]
        public void OverloadResolution_07()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                struct S : IEnumerable
                {
                    IEnumerator IEnumerable.GetEnumerator() => null;
                    public void Add(int i) { }
                }
                class Program
                {
                    static S F1(S arg) => arg;
                    static List<int> F1(List<int> arg) => arg;
                    static List<int> F2(List<int> arg) => arg;
                    static S F2(S arg) => arg;
                    static void Main()
                    {
                        var x = F1([1]);
                        var y = F2([2]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (16,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F1(S)' and 'Program.F1(List<int>)'
                //         var x = F1([1]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F1").WithArguments("Program.F1(S)", "Program.F1(System.Collections.Generic.List<int>)").WithLocation(16, 17),
                // (17,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F2(List<int>)' and 'Program.F2(S)'
                //         var y = F2([2]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F2").WithArguments("Program.F2(System.Collections.Generic.List<int>)", "Program.F2(S)").WithLocation(17, 17));
        }

        [Fact]
        public void OverloadResolution_08()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static IEnumerable<T> F1<T>(IEnumerable<T> arg) => arg;
                    static T[] F1<T>(T[] arg) => arg;
                    static T[] F2<T>(T[] arg) => arg;
                    static IEnumerable<T> F2<T>(IEnumerable<T> arg) => arg;
                    static void Main()
                    {
                        var x = F1([1]);
                        var y = F2([2]);
                        x.Report(includeType: true);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [1], (System.Int32[]) [2], ");
        }

        [Fact]
        public void OverloadResolution_09()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static int[] F1(int[] arg) => arg;
                    static string[] F1(string[] arg) => arg;
                    static List<int> F2(List<int> arg) => arg;
                    static List<string> F2(List<string> arg) => arg;
                    static string[] F3(string[] arg) => arg;
                    static List<int?> F3(List<int?> arg) => arg;
                    static void Main()
                    {
                        F1([]);
                        F2([]);
                        F3([null]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (12,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F1(int[])' and 'Program.F1(string[])'
                //         F1([]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F1").WithArguments("Program.F1(int[])", "Program.F1(string[])").WithLocation(12, 9),
                // (13,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F2(List<int>)' and 'Program.F2(List<string>)'
                //         F2([]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F2").WithArguments("Program.F2(System.Collections.Generic.List<int>)", "Program.F2(System.Collections.Generic.List<string>)").WithLocation(13, 9),
                // (14,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F3(string[])' and 'Program.F3(List<int?>)'
                //         F3([null]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F3").WithArguments("Program.F3(string[])", "Program.F3(System.Collections.Generic.List<int?>)").WithLocation(14, 9));
        }

        [Fact]
        public void OverloadResolution_ElementConversions_01()
        {
            string source = """
                class Program
                {
                    static string[] F(string[] arg) => arg;
                    static int?[] F(int?[] arg) => arg;
                    static void Main()
                    {
                        var x = F([null, 2, 3]);
                        x.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(System.Nullable<System.Int32>[]) [null, 2, 3], ");
        }

        [Fact]
        public void OverloadResolution_ElementConversions_02()
        {
            string source = """
                class Program
                {
                    static string[] F(string[] arg) => arg;
                    static int?[] F(int?[] arg) => arg;
                    static void Main()
                    {
                        int?[] x = [null, 2, 3];
                        var y = F([..x]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(System.Nullable<System.Int32>[]) [null, 2, 3], ");
        }

        [Fact]
        public void OverloadResolution_ElementConversions_03()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class MyCollection : IEnumerable
                {
                    private List<int> _items = new();
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                    public void Add(int i) { _items.Add(i); }
                }
                class Program
                {
                    static MyCollection F(MyCollection arg) => arg;
                    static int?[] F(int?[] arg) => arg;
                    static void Main()
                    {
                        var x = F([1, null]);
                        x.Report(includeType: true);
                        int?[] y = [null, 2];
                        var z = F([..y]);
                        z.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(System.Nullable<System.Int32>[]) [1, null], (System.Nullable<System.Int32>[]) [null, 2], ");
        }

        [Fact]
        public void OverloadResolution_ElementConversions_04()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class MyCollection : IEnumerable
                {
                    private List<int?> _items = new();
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                    public void Add(int? i) { _items.Add(i); }
                }
                class Program
                {
                    static MyCollection F(MyCollection arg) => arg;
                    static int[] F(int[] arg) => arg;
                    static void Main()
                    {
                        var x = F([1, null]);
                        x.Report(includeType: true);
                        int?[] y = [null, 2];
                        var z = F([..y]);
                        z.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(MyCollection) [1, null], (MyCollection) [null, 2], ");
        }

        [Fact]
        public void OverloadResolution_ElementConversions_05()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class MyCollection1 : IEnumerable
                {
                    private List<int?> _items = new();
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                    public void Add(int? i) { _items.Add(i); }
                }
                class MyCollection2 : IEnumerable
                {
                    private List<object> _items = new();
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                    public void Add(int i) { _items.Add(i); }
                    public void Add(string s) { _items.Add(s); }
                }
                class Program
                {
                    static MyCollection1 F(MyCollection1 arg) => arg;
                    static MyCollection2 F(MyCollection2 arg) => arg;
                    static void Main()
                    {
                        var x = F([1, (string)null]);
                        x.Report(includeType: true);
                        int?[] y = [null, 2];
                        var z = F([..y]);
                        z.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(MyCollection2) [1, null], (MyCollection1) [null, 2], ");
        }

        [Fact]
        public void OverloadResolution_ElementConversions_06()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create1))]
                class MyCollection1 : IEnumerable<int?>
                {
                    private List<int?> _list;
                    public MyCollection1(List<int?> list) { _list = list; }
                    IEnumerator<int?> IEnumerable<int?>.GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create2))]
                class MyCollection2 : IEnumerable<string>
                {
                    private List<string> _list;
                    public MyCollection2(List<string> list) { _list = list; }
                    IEnumerator<string> IEnumerable<string>.GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static MyCollection1 Create1(ReadOnlySpan<int?> items) => new MyCollection1(new(items.ToArray()));
                    public static MyCollection2 Create2(ReadOnlySpan<string> items) => new MyCollection2(new(items.ToArray()));
                }
                """;
            string sourceB = """
                class Program
                {
                    static MyCollection1 F(MyCollection1 arg) => arg;
                    static MyCollection2 F(MyCollection2 arg) => arg;
                    static void Main()
                    {
                        var x = F([null, 2, 3]);
                        x.Report(includeType: true);
                        string[] y = [null];
                        var z = F([..y]);
                        z.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceA, sourceB, s_collectionExtensions, CollectionBuilderAttributeDefinition },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("(MyCollection1) [null, 2, 3], (MyCollection2) [null], "));
        }

        [Fact]
        public void OverloadResolution_ElementConversions_07()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static ICollection<string> F(ICollection<string> arg) => arg;
                    static ICollection<int?> F(ICollection<int?> arg) => arg;
                    static void Main()
                    {
                        var x = F([null, 2, 3]);
                        x.Report(includeType: true);
                        string[] y = [null];
                        var z = F([..y]);
                        z.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(System.Collections.Generic.List<System.Nullable<System.Int32>>) [null, 2, 3], (System.Collections.Generic.List<System.String>) [null], ");
        }

        [Fact]
        public void OverloadResolution_ArgumentErrors()
        {
            string source = """
                using System.Linq;
                class Program
                {
                    static void Main()
                    {
                        [Unknown2].Zip([Unknown1]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,10): error CS0103: The name 'Unknown2' does not exist in the current context
                //         [Unknown2].Zip([Unknown1]);
                Diagnostic(ErrorCode.ERR_NameNotInContext, "Unknown2").WithArguments("Unknown2").WithLocation(6, 10),
                // (6,25): error CS0103: The name 'Unknown1' does not exist in the current context
                //         [Unknown2].Zip([Unknown1]);
                Diagnostic(ErrorCode.ERR_NameNotInContext, "Unknown1").WithArguments("Unknown1").WithLocation(6, 25));
        }

        private const string example_RefStructCollection = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(RefStructCollectionBuilder), nameof(RefStructCollectionBuilder.Create))]
                ref struct RefStructCollection<T>
                {
                    public IEnumerator<T> GetEnumerator() => null;
                }
                static class RefStructCollectionBuilder
                {
                    public static RefStructCollection<T> Create<T>(scoped ReadOnlySpan<T> items) => default;
                }
                """;

        private const string example_GenericClassCollection = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(GenericClassCollectionBuilder), nameof(GenericClassCollectionBuilder.Create))]
                class GenericClassCollection<T>
                {
                    public IEnumerator<T> GetEnumerator() => null;
                }
                static class GenericClassCollectionBuilder
                {
                    public static GenericClassCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;

        private const string example_NonGenericClassCollection = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(NonGenericClassCollectionBuilder), nameof(NonGenericClassCollectionBuilder.Create))]
                class NonGenericClassCollection
                {
                    public IEnumerator<object> GetEnumerator() => null;
                }
                static class NonGenericClassCollectionBuilder
                {
                    public static NonGenericClassCollection Create(ReadOnlySpan<object> items) => default;
                }
                """;

        [Theory]
        [InlineData("System.Span<T>", "T[]", "System.Span<System.Int32>")]
        [InlineData("System.Span<T>", "System.Collections.Generic.IEnumerable<T>", "System.Span<System.Int32>")]
        [InlineData("System.Span<T>", "System.Collections.Generic.IReadOnlyCollection<T>", "System.Span<System.Int32>")]
        [InlineData("System.Span<T>", "System.Collections.Generic.IReadOnlyList<T>", "System.Span<System.Int32>")]
        [InlineData("System.Span<T>", "System.Collections.Generic.ICollection<T>", "System.Span<System.Int32>")]
        [InlineData("System.Span<T>", "System.Collections.Generic.IList<T>", "System.Span<System.Int32>")]
        [InlineData("System.Span<T>", "System.Collections.Generic.HashSet<T>", "System.Span<System.Int32>")]
        [InlineData("System.Span<T>", "System.ReadOnlySpan<object>", null)] // rule requires ref struct and non- ref struct
        [InlineData("RefStructCollection<T>", "T[]", "RefStructCollection<System.Int32>", new[] { example_RefStructCollection })]
        [InlineData("RefStructCollection<T>", "RefStructCollection<object>", null, new[] { example_RefStructCollection })] // rule requires ref struct and non- ref struct
        [InlineData("RefStructCollection<int>", "GenericClassCollection<object>", "RefStructCollection<System.Int32>", new[] { example_RefStructCollection, example_GenericClassCollection })]
        [InlineData("RefStructCollection<object>", "GenericClassCollection<int>", null, new[] { example_RefStructCollection, example_GenericClassCollection })] // cannot convert object to int
        [InlineData("RefStructCollection<int>", "NonGenericClassCollection", "RefStructCollection<System.Int32>", new[] { example_RefStructCollection, example_NonGenericClassCollection })]
        [InlineData("GenericClassCollection<T>", "T[]", null, new[] { example_GenericClassCollection })] // rule requires ref struct
        [InlineData("NonGenericClassCollection", "object[]", null, new[] { example_NonGenericClassCollection })] // rule requires ref struct
        [InlineData("System.ReadOnlySpan<T>", "object[]", "System.ReadOnlySpan<System.Int32>")]
        [InlineData("System.ReadOnlySpan<T>", "long[]", "System.ReadOnlySpan<System.Int32>")]
        [InlineData("System.ReadOnlySpan<T>", "short[]", null)] // cannot convert int to short
        [InlineData("System.ReadOnlySpan<long>", "T[]", null)] // cannot convert long to int
        [InlineData("System.ReadOnlySpan<object>", "long[]", null)] // cannot convert object to long
        [InlineData("System.ReadOnlySpan<long>", "object[]", "System.ReadOnlySpan<System.Int64>")]
        [InlineData("System.ReadOnlySpan<long>", "string[]", "System.ReadOnlySpan<System.Int64>")]
        [InlineData("System.ReadOnlySpan<T>", "System.Span<T>", "System.Span<System.Int32>")] // implicit conversion from Span<T> to ReadOnlySpan<T>
        [InlineData("System.ReadOnlySpan<T>", "System.Span<int>", "System.Span<System.Int32>")]
        [InlineData("System.ReadOnlySpan<T>", "System.ReadOnlySpan<object>", null)] // cannot convert between ReadOnlySpan<int> and ReadOnlySpan<object>
        [InlineData("System.ReadOnlySpan<T>", "System.ReadOnlySpan<long>", null)] // cannot convert between ReadOnlySpan<int> and ReadOnlySpan<long>
        [InlineData("System.ReadOnlySpan<object>", "System.ReadOnlySpan<long>", null)] // cannot convert between ReadOnlySpan<object> and ReadOnlySpan<long>
        [InlineData("System.ReadOnlySpan<int>", "System.ReadOnlySpan<string>", "System.ReadOnlySpan<System.Int32>")]
        [InlineData("System.Span<int>", "int?[]", "System.Span<System.Int32>")]
        [InlineData("System.Span<int?>", "int[]", null)] // cannot convert int? to int
        [InlineData("System.Collections.Generic.List<int>", "System.Collections.Generic.IEnumerable<int>", "System.Collections.Generic.List<System.Int32>")]
        [InlineData("int[]", "object[]", null)] // rule requires ref struct
        [InlineData("int[]", "System.Collections.Generic.IReadOnlyList<object>", null)] // rule requires ref struct
        public void BetterConversionFromExpression_01(string type1, string type2, string expectedType, string[] additionalSources = null)
        {
            string source = $$"""
                using System;
                class Program
                {
                    {{generateMethod("F1", type1)}}
                    {{generateMethod("F1", type2)}}
                    {{generateMethod("F2", type2)}}
                    {{generateMethod("F2", type1)}}
                    static void Main()
                    {
                        var x = F1([1, 2, 3]);
                        Console.WriteLine(x.GetTypeName());
                        var y = F2([4, 5]);
                        Console.WriteLine(y.GetTypeName());
                    }
                }
                """;
            var comp = CreateCompilation(
                getSources(source, additionalSources),
                targetFramework: TargetFramework.Net80,
                options: TestOptions.ReleaseExe);
            if (expectedType is { })
            {
                CompileAndVerify(comp, verify: Verification.Skipped, expectedOutput: IncludeExpectedOutput($"""
                    {expectedType}
                    {expectedType}
                    """));
            }
            else
            {
                comp.VerifyEmitDiagnostics(
                    // 0.cs(10,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F1(ReadOnlySpan<long>)' and 'Program.F1(ReadOnlySpan<object>)'
                    //         var x = F1([1, 2, 3]);
                    Diagnostic(ErrorCode.ERR_AmbigCall, "F1").WithArguments(generateMethodSignature("F1", type1), generateMethodSignature("F1", type2)).WithLocation(10, 17),
                    // 0.cs(12,17): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F2(ReadOnlySpan<object>)' and 'Program.F2(ReadOnlySpan<long>)'
                    //         var y = F2([4, 5]);
                    Diagnostic(ErrorCode.ERR_AmbigCall, "F2").WithArguments(generateMethodSignature("F2", type2), generateMethodSignature("F2", type1)).WithLocation(12, 17));
            }

            static string getTypeParameters(string type) =>
                type.Contains("T[]") || type.Contains("<T>") ? "<T>" : "";

            static string generateMethod(string methodName, string parameterType) =>
                $"static Type {methodName}{getTypeParameters(parameterType)}({parameterType} value) => typeof({parameterType});";

            static string generateMethodSignature(string methodName, string parameterType) =>
                $"Program.{methodName}{getTypeParameters(parameterType)}({parameterType})";

            static string[] getSources(string source, string[] additionalSources)
            {
                var builder = ArrayBuilder<string>.GetInstance();
                builder.Add(source);
                builder.Add(s_collectionExtensions);
                if (additionalSources is { }) builder.AddRange(additionalSources);
                return builder.ToArrayAndFree();
            }
        }

        [Fact]
        public void BetterConversionFromExpression_02()
        {
            string sourceA = """
                using System;
                using static System.Console;

                partial class Program
                {
                    static void Generic<T>(Span<T> value) { WriteLine("Span<T>"); }
                    static void Generic<T>(T[] value)     { WriteLine("T[]"); }

                    static void Identical(Span<string> value) { WriteLine("Span<string>"); }
                    static void Identical(string[] value)     { WriteLine("string[]"); }

                    static void SpanDerived(Span<string> value) { WriteLine("Span<string>"); }
                    static void SpanDerived(object[] value)     { WriteLine("object[]"); }

                    static void ArrayDerived(Span<object> value) { WriteLine("Span<object>"); }
                    static void ArrayDerived(string[] value)     { WriteLine("string[]"); }
                }
                """;

            string sourceB1 = """
                partial class Program
                {
                    static void Main()
                    {
                        Generic(new[] { string.Empty }); // string[]
                        Identical(new[] { string.Empty }); // string[]
                        ArrayDerived(new[] { string.Empty }); // string[]

                        Generic([string.Empty]); // Span<string>
                        Identical([string.Empty]); // Span<string>
                        SpanDerived([string.Empty]); // Span<string>
                    }
                }
                """;
            var comp = CreateCompilation(
                new[] { sourceA, sourceB1 },
                targetFramework: TargetFramework.Net80,
                options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, verify: Verification.Skipped, expectedOutput: IncludeExpectedOutput("""
                T[]
                string[]
                string[]
                Span<T>
                Span<string>
                Span<string>
                """));

            string sourceB2 = """
                partial class Program
                {
                    static void Main()
                    {
                        SpanDerived(new[] { string.Empty }); // ambiguous
                        ArrayDerived([string.Empty]); // ambiguous
                    }
                }
                """;
            comp = CreateCompilation(
                new[] { sourceA, sourceB2 },
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 1.cs(5,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.SpanDerived(Span<string>)' and 'Program.SpanDerived(object[])'
                //         SpanDerived(new[] { string.Empty }); // ambiguous
                Diagnostic(ErrorCode.ERR_AmbigCall, "SpanDerived").WithArguments("Program.SpanDerived(System.Span<string>)", "Program.SpanDerived(object[])").WithLocation(5, 9),
                // 1.cs(6,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.ArrayDerived(Span<object>)' and 'Program.ArrayDerived(string[])'
                //         ArrayDerived([string.Empty]); // ambiguous
                Diagnostic(ErrorCode.ERR_AmbigCall, "ArrayDerived").WithArguments("Program.ArrayDerived(System.Span<object>)", "Program.ArrayDerived(string[])").WithLocation(6, 9));
        }

        [WorkItem("https://github.com/dotnet/roslyn/issues/69634")]
        [Fact]
        public void BetterConversionFromExpression_03()
        {
            string sourceA = """
                using System;
                using static System.Console;

                partial class Program
                {
                    static void Unrelated(Span<int> value) { WriteLine("Span<int>"); }
                    static void Unrelated(string[] value)     { WriteLine("string[]"); }
                }
                """;

            string sourceB1 = """
                partial class Program
                {
                    static void Main()
                    {
                        Unrelated(new[] { 1 }); // Span<int>
                        Unrelated(new[] { string.Empty }); // string[]

                        Unrelated([2]); // Span<int>
                        Unrelated([string.Empty]); // string[]
                    }
                }
                """;
            var comp = CreateCompilation(
                new[] { sourceA, sourceB1 },
                targetFramework: TargetFramework.Net80,
                options: TestOptions.ReleaseExe);
            CompileAndVerify(comp, verify: Verification.Skipped, expectedOutput: IncludeExpectedOutput("""
                Span<int>
                string[]
                Span<int>
                string[]
                """));

            string sourceB2 = """
                partial class Program
                {
                    static void Main()
                    {
                        Unrelated(new[] { default }); // error
                        Unrelated([default]); // ambiguous
                    }
                }
                """;
            comp = CreateCompilation(
                new[] { sourceA, sourceB2 },
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 1.cs(5,19): error CS0826: No best type found for implicitly-typed array
                //         Unrelated(new[] { default }); // error
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "new[] { default }").WithLocation(5, 19),
                // 1.cs(5,19): error CS1503: Argument 1: cannot convert from '?[]' to 'System.Span<int>'
                //         Unrelated(new[] { default }); // error
                Diagnostic(ErrorCode.ERR_BadArgType, "new[] { default }").WithArguments("1", "?[]", "System.Span<int>").WithLocation(5, 19),
                // 1.cs(6,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.Unrelated(Span<int>)' and 'Program.Unrelated(string[])'
                //         Unrelated([default]); // ambiguous
                Diagnostic(ErrorCode.ERR_AmbigCall, "Unrelated").WithArguments("Program.Unrelated(System.Span<int>)", "Program.Unrelated(string[])").WithLocation(6, 9));
        }

        [Fact]
        public void BetterConversionFromExpression_04()
        {
            string source = """
                using System;
                class Program
                {
                    static void F1(int[] x, int[] y) { throw null; }
                    static void F1(Span<object> x, ReadOnlySpan<int> y) { x.Report(); y.Report(); }
                    static void F2(object x, string[] y) { throw null; }
                    static void F2(string x, Span<object> y) { y.Report(); }
                    static void Main()
                    {
                        F1([1], [2]);
                        F2("3", ["4"]);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [2], [4], "));
        }

        [Fact]
        public void BetterConversionFromExpression_05()
        {
            string source = """
                using System;
                class Program
                {
                    static void F1(Span<int> x, int[] y) { throw null; }
                    static void F1(int[] x, ReadOnlySpan<int> y) { }
                    static void F2(string x, string[] y) { throw null; }
                    static void F2(object x, Span<string> y) { }
                    static void Main()
                    {
                        F1([1], [2]);
                        F2("3", ["4"]);
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (10,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F1(Span<int>, int[])' and 'Program.F1(int[], ReadOnlySpan<int>)'
                //         F1([1], [2]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F1").WithArguments("Program.F1(System.Span<int>, int[])", "Program.F1(int[], System.ReadOnlySpan<int>)").WithLocation(10, 9),
                // (11,9): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F2(string, string[])' and 'Program.F2(object, Span<string>)'
                //         F2("3", ["4"]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F2").WithArguments("Program.F2(string, string[])", "Program.F2(object, System.Span<string>)").WithLocation(11, 9));
        }

        // Two ref struct collection types, with an implicit conversion from one to the other.
        [Fact]
        public void BetterConversionFromExpression_06()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create1))]
                ref struct MyCollection1<T>
                {
                    private readonly List<T> _list;
                    public MyCollection1(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    public static implicit operator MyCollection2<T>(MyCollection1<T> c) => new(c._list);
                }
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create2))]
                ref struct MyCollection2<T>
                {
                    private readonly List<T> _list;
                    public MyCollection2(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                }
                static class MyCollectionBuilder
                {
                    public static MyCollection1<T> Create1<T>(scoped ReadOnlySpan<T> items)
                    {
                        return new MyCollection1<T>(new List<T>(items.ToArray()));
                    }
                    public static MyCollection2<T> Create2<T>(scoped ReadOnlySpan<T> items)
                    {
                        return new MyCollection2<T>(new List<T>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void F1<T>(MyCollection1<T> c) { Console.WriteLine("MyCollection1<T>"); }
                    static void F1<T>(MyCollection2<T> c) { Console.WriteLine("MyCollection2<T>"); }
                    static void F2(MyCollection2<object> c) { Console.WriteLine("MyCollection2<object>"); }
                    static void F2(MyCollection1<object> c) { Console.WriteLine("MyCollection1<object>"); }
                    static void Main()
                    {
                        F1([1, 2, 3]);
                        F2([4, null]);
                        F1((MyCollection1<object>)[6]);
                        F1((MyCollection2<int>)[7]);
                        F2((MyCollection2<object>)[8]);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, CollectionBuilderAttributeDefinition },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("""
                    MyCollection1<T>
                    MyCollection1<object>
                    MyCollection1<T>
                    MyCollection2<T>
                    MyCollection2<object>
                    """));
        }

        [Fact]
        public void BestCommonType_01()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        var x = new[] { new int[0], [1, 2, 3] };
                        x.Report(includeType: true);
                        var y = new[] { new[] { new int[0] }, [[1, 2, 3]] };
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[][]) [[], [1, 2, 3]], (System.Int32[][][]) [[[]], [[1, 2, 3]]], ");
        }

        [Fact]
        public void BestCommonType_02()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        var x = new[] { new byte[0], [1, 2, 3] };
                        x.Report(includeType: true);
                        var y = new[] { new[] { new byte[0] }, [[1, 2, 3]] };
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Byte[][]) [[], [1, 2, 3]], (System.Byte[][][]) [[[]], [[1, 2, 3]]], ");
        }

        [Fact]
        public void BestCommonType_03()
        {
            string source = """
                class Program
                {
                    static void Main(string[] args)
                    {
                        var x = new[] { [""], new object[0] };
                        var y = new[] { [[""]], [new object[0]] };
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,17): error CS0826: No best type found for implicitly-typed array
                //         var y = new[] { [[""]], [new object[0]] };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, @"new[] { [[""""]], [new object[0]] }").WithLocation(6, 17));
        }

        [Fact]
        public void BestCommonType_04()
        {
            string source = """
                class Program
                {
                    static void Main(string[] args)
                    {
                        var x = args.Length > 0 ? new int[0] : [1, 2, 3];
                        x.Report(includeType: true);
                        var y = args.Length == 0 ? [[4, 5]] : new[] { new byte[0] };
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [1, 2, 3], (System.Byte[][]) [[4, 5]], ");
        }

        [Fact]
        public void BestCommonType_05()
        {
            string source = """
                class Program
                {
                    static void Main(string[] args)
                    {
                        bool b = args.Length > 0;
                        var y = b ? [new int[0]] : [[1, 2, 3]];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,17): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'collection expressions' and 'collection expressions'
                //         var y = b ? [new int[0]] : [[1, 2, 3]];
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? [new int[0]] : [[1, 2, 3]]").WithArguments("collection expressions", "collection expressions").WithLocation(6, 17));
        }

        [Fact]
        public void TypeInference_01()
        {
            string source = """
                static class Program
                {
                    static T F<T>(T a, T b)
                    {
                        return b;
                    }
                    static void Main()
                    {
                        var x = F(["str"]);
                        var y = F([[], [1, 2]]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (9,17): error CS7036: There is no argument given that corresponds to the required parameter 'b' of 'Program.F<T>(T, T)'
                //         var x = F(["str"]);
                Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "F").WithArguments("b", "Program.F<T>(T, T)").WithLocation(9, 17),
                // (10,17): error CS7036: There is no argument given that corresponds to the required parameter 'b' of 'Program.F<T>(T, T)'
                //         var y = F([[], [1, 2]]);
                Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "F").WithArguments("b", "Program.F<T>(T, T)").WithLocation(10, 17));
        }

        [Fact]
        public void TypeInference_02()
        {
            string source = """
                static class Program
                {
                    static T F<T>(T a, T b)
                    {
                        return b;
                    }
                    static void Main()
                    {
                        _ = F([new int[0]], new[] { [1, 2, 3] });
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (9,29): error CS0826: No best type found for implicitly-typed array
                //         _ = F([new int[0]], new[] { [1, 2, 3] });
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "new[] { [1, 2, 3] }").WithLocation(9, 29));
        }

        [Fact]
        public void TypeInference_03()
        {
            string source = """
                class Program
                {
                    static T[] AsArray1<T>(T[] args) => args;
                    static T[] AsArray2<T>(params T[] args) => args;
                    static void Main()
                    {
                        var a = AsArray1([1, 2, 3]);
                        a.Report();
                        var b = AsArray2(["4", null]);
                        b.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2, 3], [4, null], ");
        }

        [Fact]
        public void TypeInference_04()
        {
            string source = """
                class Program
                {
                    static T[] AsArray<T>(T[] args)
                    {
                        return args;
                    }
                    static void Main()
                    {
                        AsArray([]);
                        AsArray([1, null]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (9,9): error CS0411: The type arguments for method 'Program.AsArray<T>(T[])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         AsArray([]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "AsArray").WithArguments("Program.AsArray<T>(T[])").WithLocation(9, 9),
                // (10,17): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'int[]'
                //         AsArray([1, null]);
                Diagnostic(ErrorCode.ERR_BadArgType, "[1, null]").WithArguments("1", "collection expressions", "int[]").WithLocation(10, 17));
        }

        [Fact]
        public void TypeInference_06()
        {
            string source = """
                class Program
                {
                    static T[] AsArray<T>(T[] args)
                    {
                        return args;
                    }
                    static void F(bool b, int x, int y)
                    {
                        var a = AsArray([.. b ? [x] : [y]]);
                        a.Report();
                    }
                    static void Main()
                    {
                        F(false, 1, 2);
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics(
                // 0.cs(9,29): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'collection expressions' and 'collection expressions'
                //         var a = AsArray([.. b ? [x] : [y]]);
                Diagnostic(ErrorCode.ERR_InvalidQM, "b ? [x] : [y]").WithArguments("collection expressions", "collection expressions").WithLocation(9, 29));
        }

        [Fact]
        public void TypeInference_07()
        {
            string source = """
                static class Program
                {
                    static T[] AsArray<T>(this T[] args)
                    {
                        return args;
                    }
                    static void Main()
                    {
                        var a = [1, 2, 3].AsArray();
                        a.Report();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics(
                // 0.cs(9,17): error CS9176: There is no target type for the collection expression.
                //         var a = [1, 2, 3].AsArray();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[1, 2, 3]").WithLocation(9, 17));
        }

        [Fact]
        public void TypeInference_08()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                struct S<T> : IEnumerable<T>
                {
                    private List<T> _list;
                    public void Add(T t)
                    {
                        _list ??= new List<T>();
                        _list.Add(t);
                    }
                    public IEnumerator<T> GetEnumerator()
                    {
                        _list ??= new List<T>();
                        return _list.GetEnumerator();
                    }
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                static class Program
                {
                    static S<T> AsCollection<T>(this S<T> args)
                    {
                        return args;
                    }
                    static void Main()
                    {
                        var a = AsCollection([1, 2, 3]);
                        var b = [4].AsCollection();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (27,17): error CS9176: There is no target type for the collection expression.
                //         var b = [4].AsCollection();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[4]").WithLocation(27, 17));
        }

        [Fact]
        public void TypeInference_09()
        {
            string source = """
                using System.Collections;
                struct S<T> : IEnumerable
                {
                    public void Add(T t) { }
                    IEnumerator IEnumerable.GetEnumerator() => null;
                }
                static class Program
                {
                    static S<T> AsCollection<T>(this S<T> args)
                    {
                        return args;
                    }
                    static void Main()
                    {
                        _ = AsCollection([1, 2, 3]);
                        _ = [4].AsCollection();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (15,13): error CS0411: The type arguments for method 'Program.AsCollection<T>(S<T>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         _ = AsCollection([1, 2, 3]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "AsCollection").WithArguments("Program.AsCollection<T>(S<T>)").WithLocation(15, 13),
                // (16,13): error CS9176: There is no target type for the collection expression.
                //         _ = [4].AsCollection();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[4]").WithLocation(16, 13));
        }

        [Fact]
        public void TypeInference_10()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static List<T> F<T>(List<T> arg) => arg;
                    static void Main()
                    {
                        _ = F([1, 2, 3]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,13): error CS0121: The call is ambiguous between the following methods or properties: 'Program.F<T>(T[])' and 'Program.F<T>(List<T>)'
                //         _ = F([1, 2, 3]);
                Diagnostic(ErrorCode.ERR_AmbigCall, "F").WithArguments("Program.F<T>(T[])", "Program.F<T>(System.Collections.Generic.List<T>)").WithLocation(8, 13));
        }

        [Fact]
        public void TypeInference_11()
        {
            string source = """
                using System.Collections;
                struct S<T> : IEnumerable
                {
                    public void Add(T t) { }
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static S<T> F<T>(S<T> arg) => arg;
                    static void Main()
                    {
                        var x = F([1, 2, 3]);
                        x.Report(true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [1, 2, 3], ");
        }

        [Fact]
        public void TypeInference_12()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[] x, T[] y) => x;
                    static void Main()
                    {
                        var x = F(["1"], [(object)"2"]);
                        x.Report(includeType: true);
                        var y = F([(object)"3"], ["4"]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Object[]) [1], (System.Object[]) [3], ");
        }

        [Fact]
        public void TypeInference_13A()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[] x, T[] y) => x;
                    static void Main()
                    {
                        var x = F([1], [(long)2]);
                        x.Report(includeType: true);
                        var y = F([(long)3], [4]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int64[]) [1], (System.Int64[]) [3], ");
        }

        [Fact]
        public void TypeInference_13B()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static HashSet<T> F<T>(HashSet<T> x, HashSet<T> y) => x;
                    static void Main()
                    {
                        var x = F([1], [(long)2]);
                        x.Report(includeType: true);
                        var y = F([(long)3], [4]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Collections.Generic.HashSet<System.Int64>) [1], (System.Collections.Generic.HashSet<System.Int64>) [3], ");
        }

        [Fact]
        public void TypeInference_14()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[][] x) => x[0];
                    static void Main()
                    {
                        var x = F([[1, 2, 3]]);
                        x.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [1, 2, 3], ");
        }

        [Fact]
        public void TypeInference_15()
        {
            string source = """
                class Program
                {
                    static T F0<T>(T[] x, T y) => y;
                    static T[] F1<T>(T[] x, T[] y) => y;
                    static T[] F2<T>(T[][] x, T[][] y) => y[0];
                    static void Main()
                    {
                        var x = F0(new byte[0], 1);
                        var y = F1(new byte[0], [1, 2]);
                        var z = F2(new[] { new byte[0] }, [[3, 4]]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,17): error CS0411: The type arguments for method 'Program.F0<T>(T[], T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x = F0(new byte[0], 1);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F0").WithArguments("Program.F0<T>(T[], T)").WithLocation(8, 17),
                // (9,17): error CS0411: The type arguments for method 'Program.F1<T>(T[], T[])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var y = F1(new byte[0], [1, 2]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F1").WithArguments("Program.F1<T>(T[], T[])").WithLocation(9, 17),
                // (10,17): error CS0411: The type arguments for method 'Program.F2<T>(T[][], T[][])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var z = F2(new[] { new byte[0] }, [[3, 4]]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F2").WithArguments("Program.F2<T>(T[][], T[][])").WithLocation(10, 17));
        }

        [Fact]
        public void TypeInference_16()
        {
            string source = """
                class Program
                {
                    static T[] F1<T>(T[] x, T[] y) => y;
                    static T[] F2<T>(T[][] x, T[][] y) => y[0];
                    static void Main()
                    {
                        var x = F1([1], [(byte)2]);
                        x.Report(true);
                        var y = F2([[3]], [[(byte)4]]);
                        y.Report(true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [2], (System.Int32[]) [4], ");
        }

        [Fact]
        public void TypeInference_17()
        {
            string source = """
                class Program
                {
                    static T[] F1<T>(T[] x, T[] y) => y;
                    static T[] F2<T>(T[][] x, T[][] y) => y[0];
                    static void Main()
                    {
                        var x = F1([(long)1], [(int?)2]);
                        var y = F2([[(int?)3]], [[(long)4]]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (7,17): error CS0411: The type arguments for method 'Program.F1<T>(T[], T[])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x = F1([(long)1], [(int?)2]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F1").WithArguments("Program.F1<T>(T[], T[])").WithLocation(7, 17),
                // (8,17): error CS0411: The type arguments for method 'Program.F2<T>(T[][], T[][])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var y = F2([[(int?)3]], [[(long)4]]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F2").WithArguments("Program.F2<T>(T[][], T[][])").WithLocation(8, 17));
        }

        [Fact]
        public void TypeInference_18()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static List<T[]> AsListOfArray<T>(List<T[]> arg) => arg;
                    static void Main()
                    {
                        var x = AsListOfArray([[4, 5], []]);
                        x.Report(true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Collections.Generic.List<System.Int32[]>) [[4, 5], []], ");
        }

        [Fact]
        public void TypeInference_19()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[][] x) => x[1];
                    static void Main()
                    {
                        var y = F([new byte[0], [1, 2, 3]]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,17): error CS0411: The type arguments for method 'Program.F<T>(T[][])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var y = F([new byte[0], [1, 2, 3]]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T[][])").WithLocation(6, 17));
        }

        [Fact]
        public void TypeInference_20()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(in T[] x, T[] y) => x;
                    static void Main()
                    {
                        var x = F([1], [2]);
                        x.Report(true);
                        var y = F([3], [(object)4]);
                        y.Report(true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [1], (System.Object[]) [3], ");
        }

        [Fact]
        public void TypeInference_21()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(in T[] x, T[] y) => x;
                    static void Main()
                    {
                        var y = F(in [3], [(object)4]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,22): error CS8156: An expression cannot be used in this context because it may not be passed or returned by reference
                //         var y = F(in [3], [(object)4]);
                Diagnostic(ErrorCode.ERR_RefReturnLvalueExpected, "[3]").WithLocation(6, 22));
        }

        [Fact]
        public void TypeInference_22()
        {
            string source = """
                class Program
                {
                    static T[] F1<T>(T[] x, T[] y) => y;
                    static T[] F2<T>(T[][] x, T[][] y) => y[0];
                    static void Main()
                    {
                        var x = F1([], [default, 2]);
                        x.Report(true);
                        var y = F2([[null]], [[default, (int?)4]]);
                        y.Report(true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [0, 2], (System.Nullable<System.Int32>[]) [null, 4], ");
        }

        [Fact]
        public void TypeInference_23()
        {
            string source = """
                class Program
                {
                    static T[] F1<T>(T[] x, T[] y) => y;
                    static T[] F2<T>(T[][] x, T[][] y) => y[0];
                    static void Main()
                    {
                        var x = F1([], [default]);
                        var y = F2([[null]], [[default]]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (7,17): error CS0411: The type arguments for method 'Program.F1<T>(T[], T[])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x = F1([], [default]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F1").WithArguments("Program.F1<T>(T[], T[])").WithLocation(7, 17),
                // (8,17): error CS0411: The type arguments for method 'Program.F2<T>(T[][], T[][])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var y = F2([[null]], [[default]]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F2").WithArguments("Program.F2<T>(T[][], T[][])").WithLocation(8, 17));
        }

        [Fact]
        public void TypeInference_24()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static ReadOnlySpan<T> F1<T>(Span<T> x, ReadOnlySpan<T> y) => y;
                    static List<T> F2<T>(Span<T[]> x, ReadOnlySpan<List<T>> y) => y[0];
                    static void Main()
                    {
                        var x = F1([], [default, 2]);
                        x.Report();
                        var y = F2([[null]], [[default, (int?)4]]);
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net70,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[0, 2], [null, 4], "));
        }

        [Fact]
        public void TypeInference_25()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static ReadOnlySpan<T> F1<T>(Span<T> x, ReadOnlySpan<T> y) => y;
                    static List<T> F2<T>(Span<T[]> x, ReadOnlySpan<List<T>> y) => y[0];
                    static void Main()
                    {
                        var x = F1([], [default]);
                        var y = F2([[null]], [[default]]);
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net70);
            comp.VerifyEmitDiagnostics(
                // (9,17): error CS0411: The type arguments for method 'Program.F1<T>(Span<T>, ReadOnlySpan<T>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x = F1([], [default]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F1").WithArguments("Program.F1<T>(System.Span<T>, System.ReadOnlySpan<T>)").WithLocation(9, 17),
                // (10,17): error CS0411: The type arguments for method 'Program.F2<T>(Span<T[]>, ReadOnlySpan<List<T>>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var y = F2([[null]], [[default]]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F2").WithArguments("Program.F2<T>(System.Span<T[]>, System.ReadOnlySpan<System.Collections.Generic.List<T>>)").WithLocation(10, 17));
        }

        [Fact]
        public void TypeInference_26()
        {
            string source = """
                class Program
                {
                    static void F<T>(T x) { }
                    static void Main()
                    {
                        F([]);
                        F([null, default, 0]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,9): error CS0411: The type arguments for method 'Program.F<T>(T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T)").WithLocation(6, 9),
                // (7,9): error CS0411: The type arguments for method 'Program.F<T>(T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([null, default, 0]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T)").WithLocation(7, 9));
        }

        [Fact]
        public void TypeInference_27()
        {
            string source = """
                class Program
                {
                    static void F<T>(T[,] x) { }
                    static void Main()
                    {
                        F([]);
                        F([null, default, 0]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,9): error CS0411: The type arguments for method 'Program.F<T>(T[*,*])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T[*,*])").WithLocation(6, 9),
                // (7,11): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'int[*,*]'
                //         F([null, default, 0]);
                Diagnostic(ErrorCode.ERR_BadArgType, "[null, default, 0]").WithArguments("1", "collection expressions", "int[*,*]").WithLocation(7, 11));
        }

        [Fact]
        public void TypeInference_28()
        {
            string source = """
                class Program
                {
                    static void F<T>(string x, T[] y) { }
                    static void Main()
                    {
                        F([], ['B']);
                        F([default], ['B']);
                        F(['A'], ['B']);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,11): error CS1729: 'string' does not contain a constructor that takes 0 arguments
                //         F([], ['B']);
                Diagnostic(ErrorCode.ERR_BadCtorArgCount, "[]").WithArguments("string", "0").WithLocation(6, 11),
                // (7,11): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'string'
                //         F([default], ['B']);
                Diagnostic(ErrorCode.ERR_BadArgType, "[default]").WithArguments("1", "collection expressions", "string").WithLocation(7, 11),
                // (8,11): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'string'
                //         F(['A'], ['B']);
                Diagnostic(ErrorCode.ERR_BadArgType, "['A']").WithArguments("1", "collection expressions", "string").WithLocation(8, 11));
        }

        [Fact]
        public void TypeInference_29()
        {
            string source = """
                delegate void D();
                enum E { }
                class Program
                {
                    static void F1<T>(dynamic x, T[] y) { }
                    static void F2<T>(D x, T[] y) { }
                    static void F3<T>(E x, T[] y) { }
                    static void Main()
                    {
                        F1([1], [2]);
                        F2([3], [4]);
                        F3([5], [6]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (10,12): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'dynamic'
                //         F1([1], [2]);
                Diagnostic(ErrorCode.ERR_BadArgType, "[1]").WithArguments("1", "collection expressions", "dynamic").WithLocation(10, 12),
                // (11,12): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'D'
                //         F2([3], [4]);
                Diagnostic(ErrorCode.ERR_BadArgType, "[3]").WithArguments("1", "collection expressions", "D").WithLocation(11, 12),
                // (12,12): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'E'
                //         F3([5], [6]);
                Diagnostic(ErrorCode.ERR_BadArgType, "[5]").WithArguments("1", "collection expressions", "E").WithLocation(12, 12));
        }

        [Fact]
        public void TypeInference_30()
        {
            string source = """
                delegate void D();
                enum E { }
                class Program
                {
                    static void F1<T>(dynamic[] x, T[] y) { }
                    static void F2<T>(D[] x, T[] y) { }
                    static void F3<T>(E[] x, T[] y) { }
                    static void Main()
                    {
                        F1([1], [2]);
                        F2([null], [4]);
                        F3([default], [6]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();
        }

        [Fact]
        public void TypeInference_31()
        {
            string source = """
                class Program
                {
                    static void F<T>(T[] x) { }
                    static void Main()
                    {
                        F([null]);
                        F([Unknown]);
                        F([Main()]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,9): error CS0411: The type arguments for method 'Program.F<T>(T[])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([null]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T[])").WithLocation(6, 9),
                // (7,12): error CS0103: The name 'Unknown' does not exist in the current context
                //         F([Unknown]);
                Diagnostic(ErrorCode.ERR_NameNotInContext, "Unknown").WithArguments("Unknown").WithLocation(7, 12),
                // (8,9): error CS0411: The type arguments for method 'Program.F<T>(T[])' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         F([Main()]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(T[])").WithLocation(8, 9));
        }

        [Fact]
        public void TypeInference_32()
        {
            string source = """
                delegate void D();
                class Program
                {
                    static T[] F<T>(T[] x) => x;
                    static void Main()
                    {
                        var x = F([null, Main]);
                        x.Report(includeType: true);
                        var y = F([Main, (D)Main]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Action[]) [null, System.Action], (D[]) [D, D], ");
        }

        [Fact]
        public void TypeInference_33()
        {
            string source = """
                delegate byte D();
                class Program
                {
                    static T[] F<T>(T[] x) => x;
                    static void Main()
                    {
                        var x = F([null, () => 1]);
                        x.Report(includeType: true);
                        var y = F([() => 2, (D)(() => 3)]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Func<System.Int32>[]) [null, System.Func`1[System.Int32]], (D[]) [D, D], ");
        }

        [Fact]
        public void TypeInference_34()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static List<Func<T>> F1<T>(List<Func<T>> x) => x;
                    static string F2() => null;
                    static void Main()
                    {
                        var x = F1([F2]);
                        x.Report();
                        var y = F1([null, () => 1]);
                        y.Report();
                        var z = F1([F2, () => default]);
                        z.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "[System.Func`1[System.String]], [null, System.Func`1[System.Int32]], [System.Func`1[System.String], System.Func`1[System.String]], ");
        }

        [Fact]
        public void TypeInference_35()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static List<Action<T>> F1<T>(List<Action<T>> x) => x;
                    static void F2(string s) { }
                    static void Main()
                    {
                        var x = F1([F2, (string s) => { }]);
                        x.Report();
                        var y = F1([null, (int a) => { }]);
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "[System.Action`1[System.String], System.Action`1[System.String]], [null, System.Action`1[System.Int32]], ");
        }

        [Fact]
        public void TypeInference_36()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static List<Func<T>> F1<T>(List<Func<T>> x) => x;
                    static string F2() => null;
                    static void Main()
                    {
                        var x = F1([() => default]);
                        var y = F1([() => 2, F2]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (9,17): error CS0411: The type arguments for method 'Program.F1<T>(List<Func<T>>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x = F1([() => default]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F1").WithArguments("Program.F1<T>(System.Collections.Generic.List<System.Func<T>>)").WithLocation(9, 17),
                // (10,17): error CS0411: The type arguments for method 'Program.F1<T>(List<Func<T>>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var y = F1([null, () => 1]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F1").WithArguments("Program.F1<T>(System.Collections.Generic.List<System.Func<T>>)").WithLocation(10, 17));
        }

        [Fact]
        public void TypeInference_37()
        {
            string source = """
                class Program
                {
                    static (T, U)[] F<T, U>((T, U)[] x) => x;
                    static void Main()
                    {
                        var x = F([(1, "2")]);
                        x.Report(includeType: true);
                        var y = F([default, (3, (byte)4)]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "(System.ValueTuple<System.Int32, System.String>[]) [(1, 2)], (System.ValueTuple<System.Int32, System.Byte>[]) [(0, 0), (3, 4)], ");
        }

        [Fact]
        public void TypeInference_38()
        {
            string source = """
                class Program
                {
                    static T[] F1<T>(T[] x, T y) => x;
                    static T[] F2<T>(T[] x, ref T y) => x;
                    static T[] F3<T>(T[] x, in T y) => x;
                    static T[] F4<T>(T[] x, out T y) { y = default; return x; }
                    static void Main()
                    {
                        object y = null;
                        var x1 = F1([1], y);
                        var x2 = F2([2], ref y);
                        var x3A = F3([3], y);
                        var x3B = F3([3], in y);
                        var x4 = F4([4], out y);
                        x1.Report(true);
                        x2.Report(true);
                        x3A.Report(true);
                        x3B.Report(true);
                        x4.Report(true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Object[]) [1], (System.Object[]) [2], (System.Object[]) [3], (System.Object[]) [3], (System.Object[]) [4], ");
        }

        [Fact]
        public void TypeInference_39A()
        {
            string source = """
                class Program
                {
                    static T[] F1<T>(T[] x, T y) => x;
                    static T[] F3<T>(T[] x, in T y) => x;
                    static void Main()
                    {
                        byte y = 0;
                        var x1 = F1([1], y);
                        var x3A = F3([3], y);
                        x1.Report(true);
                        x3A.Report(true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [1], (System.Int32[]) [3], ");
        }

        [Fact]
        public void TypeInference_39B()
        {
            string source = """
                class Program
                {
                    static T[] F2<T>(T[] x, ref T y) => x;
                    static T[] F3<T>(T[] x, in T y) => x;
                    static T[] F4<T>(T[] x, out T y) { y = default; return x; }
                    static void Main()
                    {
                        byte y = 0;
                        var x2 = F2([2], ref y);
                        var x3B = F3([3], in y);
                        var x4 = F4([4], out y);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (9,18): error CS0411: The type arguments for method 'Program.F2<T>(T[], ref T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x2 = F2([2], ref y);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F2").WithArguments("Program.F2<T>(T[], ref T)").WithLocation(9, 18),
                // (10,19): error CS0411: The type arguments for method 'Program.F3<T>(T[], in T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x3B = F3([3], in y);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F3").WithArguments("Program.F3<T>(T[], in T)").WithLocation(10, 19),
                // (11,18): error CS0411: The type arguments for method 'Program.F4<T>(T[], out T)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x4 = F4([4], out y);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F4").WithArguments("Program.F4<T>(T[], out T)").WithLocation(11, 18));
        }

        [Fact]
        public void TypeInference_40()
        {
            string source = """
                using System;
                class Program
                {
                    static Func<T[]> F<T>(Func<T[]> arg) => arg;
                    static void Main(string[] args)
                    {
                        var x = F(() => [1, 2, 3]);
                        x.Report(includeType: true);
                        var y = F(() => { if (args.Length == 0) return []; return [1, 2, 3]; });
                        y.Report(includeType: true);
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics(
                // 0.cs(7,17): error CS0411: The type arguments for method 'Program.F<T>(Func<T[]>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var x = F(() => [1, 2, 3]);
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(System.Func<T[]>)").WithLocation(7, 17),
                // 0.cs(9,17): error CS0411: The type arguments for method 'Program.F<T>(Func<T[]>)' cannot be inferred from the usage. Try specifying the type arguments explicitly.
                //         var y = F(() => { if (args.Length == 0) return []; return [1, 2, 3]; });
                Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "F").WithArguments("Program.F<T>(System.Func<T[]>)").WithLocation(9, 17));
        }

        [Fact]
        public void TypeInference_Spread_01()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static void Main()
                    {
                        int[] x = [1, 2];
                        object[] y = [3];
                        F([..x]).Report(includeType: true);
                        F([..x, ..y]).Report(includeType: true);
                        F([..y, ..x]).Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Int32[]) [1, 2], (System.Object[]) [1, 2, 3], (System.Object[]) [3, 1, 2], ");
        }

        [Fact]
        public void TypeInference_Spread_02()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static void Main()
                    {
                        object[] x = [1, 2];
                        var y = F([..x, 3]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Object[]) [1, 2, 3], ");
        }

        [Fact]
        public void TypeInference_Spread_03()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static void Main()
                    {
                        int[] x = [1, 2];
                        var y = F([..x, (object)3]);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "(System.Object[]) [1, 2, 3], ");
        }

        // If we allow inference from a spread element _expression_ rather than simply
        // from the spread element _type_, and if we allow spread element expressions to
        // be collection expressions, then MethodTypeInferrer.MakeOutputTypeInferences
        // will probably need to make an output type inference from each element within
        // the nested spread element collection expression.
        [Fact]
        public void TypeInference_Spread_04()
        {
            string source = """
                using System;
                class Program
                {
                    static Func<T>[] F<T>(Func<T>[] arg) => arg;
                    static void Main()
                    {
                        var x = F([null, .. [() => 1]]);
                        x.Report();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics(
                // 0.cs(7,29): error CS9176: There is no target type for the collection expression.
                //         var x = F([null, .. [() => 1]]);
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[() => 1]").WithLocation(7, 29));
        }

        [Fact]
        public void TypeInference_Spread_05()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static void Main()
                    {
                        object x = new[] { 2, 3 };
                        var y = F([..x]);
                        var z = F([1, ..x]);
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics(
                // 0.cs(7,22): error CS1579: foreach statement cannot operate on variables of type 'object' because 'object' does not contain a public instance or extension definition for 'GetEnumerator'
                //         var y = F([..x]);
                Diagnostic(ErrorCode.ERR_ForEachMissingMember, "x").WithArguments("object", "GetEnumerator").WithLocation(7, 22),
                // 0.cs(8,25): error CS1579: foreach statement cannot operate on variables of type 'object' because 'object' does not contain a public instance or extension definition for 'GetEnumerator'
                //         var z = F([1, ..x]);
                Diagnostic(ErrorCode.ERR_ForEachMissingMember, "x").WithArguments("object", "GetEnumerator").WithLocation(8, 25));
        }

        [Fact]
        public void TypeInference_Spread_06()
        {
            string source = """
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static void Main()
                    {
                        dynamic x = new[] { 2, 3 };
                        var y = F([..x]);
                        y.Report(includeType: true);
                        var z = F([1, ..x]);
                        z.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                references: new[] { CSharpRef },
                expectedOutput: "(System.Object[]) [2, 3], (System.Object[]) [1, 2, 3], ");
        }

        [Fact]
        public void TypeInference_Spread_07()
        {
            string source = """
                #nullable enable
                class Program
                {
                    static void Main()
                    {
                        string[] a = [];
                        string?[] b = [];
                        object[] aa = [..a, ..a];
                        object[] ab = [..a, ..b]; // 1
                        object[] bb = [..b, ..b]; // 2
                    }
                }
                """;
            // https://github.com/dotnet/roslyn/issues/68786: Infer nullability from collection expressions in type inference.
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();
        }

        [Fact]
        public void TypeInference_Spread_08()
        {
            string source = """
                #nullable enable
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<string>[] a = [];
                        IEnumerable<string?>[] b = [];
                        IEnumerable<object>[] aa = [..a, ..a];
                        IEnumerable<object>[] ab = [..a, ..b]; // 1
                        IEnumerable<object>[] bb = [..b, ..b]; // 2
                    }
                }
                """;
            // https://github.com/dotnet/roslyn/issues/68786: Infer nullability from collection expressions in type inference.
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();
        }

        [Fact]
        public void TypeInference_Spread_09()
        {
            string source = """
                using System;
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static void Main()
                    {
                        dynamic[] x = new[] { "one", null };
                        string[] y = [..x];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                references: new[] { CSharpRef },
                expectedOutput: "(System.String[]) [one, null], ");
        }

        [Fact]
        public void TypeInference_Spread_10()
        {
            string source = """
                using System;
                class Program
                {
                    static T[] F<T>(T[] arg) => arg;
                    static void Main()
                    {
                        dynamic[] x = new[] { "one", null };
                        var y = F([..x]);
                        var z = F([..x, "three"]);
                        y.Report(includeType: true);
                        Console.Write("{0}, ", y[0].Length);
                        z.Report(includeType: true);
                        Console.Write("{0}, ", z[2].Length);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                references: new[] { CSharpRef },
                expectedOutput: "(System.Object[]) [one, null], 3, (System.Object[]) [one, null, three], 5, ");
        }

        [CombinatorialData]
        [Theory]
        public void Spread_RefEnumerable(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public class MyCollection<T>
                {
                    private readonly T[] _items;
                    public MyCollection(T[] items) { _items = items; }
                    public MyEnumerator<T> GetEnumerator() => new(_items);
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => new MyCollection<T>(items.ToArray());
                }
                public struct MyEnumerator<T>
                {
                    private readonly T[] _items;
                    private int _index;
                    public MyEnumerator(T[] items)
                    {
                        _items = items;
                        _index = -1;
                    }
                    public bool MoveNext()
                    {
                        if (_index < _items.Length) _index++;
                        return _index < _items.Length;
                    }
                    public ref T Current => ref _items[_index];
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB1 = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [1, 2, 3];
                        MyCollection<object> y = [..x, 4];
                        foreach (int i in y) Console.Write("{0}, ", i);
                    }
                }
                """;
            CompileAndVerify(
                sourceB1,
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("1, 2, 3, 4, "));

            string sourceB2 = """
                using System;
                class Program
                {
                    static MyCollection<T> F<T>(MyCollection<T> c)
                    {
                        return c;
                    }
                    static void Main()
                    {
                        MyCollection<int> x = F([1, 2, 3]);
                        foreach (int i in x) Console.Write("{0}, ", i);
                        MyCollection<int> y = F([..x]);
                        foreach (int i in y) Console.Write("{0}, ", i);
                    }
                }
                """;
            CompileAndVerify(
                sourceB2,
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("1, 2, 3, 1, 2, 3, "));
        }

        [Fact]
        public void TypeInference_NullableValueType()
        {
            string source = """
using System.Collections;
using System.Collections.Generic;

var a = Program.AsCollection([1, 2, 3]);
a.Report();

struct S<T> : IEnumerable<T>
{
    private List<T> _list;
    public void Add(T t)
    {
        _list ??= new List<T>();
        _list.Add(t);
    }
    public IEnumerator<T> GetEnumerator()
    {
        _list ??= new List<T>();
        return _list.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
static partial class Program
{
    static S<T>? AsCollection<T>(S<T>? args) => args;
}
""";
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[1, 2, 3],");

            var tree = comp.SyntaxTrees.First();
            var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
            Assert.Equal("Program.AsCollection([1, 2, 3])", invocation.ToFullString());

            var model = comp.GetSemanticModel(tree);
            Assert.Equal("S<System.Int32>? Program.AsCollection<System.Int32>(S<System.Int32>? args)",
                model.GetSymbolInfo(invocation).Symbol.ToTestDisplayString());
        }

        [Fact]
        public void TypeInference_NullableValueType_ExtensionMethod()
        {
            string source = """
using System.Collections;
using System.Collections.Generic;
struct S<T> : IEnumerable<T>
{
    private List<T> _list;
    public void Add(T t)
    {
        _list ??= new List<T>();
        _list.Add(t);
    }
    public IEnumerator<T> GetEnumerator()
    {
        _list ??= new List<T>();
        return _list.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
static class Program
{
    static S<T>? AsCollection<T>(this S<T>? args)
    {
        return args;
    }
    static void Main()
    {
        var a = AsCollection([1, 2, 3]);
        var b = [4].AsCollection();
    }
}
""";
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (27,17): error CS9176: There is no target type for the collection expression.
                //         var b = [4].AsCollection();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[4]").WithLocation(27, 17)
                );
        }

        [Fact]
        public void MemberAccess_01()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        [].GetHashCode();
                        []?.GetHashCode();
                        [][0].GetHashCode();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,9): error CS9176: There is no target type for the collection expression.
                //         [].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(5, 9),
                // (6,9): error CS9176: There is no target type for the collection expression.
                //         []?.GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(6, 9),
                // (7,9): error CS9176: There is no target type for the collection expression.
                //         [][0].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 9));
        }

        [Fact]
        public void MemberAccess_02()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        [1].GetHashCode();
                        [2]?.GetHashCode();
                        [3][0].GetHashCode();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,9): error CS9176: There is no target type for the collection expression.
                //         [1].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[1]").WithLocation(5, 9),
                // (6,9): error CS9176: There is no target type for the collection expression.
                //         [2]?.GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[2]").WithLocation(6, 9),
                // (7,9): error CS9176: There is no target type for the collection expression.
                //         [3][0].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[3]").WithLocation(7, 9));
        }

        [Fact]
        public void MemberAccess_03()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        _ = [].GetHashCode();
                        _ = []?.GetHashCode();
                        _ = [][0].GetHashCode();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,13): error CS9176: There is no target type for the collection expression.
                //         _ = [].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(5, 13),
                // (6,13): error CS9176: There is no target type for the collection expression.
                //         _ = []?.GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(6, 13),
                // (7,13): error CS9176: There is no target type for the collection expression.
                //         _ = [][0].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 13));
        }

        [Fact]
        public void MemberAccess_04()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        _ = [1].GetHashCode();
                        _ = [2]?.GetHashCode();
                        _ = [3][0].GetHashCode();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,13): error CS9176: There is no target type for the collection expression.
                //         _ = [1].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[1]").WithLocation(5, 13),
                // (6,13): error CS9176: There is no target type for the collection expression.
                //         _ = [2]?.GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[2]").WithLocation(6, 13),
                // (7,13): error CS9176: There is no target type for the collection expression.
                //         _ = [3][0].GetHashCode();
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[3]").WithLocation(7, 13));
        }

        [Fact]
        public void ListBase()
        {
            string sourceA = """
                using System.Collections;
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType { }
                    public class String { }
                    public class Type { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Int32 { }
                }
                namespace System.Collections
                {
                    public interface IEnumerable { }
                }
                namespace System.Collections.Generic
                {
                    public class ListBase<T> : IEnumerable
                    {
                        public void Add(string s) { }
                    }
                    public class List<T> : ListBase<T>
                    {
                        public void Add(T t) { }
                    }
                }
                """;
            string sourceB = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        ListBase<int> x = [];
                        ListBase<int> y = [1, 2];
                        ListBase<string> z = ["a", "b"];
                    }
                }
                """;
            var comp = CreateEmptyCompilation(new[] { sourceA, sourceB }, parseOptions: TestOptions.RegularPreview.WithNoRefSafetyRulesAttribute());
            comp.VerifyEmitDiagnostics(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // 1.cs(7,28): error CS1950: The best overloaded Add method 'ListBase<int>.Add(string)' for the collection initializer has some invalid arguments
                //         ListBase<int> y = [1, 2];
                Diagnostic(ErrorCode.ERR_BadArgTypesForCollectionAdd, "1").WithArguments("System.Collections.Generic.ListBase<int>.Add(string)").WithLocation(7, 28),
                // 1.cs(7,28): error CS1503: Argument 1: cannot convert from 'int' to 'string'
                //         ListBase<int> y = [1, 2];
                Diagnostic(ErrorCode.ERR_BadArgType, "1").WithArguments("1", "int", "string").WithLocation(7, 28),
                // 1.cs(7,31): error CS1950: The best overloaded Add method 'ListBase<int>.Add(string)' for the collection initializer has some invalid arguments
                //         ListBase<int> y = [1, 2];
                Diagnostic(ErrorCode.ERR_BadArgTypesForCollectionAdd, "2").WithArguments("System.Collections.Generic.ListBase<int>.Add(string)").WithLocation(7, 31),
                // 1.cs(7,31): error CS1503: Argument 1: cannot convert from 'int' to 'string'
                //         ListBase<int> y = [1, 2];
                Diagnostic(ErrorCode.ERR_BadArgType, "2").WithArguments("1", "int", "string").WithLocation(7, 31));
        }

        [WorkItem("https://github.com/dotnet/roslyn/issues/69839")]
        [Fact]
        public void ListInterfaces_01()
        {
            string sourceA = """
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType { }
                    public class String { }
                    public class Type { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Int32 { }
                }
                namespace System.Collections
                {
                    public interface IEnumerable { }
                }
                namespace System.Collections.Generic
                {
                    public interface IA { }
                    public interface IB<T> { }
                    public interface IC<T> { }
                    public interface ID<T1, T2> { }
                    public class List<T> : IEnumerable, IA, IB<T>, IC<object>, ID<T, object>
                    {
                        public void Add(T t) { }
                    }
                }
                """;
            string sourceB = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        List<int> l = [1];
                        IA a = [2];
                        IB<object> b = [3];
                        IC<object> c = [4];
                        ID<object, object> d = [5];
                    }
                }
                """;
            var comp = CreateEmptyCompilation(new[] { sourceA, sourceB }, parseOptions: TestOptions.RegularPreview.WithNoRefSafetyRulesAttribute());
            comp.VerifyEmitDiagnostics(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // 1.cs(6,23): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         List<int> l = [1];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[1]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(6, 23),
                // 1.cs(7,16): error CS9174: Cannot initialize type 'IA' with a collection expression because the type is not constructible.
                //         IA a = [2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[2]").WithArguments("System.Collections.Generic.IA").WithLocation(7, 16),
                // 1.cs(8,24): error CS9174: Cannot initialize type 'IB<object>' with a collection expression because the type is not constructible.
                //         IB<object> b = [3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[3]").WithArguments("System.Collections.Generic.IB<object>").WithLocation(8, 24),
                // 1.cs(9,24): error CS9174: Cannot initialize type 'IC<object>' with a collection expression because the type is not constructible.
                //         IC<object> c = [4];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[4]").WithArguments("System.Collections.Generic.IC<object>").WithLocation(9, 24),
                // 1.cs(10,32): error CS9174: Cannot initialize type 'ID<object, object>' with a collection expression because the type is not constructible.
                //         ID<object, object> d = [5];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[5]").WithArguments("System.Collections.Generic.ID<object, object>").WithLocation(10, 32));
        }

        [Fact]
        public void ListInterfaces_02()
        {
            string sourceA = """
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType { }
                    public class String { }
                    public class Type { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Int32 { }
                    public interface IEquatable<T>
                    {
                        bool Equals(T other);
                    }
                }
                namespace System.Collections
                {
                    public interface IEnumerable { }
                }
                namespace System.Collections.Generic
                {
                    public class List<T> : IEnumerable, IEquatable<List<T>>
                    {
                        public bool Equals(List<T> other) => false;
                        public void Add(T t) { }
                    }
                }
                """;
            string sourceB = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        List<int> l = [1];
                        IEquatable<int> e = [2];
                    }
                }
                """;
            var comp = CreateEmptyCompilation(new[] { sourceA, sourceB }, parseOptions: TestOptions.RegularPreview.WithNoRefSafetyRulesAttribute());
            comp.VerifyEmitDiagnostics(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // 1.cs(7,23): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         List<int> l = [1];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[1]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(7, 23),
                // 1.cs(8,29): error CS9174: Cannot initialize type 'IEquatable<int>' with a collection expression because the type is not constructible.
                //         IEquatable<int> e = [2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[2]").WithArguments("System.IEquatable<int>").WithLocation(8, 29));
        }

        [Fact]
        public void ListInterfaces_NoInterfaces()
        {
            string sourceA = """
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType { }
                    public class String { }
                    public class Type { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Int32 { }
                }
                namespace System.Collections.Generic
                {
                    public interface IEnumerable<T> { }
                    public class List<T>
                    {
                        public List(int capacity) { }
                        public void Add(T t) { }
                    }
                }
                """;
            string sourceB = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        List<int> l = [1];
                        IEnumerable<int> e = [2];
                    }
                }
                """;
            var comp = CreateEmptyCompilation(new[] { sourceA, sourceB }, parseOptions: TestOptions.RegularPreview.WithNoRefSafetyRulesAttribute());
            comp.VerifyEmitDiagnostics(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // 1.cs(7,23): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         List<int> l = [1];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[1]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(7, 23),
                // 1.cs(8,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         IEnumerable<int> e = [2];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[2]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(8, 30),
                // 1.cs(8,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         IEnumerable<int> e = [2];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[2]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(8, 30));
        }

        [Theory]
        [InlineData("IEnumerable<int>")]
        [InlineData("IReadOnlyCollection<object>")]
        [InlineData("IReadOnlyList<int>")]
        [InlineData("ICollection<object>")]
        [InlineData("IList<int>")]
        public void ListInterfaces_MissingList(string collectionType)
        {
            string source = $$"""
                using System.Collections.Generic;
                class Program
                {
                    static void F(IEnumerable<int> e)
                    {
                        {{collectionType}} c;
                        c = [];
                        c = [..e];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeTypeMissing(WellKnownType.System_Collections_Generic_List_T);
            comp.VerifyEmitDiagnostics(
                // (7,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         c = [];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(7, 13),
                // (7,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         c = [];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(7, 13),
                // (7,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         c = [];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(7, 13),
                // (7,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         c = [];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(7, 13),
                // (8,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         c = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(8, 13),
                // (8,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         c = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(8, 13),
                // (8,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         c = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(8, 13),
                // (8,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         c = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(8, 13));
        }

        [Fact]
        public void Array_01()
        {
            string source = """
                class Program
                {
                    static int[] Create1() => [];
                    static object[] Create2() => [1, 2];
                    static int[] Create3() => [3, 4, 5];
                    static long?[] Create4() => [null, 7];
                    static void Main()
                    {
                        Create1().Report();
                        Create2().Report();
                        Create3().Report();
                        Create4().Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [1, 2], [3, 4, 5], [null, 7], ");
            verifier.VerifyIL("Program.Create1", """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  call       "int[] System.Array.Empty<int>()"
                  IL_0005:  ret
                }
                """);
            verifier.VerifyIL("Program.Create2", """
                {
                  // Code size       25 (0x19)
                  .maxstack  4
                  IL_0000:  ldc.i4.2
                  IL_0001:  newarr     "object"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.0
                  IL_0008:  ldc.i4.1
                  IL_0009:  box        "int"
                  IL_000e:  stelem.ref
                  IL_000f:  dup
                  IL_0010:  ldc.i4.1
                  IL_0011:  ldc.i4.2
                  IL_0012:  box        "int"
                  IL_0017:  stelem.ref
                  IL_0018:  ret
                }
                """);
            verifier.VerifyIL("Program.Create3", """
                {
                  // Code size       18 (0x12)
                  .maxstack  3
                  IL_0000:  ldc.i4.3
                  IL_0001:  newarr     "int"
                  IL_0006:  dup
                  IL_0007:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12 <PrivateImplementationDetails>.CE99AE045C8B2A2A8A58FD1A2120956E74E90322EEF45F7DFE1CA73EEFE655D4"
                  IL_000c:  call       "void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)"
                  IL_0011:  ret
                }
                """);
            verifier.VerifyIL("Program.Create4", """
                {
                  // Code size       21 (0x15)
                  .maxstack  4
                  IL_0000:  ldc.i4.2
                  IL_0001:  newarr     "long?"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.1
                  IL_0008:  ldc.i4.7
                  IL_0009:  conv.i8
                  IL_000a:  newobj     "long?..ctor(long)"
                  IL_000f:  stelem     "long?"
                  IL_0014:  ret
                }
                """);
        }

        [Fact]
        public void Array_02()
        {
            string source = """
                using System;
                class Program
                {
                    static int[][] Create1() => [];
                    static object[][] Create2() => [[]];
                    static object[][] Create3() => [[1], [2, 3]];
                    static void Main()
                    {
                        Report(Create1());
                        Report(Create2());
                        Report(Create3());
                    }
                    static void Report<T>(T[][] a)
                    {
                        Console.Write("Length={0}, ", a.Length);
                        foreach (var x in a)
                        {
                            Console.Write("Length={0}, ", x.Length);
                            foreach (var y in x)
                                Console.Write("{0}, ", y);
                        }
                        Console.WriteLine();
                    }
                }
                """;
            var verifier = CompileAndVerify(source, expectedOutput: """
                Length=0, 
                Length=1, Length=0, 
                Length=2, Length=1, 1, Length=2, 2, 3, 
                """);
            verifier.VerifyIL("Program.Create1", """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  call       "int[][] System.Array.Empty<int[]>()"
                  IL_0005:  ret
                }
                """);
            verifier.VerifyIL("Program.Create2", """
                {
                  // Code size       15 (0xf)
                  .maxstack  4
                  IL_0000:  ldc.i4.1
                  IL_0001:  newarr     "object[]"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.0
                  IL_0008:  call       "object[] System.Array.Empty<object>()"
                  IL_000d:  stelem.ref
                  IL_000e:  ret
                }
                """);
            verifier.VerifyIL("Program.Create3", """
                {
                  // Code size       52 (0x34)
                  .maxstack  7
                  IL_0000:  ldc.i4.2
                  IL_0001:  newarr     "object[]"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.0
                  IL_0008:  ldc.i4.1
                  IL_0009:  newarr     "object"
                  IL_000e:  dup
                  IL_000f:  ldc.i4.0
                  IL_0010:  ldc.i4.1
                  IL_0011:  box        "int"
                  IL_0016:  stelem.ref
                  IL_0017:  stelem.ref
                  IL_0018:  dup
                  IL_0019:  ldc.i4.1
                  IL_001a:  ldc.i4.2
                  IL_001b:  newarr     "object"
                  IL_0020:  dup
                  IL_0021:  ldc.i4.0
                  IL_0022:  ldc.i4.2
                  IL_0023:  box        "int"
                  IL_0028:  stelem.ref
                  IL_0029:  dup
                  IL_002a:  ldc.i4.1
                  IL_002b:  ldc.i4.3
                  IL_002c:  box        "int"
                  IL_0031:  stelem.ref
                  IL_0032:  stelem.ref
                  IL_0033:  ret
                }
                """);
        }

        [Fact]
        public void Array_03()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object o;
                        o = (int[])[];
                        o.Report();
                        o = (long?[])[null, 2];
                        o.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [null, 2], ");
        }

        [Fact]
        public void Array_04()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object[,] x = [];
                        int[,] y = [null, 2];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,23): error CS9174: Cannot initialize type 'object[*,*]' with a collection expression because the type is not constructible.
                //         object[,] x = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("object[*,*]").WithLocation(5, 23),
                // (6,20): error CS9174: Cannot initialize type 'int[*,*]' with a collection expression because the type is not constructible.
                //         int[,] y = [null, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[null, 2]").WithArguments("int[*,*]").WithLocation(6, 20));
        }

        [Fact]
        public void Array_05()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        int[,] z = [[1, 2], [3, 4]];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,20): error CS9174: Cannot initialize type 'int[*,*]' with a collection expression because the type is not constructible.
                //         int[,] z = [[1, 2], [3, 4]];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[[1, 2], [3, 4]]").WithArguments("int[*,*]").WithLocation(5, 20));
        }

        [Theory]
        [CombinatorialData]
        public void Span_01(bool useReadOnlySpan)
        {
            string spanType = useReadOnlySpan ? "ReadOnlySpan" : "Span";
            string source = $$"""
                using System;
                class Program
                {
                    static void Create1() { {{spanType}}<int> s = []; s.Report(); }
                    static void Create2() { {{spanType}}<object> s = [1, 2]; s.Report(); }
                    static void Create3() { {{spanType}}<int> s = [3, 4, 5]; s.Report(); }
                    static void Create4() { {{spanType}}<long?> s = [null, 7]; s.Report(); }
                    static void Main()
                    {
                        Create1();
                        Create2();
                        Create3();
                        Create4();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net70,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[], [1, 2], [3, 4, 5], [null, 7], "));
            verifier.VerifyIL("Program.Create1", $$"""
                {
                  // Code size       20 (0x14)
                  .maxstack  2
                  .locals init (System.{{spanType}}<int> V_0) //s
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  call       "int[] System.Array.Empty<int>()"
                  IL_0007:  call       "System.{{spanType}}<int>..ctor(int[])"
                  IL_000c:  ldloca.s   V_0
                  IL_000e:  call       "void CollectionExtensions.Report<int>(in System.{{spanType}}<int>)"
                  IL_0013:  ret
                }
                """);
            verifier.VerifyIL("Program.Create2", $$"""
                {
                  // Code size       39 (0x27)
                  .maxstack  5
                  .locals init (System.{{spanType}}<object> V_0) //s
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  ldc.i4.2
                  IL_0003:  newarr     "object"
                  IL_0008:  dup
                  IL_0009:  ldc.i4.0
                  IL_000a:  ldc.i4.1
                  IL_000b:  box        "int"
                  IL_0010:  stelem.ref
                  IL_0011:  dup
                  IL_0012:  ldc.i4.1
                  IL_0013:  ldc.i4.2
                  IL_0014:  box        "int"
                  IL_0019:  stelem.ref
                  IL_001a:  call       "System.{{spanType}}<object>..ctor(object[])"
                  IL_001f:  ldloca.s   V_0
                  IL_0021:  call       "void CollectionExtensions.Report<object>(in System.{{spanType}}<object>)"
                  IL_0026:  ret
                }
                """);
            if (useReadOnlySpan)
            {
                verifier.VerifyIL("Program.Create3", """
                    {
                      // Code size       19 (0x13)
                      .maxstack  1
                      .locals init (System.ReadOnlySpan<int> V_0) //s
                      IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12_Align=4 <PrivateImplementationDetails>.CE99AE045C8B2A2A8A58FD1A2120956E74E90322EEF45F7DFE1CA73EEFE655D44"
                      IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
                      IL_000a:  stloc.0
                      IL_000b:  ldloca.s   V_0
                      IL_000d:  call       "void CollectionExtensions.Report<int>(in System.ReadOnlySpan<int>)"
                      IL_0012:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.Create3", """
                    {
                      // Code size       32 (0x20)
                      .maxstack  4
                      .locals init (System.Span<int> V_0) //s
                      IL_0000:  ldloca.s   V_0
                      IL_0002:  ldc.i4.3
                      IL_0003:  newarr     "int"
                      IL_0008:  dup
                      IL_0009:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12 <PrivateImplementationDetails>.CE99AE045C8B2A2A8A58FD1A2120956E74E90322EEF45F7DFE1CA73EEFE655D4"
                      IL_000e:  call       "void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)"
                      IL_0013:  call       "System.Span<int>..ctor(int[])"
                      IL_0018:  ldloca.s   V_0
                      IL_001a:  call       "void CollectionExtensions.Report<int>(in System.Span<int>)"
                      IL_001f:  ret
                    }
                    """);
            }
            verifier.VerifyIL("Program.Create4", $$"""
                {
                  // Code size       35 (0x23)
                  .maxstack  5
                  .locals init (System.{{spanType}}<long?> V_0) //s
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  ldc.i4.2
                  IL_0003:  newarr     "long?"
                  IL_0008:  dup
                  IL_0009:  ldc.i4.1
                  IL_000a:  ldc.i4.7
                  IL_000b:  conv.i8
                  IL_000c:  newobj     "long?..ctor(long)"
                  IL_0011:  stelem     "long?"
                  IL_0016:  call       "System.{{spanType}}<long?>..ctor(long?[])"
                  IL_001b:  ldloca.s   V_0
                  IL_001d:  call       "void CollectionExtensions.Report<long?>(in System.{{spanType}}<long?>)"
                  IL_0022:  ret
                }
                """);
        }

        [Theory]
        [CombinatorialData]
        public void Span_02(bool useReadOnlySpan)
        {
            string spanType = useReadOnlySpan ? "ReadOnlySpan" : "Span";
            string source = $$"""
                using System;
                class Program
                {
                    static void Main()
                    {
                        {{spanType}}<string> x = [];
                        {{spanType}}<int> y = [1, 2, 3];
                        x.Report();
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net70,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [Theory]
        [CombinatorialData]
        public void Span_03(bool useReadOnlySpan)
        {
            string spanType = useReadOnlySpan ? "ReadOnlySpan" : "Span";
            string source = $$"""
                using System;
                class Program
                {
                    static void Main()
                    {
                        var x = ({{spanType}}<string>)[];
                        var y = ({{spanType}}<int>)[1, 2, 3];
                        x.Report();
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net70,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [Theory]
        [CombinatorialData]
        public void Span_04(bool useReadOnlySpan)
        {
            string spanType = useReadOnlySpan ? "ReadOnlySpan" : "Span";
            string source = $$"""
                using System;
                class Program
                {
                    static ref readonly {{spanType}}<int> F1()
                    {
                        return ref F2<int>([]);
                    }
                    static ref readonly {{spanType}}<T> F2<T>(in {{spanType}}<T> s)
                    {
                        return ref s;
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net70);
            comp.VerifyEmitDiagnostics(
                // (6,20): error CS8347: Cannot use a result of 'Program.F2<int>(in Span<int>)' in this context because it may expose variables referenced by parameter 's' outside of their declaration scope
                //         return ref F2<int>([]);
                Diagnostic(ErrorCode.ERR_EscapeCall, "F2<int>([])").WithArguments($"Program.F2<int>(in System.{spanType}<int>)", "s").WithLocation(6, 20),
                // (6,28): error CS8156: An expression cannot be used in this context because it may not be passed or returned by reference
                //         return ref F2<int>([]);
                Diagnostic(ErrorCode.ERR_RefReturnLvalueExpected, "[]").WithLocation(6, 28));
        }

        [Theory]
        [CombinatorialData]
        public void Span_05(bool useReadOnlySpan)
        {
            string spanType = useReadOnlySpan ? "ReadOnlySpan" : "Span";
            string source = $$"""
                using System;
                class Program
                {
                    static ref readonly {{spanType}}<int> F1()
                    {
                        return ref F2<int>([]);
                    }
                    static ref readonly {{spanType}}<T> F2<T>(scoped in {{spanType}}<T> s)
                    {
                        throw null;
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net70);
            comp.VerifyEmitDiagnostics();
        }

        [Fact]
        public void Span_MissingConstructor()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        Span<string> x = [];
                        ReadOnlySpan<int> y = [1, 2, 3];
                    }
                }
                """;

            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net70);
            comp.MakeMemberMissing(WellKnownMember.System_Span_T__ctor_Array);
            comp.VerifyEmitDiagnostics(
                // (6,26): error CS0656: Missing compiler required member 'System.Span`1..ctor'
                //         Span<string> x = [];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[]").WithArguments("System.Span`1", ".ctor").WithLocation(6, 26));

            comp = CreateCompilation(source, targetFramework: TargetFramework.Net70);
            comp.MakeMemberMissing(WellKnownMember.System_ReadOnlySpan_T__ctor_Array);
            comp.VerifyEmitDiagnostics(
                // (7,31): error CS0656: Missing compiler required member 'System.ReadOnlySpan`1..ctor'
                //         ReadOnlySpan<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[1, 2, 3]").WithArguments("System.ReadOnlySpan`1", ".ctor").WithLocation(7, 31));
        }

        [Fact]
        public void InterfaceType()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Runtime.InteropServices;

                [ComImport]
                [Guid("1FC6664D-C61E-4131-81CD-A3EE0DD6098F")]
                [CoClass(typeof(C))]
                interface I : IEnumerable
                {
                    void Add(int i);
                }

                class C : I
                {
                    IEnumerator IEnumerable.GetEnumerator() => null;
                    void I.Add(int i) { }
                }

                class Program
                {
                    static void Main()
                    {
                        I i;
                        i = new() { };
                        i = new() { 1, 2 };
                        i = [];
                        i = [3, 4];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (26,13): error CS9174: Cannot initialize type 'I' with a collection expression because the type is not constructible.
                //         i = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("I").WithLocation(26, 13),
                // (27,13): error CS9174: Cannot initialize type 'I' with a collection expression because the type is not constructible.
                //         i = [3, 4];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[3, 4]").WithArguments("I").WithLocation(27, 13));
        }

        [Fact]
        public void EnumType_01()
        {
            string source = """
                enum E { }
                class Program
                {
                    static void Main()
                    {
                        E e;
                        e = [];
                        e = [1, 2];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (7,13): error CS9174: Cannot initialize type 'E' with a collection expression because the type is not constructible.
                //         e = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("E").WithLocation(7, 13),
                // (8,13): error CS9174: Cannot initialize type 'E' with a collection expression because the type is not constructible.
                //         e = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("E").WithLocation(8, 13));
        }

        [Fact]
        public void EnumType_02()
        {
            string sourceA = """
                using System.Collections;
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType { }
                    public class String { }
                    public class Type { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Int32 { }
                    public struct Enum : IEnumerable { }
                }
                namespace System.Collections
                {
                    public interface IEnumerable { }
                }
                namespace System.Collections.Generic
                {
                    public class List<T> : IEnumerable { }
                }
                """;
            string sourceB = """
                enum E { }
                class Program
                {
                    static void Main()
                    {
                        E e;
                        e = [];
                        e = [1, 2];
                    }
                }
                """;
            var comp = CreateEmptyCompilation(new[] { sourceA, sourceB }, parseOptions: TestOptions.RegularPreview.WithNoRefSafetyRulesAttribute());
            // ConversionsBase.GetConstructibleCollectionType() ignores whether the enum
            // implements IEnumerable, so the type is not considered constructible.
            comp.VerifyEmitDiagnostics(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // 1.cs(7,13): error CS9174: Cannot initialize type 'E' with a collection expression because the type is not constructible.
                //         e = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("E").WithLocation(7, 13),
                // 1.cs(8,13): error CS9174: Cannot initialize type 'E' with a collection expression because the type is not constructible.
                //         e = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("E").WithLocation(8, 13));
        }

        [Fact]
        public void DelegateType_01()
        {
            string source = """
                delegate void D();
                class Program
                {
                    static void Main()
                    {
                        D d;
                        d = [];
                        d = [1, 2];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (7,13): error CS9174: Cannot initialize type 'D' with a collection expression because the type is not constructible.
                //         d = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("D").WithLocation(7, 13),
                // (8,13): error CS9174: Cannot initialize type 'D' with a collection expression because the type is not constructible.
                //         d = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("D").WithLocation(8, 13));
        }

        [Fact]
        public void DelegateType_02()
        {
            string sourceA = """
                using System.Collections;
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType { }
                    public class String { }
                    public class Type { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Int32 { }
                    public struct IntPtr { }
                    public abstract class Delegate : IEnumerable { }
                    public abstract class MulticastDelegate : Delegate { }
                }
                namespace System.Collections
                {
                    public interface IEnumerable { }
                }
                namespace System.Collections.Generic
                {
                    public class List<T> : IEnumerable { }
                }
                """;
            string sourceB = """
                delegate void D();
                class Program
                {
                    static void Main()
                    {
                        D d;
                        d = [];
                        d = [1, 2];
                    }
                }
                """;
            var comp = CreateEmptyCompilation(new[] { sourceA, sourceB }, parseOptions: TestOptions.RegularPreview.WithNoRefSafetyRulesAttribute());
            // ConversionsBase.GetConstructibleCollectionType() ignores whether the delegate
            // implements IEnumerable, so the type is not considered constructible.
            comp.VerifyEmitDiagnostics(
                // warning CS8021: No value for RuntimeMetadataVersion found. No assembly containing System.Object was found nor was a value for RuntimeMetadataVersion specified through options.
                Diagnostic(ErrorCode.WRN_NoRuntimeMetadataVersion).WithLocation(1, 1),
                // 1.cs(7,13): error CS9174: Cannot initialize type 'D' with a collection expression because the type is not constructible.
                //         d = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("D").WithLocation(7, 13),
                // 1.cs(8,13): error CS9174: Cannot initialize type 'D' with a collection expression because the type is not constructible.
                //         d = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("D").WithLocation(8, 13));
        }

        [Fact]
        public void PointerType_01()
        {
            string source = """
                class Program
                {
                    unsafe static void Main()
                    {
                        int* x = [];
                        int* y = [1, 2];
                        var z = (int*)[3];
                    }
                }
                """;
            var comp = CreateCompilation(source, options: TestOptions.UnsafeReleaseExe);
            comp.VerifyEmitDiagnostics(
                // (5,18): error CS9174: Cannot initialize type 'int*' with a collection expression because the type is not constructible.
                //         int* x = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("int*").WithLocation(5, 18),
                // (6,18): error CS9174: Cannot initialize type 'int*' with a collection expression because the type is not constructible.
                //         int* y = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("int*").WithLocation(6, 18),
                // (7,17): error CS9174: Cannot initialize type 'int*' with a collection expression because the type is not constructible.
                //         var z = (int*)[3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "(int*)[3]").WithArguments("int*").WithLocation(7, 17));
        }

        [Fact]
        public void PointerType_02()
        {
            string source = """
                class Program
                {
                    unsafe static void Main()
                    {
                        delegate*<void> x = [];
                        delegate*<void> y = [1, 2];
                        var z = (delegate*<void>)[3];
                    }
                }
                """;
            var comp = CreateCompilation(source, options: TestOptions.UnsafeReleaseExe);
            comp.VerifyEmitDiagnostics(
                // (5,29): error CS9174: Cannot initialize type 'delegate*<void>' with a collection expression because the type is not constructible.
                //         delegate*<void> x = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("delegate*<void>").WithLocation(5, 29),
                // (6,29): error CS9174: Cannot initialize type 'delegate*<void>' with a collection expression because the type is not constructible.
                //         delegate*<void> y = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("delegate*<void>").WithLocation(6, 29),
                // (7,17): error CS9174: Cannot initialize type 'delegate*<void>' with a collection expression because the type is not constructible.
                //         var z = (delegate*<void>)[3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "(delegate*<void>)[3]").WithArguments("delegate*<void>").WithLocation(7, 17));
        }

        [Fact]
        public void PointerType_03()
        {
            string source = """
                class Program
                {
                    unsafe static void Main()
                    {
                        void* p = null;
                        delegate*<void> d = null;
                        var x = [p];
                        var y = [d];
                    }
                }
                """;
            var comp = CreateCompilation(source, options: TestOptions.UnsafeReleaseExe);
            comp.VerifyEmitDiagnostics(
                // (7,17): error CS9176: There is no target type for the collection expression.
                //         var x = [p];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[p]").WithLocation(7, 17),
                // (8,17): error CS9176: There is no target type for the collection expression.
                //         var y = [d];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[d]").WithLocation(8, 17));
        }

        [Fact]
        public void PointerType_04()
        {
            string source = """
                using System;
                class Program
                {
                    unsafe static void Main()
                    {
                        void*[] a = [null, (void*)2];
                        foreach (void* p in a)
                            Console.Write("{0}, ", (nint)p);
                    }
                }
                """;
            CompileAndVerify(source, options: TestOptions.UnsafeReleaseExe, verify: Verification.Skipped, expectedOutput: "0, 2, ");
        }

        [Fact]
        public void PointerType_05()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C : IEnumerable
                {
                    private List<nint> _list = new List<nint>();
                    unsafe public void Add(void* p) { _list.Add((nint)p); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    unsafe static void Main()
                    {
                        void* p = (void*)2;
                        C c = [null, p];
                        c.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, options: TestOptions.UnsafeReleaseExe, verify: Verification.Skipped, expectedOutput: "[0, 2], ");
        }

        [Fact]
        public void CollectionInitializerType_01()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static List<int> Create1() => [];
                    static List<object> Create2() => [1, 2];
                    static List<int> Create3() => [3, 4, 5];
                    static List<long?> Create4() => [null, 7];
                    static void Main()
                    {
                        Create1().Report();
                        Create2().Report();
                        Create3().Report();
                        Create4().Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [1, 2], [3, 4, 5], [null, 7], ");
            verifier.VerifyIL("Program.Create1", """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  newobj     "System.Collections.Generic.List<int>..ctor()"
                  IL_0005:  ret
                }
                """);
            verifier.VerifyIL("Program.Create2", """
                {
                  // Code size       31 (0x1f)
                  .maxstack  3
                  IL_0000:  ldc.i4.2
                  IL_0001:  newobj     "System.Collections.Generic.List<object>..ctor(int)"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.1
                  IL_0008:  box        "int"
                  IL_000d:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_0012:  dup
                  IL_0013:  ldc.i4.2
                  IL_0014:  box        "int"
                  IL_0019:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_001e:  ret
                }
                """);
            verifier.VerifyIL("Program.Create3", """
                {
                  // Code size       28 (0x1c)
                  .maxstack  3
                  IL_0000:  ldc.i4.3
                  IL_0001:  newobj     "System.Collections.Generic.List<int>..ctor(int)"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.3
                  IL_0008:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_000d:  dup
                  IL_000e:  ldc.i4.4
                  IL_000f:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_0014:  dup
                  IL_0015:  ldc.i4.5
                  IL_0016:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_001b:  ret
                }
                """);
            verifier.VerifyIL("Program.Create4", """
                {
                  // Code size       35 (0x23)
                  .maxstack  3
                  .locals init (long? V_0)
                  IL_0000:  ldc.i4.2
                  IL_0001:  newobj     "System.Collections.Generic.List<long?>..ctor(int)"
                  IL_0006:  dup
                  IL_0007:  ldloca.s   V_0
                  IL_0009:  initobj    "long?"
                  IL_000f:  ldloc.0
                  IL_0010:  callvirt   "void System.Collections.Generic.List<long?>.Add(long?)"
                  IL_0015:  dup
                  IL_0016:  ldc.i4.7
                  IL_0017:  conv.i8
                  IL_0018:  newobj     "long?..ctor(long)"
                  IL_001d:  callvirt   "void System.Collections.Generic.List<long?>.Add(long?)"
                  IL_0022:  ret
                }
                """);
        }

        [Fact]
        public void CollectionInitializerType_02()
        {
            string source = """
                S s;
                s = [];
                s = [1, 2];
                s = [default];
                s = [Unknown];
                struct S { }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (2,5): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                // s = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("S").WithLocation(2, 5),
                // (3,5): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                // s = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("S").WithLocation(3, 5),
                // (4,5): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                // s = [default];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[default]").WithArguments("S").WithLocation(4, 5),
                // (5,6): error CS0103: The name 'Unknown' does not exist in the current context
                // s = [Unknown];
                Diagnostic(ErrorCode.ERR_NameNotInContext, "Unknown").WithArguments("Unknown").WithLocation(5, 6));
        }

        [Fact]
        public void CollectionInitializerType_03()
        {
            string source = """
                using System.Collections;
                S s;
                s = [];
                struct S : IEnumerable
                {
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                """;
            CompileAndVerify(source, expectedOutput: "");

            source = """
                using System.Collections;
                S s;
                s = [1, 2];
                struct S : IEnumerable
                {
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (3,6): error CS1061: 'S' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'S' could be found (are you missing a using directive or an assembly reference?)
                // s = [1, 2];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "1").WithArguments("S", "Add").WithLocation(3, 6),
                // (3,9): error CS1061: 'S' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'S' could be found (are you missing a using directive or an assembly reference?)
                // s = [1, 2];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "2").WithArguments("S", "Add").WithLocation(3, 9));

            source = """
                using System.Collections;
                S s;
                s = [.. new object()];
                struct S : IEnumerable
                {
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                """;
            comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (3,9): error CS1579: foreach statement cannot operate on variables of type 'object' because 'object' does not contain a public instance or extension definition for 'GetEnumerator'
                // s = [.. new object()];
                Diagnostic(ErrorCode.ERR_ForEachMissingMember, "new object()").WithArguments("object", "GetEnumerator").WithLocation(3, 9));
        }

        [Fact]
        public void CollectionInitializerType_04()
        {
            string source = """
                using System.Collections;
                C c;
                c = [];
                c = [1, 2];
                class C : IEnumerable
                {
                    C(object o) { }
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                    public void Add(int i) { }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (3,5): error CS1729: 'C' does not contain a constructor that takes 0 arguments
                // c = [];
                Diagnostic(ErrorCode.ERR_BadCtorArgCount, "[]").WithArguments("C", "0").WithLocation(3, 5),
                // (4,5): error CS1729: 'C' does not contain a constructor that takes 0 arguments
                // c = [1, 2];
                Diagnostic(ErrorCode.ERR_BadCtorArgCount, "[1, 2]").WithArguments("C", "0").WithLocation(4, 5));
        }

        [Fact]
        public void CollectionInitializerType_05()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class A : IEnumerable<int>
                {
                    A() { }
                    public void Add(int i) { }
                    IEnumerator<int> IEnumerable<int>.GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => null;
                    static A Create1() => [];
                }
                class B
                {
                    static A Create2() => [1, 2];
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (13,27): error CS0122: 'A.A()' is inaccessible due to its protection level
                //     static A Create2() => [1, 2];
                Diagnostic(ErrorCode.ERR_BadAccess, "[1, 2]").WithArguments("A.A()").WithLocation(13, 27));
        }

        [Fact]
        public void CollectionInitializerType_06()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C<T> : IEnumerable
                {
                    private List<T> _list = new List<T>();
                    public void Add(T t) { _list.Add(t); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C<int> c;
                        object o;
                        c = [];
                        o = (C<object>)[];
                        c.Report();
                        o.Report();
                        c = [1, 2];
                        o = (C<object>)[3, 4];
                        c.Report();
                        o.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [], [1, 2], [3, 4], ");
        }

        [Fact]
        public void CollectionInitializerType_ConstructorOptionalParameters()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C : IEnumerable<int>
                {
                    private List<int> _list = new List<int>();
                    internal C(int x = 1, int y = 2) { }
                    public void Add(int i) { _list.Add(i); }
                    IEnumerator<int> IEnumerable<int>.GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C c;
                        object o;
                        c = [];
                        o = (C)([]);
                        c.Report();
                        o.Report();
                        c = [1, 2];
                        o = (C)([3, 4]);
                        c.Report();
                        o.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [], [1, 2], [3, 4], ");
        }

        [Fact]
        public void CollectionInitializerType_ConstructorParamsArray()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C : IEnumerable<int>
                {
                    private List<int> _list = new List<int>();
                    internal C(params int[] args) { }
                    public void Add(int i) { _list.Add(i); }
                    IEnumerator<int> IEnumerable<int>.GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C c;
                        object o;
                        c = [];
                        o = (C)([]);
                        c.Report();
                        o.Report();
                        c = [1, 2];
                        o = (C)([3, 4]);
                        c.Report();
                        o.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [], [1, 2], [3, 4], ");
        }

        [Fact]
        public void CollectionInitializerType_07()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                abstract class A : IEnumerable<int>
                {
                    public void Add(int i) { }
                    IEnumerator<int> IEnumerable<int>.GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => null;
                }
                class B : A { }
                class Program
                {
                    static void Main()
                    {
                        A a = [];
                        B b = [];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (14,15): error CS0144: Cannot create an instance of the abstract type or interface 'A'
                //         A a = [];
                Diagnostic(ErrorCode.ERR_NoNewAbstract, "[]").WithArguments("A").WithLocation(14, 15));
        }

        [Fact]
        public void CollectionInitializerType_08()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                struct S0<T> : IEnumerable
                {
                    public void Add(T t) { }
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                struct S1<T> : IEnumerable<T>
                {
                    public void Add(T t) { }
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw null;
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                struct S2<T> : IEnumerable<T>
                {
                    public S2() { }
                    public void Add(T t) { }
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw null;
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                class Program
                {
                    static void M0()
                    {
                        object o = (S0<int>)[];
                        S0<int> s = [1, 2];
                    }
                    static void M1()
                    {
                        object o = (S1<int>)[];
                        S1<int> s = [1, 2];
                    }
                    static void M2()
                    {
                        S2<int> s = [];
                        object o = (S2<int>)[1, 2];
                    }
                }
                """;
            var verifier = CompileAndVerify(source);
            verifier.VerifyIL("Program.M0", """
                {
                  // Code size       35 (0x23)
                  .maxstack  2
                  .locals init (S0<int> V_0)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "S0<int>"
                  IL_0008:  ldloc.0
                  IL_0009:  pop
                  IL_000a:  ldloca.s   V_0
                  IL_000c:  initobj    "S0<int>"
                  IL_0012:  ldloca.s   V_0
                  IL_0014:  ldc.i4.1
                  IL_0015:  call       "void S0<int>.Add(int)"
                  IL_001a:  ldloca.s   V_0
                  IL_001c:  ldc.i4.2
                  IL_001d:  call       "void S0<int>.Add(int)"
                  IL_0022:  ret
                }
                """);
            verifier.VerifyIL("Program.M1", """
                {
                  // Code size       35 (0x23)
                  .maxstack  2
                  .locals init (S1<int> V_0)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "S1<int>"
                  IL_0008:  ldloc.0
                  IL_0009:  pop
                  IL_000a:  ldloca.s   V_0
                  IL_000c:  initobj    "S1<int>"
                  IL_0012:  ldloca.s   V_0
                  IL_0014:  ldc.i4.1
                  IL_0015:  call       "void S1<int>.Add(int)"
                  IL_001a:  ldloca.s   V_0
                  IL_001c:  ldc.i4.2
                  IL_001d:  call       "void S1<int>.Add(int)"
                  IL_0022:  ret
                }
                """);
            verifier.VerifyIL("Program.M2", """
                {
                  // Code size       30 (0x1e)
                  .maxstack  2
                  .locals init (S2<int> V_0)
                  IL_0000:  newobj     "S2<int>..ctor()"
                  IL_0005:  pop
                  IL_0006:  ldloca.s   V_0
                  IL_0008:  call       "S2<int>..ctor()"
                  IL_000d:  ldloca.s   V_0
                  IL_000f:  ldc.i4.1
                  IL_0010:  call       "void S2<int>.Add(int)"
                  IL_0015:  ldloca.s   V_0
                  IL_0017:  ldc.i4.2
                  IL_0018:  call       "void S2<int>.Add(int)"
                  IL_001d:  ret
                }
                """);
        }

        [Fact]
        public void CollectionInitializerType_09()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        UnknownType u;
                        u = [];
                        u = [null, B];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (7,9): error CS0246: The type or namespace name 'UnknownType' could not be found (are you missing a using directive or an assembly reference?)
                //         UnknownType u;
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownType").WithArguments("UnknownType").WithLocation(7, 9),
                // (9,20): error CS0103: The name 'B' does not exist in the current context
                //         u = [null, B];
                Diagnostic(ErrorCode.ERR_NameNotInContext, "B").WithArguments("B").WithLocation(9, 20));
        }

        [Fact]
        public void CollectionInitializerType_10()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                struct S<T> : IEnumerable<string>
                {
                    public void Add(string i) { }
                    IEnumerator<string> IEnumerable<string>.GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => null;
                }
                class Program
                {
                    static void Main()
                    {
                        S<UnknownType> s;
                        s = [];
                        s = [null, B];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (13,11): error CS0246: The type or namespace name 'UnknownType' could not be found (are you missing a using directive or an assembly reference?)
                //         S<UnknownType> s;
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "UnknownType").WithArguments("UnknownType").WithLocation(13, 11),
                // (15,20): error CS0103: The name 'B' does not exist in the current context
                //         s = [null, B];
                Diagnostic(ErrorCode.ERR_NameNotInContext, "B").WithArguments("B").WithLocation(15, 20));
        }

        [Fact]
        public void CollectionInitializerType_11()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        List<List<int>> l;
                        l = [[], [2, 3]];
                        l = [[], {2, 3}];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,18): error CS1003: Syntax error, ']' expected
                //         l = [[], {2, 3}];
                Diagnostic(ErrorCode.ERR_SyntaxError, "{").WithArguments("]").WithLocation(8, 18),
                // (8,18): error CS1002: ; expected
                //         l = [[], {2, 3}];
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "{").WithLocation(8, 18),
                // (8,20): error CS1002: ; expected
                //         l = [[], {2, 3}];
                Diagnostic(ErrorCode.ERR_SemicolonExpected, ",").WithLocation(8, 20),
                // (8,20): error CS1513: } expected
                //         l = [[], {2, 3}];
                Diagnostic(ErrorCode.ERR_RbraceExpected, ",").WithLocation(8, 20),
                // (8,23): error CS1002: ; expected
                //         l = [[], {2, 3}];
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "}").WithLocation(8, 23),
                // (8,24): error CS1513: } expected
                //         l = [[], {2, 3}];
                Diagnostic(ErrorCode.ERR_RbraceExpected, "]").WithLocation(8, 24));
        }

        [Fact]
        public void CollectionInitializerType_12()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C : IEnumerable
                {
                    List<string> _list = new List<string>();
                    public void Add(int i) { _list.Add($"i={i}"); }
                    public void Add(object o) { _list.Add($"o={o}"); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C x = [];
                        C y = [1, (object)2];
                        x.Report();
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [i=1, o=2], ");
        }

        [Fact]
        public void CollectionInitializerType_13()
        {
            string source = """
                using System.Collections;
                interface IA { }
                interface IB { }
                class AB : IA, IB { }
                class C : IEnumerable
                {
                    public void Add(IA a) { }
                    public void Add(IB b) { }
                    IEnumerator IEnumerable.GetEnumerator() => null;
                }
                class Program
                {
                    static void Main()
                    {
                        C c = [(IA)null, (IB)null, new AB()];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (15,36): error CS0121: The call is ambiguous between the following methods or properties: 'C.Add(IA)' and 'C.Add(IB)'
                //         C c = [(IA)null, (IB)null, new AB()];
                Diagnostic(ErrorCode.ERR_AmbigCall, "new AB()").WithArguments("C.Add(IA)", "C.Add(IB)").WithLocation(15, 36));
        }

        [Fact]
        public void CollectionInitializerType_14()
        {
            string source = """
                using System.Collections;
                struct S<T> : IEnumerable
                {
                    public void Add(T x, T y) { }
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                class Program
                {
                    static void Main()
                    {
                        S<int> s;
                        s = [];
                        s = [1, 2];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (13,14): error CS7036: There is no argument given that corresponds to the required parameter 'y' of 'S<int>.Add(int, int)'
                //         s = [1, 2];
                Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "1").WithArguments("y", "S<int>.Add(int, int)").WithLocation(13, 14),
                // (13,17): error CS7036: There is no argument given that corresponds to the required parameter 'y' of 'S<int>.Add(int, int)'
                //         s = [1, 2];
                Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "2").WithArguments("y", "S<int>.Add(int, int)").WithLocation(13, 17));
        }

        [Fact]
        public void CollectionInitializerType_15()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C<T> : IEnumerable
                {
                    List<T> _list = new List<T>();
                    public void Add(T t, int index = -1) { _list.Add(t); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C<int> c = [1, 2];
                        c.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2], ");
        }

        [Fact]
        public void CollectionInitializerType_16()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C<T> : IEnumerable
                {
                    List<T> _list = new List<T>();
                    public void Add(T t, params T[] args) { _list.Add(t); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C<int> c = [1, 2];
                        c.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2], ");
        }

        [Fact]
        public void CollectionInitializerType_17()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C<T> : IEnumerable
                {
                    List<T> _list = new List<T>();
                    public void Add(params T[] args) { _list.AddRange(args); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C<int> c = [[], [1, 2], 3];
                        c.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2, 3], ");
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size       61 (0x3d)
                  .maxstack  5
                  .locals init (C<int> V_0)
                  IL_0000:  newobj     "C<int>..ctor()"
                  IL_0005:  stloc.0
                  IL_0006:  ldloc.0
                  IL_0007:  call       "int[] System.Array.Empty<int>()"
                  IL_000c:  callvirt   "void C<int>.Add(params int[])"
                  IL_0011:  ldloc.0
                  IL_0012:  ldc.i4.2
                  IL_0013:  newarr     "int"
                  IL_0018:  dup
                  IL_0019:  ldc.i4.0
                  IL_001a:  ldc.i4.1
                  IL_001b:  stelem.i4
                  IL_001c:  dup
                  IL_001d:  ldc.i4.1
                  IL_001e:  ldc.i4.2
                  IL_001f:  stelem.i4
                  IL_0020:  callvirt   "void C<int>.Add(params int[])"
                  IL_0025:  ldloc.0
                  IL_0026:  ldc.i4.1
                  IL_0027:  newarr     "int"
                  IL_002c:  dup
                  IL_002d:  ldc.i4.0
                  IL_002e:  ldc.i4.3
                  IL_002f:  stelem.i4
                  IL_0030:  callvirt   "void C<int>.Add(params int[])"
                  IL_0035:  ldloc.0
                  IL_0036:  ldc.i4.0
                  IL_0037:  call       "void CollectionExtensions.Report(object, bool)"
                  IL_003c:  ret
                }
                """);
        }

        [Fact]
        public void CollectionInitializerType_18()
        {
            string source = """
                using System.Collections;
                class S<T, U> : IEnumerable
                {
                    internal void Add(T t) { }
                    private void Add(U u) { }
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                    static S<T, U> Create(T t, U u) => [t, u];
                }
                class Program
                {
                    static S<T, U> Create<T, U>(T x, U y) => [x, y];
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (11,50): error CS1950: The best overloaded Add method 'S<T, U>.Add(T)' for the collection initializer has some invalid arguments
                //     static S<T, U> Create<T, U>(T x, U y) => [x, y];
                Diagnostic(ErrorCode.ERR_BadArgTypesForCollectionAdd, "y").WithArguments("S<T, U>.Add(T)").WithLocation(11, 50),
                // (11,50): error CS1503: Argument 1: cannot convert from 'U' to 'T'
                //     static S<T, U> Create<T, U>(T x, U y) => [x, y];
                Diagnostic(ErrorCode.ERR_BadArgType, "y").WithArguments("1", "U", "T").WithLocation(11, 50));
        }

        [Fact]
        public void CollectionInitializerType_19()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        string s;
                        s = [];
                        s = ['a'];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,13): error CS1729: 'string' does not contain a constructor that takes 0 arguments
                //         s = [];
                Diagnostic(ErrorCode.ERR_BadCtorArgCount, "[]").WithArguments("string", "0").WithLocation(6, 13),
                // (7,14): error CS1061: 'string' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'string' could be found (are you missing a using directive or an assembly reference?)
                //         s = ['a'];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "'a'").WithArguments("string", "Add").WithLocation(7, 14));
        }

        [Theory]
        [InlineData("class")]
        [InlineData("struct")]
        public void TypeParameter_01(string type)
        {
            string source = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                interface I<T> : IEnumerable
                {
                    void Add(T t);
                }
                {{type}} C<T> : I<T>
                {
                    private List<T> _list;
                    public void Add(T t)
                    {
                        GetList().Add(t);
                    }
                    IEnumerator IEnumerable.GetEnumerator()
                    {
                        return GetList().GetEnumerator();
                    }
                    private List<T> GetList() => _list ??= new List<T>();
                }
                class Program
                {
                    static void Main()
                    {
                        CreateEmpty<C<object>, object>().Report();
                        Create<C<long?>, long?>(null, 2).Report();
                    }
                    static T CreateEmpty<T, U>() where T : I<U>, new()
                    {
                        return [];
                    }
                    static T Create<T, U>(U a, U b) where T : I<U>, new()
                    {
                        return [a, b];
                    }
                }
                """;
            var verifier = CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[], [null, 2], ");
            verifier.VerifyIL("Program.CreateEmpty<T, U>", """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  call       "T System.Activator.CreateInstance<T>()"
                  IL_0005:  ret
                }
                """);
            verifier.VerifyIL("Program.Create<T, U>", """
                {
                  // Code size       36 (0x24)
                  .maxstack  2
                  .locals init (T V_0)
                  IL_0000:  call       "T System.Activator.CreateInstance<T>()"
                  IL_0005:  stloc.0
                  IL_0006:  ldloca.s   V_0
                  IL_0008:  ldarg.0
                  IL_0009:  constrained. "T"
                  IL_000f:  callvirt   "void I<U>.Add(U)"
                  IL_0014:  ldloca.s   V_0
                  IL_0016:  ldarg.1
                  IL_0017:  constrained. "T"
                  IL_001d:  callvirt   "void I<U>.Add(U)"
                  IL_0022:  ldloc.0
                  IL_0023:  ret
                }
                """);
        }

        [Fact]
        public void TypeParameter_02()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                interface I<T> : IEnumerable<T>
                {
                    void Add(T t);
                }
                struct S<T> : I<T>
                {
                    List<T> _list;
                    public void Add(T t) { GetList().Add(t); }
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetList().GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetList().GetEnumerator();
                    private List<T> GetList() => _list ??= new List<T>();
                }
                class Program
                {
                    public static void Main()
                    {
                        Program.Create1<S<int>, int>().Report();
                        Program.Create2<S<int>, int>().Report();
                        Program.Create3<S<int>, int>(42, 43).Report();
                        Program.Create4<S<int>, int>(44, 45).Report();
                    }

                    static T Create1<T, U>() where T : struct, I<U> => [];
                    static T? Create2<T, U>() where T : struct, I<U> => [];
                    static T Create3<T, U>(U x, U y) where T : struct, I<U> => [x, y];
                    static T? Create4<T, U>(U x, U y) where T : struct, I<U> => [x, y];
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions }, options: TestOptions.ReleaseExe);
            comp.VerifyEmitDiagnostics();
            var verifier = CompileAndVerify(comp, expectedOutput: "[], [], [42, 43], [44, 45],");

            verifier.VerifyIL("Program.Create1<T, U>", """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  call       "T System.Activator.CreateInstance<T>()"
                  IL_0005:  ret
                }
                """);

            verifier.VerifyIL("Program.Create2<T, U>", """
                {
                  // Code size       11 (0xb)
                  .maxstack  1
                  IL_0000:  call       "T System.Activator.CreateInstance<T>()"
                  IL_0005:  newobj     "T?..ctor(T)"
                  IL_000a:  ret
                }
                """);
            verifier.VerifyIL("Program.Create3<T, U>", """
                {
                  // Code size       36 (0x24)
                  .maxstack  2
                  .locals init (T V_0)
                  IL_0000:  call       "T System.Activator.CreateInstance<T>()"
                  IL_0005:  stloc.0
                  IL_0006:  ldloca.s   V_0
                  IL_0008:  ldarg.0
                  IL_0009:  constrained. "T"
                  IL_000f:  callvirt   "void I<U>.Add(U)"
                  IL_0014:  ldloca.s   V_0
                  IL_0016:  ldarg.1
                  IL_0017:  constrained. "T"
                  IL_001d:  callvirt   "void I<U>.Add(U)"
                  IL_0022:  ldloc.0
                  IL_0023:  ret
                }
                """);
            verifier.VerifyIL("Program.Create4<T, U>", """
                {
                  // Code size       41 (0x29)
                  .maxstack  2
                  .locals init (T V_0)
                  IL_0000:  call       "T System.Activator.CreateInstance<T>()"
                  IL_0005:  stloc.0
                  IL_0006:  ldloca.s   V_0
                  IL_0008:  ldarg.0
                  IL_0009:  constrained. "T"
                  IL_000f:  callvirt   "void I<U>.Add(U)"
                  IL_0014:  ldloca.s   V_0
                  IL_0016:  ldarg.1
                  IL_0017:  constrained. "T"
                  IL_001d:  callvirt   "void I<U>.Add(U)"
                  IL_0022:  ldloc.0
                  IL_0023:  newobj     "T?..ctor(T)"
                  IL_0028:  ret
                }
                """);
        }

        [Fact]
        public void TypeParameter_03()
        {
            string source = """
                using System.Collections;
                class Program
                {
                    static T Create1<T, U>() where T : IEnumerable => []; // 1
                    static T Create2<T, U>() where T : class, IEnumerable => []; // 2
                    static T Create3<T, U>() where T : struct, IEnumerable => [];
                    static T Create4<T, U>() where T : IEnumerable, new() => [];
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (4,55): error CS0304: Cannot create an instance of the variable type 'T' because it does not have the new() constraint
                //     static T Create1<T, U>() where T : IEnumerable => []; // 1
                Diagnostic(ErrorCode.ERR_NoNewTyvar, "[]").WithArguments("T").WithLocation(4, 55),
                // (5,62): error CS0304: Cannot create an instance of the variable type 'T' because it does not have the new() constraint
                //     static T Create2<T, U>() where T : class, IEnumerable => []; // 2
                Diagnostic(ErrorCode.ERR_NoNewTyvar, "[]").WithArguments("T").WithLocation(5, 62));
        }

        [Fact]
        public void TypeParameter_04()
        {
            string source = """
                using System.Collections;
                interface IAdd : IEnumerable
                {
                    void Add(int i);
                }
                class Program
                {
                    static T Create1<T>() where T : IAdd => [1]; // 1
                    static T Create2<T>() where T : class, IAdd => [2]; // 2
                    static T Create3<T>() where T : struct, IAdd => [3];
                    static T Create4<T>() where T : IAdd, new() => [4];
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,45): error CS0304: Cannot create an instance of the variable type 'T' because it does not have the new() constraint
                //     static T Create1<T>() where T : IAdd => [1]; // 1
                Diagnostic(ErrorCode.ERR_NoNewTyvar, "[1]").WithArguments("T").WithLocation(8, 45),
                // (9,52): error CS0304: Cannot create an instance of the variable type 'T' because it does not have the new() constraint
                //     static T Create2<T>() where T : class, IAdd => [2]; // 2
                Diagnostic(ErrorCode.ERR_NoNewTyvar, "[2]").WithArguments("T").WithLocation(9, 52));
        }

        [Fact]
        public void CollectionInitializerType_MissingIEnumerable()
        {
            string source = """
                struct S
                {
                }
                class Program
                {
                    static void Main()
                    {
                        S s = [];
                        object o = (S)([1, 2]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeTypeMissing(SpecialType.System_Collections_IEnumerable);
            comp.VerifyEmitDiagnostics(
                // (8,15): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         S s = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("S").WithLocation(8, 15),
                // (9,20): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         object o = (S)([1, 2]);
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "(S)([1, 2])").WithArguments("S").WithLocation(9, 20));
        }

        [Fact]
        public void CollectionInitializerType_UseSiteErrors()
        {
            string assemblyA = GetUniqueName();
            string sourceA = """
                public class A1 { }
                public class A2 { }
                """;
            var comp = CreateCompilation(sourceA, assemblyName: assemblyA);
            var refA = comp.EmitToImageReference();

            string sourceB = """
                using System.Collections;
                using System.Collections.Generic;
                public class B1 : IEnumerable
                {
                    List<int> _list = new List<int>();
                    public B1(A1 a = null) { }
                    public void Add(int i) { _list.Add(i); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                public class B2 : IEnumerable
                {
                    List<int> _list = new List<int>();
                    public void Add(int x, A2 y = null) { _list.Add(x); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA });
            var refB = comp.EmitToImageReference();

            string sourceC = """
                class C
                {
                    static void Main()
                    {
                        B1 x;
                        x = [];
                        x.Report();
                        x = [1, 2];
                        x.Report();
                        B2 y;
                        y = [];
                        y.Report();
                        y = [3, 4];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { sourceC, s_collectionExtensions }, references: new[] { refA, refB }, expectedOutput: "[], [1, 2], [], [3, 4], ");

            comp = CreateCompilation(new[] { sourceC, s_collectionExtensions }, references: new[] { refB });
            comp.VerifyEmitDiagnostics(
                // 0.cs(6,13): error CS0012: The type 'A1' is defined in an assembly that is not referenced. You must add a reference to assembly 'a897d975-a839-4fff-828b-deccf9495adc, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
                //         x = [];
                Diagnostic(ErrorCode.ERR_NoTypeDef, "[]").WithArguments("A1", $"{assemblyA}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").WithLocation(6, 13),
                // 0.cs(8,13): error CS0012: The type 'A1' is defined in an assembly that is not referenced. You must add a reference to assembly 'a897d975-a839-4fff-828b-deccf9495adc, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
                //         x = [1, 2];
                Diagnostic(ErrorCode.ERR_NoTypeDef, "[1, 2]").WithArguments("A1", $"{assemblyA}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").WithLocation(8, 13),
                // 0.cs(13,14): error CS0012: The type 'A2' is defined in an assembly that is not referenced. You must add a reference to assembly 'a897d975-a839-4fff-828b-deccf9495adc, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
                //         y = [3, 4];
                Diagnostic(ErrorCode.ERR_NoTypeDef, "3").WithArguments("A2", $"{assemblyA}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").WithLocation(13, 14),
                // 0.cs(13,17): error CS0012: The type 'A2' is defined in an assembly that is not referenced. You must add a reference to assembly 'a897d975-a839-4fff-828b-deccf9495adc, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
                //         y = [3, 4];
                Diagnostic(ErrorCode.ERR_NoTypeDef, "4").WithArguments("A2", $"{assemblyA}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").WithLocation(13, 17));
        }

        [Fact]
        public void ConditionalAdd()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                using System.Diagnostics;
                class C<T, U> : IEnumerable
                {
                    List<object> _list = new List<object>();
                    [Conditional("DEBUG")] internal void Add(T t) { _list.Add(t); }
                    internal void Add(U u) { _list.Add(u); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C<int, string> c = [1, "2", 3];
                        c.Report();
                    }
                }
                """;
            var parseOptions = TestOptions.RegularPreview;
            CompileAndVerify(new[] { source, s_collectionExtensions }, parseOptions: parseOptions.WithPreprocessorSymbols("DEBUG"), expectedOutput: "[1, 2, 3], ");
            CompileAndVerify(new[] { source, s_collectionExtensions }, parseOptions: parseOptions, expectedOutput: "[2], ");
        }

        [Fact]
        public void DictionaryElement_01()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        Dictionary<int, int> d;
                        d = [];
                        d = [new KeyValuePair<int, int>(1, 2)];
                        d = [3:4];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (8,14): error CS7036: There is no argument given that corresponds to the required parameter 'value' of 'Dictionary<int, int>.Add(int, int)'
                //         d = [new KeyValuePair<int, int>(1, 2)];
                Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "new KeyValuePair<int, int>(1, 2)").WithArguments("value", "System.Collections.Generic.Dictionary<int, int>.Add(int, int)").WithLocation(8, 14),
                // (9,15): error CS1003: Syntax error, ',' expected
                //         d = [3:4];
                Diagnostic(ErrorCode.ERR_SyntaxError, ":").WithArguments(",").WithLocation(9, 15),
                // (9,16): error CS1003: Syntax error, ',' expected
                //         d = [3:4];
                Diagnostic(ErrorCode.ERR_SyntaxError, "4").WithArguments(",").WithLocation(9, 16));
        }

        [Theory]
        [CombinatorialData]
        public void SpreadElement_01(
            [CombinatorialValues("IEnumerable<int>", "int[]", "List<int>", "Span<int>", "ReadOnlySpan<int>")] string spreadType,
            [CombinatorialValues("IEnumerable<int>", "int[]", "List<int>", "Span<int>", "ReadOnlySpan<int>")] string collectionType)
        {
            string source = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        F([1, 2, 3]);
                    }
                    static void F({{spreadType}} x)
                    {
                        {{collectionType}} y = [..x];
                        y.Report();
                    }
                }
                """;

            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                options: TestOptions.ReleaseExe,
                targetFramework: TargetFramework.Net70,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));

            // Verify some of the cases.
            string expectedIL = (spreadType, collectionType) switch
            {
                ("IEnumerable<int>", "IEnumerable<int>") =>
                    """
                    {
                      // Code size       62 (0x3e)
                      .maxstack  2
                      .locals init (System.Collections.Generic.List<int> V_0,
                                    System.Collections.Generic.IEnumerator<int> V_1,
                                    int V_2)
                      IL_0000:  newobj     "System.Collections.Generic.List<int>..ctor()"
                      IL_0005:  stloc.0
                      IL_0006:  ldarg.0
                      IL_0007:  callvirt   "System.Collections.Generic.IEnumerator<int> System.Collections.Generic.IEnumerable<int>.GetEnumerator()"
                      IL_000c:  stloc.1
                      .try
                      {
                        IL_000d:  br.s       IL_001d
                        IL_000f:  ldloc.1
                        IL_0010:  callvirt   "int System.Collections.Generic.IEnumerator<int>.Current.get"
                        IL_0015:  stloc.2
                        IL_0016:  ldloc.0
                        IL_0017:  ldloc.2
                        IL_0018:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                        IL_001d:  ldloc.1
                        IL_001e:  callvirt   "bool System.Collections.IEnumerator.MoveNext()"
                        IL_0023:  brtrue.s   IL_000f
                        IL_0025:  leave.s    IL_0031
                      }
                      finally
                      {
                        IL_0027:  ldloc.1
                        IL_0028:  brfalse.s  IL_0030
                        IL_002a:  ldloc.1
                        IL_002b:  callvirt   "void System.IDisposable.Dispose()"
                        IL_0030:  endfinally
                      }
                      IL_0031:  ldloc.0
                      IL_0032:  newobj     "<>z__ReadOnlyList<int>..ctor(System.Collections.Generic.List<int>)"
                      IL_0037:  ldc.i4.0
                      IL_0038:  call       "void CollectionExtensions.Report(object, bool)"
                      IL_003d:  ret
                    }
                    """,
                ("IEnumerable<int>", "int[]") =>
                    """
                    {
                      // Code size       62 (0x3e)
                      .maxstack  2
                      .locals init (System.Collections.Generic.List<int> V_0,
                                    System.Collections.Generic.IEnumerator<int> V_1,
                                    int V_2)
                      IL_0000:  newobj     "System.Collections.Generic.List<int>..ctor()"
                      IL_0005:  stloc.0
                      IL_0006:  ldarg.0
                      IL_0007:  callvirt   "System.Collections.Generic.IEnumerator<int> System.Collections.Generic.IEnumerable<int>.GetEnumerator()"
                      IL_000c:  stloc.1
                      .try
                      {
                        IL_000d:  br.s       IL_001d
                        IL_000f:  ldloc.1
                        IL_0010:  callvirt   "int System.Collections.Generic.IEnumerator<int>.Current.get"
                        IL_0015:  stloc.2
                        IL_0016:  ldloc.0
                        IL_0017:  ldloc.2
                        IL_0018:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                        IL_001d:  ldloc.1
                        IL_001e:  callvirt   "bool System.Collections.IEnumerator.MoveNext()"
                        IL_0023:  brtrue.s   IL_000f
                        IL_0025:  leave.s    IL_0031
                      }
                      finally
                      {
                        IL_0027:  ldloc.1
                        IL_0028:  brfalse.s  IL_0030
                        IL_002a:  ldloc.1
                        IL_002b:  callvirt   "void System.IDisposable.Dispose()"
                        IL_0030:  endfinally
                      }
                      IL_0031:  ldloc.0
                      IL_0032:  callvirt   "int[] System.Collections.Generic.List<int>.ToArray()"
                      IL_0037:  ldc.i4.0
                      IL_0038:  call       "void CollectionExtensions.Report(object, bool)"
                      IL_003d:  ret
                    }
                    """,
                ("int[]", "int[]") =>
                    """
                    {
                      // Code size       49 (0x31)
                      .maxstack  3
                      .locals init (int V_0,
                                    int[] V_1,
                                    int[] V_2,
                                    int V_3,
                                    int V_4)
                      IL_0000:  ldarg.0
                      IL_0001:  ldc.i4.0
                      IL_0002:  stloc.0
                      IL_0003:  dup
                      IL_0004:  ldlen
                      IL_0005:  conv.i4
                      IL_0006:  newarr     "int"
                      IL_000b:  stloc.1
                      IL_000c:  stloc.2
                      IL_000d:  ldc.i4.0
                      IL_000e:  stloc.3
                      IL_000f:  br.s       IL_0023
                      IL_0011:  ldloc.2
                      IL_0012:  ldloc.3
                      IL_0013:  ldelem.i4
                      IL_0014:  stloc.s    V_4
                      IL_0016:  ldloc.1
                      IL_0017:  ldloc.0
                      IL_0018:  ldloc.s    V_4
                      IL_001a:  stelem.i4
                      IL_001b:  ldloc.0
                      IL_001c:  ldc.i4.1
                      IL_001d:  add
                      IL_001e:  stloc.0
                      IL_001f:  ldloc.3
                      IL_0020:  ldc.i4.1
                      IL_0021:  add
                      IL_0022:  stloc.3
                      IL_0023:  ldloc.3
                      IL_0024:  ldloc.2
                      IL_0025:  ldlen
                      IL_0026:  conv.i4
                      IL_0027:  blt.s      IL_0011
                      IL_0029:  ldloc.1
                      IL_002a:  ldc.i4.0
                      IL_002b:  call       "void CollectionExtensions.Report(object, bool)"
                      IL_0030:  ret
                    }
                    """,
                ("ReadOnlySpan<int>", "ReadOnlySpan<int>") =>
                    """
                    {
                      // Code size       72 (0x48)
                      .maxstack  3
                      .locals init (System.ReadOnlySpan<int> V_0, //y
                                    System.ReadOnlySpan<int> V_1,
                                    int V_2,
                                    int[] V_3,
                                    System.ReadOnlySpan<int>.Enumerator V_4,
                                    int V_5)
                      IL_0000:  ldarg.0
                      IL_0001:  stloc.1
                      IL_0002:  ldc.i4.0
                      IL_0003:  stloc.2
                      IL_0004:  ldloca.s   V_1
                      IL_0006:  call       "int System.ReadOnlySpan<int>.Length.get"
                      IL_000b:  newarr     "int"
                      IL_0010:  stloc.3
                      IL_0011:  ldloca.s   V_1
                      IL_0013:  call       "System.ReadOnlySpan<int>.Enumerator System.ReadOnlySpan<int>.GetEnumerator()"
                      IL_0018:  stloc.s    V_4
                      IL_001a:  br.s       IL_002f
                      IL_001c:  ldloca.s   V_4
                      IL_001e:  call       "ref readonly int System.ReadOnlySpan<int>.Enumerator.Current.get"
                      IL_0023:  ldind.i4
                      IL_0024:  stloc.s    V_5
                      IL_0026:  ldloc.3
                      IL_0027:  ldloc.2
                      IL_0028:  ldloc.s    V_5
                      IL_002a:  stelem.i4
                      IL_002b:  ldloc.2
                      IL_002c:  ldc.i4.1
                      IL_002d:  add
                      IL_002e:  stloc.2
                      IL_002f:  ldloca.s   V_4
                      IL_0031:  call       "bool System.ReadOnlySpan<int>.Enumerator.MoveNext()"
                      IL_0036:  brtrue.s   IL_001c
                      IL_0038:  ldloca.s   V_0
                      IL_003a:  ldloc.3
                      IL_003b:  call       "System.ReadOnlySpan<int>..ctor(int[])"
                      IL_0040:  ldloca.s   V_0
                      IL_0042:  call       "void CollectionExtensions.Report<int>(in System.ReadOnlySpan<int>)"
                      IL_0047:  ret
                    }
                    """,
                _ => null
            };
            if (expectedIL is { })
            {
                verifier.VerifyIL("Program.F", expectedIL);
            }
        }

        [Theory]
        [InlineData("int[]")]
        [InlineData("System.Collections.Generic.List<int>")]
        [InlineData("System.Span<int>")]
        [InlineData("System.ReadOnlySpan<int>")]
        public void SpreadElement_02(string collectionType)
        {
            string source = $$"""
                class Program
                {
                    static void Main()
                    {
                        {{collectionType}} c = [];
                        Append(c);
                    }
                    static void Append({{collectionType}} x)
                    {
                        {{collectionType}} y = [1, 2];
                        {{collectionType}} z = [..x, ..y];
                        z.Report();
                    }
                }
                """;

            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                options: TestOptions.ReleaseExe,
                targetFramework: TargetFramework.Net70,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2], "));

            if (collectionType == "System.ReadOnlySpan<int>")
            {
                verifier.VerifyIL("Program.Append",
                    """
                    {
                      // Code size      134 (0x86)
                      .maxstack  3
                      .locals init (System.ReadOnlySpan<int> V_0, //z
                                    System.ReadOnlySpan<int> V_1,
                                    System.ReadOnlySpan<int> V_2,
                                    int V_3,
                                    int[] V_4,
                                    System.ReadOnlySpan<int>.Enumerator V_5,
                                    int V_6)
                      IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=8_Align=4 <PrivateImplementationDetails>.34FB5C825DE7CA4AEA6E712F19D439C1DA0C92C37B423936C5F618545CA4FA1F4"
                      IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
                      IL_000a:  ldarg.0
                      IL_000b:  stloc.1
                      IL_000c:  stloc.2
                      IL_000d:  ldc.i4.0
                      IL_000e:  stloc.3
                      IL_000f:  ldloca.s   V_1
                      IL_0011:  call       "int System.ReadOnlySpan<int>.Length.get"
                      IL_0016:  ldloca.s   V_2
                      IL_0018:  call       "int System.ReadOnlySpan<int>.Length.get"
                      IL_001d:  add
                      IL_001e:  newarr     "int"
                      IL_0023:  stloc.s    V_4
                      IL_0025:  ldloca.s   V_1
                      IL_0027:  call       "System.ReadOnlySpan<int>.Enumerator System.ReadOnlySpan<int>.GetEnumerator()"
                      IL_002c:  stloc.s    V_5
                      IL_002e:  br.s       IL_0044
                      IL_0030:  ldloca.s   V_5
                      IL_0032:  call       "ref readonly int System.ReadOnlySpan<int>.Enumerator.Current.get"
                      IL_0037:  ldind.i4
                      IL_0038:  stloc.s    V_6
                      IL_003a:  ldloc.s    V_4
                      IL_003c:  ldloc.3
                      IL_003d:  ldloc.s    V_6
                      IL_003f:  stelem.i4
                      IL_0040:  ldloc.3
                      IL_0041:  ldc.i4.1
                      IL_0042:  add
                      IL_0043:  stloc.3
                      IL_0044:  ldloca.s   V_5
                      IL_0046:  call       "bool System.ReadOnlySpan<int>.Enumerator.MoveNext()"
                      IL_004b:  brtrue.s   IL_0030
                      IL_004d:  ldloca.s   V_2
                      IL_004f:  call       "System.ReadOnlySpan<int>.Enumerator System.ReadOnlySpan<int>.GetEnumerator()"
                      IL_0054:  stloc.s    V_5
                      IL_0056:  br.s       IL_006c
                      IL_0058:  ldloca.s   V_5
                      IL_005a:  call       "ref readonly int System.ReadOnlySpan<int>.Enumerator.Current.get"
                      IL_005f:  ldind.i4
                      IL_0060:  stloc.s    V_6
                      IL_0062:  ldloc.s    V_4
                      IL_0064:  ldloc.3
                      IL_0065:  ldloc.s    V_6
                      IL_0067:  stelem.i4
                      IL_0068:  ldloc.3
                      IL_0069:  ldc.i4.1
                      IL_006a:  add
                      IL_006b:  stloc.3
                      IL_006c:  ldloca.s   V_5
                      IL_006e:  call       "bool System.ReadOnlySpan<int>.Enumerator.MoveNext()"
                      IL_0073:  brtrue.s   IL_0058
                      IL_0075:  ldloca.s   V_0
                      IL_0077:  ldloc.s    V_4
                      IL_0079:  call       "System.ReadOnlySpan<int>..ctor(int[])"
                      IL_007e:  ldloca.s   V_0
                      IL_0080:  call       "void CollectionExtensions.Report<int>(in System.ReadOnlySpan<int>)"
                      IL_0085:  ret
                    }
                    """);
            }
        }

        [Fact]
        public void SpreadElement_03()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                struct S<T> : IEnumerable<T>
                {
                    private List<T> _list;
                    public void Add(T t)
                    {
                        _list ??= new List<T>();
                        _list.Add(t);
                    }
                    public IEnumerator<T> GetEnumerator()
                    {
                        _list ??= new List<T>();
                        return _list.GetEnumerator();
                    }
                    IEnumerator IEnumerable.GetEnumerator()
                    {
                        return GetEnumerator();
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        S<int> s;
                        s = [];
                        s = Append(s);
                        s.Report();
                    }
                    static S<int> Append(S<int> x)
                    {
                        S<int> y = [1, 2];
                        return [..x, ..y];
                    }
                }
                """;

            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                options: TestOptions.ReleaseExe,
                expectedOutput: "[1, 2], ");

            verifier.VerifyIL("Program.Append",
                """
                {
                  // Code size      126 (0x7e)
                  .maxstack  2
                  .locals init (S<int> V_0, //y
                                S<int> V_1,
                                System.Collections.Generic.IEnumerator<int> V_2,
                                int V_3)
                  IL_0000:  ldloca.s   V_1
                  IL_0002:  initobj    "S<int>"
                  IL_0008:  ldloca.s   V_1
                  IL_000a:  ldc.i4.1
                  IL_000b:  call       "void S<int>.Add(int)"
                  IL_0010:  ldloca.s   V_1
                  IL_0012:  ldc.i4.2
                  IL_0013:  call       "void S<int>.Add(int)"
                  IL_0018:  ldloc.1
                  IL_0019:  stloc.0
                  IL_001a:  ldloca.s   V_1
                  IL_001c:  initobj    "S<int>"
                  IL_0022:  ldarga.s   V_0
                  IL_0024:  call       "System.Collections.Generic.IEnumerator<int> S<int>.GetEnumerator()"
                  IL_0029:  stloc.2
                  .try
                  {
                    IL_002a:  br.s       IL_003b
                    IL_002c:  ldloc.2
                    IL_002d:  callvirt   "int System.Collections.Generic.IEnumerator<int>.Current.get"
                    IL_0032:  stloc.3
                    IL_0033:  ldloca.s   V_1
                    IL_0035:  ldloc.3
                    IL_0036:  call       "void S<int>.Add(int)"
                    IL_003b:  ldloc.2
                    IL_003c:  callvirt   "bool System.Collections.IEnumerator.MoveNext()"
                    IL_0041:  brtrue.s   IL_002c
                    IL_0043:  leave.s    IL_004f
                  }
                  finally
                  {
                    IL_0045:  ldloc.2
                    IL_0046:  brfalse.s  IL_004e
                    IL_0048:  ldloc.2
                    IL_0049:  callvirt   "void System.IDisposable.Dispose()"
                    IL_004e:  endfinally
                  }
                  IL_004f:  ldloca.s   V_0
                  IL_0051:  call       "System.Collections.Generic.IEnumerator<int> S<int>.GetEnumerator()"
                  IL_0056:  stloc.2
                  .try
                  {
                    IL_0057:  br.s       IL_0068
                    IL_0059:  ldloc.2
                    IL_005a:  callvirt   "int System.Collections.Generic.IEnumerator<int>.Current.get"
                    IL_005f:  stloc.3
                    IL_0060:  ldloca.s   V_1
                    IL_0062:  ldloc.3
                    IL_0063:  call       "void S<int>.Add(int)"
                    IL_0068:  ldloc.2
                    IL_0069:  callvirt   "bool System.Collections.IEnumerator.MoveNext()"
                    IL_006e:  brtrue.s   IL_0059
                    IL_0070:  leave.s    IL_007c
                  }
                  finally
                  {
                    IL_0072:  ldloc.2
                    IL_0073:  brfalse.s  IL_007b
                    IL_0075:  ldloc.2
                    IL_0076:  callvirt   "void System.IDisposable.Dispose()"
                    IL_007b:  endfinally
                  }
                  IL_007c:  ldloc.1
                  IL_007d:  ret
                }
                """);
        }

        [Fact]
        public void SpreadElement_04()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        var a = [1, 2, ..[]];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,26): error CS9176: There is no target type for the collection expression.
                //         var a = [1, 2, ..[]];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(5, 26));
        }

        [Fact]
        public void SpreadElement_05()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        int[] a = [1, 2];
                        a = [..a, ..[]];
                        a = [..[default]];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,21): error CS9176: There is no target type for the collection expression.
                //         a = [..a, ..[]];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(6, 21),
                // (7,16): error CS9176: There is no target type for the collection expression.
                //         a = [..[default]];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[default]").WithLocation(7, 16));
        }

        [Fact]
        public void SpreadElement_06()
        {
            string source = """
                class Program
                {
                    static string[] Append(string a, string b, bool c)
                    {
                        return [a, b, .. c ? [null] : []];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (5,26): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'collection expressions' and 'collection expressions'
                //         return [a, b, .. c ? [null] : []];
                Diagnostic(ErrorCode.ERR_InvalidQM, "c ? [null] : []").WithArguments("collection expressions", "collection expressions").WithLocation(5, 26));
        }

        [Fact]
        public void SpreadElement_07()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        int[,] a = new[,] { { 1, 2 }, { 3, 4 } };
                        int[] b = F(a);
                        b.Report();
                    }
                    static int[] F(int[,] a) => [..a];
                }
                """;
            var verifier = CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2, 3, 4], ");
            verifier.VerifyIL("Program.F",
                """
                {
                  // Code size      101 (0x65)
                  .maxstack  3
                  .locals init (int V_0,
                                int[] V_1,
                                int[,] V_2,
                                int V_3,
                                int V_4,
                                int V_5,
                                int V_6,
                                int V_7)
                  IL_0000:  ldarg.0
                  IL_0001:  ldc.i4.0
                  IL_0002:  stloc.0
                  IL_0003:  dup
                  IL_0004:  callvirt   "int System.Array.Length.get"
                  IL_0009:  newarr     "int"
                  IL_000e:  stloc.1
                  IL_000f:  stloc.2
                  IL_0010:  ldloc.2
                  IL_0011:  ldc.i4.0
                  IL_0012:  callvirt   "int System.Array.GetUpperBound(int)"
                  IL_0017:  stloc.3
                  IL_0018:  ldloc.2
                  IL_0019:  ldc.i4.1
                  IL_001a:  callvirt   "int System.Array.GetUpperBound(int)"
                  IL_001f:  stloc.s    V_4
                  IL_0021:  ldloc.2
                  IL_0022:  ldc.i4.0
                  IL_0023:  callvirt   "int System.Array.GetLowerBound(int)"
                  IL_0028:  stloc.s    V_5
                  IL_002a:  br.s       IL_005e
                  IL_002c:  ldloc.2
                  IL_002d:  ldc.i4.1
                  IL_002e:  callvirt   "int System.Array.GetLowerBound(int)"
                  IL_0033:  stloc.s    V_6
                  IL_0035:  br.s       IL_0052
                  IL_0037:  ldloc.2
                  IL_0038:  ldloc.s    V_5
                  IL_003a:  ldloc.s    V_6
                  IL_003c:  call       "int[*,*].Get"
                  IL_0041:  stloc.s    V_7
                  IL_0043:  ldloc.1
                  IL_0044:  ldloc.0
                  IL_0045:  ldloc.s    V_7
                  IL_0047:  stelem.i4
                  IL_0048:  ldloc.0
                  IL_0049:  ldc.i4.1
                  IL_004a:  add
                  IL_004b:  stloc.0
                  IL_004c:  ldloc.s    V_6
                  IL_004e:  ldc.i4.1
                  IL_004f:  add
                  IL_0050:  stloc.s    V_6
                  IL_0052:  ldloc.s    V_6
                  IL_0054:  ldloc.s    V_4
                  IL_0056:  ble.s      IL_0037
                  IL_0058:  ldloc.s    V_5
                  IL_005a:  ldc.i4.1
                  IL_005b:  add
                  IL_005c:  stloc.s    V_5
                  IL_005e:  ldloc.s    V_5
                  IL_0060:  ldloc.3
                  IL_0061:  ble.s      IL_002c
                  IL_0063:  ldloc.1
                  IL_0064:  ret
                }
                """);
        }

        [Fact]
        public void SpreadElement_08()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        int[] a = [1, 2, 3];
                        object[] b = F1(a);
                        b.Report();
                        long?[] c = F2(a);
                        c.Report();
                        object[] d = F3<int, object>(a);
                        d.Report();
                    }
                    static object[] F1(int[] a) => [..a];
                    static long?[] F2(int[] a) => [..a];
                    static U[] F3<T, U>(T[] a) where T : U => [..a];
                }
                """;
            var verifier = CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2, 3], [1, 2, 3], [1, 2, 3], ");
            verifier.VerifyIL("Program.F1",
                """
                {
                  // Code size       48 (0x30)
                  .maxstack  3
                  .locals init (int V_0,
                                object[] V_1,
                                int[] V_2,
                                int V_3,
                                int V_4)
                  IL_0000:  ldarg.0
                  IL_0001:  ldc.i4.0
                  IL_0002:  stloc.0
                  IL_0003:  dup
                  IL_0004:  ldlen
                  IL_0005:  conv.i4
                  IL_0006:  newarr     "object"
                  IL_000b:  stloc.1
                  IL_000c:  stloc.2
                  IL_000d:  ldc.i4.0
                  IL_000e:  stloc.3
                  IL_000f:  br.s       IL_0028
                  IL_0011:  ldloc.2
                  IL_0012:  ldloc.3
                  IL_0013:  ldelem.i4
                  IL_0014:  stloc.s    V_4
                  IL_0016:  ldloc.1
                  IL_0017:  ldloc.0
                  IL_0018:  ldloc.s    V_4
                  IL_001a:  box        "int"
                  IL_001f:  stelem.ref
                  IL_0020:  ldloc.0
                  IL_0021:  ldc.i4.1
                  IL_0022:  add
                  IL_0023:  stloc.0
                  IL_0024:  ldloc.3
                  IL_0025:  ldc.i4.1
                  IL_0026:  add
                  IL_0027:  stloc.3
                  IL_0028:  ldloc.3
                  IL_0029:  ldloc.2
                  IL_002a:  ldlen
                  IL_002b:  conv.i4
                  IL_002c:  blt.s      IL_0011
                  IL_002e:  ldloc.1
                  IL_002f:  ret
                }
                """);
            verifier.VerifyIL("Program.F2",
                """
                {
                  // Code size       53 (0x35)
                  .maxstack  3
                  .locals init (int V_0,
                                long?[] V_1,
                                int[] V_2,
                                int V_3,
                                int V_4)
                  IL_0000:  ldarg.0
                  IL_0001:  ldc.i4.0
                  IL_0002:  stloc.0
                  IL_0003:  dup
                  IL_0004:  ldlen
                  IL_0005:  conv.i4
                  IL_0006:  newarr     "long?"
                  IL_000b:  stloc.1
                  IL_000c:  stloc.2
                  IL_000d:  ldc.i4.0
                  IL_000e:  stloc.3
                  IL_000f:  br.s       IL_002d
                  IL_0011:  ldloc.2
                  IL_0012:  ldloc.3
                  IL_0013:  ldelem.i4
                  IL_0014:  stloc.s    V_4
                  IL_0016:  ldloc.1
                  IL_0017:  ldloc.0
                  IL_0018:  ldloc.s    V_4
                  IL_001a:  conv.i8
                  IL_001b:  newobj     "long?..ctor(long)"
                  IL_0020:  stelem     "long?"
                  IL_0025:  ldloc.0
                  IL_0026:  ldc.i4.1
                  IL_0027:  add
                  IL_0028:  stloc.0
                  IL_0029:  ldloc.3
                  IL_002a:  ldc.i4.1
                  IL_002b:  add
                  IL_002c:  stloc.3
                  IL_002d:  ldloc.3
                  IL_002e:  ldloc.2
                  IL_002f:  ldlen
                  IL_0030:  conv.i4
                  IL_0031:  blt.s      IL_0011
                  IL_0033:  ldloc.1
                  IL_0034:  ret
                }
                """);
            verifier.VerifyIL("Program.F3<T, U>",
                """
                {
                  // Code size       61 (0x3d)
                  .maxstack  3
                  .locals init (int V_0,
                                U[] V_1,
                                T[] V_2,
                                int V_3,
                                T V_4)
                  IL_0000:  ldarg.0
                  IL_0001:  ldc.i4.0
                  IL_0002:  stloc.0
                  IL_0003:  dup
                  IL_0004:  ldlen
                  IL_0005:  conv.i4
                  IL_0006:  newarr     "U"
                  IL_000b:  stloc.1
                  IL_000c:  stloc.2
                  IL_000d:  ldc.i4.0
                  IL_000e:  stloc.3
                  IL_000f:  br.s       IL_0035
                  IL_0011:  ldloc.2
                  IL_0012:  ldloc.3
                  IL_0013:  ldelem     "T"
                  IL_0018:  stloc.s    V_4
                  IL_001a:  ldloc.1
                  IL_001b:  ldloc.0
                  IL_001c:  ldloc.s    V_4
                  IL_001e:  box        "T"
                  IL_0023:  unbox.any  "U"
                  IL_0028:  stelem     "U"
                  IL_002d:  ldloc.0
                  IL_002e:  ldc.i4.1
                  IL_002f:  add
                  IL_0030:  stloc.0
                  IL_0031:  ldloc.3
                  IL_0032:  ldc.i4.1
                  IL_0033:  add
                  IL_0034:  stloc.3
                  IL_0035:  ldloc.3
                  IL_0036:  ldloc.2
                  IL_0037:  ldlen
                  IL_0038:  conv.i4
                  IL_0039:  blt.s      IL_0011
                  IL_003b:  ldloc.1
                  IL_003c:  ret
                }
                """);
        }

        [Theory]
        [InlineData("List")]
        [InlineData("Span")]
        [InlineData("ReadOnlySpan")]
        public void SpreadElement_09(string collectionType)
        {
            string source = $$"""
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        {{collectionType}}<int> a = [1, 2, 3];
                        F1(a);
                        F2<int, object>(a);
                    }
                    static void F1({{collectionType}}<int> a)
                    {
                        {{collectionType}}<object> b = [..a];
                        b.Report();
                    }
                    static void F2<T, U>({{collectionType}}<T> a) where T : U
                    {
                        {{collectionType}}<U> b = [..a];
                        b.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net70,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], [1, 2, 3], "));
        }

        [Fact]
        public void SpreadElement_10()
        {
            string source = """
                using System.Collections;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable a = new[] { 1, 2, 3 };
                        object[] b = [..a, 4];
                        b.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2, 3, 4], ");
        }

        [Fact]
        public void SpreadElement_11()
        {
            string source = """
                using System.Collections;
                class Program
                {
                    static void Main()
                    {
                        F([1, 2, 3]);
                    }
                    static int[] F(IEnumerable s) => [..s];
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,11): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'System.Collections.IEnumerable'
                //         F([1, 2, 3]);
                Diagnostic(ErrorCode.ERR_BadArgType, "[1, 2, 3]").WithArguments("1", "collection expressions", "System.Collections.IEnumerable").WithLocation(6, 11),
                // (8,41): error CS0029: Cannot implicitly convert type 'object' to 'int'
                //     static int[] F(IEnumerable s) => [..s];
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "s").WithArguments("object", "int").WithLocation(8, 41));
        }

        [Theory]
        [InlineData("object[]")]
        [InlineData("List<object>")]
        [InlineData("int[]")]
        [InlineData("List<int>")]
        public void SpreadElement_Dynamic_01(string resultType)
        {
            string source = $$"""
                using System.Collections.Generic;
                class Program
                {
                    static {{resultType}} F(List<dynamic> e)
                    {
                        return [..e];
                    }
                    static void Main()
                    {
                        var a = F([1, 2, 3]);
                        a.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(new[] { source, s_collectionExtensions }, references: new[] { CSharpRef }, options: TestOptions.ReleaseExe, expectedOutput: "[1, 2, 3], ");
            if (resultType == "int[]")
            {
                verifier.VerifyIL("Program.F",
                    """
                    {
                      // Code size      129 (0x81)
                      .maxstack  5
                      .locals init (int V_0,
                                    int[] V_1,
                                    System.Collections.Generic.List<dynamic>.Enumerator V_2,
                                    object V_3)
                      IL_0000:  ldarg.0
                      IL_0001:  ldc.i4.0
                      IL_0002:  stloc.0
                      IL_0003:  dup
                      IL_0004:  callvirt   "int System.Collections.Generic.List<dynamic>.Count.get"
                      IL_0009:  newarr     "int"
                      IL_000e:  stloc.1
                      IL_000f:  callvirt   "System.Collections.Generic.List<dynamic>.Enumerator System.Collections.Generic.List<dynamic>.GetEnumerator()"
                      IL_0014:  stloc.2
                      .try
                      {
                        IL_0015:  br.s       IL_0066
                        IL_0017:  ldloca.s   V_2
                        IL_0019:  call       "dynamic System.Collections.Generic.List<dynamic>.Enumerator.Current.get"
                        IL_001e:  stloc.3
                        IL_001f:  ldloc.1
                        IL_0020:  ldloc.0
                        IL_0021:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__0.<>p__0"
                        IL_0026:  brtrue.s   IL_004c
                        IL_0028:  ldc.i4.0
                        IL_0029:  ldtoken    "int"
                        IL_002e:  call       "System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)"
                        IL_0033:  ldtoken    "Program"
                        IL_0038:  call       "System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)"
                        IL_003d:  call       "System.Runtime.CompilerServices.CallSiteBinder Microsoft.CSharp.RuntimeBinder.Binder.Convert(Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags, System.Type, System.Type)"
                        IL_0042:  call       "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>>.Create(System.Runtime.CompilerServices.CallSiteBinder)"
                        IL_0047:  stsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__0.<>p__0"
                        IL_004c:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__0.<>p__0"
                        IL_0051:  ldfld      "System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>>.Target"
                        IL_0056:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__0.<>p__0"
                        IL_005b:  ldloc.3
                        IL_005c:  callvirt   "int System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>.Invoke(System.Runtime.CompilerServices.CallSite, dynamic)"
                        IL_0061:  stelem.i4
                        IL_0062:  ldloc.0
                        IL_0063:  ldc.i4.1
                        IL_0064:  add
                        IL_0065:  stloc.0
                        IL_0066:  ldloca.s   V_2
                        IL_0068:  call       "bool System.Collections.Generic.List<dynamic>.Enumerator.MoveNext()"
                        IL_006d:  brtrue.s   IL_0017
                        IL_006f:  leave.s    IL_007f
                      }
                      finally
                      {
                        IL_0071:  ldloca.s   V_2
                        IL_0073:  constrained. "System.Collections.Generic.List<dynamic>.Enumerator"
                        IL_0079:  callvirt   "void System.IDisposable.Dispose()"
                        IL_007e:  endfinally
                      }
                      IL_007f:  ldloc.1
                      IL_0080:  ret
                    }
                    """);
            }
            else if (resultType == "List<object>")
            {
                verifier.VerifyIL("Program.F",
                    """
                    {
                      // Code size       63 (0x3f)
                      .maxstack  2
                      .locals init (System.Collections.Generic.List<object> V_0,
                                    System.Collections.Generic.List<dynamic>.Enumerator V_1,
                                    object V_2)
                      IL_0000:  ldarg.0
                      IL_0001:  dup
                      IL_0002:  callvirt   "int System.Collections.Generic.List<dynamic>.Count.get"
                      IL_0007:  newobj     "System.Collections.Generic.List<object>..ctor(int)"
                      IL_000c:  stloc.0
                      IL_000d:  callvirt   "System.Collections.Generic.List<dynamic>.Enumerator System.Collections.Generic.List<dynamic>.GetEnumerator()"
                      IL_0012:  stloc.1
                      .try
                      {
                        IL_0013:  br.s       IL_0024
                        IL_0015:  ldloca.s   V_1
                        IL_0017:  call       "dynamic System.Collections.Generic.List<dynamic>.Enumerator.Current.get"
                        IL_001c:  stloc.2
                        IL_001d:  ldloc.0
                        IL_001e:  ldloc.2
                        IL_001f:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                        IL_0024:  ldloca.s   V_1
                        IL_0026:  call       "bool System.Collections.Generic.List<dynamic>.Enumerator.MoveNext()"
                        IL_002b:  brtrue.s   IL_0015
                        IL_002d:  leave.s    IL_003d
                      }
                      finally
                      {
                        IL_002f:  ldloca.s   V_1
                        IL_0031:  constrained. "System.Collections.Generic.List<dynamic>.Enumerator"
                        IL_0037:  callvirt   "void System.IDisposable.Dispose()"
                        IL_003c:  endfinally
                      }
                      IL_003d:  ldloc.0
                      IL_003e:  ret
                    }
                    """);
            }
        }

        [Fact]
        public void SpreadElement_12()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        object x = new[] { 2, 3 };
                        int[] y = [1, ..x];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,25): error CS1579: foreach statement cannot operate on variables of type 'object' because 'object' does not contain a public instance or extension definition for 'GetEnumerator'
                //         int[] y = [1, ..x];
                Diagnostic(ErrorCode.ERR_ForEachMissingMember, "x").WithArguments("object", "GetEnumerator").WithLocation(6, 25));
        }

        [Fact]
        public void SpreadElement_Dynamic_02()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        dynamic x = new[] { 2, 3 };
                        object[] y = [1, ..x];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, references: new[] { CSharpRef }, expectedOutput: "[1, 2, 3], ");
        }

        [WorkItem("https://github.com/dotnet/roslyn/issues/69704")]
        [Fact]
        public void SpreadElement_Dynamic_03()
        {
            string source = """
                class Program
                {
                    static void Main()
                    {
                        dynamic x = new[] { 2, 3 };
                        int[] y = [1, ..x];
                        y.Report();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions }, references: new[] { CSharpRef });
            // https://github.com/dotnet/roslyn/issues/69704: Should compile and run with expectedOutput: "[1, 2, 3], "
            comp.VerifyEmitDiagnostics(
                // 0.cs(6,25): error CS0029: Cannot implicitly convert type 'object' to 'int'
                //         int[] y = [1, ..x];
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "x").WithArguments("object", "int").WithLocation(6, 25));
        }

        [Fact]
        public void SpreadElement_MissingList()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        int[] a = [1, 2];
                        IEnumerable<int> e = a;
                        int[] b;
                        b = [..a];
                        b = [..e];
                    }
                }
                """;

            var comp = CreateCompilation(source);
            comp.MakeTypeMissing(WellKnownType.System_Collections_Generic_List_T);
            comp.VerifyEmitDiagnostics(
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(9, 13),
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(9, 13),
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(9, 13),
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(9, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(10, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(10, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(10, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(10, 13));

            comp = CreateCompilation(source);
            comp.MakeMemberMissing(WellKnownMember.System_Collections_Generic_List_T__ctor);
            comp.VerifyEmitDiagnostics(
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(9, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(10, 13));

            comp = CreateCompilation(source);
            comp.MakeMemberMissing(WellKnownMember.System_Collections_Generic_List_T__ctorInt32);
            comp.VerifyEmitDiagnostics(
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(9, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(10, 13));

            comp = CreateCompilation(source);
            comp.MakeMemberMissing(WellKnownMember.System_Collections_Generic_List_T__Add);
            comp.VerifyEmitDiagnostics(
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(9, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(10, 13));

            comp = CreateCompilation(source);
            comp.MakeMemberMissing(WellKnownMember.System_Collections_Generic_List_T__ToArray);
            comp.VerifyEmitDiagnostics(
                // (9,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         b = [..a];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..a]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(9, 13),
                // (10,13): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         b = [..e];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..e]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(10, 13));
        }

        [Fact]
        public void SpreadElement_KnownLength()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        int[] x = Convert([], [1, 2, 3]);
                        x.Report();
                    }
                    static T[] Convert<T>(List<T> x, List<T> y) => [..x, ..y];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.Convert<T>", """
                {
                  // Code size      137 (0x89)
                  .maxstack  3
                  .locals init (System.Collections.Generic.List<T> V_0,
                                int V_1,
                                T[] V_2,
                                System.Collections.Generic.List<T>.Enumerator V_3,
                                T V_4)
                  IL_0000:  ldarg.0
                  IL_0001:  ldarg.1
                  IL_0002:  stloc.0
                  IL_0003:  ldc.i4.0
                  IL_0004:  stloc.1
                  IL_0005:  dup
                  IL_0006:  callvirt   "int System.Collections.Generic.List<T>.Count.get"
                  IL_000b:  ldloc.0
                  IL_000c:  callvirt   "int System.Collections.Generic.List<T>.Count.get"
                  IL_0011:  add
                  IL_0012:  newarr     "T"
                  IL_0017:  stloc.2
                  IL_0018:  callvirt   "System.Collections.Generic.List<T>.Enumerator System.Collections.Generic.List<T>.GetEnumerator()"
                  IL_001d:  stloc.3
                  .try
                  {
                    IL_001e:  br.s       IL_0036
                    IL_0020:  ldloca.s   V_3
                    IL_0022:  call       "T System.Collections.Generic.List<T>.Enumerator.Current.get"
                    IL_0027:  stloc.s    V_4
                    IL_0029:  ldloc.2
                    IL_002a:  ldloc.1
                    IL_002b:  ldloc.s    V_4
                    IL_002d:  stelem     "T"
                    IL_0032:  ldloc.1
                    IL_0033:  ldc.i4.1
                    IL_0034:  add
                    IL_0035:  stloc.1
                    IL_0036:  ldloca.s   V_3
                    IL_0038:  call       "bool System.Collections.Generic.List<T>.Enumerator.MoveNext()"
                    IL_003d:  brtrue.s   IL_0020
                    IL_003f:  leave.s    IL_004f
                  }
                  finally
                  {
                    IL_0041:  ldloca.s   V_3
                    IL_0043:  constrained. "System.Collections.Generic.List<T>.Enumerator"
                    IL_0049:  callvirt   "void System.IDisposable.Dispose()"
                    IL_004e:  endfinally
                  }
                  IL_004f:  ldloc.0
                  IL_0050:  callvirt   "System.Collections.Generic.List<T>.Enumerator System.Collections.Generic.List<T>.GetEnumerator()"
                  IL_0055:  stloc.3
                  .try
                  {
                    IL_0056:  br.s       IL_006e
                    IL_0058:  ldloca.s   V_3
                    IL_005a:  call       "T System.Collections.Generic.List<T>.Enumerator.Current.get"
                    IL_005f:  stloc.s    V_4
                    IL_0061:  ldloc.2
                    IL_0062:  ldloc.1
                    IL_0063:  ldloc.s    V_4
                    IL_0065:  stelem     "T"
                    IL_006a:  ldloc.1
                    IL_006b:  ldc.i4.1
                    IL_006c:  add
                    IL_006d:  stloc.1
                    IL_006e:  ldloca.s   V_3
                    IL_0070:  call       "bool System.Collections.Generic.List<T>.Enumerator.MoveNext()"
                    IL_0075:  brtrue.s   IL_0058
                    IL_0077:  leave.s    IL_0087
                  }
                  finally
                  {
                    IL_0079:  ldloca.s   V_3
                    IL_007b:  constrained. "System.Collections.Generic.List<T>.Enumerator"
                    IL_0081:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0086:  endfinally
                  }
                  IL_0087:  ldloc.2
                  IL_0088:  ret
                }
                """);
        }

        [Fact]
        public void SpreadElement_KnownLength_EvaluationOrder_01()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static T Identity<T>(string id, T value)
                    {
                        Console.WriteLine(id);
                        return value;
                    }
                    static void Main()
                    {
                        F().Report();
                    }
                    static int[] F()
                    {
                        return [..Identity("A", new[] { 1, 2 }), ..Identity("B", new [,] { { 3, 4 }, { 5, 6 } }), ..Identity("C", new List<int> { 7, 8 })];
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("""
                    A
                    B
                    C
                    [1, 2, 3, 4, 5, 6, 7, 8], 
                    """));
            verifier.VerifyIL("Program.F", """
                {
                  // Code size      300 (0x12c)
                  .maxstack  5
                  .locals init (int[] V_0,
                                int[,] V_1,
                                System.Collections.Generic.List<int> V_2,
                                int V_3,
                                int[] V_4,
                                int[] V_5,
                                int V_6,
                                int V_7,
                                int[,] V_8,
                                int V_9,
                                int V_10,
                                int V_11,
                                System.Collections.Generic.List<int>.Enumerator V_12)
                  IL_0000:  ldstr      "A"
                  IL_0005:  ldc.i4.2
                  IL_0006:  newarr     "int"
                  IL_000b:  dup
                  IL_000c:  ldc.i4.0
                  IL_000d:  ldc.i4.1
                  IL_000e:  stelem.i4
                  IL_000f:  dup
                  IL_0010:  ldc.i4.1
                  IL_0011:  ldc.i4.2
                  IL_0012:  stelem.i4
                  IL_0013:  call       "int[] Program.Identity<int[]>(string, int[])"
                  IL_0018:  stloc.0
                  IL_0019:  ldstr      "B"
                  IL_001e:  ldc.i4.2
                  IL_001f:  ldc.i4.2
                  IL_0020:  newobj     "int[*,*]..ctor"
                  IL_0025:  dup
                  IL_0026:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=16 <PrivateImplementationDetails>.BA7C5EE6E0192FDFE80274584650A2FB8DAE9213BD63AE7B31FE4D088074CB83"
                  IL_002b:  call       "void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)"
                  IL_0030:  call       "int[,] Program.Identity<int[,]>(string, int[,])"
                  IL_0035:  stloc.1
                  IL_0036:  ldstr      "C"
                  IL_003b:  newobj     "System.Collections.Generic.List<int>..ctor()"
                  IL_0040:  dup
                  IL_0041:  ldc.i4.7
                  IL_0042:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_0047:  dup
                  IL_0048:  ldc.i4.8
                  IL_0049:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_004e:  call       "System.Collections.Generic.List<int> Program.Identity<System.Collections.Generic.List<int>>(string, System.Collections.Generic.List<int>)"
                  IL_0053:  stloc.2
                  IL_0054:  ldc.i4.0
                  IL_0055:  stloc.3
                  IL_0056:  ldloc.0
                  IL_0057:  ldlen
                  IL_0058:  conv.i4
                  IL_0059:  ldloc.1
                  IL_005a:  callvirt   "int System.Array.Length.get"
                  IL_005f:  add
                  IL_0060:  ldloc.2
                  IL_0061:  callvirt   "int System.Collections.Generic.List<int>.Count.get"
                  IL_0066:  add
                  IL_0067:  newarr     "int"
                  IL_006c:  stloc.s    V_4
                  IL_006e:  ldloc.0
                  IL_006f:  stloc.s    V_5
                  IL_0071:  ldc.i4.0
                  IL_0072:  stloc.s    V_6
                  IL_0074:  br.s       IL_008d
                  IL_0076:  ldloc.s    V_5
                  IL_0078:  ldloc.s    V_6
                  IL_007a:  ldelem.i4
                  IL_007b:  stloc.s    V_7
                  IL_007d:  ldloc.s    V_4
                  IL_007f:  ldloc.3
                  IL_0080:  ldloc.s    V_7
                  IL_0082:  stelem.i4
                  IL_0083:  ldloc.3
                  IL_0084:  ldc.i4.1
                  IL_0085:  add
                  IL_0086:  stloc.3
                  IL_0087:  ldloc.s    V_6
                  IL_0089:  ldc.i4.1
                  IL_008a:  add
                  IL_008b:  stloc.s    V_6
                  IL_008d:  ldloc.s    V_6
                  IL_008f:  ldloc.s    V_5
                  IL_0091:  ldlen
                  IL_0092:  conv.i4
                  IL_0093:  blt.s      IL_0076
                  IL_0095:  ldloc.1
                  IL_0096:  stloc.s    V_8
                  IL_0098:  ldloc.s    V_8
                  IL_009a:  ldc.i4.0
                  IL_009b:  callvirt   "int System.Array.GetUpperBound(int)"
                  IL_00a0:  stloc.s    V_6
                  IL_00a2:  ldloc.s    V_8
                  IL_00a4:  ldc.i4.1
                  IL_00a5:  callvirt   "int System.Array.GetUpperBound(int)"
                  IL_00aa:  stloc.s    V_7
                  IL_00ac:  ldloc.s    V_8
                  IL_00ae:  ldc.i4.0
                  IL_00af:  callvirt   "int System.Array.GetLowerBound(int)"
                  IL_00b4:  stloc.s    V_9
                  IL_00b6:  br.s       IL_00ed
                  IL_00b8:  ldloc.s    V_8
                  IL_00ba:  ldc.i4.1
                  IL_00bb:  callvirt   "int System.Array.GetLowerBound(int)"
                  IL_00c0:  stloc.s    V_10
                  IL_00c2:  br.s       IL_00e1
                  IL_00c4:  ldloc.s    V_8
                  IL_00c6:  ldloc.s    V_9
                  IL_00c8:  ldloc.s    V_10
                  IL_00ca:  call       "int[*,*].Get"
                  IL_00cf:  stloc.s    V_11
                  IL_00d1:  ldloc.s    V_4
                  IL_00d3:  ldloc.3
                  IL_00d4:  ldloc.s    V_11
                  IL_00d6:  stelem.i4
                  IL_00d7:  ldloc.3
                  IL_00d8:  ldc.i4.1
                  IL_00d9:  add
                  IL_00da:  stloc.3
                  IL_00db:  ldloc.s    V_10
                  IL_00dd:  ldc.i4.1
                  IL_00de:  add
                  IL_00df:  stloc.s    V_10
                  IL_00e1:  ldloc.s    V_10
                  IL_00e3:  ldloc.s    V_7
                  IL_00e5:  ble.s      IL_00c4
                  IL_00e7:  ldloc.s    V_9
                  IL_00e9:  ldc.i4.1
                  IL_00ea:  add
                  IL_00eb:  stloc.s    V_9
                  IL_00ed:  ldloc.s    V_9
                  IL_00ef:  ldloc.s    V_6
                  IL_00f1:  ble.s      IL_00b8
                  IL_00f3:  ldloc.2
                  IL_00f4:  callvirt   "System.Collections.Generic.List<int>.Enumerator System.Collections.Generic.List<int>.GetEnumerator()"
                  IL_00f9:  stloc.s    V_12
                  .try
                  {
                    IL_00fb:  br.s       IL_0110
                    IL_00fd:  ldloca.s   V_12
                    IL_00ff:  call       "int System.Collections.Generic.List<int>.Enumerator.Current.get"
                    IL_0104:  stloc.s    V_7
                    IL_0106:  ldloc.s    V_4
                    IL_0108:  ldloc.3
                    IL_0109:  ldloc.s    V_7
                    IL_010b:  stelem.i4
                    IL_010c:  ldloc.3
                    IL_010d:  ldc.i4.1
                    IL_010e:  add
                    IL_010f:  stloc.3
                    IL_0110:  ldloca.s   V_12
                    IL_0112:  call       "bool System.Collections.Generic.List<int>.Enumerator.MoveNext()"
                    IL_0117:  brtrue.s   IL_00fd
                    IL_0119:  leave.s    IL_0129
                  }
                  finally
                  {
                    IL_011b:  ldloca.s   V_12
                    IL_011d:  constrained. "System.Collections.Generic.List<int>.Enumerator"
                    IL_0123:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0128:  endfinally
                  }
                  IL_0129:  ldloc.s    V_4
                  IL_012b:  ret
                }
                """);
        }

        [Theory]
        [InlineData("int[]", false)]
        [InlineData("int[]", true)]
        [InlineData("System.Collections.Generic.IEnumerable<int>", false)]
        [InlineData("System.Collections.Generic.IEnumerable<int>", true)]
        public void SpreadElement_KnownLength_EvaluationOrder_02(string collectionType, bool includeLength)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                partial class MyCollection<T> : IEnumerable
                {
                    private readonly string _id;
                    private readonly List<T> _list;
                    public MyCollection(string id, T[] items)
                    {
                        _id = id;
                        _list = new();
                        _list.AddRange(items);
                    }
                    public MyEnumerator<T> GetEnumerator()
                    {
                        Console.WriteLine("{0}: GetEnumerator", _id);
                        return new(_id, _list);
                    }
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                class MyEnumerator<T> : IDisposable
                {
                    private readonly string _id;
                    private readonly List<T> _list;
                    private int _index;
                    public MyEnumerator(string id, List<T> list)
                    {
                        _id = id;
                        _list = list;
                        _index = -1;
                    }
                    public bool MoveNext()
                    {
                        if (_index < _list.Count) _index++;
                        return _index < _list.Count;
                    }
                    public T Current
                    {
                        get
                        {
                            Console.WriteLine("{0}: [{1}]", _id, _index);
                            return _list[_index];
                        }
                    }
                    void IDisposable.Dispose()
                    {
                        Console.WriteLine("{0}: Dispose", _id);
                    }
                }
                """;
            if (includeLength)
            {
                sourceA += """
                    partial class MyCollection<T>
                    {
                        public int Length
                        {
                            get
                            {
                                Console.WriteLine("{0}: Length", _id);
                                return _list.Count;
                            }
                        }
                    }
                    """;
            }
            string sourceB = $$"""
                using System;
                partial class Program
                {
                    static T One<T>(string id, T value)
                    {
                        Console.WriteLine("{0}: One", id);
                        return value;
                    }
                    static MyCollection<T> Many<T>(string id, params T[] items)
                    {
                        Console.WriteLine("{0}: Many", id);
                        return new(id, items);
                    }
                    static void Report({{collectionType}} c)
                    {
                        c.Report();
                    }
                }
                """;
            string sourceC;
            string expectedOutput;

            // Maximum number of temporaries.
            sourceC = """
                Report([..Many("A", 1), One("B", 2), ..Many("C", 3, 4), One("D", 5)]);
                """;
            expectedOutput = includeLength ?
                """
                A: Many
                B: One
                C: Many
                A: Length
                C: Length
                A: GetEnumerator
                A: [0]
                A: Dispose
                C: GetEnumerator
                C: [0]
                C: [1]
                C: Dispose
                D: One
                [1, 2, 3, 4, 5], 
                """ :
                """
                A: Many
                A: GetEnumerator
                A: [0]
                A: Dispose
                B: One
                C: Many
                C: GetEnumerator
                C: [0]
                C: [1]
                C: Dispose
                D: One
                [1, 2, 3, 4, 5], 
                """;
            CompileAndVerify(
                new[] { sourceA, sourceB, sourceC, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput(expectedOutput));

            // Exceeded maximum number of temporaries.
            sourceC = """
                Report([..Many("A", 1), One("B", 2), One("C", 3), ..Many("D", 4, 5)]);
                """;
            expectedOutput =
                """
                A: Many
                A: GetEnumerator
                A: [0]
                A: Dispose
                B: One
                C: One
                D: Many
                D: GetEnumerator
                D: [0]
                D: [1]
                D: Dispose
                [1, 2, 3, 4, 5], 
                """;
            CompileAndVerify(
                new[] { sourceA, sourceB, sourceC, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput(expectedOutput));
        }

        [Fact]
        public void KnownLength_List()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        var x = F0();
                        var y = F1();
                        var z = F2(y);
                        z.Report();
                    }
                    static List<int> F0() => [];
                    static List<int> F1() => [1, 2, 3];
                    static List<object> F2(List<int> c) => [4, ..c];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: "[4, 1, 2, 3], ");
            verifier.VerifyIL("Program.F0", """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  newobj     "System.Collections.Generic.List<int>..ctor()"
                  IL_0005:  ret
                }
                """);
            verifier.VerifyIL("Program.F1", """
                {
                  // Code size       28 (0x1c)
                  .maxstack  3
                  IL_0000:  ldc.i4.3
                  IL_0001:  newobj     "System.Collections.Generic.List<int>..ctor(int)"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.1
                  IL_0008:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_000d:  dup
                  IL_000e:  ldc.i4.2
                  IL_000f:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_0014:  dup
                  IL_0015:  ldc.i4.3
                  IL_0016:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_001b:  ret
                }
                """);
            verifier.VerifyIL("Program.F2", """
                {
                  // Code size       88 (0x58)
                  .maxstack  2
                  .locals init (object V_0,
                                System.Collections.Generic.List<int> V_1,
                                System.Collections.Generic.List<object> V_2,
                                System.Collections.Generic.List<int>.Enumerator V_3,
                                int V_4)
                  IL_0000:  ldc.i4.4
                  IL_0001:  box        "int"
                  IL_0006:  stloc.0
                  IL_0007:  ldarg.0
                  IL_0008:  stloc.1
                  IL_0009:  ldc.i4.1
                  IL_000a:  ldloc.1
                  IL_000b:  callvirt   "int System.Collections.Generic.List<int>.Count.get"
                  IL_0010:  add
                  IL_0011:  newobj     "System.Collections.Generic.List<object>..ctor(int)"
                  IL_0016:  stloc.2
                  IL_0017:  ldloc.2
                  IL_0018:  ldloc.0
                  IL_0019:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_001e:  ldloc.1
                  IL_001f:  callvirt   "System.Collections.Generic.List<int>.Enumerator System.Collections.Generic.List<int>.GetEnumerator()"
                  IL_0024:  stloc.3
                  .try
                  {
                    IL_0025:  br.s       IL_003d
                    IL_0027:  ldloca.s   V_3
                    IL_0029:  call       "int System.Collections.Generic.List<int>.Enumerator.Current.get"
                    IL_002e:  stloc.s    V_4
                    IL_0030:  ldloc.2
                    IL_0031:  ldloc.s    V_4
                    IL_0033:  box        "int"
                    IL_0038:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                    IL_003d:  ldloca.s   V_3
                    IL_003f:  call       "bool System.Collections.Generic.List<int>.Enumerator.MoveNext()"
                    IL_0044:  brtrue.s   IL_0027
                    IL_0046:  leave.s    IL_0056
                  }
                  finally
                  {
                    IL_0048:  ldloca.s   V_3
                    IL_004a:  constrained. "System.Collections.Generic.List<int>.Enumerator"
                    IL_0050:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0055:  endfinally
                  }
                  IL_0056:  ldloc.2
                  IL_0057:  ret
                }
                """);
        }

        [Fact]
        public void KnownLength_List_MissingConstructor()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        List<int> x = [];
                        List<int> y = [1, 2, 3];
                        List<object> z = [4, ..y];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeMemberMissing(WellKnownMember.System_Collections_Generic_List_T__ctorInt32);
            comp.VerifyEmitDiagnostics(
                // (6,23): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         List<int> x = [];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(6, 23),
                // (7,23): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         List<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[1, 2, 3]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(7, 23),
                // (8,26): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         List<object> z = [4, ..y];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[4, ..y]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(8, 26));
        }

        [Fact]
        public void SpreadElement_LengthSideEffects()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                struct S : IEnumerable<object>
                {
                    internal static int TotalUse;
                    internal int InstanceUse;
                    private object[] _items;
                    public S(object[] items) { _items = items; }
                    public int Length => GetLength();
                    private int GetLength()
                    {
                        Console.WriteLine("Length");
                        InstanceUse++;
                        TotalUse++;
                        return _items.Length;
                    }
                    public IEnumerator<object> GetEnumerator()
                    {
                        foreach (var item in _items) yield return item;
                    }
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        var s = new S(new object[] { 1, 2, 3 });
                        Console.WriteLine("Before: {0}, {1}", s.InstanceUse, S.TotalUse);
                        object[] a = [..s];
                        Console.WriteLine("After: {0}, {1}", s.InstanceUse, S.TotalUse);
                        a.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("""
                    Before: 0, 0
                    Length
                    After: 0, 1
                    [1, 2, 3], 
                    """));
        }

        [Fact]
        public void SpreadElement_LengthObsolete()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                class MyCollection : IEnumerable
                {
                    private object[] _items;
                    public MyCollection(object[] items) { _items = items; }
                    [Obsolete(null, error: true)] public int Count => _items.Length;
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = [];
                        MyCollection y = new([1, 2, 3]);
                        object[] z = [..x, ..y];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (17,30): warning CS0612: 'MyCollection.Count' is obsolete
                //         object[] z = [..x, ..y];
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "y").WithArguments("MyCollection.Count").WithLocation(17, 30));
        }

        [Fact]
        public void SpreadElement_LengthUseSiteError()
        {
            string assemblyA = GetUniqueName();
            string sourceA = """
                public class A
                {
                }
                """;
            var comp = CreateCompilation(sourceA, assemblyName: assemblyA);
            var refA = comp.EmitToImageReference();

            string sourceB = """
                using System.Collections;
                public class B : A
                {
                    private object[] _items;
                    public B(object[] items) { _items = items; }
                    public IEnumerator GetEnumerator() => _items.GetEnumerator();
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA });
            var refB = comp.EmitToImageReference();

            string sourceC = """
                class C
                {
                    static object[] F(B b) => [..b];
                }
                """;
            comp = CreateCompilation(sourceC, references: new[] { refA, refB });
            comp.VerifyEmitDiagnostics();

            comp = CreateCompilation(sourceC, references: new[] { refB });
            comp.VerifyEmitDiagnostics(
                // (3,34): error CS0012: The type 'A' is defined in an assembly that is not referenced. You must add a reference to assembly '421e2b62-28da-4a54-9838-ca85a8922250, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
                //     static object[] F(B b) => [..b];
                Diagnostic(ErrorCode.ERR_NoTypeDef, "b").WithArguments("A", $"{assemblyA}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").WithLocation(3, 34),
                // (3,34): error CS0012: The type 'A' is defined in an assembly that is not referenced. You must add a reference to assembly '421e2b62-28da-4a54-9838-ca85a8922250, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
                //     static object[] F(B b) => [..b];
                Diagnostic(ErrorCode.ERR_NoTypeDef, "b").WithArguments("A", $"{assemblyA}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").WithLocation(3, 34),
                // (3,34): error CS0012: The type 'A' is defined in an assembly that is not referenced. You must add a reference to assembly '421e2b62-28da-4a54-9838-ca85a8922250, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null'.
                //     static object[] F(B b) => [..b];
                Diagnostic(ErrorCode.ERR_NoTypeDef, "b").WithArguments("A", $"{assemblyA}, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null").WithLocation(3, 34));
        }

        [CombinatorialData]
        [Theory]
        public void ArrayEmpty_01([CombinatorialValues(TargetFramework.Mscorlib45Extended, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            if (!ExecutionConditionUtil.IsCoreClr && targetFramework == TargetFramework.Net80) return;

            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        EmptyArray<object>().Report();
                        EmptyIEnumerable<object>().Report();
                        EmptyICollection<object>().Report();
                        EmptyIList<object>().Report();
                        EmptyIReadOnlyCollection<object>().Report();
                        EmptyIReadOnlyList<object>().Report();
                    }
                    static T[] EmptyArray<T>() => [];
                    static IEnumerable<T> EmptyIEnumerable<T>() => [];
                    static ICollection<T> EmptyICollection<T>() => [];
                    static IList<T> EmptyIList<T>() => [];
                    static IReadOnlyCollection<T> EmptyIReadOnlyCollection<T>() => [];
                    static IReadOnlyList<T> EmptyIReadOnlyList<T>() => [];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                expectedOutput: "[], [], [], [], [], [], ");

            string expectedIL = (targetFramework == TargetFramework.Mscorlib45Extended) ?
                """
                {
                  // Code size        7 (0x7)
                  .maxstack  1
                  IL_0000:  ldc.i4.0
                  IL_0001:  newarr     "T"
                  IL_0006:  ret
                }
                """ :
                """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  call       "T[] System.Array.Empty<T>()"
                  IL_0005:  ret
                }
                """;
            verifier.VerifyIL("Program.EmptyArray<T>", expectedIL);
            verifier.VerifyIL("Program.EmptyIEnumerable<T>", expectedIL);
            verifier.VerifyIL("Program.EmptyIReadOnlyCollection<T>", expectedIL);
            verifier.VerifyIL("Program.EmptyIReadOnlyList<T>", expectedIL);

            expectedIL =
                """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  newobj     "System.Collections.Generic.List<T>..ctor()"
                  IL_0005:  ret
                }
                """;
            verifier.VerifyIL("Program.EmptyICollection<T>", expectedIL);
            verifier.VerifyIL("Program.EmptyIList<T>", expectedIL);
        }

        [CombinatorialData]
        [Theory]
        public void ArrayEmpty_02([CombinatorialValues(TargetFramework.Mscorlib45Extended, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            if (!ExecutionConditionUtil.IsCoreClr && targetFramework == TargetFramework.Net80) return;

            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        EmptyArray().Report();
                        EmptyIEnumerable().Report();
                        EmptyICollection().Report();
                        EmptyIList().Report();
                        EmptyIReadOnlyCollection().Report();
                        EmptyIReadOnlyList().Report();
                    }
                    static string[] EmptyArray() => [];
                    static IEnumerable<string> EmptyIEnumerable() => [];
                    static ICollection<string> EmptyICollection() => [];
                    static IList<string> EmptyIList() => [];
                    static IReadOnlyCollection<string> EmptyIReadOnlyCollection() => [];
                    static IReadOnlyList<string> EmptyIReadOnlyList() => [];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                expectedOutput: "[], [], [], [], [], [], ");

            string expectedIL = (targetFramework == TargetFramework.Mscorlib45Extended) ?
                """
                {
                  // Code size        7 (0x7)
                  .maxstack  1
                  IL_0000:  ldc.i4.0
                  IL_0001:  newarr     "string"
                  IL_0006:  ret
                }
                """ :
                """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  call       "string[] System.Array.Empty<string>()"
                  IL_0005:  ret
                }
                """;
            verifier.VerifyIL("Program.EmptyArray", expectedIL);
            verifier.VerifyIL("Program.EmptyIEnumerable", expectedIL);
            verifier.VerifyIL("Program.EmptyIReadOnlyCollection", expectedIL);
            verifier.VerifyIL("Program.EmptyIReadOnlyList", expectedIL);

            expectedIL =
                """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  newobj     "System.Collections.Generic.List<string>..ctor()"
                  IL_0005:  ret
                }
                """;
            verifier.VerifyIL("Program.EmptyICollection", expectedIL);
            verifier.VerifyIL("Program.EmptyIList", expectedIL);
        }

        [Fact]
        public void ArrayEmpty_PointerElementType()
        {
            string source = """
                unsafe class Program
                {
                    static void Main()
                    {
                        EmptyArray().Report();
                        EmptyNestedArray().Report();
                    }
                    static void*[] EmptyArray() => [];
                    static void*[][] EmptyNestedArray() => [];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                options: TestOptions.UnsafeReleaseExe,
                verify: Verification.FailsPEVerify,
                expectedOutput: "[], [], ");
            verifier.VerifyIL("Program.EmptyArray",
                """
                {
                  // Code size        7 (0x7)
                  .maxstack  1
                  IL_0000:  ldc.i4.0
                  IL_0001:  newarr     "void*"
                  IL_0006:  ret
                }
                """);
            verifier.VerifyIL("Program.EmptyNestedArray",
                """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  call       "void*[][] System.Array.Empty<void*[]>()"
                  IL_0005:  ret
                }
                """);
        }

        [Fact]
        public void ArrayEmpty_MissingMethod()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        int[] x = [];
                        IEnumerable<int> y = [];
                        x.Report();
                        y.Report();
                    }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }, options: TestOptions.ReleaseExe);
            var verifier = CompileAndVerify(comp, expectedOutput: "[], [], ");
            verifier.VerifyIL("Program.Main",
                """
                {
                  // Code size       25 (0x19)
                  .maxstack  2
                  .locals init (System.Collections.Generic.IEnumerable<int> V_0) //y
                  IL_0000:  call       "int[] System.Array.Empty<int>()"
                  IL_0005:  call       "int[] System.Array.Empty<int>()"
                  IL_000a:  stloc.0
                  IL_000b:  ldc.i4.0
                  IL_000c:  call       "void CollectionExtensions.Report(object, bool)"
                  IL_0011:  ldloc.0
                  IL_0012:  ldc.i4.0
                  IL_0013:  call       "void CollectionExtensions.Report(object, bool)"
                  IL_0018:  ret
                }
                """);

            comp = CreateCompilation(new[] { source, s_collectionExtensions }, options: TestOptions.ReleaseExe);
            comp.MakeMemberMissing(WellKnownMember.System_Array__Empty);
            verifier = CompileAndVerify(comp, expectedOutput: "[], [], ");
            verifier.VerifyIL("Program.Main",
                """
                {
                  // Code size       27 (0x1b)
                  .maxstack  3
                  .locals init (int[] V_0) //x
                  IL_0000:  ldc.i4.0
                  IL_0001:  newarr     "int"
                  IL_0006:  stloc.0
                  IL_0007:  ldc.i4.0
                  IL_0008:  newarr     "int"
                  IL_000d:  ldloc.0
                  IL_000e:  ldc.i4.0
                  IL_000f:  call       "void CollectionExtensions.Report(object, bool)"
                  IL_0014:  ldc.i4.0
                  IL_0015:  call       "void CollectionExtensions.Report(object, bool)"
                  IL_001a:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void SynthesizedReadOnlyArray([CombinatorialValues("IEnumerable<object>", "IReadOnlyCollection<object>", "IReadOnlyList<object>")] string targetType)
        {
            string source = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using static System.Console;
                class Program
                {
                    static void Main()
                    {
                        Report([1, 2, null]);
                    }
                    static void Report({{targetType}} x)
                    {
                        Write("IEnumerable.GetEnumerator(): ");
                        ((IEnumerable)x).Report(includeType: true);
                        WriteLine();
                        Write("IEnumerable<object>.GetEnumerator(): ");
                        ((IEnumerable<object>)x).Report(includeType: true);
                        WriteLine();
                        WriteLine("IReadOnlyCollection<object>.Count: {0}", ((IReadOnlyCollection<object>)x).Count);
                        WriteLine("IReadOnlyList<object>.this[1]: {0}", ((IReadOnlyList<object>)x)[1]);
                        WriteLine("ICollection<object>.Count: {0}", ((ICollection<object>)x).Count);
                        WriteLine("ICollection<object>.IsReadOnly: {0}", ((ICollection<object>)x).IsReadOnly);
                        WriteLine("ICollection<object>.Add(-1): {0}", Invoke(() => ((ICollection<object>)x).Add(-1)));
                        WriteLine("ICollection<object>.Clear(): {0}", Invoke(() => ((ICollection<object>)x).Clear()));
                        WriteLine("ICollection<object>.Contains(2): {0}", ((ICollection<object>)x).Contains(2));
                        Write("ICollection<object>.CopyTo(..., 0): ");
                        object[] a = new object[3];
                        ((ICollection<object>)x).CopyTo(a, 0);
                        a.Report(includeType: true);
                        WriteLine();
                        WriteLine("ICollection<object>.Remove(2): {0}", Invoke(() => ((ICollection<object>)x).Remove(2)));
                        WriteLine("IList<object>.this[1].get: {0}", ((IList<object>)x)[1]);
                        WriteLine("IList<object>.this[1].set: {0}", Invoke(() => ((IList<object>)x)[1] = -1));
                        WriteLine("IList<object>.IndexOf(2): {0}", ((IList<object>)x).IndexOf(2));
                        WriteLine("IList<object>.Insert(1, -1): {0}", Invoke(() => ((IList<object>)x).Insert(1, -1)));
                        WriteLine("IList<object>.RemoveAt(1): {0}", Invoke(() => ((IList<object>)x).RemoveAt(1)));
                    }
                    static string Invoke(Action a)
                    {
                        try
                        {
                            a();
                            return "completed";
                        }
                        catch (Exception e)
                        {
                            return e.GetType().FullName;
                        }
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                symbolValidator: module =>
                {
                    var synthesizedType = module.GlobalNamespace.GetTypeMember("<>z__ReadOnlyArray");
                    Assert.Equal("<>z__ReadOnlyArray<T>", synthesizedType.ToTestDisplayString());
                    Assert.Equal("<>z__ReadOnlyArray`1", synthesizedType.MetadataName);
                },
                expectedOutput: """
                    IEnumerable.GetEnumerator(): (<>z__ReadOnlyArray<System.Object>) [1, 2, null], 
                    IEnumerable<object>.GetEnumerator(): (<>z__ReadOnlyArray<System.Object>) [1, 2, null], 
                    IReadOnlyCollection<object>.Count: 3
                    IReadOnlyList<object>.this[1]: 2
                    ICollection<object>.Count: 3
                    ICollection<object>.IsReadOnly: True
                    ICollection<object>.Add(-1): System.NotSupportedException
                    ICollection<object>.Clear(): System.NotSupportedException
                    ICollection<object>.Contains(2): True
                    ICollection<object>.CopyTo(..., 0): (System.Object[]) [1, 2, null], 
                    ICollection<object>.Remove(2): System.NotSupportedException
                    IList<object>.this[1].get: 2
                    IList<object>.this[1].set: System.NotSupportedException
                    IList<object>.IndexOf(2): 1
                    IList<object>.Insert(1, -1): System.NotSupportedException
                    IList<object>.RemoveAt(1): System.NotSupportedException
                    """);

            string expectedNotSupportedIL = """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  newobj     "System.NotSupportedException..ctor()"
                  IL_0005:  throw
                }
                """;

            verifier.VerifyIL("<>z__ReadOnlyArray<T>..ctor(T[])", """
                {
                  // Code size       14 (0xe)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  call       "object..ctor()"
                  IL_0006:  ldarg.0
                  IL_0007:  ldarg.1
                  IL_0008:  stfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_000d:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.IEnumerable.GetEnumerator()", """
                {
                  // Code size       12 (0xc)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  callvirt   "System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()"
                  IL_000b:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IEnumerable<T>.GetEnumerator()", """
                {
                  // Code size       12 (0xc)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  callvirt   "System.Collections.Generic.IEnumerator<T> System.Collections.Generic.IEnumerable<T>.GetEnumerator()"
                  IL_000b:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IReadOnlyCollection<T>.get_Count()", """
                {
                  // Code size        9 (0x9)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  ldlen
                  IL_0007:  conv.i4
                  IL_0008:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IReadOnlyList<T>.get_Item(int)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  ldelem     "T"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.ICollection<T>.get_IsReadOnly()", """
                {
                  // Code size        2 (0x2)
                  .maxstack  1
                  IL_0000:  ldc.i4.1
                  IL_0001:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.ICollection<T>.get_Count()", """
                {
                  // Code size        9 (0x9)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  ldlen
                  IL_0007:  conv.i4
                  IL_0008:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.ICollection<T>.Add(T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.ICollection<T>.Clear()", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.ICollection<T>.Contains(T)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  callvirt   "bool System.Collections.Generic.ICollection<T>.Contains(T)"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.ICollection<T>.CopyTo(T[], int)", """
                {
                  // Code size       14 (0xe)
                  .maxstack  3
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  ldarg.2
                  IL_0008:  callvirt   "void System.Collections.Generic.ICollection<T>.CopyTo(T[], int)"
                  IL_000d:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.ICollection<T>.Remove(T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IList<T>.get_Item(int)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  ldelem     "T"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IList<T>.set_Item(int, T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IList<T>.IndexOf(T)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "T[] <>z__ReadOnlyArray<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  callvirt   "int System.Collections.Generic.IList<T>.IndexOf(T)"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IList<T>.Insert(int, T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyArray<T>.System.Collections.Generic.IList<T>.RemoveAt(int)", expectedNotSupportedIL);
        }

        [CombinatorialData]
        [Theory]
        public void SynthesizedReadOnlyList_01([CombinatorialValues("IEnumerable<T>", "IReadOnlyCollection<T>", "IReadOnlyList<T>")] string targetType)
        {
            string source = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using static System.Console;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = EmptyEnumerable<int>();
                        object[] y = [1, 2, null];
                        Report<int>([..x]);
                        Report<object>([..x, ..y]);
                    }
                    static IEnumerable<T> EmptyEnumerable<T>()
                    {
                        yield break;
                    }
                    static void Report<T>({{targetType}} x)
                    {
                        int length = ((IReadOnlyCollection<T>)x).Count;
                        Write("IEnumerable.GetEnumerator(): ");
                        ((IEnumerable)x).Report(includeType: true);
                        WriteLine();
                        Write("IEnumerable<T>.GetEnumerator(): ");
                        ((IEnumerable<T>)x).Report(includeType: true);
                        WriteLine();
                        WriteLine("IReadOnlyCollection<T>.Count: {0}", ((IReadOnlyCollection<T>)x).Count);
                        if (length > 1) WriteLine("IReadOnlyList<T>.this[1]: {0}", ((IReadOnlyList<T>)x)[1]);
                        WriteLine("ICollection<T>.Count: {0}", ((ICollection<T>)x).Count);
                        WriteLine("ICollection<T>.IsReadOnly: {0}", ((ICollection<T>)x).IsReadOnly);
                        WriteLine("ICollection<T>.Add(default): {0}", Invoke(() => ((ICollection<T>)x).Add(default)));
                        WriteLine("ICollection<T>.Clear(): {0}", Invoke(() => ((ICollection<T>)x).Clear()));
                        WriteLine("ICollection<T>.Contains(default): {0}", ((ICollection<T>)x).Contains(default));
                        Write("ICollection<T>.CopyTo(..., 0): ");
                        T[] a = new T[length];
                        ((ICollection<T>)x).CopyTo(a, 0);
                        a.Report(includeType: true);
                        WriteLine();
                        WriteLine("ICollection<T>.Remove(default): {0}", Invoke(() => ((ICollection<T>)x).Remove(default)));
                        if (length > 1) WriteLine("IList<T>.this[1].get: {0}", ((IList<T>)x)[1]);
                        if (length > 1) WriteLine("IList<T>.this[1].set: {0}", Invoke(() => ((IList<T>)x)[1] = default));
                        WriteLine("IList<T>.IndexOf(default): {0}", ((IList<T>)x).IndexOf(default));
                        WriteLine("IList<T>.Insert(0, default): {0}", Invoke(() => ((IList<T>)x).Insert(0, default)));
                        if (length > 1) WriteLine("IList<T>.RemoveAt(1): {0}", Invoke(() => ((IList<T>)x).RemoveAt(1)));
                    }
                    static string Invoke(Action a)
                    {
                        try
                        {
                            a();
                            return "completed";
                        }
                        catch (Exception e)
                        {
                            return e.GetType().FullName;
                        }
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                expectedOutput: """
                    IEnumerable.GetEnumerator(): (<>z__ReadOnlyList<System.Int32>) [], 
                    IEnumerable<T>.GetEnumerator(): (<>z__ReadOnlyList<System.Int32>) [], 
                    IReadOnlyCollection<T>.Count: 0
                    ICollection<T>.Count: 0
                    ICollection<T>.IsReadOnly: True
                    ICollection<T>.Add(default): System.NotSupportedException
                    ICollection<T>.Clear(): System.NotSupportedException
                    ICollection<T>.Contains(default): False
                    ICollection<T>.CopyTo(..., 0): (System.Int32[]) [], 
                    ICollection<T>.Remove(default): System.NotSupportedException
                    IList<T>.IndexOf(default): -1
                    IList<T>.Insert(0, default): System.NotSupportedException
                    IEnumerable.GetEnumerator(): (<>z__ReadOnlyList<System.Object>) [1, 2, null], 
                    IEnumerable<T>.GetEnumerator(): (<>z__ReadOnlyList<System.Object>) [1, 2, null], 
                    IReadOnlyCollection<T>.Count: 3
                    IReadOnlyList<T>.this[1]: 2
                    ICollection<T>.Count: 3
                    ICollection<T>.IsReadOnly: True
                    ICollection<T>.Add(default): System.NotSupportedException
                    ICollection<T>.Clear(): System.NotSupportedException
                    ICollection<T>.Contains(default): True
                    ICollection<T>.CopyTo(..., 0): (System.Object[]) [1, 2, null], 
                    ICollection<T>.Remove(default): System.NotSupportedException
                    IList<T>.this[1].get: 2
                    IList<T>.this[1].set: System.NotSupportedException
                    IList<T>.IndexOf(default): 2
                    IList<T>.Insert(0, default): System.NotSupportedException
                    IList<T>.RemoveAt(1): System.NotSupportedException
                    """);

            string expectedNotSupportedIL = """
                {
                  // Code size        6 (0x6)
                  .maxstack  1
                  IL_0000:  newobj     "System.NotSupportedException..ctor()"
                  IL_0005:  throw
                }
                """;

            verifier.VerifyIL("<>z__ReadOnlyList<T>..ctor(System.Collections.Generic.List<T>)", """
                {
                  // Code size       14 (0xe)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  call       "object..ctor()"
                  IL_0006:  ldarg.0
                  IL_0007:  ldarg.1
                  IL_0008:  stfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_000d:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.IEnumerable.GetEnumerator()", """
                {
                  // Code size       12 (0xc)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  callvirt   "System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()"
                  IL_000b:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IEnumerable<T>.GetEnumerator()", """
                {
                  // Code size       12 (0xc)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  callvirt   "System.Collections.Generic.IEnumerator<T> System.Collections.Generic.IEnumerable<T>.GetEnumerator()"
                  IL_000b:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IReadOnlyCollection<T>.get_Count()", """
                {
                  // Code size       12 (0xc)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  callvirt   "int System.Collections.Generic.List<T>.Count.get"
                  IL_000b:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IReadOnlyList<T>.get_Item(int)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  callvirt   "T System.Collections.Generic.List<T>.this[int].get"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.ICollection<T>.get_IsReadOnly()", """
                {
                  // Code size        2 (0x2)
                  .maxstack  1
                  IL_0000:  ldc.i4.1
                  IL_0001:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.ICollection<T>.get_Count()", """
                {
                  // Code size       12 (0xc)
                  .maxstack  1
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  callvirt   "int System.Collections.Generic.List<T>.Count.get"
                  IL_000b:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.ICollection<T>.Add(T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.ICollection<T>.Clear()", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.ICollection<T>.Contains(T)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  callvirt   "bool System.Collections.Generic.List<T>.Contains(T)"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.ICollection<T>.CopyTo(T[], int)", """
                {
                  // Code size       14 (0xe)
                  .maxstack  3
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  ldarg.2
                  IL_0008:  callvirt   "void System.Collections.Generic.List<T>.CopyTo(T[], int)"
                  IL_000d:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.ICollection<T>.Remove(T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IList<T>.get_Item(int)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  callvirt   "T System.Collections.Generic.List<T>.this[int].get"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IList<T>.set_Item(int, T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IList<T>.IndexOf(T)", """
                {
                  // Code size       13 (0xd)
                  .maxstack  2
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "System.Collections.Generic.List<T> <>z__ReadOnlyList<T>._items"
                  IL_0006:  ldarg.1
                  IL_0007:  callvirt   "int System.Collections.Generic.List<T>.IndexOf(T)"
                  IL_000c:  ret
                }
                """);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IList<T>.Insert(int, T)", expectedNotSupportedIL);
            verifier.VerifyIL("<>z__ReadOnlyList<T>.System.Collections.Generic.IList<T>.RemoveAt(int)", expectedNotSupportedIL);
        }

        [Fact]
        public void SynthesizedReadOnlyList_02()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        foreach (var i in F(1, 2))
                        {
                            Console.Write("{0}, ", i);
                        }
                    }
                    static IEnumerable<int> F(int x, int y) => [x, y];
                }
                """;
            CompileAndVerify(
                source,
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("1, 2, "));
        }

        [Fact]
        public void SynthesizedReadOnlyList_03()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        foreach (var i in F(1, 2, new[] { 3 }))
                        {
                            Console.Write("{0}, ", i);
                        }
                    }
                    static IEnumerable<int> F(int x, int y, IEnumerable<int> e) => [x, y, ..e];
                }
                """;
            CompileAndVerify(
                source,
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("1, 2, 3, "));
        }

        // Compare members of synthesized types to a similar type from source.
        [Fact]
        public void SynthesizedReadOnlyList_Members()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                internal sealed class ReadOnlyArray<T> :
                    IEnumerable<T>,
                    IReadOnlyCollection<T>,
                    IReadOnlyList<T>,
                    ICollection<T>,
                    IList<T>
                {
                    private readonly T[] _items;
                    public ReadOnlyArray(T[] items) { _items = items; }
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotSupportedException();
                    int IReadOnlyCollection<T>.Count => _items.Length;
                    T IReadOnlyList<T>.this[int index] => _items[index];
                    int ICollection<T>.Count => _items.Length;
                    bool ICollection<T>.IsReadOnly => true;
                    void ICollection<T>.Add(T item) => throw new NotSupportedException();
                    void ICollection<T>.Clear() => throw new NotSupportedException();
                    bool ICollection<T>.Contains(T item) => throw new NotSupportedException();
                    void ICollection<T>.CopyTo(T[] array, int arrayIndex) { }
                    T IList<T>.this[int index]
                    {
                        get => _items[index];
                        set => throw new NotSupportedException();
                    }
                    int IList<T>.IndexOf(T t) => Array.IndexOf<T>(_items, t);
                    void IList<T>.Insert(int index, T item) => throw new NotSupportedException();
                    bool ICollection<T>.Remove(T item) => throw new NotSupportedException();
                    void IList<T>.RemoveAt(int index) => throw new NotSupportedException();
                }
                """;
            // Collection expressions below ensure the types are synthesized.
            string sourceB = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = [0];
                        IEnumerable<int> y = [..x];
                    }
                }
                """;

            var verifier = CompileAndVerify(
                new[] { sourceA, sourceB },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify);

            var sourceType = ((CSharpCompilation)verifier.Compilation).GetMember<NamedTypeSymbol>("ReadOnlyArray");
            verifier.TestData.TryGetMethodData("<>z__ReadOnlyArray<T>..ctor(T[])", out var arrayMemberData);
            verifier.TestData.TryGetMethodData("<>z__ReadOnlyList<T>..ctor(System.Collections.Generic.List<T>)", out var listMemberData);

            compareTypes(sourceType, ((MethodSymbol)arrayMemberData.Method).ContainingType);
            compareTypes(sourceType, ((MethodSymbol)listMemberData.Method).ContainingType);

            static void compareTypes(NamedTypeSymbol sourceType, NamedTypeSymbol synthesizedType)
            {
                compareMembers(sourceType, synthesizedType, "System.Collections.Generic.ICollection<T>.IsReadOnly");
                compareMembers(sourceType, synthesizedType, "System.Collections.Generic.ICollection<T>.get_IsReadOnly");
                compareMembers(sourceType, synthesizedType, "System.Collections.Generic.ICollection<T>.Contains");
                compareMembers(sourceType, synthesizedType, "System.Collections.Generic.IList<T>.this[]");
                compareMembers(sourceType, synthesizedType, "System.Collections.Generic.IList<T>.get_Item");
                compareMembers(sourceType, synthesizedType, "System.Collections.Generic.IList<T>.set_Item");
            }

            static void compareMembers(NamedTypeSymbol sourceType, NamedTypeSymbol synthesizedType, string memberName)
            {
                var sourceMember = sourceType.GetMembers(memberName).Single();
                var synthesizedMember = synthesizedType.GetMembers(memberName).Single();
                Assert.Equal(sourceMember.IsAbstract, synthesizedMember.IsAbstract);
                Assert.Equal(sourceMember.IsVirtual, synthesizedMember.IsVirtual);
                Assert.Equal(sourceMember.IsOverride, synthesizedMember.IsOverride);
            }
        }

        [Theory]
        [InlineData(SpecialType.System_Collections_IEnumerable, "System.Collections.IEnumerable")]
        [InlineData(SpecialType.System_Collections_Generic_IEnumerable_T, "System.Collections.Generic.IEnumerable`1")]
        [InlineData(SpecialType.System_Collections_Generic_IReadOnlyCollection_T, "System.Collections.Generic.IReadOnlyCollection`1")]
        [InlineData(SpecialType.System_Collections_Generic_IReadOnlyList_T, "System.Collections.Generic.IReadOnlyList`1")]
        [InlineData(SpecialType.System_Collections_Generic_ICollection_T, "System.Collections.Generic.ICollection`1")]
        [InlineData(SpecialType.System_Collections_Generic_IList_T, "System.Collections.Generic.IList`1")]
        public void SynthesizedReadOnlyList_MissingSpecialTypes(SpecialType missingType, string missingTypeName)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = [0];
                        IEnumerable<int> y = [..x];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeTypeMissing(missingType);
            comp.VerifyEmitDiagnostics(
                // (6,30): error CS0518: Predefined type 'System.Collections.IEnumerable' is not defined or imported
                //         IEnumerable<int> x = [0];
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "[0]").WithArguments(missingTypeName).WithLocation(6, 30),
                // (7,30): error CS0518: Predefined type 'System.Collections.IEnumerable' is not defined or imported
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "[..x]").WithArguments(missingTypeName).WithLocation(7, 30));
        }

        [Theory]
        [InlineData((int)SpecialMember.System_Collections_IEnumerable__GetEnumerator, "System.Collections.IEnumerable", "GetEnumerator")]
        [InlineData((int)SpecialMember.System_Collections_Generic_IEnumerable_T__GetEnumerator, "System.Collections.Generic.IEnumerable`1", "GetEnumerator")]
        public void SynthesizedReadOnlyList_MissingSpecialMembers(int missingMember, string missingMemberTypeName, string missingMemberName)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = [0];
                        IEnumerable<int> y = [..x];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeMemberMissing((SpecialMember)missingMember);
            comp.VerifyEmitDiagnostics(
                // (6,30): error CS0656: Missing compiler required member 'System.Collections.IEnumerable.GetEnumerator'
                //         IEnumerable<int> x = [0];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[0]").WithArguments(missingMemberTypeName, missingMemberName).WithLocation(6, 30),
                // (7,30): error CS0656: Missing compiler required member 'System.Collections.IEnumerable.GetEnumerator'
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..x]").WithArguments(missingMemberTypeName, missingMemberName).WithLocation(7, 30));
        }

        [Fact]
        public void SynthesizedReadOnlyList_MissingWellKnownTypes()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = [0];
                        IEnumerable<int> y = [..x];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeTypeMissing(WellKnownType.System_Collections_Generic_List_T);
            comp.VerifyEmitDiagnostics(
                // (6,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         IEnumerable<int> x = [0];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[0]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(6, 30),
                // (6,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         IEnumerable<int> x = [0];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[0]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(6, 30),
                // (6,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         IEnumerable<int> x = [0];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[0]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(6, 30),
                // (6,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         IEnumerable<int> x = [0];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[0]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(6, 30),
                // (7,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..x]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(7, 30),
                // (7,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1..ctor'
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..x]").WithArguments("System.Collections.Generic.List`1", ".ctor").WithLocation(7, 30),
                // (7,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.Add'
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..x]").WithArguments("System.Collections.Generic.List`1", "Add").WithLocation(7, 30),
                // (7,30): error CS0656: Missing compiler required member 'System.Collections.Generic.List`1.ToArray'
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..x]").WithArguments("System.Collections.Generic.List`1", "ToArray").WithLocation(7, 30));
        }

        [Theory]
        [InlineData((int)WellKnownMember.System_Collections_Generic_IReadOnlyCollection_T__Count, "System.Collections.Generic.IReadOnlyCollection`1", "Count")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_IReadOnlyList_T__get_Item, "System.Collections.Generic.IReadOnlyList`1", "get_Item")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_ICollection_T__Count, "System.Collections.Generic.ICollection`1", "Count")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_ICollection_T__IsReadOnly, "System.Collections.Generic.ICollection`1", "IsReadOnly")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_ICollection_T__Add, "System.Collections.Generic.ICollection`1", "Add")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_ICollection_T__Clear, "System.Collections.Generic.ICollection`1", "Clear")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_ICollection_T__Contains, "System.Collections.Generic.ICollection`1", "Contains")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_ICollection_T__CopyTo, "System.Collections.Generic.ICollection`1", "CopyTo")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_ICollection_T__Remove, "System.Collections.Generic.ICollection`1", "Remove")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_IList_T__get_Item, "System.Collections.Generic.IList`1", "get_Item")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_IList_T__IndexOf, "System.Collections.Generic.IList`1", "IndexOf")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_IList_T__Insert, "System.Collections.Generic.IList`1", "Insert")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_IList_T__RemoveAt, "System.Collections.Generic.IList`1", "RemoveAt")]
        [InlineData((int)WellKnownMember.System_NotSupportedException__ctor, "System.NotSupportedException", ".ctor")]
        public void SynthesizedReadOnlyList_MissingWellKnownMembers(int missingMember, string missingMemberTypeName, string missingMemberName)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = [0];
                        IEnumerable<int> y = [..x];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeMemberMissing((WellKnownMember)missingMember);
            comp.VerifyEmitDiagnostics(
                // (6,30): error CS0656: Missing compiler required member 'System.Collections.Generic.IReadOnlyCollection`1.Count'
                //         IEnumerable<int> x = [0];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[0]").WithArguments(missingMemberTypeName, missingMemberName).WithLocation(6, 30),
                // (7,30): error CS0656: Missing compiler required member 'System.Collections.Generic.IReadOnlyCollection`1.Count'
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..x]").WithArguments(missingMemberTypeName, missingMemberName).WithLocation(7, 30));
        }

        [Theory]
        [InlineData((int)WellKnownMember.System_Collections_Generic_List_T__Contains, "System.Collections.Generic.List`1", "Contains")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_List_T__CopyTo, "System.Collections.Generic.List`1", "CopyTo")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_List_T__get_Item, "System.Collections.Generic.List`1", "get_Item")]
        [InlineData((int)WellKnownMember.System_Collections_Generic_List_T__IndexOf, "System.Collections.Generic.List`1", "IndexOf")]
        public void SynthesizedReadOnlyList_MissingWellKnownMembers_UnknownLength(int missingMember, string missingMemberTypeName, string missingMemberName)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        IEnumerable<int> x = [0];
                        IEnumerable<int> y = [..x];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.MakeMemberMissing((WellKnownMember)missingMember);
            comp.VerifyEmitDiagnostics(
                // (7,30): error CS0656: Missing compiler required member 'System.Collections.Generic.IReadOnlyCollection`1.Count'
                //         IEnumerable<int> y = [..x];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[..x]").WithArguments(missingMemberTypeName, missingMemberName).WithLocation(7, 30));
        }

        [Fact]
        public void SynthesizedReadOnlyList_Dynamic()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        dynamic d = 2;
                        IEnumerable<int> x = [1, d, default];
                        IEnumerable<dynamic> y = [1, d, default];
                        x.Report(includeType: true);
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                references: new[] { CSharpRef },
                expectedOutput: "(<>z__ReadOnlyArray<System.Int32>) [1, 2, 0], (<>z__ReadOnlyArray<System.Object>) [1, 2, null], ");
        }

        [Fact]
        public void Nullable_01()
        {
            string source = """
                #nullable enable
                class Program
                {
                    static void Main()
                    {
                        object?[] x = [1];
                        x[0].ToString(); // 1
                        object[] y = [null]; // 2
                        y[0].ToString();
                        y = [2, null]; // 3
                        y[1].ToString();
                        object[]? z = [];
                        z.ToString();
                        z = [3];
                        z.ToString();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            // https://github.com/dotnet/roslyn/issues/68786: // 2 should be reported as a warning (compare with array initializer: new object[] { null }).
            comp.VerifyEmitDiagnostics(
                // (7,9): warning CS8602: Dereference of a possibly null reference.
                //         x[0].ToString(); // 1
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x[0]").WithLocation(7, 9));
        }

        [Fact]
        public void Nullable_02()
        {
            string source = """
                #nullable enable
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        List<object?> x = [1];
                        x[0].ToString(); // 1
                        List<object> y = [null]; // 2
                        y[0].ToString();
                        y = [2, null]; // 3
                        y[1].ToString();
                        List<object>? z = [];
                        z.ToString();
                        z = [3];
                        z.ToString();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            // https://github.com/dotnet/roslyn/issues/68786: // 2 and 3 should be reported as warnings.
            comp.VerifyEmitDiagnostics(
                // (8,9): warning CS8602: Dereference of a possibly null reference.
                //         x[0].ToString(); // 1
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x[0]").WithLocation(8, 9));
        }

        [Fact]
        public void Nullable_03()
        {
            string source = """
                #nullable enable
                using System.Collections;
                struct S<T> : IEnumerable
                {
                    public void Add(T t) { }
                    public T this[int index] => default!;
                    IEnumerator IEnumerable.GetEnumerator() => default!;
                }
                class Program
                {
                    static void Main()
                    {
                        S<object?> x = [1];
                        x[0].ToString(); // 1
                        S<object> y = [null]; // 2
                        y[0].ToString();
                        y = [2, null]; // 3
                        y[1].ToString();
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (14,9): warning CS8602: Dereference of a possibly null reference.
                //         x[0].ToString(); // 1
                Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "x[0]").WithLocation(14, 9),
                // (15,24): warning CS8625: Cannot convert null literal to non-nullable reference type.
                //         S<object> y = [null]; // 2
                Diagnostic(ErrorCode.WRN_NullAsNonNullable, "null").WithLocation(15, 24),
                // (17,17): warning CS8625: Cannot convert null literal to non-nullable reference type.
                //         y = [2, null]; // 3
                Diagnostic(ErrorCode.WRN_NullAsNonNullable, "null").WithLocation(17, 17));
        }

        [Fact]
        public void Nullable_04()
        {
            string source = """
                #nullable enable
                using System.Collections;

                S<object>? x = [];
                x.Report();
                x = [];
                S<object>? y = [1];
                y = [2];

                struct S<T> : IEnumerable
                {
                    public void Add(T t) { }
                    public T this[int index] => default!;
                    IEnumerator IEnumerable.GetEnumerator() { yield break; }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensions });
            comp.VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[],");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69447")]
        public void NullableValueType_ImplicitConversion()
        {
            string src = """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

Program.M().Report();

[CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
public struct MyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    public MyCollection(List<T> list) { _list = list; }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MyCollectionBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
    {
        return new MyCollection<T>(new List<T>(items.ToArray()));
    }
}

partial class Program
{
    static MyCollection<int>? M()
    {
        return [1, 2, 3];
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: IncludeExpectedOutput("[1, 2, 3],"), verify: Verification.FailsPEVerify);
            verifier.VerifyIL("Program.M", """
{
  // Code size       21 (0x15)
  .maxstack  1
  IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12_Align=4 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D4"
  IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
  IL_000a:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
  IL_000f:  newobj     "MyCollection<int>?..ctor(MyCollection<int>)"
  IL_0014:  ret
}
""");
            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree);
            var returnValue = tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Last().Expression;
            var conversion = model.GetConversion(returnValue);
            Assert.True(conversion.IsValid);
            Assert.True(conversion.IsNullable);
            Assert.False(conversion.IsCollectionExpression);

            Assert.Equal(1, conversion.UnderlyingConversions.Length);
            var underlyingConversion = conversion.UnderlyingConversions[0];
            Assert.True(underlyingConversion.IsValid);
            Assert.False(underlyingConversion.IsNullable);
            Assert.True(underlyingConversion.IsCollectionExpression);

            var typeInfo = model.GetTypeInfo(returnValue);
            Assert.Null(typeInfo.Type);
            Assert.Equal("MyCollection<System.Int32>?", typeInfo.ConvertedType.ToTestDisplayString());
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69447")]
        public void NullableValueType_ImplicitConversion_Byte()
        {
            string src = """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

Program.M().Report();

[CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
public struct MyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    public MyCollection(List<T> list) { _list = list; }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MyCollectionBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
    {
        return new MyCollection<T>(new List<T>(items.ToArray()));
    }
}

partial class Program
{
    static MyCollection<byte>? M()
    {
        return [1, 2, 3];
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: IncludeExpectedOutput("[1, 2, 3],"), verify: Verification.Fails);

            // ILVerify:
            // [M]: Cannot change initonly field outside its .ctor. { Offset = 0x0 }
            // [M]: Field is not visible. { Offset = 0x0 }
            // [M]: Unexpected type on the stack. { Offset = 0x6, Found = address of '[78cb4f30-abc1-41ca-b5d2-939830104c72]<PrivateImplementationDetails>+__StaticArrayInitTypeSize=3', Expected = Native Int }
            verifier.VerifyIL("Program.M", """
{
  // Code size       22 (0x16)
  .maxstack  2
  IL_0000:  ldsflda    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81"
  IL_0005:  ldc.i4.3
  IL_0006:  newobj     "System.ReadOnlySpan<byte>..ctor(void*, int)"
  IL_000b:  call       "MyCollection<byte> MyCollectionBuilder.Create<byte>(System.ReadOnlySpan<byte>)"
  IL_0010:  newobj     "MyCollection<byte>?..ctor(MyCollection<byte>)"
  IL_0015:  ret
}
""");
            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree);
            var returnValue = tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Last().Expression;
            var conversion = model.GetConversion(returnValue);
            Assert.True(conversion.IsValid);
            Assert.True(conversion.IsNullable);
            Assert.False(conversion.IsCollectionExpression);

            Assert.Equal(1, conversion.UnderlyingConversions.Length);
            var underlyingConversion = conversion.UnderlyingConversions[0];
            Assert.True(underlyingConversion.IsValid);
            Assert.False(underlyingConversion.IsNullable);
            Assert.True(underlyingConversion.IsCollectionExpression);

            var typeInfo = model.GetTypeInfo(returnValue);
            Assert.Null(typeInfo.Type);
            Assert.Equal("MyCollection<System.Byte>?", typeInfo.ConvertedType.ToTestDisplayString());
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69447")]
        public void NullableValueType_ImplicitConversion_Nullability()
        {
            string src = """
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

MyCollection<int>? x = [1, 2, 3];
x.Value.ToString();
x = null;
x.Value.ToString(); // 1

[CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
public struct MyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    public MyCollection(List<T> list) { _list = list; }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MyCollectionBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
    {
        return new MyCollection<T>(new List<T>(items.ToArray()));
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics(
                // 0.cs(11,1): warning CS8629: Nullable value type may be null.
                // x.Value.ToString(); // 1
                Diagnostic(ErrorCode.WRN_NullableValueTypeMayBeNull, "x").WithLocation(11, 1)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69447")]
        public void NullableValueType_BadConversion()
        {
            string src = """
int? x = [1, 2, 3];
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics(
                // 0.cs(1,10): error CS9174: Cannot initialize type 'int?' with a collection expression because the type is not constructible.
                // int? x = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2, 3]").WithArguments("int?").WithLocation(1, 10)
                );

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree);
            var collection = tree.GetRoot().DescendantNodes().OfType<CollectionExpressionSyntax>().Single();
            var conversion = model.GetConversion(collection);
            Assert.False(conversion.IsValid);

            var typeInfo = model.GetTypeInfo(collection);
            Assert.Null(typeInfo.Type);
            Assert.Equal("System.Int32?", typeInfo.ConvertedType.ToTestDisplayString());
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69447")]
        public void NullableValueType_ExplicitCast()
        {
            string src = """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

Program.M().Report();

[CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
public struct MyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    public MyCollection(List<T> list) { _list = list; }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MyCollectionBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
    {
        return new MyCollection<T>(new List<T>(items.ToArray()));
    }
}

partial class Program
{
    static MyCollection<int>? M()
    {
        return (MyCollection<int>?)[1, 2, 3];
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: IncludeExpectedOutput("[1, 2, 3],"), verify: Verification.FailsPEVerify);
            verifier.VerifyIL("Program.M", """
{
  // Code size       21 (0x15)
  .maxstack  1
  IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12_Align=4 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D4"
  IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
  IL_000a:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
  IL_000f:  newobj     "MyCollection<int>?..ctor(MyCollection<int>)"
  IL_0014:  ret
}
""");
            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree);

            var cast = tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Last().Expression;
            Assert.Equal("(MyCollection<int>?)[1, 2, 3]", cast.ToFullString());
            var castConversion = model.GetConversion(cast);
            Assert.True(castConversion.IsIdentity);

            var value = tree.GetRoot().DescendantNodes().OfType<CastExpressionSyntax>().Last().Expression;
            Assert.Equal("[1, 2, 3]", value.ToFullString());
            var conversion = model.GetConversion(value);
            Assert.True(conversion.IsIdentity);

            var typeInfo = model.GetTypeInfo(value);
            Assert.Null(typeInfo.Type);
            Assert.Null(typeInfo.ConvertedType);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69447")]
        public void NullableValueType_MissingSystemNullableCtor()
        {
            string src = """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

Program.M().Report();

[CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
public struct MyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    public MyCollection(List<T> list) { _list = list; }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MyCollectionBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
    {
        return new MyCollection<T>(new List<T>(items.ToArray()));
    }
}

partial class Program
{
    static MyCollection<int>? M()
    {
        return (MyCollection<int>?)[1, 2, 3];
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.MakeMemberMissing(SpecialMember.System_Nullable_T__ctor);
            comp.VerifyDiagnostics(
                // 0.cs(29,36): error CS0656: Missing compiler required member 'System.Nullable`1..ctor'
                //         return (MyCollection<int>?)[1, 2, 3];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[1, 2, 3]").WithArguments("System.Nullable`1", ".ctor").WithLocation(29, 36)
                );
        }

        [Fact]
        public void ExplicitCast_SemanticModel()
        {
            string src = """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

Program.M().Report();

[CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
public struct MyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    public MyCollection(List<T> list) { _list = list; }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MyCollectionBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
    {
        return new MyCollection<T>(new List<T>(items.ToArray()));
    }
}

partial class Program
{
    static MyCollection<int> M()
    {
        return (MyCollection<int>)/*<bind>*/[1, 2, 3]/*</bind>*/;
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: IncludeExpectedOutput("[1, 2, 3],"), verify: Verification.FailsPEVerify);
            verifier.VerifyIL("Program.M", """
{
  // Code size       16 (0x10)
  .maxstack  1
  IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12_Align=4 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D4"
  IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
  IL_000a:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
  IL_000f:  ret
}
""");
            // We should extend IOperation conversions to represent IsCollectionExpression
            // Tracked by https://github.com/dotnet/roslyn/issues/68826
            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: MyCollection<System.Int32>) (Syntax: '[1, 2, 3]')
                Children(3):
                    ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 1) (Syntax: '1')
                    ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 2) (Syntax: '2')
                    ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 3) (Syntax: '3')
                """);

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree);

            var cast = tree.GetRoot().DescendantNodes().OfType<ReturnStatementSyntax>().Last().Expression;
            Assert.Equal("(MyCollection<int>)/*<bind>*/[1, 2, 3]/*</bind>*/", cast.ToFullString());
            var castConversion = model.GetConversion(cast);
            Assert.True(castConversion.IsIdentity);

            var value = tree.GetRoot().DescendantNodes().OfType<CastExpressionSyntax>().Last().Expression;
            Assert.Equal("[1, 2, 3]/*</bind>*/", value.ToFullString());
            var conversion = model.GetConversion(value);
            Assert.True(conversion.IsValid);
            Assert.True(conversion.IsIdentity);

            var typeInfo = model.GetTypeInfo(value);
            Assert.Null(typeInfo.Type);
            Assert.Null(typeInfo.ConvertedType);
        }

        [Fact]
        public void NestedCollection_SemanticModel()
        {
            string src = """
Program.M().Report();

partial class Program
{
    static int[][] M()
    {
        return /*<bind>*/[[1], [2]]/*</bind>*/;
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics();

            var verifier = CompileAndVerify(comp, expectedOutput: IncludeExpectedOutput("[[1], [2]],"), verify: Verification.FailsPEVerify);
            verifier.VerifyIL("Program.M", """
{
  // Code size       33 (0x21)
  .maxstack  7
  IL_0000:  ldc.i4.2
  IL_0001:  newarr     "int[]"
  IL_0006:  dup
  IL_0007:  ldc.i4.0
  IL_0008:  ldc.i4.1
  IL_0009:  newarr     "int"
  IL_000e:  dup
  IL_000f:  ldc.i4.0
  IL_0010:  ldc.i4.1
  IL_0011:  stelem.i4
  IL_0012:  stelem.ref
  IL_0013:  dup
  IL_0014:  ldc.i4.1
  IL_0015:  ldc.i4.1
  IL_0016:  newarr     "int"
  IL_001b:  dup
  IL_001c:  ldc.i4.0
  IL_001d:  ldc.i4.2
  IL_001e:  stelem.i4
  IL_001f:  stelem.ref
  IL_0020:  ret
}
""");
            // We should extend IOperation conversions to represent IsCollectionExpression
            // Tracked by https://github.com/dotnet/roslyn/issues/68826
            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: System.Int32[][]) (Syntax: '[[1], [2]]')
                Children(2):
                    IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Int32[], IsImplicit) (Syntax: '[1]')
                      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                      Operand:
                        IOperation:  (OperationKind.None, Type: System.Int32[]) (Syntax: '[1]')
                          Children(1):
                              ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 1) (Syntax: '1')
                    IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Int32[], IsImplicit) (Syntax: '[2]')
                      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                      Operand:
                        IOperation:  (OperationKind.None, Type: System.Int32[]) (Syntax: '[2]')
                          Children(1):
                              ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 2) (Syntax: '2')
                """);
            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree);

            var nestedCollection = tree.GetRoot().DescendantNodes().OfType<CollectionExpressionSyntax>().Last();
            Assert.Equal("[2]", nestedCollection.ToFullString());

            var conversion = model.GetConversion(nestedCollection);
            Assert.True(conversion.IsValid);
            Assert.False(conversion.IsIdentity);
            Assert.True(conversion.IsCollectionExpression);

            var typeInfo = model.GetTypeInfo(nestedCollection);
            Assert.Null(typeInfo.Type);
            Assert.Equal("System.Int32[]", typeInfo.ConvertedType.ToTestDisplayString());
        }

        [Fact]
        public void NestedCollection_NullableValueType_SemanticModel()
        {
            string src = """
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

Program.M().Report();

[CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
public struct MyCollection<T> : IEnumerable<T>
{
    private readonly List<T> _list;
    public MyCollection(List<T> list) { _list = list; }
    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MyCollectionBuilder
{
    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
    {
        return new MyCollection<T>(new List<T>(items.ToArray()));
    }
}

partial class Program
{
    static MyCollection<MyCollection<int>?> M()
    {
        return [[1], [2]];
    }
}
""";
            var comp = CreateCompilation(new[] { src, s_collectionExtensions }, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics();

            // ILVerify failure:
            //[InlineArrayAsReadOnlySpan]: Return type is ByRef, TypedReference, ArgHandle, or ArgIterator. { Offset = 0x11 }
            var verifier = CompileAndVerify(comp, expectedOutput: IncludeExpectedOutput("[[1], [2]],"), verify: Verification.Fails);
            verifier.VerifyIL("Program.M", """
{
  // Code size       88 (0x58)
  .maxstack  2
  .locals init (<>y__InlineArray2<MyCollection<int>?> V_0)
  IL_0000:  ldloca.s   V_0
  IL_0002:  initobj    "<>y__InlineArray2<MyCollection<int>?>"
  IL_0008:  ldloca.s   V_0
  IL_000a:  ldc.i4.0
  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray2<MyCollection<int>?>, MyCollection<int>?>(ref <>y__InlineArray2<MyCollection<int>?>, int)"
  IL_0010:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.67ABDD721024F0FF4E0B3F4C2FC13BC5BAD42D0B7851D456D88D203D15AAA4504"
  IL_0015:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
  IL_001a:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
  IL_001f:  newobj     "MyCollection<int>?..ctor(MyCollection<int>)"
  IL_0024:  stobj      "MyCollection<int>?"
  IL_0029:  ldloca.s   V_0
  IL_002b:  ldc.i4.1
  IL_002c:  call       "InlineArrayElementRef<<>y__InlineArray2<MyCollection<int>?>, MyCollection<int>?>(ref <>y__InlineArray2<MyCollection<int>?>, int)"
  IL_0031:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.26B25D457597A7B0463F9620F666DD10AA2C4373A505967C7C8D70922A2D6ECE4"
  IL_0036:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
  IL_003b:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
  IL_0040:  newobj     "MyCollection<int>?..ctor(MyCollection<int>)"
  IL_0045:  stobj      "MyCollection<int>?"
  IL_004a:  ldloca.s   V_0
  IL_004c:  ldc.i4.2
  IL_004d:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray2<MyCollection<int>?>, MyCollection<int>?>(in <>y__InlineArray2<MyCollection<int>?>, int)"
  IL_0052:  call       "MyCollection<MyCollection<int>?> MyCollectionBuilder.Create<MyCollection<int>?>(System.ReadOnlySpan<MyCollection<int>?>)"
  IL_0057:  ret
}
""");

            var tree = comp.SyntaxTrees.First();
            var model = comp.GetSemanticModel(tree);

            var nestedCollection = tree.GetRoot().DescendantNodes().OfType<CollectionExpressionSyntax>().Last();
            Assert.Equal("[2]", nestedCollection.ToFullString());

            var conversion = model.GetConversion(nestedCollection);
            Assert.True(conversion.IsValid);
            Assert.False(conversion.IsIdentity);
            Assert.True(conversion.IsNullable);

            Assert.Equal(1, conversion.UnderlyingConversions.Length);
            var underlyingConversion = conversion.UnderlyingConversions[0];
            Assert.True(underlyingConversion.IsValid);
            Assert.False(underlyingConversion.IsNullable);
            Assert.True(underlyingConversion.IsCollectionExpression);

            var typeInfo = model.GetTypeInfo(nestedCollection);
            Assert.Null(typeInfo.Type);
            Assert.Equal("MyCollection<System.Int32>?", typeInfo.ConvertedType.ToTestDisplayString());
        }

        [Fact]
        public void OrderOfEvaluation()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                class C<T> : IEnumerable
                {
                    private List<T> _list = new List<T>();
                    public void Add(T t)
                    {
                        Console.WriteLine("Add {0}", t);
                        _list.Add(t);
                    }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                class Program
                {
                    static void Main()
                    {
                        C<int> x = [Get(1), Get(2)];
                        C<C<int>> y = [[Get(3)], [Get(4), Get(5)]];
                    }
                    static int Get(int value)
                    {
                        Console.WriteLine("Get {0}", value);
                        return value;
                    }
                }
                """;
            CompileAndVerify(source, expectedOutput: """
                Get 1
                Add 1
                Get 2
                Add 2
                Get 3
                Add 3
                Add C`1[System.Int32]
                Get 4
                Add 4
                Get 5
                Add 5
                Add C`1[System.Int32]
                """);
        }

        // Ensure collection expression conversions are not standard implicit conversions
        // and, as a result, are ignored when determining user-defined conversions.
        [Fact]
        public void UserDefinedConversions_01()
        {
            string source = """
                struct S
                {
                    public static implicit operator S(int[] a) => default;
                }
                class Program
                {
                    static void Main()
                    {
                        S s = [];
                        s = [1, 2];
                        s = (S)([3, 4]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (9,15): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         S s = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("S").WithLocation(9, 15),
                // (10,13): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         s = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("S").WithLocation(10, 13),
                // (11,13): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         s = (S)([3, 4]);
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "(S)([3, 4])").WithArguments("S").WithLocation(11, 13));
        }

        [Fact]
        public void UserDefinedConversions_02()
        {
            string source = """
                struct S
                {
                    public static explicit operator S(int[] a) => default;
                }
                class Program
                {
                    static void Main()
                    {
                        S s = [];
                        s = [1, 2];
                        s = (S)([3, 4]);
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (9,15): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         S s = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("S").WithLocation(9, 15),
                // (10,13): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         s = [1, 2];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[1, 2]").WithArguments("S").WithLocation(10, 13),
                // (11,13): error CS9174: Cannot initialize type 'S' with a collection expression because the type is not constructible.
                //         s = (S)([3, 4]);
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "(S)([3, 4])").WithArguments("S").WithLocation(11, 13));
        }

        [Fact]
        public void PrimaryConstructorParameters_01()
        {
            string source = """
                struct S(int x, int y, int z)
                {
                    int[] F = [x, y];
                    int[] M() => [y];
                    static void Main()
                    {
                        var s = new S(1, 2, 3);
                        s.F.Report();
                        s.M().Report();
                    }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }, options: TestOptions.ReleaseExe);
            comp.VerifyEmitDiagnostics(
                // 0.cs(1,28): warning CS9113: Parameter 'z' is unread.
                // struct S(int x, int y, int z)
                Diagnostic(ErrorCode.WRN_UnreadPrimaryConstructorParameter, "z").WithArguments("z").WithLocation(1, 28));

            var verifier = CompileAndVerify(comp, expectedOutput: "[1, 2], [2], ");
            verifier.VerifyIL("S..ctor(int, int, int)",
                """
                {
                  // Code size       33 (0x21)
                  .maxstack  5
                  IL_0000:  ldarg.0
                  IL_0001:  ldarg.2
                  IL_0002:  stfld      "int S.<y>P"
                  IL_0007:  ldarg.0
                  IL_0008:  ldc.i4.2
                  IL_0009:  newarr     "int"
                  IL_000e:  dup
                  IL_000f:  ldc.i4.0
                  IL_0010:  ldarg.1
                  IL_0011:  stelem.i4
                  IL_0012:  dup
                  IL_0013:  ldc.i4.1
                  IL_0014:  ldarg.0
                  IL_0015:  ldfld      "int S.<y>P"
                  IL_001a:  stelem.i4
                  IL_001b:  stfld      "int[] S.F"
                  IL_0020:  ret
                }
                """);
        }

        [Fact]
        public void PrimaryConstructorParameters_02()
        {
            string source = """
                using System;
                class C(int x, int y, int z)
                {
                    Func<int[]> F = () => [x, y];
                    Func<int[]> M() => () => [y];
                    static void Main()
                    {
                        var c = new C(1, 2, 3);
                        c.F().Report();
                        c.M()().Report();
                    }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }, options: TestOptions.ReleaseExe);
            comp.VerifyEmitDiagnostics(
                // 0.cs(2,27): warning CS9113: Parameter 'z' is unread.
                // class C(int x, int y, int z)
                Diagnostic(ErrorCode.WRN_UnreadPrimaryConstructorParameter, "z").WithArguments("z").WithLocation(2, 27));

            var verifier = CompileAndVerify(comp, verify: Verification.Fails, expectedOutput: "[1, 2], [2], ");
            verifier.VerifyIL("C..ctor(int, int, int)",
                """
                {
                  // Code size       52 (0x34)
                  .maxstack  3
                  .locals init (C.<>c__DisplayClass0_0 V_0) //CS$<>8__locals0
                  IL_0000:  ldarg.0
                  IL_0001:  ldarg.2
                  IL_0002:  stfld      "int C.<y>P"
                  IL_0007:  newobj     "C.<>c__DisplayClass0_0..ctor()"
                  IL_000c:  stloc.0
                  IL_000d:  ldloc.0
                  IL_000e:  ldarg.1
                  IL_000f:  stfld      "int C.<>c__DisplayClass0_0.x"
                  IL_0014:  ldloc.0
                  IL_0015:  ldarg.0
                  IL_0016:  stfld      "C C.<>c__DisplayClass0_0.<>4__this"
                  IL_001b:  ldarg.0
                  IL_001c:  ldloc.0
                  IL_001d:  ldftn      "int[] C.<>c__DisplayClass0_0.<.ctor>b__0()"
                  IL_0023:  newobj     "System.Func<int[]>..ctor(object, System.IntPtr)"
                  IL_0028:  stfld      "System.Func<int[]> C.F"
                  IL_002d:  ldarg.0
                  IL_002e:  call       "object..ctor()"
                  IL_0033:  ret
                }
                """);
        }

        [Fact]
        public void PrimaryConstructorParameters_03()
        {
            string source = """
                using System.Collections.Generic;
                class A(int[] x, List<int> y)
                {
                    public int[] X = x;
                    public List<int> Y = y;
                }
                class B(int x, int y, int z) : A([y, z], [z])
                {
                }
                class Program
                {
                    static void Main()
                    {
                        var b = new B(1, 2, 3);
                        b.X.Report();
                        b.Y.Report();
                    }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }, options: TestOptions.ReleaseExe);
            comp.VerifyEmitDiagnostics(
                // 0.cs(7,13): warning CS9113: Parameter 'x' is unread.
                // class B(int x, int y, int z) : A([y, z], [z])
                Diagnostic(ErrorCode.WRN_UnreadPrimaryConstructorParameter, "x").WithArguments("x").WithLocation(7, 13));

            var verifier = CompileAndVerify(comp, expectedOutput: "[2, 3], [3], ");
            verifier.VerifyIL("B..ctor(int, int, int)",
                """
                {
                  // Code size       34 (0x22)
                  .maxstack  5
                  IL_0000:  ldarg.0
                  IL_0001:  ldc.i4.2
                  IL_0002:  newarr     "int"
                  IL_0007:  dup
                  IL_0008:  ldc.i4.0
                  IL_0009:  ldarg.2
                  IL_000a:  stelem.i4
                  IL_000b:  dup
                  IL_000c:  ldc.i4.1
                  IL_000d:  ldarg.3
                  IL_000e:  stelem.i4
                  IL_000f:  ldc.i4.1
                  IL_0010:  newobj     "System.Collections.Generic.List<int>..ctor(int)"
                  IL_0015:  dup
                  IL_0016:  ldarg.3
                  IL_0017:  callvirt   "void System.Collections.Generic.List<int>.Add(int)"
                  IL_001c:  call       "A..ctor(int[], System.Collections.Generic.List<int>)"
                  IL_0021:  ret
                }
                """);
        }

        [Fact]
        public void SemanticModel()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                struct S1 : IEnumerable
                {
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                struct S2
                {
                }
                class Program
                {
                    static void Main()
                    {
                        int[] v1 = [];
                        List<object> v2 = [];
                        Span<int> v3 = [];
                        ReadOnlySpan<object> v4 = [];
                        S1 v5 = [];
                        S2 v6 = [];
                        var v7 = (int[])[];
                        var v8 = (List<object>)[];
                        var v9 = (Span<int>)[];
                        var v10 = (ReadOnlySpan<object>)[];
                        var v11 = (S1)([]);
                        var v12 = (S2)([]);
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net70);
            comp.VerifyEmitDiagnostics(
                // (20,17): error CS9174: Cannot initialize type 'S2' with a collection expression because the type is not constructible.
                //         S2 v6 = [];
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("S2").WithLocation(20, 17),
                // (26,19): error CS9174: Cannot initialize type 'S2' with a collection expression because the type is not constructible.
                //         var v12 = (S2)([]);
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "(S2)([])").WithArguments("S2").WithLocation(26, 19));

            var tree = comp.SyntaxTrees[0];
            var model = comp.GetSemanticModel(tree);
            var collections = tree.GetRoot().DescendantNodes().OfType<CollectionExpressionSyntax>().ToArray();
            Assert.Equal(12, collections.Length);
            VerifyTypes(model, collections[0], expectedType: null, expectedConvertedType: "System.Int32[]", ConversionKind.CollectionExpression);
            VerifyTypes(model, collections[1], expectedType: null, expectedConvertedType: "System.Collections.Generic.List<System.Object>", ConversionKind.CollectionExpression);
            VerifyTypes(model, collections[2], expectedType: null, expectedConvertedType: "System.Span<System.Int32>", ConversionKind.CollectionExpression);
            VerifyTypes(model, collections[3], expectedType: null, expectedConvertedType: "System.ReadOnlySpan<System.Object>", ConversionKind.CollectionExpression);
            VerifyTypes(model, collections[4], expectedType: null, expectedConvertedType: "S1", ConversionKind.CollectionExpression);
            VerifyTypes(model, collections[5], expectedType: null, expectedConvertedType: "S2", ConversionKind.NoConversion);
            VerifyTypes(model, collections[6], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
            VerifyTypes(model, collections[7], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
            VerifyTypes(model, collections[8], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
            VerifyTypes(model, collections[9], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
            VerifyTypes(model, collections[10], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
            VerifyTypes(model, collections[11], expectedType: null, expectedConvertedType: null, ConversionKind.Identity);
        }

        private static void VerifyTypes(SemanticModel model, ExpressionSyntax expr, string expectedType, string expectedConvertedType, ConversionKind expectedConversionKind)
        {
            var typeInfo = model.GetTypeInfo(expr);
            var conversion = model.GetConversion(expr);
            Assert.Equal(expectedType, typeInfo.Type?.ToTestDisplayString());
            Assert.Equal(expectedConvertedType, typeInfo.ConvertedType?.ToTestDisplayString());
            Assert.Equal(expectedConversionKind, conversion.Kind);
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB1 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = F0();
                        x.Report();
                        MyCollection<int> y = F1();
                        y.Report();
                        MyCollection<object> z = F2(3, 4);
                        z.Report();
                    }
                    static MyCollection<string> F0()
                    {
                        return [];
                    }
                    static MyCollection<int> F1()
                    {
                        return [0, 1, 2];
                    }
                    static MyCollection<object> F2(int x, object y)
                    {
                        return [x, y, null];
                    }
                }
                """;

            var verifier = CompileAndVerify(
                new[] { sourceB1, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [0, 1, 2], [3, 4, null], "));
            verifier.VerifyIL("Program.F0",
                """
                {
                  // Code size       16 (0x10)
                  .maxstack  1
                  IL_0000:  call       "string[] System.Array.Empty<string>()"
                  IL_0005:  newobj     "System.ReadOnlySpan<string>..ctor(string[])"
                  IL_000a:  call       "MyCollection<string> MyCollectionBuilder.Create<string>(System.ReadOnlySpan<string>)"
                  IL_000f:  ret
                }
                """);
            verifier.VerifyIL("Program.F1",
                """
                {
                  // Code size       16 (0x10)
                  .maxstack  1
                  IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12_Align=4 <PrivateImplementationDetails>.AD5DC1478DE06A4C2728EA528BD9361A4B945E92A414BF4D180CEDAAEAA5F4CC4"
                  IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
                  IL_000a:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
                  IL_000f:  ret
                }
                """);
            verifier.VerifyIL("Program.F2",
                """
                {
                  // Code size       57 (0x39)
                  .maxstack  2
                  .locals init (<>y__InlineArray3<object> V_0)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "<>y__InlineArray3<object>"
                  IL_0008:  ldloca.s   V_0
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_0010:  ldarg.0
                  IL_0011:  box        "int"
                  IL_0016:  stind.ref
                  IL_0017:  ldloca.s   V_0
                  IL_0019:  ldc.i4.1
                  IL_001a:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_001f:  ldarg.1
                  IL_0020:  stind.ref
                  IL_0021:  ldloca.s   V_0
                  IL_0023:  ldc.i4.2
                  IL_0024:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_0029:  ldnull
                  IL_002a:  stind.ref
                  IL_002b:  ldloca.s   V_0
                  IL_002d:  ldc.i4.3
                  IL_002e:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<object>, object>(in <>y__InlineArray3<object>, int)"
                  IL_0033:  call       "MyCollection<object> MyCollectionBuilder.Create<object>(System.ReadOnlySpan<object>)"
                  IL_0038:  ret
                }
                """);

            string sourceB2 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<object> c = F2([1, 2]);
                        c.Report();
                    }
                    static MyCollection<object> F2(MyCollection<object> c)
                    {
                        return [..c, 3];
                    }
                }
                """;

            verifier = CompileAndVerify(
                new[] { sourceB2, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.F2",
                """
                {
                  // Code size       79 (0x4f)
                  .maxstack  2
                  .locals init (System.Collections.Generic.List<object> V_0,
                                System.Collections.Generic.IEnumerator<object> V_1,
                                object V_2)
                  IL_0000:  newobj     "System.Collections.Generic.List<object>..ctor()"
                  IL_0005:  stloc.0
                  IL_0006:  ldarga.s   V_0
                  IL_0008:  call       "System.Collections.Generic.IEnumerator<object> MyCollection<object>.GetEnumerator()"
                  IL_000d:  stloc.1
                  .try
                  {
                    IL_000e:  br.s       IL_001e
                    IL_0010:  ldloc.1
                    IL_0011:  callvirt   "object System.Collections.Generic.IEnumerator<object>.Current.get"
                    IL_0016:  stloc.2
                    IL_0017:  ldloc.0
                    IL_0018:  ldloc.2
                    IL_0019:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                    IL_001e:  ldloc.1
                    IL_001f:  callvirt   "bool System.Collections.IEnumerator.MoveNext()"
                    IL_0024:  brtrue.s   IL_0010
                    IL_0026:  leave.s    IL_0032
                  }
                  finally
                  {
                    IL_0028:  ldloc.1
                    IL_0029:  brfalse.s  IL_0031
                    IL_002b:  ldloc.1
                    IL_002c:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0031:  endfinally
                  }
                  IL_0032:  ldloc.0
                  IL_0033:  ldc.i4.3
                  IL_0034:  box        "int"
                  IL_0039:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_003e:  ldloc.0
                  IL_003f:  callvirt   "object[] System.Collections.Generic.List<object>.ToArray()"
                  IL_0044:  newobj     "System.ReadOnlySpan<object>..ctor(object[])"
                  IL_0049:  call       "MyCollection<object> MyCollectionBuilder.Create<object>(System.ReadOnlySpan<object>)"
                  IL_004e:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_02A(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework,
            bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                """;
            var sources = targetFramework == TargetFramework.Net70
                ? new[] { sourceA, CollectionBuilderAttributeDefinition }
                : new[] { sourceA };
            var comp = CreateCompilation(sources, targetFramework: targetFramework);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        var x = F();
                        x.Report();
                    }
                    static MyCollection<int?> F()
                    {
                        return [1, 2, null];
                    }
                }
                """;
            comp = CreateCompilation(new[] { sourceB, s_collectionExtensions }, references: new[] { refA }, targetFramework: targetFramework, options: TestOptions.ReleaseExe);
            comp.VerifyEmitDiagnostics();

            var verifier = CompileAndVerify(
                comp,
                symbolValidator: module =>
                {
                    var type = module.GlobalNamespace.GetTypeMembers("<>y__InlineArray3").SingleOrDefault();
                    if (targetFramework == TargetFramework.Net80)
                    {
                        Assert.NotNull(type);
                    }
                    else
                    {
                        Assert.Null(type);
                    }
                },
                verify: targetFramework == TargetFramework.Net80 ? Verification.Fails : Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, null], "));
            if (targetFramework == TargetFramework.Net80)
            {
                verifier.VerifyIL("Program.F",
                    """
                    {
                      // Code size       74 (0x4a)
                      .maxstack  2
                      .locals init (<>y__InlineArray3<int?> V_0)
                      IL_0000:  ldloca.s   V_0
                      IL_0002:  initobj    "<>y__InlineArray3<int?>"
                      IL_0008:  ldloca.s   V_0
                      IL_000a:  ldc.i4.0
                      IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray3<int?>, int?>(ref <>y__InlineArray3<int?>, int)"
                      IL_0010:  ldc.i4.1
                      IL_0011:  newobj     "int?..ctor(int)"
                      IL_0016:  stobj      "int?"
                      IL_001b:  ldloca.s   V_0
                      IL_001d:  ldc.i4.1
                      IL_001e:  call       "InlineArrayElementRef<<>y__InlineArray3<int?>, int?>(ref <>y__InlineArray3<int?>, int)"
                      IL_0023:  ldc.i4.2
                      IL_0024:  newobj     "int?..ctor(int)"
                      IL_0029:  stobj      "int?"
                      IL_002e:  ldloca.s   V_0
                      IL_0030:  ldc.i4.2
                      IL_0031:  call       "InlineArrayElementRef<<>y__InlineArray3<int?>, int?>(ref <>y__InlineArray3<int?>, int)"
                      IL_0036:  initobj    "int?"
                      IL_003c:  ldloca.s   V_0
                      IL_003e:  ldc.i4.3
                      IL_003f:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<int?>, int?>(in <>y__InlineArray3<int?>, int)"
                      IL_0044:  call       "MyCollection<int?> MyCollectionBuilder.Create<int?>(System.ReadOnlySpan<int?>)"
                      IL_0049:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.F",
                    """
                    {
                      // Code size       43 (0x2b)
                      .maxstack  4
                      IL_0000:  ldc.i4.3
                      IL_0001:  newarr     "int?"
                      IL_0006:  dup
                      IL_0007:  ldc.i4.0
                      IL_0008:  ldc.i4.1
                      IL_0009:  newobj     "int?..ctor(int)"
                      IL_000e:  stelem     "int?"
                      IL_0013:  dup
                      IL_0014:  ldc.i4.1
                      IL_0015:  ldc.i4.2
                      IL_0016:  newobj     "int?..ctor(int)"
                      IL_001b:  stelem     "int?"
                      IL_0020:  newobj     "System.ReadOnlySpan<int?>..ctor(int?[])"
                      IL_0025:  call       "MyCollection<int?> MyCollectionBuilder.Create<int?>(System.ReadOnlySpan<int?>)"
                      IL_002a:  ret
                    }
                    """);
            }
        }

        // As above, but with TargetFramework.NetFramework.
        [ConditionalFact(typeof(DesktopOnly))]
        public void CollectionBuilder_02B()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        var list = new List<T>();
                        foreach (var i in items) list.Add(i);
                        return new MyCollection<T>(list);
                    }
                }
                """;
            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        var x = F();
                        x.Report();
                    }
                    static MyCollection<int?> F()
                    {
                        return [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilationWithSpanAndMemoryExtensions(
                new[] { sourceA, sourceB, s_collectionExtensions, CollectionBuilderAttributeDefinition },
                targetFramework: TargetFramework.NetFramework,
                options: TestOptions.ReleaseExe);
            comp.VerifyEmitDiagnostics();

            var verifier = CompileAndVerify(
                comp,
                symbolValidator: module =>
                {
                    var type = module.GlobalNamespace.GetTypeMembers("<>y__InlineArray3").SingleOrDefault();
                    Assert.Null(type);
                },
                expectedOutput: "[1, 2, null], ");
            verifier.VerifyIL("Program.F",
                """
                {
                  // Code size       43 (0x2b)
                  .maxstack  4
                  IL_0000:  ldc.i4.3
                  IL_0001:  newarr     "int?"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.0
                  IL_0008:  ldc.i4.1
                  IL_0009:  newobj     "int?..ctor(int)"
                  IL_000e:  stelem     "int?"
                  IL_0013:  dup
                  IL_0014:  ldc.i4.1
                  IL_0015:  ldc.i4.2
                  IL_0016:  newobj     "int?..ctor(int)"
                  IL_001b:  stelem     "int?"
                  IL_0020:  newobj     "System.ReadOnlySpan<int?>..ctor(int?[])"
                  IL_0025:  call       "MyCollection<int?> MyCollectionBuilder.Create<int?>(System.ReadOnlySpan<int?>)"
                  IL_002a:  ret
                }
                """);
        }

        [Fact]
        public void CollectionBuilder_InlineArrayTypes()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                class A
                {
                    static void M()
                    {
                        MyCollection<object> x;
                        x = [];
                        x = [null, null];
                        x = [1, 2, 3];
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            CompileAndVerify(
                comp,
                symbolValidator: module =>
                {
                    AssertEx.Equal(new[] { "<>y__InlineArray2", "<>y__InlineArray3" }, getInlineArrayTypeNames(module));
                },
                verify: Verification.Skipped);
            var refA = comp.EmitToImageReference();

            string sourceB = """
                class B
                {
                    static void M<T>(MyCollection<T> c)
                    {
                    }
                    static void M1()
                    {
                        M<int?>([1]);
                    }
                    static void M2()
                    {
                        M([(object)4, 5, 6]);
                        M(["a"]);
                        M(["b"]);
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            CompileAndVerify(
                comp,
                symbolValidator: module =>
                {
                    AssertEx.Equal(new[] { "<>y__InlineArray1", "<>y__InlineArray3" }, getInlineArrayTypeNames(module));
                },
                verify: Verification.Skipped);

            const int n = 1025;
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(i);
            }
            string sourceC = $$"""
                using System;
                using System.Linq;
                class Program
                {
                    static void Main()
                    {
                        MyCollection<object> c = [{{builder.ToString()}}];
                        Console.WriteLine(c.Count());
                    }
                }
                """;
            comp = CreateCompilation(sourceC, references: new[] { refA }, targetFramework: TargetFramework.Net80, options: TestOptions.ReleaseExe);
            CompileAndVerify(
                comp,
                symbolValidator: module =>
                {
                    AssertEx.Equal(new[] { $"<>y__InlineArray{n}" }, getInlineArrayTypeNames(module));
                },
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput($"{n}"));

            static ImmutableArray<string> getInlineArrayTypeNames(ModuleSymbol module)
            {
                return module.GlobalNamespace.GetTypeMembers().WhereAsArray(t => t.Name.StartsWith("<>y__InlineArray")).SelectAsArray(t => t.Name);
            }
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_RefStructCollection(bool useCompilationReference, bool useScoped)
        {
            string qualifier = useScoped ? "scoped " : "";
            string sourceA = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public ref struct MyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    public T[] ToArray() => _list.ToArray();
                }
                public static class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>({{qualifier}}ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                using System;
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        F().Report();
                    }
                    static object[] F()
                    {
                        MyCollection<object> c = [1, 2, 3];
                        return c.ToArray();
                    }
                }
                """;

            var verifier = CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.F",
                $$"""
                {
                    // Code size       75 (0x4b)
                    .maxstack  2
                    .locals init (MyCollection<object> V_0, //c
                                <>y__InlineArray3<object> V_1)
                    IL_0000:  ldloca.s   V_1
                    IL_0002:  initobj    "<>y__InlineArray3<object>"
                    IL_0008:  ldloca.s   V_1
                    IL_000a:  ldc.i4.0
                    IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                    IL_0010:  ldc.i4.1
                    IL_0011:  box        "int"
                    IL_0016:  stind.ref
                    IL_0017:  ldloca.s   V_1
                    IL_0019:  ldc.i4.1
                    IL_001a:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                    IL_001f:  ldc.i4.2
                    IL_0020:  box        "int"
                    IL_0025:  stind.ref
                    IL_0026:  ldloca.s   V_1
                    IL_0028:  ldc.i4.2
                    IL_0029:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                    IL_002e:  ldc.i4.3
                    IL_002f:  box        "int"
                    IL_0034:  stind.ref
                    IL_0035:  ldloca.s   V_1
                    IL_0037:  ldc.i4.3
                    IL_0038:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<object>, object>(in <>y__InlineArray3<object>, int)"
                    IL_003d:  call       "MyCollection<object> MyCollectionBuilder.Create<object>({{qualifier}}System.ReadOnlySpan<object>)"
                    IL_0042:  stloc.0
                    IL_0043:  ldloca.s   V_0
                    IL_0045:  call       "object[] MyCollection<object>.ToArray()"
                    IL_004a:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_NonGenericCollection(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public sealed class MyCollection : IEnumerable<object>
                {
                    private readonly List<object> _list;
                    public MyCollection(List<object> list) { _list = list; }
                    public IEnumerator<object> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<object> items) =>
                        new MyCollection(new List<object>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        x.Report();
                        MyCollection y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InterfaceCollection_ReturnInterface(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public interface IMyCollection<T> : IEnumerable<T>
                {
                }
                public sealed class MyCollectionBuilder
                {
                    public static IMyCollection<T> Create<T>(ReadOnlySpan<T> items) =>
                        new MyCollection<T>(new List<T>(items.ToArray()));
                    public sealed class MyCollection<T> : IMyCollection<T>
                    {
                        private readonly List<T> _list;
                        public MyCollection(List<T> list) { _list = list; }
                        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        IMyCollection<string> x = [];
                        x.Report(includeType: true);
                        IMyCollection<int> y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("(MyCollectionBuilder.MyCollection<System.String>) [], (MyCollectionBuilder.MyCollection<System.Int32>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InterfaceCollection_ReturnImplementation(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public interface IMyCollection<T> : IEnumerable<T>
                {
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) =>
                        new MyCollection<T>(new List<T>(items.ToArray()));
                    public sealed class MyCollection<T> : IMyCollection<T>
                    {
                        private readonly List<T> _list;
                        public MyCollection(List<T> list) { _list = list; }
                        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        IMyCollection<string> x = [];
                        x.Report(includeType: true);
                        IMyCollection<int> y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("(MyCollectionBuilder.MyCollection<System.String>) [], (MyCollectionBuilder.MyCollection<System.Int32>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_NestedCollectionAndBuilder(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                public class Container
                {
                    [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                    public sealed class MyCollection<T> : IEnumerable<T>
                    {
                        private readonly List<T> _list;
                        public MyCollection(List<T> list) { _list = list; }
                        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                    public sealed class MyCollectionBuilder
                    {
                        public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                            => new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        Container.MyCollection<string> x = [];
                        x.Report(includeType: true);
                        Container.MyCollection<object> y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("(Container.MyCollection<System.String>) [], (Container.MyCollection<System.Object>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_NoElementType(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T>
                {
                    public MyCollection(T[] array) { }
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<object> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<string> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,34): error CS9188: 'MyCollection<object>' has a CollectionBuilderAttribute but no element type.
                //         MyCollection<object> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderNoElementType, "[]").WithArguments("MyCollection<object>").WithLocation(6, 34),
                // (7,31): error CS9188: 'MyCollection<int>' has a CollectionBuilderAttribute but no element type.
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderNoElementType, "[1, 2, 3]").WithArguments("MyCollection<int>").WithLocation(7, 31));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ElementTypeFromPattern_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T>
                {
                    private readonly T[] _array;
                    public MyCollection(T[] array) { _array = array; }
                    public MyEnumerator<T> GetEnumerator()
                        => new MyEnumerator<T>(_array);
                }
                public struct MyEnumerator<T>
                {
                    private readonly T[] _array;
                    private int _index;
                    public MyEnumerator(T[] array)
                    {
                        _array = array;
                        _index = -1;
                    }
                    public bool MoveNext()
                    {
                        if (_index < _array.Length) _index++;
                        return _index < _array.Length;
                    }
                    public T Current => _array[_index];
                }
                public struct MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                        => new MyCollection<T>(items.ToArray());
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        GetElements(x).Report();
                        MyCollection<int> y = [1, 2, 3];
                        GetElements(y).Report();
                    }
                    static IEnumerable<T> GetElements<T>(MyCollection<T> c)
                    {
                        foreach (var e in c) yield return e;
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ElementTypeFromPattern_02(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection
                {
                    private readonly object[] _array;
                    public MyCollection(object[] array) { _array = array; }
                    public MyEnumerator GetEnumerator()
                        => new MyEnumerator(_array);
                }
                public struct MyEnumerator
                {
                    private readonly object[] _array;
                    private int _index;
                    public MyEnumerator(object[] array)
                    {
                        _array = array;
                        _index = -1;
                    }
                    public bool MoveNext()
                    {
                        if (_index < _array.Length) _index++;
                        return _index < _array.Length;
                    }
                    public object Current => _array[_index];
                }
                public struct MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<object> items)
                        => new MyCollection(items.ToArray());
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                using System.Collections;
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        GetElements(x).Report();
                        MyCollection y = [1, 2, 3];
                        GetElements(y).Report();
                    }
                    static IEnumerable GetElements(MyCollection c)
                    {
                        foreach (var e in c) yield return e;
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ObjectElementType_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection : IEnumerable
                {
                    private readonly object[] _array;
                    public MyCollection(object[] array) { _array = array; }
                    IEnumerator IEnumerable.GetEnumerator() => _array.GetEnumerator();
                }
                public struct MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<object> items)
                        => new MyCollection(items.ToArray());
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        x.Report();
                        MyCollection y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ObjectElementType_02(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable
                {
                    public MyCollection(T[] array) { }
                    public IEnumerator GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<object> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<string> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection<T>'.
                //         MyCollection<object> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "object", "MyCollection<T>").WithLocation(6, 34),
                // (7,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection<T>'.
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, 3]").WithArguments("Create", "object", "MyCollection<T>").WithLocation(7, 31));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ConstructedElementType(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                public sealed class E<T>
                {
                    private readonly T _t;
                    public E(T t) { _t = t; }
                    public override string ToString() => $"E({_t})";
                }
                [CollectionBuilder(typeof(Builder), "Create")]
                public sealed class C<T> : IEnumerable<E<T>>
                {
                    private readonly List<E<T>> _list;
                    public C(List<E<T>> list) { _list = list; }
                    public IEnumerator<E<T>> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public sealed class Builder
                {
                    public static C<T> Create<T>(ReadOnlySpan<E<T>> items)
                        => new C<T>(new List<E<T>>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        C<string> x = [null];
                        x.Report(includeType: true);
                        C<int> y = [new E<int>(1), default];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("(C<System.String>) [null], (C<System.Int32>) [E(1), null], "));
        }

        [Fact]
        public void CollectionBuilder_ElementTypeMismatch_01()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection : IEnumerable
                {
                    private List<string> _items;
                    public MyCollection(List<string> items) { _items = items; }
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<string> items) => new MyCollection(new(items.ToArray()));
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection c = [null];
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (20,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection c = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "object", "MyCollection").WithLocation(20, 26));
        }

        [Fact]
        public void CollectionBuilder_ElementTypeMismatch_02()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection : IEnumerable<object>
                {
                    private List<string> _items;
                    public MyCollection(List<string> items) { _items = items; }
                    IEnumerator<object> IEnumerable<object>.GetEnumerator() => _items.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<string> items) => new MyCollection(new(items.ToArray()));
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection c = [null];
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (21,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection c = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "object", "MyCollection").WithLocation(21, 26));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_Dictionary(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyDictionaryBuilder), "Create")]
                public class MyImmutableDictionary<K, V> : IEnumerable<KeyValuePair<K, V>>
                {
                    private readonly Dictionary<K, V> _d;
                    public MyImmutableDictionary(ReadOnlySpan<KeyValuePair<K, V>> items)
                    {
                        _d = new();
                        foreach (var (k, v) in items) _d.Add(k, v);
                    }
                    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => _d.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyDictionaryBuilder
                {
                    public static MyImmutableDictionary<K, V> Create<K, V>(ReadOnlySpan<KeyValuePair<K, V>> items)
                        => new MyImmutableDictionary<K, V>(items);
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        MyImmutableDictionary<string, object> x = [];
                        x.Report();
                        MyImmutableDictionary<string, int> y = [KeyValuePair.Create("one", 1), KeyValuePair.Create("two", 2)];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [[one, 1], [two, 2]], "));
        }

        [Fact]
        public void CollectionBuilder_MissingBuilderType()
        {
            string sourceA = """
                public class MyCollectionBuilder
                {
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = comp.EmitToImageReference();

            string sourceB = """
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            var refB = comp.EmitToImageReference();

            string sourceC = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceC, references: new[] { refB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_MissingBuilderMethod(bool useCompilationReference)
        {
            string sourceA = """
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_NullBuilderType()
        {
            string sourceA = """
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(null, "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(4,2): error CS9185: The CollectionBuilderAttribute builder type must be a non-generic class or struct.
                // [CollectionBuilder(null, "Create")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidType, "CollectionBuilder").WithLocation(4, 2),
                // 1.cs(6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // 1.cs(7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_NullBuilderType_FromMetadata()
        {
            // [CollectionBuilder(null, "Create")]
            string sourceA = """
                .class public sealed System.Runtime.CompilerServices.CollectionBuilderAttribute extends [mscorlib]System.Attribute
                {
                  .method public hidebysig specialname rtspecialname instance void .ctor(class [mscorlib]System.Type builderType, string methodName) cil managed { ret }
                }
                .class public sealed MyCollection`1<T>
                {
                  .custom instance void System.Runtime.CompilerServices.CollectionBuilderAttribute::.ctor(class [mscorlib]System.Type, string) = { type(nullref) string('Create') }
                  .method public hidebysig specialname rtspecialname instance void .ctor() cil managed { ret }
                  .method public instance class [mscorlib]System.Collections.Generic.IEnumerator`1<!T> GetEnumerator() { ldnull ret }
                }
                """;
            var refA = CompileIL(sourceA);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(sourceB, references: new[] { refA });
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_InvalidBuilderType_Interface()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public interface MyCollectionBuilder
                {
                    MyCollection<T> Create<T>(ReadOnlySpan<T> items);
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(5,2): error CS9185: The CollectionBuilderAttribute builder type must be a non-generic class or struct.
                // [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidType, "CollectionBuilder").WithLocation(5, 2),
                // 1.cs(6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // 1.cs(7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_InvalidBuilderType_Interface_FromMetadata()
        {
            // [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
            // class MyCollection<T> { ... }
            // interface MyCollectionBuilder { ... }
            string sourceA = """
                .class public sealed System.ReadOnlySpan`1<T> extends [mscorlib]System.ValueType
                {
                }
                .class public sealed System.Runtime.CompilerServices.CollectionBuilderAttribute extends [mscorlib]System.Attribute
                {
                  .method public hidebysig specialname rtspecialname instance void .ctor(class [mscorlib]System.Type builderType, string methodName) cil managed { ret }
                }
                .class public sealed MyCollection`1<T>
                {
                  .custom instance void System.Runtime.CompilerServices.CollectionBuilderAttribute::.ctor(class [mscorlib]System.Type, string) = { type(MyCollectionBuilder) string('Create') }
                  .method public hidebysig specialname rtspecialname instance void .ctor() cil managed { ret }
                  .method public instance class [mscorlib]System.Collections.Generic.IEnumerator`1<!T> GetEnumerator() { ldnull ret }
                }
                .class interface public abstract MyCollectionBuilder
                {
                  .method public abstract virtual instance class MyCollection`1<!!T> Create<T>(valuetype System.ReadOnlySpan`1<!!T> items) { }
                }
                """;
            var refA = CompileIL(sourceA);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(sourceB, references: new[] { refA });
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InvalidBuilderType_03(
            [CombinatorialValues("public delegate void MyCollectionBuilder();", "public enum MyCollectionBuilder { }")] string builderTypeDefinition)
        {
            string sourceA = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "ToString")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                {{builderTypeDefinition}}
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(5,2): error CS9185: The CollectionBuilderAttribute builder type must be a non-generic class or struct.
                // [CollectionBuilder(typeof(MyCollectionBuilder), "ToString")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidType, "CollectionBuilder").WithLocation(5, 2),
                // 1.cs(6,31): error CS9187: Could not find an accessible 'ToString' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("ToString", "T", "MyCollection<T>").WithLocation(6, 31),
                // 1.cs(7,34): error CS9187: Could not find an accessible 'ToString' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("ToString", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InvalidBuilderType_04(
            [CombinatorialValues("int[]", "int*", "(object, object)")] string builderTypeName)
        {
            string sourceA = $$"""
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof({{builderTypeName}}), "ToString")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(4,2): error CS9185: The CollectionBuilderAttribute builder type must be a non-generic class or struct.
                // [CollectionBuilder(typeof(int*), "ToString")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidType, "CollectionBuilder").WithLocation(4, 2),
                // 1.cs(6,31): error CS9187: Could not find an accessible 'ToString' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("ToString", "T", "MyCollection<T>").WithLocation(6, 31),
                // 1.cs(7,34): error CS9187: Could not find an accessible 'ToString' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("ToString", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_InvalidBuilderType_TypeParameter()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                struct Container<T>
                {
                    [CollectionBuilder(typeof(T), "ToString")]
                    public struct MyCollection : IEnumerable<int>
                    {
                        IEnumerator<int> IEnumerable<int>.GetEnumerator() => default;
                        IEnumerator IEnumerable.GetEnumerator() => default;
                    }
                }
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        Container<int>.MyCollection x = [];
                        Container<string>.MyCollection y = [null];
                        Container<object>.MyCollection z = new();
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(6,24): error CS0416: 'T': an attribute argument cannot use type parameters
                //     [CollectionBuilder(typeof(T), "ToString")]
                Diagnostic(ErrorCode.ERR_AttrArgWithTypeVars, "typeof(T)").WithArguments("T").WithLocation(6, 24),
                // 0.cs(19,45): error CS1061: 'Container<string>.MyCollection' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'Container<string>.MyCollection' could be found (are you missing a using directive or an assembly reference?)
                //         Container<string>.MyCollection y = [null];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "null").WithArguments("Container<string>.MyCollection", "Add").WithLocation(19, 45));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_NullOrEmptyMethodName([CombinatorialValues("null", "\"\"")] string methodName)
        {
            string sourceA = $$"""
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), {{methodName}})]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(4,2): error CS9186: The CollectionBuilderAttribute method name is invalid.
                // [CollectionBuilder(typeof(MyCollectionBuilder), "")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidMethodName, "CollectionBuilder").WithLocation(4, 2),
                // 1.cs(6,31): error CS9187: Could not find an accessible '' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("", "T", "MyCollection<T>").WithLocation(6, 31),
                // 1.cs(7,34): error CS9187: Could not find an accessible '' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_NullOrEmptyMethodName_FromMetadata([CombinatorialValues("nullref", "''")] string methodName)
        {
            // [CollectionBuilder(typeof(MyCollectionBuilder), "")]
            string sourceA = $$"""
                .class public sealed System.ReadOnlySpan`1<T> extends [mscorlib]System.ValueType
                {
                }
                .class public sealed System.Runtime.CompilerServices.CollectionBuilderAttribute extends [mscorlib]System.Attribute
                {
                  .method public hidebysig specialname rtspecialname instance void .ctor(class [mscorlib]System.Type builderType, string methodName) cil managed { ret }
                }
                .class public sealed MyCollection`1<T>
                {
                  .custom instance void System.Runtime.CompilerServices.CollectionBuilderAttribute::.ctor(class [mscorlib]System.Type, string) = { type(MyCollectionBuilder) string({{methodName}}) }
                  .method public hidebysig specialname rtspecialname instance void .ctor() cil managed { ret }
                  .method public instance class [mscorlib]System.Collections.Generic.IEnumerator`1<!T> GetEnumerator() { ldnull ret }
                }
                .class public sealed MyCollectionBuilder
                {
                  .method public hidebysig specialname rtspecialname instance void .ctor() cil managed { ret }
                  .method public static class MyCollection`1<!!T> Create<T>(valuetype System.ReadOnlySpan`1<!!T> items) { ldnull ret }
                }
                """;
            var refA = CompileIL(sourceA);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(sourceB, references: new[] { refA });
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible '' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible '' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InstanceMethod(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    public MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_OtherMember_01(
            [CombinatorialValues(
                "public MyCollection Create = null;",
                "public MyCollection Create => null;",
                "public class Create { }")]
            string createMember,
            bool useCompilationReference)
        {
            string sourceA = $$"""
                using System;
                using System.Collections;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public class MyCollection : IEnumerable
                {
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                        {{createMember}}
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [null];
                        MyCollection z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "object", "MyCollection").WithLocation(6, 26),
                // (7,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "object", "MyCollection").WithLocation(7, 26));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_TypeDifferences_Dynamic_01(bool useCompilationReference)
        {
            CollectionBuilder_TypeDifferences("object", "dynamic", "1, 2, 3", "[1, 2, 3]", useCompilationReference);
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_TypeDifferences_Dynamic_02(bool useCompilationReference)
        {
            string sourceA = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection
                {
                    private readonly List<dynamic> _list;
                    public MyCollection(List<dynamic> list) { _list = list; }
                    public IEnumerator<dynamic> GetEnumerator() => _list.GetEnumerator();
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<object> items)
                        => new MyCollection(new List<dynamic>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = $$"""
                using System.Collections.Generic;
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        GetElements(x).Report();
                        MyCollection y = [1, 2, 3];
                        GetElements(y).Report();
                    }
                    static IEnumerable<object> GetElements(MyCollection c)
                    {
                        foreach (var e in c) yield return e;
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput($"[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_TypeDifferences_TupleElementNames(bool useCompilationReference)
        {
            CollectionBuilder_TypeDifferences("(int, int)", "(int A, int B)", "(1, 2), default", "[(1, 2), (0, 0)]", useCompilationReference);
            CollectionBuilder_TypeDifferences("(int A, int B)", "(int, int)", "(1, 2), default", "[(1, 2), (0, 0)]", useCompilationReference);
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_TypeDifferences_Nullability(bool useCompilationReference)
        {
            CollectionBuilder_TypeDifferences("object", "object?", "1, 2, 3", "[1, 2, 3]", useCompilationReference);
            CollectionBuilder_TypeDifferences("object?", "object", "1, null, 3", "[1, null, 3]", useCompilationReference);
        }

        private void CollectionBuilder_TypeDifferences(string collectionElementType, string builderElementType, string values, string expectedOutput, bool useCompilationReference)
        {
            string sourceA = $$"""
                #nullable enable
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection : IEnumerable<{{collectionElementType}}>
                {
                    private readonly List<{{collectionElementType}}> _list;
                    public MyCollection(List<{{collectionElementType}}> list) { _list = list; }
                    public IEnumerator<{{collectionElementType}}> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<{{builderElementType}}> items)
                        => new MyCollection(new List<{{collectionElementType}}>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = $$"""
                #nullable enable
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        x.Report();
                        MyCollection y = [{{values}}];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput($"[], {expectedOutput}, "));
        }

        // If there are multiple attributes, the first is used.
        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_MultipleAttributes(bool useCompilationReference)
        {
            string sourceAttribute = """
                namespace System.Runtime.CompilerServices
                {
                    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
                    public sealed class CollectionBuilderAttribute : Attribute
                    {
                        public CollectionBuilderAttribute(Type builderType, string methodName) { }
                    }
                }
                """;
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder1), "Create1")]
                [CollectionBuilder(typeof(MyCollectionBuilder2), "Create2")]
                public sealed class MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public struct MyCollectionBuilder1
                {
                    public static MyCollection<T> Create1<T>(ReadOnlySpan<T> items)
                        => new MyCollection<T>(new List<T>(items.ToArray()));
                }
                public struct MyCollectionBuilder2
                {
                    public static MyCollection<T> Create2<T>(ReadOnlySpan<T> items)
                        => throw null;
                }
                """;
            var comp = CreateCompilation(new[] { sourceAttribute, sourceA }, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static MyCollection<int> F() => [1, 2, 3];
                    static void Main()
                    {
                        F().Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            comp = (CSharpCompilation)verifier.Compilation;

            var collectionType = (NamedTypeSymbol)comp.GetMember<MethodSymbol>("Program.F").ReturnType;
            Assert.Equal("MyCollection<System.Int32>", collectionType.ToTestDisplayString());
            TypeSymbol builderType;
            string methodName;
            Assert.True(collectionType.HasCollectionBuilderAttribute(out builderType, out methodName));
            Assert.Equal("MyCollectionBuilder1", builderType.ToTestDisplayString());
            Assert.Equal("Create1", methodName);
        }

        [Fact]
        public void CollectionBuilder_GenericBuilderType_01()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder<>), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public sealed class MyCollectionBuilder<T>
                {
                    public static MyCollection<T> Create(ReadOnlySpan<T> items) => default;
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(5,2): error CS9185: The CollectionBuilderAttribute builder type must be a non-generic class or struct.
                // [CollectionBuilder(typeof(MyCollectionBuilder<>), "Create")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidType, "CollectionBuilder").WithLocation(5, 2),
                // 1.cs(6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // 1.cs(7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_GenericBuilderType_02()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder<int>), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public sealed class MyCollectionBuilder<T>
                {
                    public static MyCollection<U> Create<U>(ReadOnlySpan<U> items) => default;
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(5,2): error CS9185: The CollectionBuilderAttribute builder type must be a non-generic class or struct.
                // [CollectionBuilder(typeof(MyCollectionBuilder<int>), "Create")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidType, "CollectionBuilder").WithLocation(5, 2),
                // 1.cs(6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // 1.cs(7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_GenericBuilderType_03()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                public class Container<T>
                {
                    [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                    public struct MyCollection : IEnumerable<T>
                    {
                        private readonly List<T> _list;
                        public MyCollection(List<T> list) { _list = list; }
                        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                    public sealed class MyCollectionBuilder
                    {
                        public static MyCollection Create(ReadOnlySpan<T> items)
                            => new MyCollection(new List<T>(items.ToArray()));
                    }
                }
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        Container<string>.MyCollection x = [];
                        Container<int>.MyCollection y = [default];
                        Container<object>.MyCollection z = new();
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(7,24): error CS0416: 'Container<T>.MyCollectionBuilder': an attribute argument cannot use type parameters
                //     [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                Diagnostic(ErrorCode.ERR_AttrArgWithTypeVars, "typeof(MyCollectionBuilder)").WithArguments("Container<T>.MyCollectionBuilder").WithLocation(7, 24),
                // 0.cs(27,42): error CS1061: 'Container<int>.MyCollection' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'Container<int>.MyCollection' could be found (are you missing a using directive or an assembly reference?)
                //         Container<int>.MyCollection y = [default];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "default").WithArguments("Container<int>.MyCollection", "Add").WithLocation(27, 42));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_GenericCollectionContainerType_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                public class Container<T>
                {
                    [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                    public struct MyCollection : IEnumerable<T>
                    {
                        private readonly List<T> _list;
                        public MyCollection(List<T> list) { _list = list; }
                        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                }
                public sealed class MyCollectionBuilder
                {
                    public static Container<T>.MyCollection Create<T>(ReadOnlySpan<T> items)
                        => new Container<T>.MyCollection(new List<T>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        Container<string>.MyCollection x = [];
                        x.Report(includeType: true);
                        Container<int>.MyCollection y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("(Container<T>.MyCollection<System.String>) [], (Container<T>.MyCollection<System.Int32>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_GenericCollectionContainerType_02(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                public class Container<T>
                {
                    [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                    public struct MyCollection : IEnumerable<int>
                    {
                        private readonly List<int> _list;
                        public MyCollection(List<int> list) { _list = list; }
                        public IEnumerator<int> GetEnumerator() => _list.GetEnumerator();
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                }
                public sealed class MyCollectionBuilder
                {
                    public static Container<T>.MyCollection Create<T>(ReadOnlySpan<int> items)
                        => new Container<T>.MyCollection(new List<int>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        Container<int>.MyCollection x = [];
                        x.Report(includeType: true);
                        Container<string>.MyCollection y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("(Container<T>.MyCollection<System.Int32>) [], (Container<T>.MyCollection<System.String>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_GenericCollectionContainerType_03(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                public class Container<T>
                {
                    [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                    public struct MyCollection<U> : IEnumerable<U>
                    {
                        private readonly List<U> _list;
                        public MyCollection(List<U> list) { _list = list; }
                        public IEnumerator<U> GetEnumerator() => _list.GetEnumerator();
                        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    }
                }
                public sealed class MyCollectionBuilder
                {
                    public static Container<T>.MyCollection<U> Create<T, U>(ReadOnlySpan<U> items)
                        => new Container<T>.MyCollection<U>(new List<U>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        Container<int>.MyCollection<string> x = [];
                        x.Report(includeType: true);
                        Container<string>.MyCollection<int> y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("(Container<T>.MyCollection<System.Int32, System.String>) [], (Container<T>.MyCollection<System.String, System.Int32>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_GenericType_ElementTypeFirstOfTwo(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T, U> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollection<T, U> Create<T, U>(ReadOnlySpan<T> items)
                        => new MyCollection<T, U>(new List<T>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string, int> x = [];
                        x.Report(includeType: true);
                        MyCollection<int, string> y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("(MyCollection<System.String, System.Int32>) [], (MyCollection<System.Int32, System.String>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_GenericType_ElementTypeSecondOfTwo(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T, U> : IEnumerable<U>
                {
                    private readonly List<U> _list;
                    public MyCollection(List<U> list) { _list = list; }
                    public IEnumerator<U> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollection<T, U> Create<T, U>(ReadOnlySpan<U> items)
                        => new MyCollection<T, U>(new List<U>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int, string> x = [];
                        x.Report(includeType: true);
                        MyCollection<string, int> y = [1, 2, 3];
                        y.Report(includeType: true);
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("(MyCollection<System.Int32, System.String>) [], (MyCollection<System.String, System.Int32>) [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InaccessibleBuilderType_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                internal class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_NestedBuilderType(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public class MyCollection : IEnumerable<int>
                {
                    private readonly List<int> _list;
                    public MyCollection(List<int> list) { _list = list; }
                    public IEnumerator<int> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    public struct MyCollectionBuilder
                    {
                        public static MyCollection Create(ReadOnlySpan<int> items)
                            => new MyCollection(new List<int>(items.ToArray()));
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        x.Report();
                        MyCollection y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InaccessibleBuilderType_02(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public class MyCollection : IEnumerable<int>
                {
                    public IEnumerator<int> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                    protected class MyCollectionBuilder
                    {
                        public static MyCollection Create(ReadOnlySpan<int> items) => default;
                    }
                    static readonly MyCollection _instance = [1, 2, 3];
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, 3];
                        MyCollection z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<int>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "int", "MyCollection").WithLocation(6, 26),
                // (7,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<int>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, 3]").WithArguments("Create", "int", "MyCollection").WithLocation(7, 26));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InaccessibleMethod(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    static readonly MyCollection<int> _instance = [1, 2, 3];
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    internal static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 31),
                // (7,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_Overloads_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>()
                    {
                        throw null;
                    }
                    public static MyCollection<T> Create<T>(Span<T> items)
                    {
                        throw null;
                    }
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items, int index = 0)
                    {
                        throw null;
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        x.Report();
                        MyCollection<int> y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_Overloads_02(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                    public static MyCollection<int> Create(ReadOnlySpan<int> items)
                    {
                        throw null;
                    }
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        x.Report();
                        MyCollection<int> y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_UnexpectedSignature_01(
            [CombinatorialValues(
                "public static MyCollection<int> Create(ReadOnlySpan<int> items) => default;", // constructed parameter and return types
                "public static MyCollection<T> Create<T>(ReadOnlySpan<int> items) => default;", // constructed parameter type
                "public static MyCollection<int> Create<T>(ReadOnlySpan<T> items) => default;", // constructed return type
                "public static MyCollection<T> Create<T>(ReadOnlySpan<T> items, int index = 0) => default;", // optional parameter
                "public static MyCollection<T> Create<T>() => default;", // no parameters
                "public static void Create<T>(ReadOnlySpan<T> items) { }", // no return type
                "public static MyCollection<T> Create<T>(ReadOnlySpan<T> items, int index = 0) => default;", // optional parameter
                "public static MyCollection<T> Create<T>(ReadOnlySpan<T> items, params object[] args) => default;", // params
                "public static MyCollection<T> Create<T, U>(ReadOnlySpan<T> items) => default;", // extra type parameter
                "public static MyCollection<T> Create<T>(Span<T> items) => default;", // Span<T>
                "public static MyCollection<T> Create<T>(T[] items) => default;", // T[]
                "public static MyCollection<T> Create<T>(in ReadOnlySpan<T> items) => default;", // in parameter
                "public static MyCollection<T> Create<T>(ref ReadOnlySpan<T> items) => default;", // ref parameter
                "public static MyCollection<T> Create<T>(out ReadOnlySpan<T> items) { items = default; return default; }")] // out parameter
            string methodDeclaration,
            bool useCompilationReference)
        {
            string sourceA = $$"""
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    {{methodDeclaration}}
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 34),
                // (7,31): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, 3]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(7, 31));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_UnexpectedSignature_MoreTypeParameters(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection : IEnumerable<object>
                {
                    IEnumerator<object> IEnumerable<object>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, 3];
                        MyCollection z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "object", "MyCollection").WithLocation(6, 26),
                // (7,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, 3]").WithArguments("Create", "object", "MyCollection").WithLocation(7, 26));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_UnexpectedSignature_FewerTypeParameters(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T, U> : IEnumerable<T>
                {
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollection<T, int> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string, int> x = [];
                        MyCollection<int, string> y = [1, 2, 3];
                        MyCollection<int, object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,39): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T, U>'.
                //         MyCollection<string, int> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T, U>").WithLocation(6, 39),
                // (7,39): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T, U>'.
                //         MyCollection<int, string> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, 3]").WithArguments("Create", "T", "MyCollection<T, U>").WithLocation(7, 39));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_InheritedAttributeOnBaseCollection(bool useCompilationReference)
        {
            string sourceAttribute = """
                namespace System.Runtime.CompilerServices
                {
                    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
                    public sealed class CollectionBuilderAttribute : Attribute
                    {
                        public CollectionBuilderAttribute(Type builderType, string methodName) { }
                    }
                }
                """;
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public abstract class MyCollectionBase : IEnumerable<int>
                {
                    public IEnumerator<int> GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => null;
                }
                public sealed class MyCollection : MyCollectionBase
                {
                }
                public sealed class MyCollectionBuilder
                {
                    public static MyCollectionBase Create(ReadOnlySpan<int> items) => new MyCollection();
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceAttribute }, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [2];
                        MyCollection z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,27): error CS1061: 'MyCollection' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'MyCollection' could be found (are you missing a using directive or an assembly reference?)
                //         MyCollection y = [2];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "2").WithArguments("MyCollection", "Add").WithLocation(6, 27));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_CreateMethodOnBase(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public sealed class MyCollection : IEnumerable<int>
                {
                    public IEnumerator<int> GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => null;
                }
                public abstract class MyCollectionBuilderBase
                {
                    public static MyCollection Create(ReadOnlySpan<int> items) => new MyCollection();
                }
                public sealed class MyCollectionBuilder : MyCollectionBuilderBase
                {
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, 3];
                        MyCollection z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (5,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<int>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "int", "MyCollection").WithLocation(5, 26),
                // (6,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<int>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, 3]").WithArguments("Create", "int", "MyCollection").WithLocation(6, 26));
        }

        [Fact]
        public void CollectionBuilder_Conversion_ImplicitReference_NonGeneric()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                abstract class MyCollectionBase : IEnumerable<int>
                {
                    private readonly List<int> _list;
                    protected MyCollectionBase(List<int> list) { _list = list; }
                    public IEnumerator<int> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                sealed class MyCollection : MyCollectionBase
                {
                    public MyCollection(List<int> list) : base(list) { }
                }
                class MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<int> items)
                    {
                        return new MyCollection(new List<int>(items.ToArray()));
                    }
                }
                """;
            string sourceB1 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollectionBase x = [];
                        x.Report();
                        MyCollectionBase y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceA, sourceB1, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));

            string sourceB2 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, 3];
                    }
                }
                """;
            var comp = CreateCompilation(
                new[] { sourceA, sourceB2 },
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 1.cs(5,26): error CS7036: There is no argument given that corresponds to the required parameter 'list' of 'MyCollection.MyCollection(List<int>)'
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "[]").WithArguments("list", "MyCollection.MyCollection(System.Collections.Generic.List<int>)").WithLocation(5, 26),
                // 1.cs(6,27): error CS1061: 'MyCollection' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'MyCollection' could be found (are you missing a using directive or an assembly reference?)
                //         MyCollection y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "1").WithArguments("MyCollection", "Add").WithLocation(6, 27),
                // 1.cs(6,30): error CS1061: 'MyCollection' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'MyCollection' could be found (are you missing a using directive or an assembly reference?)
                //         MyCollection y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "2").WithArguments("MyCollection", "Add").WithLocation(6, 30),
                // 1.cs(6,33): error CS1061: 'MyCollection' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'MyCollection' could be found (are you missing a using directive or an assembly reference?)
                //         MyCollection y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "3").WithArguments("MyCollection", "Add").WithLocation(6, 33));
        }

        [Fact]
        public void CollectionBuilder_Conversion_ImplicitReference_Generic()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                abstract class MyCollectionBase<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    protected MyCollectionBase(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                sealed class MyCollection<T> : MyCollectionBase<T>
                {
                    public MyCollection(List<T> list) : base(list) { }
                }
                class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                """;
            string sourceB1 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollectionBase<string> x = [];
                        x.Report();
                        MyCollectionBase<object> y = [1, 2, null];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceA, sourceB1, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, null], "));

            string sourceB2 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<object> y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                new[] { sourceA, sourceB2 },
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 1.cs(5,34): error CS7036: There is no argument given that corresponds to the required parameter 'list' of 'MyCollection<string>.MyCollection(List<string>)'
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "[]").WithArguments("list", "MyCollection<string>.MyCollection(System.Collections.Generic.List<string>)").WithLocation(5, 34),
                // 1.cs(6,35): error CS1061: 'MyCollection<object>' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'MyCollection<object>' could be found (are you missing a using directive or an assembly reference?)
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "1").WithArguments("MyCollection<object>", "Add").WithLocation(6, 35),
                // 1.cs(6,38): error CS1061: 'MyCollection<object>' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'MyCollection<object>' could be found (are you missing a using directive or an assembly reference?)
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "2").WithArguments("MyCollection<object>", "Add").WithLocation(6, 38),
                // 1.cs(6,41): error CS1061: 'MyCollection<object>' does not contain a definition for 'Add' and no accessible extension method 'Add' accepting a first argument of type 'MyCollection<object>' could be found (are you missing a using directive or an assembly reference?)
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_NoSuchMemberOrExtension, "null").WithArguments("MyCollection<object>", "Add").WithLocation(6, 41));
        }

        [Fact]
        public void CollectionBuilder_Conversion_ExplicitReference_NonGeneric()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                abstract class MyCollectionBase : IEnumerable<int>
                {
                    private readonly List<int> _list;
                    protected MyCollectionBase(List<int> list) { _list = list; }
                    public IEnumerator<int> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                sealed class MyCollection : MyCollectionBase
                {
                    public MyCollection(List<int> list) : base(list) { }
                }
                class MyCollectionBuilder
                {
                    public static MyCollectionBase Create(ReadOnlySpan<int> items)
                    {
                        return new MyCollection(new List<int>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, 3];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (28,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<int>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "int", "MyCollection").WithLocation(28, 26),
                // (29,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<int>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, 3]").WithArguments("Create", "int", "MyCollection").WithLocation(29, 26));
        }

        [Fact]
        public void CollectionBuilder_Conversion_ExplicitReference_Generic()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                abstract class MyCollectionBase<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    protected MyCollectionBase(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                sealed class MyCollection<T> : MyCollectionBase<T>
                {
                    public MyCollection(List<T> list) : base(list) { }
                }
                class MyCollectionBuilder
                {
                    public static MyCollectionBase<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<object> y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (28,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(28, 34),
                // (29,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(29, 34));
        }

        [Fact]
        public void CollectionBuilder_Conversion_Boxing_NonGeneric()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                interface IMyCollection : IEnumerable<object>
                {
                }
                struct MyCollection : IMyCollection
                {
                    private readonly List<object> _list;
                    public MyCollection(List<object> list) { _list = list; }
                    public IEnumerator<object> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static MyCollection Create(ReadOnlySpan<object> items)
                    {
                        return new MyCollection(new List<object>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        IMyCollection x = [];
                        x.Report();
                        IMyCollection y = [1, 2, null];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, null], "));
        }

        [Fact]
        public void CollectionBuilder_Conversion_Boxing_Generic()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                interface IMyCollection<T> : IEnumerable<T>
                {
                }
                struct MyCollection<T> : IMyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        IMyCollection<string> x = [];
                        x.Report();
                        IMyCollection<object> y = [1, 2, null];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, null], "));
        }

        [Fact]
        public void CollectionBuilder_Conversion_Unboxing_NonGeneric()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                interface IMyCollection : IEnumerable<object>
                {
                }
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                struct MyCollection : IMyCollection
                {
                    private readonly List<object> _list;
                    public MyCollection(List<object> list) { _list = list; }
                    public IEnumerator<object> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static IMyCollection Create(ReadOnlySpan<object> items)
                    {
                        return new MyCollection(new List<object>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (27,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "object", "MyCollection").WithLocation(27, 26),
                // (28,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "object", "MyCollection").WithLocation(28, 26));
        }

        [Fact]
        public void CollectionBuilder_Conversion_Unboxing_Generic()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                interface IMyCollection<T> : IEnumerable<T>
                {
                }
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                struct MyCollection<T> : IMyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static IMyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<object> y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (27,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(27, 34),
                // (28,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(28, 34));
        }

        [Fact]
        public void CollectionBuilder_Conversion_UserDefined_NonGeneric()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection : IEnumerable<object>
                {
                    public MyCollection(List<object> list) { }
                    public IEnumerator<object> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                    public static implicit operator MyCollection(OtherCollection other) => default;
                }
                class OtherCollection
                {
                    public OtherCollection(List<object> list) { }
                }
                class MyCollectionBuilder
                {
                    public static OtherCollection Create(ReadOnlySpan<object> items)
                    {
                        return new OtherCollection(new List<object>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (28,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "object", "MyCollection").WithLocation(28, 26),
                // (29,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "object", "MyCollection").WithLocation(29, 26));
        }

        [Fact]
        public void CollectionBuilder_Conversion_UserDefined_Generic()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection<T> : IEnumerable<T>
                {
                    public MyCollection(List<T> list) { }
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                    public static implicit operator MyCollection<T>(OtherCollection<T> other) => default;
                }
                class OtherCollection<T>
                {
                    public OtherCollection(List<T> list) { }
                }
                class MyCollectionBuilder
                {
                    public static OtherCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new OtherCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<object> y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (28,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(28, 34),
                // (29,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(29, 34));
        }

        [Fact]
        public void CollectionBuilder_Conversion_ExplicitNullable_NonGeneric()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                struct MyCollection
                {
                    private readonly List<object> _list;
                    public MyCollection(List<object> list) { _list = list; }
                    public IEnumerator<object> GetEnumerator() => _list.GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static MyCollection? Create(ReadOnlySpan<object> items)
                    {
                        return new MyCollection(new List<object>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (23,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "object", "MyCollection").WithLocation(23, 26),
                // (24,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "object", "MyCollection").WithLocation(24, 26));
        }

        [Fact]
        public void CollectionBuilder_Conversion_ExplicitNullable_Generic()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                struct MyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                }
                class MyCollectionBuilder
                {
                    public static MyCollection<T>? Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<object> y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (23,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(23, 34),
                // (24,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(24, 34));
        }

        [Fact]
        public void CollectionBuilder_Conversion_Dynamic_NonGeneric()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection : IEnumerable<object>
                {
                    public MyCollection(List<object> list) { }
                    public IEnumerator<object> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                class MyCollectionBuilder
                {
                    public static dynamic Create(ReadOnlySpan<object> items)
                    {
                        return new MyCollection(new List<object>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection x = [];
                        MyCollection y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (23,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "object", "MyCollection").WithLocation(23, 26),
                // (24,26): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<object>' and return type 'MyCollection'.
                //         MyCollection y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "object", "MyCollection").WithLocation(24, 26));
        }

        [Fact]
        public void CollectionBuilder_Conversion_Dynamic_Generic()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection<T> : IEnumerable<T>
                {
                    public MyCollection(List<T> list) { }
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                class MyCollectionBuilder
                {
                    public static dynamic Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<object> y = [1, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(
                source,
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (23,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(23, 34),
                // (24,34): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         MyCollection<object> y = [1, 2, null];
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[1, 2, null]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(24, 34));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ObsoleteBuilderType_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                [Obsolete]
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,34): warning CS0612: 'MyCollectionBuilder' is obsolete
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "[]").WithArguments("MyCollectionBuilder").WithLocation(6, 34),
                // (7,31): warning CS0612: 'MyCollectionBuilder' is obsolete
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "[1, 2, 3]").WithArguments("MyCollectionBuilder").WithLocation(7, 31));
        }

        [Fact]
        public void CollectionBuilder_ObsoleteBuilderType_02()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                [Obsolete("message 2", error: true)]
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(5,27): error CS0619: 'MyCollectionBuilder' is obsolete: 'message 2'
                // [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                Diagnostic(ErrorCode.ERR_DeprecatedSymbolStr, "MyCollectionBuilder").WithArguments("MyCollectionBuilder", "message 2").WithLocation(5, 27),
                // 1.cs(6,34): error CS0619: 'MyCollectionBuilder' is obsolete: 'message 2'
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_DeprecatedSymbolStr, "[]").WithArguments("MyCollectionBuilder", "message 2").WithLocation(6, 34),
                // 1.cs(7,31): error CS0619: 'MyCollectionBuilder' is obsolete: 'message 2'
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_DeprecatedSymbolStr, "[1, 2, 3]").WithArguments("MyCollectionBuilder", "message 2").WithLocation(7, 31));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ObsoleteBuilderMethod_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    [Obsolete]
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,34): warning CS0612: 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)' is obsolete
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "[]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)").WithLocation(6, 34),
                // (7,31): warning CS0612: 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)' is obsolete
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.WRN_DeprecatedSymbol, "[1, 2, 3]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)").WithLocation(7, 31));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_ObsoleteBuilderMethod_02(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    [Obsolete("message 4", error: true)]
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,34): error CS0619: 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)' is obsolete: 'message 4'
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_DeprecatedSymbolStr, "[]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)", "message 4").WithLocation(6, 34),
                // (7,31): error CS0619: 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)' is obsolete: 'message 4'
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_DeprecatedSymbolStr, "[1, 2, 3]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)", "message 4").WithLocation(7, 31));
        }

        [Fact]
        public void CollectionBuilder_UnmanagedCallersOnly()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                using System.Runtime.InteropServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    [UnmanagedCallersOnly]
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<object> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 1.cs(6,34): error CS8901: 'MyCollectionBuilder.Create<string>(ReadOnlySpan<string>)' is attributed with 'UnmanagedCallersOnly' and cannot be called directly. Obtain a function pointer to this method.
                //         MyCollection<string> x = [];
                Diagnostic(ErrorCode.ERR_UnmanagedCallersOnlyMethodsCannotBeCalledDirectly, "[]").WithArguments("MyCollectionBuilder.Create<string>(System.ReadOnlySpan<string>)").WithLocation(6, 34),
                // 1.cs(7,31): error CS8901: 'MyCollectionBuilder.Create<int>(ReadOnlySpan<int>)' is attributed with 'UnmanagedCallersOnly' and cannot be called directly. Obtain a function pointer to this method.
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_UnmanagedCallersOnlyMethodsCannotBeCalledDirectly, "[1, 2, 3]").WithArguments("MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)").WithLocation(7, 31),
                // 0.cs(14,6): error CS8895: Methods attributed with 'UnmanagedCallersOnly' cannot have generic type parameters and cannot be declared in a generic type.
                //     [UnmanagedCallersOnly]
                Diagnostic(ErrorCode.ERR_UnmanagedCallersOnlyMethodOrTypeCannotBeGeneric, "UnmanagedCallersOnly").WithLocation(14, 6),
                // 0.cs(15,45): error CS8894: Cannot use 'ReadOnlySpan<T>' as a parameter type on a method attributed with 'UnmanagedCallersOnly'.
                //     public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                Diagnostic(ErrorCode.ERR_CannotUseManagedTypeInUnmanagedCallersOnly, "ReadOnlySpan<T> items").WithArguments("System.ReadOnlySpan<T>", "parameter").WithLocation(15, 45));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_Constraints_CollectionAndBuilder(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T> where T : new()
                {
                    private List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) where T : struct
                        => new MyCollection<T>(new List<T>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB1 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        x.Report();
                        MyCollection<int> y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB1, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));

            string sourceB2 = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int?> x = [4, null];
                        MyCollection<int?> y = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB2, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,32): error CS0453: The type 'int?' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)'
                //         MyCollection<int?> x = [4, null];
                Diagnostic(ErrorCode.ERR_ValConstraintNotSatisfied, "[4, null]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)", "T", "int?").WithLocation(6, 32));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_Constraints_BuilderOnly(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) where T : struct
                        => new MyCollection<T>(new List<T>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB1 = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        x.Report();
                        MyCollection<int> y = [1, 2, 3];
                        y.Report();
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB1, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));

            string sourceB2 = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int?> x = [4, null];
                        MyCollection<int?> y = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB2, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,32): error CS0453: The type 'int?' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)'
                //         MyCollection<int?> x = [4, null];
                Diagnostic(ErrorCode.ERR_ValConstraintNotSatisfied, "[4, null]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)", "T", "int?").WithLocation(6, 32));
        }

        [Fact]
        public void CollectionBuilder_Constraints_CollectionOnly()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T> where T : class
                {
                    private List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                        => default;
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<string> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 1.cs(7,22): error CS0452: The type 'int' must be a reference type in order to use it as parameter 'T' in the generic type or method 'MyCollection<T>'
                //         MyCollection<int> y = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_RefConstraintNotSatisfied, "int").WithArguments("MyCollection<T>", "T", "int").WithLocation(7, 22),
                // 0.cs(15,35): error CS0452: The type 'T' must be a reference type in order to use it as parameter 'T' in the generic type or method 'MyCollection<T>'
                //     public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                Diagnostic(ErrorCode.ERR_RefConstraintNotSatisfied, "Create").WithArguments("MyCollection<T>", "T", "T").WithLocation(15, 35));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_Substituted_01(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        F([]);
                        F([1, 2, 3]);
                    }
                    static void F(MyCollection<int> c)
                    {
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics();

            var collectionType = (NamedTypeSymbol)comp.GetMember<MethodSymbol>("Program.F").Parameters[0].Type;
            Assert.Equal("MyCollection<System.Int32>", collectionType.ToTestDisplayString());
            TypeSymbol builderType;
            string methodName;
            Assert.True(collectionType.HasCollectionBuilderAttribute(out builderType, out methodName));
            Assert.Equal("MyCollectionBuilder", builderType.ToTestDisplayString());
            Assert.Equal("Create", methodName);

            var originalType = collectionType.OriginalDefinition;
            Assert.Equal("MyCollection<T>", originalType.ToTestDisplayString());
            Assert.True(originalType.HasCollectionBuilderAttribute(out builderType, out methodName));
            Assert.Equal("MyCollectionBuilder", builderType.ToTestDisplayString());
            Assert.Equal("Create", methodName);
        }

        [Fact]
        public void CollectionBuilder_Substituted_02()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(Container<string>.MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class Container<T>
                {
                    public class MyCollectionBuilder
                    {
                        public static MyCollection<U> Create<U>(ReadOnlySpan<U> items) => default;
                    }
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        F([]);
                        F(new());
                    }
                    static void F(MyCollection<int> c)
                    {
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(5,2): error CS9185: The CollectionBuilderAttribute builder type must be a non-generic class or struct.
                // [CollectionBuilder(typeof(Container<string>.MyCollectionBuilder), "Create")]
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeInvalidType, "CollectionBuilder").WithLocation(5, 2),
                // 1.cs(6,11): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         F([]);
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 11));

            var collectionType = (NamedTypeSymbol)comp.GetMember<MethodSymbol>("Program.F").Parameters[0].Type;
            Assert.Equal("MyCollection<System.Int32>", collectionType.ToTestDisplayString());
            TypeSymbol builderType;
            string methodName;
            Assert.True(collectionType.HasCollectionBuilderAttribute(out builderType, out methodName));
            Assert.Equal("Container<System.String>.MyCollectionBuilder", builderType.ToTestDisplayString());
            Assert.Equal("Create", methodName);

            var originalType = collectionType.OriginalDefinition;
            Assert.Equal("MyCollection<T>", originalType.ToTestDisplayString());
            Assert.True(originalType.HasCollectionBuilderAttribute(out builderType, out methodName));
            Assert.Equal("Container<System.String>.MyCollectionBuilder", builderType.ToTestDisplayString());
            Assert.Equal("Create", methodName);
        }

        [Fact]
        public void CollectionBuilder_Substituted_03()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                public class Container<T>
                {
                    [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                    public struct MyCollection : IEnumerable<T>
                    {
                        public IEnumerator<T> GetEnumerator() => default;
                        IEnumerator IEnumerable.GetEnumerator() => default;
                    }
                    public class MyCollectionBuilder
                    {
                        public static MyCollection Create(ReadOnlySpan<T> items) => default;
                    }
                }
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        F([]);
                        F(new());
                    }
                    static void F(Container<string>.MyCollection c)
                    {
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(7,24): error CS0416: 'Container<T>.MyCollectionBuilder': an attribute argument cannot use type parameters
                //     [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                Diagnostic(ErrorCode.ERR_AttrArgWithTypeVars, "typeof(MyCollectionBuilder)").WithArguments("Container<T>.MyCollectionBuilder").WithLocation(7, 24));

            var collectionType = (NamedTypeSymbol)comp.GetMember<MethodSymbol>("Program.F").Parameters[0].Type;
            Assert.Equal("Container<System.String>.MyCollection", collectionType.ToTestDisplayString());
            Assert.False(collectionType.HasCollectionBuilderAttribute(out _, out _));

            var originalType = collectionType.OriginalDefinition;
            Assert.Equal("Container<T>.MyCollection", originalType.ToTestDisplayString());
            Assert.False(originalType.HasCollectionBuilderAttribute(out _, out _));
        }

        [Fact]
        public void CollectionBuilder_Retargeting()
        {
            string sourceA = """
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    public static void Create(int[] items) { }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, CollectionBuilderAttributeDefinition }, targetFramework: TargetFramework.Mscorlib40);
            comp.VerifyEmitDiagnostics();
            var refA = comp.ToMetadataReference();

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        F([]);
                        F(new());
                    }
                    static void F(MyCollection<int> c)
                    {
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Mscorlib45);
            comp.VerifyEmitDiagnostics(
                // (6,11): error CS9187: Could not find an accessible 'Create' method with the expected signature: a static method with a single parameter of type 'ReadOnlySpan<T>' and return type 'MyCollection<T>'.
                //         F([]);
                Diagnostic(ErrorCode.ERR_CollectionBuilderAttributeMethodNotFound, "[]").WithArguments("Create", "T", "MyCollection<T>").WithLocation(6, 11));

            var collectionType = (NamedTypeSymbol)comp.GetMember<MethodSymbol>("Program.F").Parameters[0].Type;
            Assert.Equal("MyCollection<System.Int32>", collectionType.ToTestDisplayString());
            TypeSymbol builderType;
            string methodName;
            Assert.True(collectionType.HasCollectionBuilderAttribute(out builderType, out methodName));
            Assert.Equal("MyCollectionBuilder", builderType.ToTestDisplayString());
            Assert.Equal("Create", methodName);

            var retargetingType = (RetargetingNamedTypeSymbol)collectionType.OriginalDefinition;
            Assert.Equal("MyCollection<T>", retargetingType.ToTestDisplayString());
            Assert.True(retargetingType.HasCollectionBuilderAttribute(out builderType, out methodName));
            Assert.IsType<RetargetingNamedTypeSymbol>(builderType);
            Assert.Equal("MyCollectionBuilder", builderType.ToTestDisplayString());
            Assert.Equal("Create", methodName);
        }

        [Fact]
        public void CollectionBuilder_ExtensionMethodGetEnumerator_01()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection<T>
                {
                }
                class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                namespace N
                {
                    static class Extensions
                    {
                        public static IEnumerator<T> GetEnumerator<T>(this MyCollection<T> c) => default;
                        static MyCollection<T> F<T>() => [];
                    }
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> c = [];
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (24,31): error CS9188: 'MyCollection<int>' has a CollectionBuilderAttribute but no element type.
                //         MyCollection<int> c = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderNoElementType, "[]").WithArguments("MyCollection<int>").WithLocation(24, 31));
        }

        [Fact]
        public void CollectionBuilder_ExtensionMethodGetEnumerator_02()
        {
            string sourceA = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public class MyCollection<T>
                {
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                static class Extensions
                {
                    public static IEnumerator<T> GetEnumerator<T>(this MyCollection<T> c) => default;
                    static MyCollection<T> F<T>() => [];
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics();
            var refA = comp.EmitToImageReference();

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> c = [];
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9188: 'MyCollection<int>' has a CollectionBuilderAttribute but no element type.
                //         MyCollection<int> c = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderNoElementType, "[]").WithArguments("MyCollection<int>").WithLocation(6, 31));
        }

        [Fact]
        public void CollectionBuilder_InaccessibleGetEnumerator()
        {
            string source = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                class MyCollection<T>
                {
                    internal IEnumerator<T> GetEnumerator() => default;
                    public static MyCollection<T> F() => [];
                }
                class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> c = [];
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (8,42): error CS9188: 'MyCollection<T>' has a CollectionBuilderAttribute but no element type.
                //     public static MyCollection<T> F() => [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderNoElementType, "[]").WithArguments("MyCollection<T>").WithLocation(8, 42),
                // (18,31): error CS9188: 'MyCollection<int>' has a CollectionBuilderAttribute but no element type.
                //         MyCollection<int> c = [];
                Diagnostic(ErrorCode.ERR_CollectionBuilderNoElementType, "[]").WithArguments("MyCollection<int>").WithLocation(18, 31));
        }

        [InlineData("", "", false)]
        [InlineData("", "", true)]
        [InlineData("scoped", "", false)]
        [InlineData("scoped", "", true)]
        [InlineData("scoped", "scoped", false)]
        [InlineData("scoped", "scoped", true)]
        [Theory]
        public void CollectionBuilder_Scoped(string constructorParameterModifier, string builderParameterModifier, bool useCompilationReference)
        {
            string sourceA = $$"""
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public ref struct MyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection({{constructorParameterModifier}} ReadOnlySpan<T> items)
                    {
                        _list = new List<T>(items.ToArray());
                    }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>({{builderParameterModifier}} ReadOnlySpan<T> items) => new(items);
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics();
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        GetItems(x).Report();
                        MyCollection<int> y = [1, 2, 3];
                        GetItems(y).Report();
                    }
                    static List<T> GetItems<T>(MyCollection<T> c)
                    {
                        var list = new List<T>();
                        foreach (var i in c) list.Add(i);
                        return list;
                    }
                }
                """;
            CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
        }

        [Fact]
        public void CollectionBuilder_ScopedBuilderParameterOnly()
        {
            string sourceA = $$"""
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public ref struct MyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(ReadOnlySpan<T> items)
                    {
                        _list = new List<T>(items.ToArray());
                    }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(scoped ReadOnlySpan<T> items) => new(items);
                }
                """;
            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<int> y = [1, 2, 3];
                        MyCollection<string> z = new();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(16,78): error CS8347: Cannot use a result of 'MyCollection<T>.MyCollection(ReadOnlySpan<T>)' in this context because it may expose variables referenced by parameter 'items' outside of their declaration scope
                //     public static MyCollection<T> Create<T>(scoped ReadOnlySpan<T> items) => new(items);
                Diagnostic(ErrorCode.ERR_EscapeCall, "new(items)").WithArguments("MyCollection<T>.MyCollection(System.ReadOnlySpan<T>)", "items").WithLocation(16, 78),
                // 0.cs(16,82): error CS8352: Cannot use variable 'scoped ReadOnlySpan<T> items' in this context because it may expose referenced variables outside of their declaration scope
                //     public static MyCollection<T> Create<T>(scoped ReadOnlySpan<T> items) => new(items);
                Diagnostic(ErrorCode.ERR_EscapeVariable, "items").WithArguments("scoped System.ReadOnlySpan<T> items").WithLocation(16, 82));
        }

        [CombinatorialData]
        [Theory]
        public void CollectionBuilder_MissingInt32(bool useCompilationReference)
        {
            string sourceA = """
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T>
                {
                    public IEnumerator<T> GetEnumerator() => default;
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<string> x = [];
                        MyCollection<string> y = ["2"];
                        MyCollection<object> z = new();
                    }
                }
                """;
            comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.MakeTypeMissing(SpecialType.System_Int32);
            comp.VerifyEmitDiagnostics(
                // (7,34): error CS0518: Predefined type 'System.Int32' is not defined or imported
                //         MyCollection<string> y = ["2"];
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, @"[""2""]").WithArguments("System.Int32").WithLocation(7, 34),
                // (7,34): error CS0518: Predefined type 'System.Int32' is not defined or imported
                //         MyCollection<string> y = ["2"];
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, @"[""2""]").WithArguments("System.Int32").WithLocation(7, 34),
                // (7,34): error CS0518: Predefined type 'System.Int32' is not defined or imported
                //         MyCollection<string> y = ["2"];
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, @"[""2""]").WithArguments("System.Int32").WithLocation(7, 34));
        }

        [Fact]
        public void CollectionBuilder_UseSiteError_Method()
        {
            // [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
            // public sealed class MyCollection<T>
            // {
            //     public IEnumerator<T> GetEnumerator() { }
            // }
            // public static class MyCollectionBuilder
            // {
            //     [CompilerFeatureRequired("MyFeature")]
            //     public static MyCollection<T> MyCollectionBuilder.Create<T>(ReadOnlySpan<T>) { }
            // }
            string sourceA = """
                .assembly extern System.Runtime { .ver 8:0:0:0 .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A) }
                .class public sealed MyCollection`1<T>
                {
                  .custom instance void [System.Runtime]System.Runtime.CompilerServices.CollectionBuilderAttribute::.ctor(class [System.Runtime]System.Type, string) = { type(MyCollectionBuilder) string('Create') }
                  .method public hidebysig specialname rtspecialname instance void .ctor() cil managed { ret }
                  .method public instance class [System.Runtime]System.Collections.Generic.IEnumerator`1<!T> GetEnumerator() { ldnull ret }
                }
                .class public abstract sealed MyCollectionBuilder
                {
                  .method public static class MyCollection`1<!!T> Create<T>(valuetype [System.Runtime]System.ReadOnlySpan`1<!!T> items)
                  {
                    .custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute::.ctor(string) = { string('MyFeature') }
                    ldnull ret
                  }
                }
                """;
            var refA = CompileIL(sourceA);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = MyCollectionBuilder.Create<object>(default);
                    }
                }
                """;
            var comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9041: 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)' requires compiler feature 'MyFeature', which is not supported by this version of the C# compiler.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_UnsupportedCompilerFeature, "[]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)", "MyFeature").WithLocation(6, 31),
                // (7,34): error CS9041: 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)' requires compiler feature 'MyFeature', which is not supported by this version of the C# compiler.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_UnsupportedCompilerFeature, "[null]").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)", "MyFeature").WithLocation(7, 34),
                // (8,54): error CS9041: 'MyCollectionBuilder.Create<T>(ReadOnlySpan<T>)' requires compiler feature 'MyFeature', which is not supported by this version of the C# compiler.
                //         MyCollection<object> z = MyCollectionBuilder.Create<object>(default);
                Diagnostic(ErrorCode.ERR_UnsupportedCompilerFeature, "Create<object>").WithArguments("MyCollectionBuilder.Create<T>(System.ReadOnlySpan<T>)", "MyFeature").WithLocation(8, 54));
        }

        [Fact]
        public void CollectionBuilder_UseSiteError_ContainingType()
        {
            // [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
            // public sealed class MyCollection<T>
            // {
            //     public IEnumerator<T> GetEnumerator() { }
            // }
            // [CompilerFeatureRequired("MyFeature")]
            // public static class MyCollectionBuilder
            // {
            //     public static MyCollection<T> MyCollectionBuilder.Create<T>(ReadOnlySpan<T>) { }
            // }
            string sourceA = """
                .assembly extern System.Runtime { .ver 8:0:0:0 .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A) }
                .class public sealed MyCollection`1<T>
                {
                  .custom instance void [System.Runtime]System.Runtime.CompilerServices.CollectionBuilderAttribute::.ctor(class [System.Runtime]System.Type, string) = { type(MyCollectionBuilder) string('Create') }
                  .method public hidebysig specialname rtspecialname instance void .ctor() cil managed { ret }
                  .method public instance class [System.Runtime]System.Collections.Generic.IEnumerator`1<!T> GetEnumerator() { ldnull ret }
                }
                .class public abstract sealed MyCollectionBuilder
                {
                  .custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute::.ctor(string) = { string('MyFeature') }
                  .method public static class MyCollection`1<!!T> Create<T>(valuetype [System.Runtime]System.ReadOnlySpan`1<!!T> items)
                  {
                    ldnull ret
                  }
                }
                """;
            var refA = CompileIL(sourceA);

            string sourceB = """
                #pragma warning disable 219
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> x = [];
                        MyCollection<string> y = [null];
                        MyCollection<object> z = MyCollectionBuilder.Create<object>(default);
                    }
                }
                """;
            var comp = CreateCompilation(sourceB, references: new[] { refA }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS9041: 'MyCollectionBuilder' requires compiler feature 'MyFeature', which is not supported by this version of the C# compiler.
                //         MyCollection<int> x = [];
                Diagnostic(ErrorCode.ERR_UnsupportedCompilerFeature, "[]").WithArguments("MyCollectionBuilder", "MyFeature").WithLocation(6, 31),
                // (7,34): error CS9041: 'MyCollectionBuilder' requires compiler feature 'MyFeature', which is not supported by this version of the C# compiler.
                //         MyCollection<string> y = [null];
                Diagnostic(ErrorCode.ERR_UnsupportedCompilerFeature, "[null]").WithArguments("MyCollectionBuilder", "MyFeature").WithLocation(7, 34),
                // (8,34): error CS9041: 'MyCollectionBuilder' requires compiler feature 'MyFeature', which is not supported by this version of the C# compiler.
                //         MyCollection<object> z = MyCollectionBuilder.Create<object>(default);
                Diagnostic(ErrorCode.ERR_UnsupportedCompilerFeature, "MyCollectionBuilder").WithArguments("MyCollectionBuilder", "MyFeature").WithLocation(8, 34),
                // (8,54): error CS9041: 'MyCollectionBuilder' requires compiler feature 'MyFeature', which is not supported by this version of the C# compiler.
                //         MyCollection<object> z = MyCollectionBuilder.Create<object>(default);
                Diagnostic(ErrorCode.ERR_UnsupportedCompilerFeature, "Create<object>").WithArguments("MyCollectionBuilder", "MyFeature").WithLocation(8, 54));
        }

        [Fact]
        public void CollectionBuilder_Async()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }
                """;
            string sourceB = """
                using System.Collections.Generic;
                using System.Threading.Tasks;
                class Program
                {
                    static async Task Main()
                    {
                        (await CreateCollection()).Report();
                    }
                    static async Task<MyCollection<int>> CreateCollection()
                    {
                        return [await F(1), 2, await F(3)];
                    }
                    static async Task<int> F(int i)
                    {
                        await Task.Yield();
                        return i;
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { sourceA, sourceB, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.<CreateCollection>d__1.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()",
                """
                {
                  // Code size      324 (0x144)
                  .maxstack  3
                  .locals init (int V_0,
                                MyCollection<int> V_1,
                                int V_2,
                                int V_3,
                                System.Runtime.CompilerServices.TaskAwaiter<int> V_4,
                                System.Exception V_5)
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "int Program.<CreateCollection>d__1.<>1__state"
                  IL_0006:  stloc.0
                  .try
                  {
                    IL_0007:  ldloc.0
                    IL_0008:  brfalse.s  IL_0057
                    IL_000a:  ldloc.0
                    IL_000b:  ldc.i4.1
                    IL_000c:  beq        IL_00cf
                    IL_0011:  ldarg.0
                    IL_0012:  ldflda     "<>y__InlineArray3<int> Program.<CreateCollection>d__1.<>7__wrap1"
                    IL_0017:  initobj    "<>y__InlineArray3<int>"
                    IL_001d:  ldc.i4.1
                    IL_001e:  call       "System.Threading.Tasks.Task<int> Program.F(int)"
                    IL_0023:  callvirt   "System.Runtime.CompilerServices.TaskAwaiter<int> System.Threading.Tasks.Task<int>.GetAwaiter()"
                    IL_0028:  stloc.s    V_4
                    IL_002a:  ldloca.s   V_4
                    IL_002c:  call       "bool System.Runtime.CompilerServices.TaskAwaiter<int>.IsCompleted.get"
                    IL_0031:  brtrue.s   IL_0074
                    IL_0033:  ldarg.0
                    IL_0034:  ldc.i4.0
                    IL_0035:  dup
                    IL_0036:  stloc.0
                    IL_0037:  stfld      "int Program.<CreateCollection>d__1.<>1__state"
                    IL_003c:  ldarg.0
                    IL_003d:  ldloc.s    V_4
                    IL_003f:  stfld      "System.Runtime.CompilerServices.TaskAwaiter<int> Program.<CreateCollection>d__1.<>u__1"
                    IL_0044:  ldarg.0
                    IL_0045:  ldflda     "System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>> Program.<CreateCollection>d__1.<>t__builder"
                    IL_004a:  ldloca.s   V_4
                    IL_004c:  ldarg.0
                    IL_004d:  call       "void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<int>, Program.<CreateCollection>d__1>(ref System.Runtime.CompilerServices.TaskAwaiter<int>, ref Program.<CreateCollection>d__1)"
                    IL_0052:  leave      IL_0143
                    IL_0057:  ldarg.0
                    IL_0058:  ldfld      "System.Runtime.CompilerServices.TaskAwaiter<int> Program.<CreateCollection>d__1.<>u__1"
                    IL_005d:  stloc.s    V_4
                    IL_005f:  ldarg.0
                    IL_0060:  ldflda     "System.Runtime.CompilerServices.TaskAwaiter<int> Program.<CreateCollection>d__1.<>u__1"
                    IL_0065:  initobj    "System.Runtime.CompilerServices.TaskAwaiter<int>"
                    IL_006b:  ldarg.0
                    IL_006c:  ldc.i4.m1
                    IL_006d:  dup
                    IL_006e:  stloc.0
                    IL_006f:  stfld      "int Program.<CreateCollection>d__1.<>1__state"
                    IL_0074:  ldloca.s   V_4
                    IL_0076:  call       "int System.Runtime.CompilerServices.TaskAwaiter<int>.GetResult()"
                    IL_007b:  stloc.2
                    IL_007c:  ldarg.0
                    IL_007d:  ldflda     "<>y__InlineArray3<int> Program.<CreateCollection>d__1.<>7__wrap1"
                    IL_0082:  ldc.i4.0
                    IL_0083:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                    IL_0088:  ldloc.2
                    IL_0089:  stind.i4
                    IL_008a:  ldarg.0
                    IL_008b:  ldflda     "<>y__InlineArray3<int> Program.<CreateCollection>d__1.<>7__wrap1"
                    IL_0090:  ldc.i4.1
                    IL_0091:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                    IL_0096:  ldc.i4.2
                    IL_0097:  stind.i4
                    IL_0098:  ldc.i4.3
                    IL_0099:  call       "System.Threading.Tasks.Task<int> Program.F(int)"
                    IL_009e:  callvirt   "System.Runtime.CompilerServices.TaskAwaiter<int> System.Threading.Tasks.Task<int>.GetAwaiter()"
                    IL_00a3:  stloc.s    V_4
                    IL_00a5:  ldloca.s   V_4
                    IL_00a7:  call       "bool System.Runtime.CompilerServices.TaskAwaiter<int>.IsCompleted.get"
                    IL_00ac:  brtrue.s   IL_00ec
                    IL_00ae:  ldarg.0
                    IL_00af:  ldc.i4.1
                    IL_00b0:  dup
                    IL_00b1:  stloc.0
                    IL_00b2:  stfld      "int Program.<CreateCollection>d__1.<>1__state"
                    IL_00b7:  ldarg.0
                    IL_00b8:  ldloc.s    V_4
                    IL_00ba:  stfld      "System.Runtime.CompilerServices.TaskAwaiter<int> Program.<CreateCollection>d__1.<>u__1"
                    IL_00bf:  ldarg.0
                    IL_00c0:  ldflda     "System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>> Program.<CreateCollection>d__1.<>t__builder"
                    IL_00c5:  ldloca.s   V_4
                    IL_00c7:  ldarg.0
                    IL_00c8:  call       "void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<int>, Program.<CreateCollection>d__1>(ref System.Runtime.CompilerServices.TaskAwaiter<int>, ref Program.<CreateCollection>d__1)"
                    IL_00cd:  leave.s    IL_0143
                    IL_00cf:  ldarg.0
                    IL_00d0:  ldfld      "System.Runtime.CompilerServices.TaskAwaiter<int> Program.<CreateCollection>d__1.<>u__1"
                    IL_00d5:  stloc.s    V_4
                    IL_00d7:  ldarg.0
                    IL_00d8:  ldflda     "System.Runtime.CompilerServices.TaskAwaiter<int> Program.<CreateCollection>d__1.<>u__1"
                    IL_00dd:  initobj    "System.Runtime.CompilerServices.TaskAwaiter<int>"
                    IL_00e3:  ldarg.0
                    IL_00e4:  ldc.i4.m1
                    IL_00e5:  dup
                    IL_00e6:  stloc.0
                    IL_00e7:  stfld      "int Program.<CreateCollection>d__1.<>1__state"
                    IL_00ec:  ldloca.s   V_4
                    IL_00ee:  call       "int System.Runtime.CompilerServices.TaskAwaiter<int>.GetResult()"
                    IL_00f3:  stloc.3
                    IL_00f4:  ldarg.0
                    IL_00f5:  ldflda     "<>y__InlineArray3<int> Program.<CreateCollection>d__1.<>7__wrap1"
                    IL_00fa:  ldc.i4.2
                    IL_00fb:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                    IL_0100:  ldloc.3
                    IL_0101:  stind.i4
                    IL_0102:  ldarg.0
                    IL_0103:  ldflda     "<>y__InlineArray3<int> Program.<CreateCollection>d__1.<>7__wrap1"
                    IL_0108:  ldc.i4.3
                    IL_0109:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<int>, int>(in <>y__InlineArray3<int>, int)"
                    IL_010e:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
                    IL_0113:  stloc.1
                    IL_0114:  leave.s    IL_012f
                  }
                  catch System.Exception
                  {
                    IL_0116:  stloc.s    V_5
                    IL_0118:  ldarg.0
                    IL_0119:  ldc.i4.s   -2
                    IL_011b:  stfld      "int Program.<CreateCollection>d__1.<>1__state"
                    IL_0120:  ldarg.0
                    IL_0121:  ldflda     "System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>> Program.<CreateCollection>d__1.<>t__builder"
                    IL_0126:  ldloc.s    V_5
                    IL_0128:  call       "void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>>.SetException(System.Exception)"
                    IL_012d:  leave.s    IL_0143
                  }
                  IL_012f:  ldarg.0
                  IL_0130:  ldc.i4.s   -2
                  IL_0132:  stfld      "int Program.<CreateCollection>d__1.<>1__state"
                  IL_0137:  ldarg.0
                  IL_0138:  ldflda     "System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>> Program.<CreateCollection>d__1.<>t__builder"
                  IL_013d:  ldloc.1
                  IL_013e:  call       "void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<MyCollection<int>>.SetResult(MyCollection<int>)"
                  IL_0143:  ret
                }
                """);
        }

        [Fact]
        public void CollectionBuilder_AttributeCycle()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;

                [CollectionBuilder(typeof(MyCollectionBuilder), MyCollectionBuilder.GetName([1, 2, 3]))]
                class MyCollection<T> : IEnumerable<T>
                {
                    public void Add(T t) { }
                    public IEnumerator<T> GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }

                static class MyCollectionBuilder
                {
                    public static string GetName<T>(MyCollection<T> c) => null;
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => null;
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(6,49): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
                // [CollectionBuilder(typeof(MyCollectionBuilder), MyCollectionBuilder.GetName([1, 2, 3]))]
                Diagnostic(ErrorCode.ERR_BadAttributeArgument, "MyCollectionBuilder.GetName([1, 2, 3])").WithLocation(6, 49));
        }

        [Fact]
        public void CollectionBuilder_AttributeCycle_2()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;

                [CollectionBuilder(typeof(MyCollectionBuilder), ['h', 'i'])]
                class MyCollection<T> : IEnumerable<T>
                {
                    public void Add(T t) { }
                    public IEnumerator<T> GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }

                static class MyCollectionBuilder
                {
                    public static string GetName<T>(MyCollection<T> c) => null;
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => null;
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,49): error CS1503: Argument 2: cannot convert from 'collection expressions' to 'string'
                // [CollectionBuilder(typeof(MyCollectionBuilder), ['h', 'i'])]
                Diagnostic(ErrorCode.ERR_BadArgType, "['h', 'i']").WithArguments("2", "collection expressions", "string").WithLocation(6, 49));
        }

        [Fact]
        public void CollectionBuilder_AttributeCycle_3()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;

                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                [MyCollection<int>([1, 2, 3])]
                class MyCollection<T> : Attribute, IEnumerable<T>
                {
                    public MyCollection(MyCollection<T> mc) { }
                    public void Add(T t) { }
                    public IEnumerator<T> GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }

                static class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => null;
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (7,2): error CS0181: Attribute constructor parameter 'mc' has type 'MyCollection<int>', which is not a valid attribute parameter type
                // [MyCollection<int>([1, 2, 3])]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "MyCollection<int>").WithArguments("mc", "MyCollection<int>").WithLocation(7, 2));
        }

        [WorkItem("https://github.com/dotnet/roslyn/issues/69980")]
        [Fact]
        public void ElementConversion_CollectionBuilder()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), "Create")]
                class MyCollection<T> : IEnumerable<T>
                {
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items) => default;
                }
                """;
            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> c = [string.Empty, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 1.cs(5,32): error CS0029: Cannot implicitly convert type 'string' to 'int'
                //         MyCollection<int> c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "string.Empty").WithArguments("string", "int").WithLocation(5, 32),
                // 1.cs(5,49): error CS0037: Cannot convert null to 'int' because it is a non-nullable value type
                //         MyCollection<int> c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_ValueCantBeNull, "null").WithArguments("int").WithLocation(5, 49));
        }

        [Fact]
        public void ElementConversion_CollectionInitializer()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                class MyCollection<T> : IEnumerable<T>
                {
                    public void Add(T t) { }
                    public IEnumerator<T> GetEnumerator() => default;
                    IEnumerator IEnumerable.GetEnumerator() => default;
                }
                """;
            string sourceB = """
                class Program
                {
                    static void Main()
                    {
                        MyCollection<int> c = [string.Empty, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, sourceB });
            comp.VerifyEmitDiagnostics(
                // 1.cs(5,32): error CS1950: The best overloaded Add method 'MyCollection<int>.Add(int)' for the collection initializer has some invalid arguments
                //         MyCollection<int> c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_BadArgTypesForCollectionAdd, "string.Empty").WithArguments("MyCollection<int>.Add(int)").WithLocation(5, 32),
                // 1.cs(5,32): error CS1503: Argument 1: cannot convert from 'string' to 'int'
                //         MyCollection<int> c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_BadArgType, "string.Empty").WithArguments("1", "string", "int").WithLocation(5, 32),
                // 1.cs(5,49): error CS1950: The best overloaded Add method 'MyCollection<int>.Add(int)' for the collection initializer has some invalid arguments
                //         MyCollection<int> c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_BadArgTypesForCollectionAdd, "null").WithArguments("MyCollection<int>.Add(int)").WithLocation(5, 49),
                // 1.cs(5,49): error CS1503: Argument 1: cannot convert from '<null>' to 'int'
                //         MyCollection<int> c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_BadArgType, "null").WithArguments("1", "<null>", "int").WithLocation(5, 49));
        }

        [InlineData("int[]")]
        [InlineData("System.ReadOnlySpan<int>")]
        [InlineData("System.Collections.Generic.IReadOnlyCollection<int>")]
        [InlineData("System.Collections.Generic.ICollection<int>")]
        [Theory]
        public void ElementConversion_Other(string collectionType)
        {
            string source = $$"""
                class Program
                {
                    static void Main()
                    {
                        {{collectionType}} c;
                        c = [string.Empty, 2, null];
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (6,14): error CS0029: Cannot implicitly convert type 'string' to 'int'
                //         c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "string.Empty").WithArguments("string", "int").WithLocation(6, 14),
                // (6,31): error CS0037: Cannot convert null to 'int' because it is a non-nullable value type
                //         c = [string.Empty, 2, null];
                Diagnostic(ErrorCode.ERR_ValueCantBeNull, "null").WithArguments("int").WithLocation(6, 31));
        }

        [CombinatorialData]
        [Theory]
        public void ListConstruction_01(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework,
            [CombinatorialValues("List<object>", "ICollection<object>", "IList<object>")] string targetType)
        {
            string source = $$"""
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        var x = F(1, 2, 3);
                        x.Report();
                    }
                    static {{targetType}} F<T>(T x, T y, T z) => [x, y, z];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            if (targetFramework == TargetFramework.Net80)
            {
                verifier.VerifyIL("Program.F<T>(T, T, T)", """
                    {
                      // Code size       79 (0x4f)
                      .maxstack  3
                      .locals init (System.Span<object> V_0,
                                    int V_1)
                      IL_0000:  newobj     "System.Collections.Generic.List<object>..ctor()"
                      IL_0005:  dup
                      IL_0006:  ldc.i4.3
                      IL_0007:  call       "void System.Runtime.InteropServices.CollectionsMarshal.SetCount<object>(System.Collections.Generic.List<object>, int)"
                      IL_000c:  dup
                      IL_000d:  call       "System.Span<object> System.Runtime.InteropServices.CollectionsMarshal.AsSpan<object>(System.Collections.Generic.List<object>)"
                      IL_0012:  stloc.0
                      IL_0013:  ldc.i4.0
                      IL_0014:  stloc.1
                      IL_0015:  ldloca.s   V_0
                      IL_0017:  ldloc.1
                      IL_0018:  call       "ref object System.Span<object>.this[int].get"
                      IL_001d:  ldarg.0
                      IL_001e:  box        "T"
                      IL_0023:  stind.ref
                      IL_0024:  ldloc.1
                      IL_0025:  ldc.i4.1
                      IL_0026:  add
                      IL_0027:  stloc.1
                      IL_0028:  ldloca.s   V_0
                      IL_002a:  ldloc.1
                      IL_002b:  call       "ref object System.Span<object>.this[int].get"
                      IL_0030:  ldarg.1
                      IL_0031:  box        "T"
                      IL_0036:  stind.ref
                      IL_0037:  ldloc.1
                      IL_0038:  ldc.i4.1
                      IL_0039:  add
                      IL_003a:  stloc.1
                      IL_003b:  ldloca.s   V_0
                      IL_003d:  ldloc.1
                      IL_003e:  call       "ref object System.Span<object>.this[int].get"
                      IL_0043:  ldarg.2
                      IL_0044:  box        "T"
                      IL_0049:  stind.ref
                      IL_004a:  ldloc.1
                      IL_004b:  ldc.i4.1
                      IL_004c:  add
                      IL_004d:  stloc.1
                      IL_004e:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.F<T>(T, T, T)", """
                    {
                      // Code size       43 (0x2b)
                      .maxstack  3
                      IL_0000:  ldc.i4.3
                      IL_0001:  newobj     "System.Collections.Generic.List<object>..ctor(int)"
                      IL_0006:  dup
                      IL_0007:  ldarg.0
                      IL_0008:  box        "T"
                      IL_000d:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                      IL_0012:  dup
                      IL_0013:  ldarg.1
                      IL_0014:  box        "T"
                      IL_0019:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                      IL_001e:  dup
                      IL_001f:  ldarg.2
                      IL_0020:  box        "T"
                      IL_0025:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                      IL_002a:  ret
                    }
                    """);
            }
        }

        [CombinatorialData]
        [Theory]
        public void ListConstruction_02(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        var x = F<string>([]);
                        x.Report();
                        var y = F([1, 2, 3]);
                        y.Report();
                    }
                    static List<object> F<T>(T[] items) => [..items];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
            if (targetFramework == TargetFramework.Net80)
            {
                verifier.VerifyIL("Program.F<T>(T[])", """
                    {
                      // Code size       81 (0x51)
                      .maxstack  2
                      .locals init (T[] V_0,
                                    System.Collections.Generic.List<object> V_1,
                                    System.Span<object> V_2,
                                    int V_3,
                                    T[] V_4,
                                    int V_5,
                                    T V_6)
                      IL_0000:  ldarg.0
                      IL_0001:  stloc.0
                      IL_0002:  newobj     "System.Collections.Generic.List<object>..ctor()"
                      IL_0007:  stloc.1
                      IL_0008:  ldloc.1
                      IL_0009:  ldloc.0
                      IL_000a:  ldlen
                      IL_000b:  conv.i4
                      IL_000c:  call       "void System.Runtime.InteropServices.CollectionsMarshal.SetCount<object>(System.Collections.Generic.List<object>, int)"
                      IL_0011:  ldloc.1
                      IL_0012:  call       "System.Span<object> System.Runtime.InteropServices.CollectionsMarshal.AsSpan<object>(System.Collections.Generic.List<object>)"
                      IL_0017:  stloc.2
                      IL_0018:  ldc.i4.0
                      IL_0019:  stloc.3
                      IL_001a:  ldloc.0
                      IL_001b:  stloc.s    V_4
                      IL_001d:  ldc.i4.0
                      IL_001e:  stloc.s    V_5
                      IL_0020:  br.s       IL_0047
                      IL_0022:  ldloc.s    V_4
                      IL_0024:  ldloc.s    V_5
                      IL_0026:  ldelem     "T"
                      IL_002b:  stloc.s    V_6
                      IL_002d:  ldloca.s   V_2
                      IL_002f:  ldloc.3
                      IL_0030:  call       "ref object System.Span<object>.this[int].get"
                      IL_0035:  ldloc.s    V_6
                      IL_0037:  box        "T"
                      IL_003c:  stind.ref
                      IL_003d:  ldloc.3
                      IL_003e:  ldc.i4.1
                      IL_003f:  add
                      IL_0040:  stloc.3
                      IL_0041:  ldloc.s    V_5
                      IL_0043:  ldc.i4.1
                      IL_0044:  add
                      IL_0045:  stloc.s    V_5
                      IL_0047:  ldloc.s    V_5
                      IL_0049:  ldloc.s    V_4
                      IL_004b:  ldlen
                      IL_004c:  conv.i4
                      IL_004d:  blt.s      IL_0022
                      IL_004f:  ldloc.1
                      IL_0050:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.F<T>(T[])", """
                    {
                      // Code size       47 (0x2f)
                      .maxstack  2
                      .locals init (System.Collections.Generic.List<object> V_0,
                                    T[] V_1,
                                    int V_2,
                                    T V_3)
                      IL_0000:  ldarg.0
                      IL_0001:  dup
                      IL_0002:  ldlen
                      IL_0003:  conv.i4
                      IL_0004:  newobj     "System.Collections.Generic.List<object>..ctor(int)"
                      IL_0009:  stloc.0
                      IL_000a:  stloc.1
                      IL_000b:  ldc.i4.0
                      IL_000c:  stloc.2
                      IL_000d:  br.s       IL_0027
                      IL_000f:  ldloc.1
                      IL_0010:  ldloc.2
                      IL_0011:  ldelem     "T"
                      IL_0016:  stloc.3
                      IL_0017:  ldloc.0
                      IL_0018:  ldloc.3
                      IL_0019:  box        "T"
                      IL_001e:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                      IL_0023:  ldloc.2
                      IL_0024:  ldc.i4.1
                      IL_0025:  add
                      IL_0026:  stloc.2
                      IL_0027:  ldloc.2
                      IL_0028:  ldloc.1
                      IL_0029:  ldlen
                      IL_002a:  conv.i4
                      IL_002b:  blt.s      IL_000f
                      IL_002d:  ldloc.0
                      IL_002e:  ret
                    }
                    """);
            }
        }

        [Fact]
        public void ListConstruction_03()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        var x = F([1, 2, 3]);
                        x.Report();
                    }
                    static List<object> F<T>(IEnumerable<T> items) => [..items, 4];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3, 4], "));
            verifier.VerifyIL("Program.F<T>", """
                {
                  // Code size       68 (0x44)
                  .maxstack  2
                  .locals init (System.Collections.Generic.List<object> V_0,
                                System.Collections.Generic.IEnumerator<T> V_1,
                                T V_2)
                  IL_0000:  newobj     "System.Collections.Generic.List<object>..ctor()"
                  IL_0005:  stloc.0
                  IL_0006:  ldarg.0
                  IL_0007:  callvirt   "System.Collections.Generic.IEnumerator<T> System.Collections.Generic.IEnumerable<T>.GetEnumerator()"
                  IL_000c:  stloc.1
                  .try
                  {
                    IL_000d:  br.s       IL_0022
                    IL_000f:  ldloc.1
                    IL_0010:  callvirt   "T System.Collections.Generic.IEnumerator<T>.Current.get"
                    IL_0015:  stloc.2
                    IL_0016:  ldloc.0
                    IL_0017:  ldloc.2
                    IL_0018:  box        "T"
                    IL_001d:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                    IL_0022:  ldloc.1
                    IL_0023:  callvirt   "bool System.Collections.IEnumerator.MoveNext()"
                    IL_0028:  brtrue.s   IL_000f
                    IL_002a:  leave.s    IL_0036
                  }
                  finally
                  {
                    IL_002c:  ldloc.1
                    IL_002d:  brfalse.s  IL_0035
                    IL_002f:  ldloc.1
                    IL_0030:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0035:  endfinally
                  }
                  IL_0036:  ldloc.0
                  IL_0037:  ldc.i4.4
                  IL_0038:  box        "int"
                  IL_003d:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_0042:  ldloc.0
                  IL_0043:  ret
                }
                """);
        }

        // Use List<T>..ctor(int capacity) if CollectionsMarshal members are missing.
        [InlineData(new int[0])]
        [InlineData(new[] { (int)WellKnownMember.System_Runtime_InteropServices_CollectionsMarshal__AsSpan_T })]
        [InlineData(new[] { (int)WellKnownMember.System_Runtime_InteropServices_CollectionsMarshal__SetCount_T })]
        [InlineData(new[] { (int)WellKnownMember.System_Runtime_InteropServices_CollectionsMarshal__AsSpan_T, (int)WellKnownMember.System_Runtime_InteropServices_CollectionsMarshal__SetCount_T })]
        [Theory]
        public void ListConstruction_MissingMembers_CollectionsMarshal(int[] missingMembers)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        var x = F([1, 2, 3]);
                        x.Report();
                    }
                    static List<T> F<T>(T[] items) => [..items];
                }
                """;

            var comp = CreateCompilation(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                options: TestOptions.ReleaseExe);

            foreach (int missingMember in missingMembers)
            {
                comp.MakeMemberMissing((WellKnownMember)missingMember);
            }

            var verifier = CompileAndVerify(
                comp,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));

            if (missingMembers.Length == 0)
            {
                verifier.VerifyIL("Program.F<T>(T[])", """
                    {
                      // Code size       80 (0x50)
                      .maxstack  2
                      .locals init (T[] V_0,
                                    System.Collections.Generic.List<T> V_1,
                                    System.Span<T> V_2,
                                    int V_3,
                                    T[] V_4,
                                    int V_5,
                                    T V_6)
                      IL_0000:  ldarg.0
                      IL_0001:  stloc.0
                      IL_0002:  newobj     "System.Collections.Generic.List<T>..ctor()"
                      IL_0007:  stloc.1
                      IL_0008:  ldloc.1
                      IL_0009:  ldloc.0
                      IL_000a:  ldlen
                      IL_000b:  conv.i4
                      IL_000c:  call       "void System.Runtime.InteropServices.CollectionsMarshal.SetCount<T>(System.Collections.Generic.List<T>, int)"
                      IL_0011:  ldloc.1
                      IL_0012:  call       "System.Span<T> System.Runtime.InteropServices.CollectionsMarshal.AsSpan<T>(System.Collections.Generic.List<T>)"
                      IL_0017:  stloc.2
                      IL_0018:  ldc.i4.0
                      IL_0019:  stloc.3
                      IL_001a:  ldloc.0
                      IL_001b:  stloc.s    V_4
                      IL_001d:  ldc.i4.0
                      IL_001e:  stloc.s    V_5
                      IL_0020:  br.s       IL_0046
                      IL_0022:  ldloc.s    V_4
                      IL_0024:  ldloc.s    V_5
                      IL_0026:  ldelem     "T"
                      IL_002b:  stloc.s    V_6
                      IL_002d:  ldloca.s   V_2
                      IL_002f:  ldloc.3
                      IL_0030:  call       "ref T System.Span<T>.this[int].get"
                      IL_0035:  ldloc.s    V_6
                      IL_0037:  stobj      "T"
                      IL_003c:  ldloc.3
                      IL_003d:  ldc.i4.1
                      IL_003e:  add
                      IL_003f:  stloc.3
                      IL_0040:  ldloc.s    V_5
                      IL_0042:  ldc.i4.1
                      IL_0043:  add
                      IL_0044:  stloc.s    V_5
                      IL_0046:  ldloc.s    V_5
                      IL_0048:  ldloc.s    V_4
                      IL_004a:  ldlen
                      IL_004b:  conv.i4
                      IL_004c:  blt.s      IL_0022
                      IL_004e:  ldloc.1
                      IL_004f:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.F<T>(T[])", """
                    {
                      // Code size       42 (0x2a)
                      .maxstack  2
                      .locals init (System.Collections.Generic.List<T> V_0,
                                    T[] V_1,
                                    int V_2,
                                    T V_3)
                      IL_0000:  ldarg.0
                      IL_0001:  dup
                      IL_0002:  ldlen
                      IL_0003:  conv.i4
                      IL_0004:  newobj     "System.Collections.Generic.List<T>..ctor(int)"
                      IL_0009:  stloc.0
                      IL_000a:  stloc.1
                      IL_000b:  ldc.i4.0
                      IL_000c:  stloc.2
                      IL_000d:  br.s       IL_0022
                      IL_000f:  ldloc.1
                      IL_0010:  ldloc.2
                      IL_0011:  ldelem     "T"
                      IL_0016:  stloc.3
                      IL_0017:  ldloc.0
                      IL_0018:  ldloc.3
                      IL_0019:  callvirt   "void System.Collections.Generic.List<T>.Add(T)"
                      IL_001e:  ldloc.2
                      IL_001f:  ldc.i4.1
                      IL_0020:  add
                      IL_0021:  stloc.2
                      IL_0022:  ldloc.2
                      IL_0023:  ldloc.1
                      IL_0024:  ldlen
                      IL_0025:  conv.i4
                      IL_0026:  blt.s      IL_000f
                      IL_0028:  ldloc.0
                      IL_0029:  ret
                    }
                    """);
            }
        }

        // List<T> optimizations are not applied to derived types.
        [CombinatorialData]
        [Theory]
        public void ListConstruction_DerivedType_01(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System.Collections.Generic;
                class MyList<T> : List<T>
                {
                }
                class Program
                {
                    static void Main()
                    {
                        var x = F(1, 2, 3);
                        x.Report();
                    }
                    static MyList<object> F<T>(T x, T y, T z) => [x, y, z];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.F<T>(T, T, T)", """
                {
                  // Code size       42 (0x2a)
                  .maxstack  3
                  IL_0000:  newobj     "MyList<object>..ctor()"
                  IL_0005:  dup
                  IL_0006:  ldarg.0
                  IL_0007:  box        "T"
                  IL_000c:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_0011:  dup
                  IL_0012:  ldarg.1
                  IL_0013:  box        "T"
                  IL_0018:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_001d:  dup
                  IL_001e:  ldarg.2
                  IL_001f:  box        "T"
                  IL_0024:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                  IL_0029:  ret
                }
                """);
        }

        // List<T> optimizations are not applied to derived types.
        [CombinatorialData]
        [Theory]
        public void ListConstruction_DerivedType_02(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        var x = F<int, List<int>>(1, 2, 3);
                        x.Report();
                    }
                    static U F<T, U>(T x, T y, T z) where U : List<T>, new() => [x, y, z];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.F<T, U>(T, T, T)", """
                {
                  // Code size       42 (0x2a)
                  .maxstack  3
                  IL_0000:  call       "U System.Activator.CreateInstance<U>()"
                  IL_0005:  dup
                  IL_0006:  box        "U"
                  IL_000b:  ldarg.0
                  IL_000c:  callvirt   "void System.Collections.Generic.List<T>.Add(T)"
                  IL_0011:  dup
                  IL_0012:  box        "U"
                  IL_0017:  ldarg.1
                  IL_0018:  callvirt   "void System.Collections.Generic.List<T>.Add(T)"
                  IL_001d:  dup
                  IL_001e:  box        "U"
                  IL_0023:  ldarg.2
                  IL_0024:  callvirt   "void System.Collections.Generic.List<T>.Add(T)"
                  IL_0029:  ret
                }
                """);
        }

        [Fact]
        public void ListConstruction_DerivedType_03()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        var x = F<int, List<int>>(1, 2, 3);
                    }
                    static U F<T, U>(T x, T y, T z) where U : IList<T>, new() => [x, y, z];
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();
        }

        // List<T> optimizations are skipped in async methods since the optimizations use Span<T>.
        [CombinatorialData]
        [Theory]
        public void ListConstruction_Async(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = $$"""
                using System.Collections.Generic;
                using System.Threading.Tasks;
                class Program
                {
                    static async Task Main()
                    {
                        var x = await F(1, 2, 3);
                        x.Report();
                    }
                    static async Task<T> Yield<T>(T t)
                    {
                        Task.Yield();
                        return t;
                    }
                    static async Task<List<T>> F<T>(T x, T y, T z)
                    {
                        return [x, await Yield(y), z];
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.<F>d__2<T>.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", """
                {
                  // Code size      229 (0xe5)
                  .maxstack  3
                  .locals init (int V_0,
                                System.Collections.Generic.List<T> V_1,
                                T V_2,
                                System.Runtime.CompilerServices.TaskAwaiter<T> V_3,
                                System.Exception V_4)
                  IL_0000:  ldarg.0
                  IL_0001:  ldfld      "int Program.<F>d__2<T>.<>1__state"
                  IL_0006:  stloc.0
                  .try
                  {
                    IL_0007:  ldloc.0
                    IL_0008:  brfalse.s  IL_006d
                    IL_000a:  ldarg.0
                    IL_000b:  ldc.i4.3
                    IL_000c:  newobj     "System.Collections.Generic.List<T>..ctor(int)"
                    IL_0011:  stfld      "System.Collections.Generic.List<T> Program.<F>d__2<T>.<>7__wrap2"
                    IL_0016:  ldarg.0
                    IL_0017:  ldfld      "System.Collections.Generic.List<T> Program.<F>d__2<T>.<>7__wrap2"
                    IL_001c:  ldarg.0
                    IL_001d:  ldfld      "T Program.<F>d__2<T>.x"
                    IL_0022:  callvirt   "void System.Collections.Generic.List<T>.Add(T)"
                    IL_0027:  ldarg.0
                    IL_0028:  ldarg.0
                    IL_0029:  ldfld      "System.Collections.Generic.List<T> Program.<F>d__2<T>.<>7__wrap2"
                    IL_002e:  stfld      "System.Collections.Generic.List<T> Program.<F>d__2<T>.<>7__wrap1"
                    IL_0033:  ldarg.0
                    IL_0034:  ldfld      "T Program.<F>d__2<T>.y"
                    IL_0039:  call       "System.Threading.Tasks.Task<T> Program.Yield<T>(T)"
                    IL_003e:  callvirt   "System.Runtime.CompilerServices.TaskAwaiter<T> System.Threading.Tasks.Task<T>.GetAwaiter()"
                    IL_0043:  stloc.3
                    IL_0044:  ldloca.s   V_3
                    IL_0046:  call       "bool System.Runtime.CompilerServices.TaskAwaiter<T>.IsCompleted.get"
                    IL_004b:  brtrue.s   IL_0089
                    IL_004d:  ldarg.0
                    IL_004e:  ldc.i4.0
                    IL_004f:  dup
                    IL_0050:  stloc.0
                    IL_0051:  stfld      "int Program.<F>d__2<T>.<>1__state"
                    IL_0056:  ldarg.0
                    IL_0057:  ldloc.3
                    IL_0058:  stfld      "System.Runtime.CompilerServices.TaskAwaiter<T> Program.<F>d__2<T>.<>u__1"
                    IL_005d:  ldarg.0
                    IL_005e:  ldflda     "System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Collections.Generic.List<T>> Program.<F>d__2<T>.<>t__builder"
                    IL_0063:  ldloca.s   V_3
                    IL_0065:  ldarg.0
                    IL_0066:  call       "void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Collections.Generic.List<T>>.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<T>, Program.<F>d__2<T>>(ref System.Runtime.CompilerServices.TaskAwaiter<T>, ref Program.<F>d__2<T>)"
                    IL_006b:  leave.s    IL_00e4
                    IL_006d:  ldarg.0
                    IL_006e:  ldfld      "System.Runtime.CompilerServices.TaskAwaiter<T> Program.<F>d__2<T>.<>u__1"
                    IL_0073:  stloc.3
                    IL_0074:  ldarg.0
                    IL_0075:  ldflda     "System.Runtime.CompilerServices.TaskAwaiter<T> Program.<F>d__2<T>.<>u__1"
                    IL_007a:  initobj    "System.Runtime.CompilerServices.TaskAwaiter<T>"
                    IL_0080:  ldarg.0
                    IL_0081:  ldc.i4.m1
                    IL_0082:  dup
                    IL_0083:  stloc.0
                    IL_0084:  stfld      "int Program.<F>d__2<T>.<>1__state"
                    IL_0089:  ldloca.s   V_3
                    IL_008b:  call       "T System.Runtime.CompilerServices.TaskAwaiter<T>.GetResult()"
                    IL_0090:  stloc.2
                    IL_0091:  ldarg.0
                    IL_0092:  ldfld      "System.Collections.Generic.List<T> Program.<F>d__2<T>.<>7__wrap1"
                    IL_0097:  ldloc.2
                    IL_0098:  callvirt   "void System.Collections.Generic.List<T>.Add(T)"
                    IL_009d:  ldarg.0
                    IL_009e:  ldfld      "System.Collections.Generic.List<T> Program.<F>d__2<T>.<>7__wrap2"
                    IL_00a3:  ldarg.0
                    IL_00a4:  ldfld      "T Program.<F>d__2<T>.z"
                    IL_00a9:  callvirt   "void System.Collections.Generic.List<T>.Add(T)"
                    IL_00ae:  ldarg.0
                    IL_00af:  ldfld      "System.Collections.Generic.List<T> Program.<F>d__2<T>.<>7__wrap2"
                    IL_00b4:  stloc.1
                    IL_00b5:  leave.s    IL_00d0
                  }
                  catch System.Exception
                  {
                    IL_00b7:  stloc.s    V_4
                    IL_00b9:  ldarg.0
                    IL_00ba:  ldc.i4.s   -2
                    IL_00bc:  stfld      "int Program.<F>d__2<T>.<>1__state"
                    IL_00c1:  ldarg.0
                    IL_00c2:  ldflda     "System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Collections.Generic.List<T>> Program.<F>d__2<T>.<>t__builder"
                    IL_00c7:  ldloc.s    V_4
                    IL_00c9:  call       "void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Collections.Generic.List<T>>.SetException(System.Exception)"
                    IL_00ce:  leave.s    IL_00e4
                  }
                  IL_00d0:  ldarg.0
                  IL_00d1:  ldc.i4.s   -2
                  IL_00d3:  stfld      "int Program.<F>d__2<T>.<>1__state"
                  IL_00d8:  ldarg.0
                  IL_00d9:  ldflda     "System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Collections.Generic.List<T>> Program.<F>d__2<T>.<>t__builder"
                  IL_00de:  ldloc.1
                  IL_00df:  call       "void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<System.Collections.Generic.List<T>>.SetResult(System.Collections.Generic.List<T>)"
                  IL_00e4:  ret
                }
                """);
        }

        [Fact]
        public void ListConstruction_Dynamic_01()
        {
            string source = $$"""
                using System.Collections.Generic;
                class Program
                {
                    static List<object> F1(List<dynamic> e) => [..e];
                    static List<int> F2(List<dynamic> e) => [..e];
                    static void Main()
                    {
                        F1([1, 2, 3]).Report();
                        F2([4, 5]).Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                options: TestOptions.ReleaseExe,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], [4, 5], "));
            verifier.VerifyIL("Program.F1",
                """
                {
                  // Code size       90 (0x5a)
                  .maxstack  2
                  .locals init (System.Collections.Generic.List<dynamic> V_0,
                                System.Collections.Generic.List<object> V_1,
                                System.Span<object> V_2,
                                int V_3,
                                System.Collections.Generic.List<dynamic>.Enumerator V_4,
                                object V_5)
                  IL_0000:  ldarg.0
                  IL_0001:  stloc.0
                  IL_0002:  newobj     "System.Collections.Generic.List<object>..ctor()"
                  IL_0007:  stloc.1
                  IL_0008:  ldloc.1
                  IL_0009:  ldloc.0
                  IL_000a:  callvirt   "int System.Collections.Generic.List<dynamic>.Count.get"
                  IL_000f:  call       "void System.Runtime.InteropServices.CollectionsMarshal.SetCount<object>(System.Collections.Generic.List<object>, int)"
                  IL_0014:  ldloc.1
                  IL_0015:  call       "System.Span<object> System.Runtime.InteropServices.CollectionsMarshal.AsSpan<object>(System.Collections.Generic.List<object>)"
                  IL_001a:  stloc.2
                  IL_001b:  ldc.i4.0
                  IL_001c:  stloc.3
                  IL_001d:  ldloc.0
                  IL_001e:  callvirt   "System.Collections.Generic.List<dynamic>.Enumerator System.Collections.Generic.List<dynamic>.GetEnumerator()"
                  IL_0023:  stloc.s    V_4
                  .try
                  {
                    IL_0025:  br.s       IL_003f
                    IL_0027:  ldloca.s   V_4
                    IL_0029:  call       "dynamic System.Collections.Generic.List<dynamic>.Enumerator.Current.get"
                    IL_002e:  stloc.s    V_5
                    IL_0030:  ldloca.s   V_2
                    IL_0032:  ldloc.3
                    IL_0033:  call       "ref object System.Span<object>.this[int].get"
                    IL_0038:  ldloc.s    V_5
                    IL_003a:  stind.ref
                    IL_003b:  ldloc.3
                    IL_003c:  ldc.i4.1
                    IL_003d:  add
                    IL_003e:  stloc.3
                    IL_003f:  ldloca.s   V_4
                    IL_0041:  call       "bool System.Collections.Generic.List<dynamic>.Enumerator.MoveNext()"
                    IL_0046:  brtrue.s   IL_0027
                    IL_0048:  leave.s    IL_0058
                  }
                  finally
                  {
                    IL_004a:  ldloca.s   V_4
                    IL_004c:  constrained. "System.Collections.Generic.List<dynamic>.Enumerator"
                    IL_0052:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0057:  endfinally
                  }
                  IL_0058:  ldloc.1
                  IL_0059:  ret
                }
                """);
            verifier.VerifyIL("Program.F2",
                """
                {
                  // Code size      153 (0x99)
                  .maxstack  4
                  .locals init (System.Collections.Generic.List<dynamic> V_0,
                                System.Collections.Generic.List<int> V_1,
                                System.Span<int> V_2,
                                int V_3,
                                System.Collections.Generic.List<dynamic>.Enumerator V_4,
                                object V_5)
                  IL_0000:  ldarg.0
                  IL_0001:  stloc.0
                  IL_0002:  newobj     "System.Collections.Generic.List<int>..ctor()"
                  IL_0007:  stloc.1
                  IL_0008:  ldloc.1
                  IL_0009:  ldloc.0
                  IL_000a:  callvirt   "int System.Collections.Generic.List<dynamic>.Count.get"
                  IL_000f:  call       "void System.Runtime.InteropServices.CollectionsMarshal.SetCount<int>(System.Collections.Generic.List<int>, int)"
                  IL_0014:  ldloc.1
                  IL_0015:  call       "System.Span<int> System.Runtime.InteropServices.CollectionsMarshal.AsSpan<int>(System.Collections.Generic.List<int>)"
                  IL_001a:  stloc.2
                  IL_001b:  ldc.i4.0
                  IL_001c:  stloc.3
                  IL_001d:  ldloc.0
                  IL_001e:  callvirt   "System.Collections.Generic.List<dynamic>.Enumerator System.Collections.Generic.List<dynamic>.GetEnumerator()"
                  IL_0023:  stloc.s    V_4
                  .try
                  {
                    IL_0025:  br.s       IL_007e
                    IL_0027:  ldloca.s   V_4
                    IL_0029:  call       "dynamic System.Collections.Generic.List<dynamic>.Enumerator.Current.get"
                    IL_002e:  stloc.s    V_5
                    IL_0030:  ldloca.s   V_2
                    IL_0032:  ldloc.3
                    IL_0033:  call       "ref int System.Span<int>.this[int].get"
                    IL_0038:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__1.<>p__0"
                    IL_003d:  brtrue.s   IL_0063
                    IL_003f:  ldc.i4.0
                    IL_0040:  ldtoken    "int"
                    IL_0045:  call       "System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)"
                    IL_004a:  ldtoken    "Program"
                    IL_004f:  call       "System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)"
                    IL_0054:  call       "System.Runtime.CompilerServices.CallSiteBinder Microsoft.CSharp.RuntimeBinder.Binder.Convert(Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags, System.Type, System.Type)"
                    IL_0059:  call       "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>>.Create(System.Runtime.CompilerServices.CallSiteBinder)"
                    IL_005e:  stsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__1.<>p__0"
                    IL_0063:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__1.<>p__0"
                    IL_0068:  ldfld      "System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>>.Target"
                    IL_006d:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>> Program.<>o__1.<>p__0"
                    IL_0072:  ldloc.s    V_5
                    IL_0074:  callvirt   "int System.Func<System.Runtime.CompilerServices.CallSite, dynamic, int>.Invoke(System.Runtime.CompilerServices.CallSite, dynamic)"
                    IL_0079:  stind.i4
                    IL_007a:  ldloc.3
                    IL_007b:  ldc.i4.1
                    IL_007c:  add
                    IL_007d:  stloc.3
                    IL_007e:  ldloca.s   V_4
                    IL_0080:  call       "bool System.Collections.Generic.List<dynamic>.Enumerator.MoveNext()"
                    IL_0085:  brtrue.s   IL_0027
                    IL_0087:  leave.s    IL_0097
                  }
                  finally
                  {
                    IL_0089:  ldloca.s   V_4
                    IL_008b:  constrained. "System.Collections.Generic.List<dynamic>.Enumerator"
                    IL_0091:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0096:  endfinally
                  }
                  IL_0097:  ldloc.1
                  IL_0098:  ret
                }
                """);
        }

        [Fact]
        public void ListConstruction_Dynamic_02()
        {
            string source = $$"""
                using System.Collections.Generic;
                class Program
                {
                    static List<object> F1(dynamic e) => [..e];
                    static void Main()
                    {
                        F1((List<int>)[1, 2, 3]).Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                options: TestOptions.ReleaseExe,
                verify: Verification.FailsPEVerify,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.F1",
                """
                {
                  // Code size      121 (0x79)
                  .maxstack  3
                  .locals init (System.Collections.Generic.List<object> V_0,
                                System.Collections.IEnumerator V_1,
                                object V_2,
                                System.IDisposable V_3)
                  IL_0000:  newobj     "System.Collections.Generic.List<object>..ctor()"
                  IL_0005:  stloc.0
                  IL_0006:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>> Program.<>o__0.<>p__0"
                  IL_000b:  brtrue.s   IL_0031
                  IL_000d:  ldc.i4.0
                  IL_000e:  ldtoken    "System.Collections.IEnumerable"
                  IL_0013:  call       "System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)"
                  IL_0018:  ldtoken    "Program"
                  IL_001d:  call       "System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)"
                  IL_0022:  call       "System.Runtime.CompilerServices.CallSiteBinder Microsoft.CSharp.RuntimeBinder.Binder.Convert(Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags, System.Type, System.Type)"
                  IL_0027:  call       "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>>.Create(System.Runtime.CompilerServices.CallSiteBinder)"
                  IL_002c:  stsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>> Program.<>o__0.<>p__0"
                  IL_0031:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>> Program.<>o__0.<>p__0"
                  IL_0036:  ldfld      "System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>>.Target"
                  IL_003b:  ldsfld     "System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>> Program.<>o__0.<>p__0"
                  IL_0040:  ldarg.0
                  IL_0041:  callvirt   "System.Collections.IEnumerable System.Func<System.Runtime.CompilerServices.CallSite, dynamic, System.Collections.IEnumerable>.Invoke(System.Runtime.CompilerServices.CallSite, dynamic)"
                  IL_0046:  callvirt   "System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()"
                  IL_004b:  stloc.1
                  .try
                  {
                    IL_004c:  br.s       IL_005c
                    IL_004e:  ldloc.1
                    IL_004f:  callvirt   "object System.Collections.IEnumerator.Current.get"
                    IL_0054:  stloc.2
                    IL_0055:  ldloc.0
                    IL_0056:  ldloc.2
                    IL_0057:  callvirt   "void System.Collections.Generic.List<object>.Add(object)"
                    IL_005c:  ldloc.1
                    IL_005d:  callvirt   "bool System.Collections.IEnumerator.MoveNext()"
                    IL_0062:  brtrue.s   IL_004e
                    IL_0064:  leave.s    IL_0077
                  }
                  finally
                  {
                    IL_0066:  ldloc.1
                    IL_0067:  isinst     "System.IDisposable"
                    IL_006c:  stloc.3
                    IL_006d:  ldloc.3
                    IL_006e:  brfalse.s  IL_0076
                    IL_0070:  ldloc.3
                    IL_0071:  callvirt   "void System.IDisposable.Dispose()"
                    IL_0076:  endfinally
                  }
                  IL_0077:  ldloc.0
                  IL_0078:  ret
                }
                """);
        }

        [Fact]
        public void ListConstruction_Dynamic_03()
        {
            string source = $$"""
                using System.Collections.Generic;
                class Program
                {
                    static List<int> F2(dynamic e) => [..e];
                    static void Main()
                    {
                        F2((int[])[4, 5]).Report();
                    }
                }
                """;
            // https://github.com/dotnet/roslyn/issues/69704: Should compile and run with expectedOutput: "[4, 5], "
            var comp = CreateCompilation(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(4,42): error CS0029: Cannot implicitly convert type 'object' to 'int'
                //     static List<int> F2(dynamic e) => [..e];
                Diagnostic(ErrorCode.ERR_NoImplicitConv, "e").WithArguments("object", "int").WithLocation(4, 42));
        }

        [ConditionalFact(typeof(DesktopOnly))]
        public void RestrictedTypes()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        var x = [default(TypedReference)];
                        var y = [default(ArgIterator)];
                        var z = [default(RuntimeArgumentHandle)];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (6,17): error CS9176: There is no target type for the collection expression.
                //         var x = [default(TypedReference)];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[default(TypedReference)]").WithLocation(6, 17),
                // (7,17): error CS9176: There is no target type for the collection expression.
                //         var y = [default(ArgIterator)];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[default(ArgIterator)]").WithLocation(7, 17),
                // (8,17): error CS9176: There is no target type for the collection expression.
                //         var z = [default(RuntimeArgumentHandle)];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[default(RuntimeArgumentHandle)]").WithLocation(8, 17));
        }

        [Fact]
        public void RefStruct_01()
        {
            string source = """
                ref struct R
                {
                    public R(ref int i) { }
                }
                class Program
                {
                    static void Main()
                    {
                        int i = 0;
                        var x = [default(R)];
                        var y = [new R(ref i)];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (10,17): error CS9176: There is no target type for the collection expression.
                //         var x = [default(R)];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[default(R)]").WithLocation(10, 17),
                // (11,17): error CS9176: There is no target type for the collection expression.
                //         var y = [new R(ref i)];
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[new R(ref i)]").WithLocation(11, 17));
        }

        [Fact]
        public void RefStruct_02()
        {
            string source = """
                using System.Collections.Generic;
                ref struct R
                {
                    public int _i;
                    public R(ref int i) { _i = i; }
                    public static implicit operator int(R r) => r._i;
                }
                class Program
                {
                    static void Main()
                    {
                        int i = 1;
                        int[] a = [default(R), new R(ref i)];
                        a.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[0, 1], ");
        }

        [Fact]
        public void RefStruct_03()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                class C : IEnumerable
                {
                    private List<int> _list = new List<int>();
                    public void Add(R r) { _list.Add(r._i); }
                    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
                }
                ref struct R
                {
                    public int _i;
                    public R(ref int i) { _i = i; }
                }
                class Program
                {
                    static void Main()
                    {
                        int i = 1;
                        C c = [default(R), new R(ref i)];
                        c.Report();
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[0, 1], ");
        }

        [CombinatorialData]
        [Theory]
        public void RefSafety_Return_01([CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F1<int>().Report();
                        F2<string>().Report();
                    }
                    static Span<T> F1<T>() => [];
                    static ReadOnlySpan<T> F2<T>() => [];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: targetFramework,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[], [], "));
            verifier.VerifyIL("Program.F1<T>", """
                {
                  // Code size       11 (0xb)
                  .maxstack  1
                  IL_0000:  call       "T[] System.Array.Empty<T>()"
                  IL_0005:  newobj     "System.Span<T>..ctor(T[])"
                  IL_000a:  ret
                }
                """);
            verifier.VerifyIL("Program.F2<T>", """
                {
                  // Code size       11 (0xb)
                  .maxstack  1
                  IL_0000:  call       "T[] System.Array.Empty<T>()"
                  IL_0005:  newobj     "System.ReadOnlySpan<T>..ctor(T[])"
                  IL_000a:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void RefSafety_Return_02([CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                using System.Collections.Generic;
                class Program
                {
                    static Span<T> F1<T>(T x, T y) => [x, y];
                    static ReadOnlySpan<T> F2<T>(T x, T y) => [x, y];
                    static ReadOnlySpan<T> F3<T>(IEnumerable<T> e) => [..e];
                }
                """;
            var comp = CreateCompilation(source, targetFramework: targetFramework);
            comp.VerifyEmitDiagnostics(
                // (5,39): error CS9203: A collection expression of type 'Span<T>' cannot be used in this context because it may be exposed outside of the current scope.
                //     static Span<T> F1<T>(T x, T y) => [x, y];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[x, y]").WithArguments("System.Span<T>").WithLocation(5, 39),
                // (6,47): error CS9203: A collection expression of type 'ReadOnlySpan<T>' cannot be used in this context because it may be exposed outside of the current scope.
                //     static ReadOnlySpan<T> F2<T>(T x, T y) => [x, y];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[x, y]").WithArguments("System.ReadOnlySpan<T>").WithLocation(6, 47),
                // (7,55): error CS9203: A collection expression of type 'ReadOnlySpan<T>' cannot be used in this context because it may be exposed outside of the current scope.
                //     static ReadOnlySpan<T> F3<T>(IEnumerable<T> e) => [..e];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[..e]").WithArguments("System.ReadOnlySpan<T>").WithLocation(7, 55));
        }

        [CombinatorialData]
        [Theory]
        public void RefSafety_Return_03([CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F1<int>(1, 2).Report();
                        F2<string>("3", null).Report();
                    }
                    static Span<T> F1<T>(T x, T y) => (T[])[x, y];
                    static ReadOnlySpan<T> F2<T>(T x, T y) => (T[])[x, y];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: targetFramework,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2], [3, null], "));
            verifier.VerifyIL("Program.F1<T>", """
                {
                    // Code size       28 (0x1c)
                    .maxstack  4
                    IL_0000:  ldc.i4.2
                    IL_0001:  newarr     "T"
                    IL_0006:  dup
                    IL_0007:  ldc.i4.0
                    IL_0008:  ldarg.0
                    IL_0009:  stelem     "T"
                    IL_000e:  dup
                    IL_000f:  ldc.i4.1
                    IL_0010:  ldarg.1
                    IL_0011:  stelem     "T"
                    IL_0016:  call       "System.Span<T> System.Span<T>.op_Implicit(T[])"
                    IL_001b:  ret
                }
                """);
            verifier.VerifyIL("Program.F2<T>", """
                {
                  // Code size       28 (0x1c)
                  .maxstack  4
                  IL_0000:  ldc.i4.2
                  IL_0001:  newarr     "T"
                  IL_0006:  dup
                  IL_0007:  ldc.i4.0
                  IL_0008:  ldarg.0
                  IL_0009:  stelem     "T"
                  IL_000e:  dup
                  IL_000f:  ldc.i4.1
                  IL_0010:  ldarg.1
                  IL_0011:  stelem     "T"
                  IL_0016:  call       "System.ReadOnlySpan<T> System.ReadOnlySpan<T>.op_Implicit(T[])"
                  IL_001b:  ret
                }
                """);
        }

        [Fact]
        public void RefSafety_Return_04()
        {
            string source = """
                using System;
                delegate Span<T> D<T>();
                class Program
                {
                    static void Main()
                    {
                        D<int> d = () => [1, 2, 3];
                        Span<int> s = d();
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (7,26): error CS9203: A collection expression of type 'Span<int>' cannot be used in this context because it may be exposed outside of the current scope.
                //         D<int> d = () => [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[1, 2, 3]").WithArguments("System.Span<int>").WithLocation(7, 26));
        }

        [CombinatorialData]
        [Theory]
        public void RefSafety_RefStruct(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework,
            bool useScoped,
            bool useUnsafe)
        {
            string sourceA = $$"""
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public ref struct MyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>({{(useScoped ? "scoped" : "")}} ReadOnlySpan<T> items)
                        => new MyCollection<T>(new List<T>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(
                targetFramework == TargetFramework.Net80 ? new[] { sourceA } : new[] { sourceA, CollectionBuilderAttributeDefinition },
                targetFramework: targetFramework);
            comp.VerifyEmitDiagnostics();
            var refA = comp.EmitToImageReference();

            string sourceB = $$"""
                using System.Collections.Generic;
                {{(useUnsafe ? "unsafe" : "")}} class Program
                {
                    static void Main()
                    {
                        MyCollection<object> x = Empty<object>();
                        MyCollection<object> y = ThreeItems<object>(1, 2, 3);
                        Report(x);
                        Report(y);
                    }
                    static MyCollection<T> Empty<T>() => [];
                    static MyCollection<T> ThreeItems<T>(T x, T y, T z) => [x, y, z];
                    static void Report<T>(MyCollection<T> c)
                    {
                        var list = new List<T>();
                        foreach (var i in c) list.Add(i);
                        list.Report();
                    }
                }
                """;
            comp = CreateCompilation(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: targetFramework,
                options: useUnsafe ? TestOptions.UnsafeReleaseExe : TestOptions.ReleaseExe);
            if (!useScoped)
            {
                comp.VerifyEmitDiagnostics(
                    // 0.cs(12,60): error CS9203: A collection expression of type 'MyCollection<T>' cannot be used in this context because it may be exposed outside of the current scope.
                    //     static MyCollection<T> ThreeItems<T>(T x, T y, T z) => [x, y, z];
                    Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[x, y, z]").WithArguments("MyCollection<T>").WithLocation(12, 60));
            }
            else
            {
                var verifier = CompileAndVerify(comp,
                    verify: Verification.Skipped,
                    expectedOutput: IncludeExpectedOutput("[], [1, 2, 3], "));
                verifier.VerifyIL("Program.Empty<T>", """
                    {
                      // Code size       16 (0x10)
                      .maxstack  1
                      IL_0000:  call       "T[] System.Array.Empty<T>()"
                      IL_0005:  newobj     "System.ReadOnlySpan<T>..ctor(T[])"
                      IL_000a:  call       "MyCollection<T> MyCollectionBuilder.Create<T>(scoped System.ReadOnlySpan<T>)"
                      IL_000f:  ret
                    }
                    """);
                if (targetFramework == TargetFramework.Net80)
                {
                    verifier.VerifyIL("Program.ThreeItems<T>", """
                        {
                          // Code size       64 (0x40)
                          .maxstack  2
                          .locals init (<>y__InlineArray3<T> V_0)
                          IL_0000:  ldloca.s   V_0
                          IL_0002:  initobj    "<>y__InlineArray3<T>"
                          IL_0008:  ldloca.s   V_0
                          IL_000a:  ldc.i4.0
                          IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                          IL_0010:  ldarg.0
                          IL_0011:  stobj      "T"
                          IL_0016:  ldloca.s   V_0
                          IL_0018:  ldc.i4.1
                          IL_0019:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                          IL_001e:  ldarg.1
                          IL_001f:  stobj      "T"
                          IL_0024:  ldloca.s   V_0
                          IL_0026:  ldc.i4.2
                          IL_0027:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                          IL_002c:  ldarg.2
                          IL_002d:  stobj      "T"
                          IL_0032:  ldloca.s   V_0
                          IL_0034:  ldc.i4.3
                          IL_0035:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<T>, T>(in <>y__InlineArray3<T>, int)"
                          IL_003a:  call       "MyCollection<T> MyCollectionBuilder.Create<T>(scoped System.ReadOnlySpan<T>)"
                          IL_003f:  ret
                        }
                        """);
                }
                else
                {
                    verifier.VerifyIL("Program.ThreeItems<T>", """
                        {
                          // Code size       41 (0x29)
                          .maxstack  4
                          IL_0000:  ldc.i4.3
                          IL_0001:  newarr     "T"
                          IL_0006:  dup
                          IL_0007:  ldc.i4.0
                          IL_0008:  ldarg.0
                          IL_0009:  stelem     "T"
                          IL_000e:  dup
                          IL_000f:  ldc.i4.1
                          IL_0010:  ldarg.1
                          IL_0011:  stelem     "T"
                          IL_0016:  dup
                          IL_0017:  ldc.i4.2
                          IL_0018:  ldarg.2
                          IL_0019:  stelem     "T"
                          IL_001e:  newobj     "System.ReadOnlySpan<T>..ctor(T[])"
                          IL_0023:  call       "MyCollection<T> MyCollectionBuilder.Create<T>(scoped System.ReadOnlySpan<T>)"
                          IL_0028:  ret
                        }
                        """);
                }
            }
        }

        // As above, but with C#10 ref safety rules.
        [Theory]
        [CombinatorialData]
        public void RefSafety_RefStruct_CSharp10Rules(bool useCompilationReference)
        {
            string sourceA = $$"""
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public ref struct MyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                        => new MyCollection<T>(new List<T>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(new[] { sourceA, CollectionBuilderAttributeDefinition }, parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.CSharp10), targetFramework: TargetFramework.Net60);
            comp.VerifyEmitDiagnostics();
            Assert.False(comp.SourceModule.UseUpdatedEscapeRules);

            var refA = AsReference(comp, useCompilationReference);

            string sourceB = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        MyCollection<object> x = Empty<object>();
                        MyCollection<object> y = ThreeItems<object>(1, 2, 3);
                        Report(x);
                        Report(y);
                    }
                    static MyCollection<T> Empty<T>() => [];
                    static MyCollection<T> ThreeItems<T>(T x, T y, T z) => [x, y, z];
                    static void Report<T>(MyCollection<T> c)
                    {
                        var list = new List<T>();
                        foreach (var i in c) list.Add(i);
                        list.Report();
                    }
                }
                """;
            comp = CreateCompilation(new[] { sourceB, s_collectionExtensions }, references: new[] { refA }, targetFramework: TargetFramework.Net60, options: TestOptions.ReleaseExe);
            comp.VerifyEmitDiagnostics(
                // 0.cs(12,60): error CS9203: A collection expression of type 'MyCollection<T>' cannot be used in this context because it may be exposed outside of the current scope.
                //     static MyCollection<T> ThreeItems<T>(T x, T y, T z) => [x, y, z];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[x, y, z]").WithArguments("MyCollection<T>").WithLocation(12, 60));
        }

        [CombinatorialData]
        [Theory]
        public void SpanArgument_01([CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F1<object>([1]);
                        F2<int?>([2]);
                        F3<int?>([3]);
                        F4<object>([4]);
                    }
                    static void F1<T>(Span<T> s) { s.Report(); }
                    static void F2<T>(ReadOnlySpan<T> s) { s.Report(); }
                    static void F3<T>(in Span<T> s) { s.Report(); }
                    static void F4<T>(in ReadOnlySpan<T> s) { s.Report(); }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: targetFramework,
                verify: Verification.Skipped,
                symbolValidator: module =>
                {
                    if (targetFramework == TargetFramework.Net80)
                    {
                        var synthesizedType = module.GlobalNamespace.GetTypeMember("<>y__InlineArray1");
                        Assert.Equal("<>y__InlineArray1<T>", synthesizedType.ToTestDisplayString());
                        Assert.Equal("<>y__InlineArray1`1", synthesizedType.MetadataName);
                    }
                },
                expectedOutput: IncludeExpectedOutput("[1], [2], [3], [4], "));
            if (targetFramework == TargetFramework.Net80)
            {
                verifier.VerifyIL("Program.Main", """
                    {
                      // Code size      161 (0xa1)
                      .maxstack  2
                      .locals init (<>y__InlineArray1<object> V_0,
                                    <>y__InlineArray1<int?> V_1,
                                    <>y__InlineArray1<int?> V_2,
                                    <>y__InlineArray1<object> V_3,
                                    System.Span<int?> V_4,
                                    System.ReadOnlySpan<object> V_5)
                      IL_0000:  ldloca.s   V_0
                      IL_0002:  initobj    "<>y__InlineArray1<object>"
                      IL_0008:  ldloca.s   V_0
                      IL_000a:  ldc.i4.0
                      IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0010:  ldc.i4.1
                      IL_0011:  box        "int"
                      IL_0016:  stind.ref
                      IL_0017:  ldloca.s   V_0
                      IL_0019:  ldc.i4.1
                      IL_001a:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_001f:  call       "void Program.F1<object>(System.Span<object>)"
                      IL_0024:  ldloca.s   V_1
                      IL_0026:  initobj    "<>y__InlineArray1<int?>"
                      IL_002c:  ldloca.s   V_1
                      IL_002e:  ldc.i4.0
                      IL_002f:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                      IL_0034:  ldc.i4.2
                      IL_0035:  newobj     "int?..ctor(int)"
                      IL_003a:  stobj      "int?"
                      IL_003f:  ldloca.s   V_1
                      IL_0041:  ldc.i4.1
                      IL_0042:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                      IL_0047:  call       "void Program.F2<int?>(System.ReadOnlySpan<int?>)"
                      IL_004c:  ldloca.s   V_2
                      IL_004e:  initobj    "<>y__InlineArray1<int?>"
                      IL_0054:  ldloca.s   V_2
                      IL_0056:  ldc.i4.0
                      IL_0057:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                      IL_005c:  ldc.i4.3
                      IL_005d:  newobj     "int?..ctor(int)"
                      IL_0062:  stobj      "int?"
                      IL_0067:  ldloca.s   V_2
                      IL_0069:  ldc.i4.1
                      IL_006a:  call       "InlineArrayAsSpan<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                      IL_006f:  stloc.s    V_4
                      IL_0071:  ldloca.s   V_4
                      IL_0073:  call       "void Program.F3<int?>(in System.Span<int?>)"
                      IL_0078:  ldloca.s   V_3
                      IL_007a:  initobj    "<>y__InlineArray1<object>"
                      IL_0080:  ldloca.s   V_3
                      IL_0082:  ldc.i4.0
                      IL_0083:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0088:  ldc.i4.4
                      IL_0089:  box        "int"
                      IL_008e:  stind.ref
                      IL_008f:  ldloca.s   V_3
                      IL_0091:  ldc.i4.1
                      IL_0092:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                      IL_0097:  stloc.s    V_5
                      IL_0099:  ldloca.s   V_5
                      IL_009b:  call       "void Program.F4<object>(in System.ReadOnlySpan<object>)"
                      IL_00a0:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.Main", """
                    {
                      // Code size      115 (0x73)
                      .maxstack  4
                      .locals init (System.Span<int?> V_0,
                                    System.ReadOnlySpan<object> V_1)
                      IL_0000:  ldc.i4.1
                      IL_0001:  newarr     "object"
                      IL_0006:  dup
                      IL_0007:  ldc.i4.0
                      IL_0008:  ldc.i4.1
                      IL_0009:  box        "int"
                      IL_000e:  stelem.ref
                      IL_000f:  newobj     "System.Span<object>..ctor(object[])"
                      IL_0014:  call       "void Program.F1<object>(System.Span<object>)"
                      IL_0019:  ldc.i4.1
                      IL_001a:  newarr     "int?"
                      IL_001f:  dup
                      IL_0020:  ldc.i4.0
                      IL_0021:  ldc.i4.2
                      IL_0022:  newobj     "int?..ctor(int)"
                      IL_0027:  stelem     "int?"
                      IL_002c:  newobj     "System.ReadOnlySpan<int?>..ctor(int?[])"
                      IL_0031:  call       "void Program.F2<int?>(System.ReadOnlySpan<int?>)"
                      IL_0036:  ldc.i4.1
                      IL_0037:  newarr     "int?"
                      IL_003c:  dup
                      IL_003d:  ldc.i4.0
                      IL_003e:  ldc.i4.3
                      IL_003f:  newobj     "int?..ctor(int)"
                      IL_0044:  stelem     "int?"
                      IL_0049:  newobj     "System.Span<int?>..ctor(int?[])"
                      IL_004e:  stloc.0
                      IL_004f:  ldloca.s   V_0
                      IL_0051:  call       "void Program.F3<int?>(in System.Span<int?>)"
                      IL_0056:  ldc.i4.1
                      IL_0057:  newarr     "object"
                      IL_005c:  dup
                      IL_005d:  ldc.i4.0
                      IL_005e:  ldc.i4.4
                      IL_005f:  box        "int"
                      IL_0064:  stelem.ref
                      IL_0065:  newobj     "System.ReadOnlySpan<object>..ctor(object[])"
                      IL_006a:  stloc.1
                      IL_006b:  ldloca.s   V_1
                      IL_006d:  call       "void Program.F4<object>(in System.ReadOnlySpan<object>)"
                      IL_0072:  ret
                    }
                    """);
            }
        }

        [Fact]
        public void SpanArgument_02()
        {
            string source = """
                using System;
                struct S { }
                ref struct R { }
                class Program
                {
                    static void Main()
                    {
                        ReturnsStruct<object>([1]);
                        ReturnsRefStruct<object>([2]);
                        ReturnsRef<object>([3]);
                        ReturnsRefReadOnly<object>([4]);
                    }
                    static int _f = 0;
                    static S ReturnsStruct<T>(Span<T> s) { s.Report(); return default; }
                    static R ReturnsRefStruct<T>(Span<T> s) { s.Report(); return default; }
                    static ref int ReturnsRef<T>(Span<T> s) { s.Report(); return ref _f; }
                    static ref readonly int ReturnsRefReadOnly<T>(Span<T> s) { s.Report(); return ref _f; }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [2], [3], [4], "));
            verifier.VerifyIL("Program.Main", """
                {
                    // Code size      149 (0x95)
                    .maxstack  2
                    .locals init (<>y__InlineArray1<object> V_0,
                                <>y__InlineArray1<object> V_1,
                                <>y__InlineArray1<object> V_2,
                                <>y__InlineArray1<object> V_3)
                    IL_0000:  ldloca.s   V_0
                    IL_0002:  initobj    "<>y__InlineArray1<object>"
                    IL_0008:  ldloca.s   V_0
                    IL_000a:  ldc.i4.0
                    IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0010:  ldc.i4.1
                    IL_0011:  box        "int"
                    IL_0016:  stind.ref
                    IL_0017:  ldloca.s   V_0
                    IL_0019:  ldc.i4.1
                    IL_001a:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_001f:  call       "S Program.ReturnsStruct<object>(System.Span<object>)"
                    IL_0024:  pop
                    IL_0025:  ldloca.s   V_1
                    IL_0027:  initobj    "<>y__InlineArray1<object>"
                    IL_002d:  ldloca.s   V_1
                    IL_002f:  ldc.i4.0
                    IL_0030:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0035:  ldc.i4.2
                    IL_0036:  box        "int"
                    IL_003b:  stind.ref
                    IL_003c:  ldloca.s   V_1
                    IL_003e:  ldc.i4.1
                    IL_003f:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0044:  call       "R Program.ReturnsRefStruct<object>(System.Span<object>)"
                    IL_0049:  pop
                    IL_004a:  ldloca.s   V_2
                    IL_004c:  initobj    "<>y__InlineArray1<object>"
                    IL_0052:  ldloca.s   V_2
                    IL_0054:  ldc.i4.0
                    IL_0055:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_005a:  ldc.i4.3
                    IL_005b:  box        "int"
                    IL_0060:  stind.ref
                    IL_0061:  ldloca.s   V_2
                    IL_0063:  ldc.i4.1
                    IL_0064:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0069:  call       "ref int Program.ReturnsRef<object>(System.Span<object>)"
                    IL_006e:  pop
                    IL_006f:  ldloca.s   V_3
                    IL_0071:  initobj    "<>y__InlineArray1<object>"
                    IL_0077:  ldloca.s   V_3
                    IL_0079:  ldc.i4.0
                    IL_007a:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_007f:  ldc.i4.4
                    IL_0080:  box        "int"
                    IL_0085:  stind.ref
                    IL_0086:  ldloca.s   V_3
                    IL_0088:  ldc.i4.1
                    IL_0089:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_008e:  call       "ref readonly int Program.ReturnsRefReadOnly<object>(System.Span<object>)"
                    IL_0093:  pop
                    IL_0094:  ret
                }
                """);
        }

        [Fact]
        public void SpanArgument_03()
        {
            string source = """
                using System;
                struct S { }
                ref struct R { }
                class Program
                {
                    static void Main()
                    {
                        ReturnsRefStruct<object>([2]);
                        ReturnsRef<object>([3]);
                        ReturnsRefReadOnly<object>([4]);
                    }
                    static int _f = 0;
                    static R ReturnsRefStruct<T>(scoped Span<T> s) { s.Report(); return default; }
                    static ref int ReturnsRef<T>(scoped Span<T> s) { s.Report(); return ref _f; }
                    static ref readonly int ReturnsRefReadOnly<T>(scoped Span<T> s) { s.Report(); return ref _f; }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[2], [3], [4], "));
            verifier.VerifyIL("Program.Main", """
                {
                    // Code size      112 (0x70)
                    .maxstack  2
                    .locals init (<>y__InlineArray1<object> V_0,
                                <>y__InlineArray1<object> V_1,
                                <>y__InlineArray1<object> V_2)
                    IL_0000:  ldloca.s   V_0
                    IL_0002:  initobj    "<>y__InlineArray1<object>"
                    IL_0008:  ldloca.s   V_0
                    IL_000a:  ldc.i4.0
                    IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0010:  ldc.i4.2
                    IL_0011:  box        "int"
                    IL_0016:  stind.ref
                    IL_0017:  ldloca.s   V_0
                    IL_0019:  ldc.i4.1
                    IL_001a:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_001f:  call       "R Program.ReturnsRefStruct<object>(scoped System.Span<object>)"
                    IL_0024:  pop
                    IL_0025:  ldloca.s   V_1
                    IL_0027:  initobj    "<>y__InlineArray1<object>"
                    IL_002d:  ldloca.s   V_1
                    IL_002f:  ldc.i4.0
                    IL_0030:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0035:  ldc.i4.3
                    IL_0036:  box        "int"
                    IL_003b:  stind.ref
                    IL_003c:  ldloca.s   V_1
                    IL_003e:  ldc.i4.1
                    IL_003f:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0044:  call       "ref int Program.ReturnsRef<object>(scoped System.Span<object>)"
                    IL_0049:  pop
                    IL_004a:  ldloca.s   V_2
                    IL_004c:  initobj    "<>y__InlineArray1<object>"
                    IL_0052:  ldloca.s   V_2
                    IL_0054:  ldc.i4.0
                    IL_0055:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_005a:  ldc.i4.4
                    IL_005b:  box        "int"
                    IL_0060:  stind.ref
                    IL_0061:  ldloca.s   V_2
                    IL_0063:  ldc.i4.1
                    IL_0064:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0069:  call       "ref readonly int Program.ReturnsRefReadOnly<object>(scoped System.Span<object>)"
                    IL_006e:  pop
                    IL_006f:  ret
                }
                """);
        }

        [Fact]
        public void SpanArgument_04()
        {
            string source = """
                using System;
                ref struct R1
                {
                    public void M(ReadOnlySpan<int?> s) { s.Report(); }
                    public object this[ReadOnlySpan<int?> s] { set { s.Report(); } }
                }
                class Program
                {
                    static void Main()
                    {
                        var r1 = new R1();
                        r1.M([3]);
                        r1[[4]] = null;

                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensionsWithSpan }, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // 0.cs(12,9): error CS8350: This combination of arguments to 'R1.M(ReadOnlySpan<int?>)' is disallowed because it may expose variables referenced by parameter 's' outside of their declaration scope
                //         r1.M([3]);
                Diagnostic(ErrorCode.ERR_CallArgMixing, "r1.M([3])").WithArguments("R1.M(System.ReadOnlySpan<int?>)", "s").WithLocation(12, 9),
                // 0.cs(12,14): error CS9203: A collection expression of type 'ReadOnlySpan<int?>' cannot be used in this context because it may be exposed outside of the current scope.
                //         r1.M([3]);
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[3]").WithArguments("System.ReadOnlySpan<int?>").WithLocation(12, 14),
                // 0.cs(13,9): error CS8350: This combination of arguments to 'R1.this[ReadOnlySpan<int?>]' is disallowed because it may expose variables referenced by parameter 's' outside of their declaration scope
                //         r1[[4]] = null;
                Diagnostic(ErrorCode.ERR_CallArgMixing, "r1[[4]]").WithArguments("R1.this[System.ReadOnlySpan<int?>]", "s").WithLocation(13, 9),
                // 0.cs(13,12): error CS9203: A collection expression of type 'ReadOnlySpan<int?>' cannot be used in this context because it may be exposed outside of the current scope.
                //         r1[[4]] = null;
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[4]").WithArguments("System.ReadOnlySpan<int?>").WithLocation(13, 12));
        }

        [Fact]
        public void SpanArgument_05()
        {
            string source = """
                using System;
                struct S
                {
                    public void M(ReadOnlySpan<int?> s) { s.Report(); }
                    public object this[ReadOnlySpan<int?> s] { set { s.Report(); } }
                }
                ref struct R1
                {
                    public void M(ReadOnlySpan<int?> s) { s.Report(); }
                    public object this[ReadOnlySpan<int?> s] { set { s.Report(); } }
                }
                ref struct R2
                {
                    public void M(scoped ReadOnlySpan<int?> s) { s.Report(); }
                    public object this[scoped ReadOnlySpan<int?> s] { set { s.Report(); } }
                }
                class Program
                {
                    static void Main()
                    {
                        var s = new S();
                        s.M([1]);
                        s[[2]] = null;
                        scoped var r1 = new R1();
                        r1.M([3]);
                        r1[[4]] = null;
                        var r2 = new R2();
                        r2.M([5]);
                        r2[[6]] = null;
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [2], [3], [4], [5], [6], "));
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size      280 (0x118)
                  .maxstack  3
                  .locals init (S V_0, //s
                                R1 V_1, //r1
                                R2 V_2, //r2
                                <>y__InlineArray1<int?> V_3,
                                <>y__InlineArray1<int?> V_4,
                                <>y__InlineArray1<int?> V_5,
                                <>y__InlineArray1<int?> V_6,
                                <>y__InlineArray1<int?> V_7,
                                <>y__InlineArray1<int?> V_8)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "S"
                  IL_0008:  ldloca.s   V_0
                  IL_000a:  ldloca.s   V_3
                  IL_000c:  initobj    "<>y__InlineArray1<int?>"
                  IL_0012:  ldloca.s   V_3
                  IL_0014:  ldc.i4.0
                  IL_0015:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_001a:  ldc.i4.1
                  IL_001b:  newobj     "int?..ctor(int)"
                  IL_0020:  stobj      "int?"
                  IL_0025:  ldloca.s   V_3
                  IL_0027:  ldc.i4.1
                  IL_0028:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_002d:  call       "void S.M(System.ReadOnlySpan<int?>)"
                  IL_0032:  ldloca.s   V_0
                  IL_0034:  ldloca.s   V_4
                  IL_0036:  initobj    "<>y__InlineArray1<int?>"
                  IL_003c:  ldloca.s   V_4
                  IL_003e:  ldc.i4.0
                  IL_003f:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_0044:  ldc.i4.2
                  IL_0045:  newobj     "int?..ctor(int)"
                  IL_004a:  stobj      "int?"
                  IL_004f:  ldloca.s   V_4
                  IL_0051:  ldc.i4.1
                  IL_0052:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_0057:  ldnull
                  IL_0058:  call       "void S.this[System.ReadOnlySpan<int?>].set"
                  IL_005d:  ldloca.s   V_1
                  IL_005f:  initobj    "R1"
                  IL_0065:  ldloca.s   V_1
                  IL_0067:  ldloca.s   V_5
                  IL_0069:  initobj    "<>y__InlineArray1<int?>"
                  IL_006f:  ldloca.s   V_5
                  IL_0071:  ldc.i4.0
                  IL_0072:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_0077:  ldc.i4.3
                  IL_0078:  newobj     "int?..ctor(int)"
                  IL_007d:  stobj      "int?"
                  IL_0082:  ldloca.s   V_5
                  IL_0084:  ldc.i4.1
                  IL_0085:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_008a:  call       "void R1.M(System.ReadOnlySpan<int?>)"
                  IL_008f:  ldloca.s   V_1
                  IL_0091:  ldloca.s   V_6
                  IL_0093:  initobj    "<>y__InlineArray1<int?>"
                  IL_0099:  ldloca.s   V_6
                  IL_009b:  ldc.i4.0
                  IL_009c:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_00a1:  ldc.i4.4
                  IL_00a2:  newobj     "int?..ctor(int)"
                  IL_00a7:  stobj      "int?"
                  IL_00ac:  ldloca.s   V_6
                  IL_00ae:  ldc.i4.1
                  IL_00af:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_00b4:  ldnull
                  IL_00b5:  call       "void R1.this[System.ReadOnlySpan<int?>].set"
                  IL_00ba:  ldloca.s   V_2
                  IL_00bc:  initobj    "R2"
                  IL_00c2:  ldloca.s   V_2
                  IL_00c4:  ldloca.s   V_7
                  IL_00c6:  initobj    "<>y__InlineArray1<int?>"
                  IL_00cc:  ldloca.s   V_7
                  IL_00ce:  ldc.i4.0
                  IL_00cf:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_00d4:  ldc.i4.5
                  IL_00d5:  newobj     "int?..ctor(int)"
                  IL_00da:  stobj      "int?"
                  IL_00df:  ldloca.s   V_7
                  IL_00e1:  ldc.i4.1
                  IL_00e2:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_00e7:  call       "void R2.M(scoped System.ReadOnlySpan<int?>)"
                  IL_00ec:  ldloca.s   V_2
                  IL_00ee:  ldloca.s   V_8
                  IL_00f0:  initobj    "<>y__InlineArray1<int?>"
                  IL_00f6:  ldloca.s   V_8
                  IL_00f8:  ldc.i4.0
                  IL_00f9:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_00fe:  ldc.i4.6
                  IL_00ff:  newobj     "int?..ctor(int)"
                  IL_0104:  stobj      "int?"
                  IL_0109:  ldloca.s   V_8
                  IL_010b:  ldc.i4.1
                  IL_010c:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_0111:  ldnull
                  IL_0112:  call       "void R2.this[scoped System.ReadOnlySpan<int?>].set"
                  IL_0117:  ret
                }
                """);
        }

        [Fact]
        public void SpanArgument_ReadOnlyMembers()
        {
            string source = """
                using System;
                readonly ref struct R1
                {
                    public void M(ReadOnlySpan<int?> s) { s.Report(); }
                    public object this[ReadOnlySpan<int?> s] { get { s.Report(); return null; } }
                }
                ref struct R2
                {
                    public readonly void M(ReadOnlySpan<int?> s) { s.Report(); }
                    public readonly object this[ReadOnlySpan<int?> s] { get { s.Report(); return null; } }
                }
                class Program
                {
                    static void Main()
                    {
                        var r1 = new R1();
                        r1.M([3]);
                        _ = r1[[4]];
                        var r2 = new R2();
                        r2.M([5]);
                        _ = r2[[6]];
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[3], [4], [5], [6], "));
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size      187 (0xbb)
                  .maxstack  3
                  .locals init (R1 V_0, //r1
                                R2 V_1, //r2
                                <>y__InlineArray1<int?> V_2,
                                <>y__InlineArray1<int?> V_3,
                                <>y__InlineArray1<int?> V_4,
                                <>y__InlineArray1<int?> V_5)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "R1"
                  IL_0008:  ldloca.s   V_0
                  IL_000a:  ldloca.s   V_2
                  IL_000c:  initobj    "<>y__InlineArray1<int?>"
                  IL_0012:  ldloca.s   V_2
                  IL_0014:  ldc.i4.0
                  IL_0015:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_001a:  ldc.i4.3
                  IL_001b:  newobj     "int?..ctor(int)"
                  IL_0020:  stobj      "int?"
                  IL_0025:  ldloca.s   V_2
                  IL_0027:  ldc.i4.1
                  IL_0028:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_002d:  call       "void R1.M(System.ReadOnlySpan<int?>)"
                  IL_0032:  ldloca.s   V_0
                  IL_0034:  ldloca.s   V_3
                  IL_0036:  initobj    "<>y__InlineArray1<int?>"
                  IL_003c:  ldloca.s   V_3
                  IL_003e:  ldc.i4.0
                  IL_003f:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_0044:  ldc.i4.4
                  IL_0045:  newobj     "int?..ctor(int)"
                  IL_004a:  stobj      "int?"
                  IL_004f:  ldloca.s   V_3
                  IL_0051:  ldc.i4.1
                  IL_0052:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_0057:  call       "object R1.this[System.ReadOnlySpan<int?>].get"
                  IL_005c:  pop
                  IL_005d:  ldloca.s   V_1
                  IL_005f:  initobj    "R2"
                  IL_0065:  ldloca.s   V_1
                  IL_0067:  ldloca.s   V_4
                  IL_0069:  initobj    "<>y__InlineArray1<int?>"
                  IL_006f:  ldloca.s   V_4
                  IL_0071:  ldc.i4.0
                  IL_0072:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_0077:  ldc.i4.5
                  IL_0078:  newobj     "int?..ctor(int)"
                  IL_007d:  stobj      "int?"
                  IL_0082:  ldloca.s   V_4
                  IL_0084:  ldc.i4.1
                  IL_0085:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_008a:  call       "readonly void R2.M(System.ReadOnlySpan<int?>)"
                  IL_008f:  ldloca.s   V_1
                  IL_0091:  ldloca.s   V_5
                  IL_0093:  initobj    "<>y__InlineArray1<int?>"
                  IL_0099:  ldloca.s   V_5
                  IL_009b:  ldc.i4.0
                  IL_009c:  call       "InlineArrayElementRef<<>y__InlineArray1<int?>, int?>(ref <>y__InlineArray1<int?>, int)"
                  IL_00a1:  ldc.i4.6
                  IL_00a2:  newobj     "int?..ctor(int)"
                  IL_00a7:  stobj      "int?"
                  IL_00ac:  ldloca.s   V_5
                  IL_00ae:  ldc.i4.1
                  IL_00af:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int?>, int?>(in <>y__InlineArray1<int?>, int)"
                  IL_00b4:  call       "readonly object R2.this[System.ReadOnlySpan<int?>].get"
                  IL_00b9:  pop
                  IL_00ba:  ret
                }
                """);
        }

        [Fact]
        public void SpanArgument_Nested()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F1([F1([1]) + 2]);
                        F2([F2([2]) + 2]);
                    }
                    static T F1<T>(Span<T> s) { s.Report(); return s[0]; }
                    static T F2<T>(ReadOnlySpan<T> s) { s.Report(); return s[0]; }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [3], [2], [4], "));
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size      113 (0x71)
                  .maxstack  3
                  .locals init (<>y__InlineArray1<int> V_0,
                                <>y__InlineArray1<int> V_1,
                                <>y__InlineArray1<int> V_2)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "<>y__InlineArray1<int>"
                  IL_0008:  ldloca.s   V_0
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<int>, int>(ref <>y__InlineArray1<int>, int)"
                  IL_0010:  ldloca.s   V_1
                  IL_0012:  initobj    "<>y__InlineArray1<int>"
                  IL_0018:  ldloca.s   V_1
                  IL_001a:  ldc.i4.0
                  IL_001b:  call       "InlineArrayElementRef<<>y__InlineArray1<int>, int>(ref <>y__InlineArray1<int>, int)"
                  IL_0020:  ldc.i4.1
                  IL_0021:  stind.i4
                  IL_0022:  ldloca.s   V_1
                  IL_0024:  ldc.i4.1
                  IL_0025:  call       "InlineArrayAsSpan<<>y__InlineArray1<int>, int>(ref <>y__InlineArray1<int>, int)"
                  IL_002a:  call       "int Program.F1<int>(System.Span<int>)"
                  IL_002f:  ldc.i4.2
                  IL_0030:  add
                  IL_0031:  stind.i4
                  IL_0032:  ldloca.s   V_0
                  IL_0034:  ldc.i4.1
                  IL_0035:  call       "InlineArrayAsSpan<<>y__InlineArray1<int>, int>(ref <>y__InlineArray1<int>, int)"
                  IL_003a:  call       "int Program.F1<int>(System.Span<int>)"
                  IL_003f:  pop
                  IL_0040:  ldloca.s   V_2
                  IL_0042:  initobj    "<>y__InlineArray1<int>"
                  IL_0048:  ldloca.s   V_2
                  IL_004a:  ldc.i4.0
                  IL_004b:  call       "InlineArrayElementRef<<>y__InlineArray1<int>, int>(ref <>y__InlineArray1<int>, int)"
                  IL_0050:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.26B25D457597A7B0463F9620F666DD10AA2C4373A505967C7C8D70922A2D6ECE4"
                  IL_0055:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
                  IL_005a:  call       "int Program.F2<int>(System.ReadOnlySpan<int>)"
                  IL_005f:  ldc.i4.2
                  IL_0060:  add
                  IL_0061:  stind.i4
                  IL_0062:  ldloca.s   V_2
                  IL_0064:  ldc.i4.1
                  IL_0065:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<int>, int>(in <>y__InlineArray1<int>, int)"
                  IL_006a:  call       "int Program.F2<int>(System.ReadOnlySpan<int>)"
                  IL_006f:  pop
                  IL_0070:  ret
                }
                """);
        }

        [Fact]
        public void SpanArgument_Reordered()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F1<object>(y: [1], x: [2]);
                        F2<object>(y: [3], x: [4]);
                    }
                    static Span<T> F1<T>(Span<T> x, scoped Span<T> y)
                    {
                        x.Report();
                        y.Report();
                        return x;
                    }
                    static ReadOnlySpan<T> F2<T>(scoped ReadOnlySpan<T> x, ReadOnlySpan<T> y)
                    {
                        x.Report();
                        y.Report();
                        return y;
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[2], [1], [4], [3], "));
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size      145 (0x91)
                  .maxstack  2
                  .locals init (<>y__InlineArray1<object> V_0,
                                <>y__InlineArray1<object> V_1,
                                <>y__InlineArray1<object> V_2,
                                <>y__InlineArray1<object> V_3,
                                System.Span<object> V_4,
                                System.ReadOnlySpan<object> V_5)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "<>y__InlineArray1<object>"
                  IL_0008:  ldloca.s   V_0
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0010:  ldc.i4.1
                  IL_0011:  box        "int"
                  IL_0016:  stind.ref
                  IL_0017:  ldloca.s   V_0
                  IL_0019:  ldc.i4.1
                  IL_001a:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_001f:  stloc.s    V_4
                  IL_0021:  ldloca.s   V_1
                  IL_0023:  initobj    "<>y__InlineArray1<object>"
                  IL_0029:  ldloca.s   V_1
                  IL_002b:  ldc.i4.0
                  IL_002c:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0031:  ldc.i4.2
                  IL_0032:  box        "int"
                  IL_0037:  stind.ref
                  IL_0038:  ldloca.s   V_1
                  IL_003a:  ldc.i4.1
                  IL_003b:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0040:  ldloc.s    V_4
                  IL_0042:  call       "System.Span<object> Program.F1<object>(System.Span<object>, scoped System.Span<object>)"
                  IL_0047:  pop
                  IL_0048:  ldloca.s   V_2
                  IL_004a:  initobj    "<>y__InlineArray1<object>"
                  IL_0050:  ldloca.s   V_2
                  IL_0052:  ldc.i4.0
                  IL_0053:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0058:  ldc.i4.3
                  IL_0059:  box        "int"
                  IL_005e:  stind.ref
                  IL_005f:  ldloca.s   V_2
                  IL_0061:  ldc.i4.1
                  IL_0062:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                  IL_0067:  stloc.s    V_5
                  IL_0069:  ldloca.s   V_3
                  IL_006b:  initobj    "<>y__InlineArray1<object>"
                  IL_0071:  ldloca.s   V_3
                  IL_0073:  ldc.i4.0
                  IL_0074:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0079:  ldc.i4.4
                  IL_007a:  box        "int"
                  IL_007f:  stind.ref
                  IL_0080:  ldloca.s   V_3
                  IL_0082:  ldc.i4.1
                  IL_0083:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                  IL_0088:  ldloc.s    V_5
                  IL_008a:  call       "System.ReadOnlySpan<object> Program.F2<object>(scoped System.ReadOnlySpan<object>, System.ReadOnlySpan<object>)"
                  IL_008f:  pop
                  IL_0090:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void SpanArgument_Constructor_01(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework,
            bool useScoped)
        {
            string source = $$"""
                using System;
                ref struct R<T>
                {
                    public R(T x, T y, T z) : this([x, y, z])
                    {
                    }
                    public R(int x, T[] y) : this([..y])
                    {
                    }
                    public R({{(useScoped ? "scoped" : "")}} Span<T> s)
                    {
                        F = s.ToArray();
                    }
                    public readonly T[] F;
                }
                class Program
                {
                    static void Main()
                    {
                        R<int> x = new R<int>(1, 2, 3);
                        R<object> y = new R<object>(new object[] { 4, 5 });
                        x.F.Report();
                        y.F.Report();
                    }
                }
                """;
            var comp = CreateCompilation(
                new[] { source, s_collectionExtensions },
                targetFramework: targetFramework,
                options: TestOptions.ReleaseExe);
            if (!useScoped)
            {
                comp.VerifyEmitDiagnostics(
                    // 0.cs(4,29): error CS8350: This combination of arguments to 'R<T>.R(Span<T>)' is disallowed because it may expose variables referenced by parameter 's' outside of their declaration scope
                    //     public R(T x, T y, T z) : this([x, y, z])
                    Diagnostic(ErrorCode.ERR_CallArgMixing, ": this([x, y, z])").WithArguments("R<T>.R(System.Span<T>)", "s").WithLocation(4, 29),
                    // 0.cs(4,36): error CS9203: A collection expression of type 'Span<T>' cannot be used in this context because it may be exposed outside of the current scope.
                    //     public R(T x, T y, T z) : this([x, y, z])
                    Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[x, y, z]").WithArguments("System.Span<T>").WithLocation(4, 36),
                    // 0.cs(7,28): error CS8350: This combination of arguments to 'R<T>.R(Span<T>)' is disallowed because it may expose variables referenced by parameter 's' outside of their declaration scope
                    //     public R(int x, T[] y) : this([..y])
                    Diagnostic(ErrorCode.ERR_CallArgMixing, ": this([..y])").WithArguments("R<T>.R(System.Span<T>)", "s").WithLocation(7, 28),
                    // 0.cs(7,35): error CS9203: A collection expression of type 'Span<T>' cannot be used in this context because it may be exposed outside of the current scope.
                    //     public R(int x, T[] y) : this([..y])
                    Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[..y]").WithArguments("System.Span<T>").WithLocation(7, 35));
            }
            else if (targetFramework == TargetFramework.Net80)
            {
                var verifier = CompileAndVerify(
                    comp,
                    verify: Verification.Skipped,
                    expectedOutput: IncludeExpectedOutput("[1, 2, 3], [4, 5], "));
                verifier.VerifyIL("R<T>..ctor(T, T, T)", """
                    {
                      // Code size       65 (0x41)
                      .maxstack  3
                      .locals init (<>y__InlineArray3<T> V_0)
                      IL_0000:  ldarg.0
                      IL_0001:  ldloca.s   V_0
                      IL_0003:  initobj    "<>y__InlineArray3<T>"
                      IL_0009:  ldloca.s   V_0
                      IL_000b:  ldc.i4.0
                      IL_000c:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                      IL_0011:  ldarg.1
                      IL_0012:  stobj      "T"
                      IL_0017:  ldloca.s   V_0
                      IL_0019:  ldc.i4.1
                      IL_001a:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                      IL_001f:  ldarg.2
                      IL_0020:  stobj      "T"
                      IL_0025:  ldloca.s   V_0
                      IL_0027:  ldc.i4.2
                      IL_0028:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                      IL_002d:  ldarg.3
                      IL_002e:  stobj      "T"
                      IL_0033:  ldloca.s   V_0
                      IL_0035:  ldc.i4.3
                      IL_0036:  call       "InlineArrayAsSpan<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                      IL_003b:  call       "R<T>..ctor(scoped System.Span<T>)"
                      IL_0040:  ret
                    }
                    """);
                verifier.VerifyIL("R<T>..ctor(int, T[])", """
                    {
                      // Code size       62 (0x3e)
                      .maxstack  3
                      .locals init (int V_0,
                                    T[] V_1,
                                    T[] V_2,
                                    int V_3,
                                    T V_4)
                      IL_0000:  ldarg.2
                      IL_0001:  ldc.i4.0
                      IL_0002:  stloc.0
                      IL_0003:  dup
                      IL_0004:  ldlen
                      IL_0005:  conv.i4
                      IL_0006:  newarr     "T"
                      IL_000b:  stloc.1
                      IL_000c:  stloc.2
                      IL_000d:  ldc.i4.0
                      IL_000e:  stloc.3
                      IL_000f:  br.s       IL_002b
                      IL_0011:  ldloc.2
                      IL_0012:  ldloc.3
                      IL_0013:  ldelem     "T"
                      IL_0018:  stloc.s    V_4
                      IL_001a:  ldloc.1
                      IL_001b:  ldloc.0
                      IL_001c:  ldloc.s    V_4
                      IL_001e:  stelem     "T"
                      IL_0023:  ldloc.0
                      IL_0024:  ldc.i4.1
                      IL_0025:  add
                      IL_0026:  stloc.0
                      IL_0027:  ldloc.3
                      IL_0028:  ldc.i4.1
                      IL_0029:  add
                      IL_002a:  stloc.3
                      IL_002b:  ldloc.3
                      IL_002c:  ldloc.2
                      IL_002d:  ldlen
                      IL_002e:  conv.i4
                      IL_002f:  blt.s      IL_0011
                      IL_0031:  ldarg.0
                      IL_0032:  ldloc.1
                      IL_0033:  newobj     "System.Span<T>..ctor(T[])"
                      IL_0038:  call       "R<T>..ctor(scoped System.Span<T>)"
                      IL_003d:  ret
                    }
                    """);
            }
        }

        [Fact]
        public void SpanArgument_Constructor_02()
        {
            string source = """
                using System;
                record class A<T>(T[] F)
                {
                    public static T[] ToArray(ReadOnlySpan<T> s) => s.ToArray();
                }
                record class B<T>(T x, T y, T z) : A<T>(ToArray([x, y, z]));
                class Program
                {
                    static void Main()
                    {
                        object[] a = F<object>(1, 2, 3);
                        a.Report();
                    }
                    static T[] F<T>(T x, T y, T z)
                    {
                        B<T> b = new B<T>(x, y, z);
                        return b.F;
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("B<T>..ctor(T, T, T)", """
                {
                  // Code size       91 (0x5b)
                  .maxstack  3
                  .locals init (<>y__InlineArray3<T> V_0)
                  IL_0000:  ldarg.0
                  IL_0001:  ldarg.1
                  IL_0002:  stfld      "T B<T>.<x>k__BackingField"
                  IL_0007:  ldarg.0
                  IL_0008:  ldarg.2
                  IL_0009:  stfld      "T B<T>.<y>k__BackingField"
                  IL_000e:  ldarg.0
                  IL_000f:  ldarg.3
                  IL_0010:  stfld      "T B<T>.<z>k__BackingField"
                  IL_0015:  ldarg.0
                  IL_0016:  ldloca.s   V_0
                  IL_0018:  initobj    "<>y__InlineArray3<T>"
                  IL_001e:  ldloca.s   V_0
                  IL_0020:  ldc.i4.0
                  IL_0021:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0026:  ldarg.1
                  IL_0027:  stobj      "T"
                  IL_002c:  ldloca.s   V_0
                  IL_002e:  ldc.i4.1
                  IL_002f:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0034:  ldarg.2
                  IL_0035:  stobj      "T"
                  IL_003a:  ldloca.s   V_0
                  IL_003c:  ldc.i4.2
                  IL_003d:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0042:  ldarg.3
                  IL_0043:  stobj      "T"
                  IL_0048:  ldloca.s   V_0
                  IL_004a:  ldc.i4.3
                  IL_004b:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<T>, T>(in <>y__InlineArray3<T>, int)"
                  IL_0050:  call       "T[] A<T>.ToArray(System.ReadOnlySpan<T>)"
                  IL_0055:  call       "A<T>..ctor(T[])"
                  IL_005a:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void SpanAssignment_01(
            [CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework,
            [CombinatorialValues("Span<object>", "ReadOnlySpan<object>")] string spanType)
        {
            string source = $$"""
                using System;
                class Program
                {
                    static {{spanType}} F1()
                    {
                        {{spanType}} s1 = [];
                        return s1;
                    }
                    static {{spanType}} F2()
                    {
                        {{spanType}} s2 = [2];
                        return s2;
                    }
                    static {{spanType}} F3()
                    {
                        {{spanType}} s3;
                        s3 = [3];
                        return s3;
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: targetFramework);
            comp.VerifyEmitDiagnostics(
                // (12,16): error CS8352: Cannot use variable 's2' in this context because it may expose referenced variables outside of their declaration scope
                //         return s2;
                Diagnostic(ErrorCode.ERR_EscapeVariable, "s2").WithArguments("s2").WithLocation(12, 16),
                // (17,14): error CS9203: A collection expression of type 'Span<object>' cannot be used in this context because it may be exposed outside of the current scope.
                //         s3 = [3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[3]").WithArguments($"System.{spanType}").WithLocation(17, 14));
        }

        [Fact]
        public void SpanAssignment_02()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F1().Report();
                        F2().Report();
                    }
                    static object[] F1()
                    {
                        Span<object> s1 = [1];
                        return s1.ToArray();
                    }
                    static object[] F2()
                    {
                        ReadOnlySpan<object> s2 = [2];
                        return s2.ToArray();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [2], "));
            verifier.VerifyIL("Program.F1", """
                {
                    // Code size       40 (0x28)
                    .maxstack  2
                    .locals init (System.Span<object> V_0, //s1
                                <>y__InlineArray1<object> V_1)
                    IL_0000:  ldloca.s   V_1
                    IL_0002:  initobj    "<>y__InlineArray1<object>"
                    IL_0008:  ldloca.s   V_1
                    IL_000a:  ldc.i4.0
                    IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0010:  ldc.i4.1
                    IL_0011:  box        "int"
                    IL_0016:  stind.ref
                    IL_0017:  ldloca.s   V_1
                    IL_0019:  ldc.i4.1
                    IL_001a:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_001f:  stloc.0
                    IL_0020:  ldloca.s   V_0
                    IL_0022:  call       "object[] System.Span<object>.ToArray()"
                    IL_0027:  ret
                }
                """);
            verifier.VerifyIL("Program.F2", """
                {
                    // Code size       40 (0x28)
                    .maxstack  2
                    .locals init (System.ReadOnlySpan<object> V_0, //s2
                                <>y__InlineArray1<object> V_1)
                    IL_0000:  ldloca.s   V_1
                    IL_0002:  initobj    "<>y__InlineArray1<object>"
                    IL_0008:  ldloca.s   V_1
                    IL_000a:  ldc.i4.0
                    IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0010:  ldc.i4.2
                    IL_0011:  box        "int"
                    IL_0016:  stind.ref
                    IL_0017:  ldloca.s   V_1
                    IL_0019:  ldc.i4.1
                    IL_001a:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                    IL_001f:  stloc.0
                    IL_0020:  ldloca.s   V_0
                    IL_0022:  call       "object[] System.ReadOnlySpan<object>.ToArray()"
                    IL_0027:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void SpanAssignment_03([CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        scoped Span<object> x;
                        scoped ReadOnlySpan<object> y;
                        x = [1];
                        y = [2];
                        x.Report();
                        y.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: targetFramework,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [2], "));
            if (targetFramework == TargetFramework.Net80)
            {
                verifier.VerifyIL("Program.Main", """
                    {
                      // Code size       79 (0x4f)
                      .maxstack  2
                      .locals init (System.Span<object> V_0, //x
                                    System.ReadOnlySpan<object> V_1, //y
                                    <>y__InlineArray1<object> V_2,
                                    <>y__InlineArray1<object> V_3)
                      IL_0000:  ldloca.s   V_2
                      IL_0002:  initobj    "<>y__InlineArray1<object>"
                      IL_0008:  ldloca.s   V_2
                      IL_000a:  ldc.i4.0
                      IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0010:  ldc.i4.1
                      IL_0011:  box        "int"
                      IL_0016:  stind.ref
                      IL_0017:  ldloca.s   V_2
                      IL_0019:  ldc.i4.1
                      IL_001a:  call       "InlineArrayAsSpan<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_001f:  stloc.0
                      IL_0020:  ldloca.s   V_3
                      IL_0022:  initobj    "<>y__InlineArray1<object>"
                      IL_0028:  ldloca.s   V_3
                      IL_002a:  ldc.i4.0
                      IL_002b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0030:  ldc.i4.2
                      IL_0031:  box        "int"
                      IL_0036:  stind.ref
                      IL_0037:  ldloca.s   V_3
                      IL_0039:  ldc.i4.1
                      IL_003a:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                      IL_003f:  stloc.1
                      IL_0040:  ldloca.s   V_0
                      IL_0042:  call       "void CollectionExtensions.Report<object>(in System.Span<object>)"
                      IL_0047:  ldloca.s   V_1
                      IL_0049:  call       "void CollectionExtensions.Report<object>(in System.ReadOnlySpan<object>)"
                      IL_004e:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.Main", """
                    {
                      // Code size       59 (0x3b)
                      .maxstack  5
                      .locals init (System.Span<object> V_0, //x
                                    System.ReadOnlySpan<object> V_1) //y
                      IL_0000:  ldloca.s   V_0
                      IL_0002:  ldc.i4.1
                      IL_0003:  newarr     "object"
                      IL_0008:  dup
                      IL_0009:  ldc.i4.0
                      IL_000a:  ldc.i4.1
                      IL_000b:  box        "int"
                      IL_0010:  stelem.ref
                      IL_0011:  call       "System.Span<object>..ctor(object[])"
                      IL_0016:  ldloca.s   V_1
                      IL_0018:  ldc.i4.1
                      IL_0019:  newarr     "object"
                      IL_001e:  dup
                      IL_001f:  ldc.i4.0
                      IL_0020:  ldc.i4.2
                      IL_0021:  box        "int"
                      IL_0026:  stelem.ref
                      IL_0027:  call       "System.ReadOnlySpan<object>..ctor(object[])"
                      IL_002c:  ldloca.s   V_0
                      IL_002e:  call       "void CollectionExtensions.Report<object>(in System.Span<object>)"
                      IL_0033:  ldloca.s   V_1
                      IL_0035:  call       "void CollectionExtensions.Report<object>(in System.ReadOnlySpan<object>)"
                      IL_003a:  ret
                    }
                    """);
            }
        }

        [Fact]
        public void SpanAssignment_Field_01()
        {
            string source = """
                using System;
                ref struct R<T>
                {
                    public ReadOnlySpan<T> F;
                }
                class Program
                {
                    static void Main()
                    {
                        R<object> r = default;
                        r.F = [1];
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (11,15): error CS9203: A collection expression of type 'ReadOnlySpan<object>' cannot be used in this context because it may be exposed outside of the current scope.
                //         r.F = [1];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[1]").WithArguments("System.ReadOnlySpan<object>").WithLocation(11, 15));
        }

        [Fact]
        public void SpanAssignment_Field_02()
        {
            string source = """
                using System;
                ref struct R<T>
                {
                    public ReadOnlySpan<T> F;
                }
                class Program
                {
                    static void Main()
                    {
                        scoped R<object> x = default;
                        scoped R<object> y = default;
                        x.F = [1];
                        y.F = [2];
                        x.F.Report();
                        y.F.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [2], "));
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size      117 (0x75)
                  .maxstack  3
                  .locals init (R<object> V_0, //x
                                R<object> V_1, //y
                                <>y__InlineArray1<object> V_2,
                                <>y__InlineArray1<object> V_3)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "R<object>"
                  IL_0008:  ldloca.s   V_1
                  IL_000a:  initobj    "R<object>"
                  IL_0010:  ldloca.s   V_0
                  IL_0012:  ldloca.s   V_2
                  IL_0014:  initobj    "<>y__InlineArray1<object>"
                  IL_001a:  ldloca.s   V_2
                  IL_001c:  ldc.i4.0
                  IL_001d:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0022:  ldc.i4.1
                  IL_0023:  box        "int"
                  IL_0028:  stind.ref
                  IL_0029:  ldloca.s   V_2
                  IL_002b:  ldc.i4.1
                  IL_002c:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                  IL_0031:  stfld      "System.ReadOnlySpan<object> R<object>.F"
                  IL_0036:  ldloca.s   V_1
                  IL_0038:  ldloca.s   V_3
                  IL_003a:  initobj    "<>y__InlineArray1<object>"
                  IL_0040:  ldloca.s   V_3
                  IL_0042:  ldc.i4.0
                  IL_0043:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0048:  ldc.i4.2
                  IL_0049:  box        "int"
                  IL_004e:  stind.ref
                  IL_004f:  ldloca.s   V_3
                  IL_0051:  ldc.i4.1
                  IL_0052:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                  IL_0057:  stfld      "System.ReadOnlySpan<object> R<object>.F"
                  IL_005c:  ldloca.s   V_0
                  IL_005e:  ldflda     "System.ReadOnlySpan<object> R<object>.F"
                  IL_0063:  call       "void CollectionExtensions.Report<object>(in System.ReadOnlySpan<object>)"
                  IL_0068:  ldloca.s   V_1
                  IL_006a:  ldflda     "System.ReadOnlySpan<object> R<object>.F"
                  IL_006f:  call       "void CollectionExtensions.Report<object>(in System.ReadOnlySpan<object>)"
                  IL_0074:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_FieldInitializer_01()
        {
            string source = """
                using System;
                ref struct R
                {
                    public ReadOnlySpan<object> F = [1, 2, 3];
                    public R() { }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (4,37): error CS9203: A collection expression of type 'ReadOnlySpan<object>' cannot be used in this context because it may be exposed outside of the current scope.
                //     public ReadOnlySpan<object> F = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[1, 2, 3]").WithArguments("System.ReadOnlySpan<object>").WithLocation(4, 37));
        }

        [Fact]
        public void SpanAssignment_FieldInitializer_02()
        {
            string source = """
                using System;
                class Program
                {
                    static T[] FromSpan<T>(Span<T> s) => s.ToArray();
                    static int[] F = FromSpan([1, 2, 3]);
                    static void Main()
                    {
                        F.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program..cctor", """
                {
                  // Code size       57 (0x39)
                  .maxstack  2
                  .locals init (<>y__InlineArray3<int> V_0)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "<>y__InlineArray3<int>"
                  IL_0008:  ldloca.s   V_0
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_0010:  ldc.i4.1
                  IL_0011:  stind.i4
                  IL_0012:  ldloca.s   V_0
                  IL_0014:  ldc.i4.1
                  IL_0015:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_001a:  ldc.i4.2
                  IL_001b:  stind.i4
                  IL_001c:  ldloca.s   V_0
                  IL_001e:  ldc.i4.2
                  IL_001f:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_0024:  ldc.i4.3
                  IL_0025:  stind.i4
                  IL_0026:  ldloca.s   V_0
                  IL_0028:  ldc.i4.3
                  IL_0029:  call       "InlineArrayAsSpan<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_002e:  call       "int[] Program.FromSpan<int>(System.Span<int>)"
                  IL_0033:  stsfld     "int[] Program.F"
                  IL_0038:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_FieldInitializer_03()
        {
            string source = """
                using System;
                class C
                {
                    static T[] FromSpan<T>(ReadOnlySpan<T> s) => s.ToArray();
                    public object[] F = FromSpan<object>([1, 2, 3]);
                }
                class Program
                {
                    static void Main()
                    {
                        (new C()).F.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("C..ctor", """
                {
                  // Code size       79 (0x4f)
                  .maxstack  3
                  .locals init (<>y__InlineArray3<object> V_0)
                  IL_0000:  ldarg.0
                  IL_0001:  ldloca.s   V_0
                  IL_0003:  initobj    "<>y__InlineArray3<object>"
                  IL_0009:  ldloca.s   V_0
                  IL_000b:  ldc.i4.0
                  IL_000c:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_0011:  ldc.i4.1
                  IL_0012:  box        "int"
                  IL_0017:  stind.ref
                  IL_0018:  ldloca.s   V_0
                  IL_001a:  ldc.i4.1
                  IL_001b:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_0020:  ldc.i4.2
                  IL_0021:  box        "int"
                  IL_0026:  stind.ref
                  IL_0027:  ldloca.s   V_0
                  IL_0029:  ldc.i4.2
                  IL_002a:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_002f:  ldc.i4.3
                  IL_0030:  box        "int"
                  IL_0035:  stind.ref
                  IL_0036:  ldloca.s   V_0
                  IL_0038:  ldc.i4.3
                  IL_0039:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<object>, object>(in <>y__InlineArray3<object>, int)"
                  IL_003e:  call       "object[] C.FromSpan<object>(System.ReadOnlySpan<object>)"
                  IL_0043:  stfld      "object[] C.F"
                  IL_0048:  ldarg.0
                  IL_0049:  call       "object..ctor()"
                  IL_004e:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_FieldInitializer_04()
        {
            string source = """
                using System;
                struct S
                {
                    static T[] FromSpan<T>(ReadOnlySpan<T> s) => s.ToArray();
                    public object[] F = FromSpan<object>([1, 2, 3]);
                    public S() { }
                }
                class Program
                {
                    static void Main()
                    {
                        (new S()).F.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("S..ctor", """
                {
                  // Code size       73 (0x49)
                  .maxstack  3
                  .locals init (<>y__InlineArray3<object> V_0)
                  IL_0000:  ldarg.0
                  IL_0001:  ldloca.s   V_0
                  IL_0003:  initobj    "<>y__InlineArray3<object>"
                  IL_0009:  ldloca.s   V_0
                  IL_000b:  ldc.i4.0
                  IL_000c:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_0011:  ldc.i4.1
                  IL_0012:  box        "int"
                  IL_0017:  stind.ref
                  IL_0018:  ldloca.s   V_0
                  IL_001a:  ldc.i4.1
                  IL_001b:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_0020:  ldc.i4.2
                  IL_0021:  box        "int"
                  IL_0026:  stind.ref
                  IL_0027:  ldloca.s   V_0
                  IL_0029:  ldc.i4.2
                  IL_002a:  call       "InlineArrayElementRef<<>y__InlineArray3<object>, object>(ref <>y__InlineArray3<object>, int)"
                  IL_002f:  ldc.i4.3
                  IL_0030:  box        "int"
                  IL_0035:  stind.ref
                  IL_0036:  ldloca.s   V_0
                  IL_0038:  ldc.i4.3
                  IL_0039:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<object>, object>(in <>y__InlineArray3<object>, int)"
                  IL_003e:  call       "object[] S.FromSpan<object>(System.ReadOnlySpan<object>)"
                  IL_0043:  stfld      "object[] S.F"
                  IL_0048:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void SpanAssignment_RefLocal([CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class Program
                {
                    static Span<object> F()
                    {
                        Span<object> s = default;
                        ref Span<object> r = ref s;
                        r = new Span<object>(new object[] { 1 });
                        r = [1];
                        return r;
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: targetFramework);
            comp.VerifyEmitDiagnostics(
                // (9,13): error CS9203: A collection expression of type 'Span<object>' cannot be used in this context because it may be exposed outside of the current scope.
                //         r = [1];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[1]").WithArguments("System.Span<object>").WithLocation(9, 13));
        }

        [CombinatorialData]
        [Theory]
        public void SpanAssignment_NestedScope_01([CombinatorialValues(TargetFramework.Net70, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F(false);
                        F(true);
                    }
                    static void F(bool b)
                    {
                        ReadOnlySpan<object> x = [1];
                        if (b)
                        {
                            x = [2];
                        }
                        else
                        {
                            ReadOnlySpan<object> y = [3];
                            x = y;
                        }
                        ReadOnlySpan<object> z = [4];
                        x = z;
                    }
                }
                """;
            var verifier = CompileAndVerify(
                source,
                targetFramework: targetFramework,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput(""));
            if (targetFramework == TargetFramework.Net80)
            {
                verifier.VerifyIL("Program.F", """
                    {
                      // Code size      134 (0x86)
                      .maxstack  2
                      .locals init (<>y__InlineArray1<object> V_0,
                                    <>y__InlineArray1<object> V_1,
                                    <>y__InlineArray1<object> V_2,
                                    <>y__InlineArray1<object> V_3)
                      IL_0000:  ldloca.s   V_0
                      IL_0002:  initobj    "<>y__InlineArray1<object>"
                      IL_0008:  ldloca.s   V_0
                      IL_000a:  ldc.i4.0
                      IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0010:  ldc.i4.1
                      IL_0011:  box        "int"
                      IL_0016:  stind.ref
                      IL_0017:  ldloca.s   V_0
                      IL_0019:  ldc.i4.1
                      IL_001a:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                      IL_001f:  pop
                      IL_0020:  ldarg.0
                      IL_0021:  brfalse.s  IL_0045
                      IL_0023:  ldloca.s   V_1
                      IL_0025:  initobj    "<>y__InlineArray1<object>"
                      IL_002b:  ldloca.s   V_1
                      IL_002d:  ldc.i4.0
                      IL_002e:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0033:  ldc.i4.2
                      IL_0034:  box        "int"
                      IL_0039:  stind.ref
                      IL_003a:  ldloca.s   V_1
                      IL_003c:  ldc.i4.1
                      IL_003d:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                      IL_0042:  pop
                      IL_0043:  br.s       IL_0065
                      IL_0045:  ldloca.s   V_2
                      IL_0047:  initobj    "<>y__InlineArray1<object>"
                      IL_004d:  ldloca.s   V_2
                      IL_004f:  ldc.i4.0
                      IL_0050:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0055:  ldc.i4.3
                      IL_0056:  box        "int"
                      IL_005b:  stind.ref
                      IL_005c:  ldloca.s   V_2
                      IL_005e:  ldc.i4.1
                      IL_005f:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                      IL_0064:  pop
                      IL_0065:  ldloca.s   V_3
                      IL_0067:  initobj    "<>y__InlineArray1<object>"
                      IL_006d:  ldloca.s   V_3
                      IL_006f:  ldc.i4.0
                      IL_0070:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                      IL_0075:  ldc.i4.4
                      IL_0076:  box        "int"
                      IL_007b:  stind.ref
                      IL_007c:  ldloca.s   V_3
                      IL_007e:  ldc.i4.1
                      IL_007f:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                      IL_0084:  pop
                      IL_0085:  ret
                    }
                    """);
            }
        }

        [Fact]
        public void SpanAssignment_NestedScope_02()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        M(true, 1, 2, 3, 4);
                    }
                    static void M<T>(bool b, T x, T y, T z, T w)
                    {
                        scoped Span<T> s = default;
                        if (b)
                        {
                            s = [x, y, z];
                        }
                        if (b)
                        {
                            s = [z, w];
                        }
                        s.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[3, 4], "));
            verifier.VerifyIL("Program.M<T>", """
                {
                  // Code size      127 (0x7f)
                  .maxstack  2
                  .locals init (System.Span<T> V_0, //s
                                <>y__InlineArray3<T> V_1,
                                <>y__InlineArray2<T> V_2)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "System.Span<T>"
                  IL_0008:  ldarg.0
                  IL_0009:  brfalse.s  IL_0046
                  IL_000b:  ldloca.s   V_1
                  IL_000d:  initobj    "<>y__InlineArray3<T>"
                  IL_0013:  ldloca.s   V_1
                  IL_0015:  ldc.i4.0
                  IL_0016:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_001b:  ldarg.1
                  IL_001c:  stobj      "T"
                  IL_0021:  ldloca.s   V_1
                  IL_0023:  ldc.i4.1
                  IL_0024:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0029:  ldarg.2
                  IL_002a:  stobj      "T"
                  IL_002f:  ldloca.s   V_1
                  IL_0031:  ldc.i4.2
                  IL_0032:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0037:  ldarg.3
                  IL_0038:  stobj      "T"
                  IL_003d:  ldloca.s   V_1
                  IL_003f:  ldc.i4.3
                  IL_0040:  call       "InlineArrayAsSpan<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0045:  stloc.0
                  IL_0046:  ldarg.0
                  IL_0047:  brfalse.s  IL_0077
                  IL_0049:  ldloca.s   V_2
                  IL_004b:  initobj    "<>y__InlineArray2<T>"
                  IL_0051:  ldloca.s   V_2
                  IL_0053:  ldc.i4.0
                  IL_0054:  call       "InlineArrayElementRef<<>y__InlineArray2<T>, T>(ref <>y__InlineArray2<T>, int)"
                  IL_0059:  ldarg.3
                  IL_005a:  stobj      "T"
                  IL_005f:  ldloca.s   V_2
                  IL_0061:  ldc.i4.1
                  IL_0062:  call       "InlineArrayElementRef<<>y__InlineArray2<T>, T>(ref <>y__InlineArray2<T>, int)"
                  IL_0067:  ldarg.s    V_4
                  IL_0069:  stobj      "T"
                  IL_006e:  ldloca.s   V_2
                  IL_0070:  ldc.i4.2
                  IL_0071:  call       "InlineArrayAsSpan<<>y__InlineArray2<T>, T>(ref <>y__InlineArray2<T>, int)"
                  IL_0076:  stloc.0
                  IL_0077:  ldloca.s   V_0
                  IL_0079:  call       "void CollectionExtensions.Report<T>(in System.Span<T>)"
                  IL_007e:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_NestedScope_03()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        M<object>(true, [1, null, 3]);
                    }
                    static void M<T>(bool b, T[] a)
                    {
                        scoped Span<T> s = default;
                        if (b)
                        {
                            s = [..a];
                        }
                        s.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, null, 3], "));
            verifier.VerifyIL("Program.M<T>", """
                {
                  // Code size       81 (0x51)
                  .maxstack  3
                  .locals init (System.Span<T> V_0, //s
                                int V_1,
                                T[] V_2,
                                T[] V_3,
                                int V_4,
                                T V_5)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "System.Span<T>"
                  IL_0008:  ldarg.0
                  IL_0009:  brfalse.s  IL_0049
                  IL_000b:  ldarg.1
                  IL_000c:  ldc.i4.0
                  IL_000d:  stloc.1
                  IL_000e:  dup
                  IL_000f:  ldlen
                  IL_0010:  conv.i4
                  IL_0011:  newarr     "T"
                  IL_0016:  stloc.2
                  IL_0017:  stloc.3
                  IL_0018:  ldc.i4.0
                  IL_0019:  stloc.s    V_4
                  IL_001b:  br.s       IL_003a
                  IL_001d:  ldloc.3
                  IL_001e:  ldloc.s    V_4
                  IL_0020:  ldelem     "T"
                  IL_0025:  stloc.s    V_5
                  IL_0027:  ldloc.2
                  IL_0028:  ldloc.1
                  IL_0029:  ldloc.s    V_5
                  IL_002b:  stelem     "T"
                  IL_0030:  ldloc.1
                  IL_0031:  ldc.i4.1
                  IL_0032:  add
                  IL_0033:  stloc.1
                  IL_0034:  ldloc.s    V_4
                  IL_0036:  ldc.i4.1
                  IL_0037:  add
                  IL_0038:  stloc.s    V_4
                  IL_003a:  ldloc.s    V_4
                  IL_003c:  ldloc.3
                  IL_003d:  ldlen
                  IL_003e:  conv.i4
                  IL_003f:  blt.s      IL_001d
                  IL_0041:  ldloca.s   V_0
                  IL_0043:  ldloc.2
                  IL_0044:  call       "System.Span<T>..ctor(T[])"
                  IL_0049:  ldloca.s   V_0
                  IL_004b:  call       "void CollectionExtensions.Report<T>(in System.Span<T>)"
                  IL_0050:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_NestedScope_04()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        F<object>()(true, 1, null, 3);
                    }
                    static Action<bool, T, T, T> F<T>()
                    {
                        return (bool b, T x, T y, T z) =>
                            {
                                scoped Span<T> s1 = default;
                                if (b)
                                {
                                    Span<T> s2 = [x, y, z];
                                    s1 = s2;
                                }
                                s1.Report();
                            };
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, null, 3], "));
            verifier.VerifyIL("Program.<>c__1<T>.<F>b__1_0(bool, T, T, T)", """
                {
                  // Code size       79 (0x4f)
                  .maxstack  2
                  .locals init (System.Span<T> V_0, //s1
                                <>y__InlineArray3<T> V_1)
                  IL_0000:  ldloca.s   V_0
                  IL_0002:  initobj    "System.Span<T>"
                  IL_0008:  ldarg.1
                  IL_0009:  brfalse.s  IL_0047
                  IL_000b:  ldloca.s   V_1
                  IL_000d:  initobj    "<>y__InlineArray3<T>"
                  IL_0013:  ldloca.s   V_1
                  IL_0015:  ldc.i4.0
                  IL_0016:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_001b:  ldarg.2
                  IL_001c:  stobj      "T"
                  IL_0021:  ldloca.s   V_1
                  IL_0023:  ldc.i4.1
                  IL_0024:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0029:  ldarg.3
                  IL_002a:  stobj      "T"
                  IL_002f:  ldloca.s   V_1
                  IL_0031:  ldc.i4.2
                  IL_0032:  call       "InlineArrayElementRef<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0037:  ldarg.s    V_4
                  IL_0039:  stobj      "T"
                  IL_003e:  ldloca.s   V_1
                  IL_0040:  ldc.i4.3
                  IL_0041:  call       "InlineArrayAsSpan<<>y__InlineArray3<T>, T>(ref <>y__InlineArray3<T>, int)"
                  IL_0046:  stloc.0
                  IL_0047:  ldloca.s   V_0
                  IL_0049:  call       "void CollectionExtensions.Report<T>(in System.Span<T>)"
                  IL_004e:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_NestedScope_05()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        M<object>(1, 2, 3, 4);
                    }
                    static void M<T>(T x, T y, T z, T w)
                    {
                        scoped Span<T> s1;
                        s1 = [x];
                        s1.Report();
                        Action a1 = () =>
                            {
                                scoped Span<T> s2;
                                s2 = [y];
                                s2.Report();
                                void A2()
                                {
                                    scoped Span<T> s3;
                                    s3 = [z];
                                    s3.Report();
                                }
                                A2();
                                s2 = [w];
                                s2.Report();
                            };
                        a1();
                        s1 = [x];
                        s1.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1], [2], [3], [4], [1], "));
            verifier.VerifyIL("Program.<>c__DisplayClass1_0<T>.<M>g__A2|1()", """
                {
                  // Code size       44 (0x2c)
                  .maxstack  2
                  .locals init (System.Span<T> V_0, //s3
                                <>y__InlineArray1<T> V_1)
                  IL_0000:  ldloca.s   V_1
                  IL_0002:  initobj    "<>y__InlineArray1<T>"
                  IL_0008:  ldloca.s   V_1
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<T>, T>(ref <>y__InlineArray1<T>, int)"
                  IL_0010:  ldarg.0
                  IL_0011:  ldfld      "T Program.<>c__DisplayClass1_0<T>.z"
                  IL_0016:  stobj      "T"
                  IL_001b:  ldloca.s   V_1
                  IL_001d:  ldc.i4.1
                  IL_001e:  call       "InlineArrayAsSpan<<>y__InlineArray1<T>, T>(ref <>y__InlineArray1<T>, int)"
                  IL_0023:  stloc.0
                  IL_0024:  ldloca.s   V_0
                  IL_0026:  call       "void CollectionExtensions.Report<T>(in System.Span<T>)"
                  IL_002b:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_NestedScope_06()
        {
            string source = """
                using System;
                class C<T>
                {
                    public Action<T, T> F = (T x, T y) =>
                        {
                            scoped ReadOnlySpan<T> r1;
                            Action<T> a = (T z) =>
                                {
                                    scoped ReadOnlySpan<T> r2;
                                    r2 = [z];
                                    r2.Report();
                                };
                            a(y);
                            r1 = [x];
                            r1.Report();
                        };
                }
                class Program
                {
                    static void Main()
                    {
                        var c = new C<string>();
                        c.F("a", "b");
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[b], [a], "));
            verifier.VerifyIL("C<T>.<>c.<.ctor>b__1_0(T, T)", """
                {
                  // Code size       76 (0x4c)
                  .maxstack  2
                  .locals init (System.ReadOnlySpan<T> V_0, //r1
                                <>y__InlineArray1<T> V_1)
                  IL_0000:  ldsfld     "System.Action<T> C<T>.<>c.<>9__1_1"
                  IL_0005:  dup
                  IL_0006:  brtrue.s   IL_001f
                  IL_0008:  pop
                  IL_0009:  ldsfld     "C<T>.<>c C<T>.<>c.<>9"
                  IL_000e:  ldftn      "void C<T>.<>c.<.ctor>b__1_1(T)"
                  IL_0014:  newobj     "System.Action<T>..ctor(object, nint)"
                  IL_0019:  dup
                  IL_001a:  stsfld     "System.Action<T> C<T>.<>c.<>9__1_1"
                  IL_001f:  ldarg.2
                  IL_0020:  callvirt   "void System.Action<T>.Invoke(T)"
                  IL_0025:  ldloca.s   V_1
                  IL_0027:  initobj    "<>y__InlineArray1<T>"
                  IL_002d:  ldloca.s   V_1
                  IL_002f:  ldc.i4.0
                  IL_0030:  call       "InlineArrayElementRef<<>y__InlineArray1<T>, T>(ref <>y__InlineArray1<T>, int)"
                  IL_0035:  ldarg.1
                  IL_0036:  stobj      "T"
                  IL_003b:  ldloca.s   V_1
                  IL_003d:  ldc.i4.1
                  IL_003e:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<T>, T>(in <>y__InlineArray1<T>, int)"
                  IL_0043:  stloc.0
                  IL_0044:  ldloca.s   V_0
                  IL_0046:  call       "void CollectionExtensions.Report<T>(in System.ReadOnlySpan<T>)"
                  IL_004b:  ret
                }
                """);
        }

        [Fact]
        public void SpanAssignment_WithUsingDeclaration()
        {
            string source = """
                using System;
                class Disposable : IDisposable
                {
                    void IDisposable.Dispose() { Console.Write("Disposed, "); }
                }
                class Program
                {
                    static void Main()
                    {
                        ReadOnlySpan<object> x = [1];
                        using var d = new Disposable();
                        ReadOnlySpan<object> y = [2];
                        x.Report();
                        y.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[1], [2], Disposed, "));
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size       97 (0x61)
                  .maxstack  2
                  .locals init (System.ReadOnlySpan<object> V_0, //x
                                Disposable V_1, //d
                                System.ReadOnlySpan<object> V_2, //y
                                <>y__InlineArray1<object> V_3,
                                <>y__InlineArray1<object> V_4)
                  IL_0000:  ldloca.s   V_3
                  IL_0002:  initobj    "<>y__InlineArray1<object>"
                  IL_0008:  ldloca.s   V_3
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                  IL_0010:  ldc.i4.1
                  IL_0011:  box        "int"
                  IL_0016:  stind.ref
                  IL_0017:  ldloca.s   V_3
                  IL_0019:  ldc.i4.1
                  IL_001a:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                  IL_001f:  stloc.0
                  IL_0020:  newobj     "Disposable..ctor()"
                  IL_0025:  stloc.1
                  .try
                  {
                    IL_0026:  ldloca.s   V_4
                    IL_0028:  initobj    "<>y__InlineArray1<object>"
                    IL_002e:  ldloca.s   V_4
                    IL_0030:  ldc.i4.0
                    IL_0031:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0036:  ldc.i4.2
                    IL_0037:  box        "int"
                    IL_003c:  stind.ref
                    IL_003d:  ldloca.s   V_4
                    IL_003f:  ldc.i4.1
                    IL_0040:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                    IL_0045:  stloc.2
                    IL_0046:  ldloca.s   V_0
                    IL_0048:  call       "void CollectionExtensions.Report<object>(in System.ReadOnlySpan<object>)"
                    IL_004d:  ldloca.s   V_2
                    IL_004f:  call       "void CollectionExtensions.Report<object>(in System.ReadOnlySpan<object>)"
                    IL_0054:  leave.s    IL_0060
                  }
                  finally
                  {
                    IL_0056:  ldloc.1
                    IL_0057:  brfalse.s  IL_005f
                    IL_0059:  ldloc.1
                    IL_005a:  callvirt   "void System.IDisposable.Dispose()"
                    IL_005f:  endfinally
                  }
                  IL_0060:  ret
                }
                """);
        }

        [Fact]
        public void SpanUpdate()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        Span<object> x = [1, 2];
                        x[0] = null;
                        Span<int> y = [3, 4];
                        y[1] = 5;
                        x.Report();
                        y.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[null, 2], [3, 5], "));
            verifier.VerifyIL("Program.Main", """
                {
                    // Code size      119 (0x77)
                    .maxstack  2
                    .locals init (System.Span<object> V_0, //x
                                System.Span<int> V_1, //y
                                <>y__InlineArray2<object> V_2,
                                <>y__InlineArray2<int> V_3)
                    IL_0000:  ldloca.s   V_2
                    IL_0002:  initobj    "<>y__InlineArray2<object>"
                    IL_0008:  ldloca.s   V_2
                    IL_000a:  ldc.i4.0
                    IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray2<object>, object>(ref <>y__InlineArray2<object>, int)"
                    IL_0010:  ldc.i4.1
                    IL_0011:  box        "int"
                    IL_0016:  stind.ref
                    IL_0017:  ldloca.s   V_2
                    IL_0019:  ldc.i4.1
                    IL_001a:  call       "InlineArrayElementRef<<>y__InlineArray2<object>, object>(ref <>y__InlineArray2<object>, int)"
                    IL_001f:  ldc.i4.2
                    IL_0020:  box        "int"
                    IL_0025:  stind.ref
                    IL_0026:  ldloca.s   V_2
                    IL_0028:  ldc.i4.2
                    IL_0029:  call       "InlineArrayAsSpan<<>y__InlineArray2<object>, object>(ref <>y__InlineArray2<object>, int)"
                    IL_002e:  stloc.0
                    IL_002f:  ldloca.s   V_0
                    IL_0031:  ldc.i4.0
                    IL_0032:  call       "ref object System.Span<object>.this[int].get"
                    IL_0037:  ldnull
                    IL_0038:  stind.ref
                    IL_0039:  ldloca.s   V_3
                    IL_003b:  initobj    "<>y__InlineArray2<int>"
                    IL_0041:  ldloca.s   V_3
                    IL_0043:  ldc.i4.0
                    IL_0044:  call       "InlineArrayElementRef<<>y__InlineArray2<int>, int>(ref <>y__InlineArray2<int>, int)"
                    IL_0049:  ldc.i4.3
                    IL_004a:  stind.i4
                    IL_004b:  ldloca.s   V_3
                    IL_004d:  ldc.i4.1
                    IL_004e:  call       "InlineArrayElementRef<<>y__InlineArray2<int>, int>(ref <>y__InlineArray2<int>, int)"
                    IL_0053:  ldc.i4.4
                    IL_0054:  stind.i4
                    IL_0055:  ldloca.s   V_3
                    IL_0057:  ldc.i4.2
                    IL_0058:  call       "InlineArrayAsSpan<<>y__InlineArray2<int>, int>(ref <>y__InlineArray2<int>, int)"
                    IL_005d:  stloc.1
                    IL_005e:  ldloca.s   V_1
                    IL_0060:  ldc.i4.1
                    IL_0061:  call       "ref int System.Span<int>.this[int].get"
                    IL_0066:  ldc.i4.5
                    IL_0067:  stind.i4
                    IL_0068:  ldloca.s   V_0
                    IL_006a:  call       "void CollectionExtensions.Report<object>(in System.Span<object>)"
                    IL_006f:  ldloca.s   V_1
                    IL_0071:  call       "void CollectionExtensions.Report<int>(in System.Span<int>)"
                    IL_0076:  ret
                }
                """);
        }

        [Fact]
        public void TopLevelStatement_01()
        {
            string source = """
                using System;
                Span<int?> x = [1, null];
                ReadOnlySpan<object> y = [..x, 3];
                y.Report();
                return y.Length;
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("[1, null, 3], "));
            verifier.VerifyIL("<top-level-statements-entry-point>", """
                {
                  // Code size      155 (0x9b)
                  .maxstack  3
                  .locals init (System.ReadOnlySpan<object> V_0, //y
                                <>y__InlineArray2<int?> V_1,
                                System.Span<int?> V_2,
                                int V_3,
                                object[] V_4,
                                System.Span<int?>.Enumerator V_5,
                                int? V_6)
                  IL_0000:  ldloca.s   V_1
                  IL_0002:  initobj    "<>y__InlineArray2<int?>"
                  IL_0008:  ldloca.s   V_1
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray2<int?>, int?>(ref <>y__InlineArray2<int?>, int)"
                  IL_0010:  ldc.i4.1
                  IL_0011:  newobj     "int?..ctor(int)"
                  IL_0016:  stobj      "int?"
                  IL_001b:  ldloca.s   V_1
                  IL_001d:  ldc.i4.1
                  IL_001e:  call       "InlineArrayElementRef<<>y__InlineArray2<int?>, int?>(ref <>y__InlineArray2<int?>, int)"
                  IL_0023:  initobj    "int?"
                  IL_0029:  ldloca.s   V_1
                  IL_002b:  ldc.i4.2
                  IL_002c:  call       "InlineArrayAsSpan<<>y__InlineArray2<int?>, int?>(ref <>y__InlineArray2<int?>, int)"
                  IL_0031:  stloc.2
                  IL_0032:  ldc.i4.0
                  IL_0033:  stloc.3
                  IL_0034:  ldc.i4.1
                  IL_0035:  ldloca.s   V_2
                  IL_0037:  call       "int System.Span<int?>.Length.get"
                  IL_003c:  add
                  IL_003d:  newarr     "object"
                  IL_0042:  stloc.s    V_4
                  IL_0044:  ldloca.s   V_2
                  IL_0046:  call       "System.Span<int?>.Enumerator System.Span<int?>.GetEnumerator()"
                  IL_004b:  stloc.s    V_5
                  IL_004d:  br.s       IL_006c
                  IL_004f:  ldloca.s   V_5
                  IL_0051:  call       "ref int? System.Span<int?>.Enumerator.Current.get"
                  IL_0056:  ldobj      "int?"
                  IL_005b:  stloc.s    V_6
                  IL_005d:  ldloc.s    V_4
                  IL_005f:  ldloc.3
                  IL_0060:  ldloc.s    V_6
                  IL_0062:  box        "int?"
                  IL_0067:  stelem.ref
                  IL_0068:  ldloc.3
                  IL_0069:  ldc.i4.1
                  IL_006a:  add
                  IL_006b:  stloc.3
                  IL_006c:  ldloca.s   V_5
                  IL_006e:  call       "bool System.Span<int?>.Enumerator.MoveNext()"
                  IL_0073:  brtrue.s   IL_004f
                  IL_0075:  ldloc.s    V_4
                  IL_0077:  ldloc.3
                  IL_0078:  ldc.i4.3
                  IL_0079:  box        "int"
                  IL_007e:  stelem.ref
                  IL_007f:  ldloc.3
                  IL_0080:  ldc.i4.1
                  IL_0081:  add
                  IL_0082:  stloc.3
                  IL_0083:  ldloca.s   V_0
                  IL_0085:  ldloc.s    V_4
                  IL_0087:  call       "System.ReadOnlySpan<object>..ctor(object[])"
                  IL_008c:  ldloca.s   V_0
                  IL_008e:  call       "void CollectionExtensions.Report<object>(in System.ReadOnlySpan<object>)"
                  IL_0093:  ldloca.s   V_0
                  IL_0095:  call       "int System.ReadOnlySpan<object>.Length.get"
                  IL_009a:  ret
                }
                """);
        }

        [Fact]
        public void TopLevelStatement_02()
        {
            string source = """
                using System;

                S.F = [..S.GetSpan(), 3];

                struct S
                {
                    public static Span<int?> GetSpan() => (int?[])[1, null];
                    public static ReadOnlySpan<object> F;
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.VerifyEmitDiagnostics(
                // (3,7): error CS9203: A collection expression of type 'ReadOnlySpan<object>' cannot be used in this context because it may be exposed outside of the current scope.
                // S.F = [..S.GetSpan(), 3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[..S.GetSpan(), 3]").WithArguments("System.ReadOnlySpan<object>").WithLocation(3, 7),
                // (8,19): error CS8345: Field or auto-implemented property cannot be of type 'ReadOnlySpan<object>' unless it is an instance member of a ref struct.
                //     public static ReadOnlySpan<object> F;
                Diagnostic(ErrorCode.ERR_FieldAutoPropCantBeByRefLike, "ReadOnlySpan<object>").WithArguments("System.ReadOnlySpan<object>").WithLocation(8, 19));
        }

        [Fact]
        public void RuntimeHelpers_CreateSpan_Primitives()
        {
            string source = """
                using System;
                class  Program
                {
                    static void Main()
                    {
                        Report<bool>([true]);
                        Report<sbyte>([1]);
                        Report<byte>([2]);
                        Report<short>([3]);
                        Report<ushort>([4]);
                        Report<char>(['5']);
                        Report<int>([6]);
                        Report<uint>([7]);
                        Report<long>([8]);
                        Report<ulong>([9]);
                        Report<float>([10]);
                        Report<double>([11]);
                    }
                    static void Report<T>(ReadOnlySpan<T> s)
                    {
                        s.ToArray().Report(includeType: true);
                        Console.WriteLine();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("""
                    (System.Boolean[]) [True], 
                    (System.SByte[]) [1], 
                    (System.Byte[]) [2], 
                    (System.Int16[]) [3], 
                    (System.UInt16[]) [4], 
                    (System.Char[]) [5], 
                    (System.Int32[]) [6], 
                    (System.UInt32[]) [7], 
                    (System.Int64[]) [8], 
                    (System.UInt64[]) [9], 
                    (System.Single[]) [10], 
                    (System.Double[]) [11], 
                    """));

            verifier.VerifyIL("Program.Main", """
                {
                  // Code size      184 (0xb8)
                  .maxstack  2
                  IL_0000:  ldsflda    "byte <PrivateImplementationDetails>.4BF5122F344554C53BDE2EBB8CD2B7E3D1600AD631C385A5D7CCE23C7785459A"
                  IL_0005:  ldc.i4.1
                  IL_0006:  newobj     "System.ReadOnlySpan<bool>..ctor(void*, int)"
                  IL_000b:  call       "void Program.Report<bool>(System.ReadOnlySpan<bool>)"
                  IL_0010:  ldsflda    "byte <PrivateImplementationDetails>.4BF5122F344554C53BDE2EBB8CD2B7E3D1600AD631C385A5D7CCE23C7785459A"
                  IL_0015:  ldc.i4.1
                  IL_0016:  newobj     "System.ReadOnlySpan<sbyte>..ctor(void*, int)"
                  IL_001b:  call       "void Program.Report<sbyte>(System.ReadOnlySpan<sbyte>)"
                  IL_0020:  ldsflda    "byte <PrivateImplementationDetails>.DBC1B4C900FFE48D575B5DA5C638040125F65DB0FE3E24494B76EA986457D986"
                  IL_0025:  ldc.i4.1
                  IL_0026:  newobj     "System.ReadOnlySpan<byte>..ctor(void*, int)"
                  IL_002b:  call       "void Program.Report<byte>(System.ReadOnlySpan<byte>)"
                  IL_0030:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=2_Align=2 <PrivateImplementationDetails>.9B4FB24EDD6D1D8830E272398263CDBF026B97392CC35387B991DC0248A628F92"
                  IL_0035:  call       "System.ReadOnlySpan<short> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<short>(System.RuntimeFieldHandle)"
                  IL_003a:  call       "void Program.Report<short>(System.ReadOnlySpan<short>)"
                  IL_003f:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=2_Align=2 <PrivateImplementationDetails>.C0BA8A33AC67F44ABFF5984DFBB6F56C46B880AC2B86E1F23E7FA9C402C53AE72"
                  IL_0044:  call       "System.ReadOnlySpan<ushort> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<ushort>(System.RuntimeFieldHandle)"
                  IL_0049:  call       "void Program.Report<ushort>(System.ReadOnlySpan<ushort>)"
                  IL_004e:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=2_Align=2 <PrivateImplementationDetails>.166F829E016F2315A8099E3A8D2DBEC6D91572379FF02C760BA4E0335789D47F2"
                  IL_0053:  call       "System.ReadOnlySpan<char> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<char>(System.RuntimeFieldHandle)"
                  IL_0058:  call       "void Program.Report<char>(System.ReadOnlySpan<char>)"
                  IL_005d:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.7AA8CA4A02506DA9133D8F889678B76F716CE45D02E22FDB7B70A15E56A0EFF84"
                  IL_0062:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
                  IL_0067:  call       "void Program.Report<int>(System.ReadOnlySpan<int>)"
                  IL_006c:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.E8613F5A5BC9F9FEEDA32A8E7C80B69DD4878E47B6A91723FB15EB84236B6A2B4"
                  IL_0071:  call       "System.ReadOnlySpan<uint> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<uint>(System.RuntimeFieldHandle)"
                  IL_0076:  call       "void Program.Report<uint>(System.ReadOnlySpan<uint>)"
                  IL_007b:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=8_Align=8 <PrivateImplementationDetails>.6CC16ABD70EEFB90DC0BA0D14FB088630873B2C6AD943F7442356735984C35A38"
                  IL_0080:  call       "System.ReadOnlySpan<long> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<long>(System.RuntimeFieldHandle)"
                  IL_0085:  call       "void Program.Report<long>(System.ReadOnlySpan<long>)"
                  IL_008a:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=8_Align=8 <PrivateImplementationDetails>.CBBD5F990C53684D7AE650B40FCB5656E02261B53DA5F6A7D8C819C92F2828F88"
                  IL_008f:  call       "System.ReadOnlySpan<ulong> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<ulong>(System.RuntimeFieldHandle)"
                  IL_0094:  call       "void Program.Report<ulong>(System.ReadOnlySpan<ulong>)"
                  IL_0099:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.80C8A717CCD70C8809EB78E6A9591C003E11C721FE0CCAF62FD592ABDA1A55934"
                  IL_009e:  call       "System.ReadOnlySpan<float> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<float>(System.RuntimeFieldHandle)"
                  IL_00a3:  call       "void Program.Report<float>(System.ReadOnlySpan<float>)"
                  IL_00a8:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=8_Align=8 <PrivateImplementationDetails>.9EE2B49423E1506EC86B25B2FEBB317DA93338F594CDCDCD1B38E3A726706DE08"
                  IL_00ad:  call       "System.ReadOnlySpan<double> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<double>(System.RuntimeFieldHandle)"
                  IL_00b2:  call       "void Program.Report<double>(System.ReadOnlySpan<double>)"
                  IL_00b7:  ret
                }
                """);
        }

        [Fact]
        public void RuntimeHelpers_CreateSpan_NotPrimitives()
        {
            string source = """
                using System;
                class  Program
                {
                    static void Main()
                    {
                        Report<object>(["1"]);
                        Report<string>(["2"]);
                        Report<nint>([3]);
                        Report<nuint>([4]);
                    }
                    static void Report<T>(ReadOnlySpan<T> s)
                    {
                        s.ToArray().Report(includeType: true);
                        Console.WriteLine();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Skipped,
                expectedOutput: IncludeExpectedOutput("""
                    (System.Object[]) [1], 
                    (System.String[]) [2], 
                    (System.IntPtr[]) [3], 
                    (System.UIntPtr[]) [4], 
                    """));
            verifier.VerifyIL("Program.Main", """
                {
                    // Code size      135 (0x87)
                    .maxstack  2
                    .locals init (<>y__InlineArray1<object> V_0,
                                <>y__InlineArray1<string> V_1,
                                <>y__InlineArray1<nint> V_2,
                                <>y__InlineArray1<nuint> V_3)
                    IL_0000:  ldloca.s   V_0
                    IL_0002:  initobj    "<>y__InlineArray1<object>"
                    IL_0008:  ldloca.s   V_0
                    IL_000a:  ldc.i4.0
                    IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray1<object>, object>(ref <>y__InlineArray1<object>, int)"
                    IL_0010:  ldstr      "1"
                    IL_0015:  stind.ref
                    IL_0016:  ldloca.s   V_0
                    IL_0018:  ldc.i4.1
                    IL_0019:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<object>, object>(in <>y__InlineArray1<object>, int)"
                    IL_001e:  call       "void Program.Report<object>(System.ReadOnlySpan<object>)"
                    IL_0023:  ldloca.s   V_1
                    IL_0025:  initobj    "<>y__InlineArray1<string>"
                    IL_002b:  ldloca.s   V_1
                    IL_002d:  ldc.i4.0
                    IL_002e:  call       "InlineArrayElementRef<<>y__InlineArray1<string>, string>(ref <>y__InlineArray1<string>, int)"
                    IL_0033:  ldstr      "2"
                    IL_0038:  stind.ref
                    IL_0039:  ldloca.s   V_1
                    IL_003b:  ldc.i4.1
                    IL_003c:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<string>, string>(in <>y__InlineArray1<string>, int)"
                    IL_0041:  call       "void Program.Report<string>(System.ReadOnlySpan<string>)"
                    IL_0046:  ldloca.s   V_2
                    IL_0048:  initobj    "<>y__InlineArray1<nint>"
                    IL_004e:  ldloca.s   V_2
                    IL_0050:  ldc.i4.0
                    IL_0051:  call       "InlineArrayElementRef<<>y__InlineArray1<nint>, nint>(ref <>y__InlineArray1<nint>, int)"
                    IL_0056:  ldc.i4.3
                    IL_0057:  conv.i
                    IL_0058:  stind.i
                    IL_0059:  ldloca.s   V_2
                    IL_005b:  ldc.i4.1
                    IL_005c:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<nint>, nint>(in <>y__InlineArray1<nint>, int)"
                    IL_0061:  call       "void Program.Report<nint>(System.ReadOnlySpan<nint>)"
                    IL_0066:  ldloca.s   V_3
                    IL_0068:  initobj    "<>y__InlineArray1<nuint>"
                    IL_006e:  ldloca.s   V_3
                    IL_0070:  ldc.i4.0
                    IL_0071:  call       "InlineArrayElementRef<<>y__InlineArray1<nuint>, nuint>(ref <>y__InlineArray1<nuint>, int)"
                    IL_0076:  ldc.i4.4
                    IL_0077:  conv.i
                    IL_0078:  stind.i
                    IL_0079:  ldloca.s   V_3
                    IL_007b:  ldc.i4.1
                    IL_007c:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray1<nuint>, nuint>(in <>y__InlineArray1<nuint>, int)"
                    IL_0081:  call       "void Program.Report<nuint>(System.ReadOnlySpan<nuint>)"
                    IL_0086:  ret
                }
                """);
        }

        [Fact]
        public void RuntimeHelpers_CreateSpan_Enums()
        {
            string source = """
                using System;
                enum E_sbyte : sbyte { A = 1 }
                enum E_byte : byte { B = 2 }
                enum E_short : short { C = 3 }
                enum E_ushort : ushort { D = 4 }
                enum E_int : int { E = 5 }
                enum E_uint : uint { F = 6 }
                enum E_long : long { G = 7 }
                enum E_ulong : ulong { H = 8 }
                class  Program
                {
                    static void Main()
                    {
                        Report([E_sbyte.A]);
                        Report([E_byte.B]);
                        Report([E_short.C]);
                        Report([E_ushort.D]);
                        Report([E_int.E]);
                        Report([E_uint.F]);
                        Report([E_long.G]);
                        Report([E_ulong.H]);
                    }
                    static void Report<T>(ReadOnlySpan<T> s)
                    {
                        s.ToArray().Report(includeType: true);
                        Console.WriteLine();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensions },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("""
                    (E_sbyte[]) [A], 
                    (E_byte[]) [B], 
                    (E_short[]) [C], 
                    (E_ushort[]) [D], 
                    (E_int[]) [E], 
                    (E_uint[]) [F], 
                    (E_long[]) [G], 
                    (E_ulong[]) [H], 
                    """));

            verifier.VerifyIL("Program.Main", """
                {
                  // Code size      123 (0x7b)
                  .maxstack  2
                  IL_0000:  ldsflda    "byte <PrivateImplementationDetails>.4BF5122F344554C53BDE2EBB8CD2B7E3D1600AD631C385A5D7CCE23C7785459A"
                  IL_0005:  ldc.i4.1
                  IL_0006:  newobj     "System.ReadOnlySpan<E_sbyte>..ctor(void*, int)"
                  IL_000b:  call       "void Program.Report<E_sbyte>(System.ReadOnlySpan<E_sbyte>)"
                  IL_0010:  ldsflda    "byte <PrivateImplementationDetails>.DBC1B4C900FFE48D575B5DA5C638040125F65DB0FE3E24494B76EA986457D986"
                  IL_0015:  ldc.i4.1
                  IL_0016:  newobj     "System.ReadOnlySpan<E_byte>..ctor(void*, int)"
                  IL_001b:  call       "void Program.Report<E_byte>(System.ReadOnlySpan<E_byte>)"
                  IL_0020:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=2_Align=2 <PrivateImplementationDetails>.9B4FB24EDD6D1D8830E272398263CDBF026B97392CC35387B991DC0248A628F92"
                  IL_0025:  call       "System.ReadOnlySpan<E_short> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<E_short>(System.RuntimeFieldHandle)"
                  IL_002a:  call       "void Program.Report<E_short>(System.ReadOnlySpan<E_short>)"
                  IL_002f:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=2_Align=2 <PrivateImplementationDetails>.C0BA8A33AC67F44ABFF5984DFBB6F56C46B880AC2B86E1F23E7FA9C402C53AE72"
                  IL_0034:  call       "System.ReadOnlySpan<E_ushort> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<E_ushort>(System.RuntimeFieldHandle)"
                  IL_0039:  call       "void Program.Report<E_ushort>(System.ReadOnlySpan<E_ushort>)"
                  IL_003e:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.2594B6A92EBFB1C3312DEB7D01C015FB95E9FBE9BD7BC6B527AF07813EC7B9104"
                  IL_0043:  call       "System.ReadOnlySpan<E_int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<E_int>(System.RuntimeFieldHandle)"
                  IL_0048:  call       "void Program.Report<E_int>(System.ReadOnlySpan<E_int>)"
                  IL_004d:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=4_Align=4 <PrivateImplementationDetails>.7AA8CA4A02506DA9133D8F889678B76F716CE45D02E22FDB7B70A15E56A0EFF84"
                  IL_0052:  call       "System.ReadOnlySpan<E_uint> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<E_uint>(System.RuntimeFieldHandle)"
                  IL_0057:  call       "void Program.Report<E_uint>(System.ReadOnlySpan<E_uint>)"
                  IL_005c:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=8_Align=8 <PrivateImplementationDetails>.AAE89FC0F03E2959AE4D701A80CC3915918C950B159F6ABB6C92C1433B1A85348"
                  IL_0061:  call       "System.ReadOnlySpan<E_long> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<E_long>(System.RuntimeFieldHandle)"
                  IL_0066:  call       "void Program.Report<E_long>(System.ReadOnlySpan<E_long>)"
                  IL_006b:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=8_Align=8 <PrivateImplementationDetails>.6CC16ABD70EEFB90DC0BA0D14FB088630873B2C6AD943F7442356735984C35A38"
                  IL_0070:  call       "System.ReadOnlySpan<E_ulong> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<E_ulong>(System.RuntimeFieldHandle)"
                  IL_0075:  call       "void Program.Report<E_ulong>(System.ReadOnlySpan<E_ulong>)"
                  IL_007a:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void RuntimeHelpers_CreateSpan([CombinatorialValues(TargetFramework.Net60, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class  Program
                {
                    static void Main()
                    {
                        F1().Report();
                        F2().Report();
                    }
                    static ReadOnlySpan<int> F1() => new[] { 1, 2, 3 };
                    static ReadOnlySpan<int> F2() => [1, 2, 3];
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: targetFramework,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], [1, 2, 3], "));

            string expectedIL = targetFramework == TargetFramework.Net60 ?
                """
                {
                  // Code size       38 (0x26)
                  .maxstack  3
                  IL_0000:  ldsfld     "int[] <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D_A6"
                  IL_0005:  dup
                  IL_0006:  brtrue.s   IL_0020
                  IL_0008:  pop
                  IL_0009:  ldc.i4.3
                  IL_000a:  newarr     "int"
                  IL_000f:  dup
                  IL_0010:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D"
                  IL_0015:  call       "void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)"
                  IL_001a:  dup
                  IL_001b:  stsfld     "int[] <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D_A6"
                  IL_0020:  newobj     "System.ReadOnlySpan<int>..ctor(int[])"
                  IL_0025:  ret
                }
                """ :
                """
                {
                  // Code size       11 (0xb)
                  .maxstack  1
                  IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12_Align=4 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D4"
                  IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
                  IL_000a:  ret
                }
                """;
            verifier.VerifyIL("Program.F1", expectedIL);
            verifier.VerifyIL("Program.F2", expectedIL);
        }

        [CombinatorialData]
        [Theory]
        public void RuntimeHelpers_CreateSpan_Byte([CombinatorialValues(TargetFramework.Net60, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class  Program
                {
                    static void Main()
                    {
                        F1().Report();
                        F2().Report();
                    }
                    static ReadOnlySpan<byte> F1()
                    {
                        ReadOnlySpan<byte> s = new byte[] { 1, 2, 3 };
                        return s;
                    }
                    static ReadOnlySpan<byte> F2()
                    {
                        ReadOnlySpan<byte> s = [1, 2, 3];
                        return s;
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: targetFramework,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], [1, 2, 3], "));

            string expectedIL =
                """
                {
                  // Code size       12 (0xc)
                  .maxstack  2
                  IL_0000:  ldsflda    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81"
                  IL_0005:  ldc.i4.3
                  IL_0006:  newobj     "System.ReadOnlySpan<byte>..ctor(void*, int)"
                  IL_000b:  ret
                }
                """;
            verifier.VerifyIL("Program.F1", expectedIL);
            verifier.VerifyIL("Program.F2", expectedIL);
        }

        [CombinatorialData]
        [Theory]
        public void RuntimeHelpers_CreateSpan_NotApplicable_01([CombinatorialValues(TargetFramework.Net60, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string source = """
                using System;
                class  Program
                {
                    static Span<int> NotReadOnlySpan() => [1, 2, 3];
                    static ReadOnlySpan<int> NotConstants(int c) => [1, 2, c];
                }
                """;
            var comp = CreateCompilation(source, targetFramework: targetFramework);
            comp.VerifyEmitDiagnostics(
                // (4,43): error CS9203: A collection expression of type 'Span<int>' cannot be used in this context because it may be exposed outside of the current scope.
                //     static Span<int> NotReadOnlySpan() => [1, 2, 3];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[1, 2, 3]").WithArguments("System.Span<int>").WithLocation(4, 43),
                // (5,53): error CS9203: A collection expression of type 'ReadOnlySpan<int>' cannot be used in this context because it may be exposed outside of the current scope.
                //     static ReadOnlySpan<int> NotConstants(int c) => [1, 2, c];
                Diagnostic(ErrorCode.ERR_CollectionExpressionEscape, "[1, 2, c]").WithArguments("System.ReadOnlySpan<int>").WithLocation(5, 53));
        }

        [Fact]
        public void RuntimeHelpers_CreateSpan_NotApplicable_02()
        {
            string source = """
                using System;
                class  Program
                {
                    static void Main()
                    {
                        NotReadOnlySpan();
                        NotConstants(3);
                    }
                    static void NotReadOnlySpan()
                    {
                        Span<int> s = [1, 2, 3];
                        s.Report();
                    }
                    static void NotConstants(int c)
                    {
                        ReadOnlySpan<int> s =[1, 2, c];
                        s.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { source, s_collectionExtensionsWithSpan },
                targetFramework: TargetFramework.Net80,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], [1, 2, 3], "));
            verifier.VerifyIL("Program.NotReadOnlySpan", """
                {
                  // Code size       55 (0x37)
                  .maxstack  2
                  .locals init (System.Span<int> V_0, //s
                                <>y__InlineArray3<int> V_1)
                  IL_0000:  ldloca.s   V_1
                  IL_0002:  initobj    "<>y__InlineArray3<int>"
                  IL_0008:  ldloca.s   V_1
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_0010:  ldc.i4.1
                  IL_0011:  stind.i4
                  IL_0012:  ldloca.s   V_1
                  IL_0014:  ldc.i4.1
                  IL_0015:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_001a:  ldc.i4.2
                  IL_001b:  stind.i4
                  IL_001c:  ldloca.s   V_1
                  IL_001e:  ldc.i4.2
                  IL_001f:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_0024:  ldc.i4.3
                  IL_0025:  stind.i4
                  IL_0026:  ldloca.s   V_1
                  IL_0028:  ldc.i4.3
                  IL_0029:  call       "InlineArrayAsSpan<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_002e:  stloc.0
                  IL_002f:  ldloca.s   V_0
                  IL_0031:  call       "void CollectionExtensions.Report<int>(in System.Span<int>)"
                  IL_0036:  ret
                }
                """);
            verifier.VerifyIL("Program.NotConstants", """
                {
                  // Code size       55 (0x37)
                  .maxstack  2
                  .locals init (System.ReadOnlySpan<int> V_0, //s
                                <>y__InlineArray3<int> V_1)
                  IL_0000:  ldloca.s   V_1
                  IL_0002:  initobj    "<>y__InlineArray3<int>"
                  IL_0008:  ldloca.s   V_1
                  IL_000a:  ldc.i4.0
                  IL_000b:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_0010:  ldc.i4.1
                  IL_0011:  stind.i4
                  IL_0012:  ldloca.s   V_1
                  IL_0014:  ldc.i4.1
                  IL_0015:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_001a:  ldc.i4.2
                  IL_001b:  stind.i4
                  IL_001c:  ldloca.s   V_1
                  IL_001e:  ldc.i4.2
                  IL_001f:  call       "InlineArrayElementRef<<>y__InlineArray3<int>, int>(ref <>y__InlineArray3<int>, int)"
                  IL_0024:  ldarg.0
                  IL_0025:  stind.i4
                  IL_0026:  ldloca.s   V_1
                  IL_0028:  ldc.i4.3
                  IL_0029:  call       "InlineArrayAsReadOnlySpan<<>y__InlineArray3<int>, int>(in <>y__InlineArray3<int>, int)"
                  IL_002e:  stloc.0
                  IL_002f:  ldloca.s   V_0
                  IL_0031:  call       "void CollectionExtensions.Report<int>(in System.ReadOnlySpan<int>)"
                  IL_0036:  ret
                }
                """);
        }

        [CombinatorialData]
        [Theory]
        public void RuntimeHelpers_CreateSpan_RefStruct([CombinatorialValues(TargetFramework.Net60, TargetFramework.Net80)] TargetFramework targetFramework)
        {
            string sourceA = $$"""
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public ref struct MyCollection<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                        => new MyCollection<T>(new List<T>(items.ToArray()));
                }
                """;
            var comp = CreateCompilation(
                targetFramework == TargetFramework.Net80 ? new[] { sourceA } : new[] { sourceA, CollectionBuilderAttributeDefinition },
                targetFramework: targetFramework);
            comp.VerifyEmitDiagnostics();
            var refA = comp.EmitToImageReference();

            string sourceB = $$"""
                using System.Collections.Generic;
                using System;
                enum E : byte { A = 1, B = 2, C = 3 }
                class  Program
                {
                    static void Main()
                    {
                        MyCollection<byte> x = F1();
                        MyCollection<int> y = F2();
                        MyCollection<E> z = F3();
                        Report(x);
                        Report(y);
                        Report(z);
                    }
                    static MyCollection<byte> F1() => [1, 2, 3];
                    static MyCollection<int> F2() => [1, 2, 3];
                    static MyCollection<E> F3() => [E.A, E.B, E.C];
                    static void Report<T>(MyCollection<T> c)
                    {
                        var list = new List<T>();
                        foreach (var i in c) list.Add(i);
                        list.Report();
                    }
                }
                """;
            var verifier = CompileAndVerify(
                new[] { sourceB, s_collectionExtensions },
                references: new[] { refA },
                targetFramework: targetFramework,
                verify: Verification.Fails,
                expectedOutput: IncludeExpectedOutput("[1, 2, 3], [1, 2, 3], [A, B, C], "));

            verifier.VerifyIL("Program.F1", """
                {
                  // Code size       17 (0x11)
                  .maxstack  2
                  IL_0000:  ldsflda    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81"
                  IL_0005:  ldc.i4.3
                  IL_0006:  newobj     "System.ReadOnlySpan<byte>..ctor(void*, int)"
                  IL_000b:  call       "MyCollection<byte> MyCollectionBuilder.Create<byte>(System.ReadOnlySpan<byte>)"
                  IL_0010:  ret
                }
                """);
            if (targetFramework == TargetFramework.Net60)
            {
                verifier.VerifyIL("Program.F2", """
                    {
                      // Code size       43 (0x2b)
                      .maxstack  3
                      IL_0000:  ldsfld     "int[] <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D_A6"
                      IL_0005:  dup
                      IL_0006:  brtrue.s   IL_0020
                      IL_0008:  pop
                      IL_0009:  ldc.i4.3
                      IL_000a:  newarr     "int"
                      IL_000f:  dup
                      IL_0010:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D"
                      IL_0015:  call       "void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)"
                      IL_001a:  dup
                      IL_001b:  stsfld     "int[] <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D_A6"
                      IL_0020:  newobj     "System.ReadOnlySpan<int>..ctor(int[])"
                      IL_0025:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
                      IL_002a:  ret
                    }
                    """);
            }
            else
            {
                verifier.VerifyIL("Program.F2", """
                    {
                      // Code size       16 (0x10)
                      .maxstack  1
                      IL_0000:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12_Align=4 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D4"
                      IL_0005:  call       "System.ReadOnlySpan<int> System.Runtime.CompilerServices.RuntimeHelpers.CreateSpan<int>(System.RuntimeFieldHandle)"
                      IL_000a:  call       "MyCollection<int> MyCollectionBuilder.Create<int>(System.ReadOnlySpan<int>)"
                      IL_000f:  ret
                    }
                    """);
            }
            verifier.VerifyIL("Program.F3", """
                {
                    // Code size       17 (0x11)
                    .maxstack  2
                    IL_0000:  ldsflda    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=3 <PrivateImplementationDetails>.039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81"
                    IL_0005:  ldc.i4.3
                    IL_0006:  newobj     "System.ReadOnlySpan<E>..ctor(void*, int)"
                    IL_000b:  call       "MyCollection<E> MyCollectionBuilder.Create<E>(System.ReadOnlySpan<E>)"
                    IL_0010:  ret
                }
                """);
        }

        [Fact]
        public void RuntimeHelpers_CreateSpan_MissingCreateSpan()
        {
            string source = """
                using System;
                class  Program
                {
                    static void Main()
                    {
                        ReadOnlySpan<int> s = [1, 2, 3];
                        s.Report();
                    }
                }
                """;
            var comp = CreateCompilation(new[] { source, s_collectionExtensionsWithSpan }, targetFramework: TargetFramework.Net80, options: TestOptions.ReleaseExe);
            comp.MakeMemberMissing(WellKnownMember.System_Runtime_CompilerServices_RuntimeHelpers__CreateSpanRuntimeFieldHandle);

            var verifier = CompileAndVerify(comp, verify: Verification.Fails, expectedOutput: IncludeExpectedOutput("[1, 2, 3], "));
            verifier.VerifyIL("Program.Main", """
                {
                  // Code size       46 (0x2e)
                  .maxstack  3
                  .locals init (System.ReadOnlySpan<int> V_0) //s
                  IL_0000:  ldsfld     "int[] <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D_A6"
                  IL_0005:  dup
                  IL_0006:  brtrue.s   IL_0020
                  IL_0008:  pop
                  IL_0009:  ldc.i4.3
                  IL_000a:  newarr     "int"
                  IL_000f:  dup
                  IL_0010:  ldtoken    "<PrivateImplementationDetails>.__StaticArrayInitTypeSize=12 <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D"
                  IL_0015:  call       "void System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray(System.Array, System.RuntimeFieldHandle)"
                  IL_001a:  dup
                  IL_001b:  stsfld     "int[] <PrivateImplementationDetails>.4636993D3E1DA4E9D6B8F87B79E8F7C6D018580D52661950EABC3845C5897A4D_A6"
                  IL_0020:  newobj     "System.ReadOnlySpan<int>..ctor(int[])"
                  IL_0025:  stloc.0
                  IL_0026:  ldloca.s   V_0
                  IL_0028:  call       "void CollectionExtensions.Report<int>(in System.ReadOnlySpan<int>)"
                  IL_002d:  ret
                }
                """);
        }

        [Fact]
        public void RuntimeHelpers_CreateSpan_MissingConstructor()
        {
            string source = """
                using System;
                class Program
                {
                    static void Main()
                    {
                        ReadOnlySpan<int> s = [1, 2, 3];
                    }
                }
                """;
            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net80);
            comp.MakeMemberMissing(WellKnownMember.System_ReadOnlySpan_T__ctor_Array);
            comp.VerifyEmitDiagnostics(
                // (6,31): error CS0656: Missing compiler required member 'System.ReadOnlySpan`1..ctor'
                //         ReadOnlySpan<int> s = [1, 2, 3];
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "[1, 2, 3]").WithArguments("System.ReadOnlySpan`1", ".ctor").WithLocation(6, 31));
        }

        [Fact]
        public void ExpressionTrees()
        {
            string source = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Linq.Expressions;
                interface I<T> : IEnumerable
                {
                    void Add(T t);
                }
                class Program
                {
                    static Expression<Func<int[]>> Create1()
                    {
                        return () => [];
                    }
                    static Expression<Func<List<object>>> Create2()
                    {
                        return () => [1, 2];
                    }
                    static Expression<Func<T>> Create3<T, U>(U a, U b) where T : I<U>, new()
                    {
                        return () => [a, b];
                    }
                }
                """;
            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics(
                // (13,22): error CS9175: An expression tree may not contain a collection expression.
                //         return () => [];
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsCollectionExpression, "[]").WithLocation(13, 22),
                // (17,22): error CS9175: An expression tree may not contain a collection expression.
                //         return () => [1, 2];
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsCollectionExpression, "[1, 2]").WithLocation(17, 22),
                // (21,22): error CS9175: An expression tree may not contain a collection expression.
                //         return () => [a, b];
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsCollectionExpression, "[a, b]").WithLocation(21, 22));
        }

        [Fact]
        public void IOperation_Array()
        {
            string source = """
                class Program
                {
                    static T[] Create<T>(T a, T b)
                    {
                        return /*<bind>*/[a, b]/*</bind>*/;
                    }
                }
                """;

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();

            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: T[]) (Syntax: '[a, b]')
                Children(2):
                    IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: T) (Syntax: 'a')
                    IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: T) (Syntax: 'b')
                """);

            var tree = comp.SyntaxTrees[0];
            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Create");
            VerifyFlowGraph(comp, method,
                """
                Block[B0] - Entry
                    Statements (0)
                    Next (Regular) Block[B1]
                Block[B1] - Block
                    Predecessors: [B0]
                    Statements (0)
                    Next (Return) Block[B2]
                        IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: T[], IsImplicit) (Syntax: '[a, b]')
                          Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                            (CollectionExpression)
                          Operand:
                            IOperation:  (OperationKind.None, Type: T[]) (Syntax: '[a, b]')
                              Children(2):
                                  IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: T) (Syntax: 'a')
                                  IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: T) (Syntax: 'b')
                Block[B2] - Exit
                    Predecessors: [B1]
                    Statements (0)
                """);
        }

        [Fact]
        public void IOperation_Span()
        {
            string source = """
                using System;
                class Program
                {
                    static void Create<T>(T a, T b)
                    {
                        Span<T> s = /*<bind>*/[a, b]/*</bind>*/;
                    }
                }
                """;

            var comp = CreateCompilation(source, targetFramework: TargetFramework.Net70);
            comp.VerifyEmitDiagnostics();

            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: System.Span<T>) (Syntax: '[a, b]')
                Children(2):
                    IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: T) (Syntax: 'a')
                    IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: T) (Syntax: 'b')
                """);

            var tree = comp.SyntaxTrees[0];
            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Create");
            VerifyFlowGraph(comp, method,
                """
                Block[B0] - Entry
                    Statements (0)
                    Next (Regular) Block[B1]
                        Entering: {R1}
                .locals {R1}
                {
                    Locals: [System.Span<T> s]
                    Block[B1] - Block
                        Predecessors: [B0]
                        Statements (1)
                            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Span<T>, IsImplicit) (Syntax: 's = /*<bind>*/[a, b]')
                              Left:
                                ILocalReferenceOperation: s (IsDeclaration: True) (OperationKind.LocalReference, Type: System.Span<T>, IsImplicit) (Syntax: 's = /*<bind>*/[a, b]')
                              Right:
                                IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Span<T>, IsImplicit) (Syntax: '[a, b]')
                                  Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                                    (CollectionExpression)
                                  Operand:
                                    IOperation:  (OperationKind.None, Type: System.Span<T>) (Syntax: '[a, b]')
                                      Children(2):
                                          IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: T) (Syntax: 'a')
                                          IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: T) (Syntax: 'b')
                        Next (Regular) Block[B2]
                            Leaving: {R1}
                }
                Block[B2] - Exit
                    Predecessors: [B1]
                    Statements (0)
                """);
        }

        [Fact]
        public void IOperation_CollectionInitializer()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                interface I<T> : IEnumerable<T>
                {
                    void Add(T t);
                }
                struct S<T> : I<T>
                {
                    public void Add(T t) { }
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw null;
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                class Program
                {
                    static S<T> Create<T>(T a, T b)
                    {
                        return /*<bind>*/[a, b]/*</bind>*/;
                    }
                }
                """;

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();

            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: S<T>) (Syntax: '[a, b]')
                  Children(2):
                      IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: T) (Syntax: 'a')
                      IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: T) (Syntax: 'b')
                """);

            var tree = comp.SyntaxTrees[0];
            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Create");
            VerifyFlowGraph(comp, method,
                """
                Block[B0] - Entry
                    Statements (0)
                    Next (Regular) Block[B1]
                Block[B1] - Block
                    Predecessors: [B0]
                    Statements (0)
                    Next (Return) Block[B2]
                        IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: S<T>, IsImplicit) (Syntax: '[a, b]')
                          Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                            (CollectionExpression)
                          Operand:
                            IOperation:  (OperationKind.None, Type: S<T>) (Syntax: '[a, b]')
                              Children(2):
                                  IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: T) (Syntax: 'a')
                                  IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: T) (Syntax: 'b')
                Block[B2] - Exit
                    Predecessors: [B1]
                    Statements (0)
                """);
        }

        [Fact]
        public void IOperation_TypeParameter()
        {
            string source = """
                using System.Collections;
                using System.Collections.Generic;
                interface I<T> : IEnumerable<T>
                {
                    void Add(T t);
                }
                struct S<T> : I<T>
                {
                    public void Add(T t) { }
                    IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw null;
                    IEnumerator IEnumerable.GetEnumerator() => throw null;
                }
                class Program
                {
                    static T Create<T, U>(U a, U b) where T : I<U>, new()
                    {
                        return /*<bind>*/[a, b]/*</bind>*/;
                    }
                }
                """;

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();

            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: T) (Syntax: '[a, b]')
                  Children(2):
                      IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: U) (Syntax: 'a')
                      IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: U) (Syntax: 'b')
                """);

            var tree = comp.SyntaxTrees[0];
            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Create");
            VerifyFlowGraph(comp, method,
                """
                Block[B0] - Entry
                    Statements (0)
                    Next (Regular) Block[B1]
                Block[B1] - Block
                    Predecessors: [B0]
                    Statements (0)
                    Next (Return) Block[B2]
                        IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: T, IsImplicit) (Syntax: '[a, b]')
                          Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                            (CollectionExpression)
                          Operand:
                            IOperation:  (OperationKind.None, Type: T) (Syntax: '[a, b]')
                              Children(2):
                                  IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: U) (Syntax: 'a')
                                  IParameterReferenceOperation: b (OperationKind.ParameterReference, Type: U) (Syntax: 'b')
                Block[B2] - Exit
                    Predecessors: [B1]
                    Statements (0)
                """);
        }

        [Fact]
        public void IOperation_Nested()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static void Main()
                    {
                        List<List<int>> x = /*<bind>*/[[Get(1)]]/*</bind>*/;
                    }
                    static int Get(int value) => value;
                }
                """;

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();

            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: System.Collections.Generic.List<System.Collections.Generic.List<System.Int32>>) (Syntax: '[[Get(1)]]')
                  Children(1):
                      IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Collections.Generic.List<System.Int32>, IsImplicit) (Syntax: '[Get(1)]')
                        Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                        Operand:
                          IOperation:  (OperationKind.None, Type: System.Collections.Generic.List<System.Int32>) (Syntax: '[Get(1)]')
                            Children(1):
                                IInvocationOperation (System.Int32 Program.Get(System.Int32 value)) (OperationKind.Invocation, Type: System.Int32) (Syntax: 'Get(1)')
                                  Instance Receiver:
                                    null
                                  Arguments(1):
                                      IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '1')
                                        ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 1) (Syntax: '1')
                                        InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                                        OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                """);

            var tree = comp.SyntaxTrees[0];
            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Main");
            VerifyFlowGraph(comp, method,
                """
                Block[B0] - Entry
                    Statements (0)
                    Next (Regular) Block[B1]
                        Entering: {R1}
                .locals {R1}
                {
                    Locals: [System.Collections.Generic.List<System.Collections.Generic.List<System.Int32>> x]
                    Block[B1] - Block
                        Predecessors: [B0]
                        Statements (1)
                            ISimpleAssignmentOperation (OperationKind.SimpleAssignment, Type: System.Collections.Generic.List<System.Collections.Generic.List<System.Int32>>, IsImplicit) (Syntax: 'x = /*<bind>*/[[Get(1)]]')
                              Left:
                                ILocalReferenceOperation: x (IsDeclaration: True) (OperationKind.LocalReference, Type: System.Collections.Generic.List<System.Collections.Generic.List<System.Int32>>, IsImplicit) (Syntax: 'x = /*<bind>*/[[Get(1)]]')
                              Right:
                                IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Collections.Generic.List<System.Collections.Generic.List<System.Int32>>, IsImplicit) (Syntax: '[[Get(1)]]')
                                  Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                                    (CollectionExpression)
                                  Operand:
                                    IOperation:  (OperationKind.None, Type: System.Collections.Generic.List<System.Collections.Generic.List<System.Int32>>) (Syntax: '[[Get(1)]]')
                                      Children(1):
                                          IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Collections.Generic.List<System.Int32>, IsImplicit) (Syntax: '[Get(1)]')
                                            Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                                              (CollectionExpression)
                                            Operand:
                                              IOperation:  (OperationKind.None, Type: System.Collections.Generic.List<System.Int32>) (Syntax: '[Get(1)]')
                                                Children(1):
                                                    IInvocationOperation (System.Int32 Program.Get(System.Int32 value)) (OperationKind.Invocation, Type: System.Int32) (Syntax: 'Get(1)')
                                                      Instance Receiver:
                                                        null
                                                      Arguments(1):
                                                          IArgumentOperation (ArgumentKind.Explicit, Matching Parameter: value) (OperationKind.Argument, Type: null) (Syntax: '1')
                                                            ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 1) (Syntax: '1')
                                                            InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                                                            OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                        Next (Regular) Block[B2]
                            Leaving: {R1}
                }
                Block[B2] - Exit
                    Predecessors: [B1]
                    Statements (0)
                """);
        }

        [Fact]
        public void IOperation_SpreadElement_01()
        {
            string source = """
                class Program
                {
                    static int[] Append(int[] a)
                    {
                        return /*<bind>*/[..a]/*</bind>*/;
                    }
                }
                """;

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();

            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: System.Int32[]) (Syntax: '[..a]')
                  Children(1):
                      IOperation:  (OperationKind.None, Type: null, IsImplicit) (Syntax: '..a')
                        Children(1):
                            IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: System.Int32[]) (Syntax: 'a')
                """);

            var tree = comp.SyntaxTrees[0];
            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Append");
            VerifyFlowGraph(comp, method,
                """
                Block[B0] - Entry
                    Statements (0)
                    Next (Regular) Block[B1]
                Block[B1] - Block
                    Predecessors: [B0]
                    Statements (0)
                    Next (Return) Block[B2]
                        IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Int32[], IsImplicit) (Syntax: '[..a]')
                          Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                            (CollectionExpression)
                          Operand:
                            IOperation:  (OperationKind.None, Type: System.Int32[]) (Syntax: '[..a]')
                              Children(1):
                                  IOperation:  (OperationKind.None, Type: null, IsImplicit) (Syntax: '..a')
                                    Children(1):
                                        IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: System.Int32[]) (Syntax: 'a')
                Block[B2] - Exit
                    Predecessors: [B1]
                    Statements (0)
                """);
        }

        [Fact]
        public void IOperation_SpreadElement_02()
        {
            string source = """
                using System.Collections.Generic;
                class Program
                {
                    static List<int> Append(int[] a)
                    {
                        return /*<bind>*/[..a]/*</bind>*/;
                    }
                }
                """;

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();

            VerifyOperationTreeForTest<CollectionExpressionSyntax>(comp,
                """
                IOperation:  (OperationKind.None, Type: System.Collections.Generic.List<System.Int32>) (Syntax: '[..a]')
                  Children(1):
                      IOperation:  (OperationKind.None, Type: null, IsImplicit) (Syntax: '..a')
                        Children(1):
                            IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: System.Int32[]) (Syntax: 'a')
                """);

            var tree = comp.SyntaxTrees[0];
            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "Append");
            VerifyFlowGraph(comp, method,
                """
                Block[B0] - Entry
                    Statements (0)
                    Next (Regular) Block[B1]
                Block[B1] - Block
                    Predecessors: [B0]
                    Statements (0)
                    Next (Return) Block[B2]
                        IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: System.Collections.Generic.List<System.Int32>, IsImplicit) (Syntax: '[..a]')
                          Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
                            (CollectionExpression)
                          Operand:
                            IOperation:  (OperationKind.None, Type: System.Collections.Generic.List<System.Int32>) (Syntax: '[..a]')
                              Children(1):
                                  IOperation:  (OperationKind.None, Type: null, IsImplicit) (Syntax: '..a')
                                    Children(1):
                                        IParameterReferenceOperation: a (OperationKind.ParameterReference, Type: System.Int32[]) (Syntax: 'a')
                Block[B2] - Exit
                    Predecessors: [B1]
                    Statements (0)
                """);
        }

        [Fact]
        public void Async_01()
        {
            string source = """
                using System.Collections.Generic;
                using System.Threading.Tasks;
                class Program
                {
                    static async Task Main()
                    {
                        (await CreateArray()).Report();
                        (await CreateList()).Report();
                    }
                    static async Task<int[]> CreateArray()
                    {
                        return [await F(1), await F(2)];
                    }
                    static async Task<List<int>> CreateList()
                    {
                        return [await F(3), await F(4)];
                    }
                    static async Task<int> F(int i)
                    {
                        await Task.Yield();
                        return i;
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[1, 2], [3, 4], ");
        }

        [Fact]
        public void Async_02()
        {
            string source = """
                using System.Collections.Generic;
                using System.Threading.Tasks;
                class Program
                {
                    static async Task Main()
                    {
                        (await F2(F1())).Report();
                    }
                    static async Task<int[]> F1()
                    {
                        return [await F(1), await F(2)];
                    }
                    static async Task<int[]> F2(Task<int[]> e)
                    {
                        return [3, .. await e, 4];
                    }
                    static async Task<T> F<T>(T t)
                    {
                        await Task.Yield();
                        return t;
                    }
                }
                """;
            CompileAndVerify(new[] { source, s_collectionExtensions }, expectedOutput: "[3, 1, 2, 4], ");
        }

        [Fact]
        public void PostfixIncrementDecrement()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main()
                    {
                        []++;
                        []--;
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,9): error CS1059: The operand of an increment or decrement operator must be a variable, property or indexer
                //         []++;
                Diagnostic(ErrorCode.ERR_IncrementLvalueExpected, "[]").WithLocation(7, 9),
                // (8,9): error CS1059: The operand of an increment or decrement operator must be a variable, property or indexer
                //         []--;
                Diagnostic(ErrorCode.ERR_IncrementLvalueExpected, "[]").WithLocation(8, 9));
        }

        [Fact]
        public void PostfixPointerAccess()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main()
                    {
                        var v = []->Count;
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,17): error CS9503: There is no target type for the collection expression.
                //         var v = []->Count;
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 17));
        }

        [Fact]
        public void LeftHandAssignment()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main()
                    {
                        [] = null;
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,9): error CS0131: The left-hand side of an assignment must be a variable, property or indexer
                //         [] = null;
                Diagnostic(ErrorCode.ERR_AssgLvalueExpected, "[]").WithLocation(7, 9));
        }

        [Fact]
        public void BinaryOperator()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main(List<int> list)
                    {
                        [] + list;
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,9): error CS1729: 'string' does not contain a constructor that takes 0 arguments
                //         [] + list;
                Diagnostic(ErrorCode.ERR_BadCtorArgCount, "[]").WithArguments("string", "0").WithLocation(7, 9),
                // (7,9): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                //         [] + list;
                Diagnostic(ErrorCode.ERR_IllegalStatement, "[] + list").WithLocation(7, 9));
        }

        [Fact]
        public void RangeOperator()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main(List<int> list)
                    {
                        []..;
                    }
                }
                """;
            CreateCompilationWithIndexAndRangeAndSpan(source).VerifyEmitDiagnostics(
                // (7,9): error CS9500: Cannot initialize type 'Index' with a collection expression because the type is not constructible.
                //         []..;
                Diagnostic(ErrorCode.ERR_CollectionExpressionTargetTypeNotConstructible, "[]").WithArguments("System.Index").WithLocation(7, 9),
                // (7,9): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                //         []..;
                Diagnostic(ErrorCode.ERR_IllegalStatement, "[]..").WithLocation(7, 9));
        }

        [Fact]
        public void TopLevelSwitchExpression()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main(List<int> list)
                    {
                        [] switch { null => 0 };
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,9): error CS9503: There is no target type for the collection expression.
                //         [] switch
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 9),
                // (7,9): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                //         [] switch
                Diagnostic(ErrorCode.ERR_IllegalStatement, "[] switch { null => 0 }").WithLocation(7, 9));
        }

        [Fact]
        public void TopLevelWithExpression()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main(List<int> list)
                    {
                        [] with { Count = 1, };
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,9): error CS9503: There is no target type for the collection expression.
                //         [] with { Count = 1, };
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 9),
                // (7,9): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                //         [] with { Count = 1, };
                Diagnostic(ErrorCode.ERR_IllegalStatement, "[] with { Count = 1, }").WithLocation(7, 9));
        }

        [Fact]
        public void TopLevelIsExpressions()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main(List<int> list)
                    {
                        [] is object;
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,9): error CS9503: There is no target type for the collection expression.
                //         [] is object;
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 9),
                // (7,9): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                //         [] is object;
                Diagnostic(ErrorCode.ERR_IllegalStatement, "[] is object").WithLocation(7, 9));
        }

        [Fact]
        public void TopLevelAsExpressions()
        {
            string source = """
                using System.Collections.Generic;

                class Program
                {
                    static void Main(List<int> list)
                    {
                        [] as List<int>;
                    }
                }
                """;
            CreateCompilation(source).VerifyEmitDiagnostics(
                // (7,9): error CS9503: There is no target type for the collection expression.
                //         [] as List<int>;
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[]").WithLocation(7, 9),
                // (7,9): error CS0201: Only assignment, call, increment, decrement, await, and new object expressions can be used as a statement
                //         [] as List<int>;
                Diagnostic(ErrorCode.ERR_IllegalStatement, "[] as List<int>").WithLocation(7, 9));
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute()
        {
            string source = """
                var attr = (XAttribute)System.Attribute.GetCustomAttribute(typeof(C), typeof(XAttribute));
                attr._values.Report();

                [X([42, 43, 44])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public int[] _values;
                    public XAttribute(int[] values) { _values = values; }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }).VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[42, 43, 44],");

            var program = comp.GetMember<NamedTypeSymbol>("C");
            var argument = program.GetAttributes().Single().ConstructorArguments.Single();
            var values = argument.Values;
            Assert.Equal(3, values.Length);
            Assert.Equal(42, values[0].Value);
            Assert.Equal(43, values[1].Value);
            Assert.Equal(44, values[2].Value);
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_Named()
        {
            string source = """
                var attr = (XAttribute)System.Attribute.GetCustomAttribute(typeof(C), typeof(XAttribute));
                attr.Values.Report();

                [X(Values = [42, 43, 44])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public int[] Values { get; set; }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }).VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[42, 43, 44],");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_Params()
        {
            string source = """
                var attr = (XAttribute)System.Attribute.GetCustomAttribute(typeof(C), typeof(XAttribute));
                attr._values.Report();

                [X([42, 43, 44])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public int[] _values;
                    public XAttribute(params int[] values) { _values = values; }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }).VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[42, 43, 44],");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_StringConstants()
        {
            string source = """
                var attr = (XAttribute)System.Attribute.GetCustomAttribute(typeof(C), typeof(XAttribute));
                attr._values.Report();

                [X(["hi", null])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public string[] _values;
                    public XAttribute(string[] values) { _values = values; }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }).VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[hi, null],");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_NestedArray()
        {
            string source = """
                [X([[1], [2]])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(int[][] values) { }
                }
                """;

            CreateCompilation(source).VerifyEmitDiagnostics(
                // (1,2): error CS0181: Attribute constructor parameter 'values' has type 'int[][]', which is not a valid attribute parameter type
                // [X([[1], [2]])]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "X").WithArguments("values", "int[][]").WithLocation(1, 2)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_NestedArrayAsObject()
        {
            string source = """
                var attr = (XAttribute)System.Attribute.GetCustomAttribute(typeof(C), typeof(XAttribute));
                var inner = (int[])attr._values[0];
                inner.Report();

                [X([(int[])[1]])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public object[] _values;
                    public XAttribute(object[] values) { _values = values; }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }).VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[1],");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_ArrayAsObject()
        {
            string source = """
                var attr = (XAttribute)System.Attribute.GetCustomAttribute(typeof(C), typeof(XAttribute));
                var array = (int[])attr._value;
                array.Report();

                [X((int[])[1, 2, 3])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public object _value;
                    public XAttribute(object value) { _value = value; }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }).VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[1, 2, 3],");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_Empty()
        {
            string source = """
                var attr = (XAttribute)System.Attribute.GetCustomAttribute(typeof(C), typeof(XAttribute));
                attr._values.Report();
                
                [X([])]
                class C
                {
                }
                
                public class XAttribute : System.Attribute
                {
                    public int[] _values;
                    public XAttribute(int[] values) { _values = values; }
                }
                """;

            var comp = CreateCompilation(new[] { source, s_collectionExtensions }).VerifyEmitDiagnostics();
            CompileAndVerify(comp, expectedOutput: "[],");
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_NotConstant()
        {
            string source = """
                [X([1, 2, C.M()])]
                class C
                {
                    public static int M() => 0;
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(int[] values) { }
                }
                """;

            var comp = CreateCompilation(source).VerifyEmitDiagnostics(
                // (1,11): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
                // [X([1, 2, C.M()])]
                Diagnostic(ErrorCode.ERR_BadAttributeArgument, "C.M()").WithLocation(1, 11)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_NotConstant_CollectionSpread()
        {
            string source = """
                [X([1, 2, .. [3]])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(int[] values) { }
                }
                """;

            CreateCompilation(source).VerifyEmitDiagnostics(
                // (1,14): error CS9176: There is no target type for the collection expression.
                // [X([1, 2, .. [3]])]
                Diagnostic(ErrorCode.ERR_CollectionExpressionNoTargetType, "[3]").WithLocation(1, 14)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_NotConstant_ListSpread()
        {
            string source = """
                using System.Collections.Generic;

                [X([.. new List<int>()])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(int[] values) { }
                }
                """;

            CreateCompilation(source).VerifyEmitDiagnostics(
                // (3,5): error CS0182: An attribute argument must be a constant expression, typeof expression or array creation expression of an attribute parameter type
                // [X([.. new List<int>()])]
                Diagnostic(ErrorCode.ERR_BadAttributeArgument, ".. new List<int>()").WithLocation(3, 5)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_BadArrayType()
        {
            string source = """
                [X([1])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(ERROR[] values) { }
                }
                """;

            CreateCompilation(source).VerifyEmitDiagnostics(
                // (1,4): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'ERROR[]'
                // [X([1])]
                Diagnostic(ErrorCode.ERR_BadArgType, "[1]").WithArguments("1", "collection expressions", "ERROR[]").WithLocation(1, 4),
                // (8,23): error CS0246: The type or namespace name 'ERROR' could not be found (are you missing a using directive or an assembly reference?)
                //     public XAttribute(ERROR[] values) { }
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "ERROR").WithArguments("ERROR").WithLocation(8, 23)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_NotArrayType()
        {
            string source = """
                [X([1])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(int NOT_ARRAY) { }
                }
                """;

            CreateCompilation(source).VerifyEmitDiagnostics(
                // (1,4): error CS1503: Argument 1: cannot convert from 'collection expressions' to 'int'
                // [X([1])]
                Diagnostic(ErrorCode.ERR_BadArgType, "[1]").WithArguments("1", "collection expressions", "int").WithLocation(1, 4)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_SpanType()
        {
            string source = """
                [X([1])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(System.Span<int> s) { }
                }
                """;

            CreateCompilation(source, targetFramework: TargetFramework.Net70).VerifyEmitDiagnostics(
                // (1,2): error CS0181: Attribute constructor parameter 's' has type 'Span<int>', which is not a valid attribute parameter type
                // [X([1])]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "X").WithArguments("s", "System.Span<int>").WithLocation(1, 2)
                );
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69133")]
        public void InAttribute_ReadOnlySpanType()
        {
            string source = """
                [X([1])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(System.ReadOnlySpan<int> s) { }
                }
                """;

            CreateCompilation(source, targetFramework: TargetFramework.Net70).VerifyEmitDiagnostics(
                // (1,2): error CS0181: Attribute constructor parameter 's' has type 'ReadOnlySpan<int>', which is not a valid attribute parameter type
                // [X([1])]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "X").WithArguments("s", "System.ReadOnlySpan<int>").WithLocation(1, 2)
                );
        }

        [Fact]
        public void InAttribute_CollectionBuilderType()
        {
            string sourceA = """
                using System;
                using System.Collections;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;

                [CollectionBuilder(typeof(MyCollectionBuilder), nameof(MyCollectionBuilder.Create))]
                public struct MyCollection<T> : IEnumerable<T>
                {
                    private readonly List<T> _list;
                    public MyCollection(List<T> list) { _list = list; }
                    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                }
                public class MyCollectionBuilder
                {
                    public static MyCollection<T> Create<T>(ReadOnlySpan<T> items)
                    {
                        return new MyCollection<T>(new List<T>(items.ToArray()));
                    }
                }

                [X([1])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(MyCollection<int> s) { }
                }
                """;

            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics(
                // (22,2): error CS0181: Attribute constructor parameter 's' has type 'MyCollection<int>', which is not a valid attribute parameter type
                // [X([1])]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "X").WithArguments("s", "MyCollection<int>").WithLocation(22, 2)
                );
        }

        [Fact]
        public void InAttribute_CollectionInitializerType()
        {
            string sourceA = """
                using System.Collections;
                using System.Collections.Generic;

                public class A : IEnumerable<int>
                {
                    public void Add(int i) { }
                    IEnumerator<int> IEnumerable<int>.GetEnumerator() => null;
                    IEnumerator IEnumerable.GetEnumerator() => null;
                    static A Create1() => [];
                }

                [X([1])]
                class C
                {
                }

                public class XAttribute : System.Attribute
                {
                    public XAttribute(A a) { }
                }
                """;

            var comp = CreateCompilation(sourceA, targetFramework: TargetFramework.Net80);
            comp.VerifyDiagnostics(
                // (12,2): error CS0181: Attribute constructor parameter 'a' has type 'A', which is not a valid attribute parameter type
                // [X([1])]
                Diagnostic(ErrorCode.ERR_BadAttributeParamType, "X").WithArguments("a", "A").WithLocation(12, 2)
                );
        }
    }
}
