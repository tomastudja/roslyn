﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.CSharp.UsePatternCombinators;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.UsePatternCombinators
{
    public class CSharpUsePatternCombinatorsDiagnosticAnalyzerTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        private static readonly ParseOptions CSharp9 = TestOptions.RegularPreview;

        internal override (DiagnosticAnalyzer, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
            => (new CSharpUsePatternCombinatorsDiagnosticAnalyzer(), new CSharpUsePatternCombinatorsCodeFixProvider());

        private Task TestMissingInCSharp9Async(string initialMarkup)
            => TestMissingAsync(initialMarkup, new TestParameters(CSharp9));

        private Task TestInCSharp9Async(string initialMarkup, string expectedMarkup)
            => TestInRegularAndScriptAsync(initialMarkup, expectedMarkup, parseOptions: CSharp9);

        private static readonly string s_initialMarkup = @"
using System;
using System.Collections.Generic;
class C
{
    bool field = {|FixAllInDocument:EXPRESSION|};
    bool Method() => EXPRESSION;
    bool Prop1 => EXPRESSION;
    bool Prop2 { get } = EXPRESSION;
    void If() { if (EXPRESSION) ; }
    void Argument1() => Test(EXPRESSION);
    void Argument2() => Test(() => EXPRESSION);
    void Argument3() => Test(_ => EXPRESSION);
    void For() { for (; EXPRESSION; ); }
    void Local() { var local = EXPRESSION; }
    void Conditional() { _ = EXPRESSION ? true : false; }
    void Assignment() { _ = EXPRESSION; }
    void Do() { do ; while (EXPRESSION); }
    void While() { while (EXPRESSION) ; }
    bool When() => o switch { _ when EXPRESSION => EXPRESSION };
    bool Return() { return EXPRESSION; }
    IEnumerable<bool> YieldReturn() { yield return EXPRESSION; }
    Func<object, bool> SimpleLambda() => o => EXPRESSION;
    Func<bool> ParenthesizedLambda() => () => EXPRESSION;
    int i;
    object o;
}
";

        [InlineData("i == 0")]
        [InlineData("i > 0")]
        [InlineData("o is C")]
        [InlineData("o is C c")]
        [InlineData("o != null")]
        [InlineData("!(o is C c)")]
        [InlineData("!(o is null)")]
        [InlineData("o is int ii || o is long jj")]
        [Theory, Trait(Traits.Feature, Traits.Features.CodeActionsUsePatternCombinators)]
        public async Task TestMissingOnExpression(string expression)
        {
            await TestMissingInCSharp9Async(s_initialMarkup.Replace("EXPRESSION", expression));
        }

        [InlineData("o is int ii && o is long jj", "o is int ii and long jj")]
        [InlineData("!(o is C)", "o is not C")]
        [InlineData("!(o is C _)", "o is not C _")]
        [InlineData("i == 1 || 2 == i", "i is 1 or 2")]
        [InlineData("i != 1 || 2 != i", "i is not (1 and 2)")]
        [InlineData("i != 1 && 2 != i", "i is not (1 or 2)")]
        [InlineData("!(i != 1 && 2 != i)", "i is 1 or 2")]
        [InlineData("i < 1 && 2 <= i", "i is < 1 and >= 2")]
        [InlineData("i < 1 && 2 <= i && i is not 0", "i is < 1 and >= 2 and not 0")]
        [InlineData("(int.MaxValue - 1D) < i && i > 0", "i is > (int.MaxValue - 1D) and > 0")]
        [Theory, Trait(Traits.Feature, Traits.Features.CodeActionsUsePatternCombinators)]
        public async Task TestOnExpression(string expression, string expected)
        {
            await TestInCSharp9Async(
                s_initialMarkup.Replace("EXPRESSION", expression),
                s_initialMarkup.Replace("EXPRESSION", expected));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUsePatternCombinators)]
        public async Task TestMultiline()
        {
            await TestInCSharp9Async(
@"class C
{
    bool M0(int variable)
    {
        return {|FixAllInDocument:variable == 0 ||
               variable == 1 ||
               variable == 2|};
    }
    bool M1(int variable)
    {
        return variable != 0 &&
               variable != 1 &&
               variable != 2;
    }
}",
@"class C
{
    bool M0(int variable)
    {
        return variable is 0 or
               1 or
               2;
    }
    bool M1(int variable)
    {
        return variable is not (0 or
               1 or
               2);
    }
}");
        }
    }
}
