﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.InlineMethod
{
    [Trait(Traits.Feature, Traits.Features.CodeActionsInlineMethod)]
    public class CSharpInlineMethodTests
    {
        private class TestVerifier : CSharpCodeRefactoringVerifier<CSharpInlineMethodRefactoringProvider>.Test
        {
            private const string Marker = "##";

            public static async Task TestInRegularScriptsInDifferentFilesAsync(
                string initialMarkUpForFile1,
                string initialMarkUpForFile2,
                string expectedMarkUpForFile1,
                string expectedMarkUpForFile2,
                List<DiagnosticResult> diagnosticResults = null,
                bool keepInlinedMethod = true)
            {
                diagnosticResults ??= new List<DiagnosticResult>();
                var test = new TestVerifier
                {
                    CodeActionIndex = keepInlinedMethod ? 0 : 1,
                    TestState =
                    {
                        Sources =
                        {
                            ("File1", initialMarkUpForFile1),
                            ("File2", initialMarkUpForFile2),
                        }
                    },
                    FixedState =
                    {
                        Sources =
                        {
                            ("File1", expectedMarkUpForFile1),
                            ("File2", expectedMarkUpForFile2),
                        }
                    },
                    CodeActionValidationMode = CodeActionValidationMode.None
                };
                test.FixedState.ExpectedDiagnostics.AddRange(diagnosticResults);
                await test.RunAsync().ConfigureAwait(false);
            }

            public static async Task TestInRegularAndScriptInTheSameFileAsync(
                string initialMarkUp,
                string expectedMarkUp,
                List<DiagnosticResult> diagnosticResults = null,
                bool keepInlinedMethod = true)
            {
                diagnosticResults ??= new List<DiagnosticResult>();
                var test = new TestVerifier
                {
                    CodeActionIndex = keepInlinedMethod ? 0 : 1,
                    TestState =
                    {
                        Sources = { initialMarkUp }
                    },
                    FixedState =
                    {
                        Sources = { expectedMarkUp },
                    },
                    CodeActionValidationMode = CodeActionValidationMode.None
                };
                test.FixedState.ExpectedDiagnostics.AddRange(diagnosticResults);
                await test.RunAsync().ConfigureAwait(false);
            }

            public static async Task TestBothKeepAndRemoveInlinedMethodInDifferentFileAsync(
                string initialMarkUpForCaller,
                string initialMarkUpForCallee,
                string expectedMarkUpForCaller,
                string expectedMarkUpForCallee,
                List<DiagnosticResult> diagnosticResultsWhenKeepInlinedMethod = null,
                List<DiagnosticResult> diagnosticResultsWhenRemoveInlinedMethod = null)
            {
                var firstMarkerIndex = expectedMarkUpForCallee.IndexOf(Marker);
                var secondMarkerIndex = expectedMarkUpForCallee.LastIndexOf(Marker);
                if (firstMarkerIndex == -1 || secondMarkerIndex == -1 || firstMarkerIndex == secondMarkerIndex)
                {
                    Assert.True(false, "Can't find proper marks that contains inlined method.");
                }

                var firstPartitionBeforeMarkUp = expectedMarkUpForCallee.Substring(0, firstMarkerIndex);
                var inlinedMethod = expectedMarkUpForCallee.Substring(firstMarkerIndex + 2, secondMarkerIndex - firstMarkerIndex - 2);
                var lastPartitionAfterMarkup = expectedMarkUpForCallee.Substring(secondMarkerIndex + 2);

                await TestInRegularScriptsInDifferentFilesAsync(
                    initialMarkUpForCaller,
                    initialMarkUpForCallee,
                    expectedMarkUpForCaller,
                    string.Concat(firstPartitionBeforeMarkUp, inlinedMethod, lastPartitionAfterMarkup),
                    diagnosticResultsWhenKeepInlinedMethod,
                    keepInlinedMethod: true).ConfigureAwait(false);

                await TestInRegularScriptsInDifferentFilesAsync(
                    initialMarkUpForCaller,
                    initialMarkUpForCallee,
                    expectedMarkUpForCaller,
                    string.Concat(firstPartitionBeforeMarkUp, lastPartitionAfterMarkup),
                    diagnosticResultsWhenKeepInlinedMethod,
                    keepInlinedMethod: false).ConfigureAwait(false);
            }

            public static async Task TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                string initialMarkUp,
                string expectedMarkUp,
                List<DiagnosticResult> diagnosticResultsWhenKeepInlinedMethod = null,
                List<DiagnosticResult> diagnosticResultsWhenRemoveInlinedMethod = null)
            {
                var firstMarkerIndex = expectedMarkUp.IndexOf(Marker);
                var secondMarkerIndex = expectedMarkUp.LastIndexOf(Marker);
                if (firstMarkerIndex == -1 || secondMarkerIndex == -1 || firstMarkerIndex == secondMarkerIndex)
                {
                    Assert.True(false, "Can't find proper marks that contains inlined method.");
                }

                var firstPartitionBeforeMarkUp = expectedMarkUp.Substring(0, firstMarkerIndex);
                var inlinedMethod = expectedMarkUp.Substring(firstMarkerIndex + 2, secondMarkerIndex - firstMarkerIndex - 2);
                var lastPartitionAfterMarkup = expectedMarkUp.Substring(secondMarkerIndex + 2);

                await TestInRegularAndScriptInTheSameFileAsync(
                    initialMarkUp,
                    string.Concat(
                        firstPartitionBeforeMarkUp,
                        inlinedMethod,
                        lastPartitionAfterMarkup),
                    diagnosticResultsWhenKeepInlinedMethod,
                    keepInlinedMethod: true).ConfigureAwait(false);

                await TestInRegularAndScriptInTheSameFileAsync(
                    initialMarkUp,
                    string.Concat(
                        firstPartitionBeforeMarkUp,
                        lastPartitionAfterMarkup),
                    diagnosticResultsWhenRemoveInlinedMethod,
                    keepInlinedMethod: false).ConfigureAwait(false);
            }
        }

//         [Fact]
//         public Task Test2()
//             => TestVerifier.TestInRegularScriptsInDifferentFilesAsync(
//                 @"
// using System.Collections.Generic;
// public partial class TestClass
// {
//     private void Caller(int i)
//     {
//         var h = new HashSet<int>();
//         Ca[||]llee(i, h);
//     }
// }",
//                 @"
// using System.Collections.Generic;
// public partial class TestClass
// {
//     private bool Callee(int i, HashSet<int> set)
//     {
//         return set.Add(i);
//     }
// }",
//                 @"
// using System.Collections.Generic;
// public partial class TestClass
// {
//     private void Caller(int i)
//     {
//         var h = new HashSet<int>();
//         h.Add(i);
//     }
// }",
//                 @"
// using System.Collections.Generic;
// public partial class TestClass
// {
//     private bool Callee(int i, HashSet<int> set)
//     {
//         return set.Add(i);
//     }
// }");

        [Fact]
        public Task TestInlineInvocationExpressionForExpressionStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Collections.Generic;
public class TestClass
{
    private void Caller(int i)
    {
        var h = new HashSet<int>();
        Ca[||]llee(i, h);
    }

    private bool Callee(int i, HashSet<int> set)
    {
        return set.Add(i);
    }
}",
                @"
using System.Collections.Generic;
public class TestClass
{
    private void Caller(int i)
    {
        var h = new HashSet<int>();
        h.Add(i);
    }
##
    private bool Callee(int i, HashSet<int> set)
    {
        return set.Add(i);
    }
##}");

        [Fact]
        public Task TestInlineMethodWithSingleStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller(int i, int j)
    {
        Ca[||]llee(i, j);
    }

    private void Callee(int i, int j)
    {
        System.Console.WriteLine(i + j);
    }
}",
                @"
public class TestClass
{
    private void Caller(int i, int j)
    {
        System.Console.WriteLine(i + j);
    }
##
    private void Callee(int i, int j)
    {
        System.Console.WriteLine(i + j);
    }
##}");

        [Fact]
        public Task TestExtractArrowExpressionBody()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Caller(int i, int j)
    {
        Ca[||]llee(i, j);
    }

    private void Callee(int i, int j)
        => System.Console.WriteLine(i + j);
}",
                @"
public class TestClass
{
    private void Caller(int i, int j)
    {
        System.Console.WriteLine(i + j);
    }
##
    private void Callee(int i, int j)
        => System.Console.WriteLine(i + j);
##}");

        [Fact]
        public Task TestExtractExpressionBody()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller(int i, int j)
    {
        var x = Ca[||]llee(i, j);
    }

    private int Callee(int i, int j)
        => i + j;
}",
                @"
public class TestClass
{
    private void Caller(int i, int j)
    {
        var x = i + j;
    }
##
    private int Callee(int i, int j)
        => i + j;
##}");

        [Fact]
        public Task TestDefaultValueReplacementForExpressionStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee();
    }

    private void Callee(int i = 1, string c = null, bool y = false)
    {
        System.Console.WriteLine(y ? i : (c ?? ""Hello"").Length);
    }
}",
                @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine(false ? 1 : (null ?? ""Hello"").Length);
    }
