// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Scripting;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;
using System.IO;
using System.Globalization;
using System.Text;

namespace Microsoft.CodeAnalysis.CSharp.Scripting.UnitTests
{
    public class ScriptTests : TestBase
    {
        public class Globals
        {
            public int X;
            public int Y;
        }

        [Fact]
        public void TestCreateScript()
        {
            var script = CSharpScript.Create("1 + 2");
            Assert.Equal("1 + 2", script.Code);
        }

        [Fact]
        public async Task TestGetCompilation()
        {
            var state = await CSharpScript.RunAsync("1 + 2", globals: new ScriptTests());
            var compilation = state.Script.GetCompilation();
            Assert.Equal(state.Script.Code, compilation.SyntaxTrees.First().GetText().ToString());
        }

        [Fact]
        public void TestCreateScriptDelegate()
        {
            // create a delegate for the entire script
            var script = CSharpScript.Create("1 + 2");
            var fn = script.CreateDelegate();

            Assert.Equal(3, fn().Result);
            Assert.ThrowsAsync<ArgumentException>("globals", () => fn(new object()));
        }

        [Fact]
        public void TestCreateScriptDelegateWithGlobals()
        {
            // create a delegate for the entire script
            var script = CSharpScript.Create<int>("X + Y", globalsType: typeof(Globals));
            var fn = script.CreateDelegate();

            Assert.ThrowsAsync<ArgumentException>("globals", () => fn());
            Assert.ThrowsAsync<ArgumentException>("globals", () => fn(new object()));
            Assert.Equal(4, fn(new Globals { X = 1, Y = 3 }).Result);
        }

