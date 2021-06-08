﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.UseIsNullCheck;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.UseIsNullCheck
{
    using VerifyCS = CSharpCodeFixVerifier<CSharpUseIsNullCheckOverIsObjectDiagnosticAnalyzer, Testing.EmptyCodeFixProvider>;

    [Trait(Traits.Feature, Traits.Features.CodeActionsUseIsNullCheck)]
    public class UseIsNullCheckOverIsObjectTests
    {
        private static async Task VerifyAsync(string source, string fixedSource, LanguageVersion languageVersion)
        {
            await new VerifyCS.Test
            {
                TestCode = source,
                FixedCode = fixedSource,
                LanguageVersion = languageVersion,
            }.RunAsync();
        }

        private static async Task VerifyCSharp9Async(string source, string fixedSource)
            => await VerifyAsync(source, fixedSource, LanguageVersion.CSharp9);

        private static async Task VerifyCSharp8Async(string source, string fixedSource)
            => await VerifyAsync(source, fixedSource, LanguageVersion.CSharp8);

        [Fact]
        public async Task TestIsObjectCSharp8()
        {
            var source = @"
public class C
{
    public bool M(string value)
    {
        return value is object;
    }
}
";
            await VerifyCSharp8Async(source, source);
        }

        [Fact]
        public async Task TestIsObject()
        {
            var source = @"
public class C
{
    public bool M(string value)
    {
        return [|value is object|];
    }
}
";
            var fixedSource = @"
public class C
{
    public bool M(string value)
    {
        return value is not null;
    }
}
";
            await VerifyCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task TestIsNotObject()
        {
            var source = @"
public class C
{
    public bool M(string value)
    {
        return value is [|not object|];
    }
}
";
            var fixedSource = @"
public class C
{
    public bool M(string value)
    {
        return value is null;
    }
}
";
            await VerifyCSharp9Async(source, fixedSource);
        }

        [Fact]
        public async Task TestIsStringAgainstObject_NoDiagnostic()
        {
            var source = @"
public class C
{
    public bool M(object value)
    {
        return value is string;
    }
}
";
            await VerifyCSharp9Async(source, source);
        }

        [Fact]
        public async Task TestIsStringAgainstString()
        {
            // Currently no diagnostic, but a diagnostic is reasonable too.
            var source = @"
public class C
{
    public bool M(string value)
    {
        return value is string;
    }
}
";
            await VerifyCSharp9Async(source, source);
        }

        [Fact]
        public async Task TestIsNotStringAgainstObject_NoDiagnostic()
        {
            var source = @"
public class C
{
    public bool M(object value)
    {
        return value is string;
    }
}
";
            await VerifyCSharp9Async(source, source);
        }

        [Fact]
        public async Task TestIsNotStringAgainstString()
        {
            // Currently no diagnostic, but a diagnostic is reasonable too.
            var source = @"
public class C
{
    public bool M(string value)
    {
        return value is not string;
    }
}
";
            await VerifyCSharp9Async(source, source);
        }
    }
}
