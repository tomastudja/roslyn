﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.UseExpressionBody;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.UseExpressionBody
{
    using VerifyCS = CSharpCodeFixVerifier<
        UseExpressionBodyDiagnosticAnalyzer,
        UseExpressionBodyCodeFixProvider>;

    public class UseExpressionBodyForPropertiesAnalyzerTests
    {
        private static async Task TestWithUseExpressionBody(string code, string fixedCode, LanguageVersion version = LanguageVersion.CSharp8)
        {
            await new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                LanguageVersion = version,
                Options =
                {
                    { CSharpCodeStyleOptions.PreferExpressionBodiedProperties, CSharpCodeStyleOptions.WhenPossibleWithSilentEnforcement },
                    { CSharpCodeStyleOptions.PreferExpressionBodiedAccessors, CSharpCodeStyleOptions.NeverWithSilentEnforcement },
                }
            }.RunAsync();
        }

        private static async Task TestWithUseBlockBody(string code, string fixedCode)
        {
            await new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                Options =
                {
                    { CSharpCodeStyleOptions.PreferExpressionBodiedProperties, CSharpCodeStyleOptions.NeverWithSilentEnforcement },
                    { CSharpCodeStyleOptions.PreferExpressionBodiedAccessors, CSharpCodeStyleOptions.NeverWithSilentEnforcement },
                }
            }.RunAsync();
        }

        private static async Task TestWithUseBlockBodyExceptAccessor(string code, string fixedCode)
        {
            await new VerifyCS.Test
            {
                TestCode = code,
                FixedCode = fixedCode,
                Options =
                {
                    { CSharpCodeStyleOptions.PreferExpressionBodiedProperties, CSharpCodeStyleOptions.NeverWithSilentEnforcement },
                    { CSharpCodeStyleOptions.PreferExpressionBodiedAccessors, CSharpCodeStyleOptions.WhenPossibleWithSilentEnforcement },
                }
            }.RunAsync();
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseExpressionBody1()
        {
            var code = @"
class C
{
    int Goo
    {
        get
        {
            [|return|] Bar();
        }
    }
}";
            var fixedCode = @"
class C
{
    int Goo => Bar();
}";
            await TestWithUseExpressionBody(code, fixedCode);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestMissingWithSetter()
        {
            var code = @"
class C
{
    int Goo
    {
        get
        {
            return Bar();
        }

        set
        {
        }
    }
}";
            await TestWithUseExpressionBody(code, code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestMissingWithAttribute()
        {
            var code = @"
class C
{
    int Goo
    {
        [A]
        get
        {
            return Bar();
        }
    }
}";
            await TestWithUseExpressionBody(code, code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestMissingOnSetter1()
        {
            var code = @"
class C
{
    int Goo
    {
        set
        {
            Bar();
        }
    }
}";
            await TestWithUseExpressionBody(code, code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseExpressionBody3()
        {
            var code = @"
class C
{
    int Goo
    {
        get
        {
            [|throw|] new NotImplementedException();
        }
    }
}";
            var fixedCode = @"
class C
{
    int Goo => throw new NotImplementedException();
}";
            await TestWithUseExpressionBody(code, fixedCode);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseExpressionBody4()
        {
            var code = @"
class C
{
    int Goo
    {
        get
        {
            [|throw|] new NotImplementedException(); // comment
        }
    }
}";
            var fixedCode = @"
class C
{
    int Goo => throw new NotImplementedException(); // comment
}";
            await TestWithUseExpressionBody(code, fixedCode);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseBlockBody1()
        {
            var code = @"
class C
{
    int Goo [|=>|] Bar();
}";
            var fixedCode = @"
class C
{
    int Goo
    {
        get
        {
            return Bar();
        }
    }
}";
            await TestWithUseBlockBody(code, fixedCode);
        }

        [WorkItem(20363, "https://github.com/dotnet/roslyn/issues/20363")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseBlockBodyForAccessorEventWhenAccessorWantExpression1()
        {
            var code = @"
class C
{
    int Goo [|=>|] Bar();
}";
            var fixedCode = @"
class C
{
    int Goo
    {
        get
        {
            return Bar();
        }
    }
}";
            await TestWithUseBlockBodyExceptAccessor(code, fixedCode);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseBlockBody3()
        {
            var code = @"
class C
{
    int Goo [|=>|] throw new NotImplementedException();
}";
            var fixedCode = @"
class C
{
    int Goo
    {
        get
        {
            throw new NotImplementedException();
        }
    }
}";
            await TestWithUseBlockBody(code, fixedCode);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseBlockBody4()
        {
            var code = @"
class C
{
    int Goo [|=>|] throw new NotImplementedException(); // comment
}";
            var fixedCode = @"
class C
{
    int Goo
    {
        get
        {
            throw new NotImplementedException(); // comment
        }
    }
}";
            await TestWithUseBlockBody(code, fixedCode);
        }

        [WorkItem(16386, "https://github.com/dotnet/roslyn/issues/16386")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestUseExpressionBodyKeepTrailingTrivia()
        {
            var code = @"
class C
{
    private string _prop = ""HELLO THERE!"";
    public string Prop { get { [|return|] _prop; } }

    public string OtherThing => ""Pickles"";
}";
            var fixedCode = @"
class C
{
    private string _prop = ""HELLO THERE!"";
    public string Prop => _prop;

    public string OtherThing => ""Pickles"";
}";
            await TestWithUseExpressionBody(code, fixedCode);
        }

        [WorkItem(19235, "https://github.com/dotnet/roslyn/issues/19235")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestDirectivesInBlockBody1()
        {
            var code = @"
class C
{
    int Goo
    {
        get
        {
#if true
            [|return|] Bar();
#else
            return Baz();
#endif
        }
    }
}";
            var fixedCode = @"
class C
{
    int Goo =>
#if true
            Bar();
#else
            return Baz();
#endif

}";
            await TestWithUseExpressionBody(code, fixedCode);
        }

        [WorkItem(19235, "https://github.com/dotnet/roslyn/issues/19235")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestDirectivesInBlockBody2()
        {
            var code = @"
class C
{
    int Goo
    {
        get
        {
#if false
            return Bar();
#else
            [|return|] Baz();
#endif
        }
    }
}";
            var fixedCode = @"
class C
{
    int Goo =>
#if false
            return Bar();
#else
            Baz();
#endif

}";
            await TestWithUseExpressionBody(code, fixedCode);
        }

        [WorkItem(19235, "https://github.com/dotnet/roslyn/issues/19235")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestMissingWithDirectivesInExpressionBody1()
        {
            var code = @"
class C
{
    int Goo =>
#if true
            Bar();
#else
            Baz();
#endif
}";
            await TestWithUseBlockBody(code, code);
        }

        [WorkItem(19235, "https://github.com/dotnet/roslyn/issues/19235")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestMissingWithDirectivesInExpressionBody2()
        {
            var code = @"
class C
{
    int Goo =>
#if false
            Bar();
#else
            Baz();
#endif
}";
            await TestWithUseBlockBody(code, code);
        }

        [WorkItem(19193, "https://github.com/dotnet/roslyn/issues/19193")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestMoveTriviaFromExpressionToReturnStatement()
        {
            var code = @"
class C
{
    int Goo(int i) [|=>|]
        //comment
        i * i;
}";
            var fixedCode = @"
class C
{
    int Goo(int i)
    {
        //comment
        return i * i;
    }
}";
            await TestWithUseBlockBody(code, fixedCode);
        }

        [WorkItem(20362, "https://github.com/dotnet/roslyn/issues/20362")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestOfferToConvertToBlockEvenIfExpressionBodyPreferredIfHasThrowExpressionPriorToCSharp7()
        {
            var code = @"
using System;
class C
{
    int Goo [|=>|] throw new NotImplementedException();
}";
            var fixedCode = @"
using System;
class C
{
    int Goo
    {
        get
        {
            throw new NotImplementedException();
        }
    }
}";
            await TestWithUseExpressionBody(code, fixedCode, LanguageVersion.CSharp6);
        }

        [WorkItem(20362, "https://github.com/dotnet/roslyn/issues/20362")]
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsUseExpressionBody)]
        public async Task TestOfferToConvertToBlockEvenIfExpressionBodyPreferredIfHasThrowExpressionPriorToCSharp7_FixAll()
        {
            var code = @"
using System;
class C
{
    int Goo [|=>|] throw new NotImplementedException();
    int Bar [|=>|] throw new NotImplementedException();
}";
            var fixedCode = @"
using System;
class C
{
    int Goo
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    int Bar
    {
        get
        {
            throw new NotImplementedException();
        }
    }
}";
            await TestWithUseExpressionBody(code, fixedCode, LanguageVersion.CSharp6);
        }
    }
}
