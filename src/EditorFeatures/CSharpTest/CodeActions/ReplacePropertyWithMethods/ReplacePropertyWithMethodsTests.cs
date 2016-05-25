﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.ReplacePropertyWithMethods;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeActions.ReplacePropertyWithMethods
{
    public class ReplacePropertyWithMethodsTests : AbstractCSharpCodeActionTest
    {
        protected override object CreateCodeRefactoringProvider(Workspace workspace)
        {
            return new ReplacePropertyWithMethodsCodeRefactoringProvider();
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplacePropertyWithMethods)]
        public async Task TestGetWithBody()
        {
            await TestAsync(
@"class C { int [||]Prop { get { return 0; } } }",
@"class C { private int GetProp() { return 0; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplacePropertyWithMethods)]
        public async Task TestPublicProperty()
        {
            await TestAsync(
@"class C { public int [||]Prop { get { return 0; } } }",
@"class C { public int GetProp() { return 0; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplacePropertyWithMethods)]
        public async Task TestAnonyousType1()
        {
            await TestAsync(
@"class C {
    public int [||]Prop { get { return 0; } } 
    public void M() { var v = new { P = this.Prop } }
}",
@"class C {
    public int GetProp() { return 0; } 
    public void M() { var v = new { P = this.GetProp() } }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplacePropertyWithMethods)]
        public async Task TestAnonyousType2()
        {
            await TestAsync(
@"class C {
    public int [||]Prop { get { return 0; } } 
    public void M() { var v = new { this.Prop } }
}",
@"class C {
    public int GetProp() { return 0; } 
    public void M() { var v = new { Prop = this.GetProp() } }
}");
        }
    }
}