##
    private void Callee(int i = 1, string c = null, bool y = false)
    {
        System.Console.WriteLine(y ? i : (c ?? ""Hello"").Length);
    }
##}");

        [Fact]
        public Task TestDefaultValueReplacementForArrowExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee();
    }

    private void Callee(int i = default, string c = default, bool y = false) =>
        System.Console.WriteLine(y ? i : (c ?? ""Hello"").Length);
}",
                @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine(false ? 0 : (null ?? ""Hello"").Length);
    }
##
    private void Callee(int i = default, string c = default, bool y = false) =>
        System.Console.WriteLine(y ? i : (c ?? ""Hello"").Length);
##}");

        [Fact]
        public Task TestInlineMethodWithLiteralValue()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee(1, 'y', true, ""Hello"");
    }

    private void Callee(int i, char c, bool x, string y) =>
        System.Console.WriteLine(i + (int)c + (x ? 1 : y.Length));
}",
                @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine(1 + (int)'y' + (true ? 1 : ""Hello"".Length));
    }
##
    private void Callee(int i, char c, bool x, string y) =>
        System.Console.WriteLine(i + (int)c + (x ? 1 : y.Length));
##}");

        [Fact]
        public Task TestInlineMethodWithIdentifierReplacement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller(int m)
    {
        Cal[||]lee(10, m, k: ""Hello"");
    }

    private void Callee(int i, int j = 100, string k = null)
    {
        System.Console.WriteLine(i + j + (k ?? """"));
    }
}",
                @"
public class TestClass
{
    private void Caller(int m)
    {
        System.Console.WriteLine(10 + m + (""Hello"" ?? """"));
    }
##
    private void Callee(int i, int j = 100, string k = null)
    {
        System.Console.WriteLine(i + j + (k ?? """"));
    }
##}");

        [Fact]
        public Task TestInlineMethodWithMethodExtraction()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller(float r1, float r2)
    {
        Cal[||]lee(SomeCaculation(r1), SomeCaculation(r2));
    }

    private void Callee(float s1, float s2)
    {
        System.Console.WriteLine(""This is s1"" + s1 + ""This is S2"" + s2);
    }

    public float SomeCaculation(float r)
    {
        return r * r * 3.14f;
    }
}",
                @"
public class TestClass
{
    private void Caller(float r1, float r2)
    {
        System.Console.WriteLine(""This is s1"" + SomeCaculation(r1) + ""This is S2"" + SomeCaculation(r2));
    }
##
    private void Callee(float s1, float s2)
    {
        System.Console.WriteLine(""This is s1"" + s1 + ""This is S2"" + s2);
    }
##
    public float SomeCaculation(float r)
    {
        return r * r * 3.14f;
    }
}");

        [Fact]
        public Task TestInlineParamsArrayWithArrayImplicitInitializerExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee(new int[] {1, 2, 3, 4, 5, 6});
    }

    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
}"
,
                @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine((new int[] {1, 2, 3, 4, 5, 6}).Length);
    }
##
    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
##}");

        [Fact]
        public Task TestInlineParamsArrayWithArrayInitializerExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee(new int[6] {1, 2, 3, 4, 5, 6});
    }

    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
}"
,
                @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine((new int[6] {1, 2, 3, 4, 5, 6}).Length);
    }
##
    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
##}");

        [Fact]
        public Task TestInlineParamsArrayWithOneElement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee(1);
    }

    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
}"
,
                @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine((new int[] { 1 }).Length);
    }
##
    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
##}");

        [Fact]
        public Task TestInlineParamsArrayMethodWithIdentifier()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Caller()
    {
        var i = new int[] {1, 2, 3, 4, 5};
        Cal[||]lee(i);
    }

    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
}",
                @"
public class TestClass
{
    private void Caller()
    {
        var i = new int[] {1, 2, 3, 4, 5};
        System.Console.WriteLine(i.Length);
    }
##
    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
##}");
        [Fact]
        public Task TestInlineMethodWithNoElementInParamsArray()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                    @"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee();
    }

    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
}",
                    @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine((new int[] { }).Length);
    }
##
    private void Callee(params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
##}");

        [Fact]
        public Task TestInlineMethodWithParamsArray()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        Ca[||]llee(""Hello"", 1, 2, 3, 4, 5, 6);
    }

    private void Callee(string z, params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
}",
                @"
public class TestClass
{
    private void Caller()
    {
        System.Console.WriteLine((new int[] { 1, 2, 3, 4, 5, 6 }).Length);
    }
##
    private void Callee(string z, params int[] x)
    {
        System.Console.WriteLine(x.Length);
    }
##}");

        [Fact]
        public Task TestInlineMethodWithVariableDeclaration1()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee(out var x);
    }

    private void Callee(out int z)
    {
        z = 10;
    }
}",
                @"
public class TestClass
{
    private void Caller()
    {
        int x = 10;
    }
##
    private void Callee(out int z)
    {
        z = 10;
    }
##}");

        [Fact]
        public Task TestInlineMethodWithVariableDeclaration2()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee(out var x, out var y, out var z);
    }

    private void Callee(out int z, out int x, out int y)
    {
        z = x = y = 10;
    }
}",
                @"
public class TestClass
{
    private void Caller()
    {
        int x;
        int y;
        int z;
        x = y = z = 10;
    }
##
    private void Callee(out int z, out int x, out int y)
    {
        z = x = y = 10;
    }
##}");

        [Fact]
        public Task TestInlineMethodWithVariableDeclaration3()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        Cal[||]lee(out var x);
    }

    private void Callee(out int z)
    {
        DoSometing(out z);
    }

    private void DoSometing(out int z)
    {
        z = 100;
    }
}",
                @"
public class TestClass
{
    private void Caller()
    {
        int x;
        DoSometing(out x);
    }
##
    private void Callee(out int z)
    {
        DoSometing(out z);
    }
##
    private void DoSometing(out int z)
    {
        z = 100;
    }
}");

        [Fact]
        public Task TestInlineCalleeSelf()
            => TestVerifier.TestInRegularAndScriptInTheSameFileAsync(
                @"
public class TestClass
{
    public TestClass()
    {
        var x = Callee1(Cal[||]lee1(Callee1(Callee1(10))));
    }

    private int Callee1(int j)
    {
        return 1 + 2 + j;
    }
}",
                @"
public class TestClass
{
    public TestClass()
    {
        var x = Callee1(1 + 2 + Callee1(Callee1(10)));
    }

    private int Callee1(int j)
    {
        return 1 + 2 + j;
    }
}");
        [Fact]
        public Task TestInlineMethodWithConditionalExpression()
            => TestVerifier.TestInRegularAndScriptInTheSameFileAsync(
                @"
public class TestClass
{
    public void Caller(bool x)
    {
        int t = C[||]allee(x ? Callee(1) : Callee(2));
    }

    private int Callee(int i)
    {
        return i + 1;
    }
}",
                @"
public class TestClass
{
    public void Caller(bool x)
    {
        int t = (x ? Callee(1) : Callee(2)) + 1;
    }

    private int Callee(int i)
    {
        return i + 1;
    }
}");

        [Fact]
        public Task TestInlineExpressionWithoutAssignedToVariable()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    public void Caller(int j)
    {
        Cal[||]lee(j);
    }

    private int Callee(int i)
    {
        return i + 1;
    }
}", @"
public class TestClass
{
    public void Caller(int j)
    {
        int temp = j + 1;
    }
##
    private int Callee(int i)
    {
        return i + 1;
    }
##}");

        [Fact]
        public Task TestInlineMethodWithNullCoalescingExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    public void Caller(int? i)
    {
        var t = Cal[||]lee(i ?? 1);
    }

    private int Callee(int i)
    {
        return i + 1;
    }
}", @"
public class TestClass
{
    public void Caller(int? i)
    {
        var t = (i ?? 1) + 1;
    }
##
    private int Callee(int i)
    {
        return i + 1;
    }
##}");

        [Fact]
        public Task TestInlineSimpleLambdaExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    public System.Func<int, int, int> Caller()
    {
        return Ca[||]llee();
    }

    private System.Func<int, int, int> Callee() => (i, j) => i + j;
}", @"
public class TestClass
{
    public System.Func<int, int, int> Caller()
    {
        return (i, j) => i + j;
    }
##
    private System.Func<int, int, int> Callee() => (i, j) => i + j;
##}");

        [Fact]
        public Task TestInlineMethodWithGenericsArguments()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System;
public class TestClass
{
    private void Caller<U>()
    {
        Ca[||]llee<int, U>(1, 2, 3);
    }

    private void Callee<T, U>(params T[] i)
    {
        System.Console.WriteLine(typeof(T).Name.Length + i.Length + typeof(U).Name.Length);
    }
}",
                @"
using System;
public class TestClass
{
    private void Caller<U>()
    {
        System.Console.WriteLine(typeof(int).Name.Length + (new int[] { 1, 2, 3 }).Length + typeof(U).Name.Length);
    }
##
    private void Callee<T, U>(params T[] i)
    {
        System.Console.WriteLine(typeof(T).Name.Length + i.Length + typeof(U).Name.Length);
    }
##}");

        [Fact]
        public Task TestAwaitExpressionInMethod()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
public class TestClass
{
    public Task<int> Caller(bool x)
    {
        System.Console.WriteLine("""");
        return Call[||]ee(10, x ? 1 : 2);
    }

    private async Task<int> Callee(int i, int j)
    {
        return await Task.FromResult(i + j);
    }
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    public Task<int> Caller(bool x)
    {
        System.Console.WriteLine("""");
        return Task.FromResult(10 + (x ? 1 : 2));
    }
##
    private async Task<int> Callee(int i, int j)
    {
        return await Task.FromResult(i + j);
    }
##}");

        [Fact]
        public Task TestAwaitExpressionInMethod2()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
using System;
public class TestClass
{
    public void Caller(bool x)
    {
        System.Console.WriteLine("""");
        var f = C[||]allee();
    }

    private Func<Task> Callee()
    {
        return async () => await Task.Delay(100);
    }
}",
                @"
using System.Threading.Tasks;
using System;
public class TestClass
{
    public void Caller(bool x)
    {
        System.Console.WriteLine("""");
        var f = (Func<Task>)(async () => await Task.Delay(100));
    }
##
    private Func<Task> Callee()
    {
        return async () => await Task.Delay(100);
    }
##}");

        [Fact]
        public Task TestAwaitExpresssion1()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
public class TestClass
{
    public Task Caller()
    {
        return Cal[||]lee();
    }

    private async Task Callee()
    {
        await Task.CompletedTask;
    }
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    public Task Caller()
    {
        return Task.CompletedTask;
    }
##
    private async Task Callee()
    {
        await Task.CompletedTask;
    }
##}");

        [Fact]
        public Task TestAwaitExpresssion2()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
public class TestClass
{
    public async Task Caller()
    {
        await Cal[||]lee().ConfigureAwait(false);
    }

    private async Task Callee() => await Task.CompletedTask;
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    public async Task Caller()
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }
##
    private async Task Callee() => await Task.CompletedTask;
##}");

        [Fact]
        public Task TestAwaitExpresssion3()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
public class TestClass
{
    public Task<int> Caller()
    {
        return Cal[||]lee();
    }

    private async Task<int> Callee() => await Task.FromResult(1);
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    public Task<int> Caller()
    {
        return Task.FromResult(1);
    }
##
    private async Task<int> Callee() => await Task.FromResult(1);
##}");

        [Fact]
        public Task TestAwaitExpresssion4() =>
            TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
public class TestClass
{
    public void Caller()
    {
        var x = Cal[||]lee();
    }

    private async Task<int> Callee()
    {
        return await Task.FromResult(await SomeCalculation());
    }

    private async Task<int> SomeCalculation() => await Task.FromResult(10);
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    public async void Caller()
    {
        var x = Task.FromResult(await SomeCalculation());
    }
##
    private async Task<int> Callee()
    {
        return await Task.FromResult(await SomeCalculation());
    }
##
    private async Task<int> SomeCalculation() => await Task.FromResult(10);
}");

        [Fact]
        public Task TestAwaitExpresssion5()
        {
            var diagnostic = new List<DiagnosticResult>()
            {
                // Await can't be used in non-async method.
                DiagnosticResult.CompilerError("CS4032").WithSpan(7, 33, 7, 56).WithArguments("int")
            };
            return TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                           @"
using System.Threading.Tasks;
public class TestClass
{
    public int Caller()
    {
        var x = Cal[||]lee();
        return 1;
    }

    private async Task<int> Callee()
    {
        return await Task.FromResult(await SomeCalculation());
    }

    private async Task<int> SomeCalculation() => await Task.FromResult(10);
}",
                           @"
using System.Threading.Tasks;
public class TestClass
{
    public int Caller()
    {
        var x = Task.FromResult(await SomeCalculation());
        return 1;
    }
##
    private async Task<int> Callee()
    {
        return await Task.FromResult(await SomeCalculation());
    }
##
    private async Task<int> SomeCalculation() => await Task.FromResult(10);
}", diagnosticResultsWhenKeepInlinedMethod: diagnostic, diagnosticResultsWhenRemoveInlinedMethod: diagnostic);
        }

        [Fact]
        public Task TestAwaitExpresssion6() =>
            TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
public class TestClass
{
    public void Caller()
    {
        var x = Cal[||]lee().Result;
    }

    private async Task<int> Callee()
    {
        return await Task.FromResult(100);
    }
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    public void Caller()
    {
        var x = Task.FromResult(100).Result;
    }
##
    private async Task<int> Callee()
    {
        return await Task.FromResult(100);
    }
##}");

        [Fact]
        public Task TestAwaitExpressionInLambda()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System;
using System.Threading.Tasks;
public class TestClass
{
    private void Method()
    {
        Func<bool, Task<int>> x1 = (x) =>
        {
            System.Console.WriteLine("""");
            return Call[||]ee();
        };
    }

    private async Task<int> Callee()
    {
        return await Task.FromResult(10);
    }
}",
                @"
using System;
using System.Threading.Tasks;
public class TestClass
{
    private void Method()
    {
        Func<bool, Task<int>> x1 = (x) =>
        {
            System.Console.WriteLine("""");
            return Task.FromResult(10);
        };
    }
##
    private async Task<int> Callee()
    {
        return await Task.FromResult(10);
    }
##}");

        [Fact]
        public Task TestAwaitExpressionInLocalMethod()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System.Threading.Tasks;
public class TestClass
{
    private void Method()
    {
        Task<int> Caller(bool x)
        {
            System.Console.WriteLine("""");
            return Call[||]ee(10, x ? 1 : 2);
        }
    }

    private async Task<int> Callee(int i, int j)
    {
        return await Task.FromResult(i + j);
    }
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    private void Method()
    {
        Task<int> Caller(bool x)
        {
            System.Console.WriteLine("""");
            return Task.FromResult(10 + (x ? 1 : 2));
        }
    }
##
    private async Task<int> Callee(int i, int j)
    {
        return await Task.FromResult(i + j);
    }
##}");
        [Fact]
        public Task TestInlineMethodForLambda()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
using System;
public class TestClass
{
    public void Caller()
    {
        Call[||]ee()(10);
    }

    private Func<int, int> Callee()
        => i => 1;
}",
                @"
using System;
public class TestClass
{
    public void Caller()
    {
        ((Func<int, int>)(i => 1))(10);
    }
##
    private Func<int, int> Callee()
        => i => 1;
##}");

        [Fact]
        public Task TestInlineWithinDoStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        do
        {
        } while(Cal[||]lee(SomeInt()) == 1);
    }

    private int Callee(int i)
    {
        return i + 1;
    }

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private void Caller()
    {
        do
        {
        } while(SomeInt() + 1 == 1);
    }
##
    private int Callee(int i)
    {
        return i + 1;
    }
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineWithinForStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        for (int i = Ca[||]llee(SomeInt()); i < 10; i++)
        {
        }
    }

    private int Callee(int i)
    {
        return i + 1;
    }

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private void Caller()
    {
        for (int i = SomeInt() + 1; i < 10; i++)
        {
        }
    }
##
    private int Callee(int i)
    {
        return i + 1;
    }
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineWithinIfStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        if (Ca[||]llee(SomeInt()) == 1)
        {
        }
    }

    private int Callee(int i)
    {
        return i + 1;
    }

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private void Caller()
    {
        if (SomeInt() + 1 == 1)
        {
        }
    }
##
    private int Callee(int i)
    {
        return i + 1;
    }
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineWithinLockStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        lock (Ca[||]llee(SomeInt()))
        {
        }
    }

    private string Callee(int i)
    {
        return ""Hello"" + i;
    }

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private void Caller()
    {
        lock (""Hello"" + SomeInt())
        {
        }
    }
##
    private string Callee(int i)
    {
        return ""Hello"" + i;
    }
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineWithinReturnStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private string Caller()
    {
        return Call[||]ee(SomeInt());
    }

    private string Callee(int i)
    {
        return ""Hello"" + i;
    }

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private string Caller()
    {
        return ""Hello"" + SomeInt();
    }
##
    private string Callee(int i)
    {
        return ""Hello"" + i;
    }
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineMethodWithinThrowStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        throw new System.Exception(Call[||]ee(SomeInt()) + """");
    }

    private int Callee(int i)
    {
        return i + 20;
    }

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private void Caller()
    {
        throw new System.Exception(SomeInt() + 20 + """");
    }
##
    private int Callee(int i)
    {
        return i + 20;
    }
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineWithinWhileStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    private void Caller()
    {
        while (Cal[||]lee(SomeInt()) == 1)
        {}
    }

    private int Callee(int i)
    {
        return i + 1;
    }

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private void Caller()
    {
        while (SomeInt() + 1 == 1)
        {}
    }
##
    private int Callee(int i)
    {
        return i + 1;
    }
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineMethodWithinTryStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
            @"
public class TestClass
{
    private void Calller()
    {
        try
        {
        }
        catch (System.Exception e) when (Ca[||]llee(e, SomeInt()))
        {
        }
    }

    private bool Callee(System.Exception e, int i) => i == 1;

    private int SomeInt() => 10;
}",
                @"
public class TestClass
{
    private void Calller()
    {
        try
        {
        }
        catch (System.Exception e) when (SomeInt() == 1)
        {
        }
    }
##
    private bool Callee(System.Exception e, int i) => i == 1;
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineMethodWithinYieldReturnStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass2
{
    private System.Collections.Generic.IEnumerable<int> Calller()
    {
        yield return 1;
        yield return Cal[||]lee(SomeInt());
        yield return 3;
    }

    private int Callee(int i) => i + 10;

    private int SomeInt() => 10;
}",
                @"
public class TestClass2
{
    private System.Collections.Generic.IEnumerable<int> Calller()
    {
        yield return 1;
        yield return SomeInt() + 10;
        yield return 3;
    }
##
    private int Callee(int i) => i + 10;
##
    private int SomeInt() => 10;
}");

        [Fact]
        public Task TestInlineExtensionMethod1()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
static class Program
{
    static void Main(string[] args)
    {
        var value = 0;
        value.Ge[||]tNext();
    }

    private static int GetNext(this int i)
    {
        return i + 1;
    }
}",
                @"
static class Program
{
    static void Main(string[] args)
    {
        var value = 0;
        int temp = value + 1;
    }
##
    private static int GetNext(this int i)
    {
        return i + 1;
    }
##}");

        [Fact]
        public Task TestInlineExtensionMethod2()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
static class Program
{
    static void Main(string[] args)
    {
        var x = 0.Ge[||]tNext();
    }

    private static int GetNext(this int i)
    {
        return i + 1;
    }
}",
                @"
static class Program
{
    static void Main(string[] args)
    {
        var x = 0 + 1;
    }
##
    private static int GetNext(this int i)
    {
        return i + 1;
    }
##}");

        [Fact]
        public Task TestInlineExtensionMethod3()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
static class Program
{
    static void Main(string[] args)
    {
        GetInt().Ge[||]tNext();
    }

    private static int GetInt() => 10;

    private static int GetNext(this int i)
    {
        return i + 1;
    }
}",
                @"
static class Program
{
    static void Main(string[] args)
    {
        int temp = GetInt() + 1;
    }

    private static int GetInt() => 10;
##
    private static int GetNext(this int i)
    {
        return i + 1;
    }
##}");

        [Fact]
        public Task TestInlineExpressionAsLeftValueInLeftAssociativeExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public int Caller
    {
        get
        {
            return Ca[||]llee(1, 2) - 1;
        }
    }

    private int Callee(int i, int j)
    {
        return i + j;
    }
}",
                @"
public class TestClass
{
    public int Caller
    {
        get
        {
            return 1 + 2 - 1;
        }
    }
##
    private int Callee(int i, int j)
    {
        return i + j;
    }
##}");

        [Fact]
        public Task TestInlineExpressionAsRightValueInRightAssociativeExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public int Caller
    {
        get
        {
            return 1 - Ca[||]llee(1, 2);
        }
    }

    private int Callee(int i, int j)
    {
        return i + j;
    }
}",
                @"
public class TestClass
{
    public int Caller
    {
        get
        {
            return 1 - (1 + 2);
        }
    }
##
    private int Callee(int i, int j)
    {
        return i + j;
    }
##}");

        [Fact]
        public Task TestAddExpressionWithMultiply()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public int Caller
    {
        get
        {
            return Ca[||]llee(1, 2) * 2;
        }
    }

    private int Callee(int i, int j)
    {
        return i + j;
    }
}",
                @"
public class TestClass
{
    public int Caller
    {
        get
        {
            return (1 + 2) * 2;
        }
    }
##
    private int Callee(int i, int j)
    {
        return i + j;
    }
##}");

        [Fact]
        public Task TestIsExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public bool Caller(int i, int j)
    {
        return Ca[||]llee(i, j) is int;
    }

    private bool Callee(int i, int j)
    {
        return i == j;
    }
}",
                @"
public class TestClass
{
    public bool Caller(int i, int j)
    {
        return (i == j) is int;
    }
##
    private bool Callee(int i, int j)
    {
        return i == j;
    }
##}");

        [Fact]
        public Task TestUnaryPlusOperator()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public int Caller()
    {
        return +Call[||]ee();
    }

    private int Callee()
    {
        return 1 + 2;
    }
}",
                @"
public class TestClass
{
    public int Caller()
    {
        return +(1 + 2);
    }
##
    private int Callee()
    {
        return 1 + 2;
    }
##}");

        [Fact]
        public Task TestLogicalNotExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public bool Caller(int i, int j)
    {
        return !Ca[||]llee(i, j);
    }

    private bool Callee(int i, int j)
    {
        return i == j;
    }
}",
                @"
public class TestClass
{
    public bool Caller(int i, int j)
    {
        return !(i == j);
    }
##
    private bool Callee(int i, int j)
    {
        return i == j;
    }
##}");

        [Fact]
        public Task TestBitWiseNotExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public int Caller(int i, int j)
    {
        return ~Call[||]ee(i, j);
    }

    private int Callee(int i, int j)
    {
        return i | j;
    }
}",
                @"
public class TestClass
{
    public int Caller(int i, int j)
    {
        return ~(i | j);
    }
##
    private int Callee(int i, int j)
    {
        return i | j;
    }
##}");

        [Fact]
        public Task TestCastExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public char Caller(int i, int j)
    {
        return (char)Call[||]ee(i, j);
    }

    private int Callee(int i, int j)
    {
        return i + j;
    }
}",
                @"
public class TestClass
{
    public char Caller(int i, int j)
    {
        return (char)(i + j);
    }
##
    private int Callee(int i, int j)
    {
        return i + j;
    }
##}");

        [Fact]
        public Task TestIsPatternExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Calller()
    {
        if (Ca[||]llee() is int i)
        {
        }
    }

    private int Callee()
    {
        return 1 | 2;
    }
}",
                @"
public class TestClass
{
    public void Calller()
    {
        if ((1 | 2) is int i)
        {
        }
    }
##
    private int Callee()
    {
        return 1 | 2;
    }
##}");

        [Fact]
        public Task TestAsExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Calller(bool f)
    {
        var x = Cal[||]lee(f) as string;
    }

    private object Callee(bool f)
    {
        return f ? ""Hello"" : ""World"";
    }
}",
                @"
public class TestClass
{
    public void Calller(bool f)
    {
        var x = (f ? ""Hello"" : ""World"") as string;
    }
##
    private object Callee(bool f)
    {
        return f ? ""Hello"" : ""World"";
    }
##}");

        [Fact]
        public Task TestCoalesceExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public int Caller(int? c)
    {
        return 1 + Cal[||]lee(c);
    }

    private int Callee(int? c)
    {
        return c ?? 1;
    }
}",
                @"
public class TestClass
{
    public int Caller(int? c)
    {
        return 1 + (c ?? 1);
    }
##
    private int Callee(int? c)
    {
        return c ?? 1;
    }
##}");

        [Fact]
        public Task TestCoalesceExpressionAsRightValue()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public string Caller(string c)
    {
        return c ?? Cal[||]lee(null);
    }

    private string Callee(string c2)
    {
        return c2 ?? ""Hello"";
    }
}",
                @"
public class TestClass
{
    public string Caller(string c)
    {
        return c ?? null ?? ""Hello"";
    }
##
    private string Callee(string c2)
    {
        return c2 ?? ""Hello"";
    }
##}");

        [Fact]
        public Task TestSimpleMemberAccessExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Caller()
    {
        var x = C[||]allee().Length;
    }

    private string Callee() => ""H"" + ""L"";
}",
                @"
public class TestClass
{
    public void Caller()
    {
        var x = (""H"" + ""L"").Length;
    }
##
    private string Callee() => ""H"" + ""L"";
##}");

        [Fact]
        public Task TestElementAccessExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Caller()
    {
        var x = C[||]allee()[0];
    }

    private string Callee() => ""H"" + ""L"";
}",
                @"
public class TestClass
{
    public void Caller()
    {
        var x = (""H"" + ""L"")[0];
    }
##
    private string Callee() => ""H"" + ""L"";
##}");

        [Fact]
        public Task TestSwitchExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Calller()
    {
        var x = C[||]allee() switch
        {
            1 => 1,
            2 => 2,
            _ => 3
        };
    }

    private int Callee() => 1 + 2;
}",
                @"
public class TestClass
{
    private void Calller()
    {
        var x = (1 + 2) switch
        {
            1 => 1,
            2 => 2,
            _ => 3
        };
    }
##
    private int Callee() => 1 + 2;
##}");

        [Fact]
        public Task TestConditionalExpressionSyntax()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Calller(int x)
    {
        var z = true;
        var z1 = false;
        var y = z ? 3 : z1 ? 1 : Ca[||]llee(x);
    }

    private int Callee(int x) => x = 1;
}",
                @"
public class TestClass
{
    private void Calller(int x)
    {
        var z = true;
        var z1 = false;
        var y = z ? 3 : z1 ? 1 : (x = 1);
    }
##
    private int Callee(int x) => x = 1;
##}");

        [Fact]
        public Task TestSuppressNullableWarningExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
#nullable enable
public class TestClass
{
    private object Calller(int x)
    {
        return Ca[||]llee(x)!;
    }

    private object Callee(int x) => x = 1;
}",
                @"
#nullable enable
public class TestClass
{
    private object Calller(int x)
    {
        return (x = 1)!;
    }
##
    private object Callee(int x) => x = 1;
##}");

        [Fact]
        public Task TestSimpleAssignmentExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private int Calller(int x)
    {
        return Ca[||]llee(x) + 1;
    }

    private int Callee(int x) => x = 1;
}",
                @"
public class TestClass
{
    private int Calller(int x)
    {
        return (x = 1) + 1;
    }
##
    private int Callee(int x) => x = 1;
##}");

        [Theory]
        [InlineData("++")]
        [InlineData("--")]
        public Task TestPreExpression(string op)
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Caller()
    {
        int i = 1;
        Cal[||]lee(i);
    }

    private int Callee(int i)
    {
        return (op)i;
    }
}".Replace("(op)", op),
                @"
public class TestClass
{
    public void Caller()
    {
        int i = 1;
        (op)i;
    }
##
    private int Callee(int i)
    {
        return (op)i;
    }
##}".Replace("(op)", op));

        [Fact]
        public Task TestAwaitExpressionWithFireAndForgot()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
using System.Threading.Tasks;
public class TestClass
{
    public async void Caller()
    {
        Cal[||]lee();
    }
    private async Task Callee()
    {
        await Task.Delay(100);
    }
}",
                @"
using System.Threading.Tasks;
public class TestClass
{
    public async void Caller()
    {
        Task.Delay(100);
    }
##    private async Task Callee()
    {
        await Task.Delay(100);
    }
##}");

        [Theory]
        [InlineData("++")]
        [InlineData("--")]
        public Task TestPostExpression(string op)
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Caller()
    {
        int i = 1;
        Cal[||]lee(i);
    }

    private int Callee(int i)
    {
        return i(op);
    }
}".Replace("(op)", op),
                @"
public class TestClass
{
    public void Caller()
    {
        int i = 1;
        i(op);
    }
##
    private int Callee(int i)
    {
        return i(op);
    }
##}".Replace("(op)", op));

        [Fact]
        public Task TestObjectCreationExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Caller()
    {
        Call[||]ee();
    }

    private object Callee()
    {
        return new object();
    }
}",
                @"
public class TestClass
{
    public void Caller()
    {
        new object();
    }
##
    private object Callee()
    {
        return new object();
    }
##}");
        [Fact]
        public Task TestConditionalInvocationExpression2()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Caller()
    {
        Cal[||]lee()?.ToCharArray();
    }

    private string Callee() => ""Hello"" + ""World"";
}",
                @"
public class TestClass
{
    public void Caller()
    {
        (""Hello"" + ""World"")?.ToCharArray();
    }
##
    private string Callee() => ""Hello"" + ""World"";
##}");

        [Fact]
        public Task TestConditionalInvocationExpression1()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(
                @"
public class TestClass
{
    public void Caller()
    {
        Cal[||]lee();
    }

    private char[] Callee() => (""Hello"" + ""World"")?.ToCharArray();
}",
                @"
public class TestClass
{
    public void Caller()
    {
        (""Hello"" + ""World"")?.ToCharArray();
    }
##
    private char[] Callee() => (""Hello"" + ""World"")?.ToCharArray();
##}");

        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("%")]
        [InlineData("&")]
        [InlineData("|")]
        [InlineData("^")]
        [InlineData("")]
        [InlineData(">>")]
        [InlineData("<<")]
        public Task TestAssignmentExpression(string op)
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    private void Calller(int x)
    {
        Ca[||]llee(x);
    }

    private int Callee(int x) => x (op)= 1;
}".Replace("(op)", op),
                @"
public class TestClass
{
    private void Calller(int x)
    {
        x (op)= 1;
    }
##
    private int Callee(int x) => x (op)= 1;
##}".Replace("(op)", op));

        [Fact]
        public Task TestInlineLambdaInsideInvocation()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
using System;
public class TestClass
{
    public void Caller()
    {
        var x = Cal[||]lee()();
    }

    private Func<int> Callee()
    {
        return () => 1;
    }
}
", @"
using System;
public class TestClass
{
    public void Caller()
    {
        var x = ((Func<int>)(() => 1))();
    }
##
    private Func<int> Callee()
    {
        return () => 1;
    }
##}
");

        [Fact]
        public Task TestInlineTypeCast()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    public void Caller()
    {
        var x = Cal[||]lee();
    }

    private long Callee()
    {
        return 1;
    }
}
", @"
public class TestClass
{
    public void Caller()
    {
        var x = (long)1;
    }
##
    private long Callee()
    {
        return 1;
    }
##}
");

        [Fact]
        public Task TestNestedConditionalInvocationExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class LinkedList
{
    public LinkedList Next { get; }
}

public class TestClass
{
    public void Caller()
    {
        var l = new LinkedList();
        Cal[||]lee(l);
    }

    private string Callee(LinkedList l)
    {
        return l?.Next?.Next?.Next?.Next?.ToString();
    }
}
", @"
public class LinkedList
{
    public LinkedList Next { get; }
}

public class TestClass
{
    public void Caller()
    {
        var l = new LinkedList();
        l?.Next?.Next?.Next?.Next?.ToString();
    }
##
    private string Callee(LinkedList l)
    {
        return l?.Next?.Next?.Next?.Next?.ToString();
    }
##}
");

        [Fact]
        public Task TestThrowStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
using System;
public class TestClass
{
    public void Caller()
    {
        Call[||]ee();
    }

    private string Callee()
    {
        throw new Exception();
    }
}
", @"
using System;
public class TestClass
{
    public void Caller()
    {
        throw new Exception();
    }
##
    private string Callee()
    {
        throw new Exception();
    }
##}
");

        [Fact]
        public Task TestThrowExpressionToThrowStatement()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
using System;
public class TestClass
{
    public void Caller()
    {
        Call[||]ee();
    }

    private string Callee() => throw new Exception();
}
", @"
using System;
public class TestClass
{
    public void Caller()
    {
        throw new Exception();
    }
##
    private string Callee() => throw new Exception();
##}
");

        [Fact]
        public Task TestThrowExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
using System;
public class TestClass
{
    public void Caller(bool a)
    {
        var x = a ? Call[||]ee() : ""Hello"";
    }

    private string Callee() => throw new Exception();
}
", @"
using System;
public class TestClass
{
    public void Caller(bool a)
    {
        var x = a ? throw new Exception() : ""Hello"";
    }
##
    private string Callee() => throw new Exception();
##}
");

        [Fact]
        public Task TestThrowStatementToThrowExpression()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
using System;
public class TestClass
{
    public void Caller(bool a)
    {
        var x = a ? Call[||]ee() : ""Hello"";
    }

    private string Callee()
    {
        throw new Exception();
    }
}
", @"
using System;
public class TestClass
{
    public void Caller(bool a)
    {
        var x = a ? throw new Exception() : ""Hello"";
    }
##
    private string Callee()
    {
        throw new Exception();
    }
##}
");

        [Fact]
        public Task TestWriteSingleParameter()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    public void Caller(bool a)
    {
        var x = C[||]allee(a ? 10 : 100);
    }

    private int Callee(int c)
    {
        return c = 1000;
    }
}
", @"
public class TestClass
{
    public void Caller(bool a)
    {
        int c = a ? 10 : 100;
        var x = c = 1000;
    }
##
    private int Callee(int c)
    {
        return c = 1000;
    }
##}
");

        [Fact]
        public Task TestReadMultipleTimesForParameter()
            => TestVerifier.TestBothKeepAndRemoveInlinedMethodInSameFileAsync(@"
public class TestClass
{
    public void Caller(bool a)
    {
        var x = C[||]allee(a ? 10 : 100, a);
    }

    private int Callee(int c, bool a)
    {
        return a ? c + 1000 : c + 10000;
    }
}
", @"
public class TestClass
{
    public void Caller(bool a)
    {
        int c = a ? 10 : 100;
        var x = a ? c + 1000 : c + 10000;
    }
##
    private int Callee(int c, bool a)
    {
        return a ? c + 1000 : c + 10000;
    }
##}
");
    }
}
