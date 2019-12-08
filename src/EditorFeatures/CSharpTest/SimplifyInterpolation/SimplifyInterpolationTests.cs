﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.SimplifyInterpolation;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.SimplifyInterpolation
{
    [Trait(Traits.Feature, Traits.Features.CodeActionsSimplifyInterpolation)]
    public partial class SimplifyInterpolationTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        internal override (DiagnosticAnalyzer, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
            => (new CSharpSimplifyInterpolationDiagnosticAnalyzer(), new CSharpSimplifyInterpolationCodeFixProvider());

        [Fact]
        public async Task ToStringWithNoParameter()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString()|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithStringLiteralParameter()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString(""|}some format code{|Unnecessary:"")|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue:some format code} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithEscapeSequences()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString(""|}\\d \""d\""{|Unnecessary:"")|}} suffix"";
    }
}",
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $""prefix {someValue:\\d \""d\""} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithVerbatimStringLiteralParameter()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString(@""|}some format code{|Unnecessary:"")|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue:some format code} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithVerbatimEscapeSequencesInsideVerbatimInterpolatedString()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $@""prefix {someValue{|Unnecessary:[||].ToString(@""|}\d """"d""""{|Unnecessary:"")|}} suffix"";
    }
}",
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $@""prefix {someValue:\d """"d""""} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithVerbatimEscapeSequencesInsideNonVerbatimInterpolatedString()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString(@""|}\d """"d""""{|Unnecessary:"")|}} suffix"";
    }
}",
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $""prefix {someValue:\\d \""d\""} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithNonVerbatimEscapeSequencesInsideVerbatimInterpolatedString()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $@""prefix {someValue{|Unnecessary:[||].ToString(""|}\\d \""d\""{|Unnecessary:"")|}} suffix"";
    }
}",
@"class C
{
    void M(System.DateTime someValue)
    {
        _ = $@""prefix {someValue:\d """"d""""} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithStringConstantParameter()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        const string someConst = ""some format code"";
        _ = $""prefix {someValue[||].ToString(someConst)} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithCharacterLiteralParameter()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(C someValue)
    {
        _ = $""prefix {someValue[||].ToString('f')} suffix"";
    }

    public string ToString(object obj) => null;
}");
        }

        [Fact]
        public async Task ToStringWithFormatProvider()
        {
            // (If someone is explicitly specifying culture, an implicit form should not be encouraged.)

            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].ToString(""some format code"", System.Globalization.CultureInfo.CurrentCulture)} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftWithIntegerLiteral()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].PadLeft(|}3{|Unnecessary:)|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,-3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadRightWithIntegerLiteral()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].PadRight(|}3{|Unnecessary:)|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftWithComplexConstantExpressionRequiringParentheses()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        const int someConstant = 1;
        _ = $""prefix {someValue{|Unnecessary:[||].PadLeft(|}(byte)3.3 + someConstant{|Unnecessary:)|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,-((byte)3.3 + someConstant)} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftWithSpaceChar()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].PadLeft(|}3{|Unnecessary:, ' ')|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,-3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadRightWithSpaceChar()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].PadRight(|}3{|Unnecessary:, ' ')|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftWithNonSpaceChar()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadLeft(3, '\t')} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadRightWithNonSpaceChar()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadRight(3, '\t')} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadRightWithComplexConstantExpression()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        const int someConstant = 1;
        _ = $""prefix {someValue{|Unnecessary:[||].PadRight(|}(byte)3.3 + someConstant{|Unnecessary:)|}} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,(byte)3.3 + someConstant} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithNoParameterWhenFormattingComponentIsSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].ToString():foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithStringLiteralParameterWhenFormattingComponentIsSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].ToString(""bar""):foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithNoParameterWhenAlignmentComponentIsSpecified()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString()|},3} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,3} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithStringLiteralParameterWhenAlignmentComponentIsSpecified()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString(""|}some format code{|Unnecessary:"")|},3} suffix"";
    }
}",
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,3:some format code} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithNoParameterWhenBothComponentsAreSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].ToString(),3:foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithStringLiteralParameterWhenBothComponentsAreSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].ToString(""some format code""),3:foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftWhenFormattingComponentIsSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadLeft(3):foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadRightWhenFormattingComponentIsSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadRight(3):foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftWhenAlignmentComponentIsSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadLeft(3),3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadRightWhenAlignmentComponentIsSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadRight(3),3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftWhenBothComponentsAreSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadLeft(3),3:foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadRightWhenBothComponentsAreSpecified()
        {
            await TestMissingInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue[||].PadRight(3),3:foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithoutFormatThenPadLeft()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString().PadLeft(|}3{|Unnecessary:)|}} suffix"";
    }
}", @"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,-3} suffix"";
    }
}");
        }

        [Fact]
        public async Task ToStringWithFormatThenPadLeft()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].ToString(""|}foo{|Unnecessary:"").PadLeft(|}3{|Unnecessary:)|}} suffix"";
    }
}", @"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,-3:foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftThenToStringWithoutFormat()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue{|Unnecessary:[||].PadLeft(|}3{|Unnecessary:).ToString()|}} suffix"";
    }
}", @"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,-3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftThenToStringWithFormat()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue.PadLeft(3){|Unnecessary:[||].ToString(""|}foo{|Unnecessary:"")|}} suffix"";
    }
}", @"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue.PadLeft(3):foo} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftThenToStringWithoutFormatWhenAlignmentComponentIsSpecified()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue.PadLeft(3){|Unnecessary:[||].ToString()|},3} suffix"";
    }
}", @"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue,3} suffix"";
    }
}");
        }

        [Fact]
        public async Task PadLeftThenToStringWithFormatWhenAlignmentComponentIsSpecified()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue.PadLeft(3){|Unnecessary:[||].ToString(""|}foo{|Unnecessary:"")|},3} suffix"";
    }
}", @"class C
{
    void M(int someValue)
    {
        _ = $""prefix {someValue.PadLeft(3),3:foo} suffix"";
    }
}");
        }
    }
}
