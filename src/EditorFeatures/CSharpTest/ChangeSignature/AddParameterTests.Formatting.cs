﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ChangeSignature;
using Microsoft.CodeAnalysis.Editor.UnitTests.ChangeSignature;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities.ChangeSignature;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.ChangeSignature
{
    public partial class ChangeSignatureTests : AbstractChangeSignatureTests
    {
        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_KeepCountsPerLine()
        {
            var markup = @"
class C
{
    void $$Method(int a, int b, int c,
        int d, int e,
        int f)
    {
        Method(1,
            2, 3,
            4, 5, 6);
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(5),
                new AddedParameterOrExistingIndex(4),
                new AddedParameterOrExistingIndex(3),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(2),
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
class C
{
    void Method(int f, int e, int d,
        byte bb, int c,
        int b, int a)
    {
        Method(6,
            5, 4,
            34, 3, 2, 1);
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        [WorkItem(28156, "https://github.com/dotnet/roslyn/issues/28156")]
        public async Task AddParameter_Formatting_KeepTrivia()
        {
            var markup = @"
class C
{
    void $$Method(
        int a, int b, int c,
        int d, int e,
        int f)
    {
        Method(
            1, 2, 3,
            4, 5, 6);
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(2),
                new AddedParameterOrExistingIndex(3),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(4),
                new AddedParameterOrExistingIndex(5)};
            var expectedUpdatedCode = @"
class C
{
    void Method(
        int b, int c, int d,
        byte bb, int e,
        int f)
    {
        Method(
            2, 3, 4,
            34, 5, 6);
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        [WorkItem(28156, "https://github.com/dotnet/roslyn/issues/28156")]
        public async Task AddParameter_Formatting_KeepTrivia_WithArgumentNames()
        {
            var markup = @"
class C
{
    void $$Method(
        int a, int b, int c,
        int d, int e,
        int f)
    {
        Method(
            a: 1, b: 2, c: 3,
            d: 4, e: 5, f: 6);
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(2),
                new AddedParameterOrExistingIndex(3),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(4),
                new AddedParameterOrExistingIndex(5)};
            var expectedUpdatedCode = @"
class C
{
    void Method(
        int b, int c, int d,
        byte bb, int e,
        int f)
    {
        Method(
            b: 2, c: 3, d: 4,
            bb: 34, e: 5, f: 6);
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_Method()
        {
            var markup = @"
class C
{
    void $$Method(int a, 
        int b)
    {
        Method(1,
            2);
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
class C
{
    void Method(int b,
        byte bb, int a)
    {
        Method(2,
            34, 1);
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_Constructor()
        {
            var markup = @"
class SomeClass
{
    $$SomeClass(int a,
        int b)
    {
        new SomeClass(1,
            2);
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
class SomeClass
{
    SomeClass(int b,
        byte bb, int a)
    {
        new SomeClass(2,
            a: 1, bb: 34);
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_Indexer()
        {
            var markup = @"
class SomeClass
{
    public int $$this[int a,
        int b]
    {
        get
        {
            return new SomeClass()[1,
                2];
        }
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
class SomeClass
{
    public int this[int b,
        byte bb, int a]
    {
        get
        {
            return new SomeClass()[2,
                a: 1, bb: 34];
        }
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_Delegate()
        {
            var markup = @"
class SomeClass
{
    delegate void $$MyDelegate(int a,
        int b);

    void M(int a,
        int b)
    {
        var myDel = new MyDelegate(M);
        myDel(1,
            2);
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
class SomeClass
{
    delegate void MyDelegate(int b,
        byte bb, int a);

    void M(int b,
        byte bb, int a)
    {
        var myDel = new MyDelegate(M);
        myDel(2,
            34, 1);
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_AnonymousMethod()
        {
            var markup = @"
class SomeClass
{
    delegate void $$MyDelegate(int a,
        int b);

    void M()
    {
        MyDelegate myDel = delegate (int x,
            int y)
        {
            // Nothing
        };
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
class SomeClass
{
    delegate void MyDelegate(int b,
        byte bb, int a);

    void M()
    {
        MyDelegate myDel = delegate (int y,
            byte bb, int x)
        {
            // Nothing
        };
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_ConstructorInitializers()
        {
            var markup = @"
class B
{
    public $$B(int x, int y) { }
    public B() : this(1,
        2)
    { }
}

class D : B
{
    public D() : base(1,
        2)
    { }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
class B
{
    public B(int y, byte bb, int x) { }
    public B() : this(2,
        x: 1, bb: 34)
    { }
}

class D : B
{
    public D() : base(2,
        x: 1, bb: 34)
    { }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_Attribute()
        {
            var markup = @"
[Custom(1,
    2)]
class CustomAttribute : System.Attribute
{
    public $$CustomAttribute(int x, int y) { }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(0)};
            var expectedUpdatedCode = @"
[Custom(2,
    x: 1, bb: 34)]
class CustomAttribute : System.Attribute
{
    public CustomAttribute(int y, byte bb, int x) { }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        [WorkItem(28156, "https://github.com/dotnet/roslyn/issues/28156")]
        public async Task AddParameter_Formatting_Attribute_KeepTrivia()
        {
            var markup = @"
[Custom(
    1, 2)]
class CustomAttribute : System.Attribute
{
    public $$CustomAttribute(int x, int y) { }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")) };
            var expectedUpdatedCode = @"
[Custom(
    2, bb: 34)]
class CustomAttribute : System.Attribute
{
    public CustomAttribute(int y, byte bb) { }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        [WorkItem(28156, "https://github.com/dotnet/roslyn/issues/28156")]
        public async Task AddParameter_Formatting_Attribute_KeepTrivia_RemovingSecond()
        {
            var markup = @"
[Custom(
    1, 2)]
class CustomAttribute : System.Attribute
{
    public $$CustomAttribute(int x, int y) { }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(0),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34"))};
            var expectedUpdatedCode = @"
[Custom(
    1, bb: 34)]
class CustomAttribute : System.Attribute
{
    public CustomAttribute(int x, byte bb) { }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        [WorkItem(28156, "https://github.com/dotnet/roslyn/issues/28156")]
        public async Task AddParameter_Formatting_Attribute_KeepTrivia_RemovingBothAddingNew()
        {
            var markup = @"
[Custom(
    1, 2)]
class CustomAttribute : System.Attribute
{
    public $$CustomAttribute(int x, int y) { }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34"))};
            var expectedUpdatedCode = @"
[Custom(
    bb: 34)]
class CustomAttribute : System.Attribute
{
    public CustomAttribute(byte bb) { }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        [WorkItem(28156, "https://github.com/dotnet/roslyn/issues/28156")]
        public async Task AddParameter_Formatting_Attribute_KeepTrivia_RemovingBeforeNewlineComma()
        {
            var markup = @"
[Custom(1
    , 2, 3)]
class CustomAttribute : System.Attribute
{
    public $$CustomAttribute(int x, int y, int z) { }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(1),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34")),
                new AddedParameterOrExistingIndex(2)};
            var expectedUpdatedCode = @"
[Custom(2, z: 3, bb: 34)]
class CustomAttribute : System.Attribute
{
    public CustomAttribute(int y, byte bb, int z) { }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }

        [WorkItem(946220, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/946220")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.ChangeSignature)]
        public async Task AddParameter_Formatting_LambdaAsArgument()
        {
            var markup = @"class C
{
    void M(System.Action<int, int> f, int z$$)
    {
        M((x, y) => System.Console.WriteLine(x + y), 5);
    }
}";
            var updatedSignature = new[] {
                new AddedParameterOrExistingIndex(0),
                new AddedParameterOrExistingIndex(new AddedParameter("byte", "bb", "34"))};
            var expectedUpdatedCode = @"class C
{
    void M(System.Action<int, int> f, byte bb)
    {
        M((x, y) => System.Console.WriteLine(x + y), 34);
    }
}";
            await TestChangeSignatureViaCommandAsync(LanguageNames.CSharp, markup, updatedSignature: updatedSignature, expectedUpdatedInvocationDocumentCode: expectedUpdatedCode);
        }
    }
}
