﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ReplaceMethodWithProperty;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.ReplaceMethodWithProperty;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeActions.ReplaceMethodWithProperty
{
    public class ReplaceMethodWithPropertyTests : AbstractCSharpCodeActionTest
    {
        protected override object CreateCodeRefactoringProvider(Workspace workspace)
        {
            return new ReplaceMethodWithPropertyCodeRefactoringProvider();
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithGetName()
        {
            Test(
@"class C { int [||]GetFoo() { } }",
@"class C { int Foo { get { } } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithoutGetName()
        {
            Test(
@"class C { int [||]Foo() { } }",
@"class C { int Foo { get { } } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithArrowBody()
        {
            Test(
@"class C { int [||]GetFoo() => 0; }",
@"class C { int Foo { get; } => 0; }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithoutBody()
        {
            Test(
@"class C { int [||]GetFoo(); }",
@"class C { int Foo { get; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithModifiers()
        {
            Test(
@"class C { public static int [||]GetFoo() { } }",
@"class C { public static int Foo { get { } } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithAttributes()
        {
            Test(
@"class C { [A]int [||]GetFoo() { } }",
@"class C { [A]int Foo { get { } } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestExplicitInterfaceMethod()
        {
            Test(
@"class C { int [||]I.GetFoo() { } }",
@"class C { int I.Foo { get { } } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestVoidMethod()
        {
            TestMissing(
@"class C { void [||]GetFoo() { } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithParameters_1()
        {
            TestMissing(
@"class C { int [||]GetFoo(int i) { } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestMethodWithParameters_2()
        {
            TestMissing(
@"class C { int [||]GetFoo(int i = 0) { } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestNotInSignature_1()
        {
            TestMissing(
@"class C { [At[||]tr]int GetFoo() { } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestNotInSignature_2()
        {
            TestMissing(
@"class C { int GetFoo() { [||] } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetReferenceNotInMethod()
        {
            Test(
@"class C { int [||]GetFoo() { } void Bar() { var x = GetFoo(); } }",
@"class C { int Foo { get { } } void Bar() { var x = Foo; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetReferenceSimpleInvocation()
        {
            Test(
@"class C { int [||]GetFoo() { } void Bar() { var x = GetFoo(); } }",
@"class C { int Foo { get { } } void Bar() { var x = Foo; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetReferenceMemberAccessInvocation()
        {
            Test(
@"class C { int [||]GetFoo() { } void Bar() { var x = this.GetFoo(); } }",
@"class C { int Foo { get { } } void Bar() { var x = this.Foo; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetReferenceBindingMemberInvocation()
        {
            Test(
@"class C { int [||]GetFoo() { } void Bar() { C x; var v = x?.GetFoo(); } }",
@"class C { int Foo { get { } } void Bar() { C x; var v = x?.Foo; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetReferenceInMethod()
        {
            Test(
@"class C { int [||]GetFoo() { return GetFoo(); } }",
@"class C { int Foo { get { return Foo; } } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestOverride()
        {
            Test(
@"class C { public virtual int [||]GetFoo() { } } class D : C { public override int GetFoo() { } }",
@"class C { public virtual int Foo { get { } } } class D : C { public override int Foo { get { } } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetReference_NonInvoked()
        {
            Test(
@"using System; class C { int [||]GetFoo() { } void Bar() { Action<int> i = GetFoo; } }",
@"using System; class C { int Foo { get { } } void Bar() { Action<int> i = {|Conflict:Foo|}; } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetReference_ImplicitReference()
        {
            Test(
@"using System.Collections; class C { public IEnumerator [||]GetEnumerator() { } void Bar() { foreach (var x in this) { } } }",
@"using System.Collections; class C { public IEnumerator Enumerator { get { } } void Bar() { {|Conflict:foreach (var x in this) { }|} } }");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetSet()
        {
            Test(
@"using System; class C { int [||]GetFoo() { } void SetFoo(int i) { } }",
@"using System; class C { int Foo { get { } set { } } }",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetSetReference_NonInvoked()
        {
            Test(
@"using System; class C { int [||]GetFoo() { } void SetFoo(int i) { } void Bar() { Action<int> i = SetFoo; } }",
@"using System; class C { int Foo { get { } set { } } void Bar() { Action<int> i = {|Conflict:Foo|}; } }",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetSet_SetterAccessibility()
        {
            Test(
@"using System; class C { public int [||]GetFoo() { } private void SetFoo(int i) { } }",
@"using System; class C { public int Foo { get { } private set { } } }",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetSet_ExpressionBodies()
        {
            Test(
@"using System; class C { int [||]GetFoo() => 0; void SetFoo(int i) => Bar(); }",
@"using System; class C { int Foo { get { return 0; } set { Bar(); } } }",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetSet_GetInSetReference()
        {
            Test(
@"using System; class C { int [||]GetFoo() { } void SetFoo(int i) { } void Bar() { SetFoo(GetFoo() + 1); } }",
@"using System; class C { int Foo { get { } set { } } void Bar() { Foo = Foo + 1; } }",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetSet_UpdateSetPrameterName()
        {
            Test(
@"using System; class C { int [||]GetFoo() { } void SetFoo(int i) { v = i; } }",
@"using System; class C { int Foo { get { } set { v = value; } } }",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestUpdateGetSet_SetReferenceInSetter()
        {
            Test(
@"using System; class C { int [||]GetFoo() { } void SetFoo(int i) { SetFoo(i - 1); } }",
@"using System; class C { int Foo { get { } set { Foo = value - 1; } } }",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestVirtualGetWithOverride_1()
        {
            Test(
@"class C { protected virtual int [||]GetFoo() { } } class D : C { protected override int GetFoo() { } }",
@"class C { protected virtual int Foo { get { } } } class D : C { protected override int Foo{ get { } } }",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestVirtualGetWithOverride_2()
        {
            Test(
@"class C { protected virtual int [||]GetFoo() { } } class D : C { protected override int GetFoo() { base.GetFoo(); } }",
@"class C { protected virtual int Foo { get { } } } class D : C { protected override int Foo { get { base.Foo; } } }",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsReplaceMethodWithProperty)]
        public void TestGetWithInterface()
        {
            Test(
@"interface I { int [||]GetFoo(); } class C : I { public int GetFoo() { } }",
@"interface I { int Foo { get; } } class C : I { public int Foo { get { } } }",
index: 0);
        }
    }
}