        [Fact]
        public async Task TestRunScript()
        {
            var state = await CSharpScript.RunAsync("1 + 2");
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestCreateAndRunScript()
        {
            var script = CSharpScript.Create("1 + 2");
            var state = await script.RunAsync();
            Assert.Same(script, state.Script);
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestEvalScript()
        {
            var value = await CSharpScript.EvaluateAsync("1 + 2");
            Assert.Equal(3, value);
        }

        [Fact]
        public async Task TestRunScriptWithSpecifiedReturnType()
        {
            var state = await CSharpScript.RunAsync("1 + 2");
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestRunVoidScript()
        {
            using (var redirect = new OutputRedirect(CultureInfo.InvariantCulture))
            {
                var state = await CSharpScript.RunAsync("System.Console.WriteLine(0);");
                Assert.Null(state.ReturnValue);
            }
        }

        [WorkItem(5279, "https://github.com/dotnet/roslyn/issues/5279")]
        [Fact]
        public async void TestRunExpressionStatement()
        {
            var state = await CSharpScript.RunAsync(
@"int F() { return 1; }
F();");
            Assert.Null(state.ReturnValue);
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/170")]
        public void TestRunDynamicVoidScriptWithTerminatingSemicolon()
        {
            var result = CSharpScript.RunAsync(@"
class SomeClass
{
    public void Do()
    {
    }
}
dynamic d = new SomeClass();
d.Do();"
, ScriptOptions.Default.WithReferences(MscorlibRef, SystemRef, SystemCoreRef, CSharpRef));
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/170")]
        public void TestRunDynamicVoidScriptWithoutTerminatingSemicolon()
        {
            var result = CSharpScript.RunAsync(@"
class SomeClass
{
    public void Do()
    {
    }
}
dynamic d = new SomeClass();
d.Do()"
, ScriptOptions.Default.WithReferences(MscorlibRef, SystemRef, SystemCoreRef, CSharpRef));
        }

        [Fact]
        public async Task TestRunScriptWithGlobals()
        {
            var state = await CSharpScript.RunAsync("X + Y", globals: new Globals { X = 1, Y = 2 });
            Assert.Equal(3, state.ReturnValue);
        }

        [Fact]
        public async Task TestRunCreatedScriptWithExpectedGlobals()
        {
            var script = CSharpScript.Create("X + Y", globalsType: typeof(Globals));
            var state = await script.RunAsync(new Globals { X = 1, Y = 2 });
            Assert.Equal(3, state.ReturnValue);
            Assert.Same(script, state.Script);
        }

        [Fact]
        public void TestRunCreatedScriptWithUnexpectedGlobals()
        {
            var script = CSharpScript.Create("X + Y");

            // Global variables passed to a script without a global type
            Assert.ThrowsAsync<ArgumentException>("globals", () => script.RunAsync(new Globals { X = 1, Y = 2 }));
        }

        [Fact]
        public void TestRunCreatedScriptWithoutGlobals()
        {
            var script = CSharpScript.Create("X + Y", globalsType: typeof(Globals));

            //  The script requires access to global variables but none were given
            Assert.ThrowsAsync<ArgumentException>("globals", () => script.RunAsync());
        }

        [Fact]
        public void TestRunCreatedScriptWithMismatchedGlobals()
        {
            var script = CSharpScript.Create("X + Y", globalsType: typeof(Globals));

            //  The globals of type 'System.Object' is not assignable to 'Microsoft.CodeAnalysis.CSharp.Scripting.Test.ScriptTests+Globals'
            Assert.ThrowsAsync<ArgumentException>("globals", () => script.RunAsync(new object()));
        }

        [Fact]
        public async Task ContinueAsync_Error1()
        {
            var state = await CSharpScript.RunAsync("X + Y", globals: new Globals());

            await Assert.ThrowsAsync<ArgumentNullException>("previousState", () => state.Script.RunFromAsync(null));
        }

        [Fact]
        public async Task ContinueAsync_Error2()
        {
            var state1 = await CSharpScript.RunAsync("X + Y + 1", globals: new Globals());
            var state2 = await CSharpScript.RunAsync("X + Y + 2", globals: new Globals());

            await Assert.ThrowsAsync<ArgumentException>("previousState", () => state1.Script.RunFromAsync(state2));
        }

        [Fact]
        public async Task TestRunScriptWithScriptState()
        {
            // run a script using another scripts end state as the starting state (globals)
            var state = await CSharpScript.RunAsync("int X = 100;").ContinueWith("X + X");
            Assert.Equal(200, state.ReturnValue);
        }

        [Fact]
        public async Task TestRepl()
        {
            string[] submissions = new[]
            {
                "int x = 100;",
                "int y = x * x;",
                "x + y"
            };

            var state = await CSharpScript.RunAsync("");
            foreach (var submission in submissions)
            {
                state = await state.ContinueWithAsync(submission);
            }

            Assert.Equal(10100, state.ReturnValue);
        }

#if TODO // https://github.com/dotnet/roslyn/issues/3720
        [Fact]
        public void TestCreateMethodDelegate()
        {
            // create a delegate to a method declared in the script
            var state = CSharpScript.Run("int Times(int x) { return x * x; }");
            var fn = state.CreateDelegate<Func<int, int>>("Times");
            var result = fn(5);
            Assert.Equal(25, result);
        }
#endif

        [Fact]
        public async Task ScriptVariables_Chain()
        {
            var globals = new Globals { X = 10, Y = 20 };

            var script =
                CSharpScript.Create(
                    "var a = '1';",
                    globalsType: globals.GetType()).
                ContinueWith("var b = 2u;").
                ContinueWith("var a = 3m;").
                ContinueWith("var x = a + b;").
                ContinueWith("var X = Y;");

            var state = await script.RunAsync(globals);

            AssertEx.Equal(new[] { "a", "b", "a", "x", "X" }, state.Variables.Select(v => v.Name));
            AssertEx.Equal(new object[] { '1', 2u, 3m, 5m, 20 }, state.Variables.Select(v => v.Value));
            AssertEx.Equal(new Type[] { typeof(char), typeof(uint), typeof(decimal), typeof(decimal), typeof(int) }, state.Variables.Select(v => v.Type));

            Assert.Equal(3m, state.GetVariable("a").Value);
            Assert.Equal(2u, state.GetVariable("b").Value);
            Assert.Equal(5m, state.GetVariable("x").Value);
            Assert.Equal(20, state.GetVariable("X").Value);

            Assert.Equal(null, state.GetVariable("A"));
            Assert.Same(state.GetVariable("X"), state.GetVariable("X"));
        }

        [Fact]
        public async Task ScriptVariable_SetValue()
        {
            var script = CSharpScript.Create("var x = 1;");

            var s1 = await script.RunAsync();
            s1.GetVariable("x").Value = 2;
            Assert.Equal(2, s1.GetVariable("x").Value);

            // rerunning the script from the beginning rebuilds the state:
            var s2 = await s1.Script.RunAsync();
            Assert.Equal(1, s2.GetVariable("x").Value);

            // continuing preserves the state:
            var s3 = await s1.ContinueWithAsync("x");
            Assert.Equal(2, s3.GetVariable("x").Value);
            Assert.Equal(2, s3.ReturnValue);
        }

        [Fact]
        public async Task ScriptVariable_SetValue_Errors()
        {
            var state = await CSharpScript.RunAsync(@"
var x = 1;
readonly var y = 2;
const int z = 3;
");

            Assert.False(state.GetVariable("x").IsReadOnly);
            Assert.True(state.GetVariable("y").IsReadOnly);
            Assert.True(state.GetVariable("z").IsReadOnly);

            Assert.Throws<ArgumentException>(() => state.GetVariable("x").Value = "str");
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("y").Value = "str");
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("z").Value = "str");
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("y").Value = 0);
            Assert.Throws<InvalidOperationException>(() => state.GetVariable("z").Value = 0);
        }

        [Fact]
        public async Task TestBranchingSubscripts()
        {
            // run script to create declaration of M
            var state1 = await CSharpScript.RunAsync("int M(int x) { return x + x; }");

            // run second script starting from first script's end state
            // this script's new declaration should hide the old declaration
            var state2 = await state1.ContinueWithAsync("int M(int x) { return x * x; } M(5)");
            Assert.Equal(25, state2.ReturnValue);

            // run third script also starting from first script's end state
            // it should not see any declarations made by the second script.
            var state3 = await state1.ContinueWithAsync("M(5)");
            Assert.Equal(10, state3.ReturnValue);
        }

        [Fact]
        public async Task ReturnIntAsObject()
        {
            var expected = 42;
            var script = CSharpScript.Create<object>($"return {expected};");
            var result = await script.EvaluateAsync();
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task NoReturn()
        {
            using (var redirect = new OutputRedirect(CultureInfo.InvariantCulture))
            {
                var script = CSharpScript.Create<object>("System.Console.WriteLine();");
                var result = await script.EvaluateAsync();
                Assert.Null(result);
            }
        }

        [Fact]
        public async Task ReturnAwait()
        {
            var script = CSharpScript.Create<int>("return await System.Threading.Tasks.Task.FromResult(42);");
            var result = await script.EvaluateAsync();
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ReturnInNestedScopeNoTrailingExpression()
        {
            var script = CSharpScript.Create(@"
bool condition = false;
if (condition)
{
    return 1;
}");
            var result = await script.EvaluateAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnInNestedScopeWithTrailingVoidExpression()
        {
            var script = CSharpScript.Create(@"
bool condition = false;
if (condition)
{
    return 1;
}
System.Console.WriteLine();");
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
bool condition = true;
if (condition)
{
    return 1;
}
System.Console.WriteLine();");

            using (var redirect = new OutputRedirect(CultureInfo.InvariantCulture))
            {
                result = await script.EvaluateAsync();
                Assert.Equal(1, result);
            }
        }

        [Fact]
        public async Task ReturnInNestedScopeWithTrailingVoidExpressionAsInt()
        {
            var script = CSharpScript.Create<int>(@"
bool condition = false;
if (condition)
{
    return 1;
}
System.Console.WriteLine();");
            var result = await script.EvaluateAsync();
            Assert.Equal(0, result);

            script = CSharpScript.Create<int>(@"
bool condition = false;
if (condition)
{
    return 1;
}
System.Console.WriteLine()");

            using (var redirect = new OutputRedirect(CultureInfo.InvariantCulture))
            {
                result = await script.EvaluateAsync();
                Assert.Equal(0, result);
            }
        }

        [Fact]
        public async Task ReturnIntWithTrailingDoubleExpression()
        {
            var script = CSharpScript.Create(@"
bool condition = false;
if (condition)
{
    return 1;
}
1.1");
            var result = await script.EvaluateAsync();
            Assert.Equal(1.1, result);

            script = CSharpScript.Create(@"
bool condition = true;
if (condition)
{
    return 1;
}
1.1");
            result = await script.EvaluateAsync();
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task ReturnGenericAsInterface()
        {
            var script = CSharpScript.Create<IEnumerable<int>>(@"
if (false)
{
    return new System.Collections.Generic.List<int> { 1, 2, 3 };
}");
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create<IEnumerable<int>>(@"
if (true)
{
    return new System.Collections.Generic.List<int> { 1, 2, 3 };
}");
            result = await script.EvaluateAsync();
            Assert.Equal(new List<int> { 1, 2, 3 }, result);
        }

        [Fact]
        public async Task ReturnNullable()
        {
            var script = CSharpScript.Create<int?>(@"
if (false)
{
    return 42;
}");
            var result = await script.EvaluateAsync();
            Assert.False(result.HasValue);

            script = CSharpScript.Create<int?>(@"
if (true)
{
    return 42;
}");
            result = await script.EvaluateAsync();
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ReturnInLoadedFile()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 42;"));
            var options = ScriptOptions.Default.WithSourceResolver(resolver);

            var script = CSharpScript.Create("#load \"a.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Equal(42, result);

            script = CSharpScript.Create(@"
#load ""a.csx""
-1", options);
            result = await script.EvaluateAsync();
            Assert.Equal(42, result);
        }

        [Fact]
        public async Task ReturnInLoadedFileTrailingExpression()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", @"
if (false)
{
    return 42;
}
1"));
            var options = ScriptOptions.Default.WithSourceResolver(resolver);

            var script = CSharpScript.Create("#load \"a.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
#load ""a.csx""
2", options);
            result = await script.EvaluateAsync();
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task ReturnInLoadedFileTrailingVoidExpression()
        {
            using (var redirect = new OutputRedirect(CultureInfo.InvariantCulture))
            {
                var resolver = TestSourceReferenceResolver.Create(
                    KeyValuePair.Create("a.csx", @"
if (false)
{
    return 1;
}
System.Console.WriteLine(42)"));
                var options = ScriptOptions.Default.WithSourceResolver(resolver);

                var script = CSharpScript.Create("#load \"a.csx\"", options);
                var result = await script.EvaluateAsync();
                Assert.Null(result);

                script = CSharpScript.Create(@"
#load ""a.csx""
2", options);
                result = await script.EvaluateAsync();
                Assert.Equal(2, result);
            }
        }

        [Fact]
        public async Task MultipleLoadedFilesWithTrailingExpression()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "1"),
                KeyValuePair.Create("b.csx", @"
#load ""a.csx""
2"));
            var options = ScriptOptions.Default.WithSourceResolver(resolver);
            var script = CSharpScript.Create("#load \"b.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "1"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.Default.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""", options);
            result = await script.EvaluateAsync();
            Assert.Null(result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "1"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.Default.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""
3", options);
            result = await script.EvaluateAsync();
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task MultipleLoadedFilesWithReturnAndTrailingExpression()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 1;"),
                KeyValuePair.Create("b.csx", @"
#load ""a.csx""
2"));
            var options = ScriptOptions.Default.WithSourceResolver(resolver);
            var script = CSharpScript.Create("#load \"b.csx\"", options);
            var result = await script.EvaluateAsync();
            Assert.Equal(1, result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 1;"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.Default.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""", options);
            result = await script.EvaluateAsync();
            Assert.Equal(1, result);

            resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", "return 1;"),
                KeyValuePair.Create("b.csx", "2"));
            options = ScriptOptions.Default.WithSourceResolver(resolver);
            script = CSharpScript.Create(@"
#load ""a.csx""
#load ""b.csx""
return 3;", options);
            result = await script.EvaluateAsync();
            Assert.Equal(1, result);
        }

        [Fact]
        public async Task LoadedFileWithReturnAndGoto()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", @"
goto EOF;
NEXT:
return 1;
EOF:;
2"));
            var options = ScriptOptions.Default.WithSourceResolver(resolver);

            var script = CSharpScript.Create(@"
#load ""a.csx""
goto NEXT;
return 3;
NEXT:;", options);
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
#load ""a.csx""
L1: goto EOF;
L2: return 3;
EOF:
EOF2: ;
4", options);
            result = await script.EvaluateAsync();
            Assert.Equal(4, result);
        }

        [Fact]
        public async Task VoidReturn()
        {
            var script = CSharpScript.Create("return;");
            var result = await script.EvaluateAsync();
            Assert.Null(result);

            script = CSharpScript.Create(@"
var b = true;
if (b)
{
    return;
}
b");
            result = await script.EvaluateAsync();
            Assert.Null(result);
        }

        [Fact]
        public async Task LoadedFileWithVoidReturn()
        {
            var resolver = TestSourceReferenceResolver.Create(
                KeyValuePair.Create("a.csx", @"
var i = 42;
return;
i = -1;"));
            var options = ScriptOptions.Default.WithSourceResolver(resolver);
            var script = CSharpScript.Create<int>(@"
#load ""a.csx""
i", options);
            var result = await script.EvaluateAsync();
            Assert.Equal(0, result);
        }

        [Fact]
        public async Task ScriptOptionsConfiguredForDebuggingResultInPdbEmitted()
        {
            try
            {
                var opts = ScriptOptions.Default.WithEmitDebugInformation(true);
                var script = await CSharpScript.RunAsync("throw new System.Exception();", opts);
            }
            catch (Exception ex)
            {
                // line information is only available when PDBs have been emitted
                Assert.Equal("at Submission#0.<<Initialize>>d__0.MoveNext() in :line 1", GetStackTraceLine(ex, 0));
            }
        }

        [Fact]
        public async Task ScriptOptionsNotConfiguredForDebuggingResultInPdbNotEmitted()
        {
            try
            {
                var script = await CSharpScript.RunAsync("throw new System.Exception();", ScriptOptions.Default);
            }
            catch (Exception ex)
            {
                // line information is only available when PDBs have been emitted
                Assert.Equal("at Submission#0.<<Initialize>>d__0.MoveNext()", GetStackTraceLine(ex, 0));
            }
        }

        [WorkItem(12348, "https://github.com/dotnet/roslyn/issues/12348")]
        [Fact]
        public async Task StreamWithOffset()
        {
            var resolver = new StreamOffsetResolver();
            var options = ScriptOptions.Default.WithSourceResolver(resolver);
            var script = CSharpScript.Create(@"#load ""a.csx""", options);
            using (var redirect = new OutputRedirect(CultureInfo.InvariantCulture))
            {
                await script.EvaluateAsync();
            }
        }

        private class StreamOffsetResolver : SourceReferenceResolver
        {
            public override bool Equals(object other) => ReferenceEquals(this, other);
            public override int GetHashCode() => 42;
            public override string ResolveReference(string path, string baseFilePath) => path;
            public override string NormalizePath(string path, string baseFilePath) => path;

            public override Stream OpenRead(string resolvedPath)
            {
                // Make an ASCII text buffer with Hello World script preceded by padding Qs
                const int padding = 42;
                string text = @"System.Console.WriteLine(""Hello World!"");";
                byte[] bytes = Enumerable.Repeat((byte)'Q', text.Length + padding).ToArray();
                System.Text.Encoding.ASCII.GetBytes(text, 0, text.Length, bytes, padding);

                // Make a stream over the program portion, skipping the Qs.
                var stream = new MemoryStream(
                    bytes,
                    padding,
                    text.Length,
                    writable: false,
                    publiclyVisible: true);

                // sanity check that reading entire stream gives us back our text.
                using (var streamReader = new StreamReader(
                    stream,
                    System.Text.Encoding.ASCII,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: bytes.Length,
                    leaveOpen: true))
                {
                    var textFromStream = streamReader.ReadToEnd();
                    Assert.Equal(textFromStream, text);
                }

                stream.Position = 0;
                return stream;
            }
        }

        private string GetStackTraceLine(Exception ex, int index)
        {
            if (ex == null || ex.StackTrace == null) return null;

            var stackTrace = ex.StackTrace?.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            if (stackTrace.Length >= index)
            {
                return stackTrace[index].Trim();
            }

            return null;
        }
    }
}
