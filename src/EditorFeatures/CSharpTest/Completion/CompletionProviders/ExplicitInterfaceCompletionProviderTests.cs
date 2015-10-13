﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Completion.Providers;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Completion.CompletionProviders
{
    public class ExplicitInterfaceCompletionProviderTests : AbstractCSharpCompletionProviderTests
    {
        public ExplicitInterfaceCompletionProviderTests(CSharpTestWorkspaceFixture workspaceFixture) : base(workspaceFixture)
        {
        }

        internal override CompletionListProvider CreateCompletionProvider()
        {
            return new ExplicitInterfaceCompletionProvider();
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.Completion)]
        public void ExplicitInterfaceMember()
        {
            var markup = @"
interface IFoo
{
    void Foo();
    void Foo(int x);
    int Prop { get; }
}

class Bar : IFoo
{
     void IFoo.$$
}";

            VerifyItemExists(markup, "Foo()");
            VerifyItemExists(markup, "Foo(int x)");
            VerifyItemExists(markup, "Prop");
        }

        [WorkItem(709988)]
        [WpfFact, Trait(Traits.Feature, Traits.Features.Completion)]
        public void CommitOnNotParen()
        {
            var markup = @"
interface IFoo
{
    void Foo();
}

class Bar : IFoo
{
     void IFoo.$$
}";

            var expected = @"
interface IFoo
{
    void Foo();
}

class Bar : IFoo
{
     void IFoo.Foo()
}";

            VerifyProviderCommit(markup, "Foo()", expected, null, "");
        }

        [WorkItem(709988)]
        [WpfFact, Trait(Traits.Feature, Traits.Features.Completion)]
        public void CommitOnParen()
        {
            var markup = @"
interface IFoo
{
    void Foo();
}

class Bar : IFoo
{
     void IFoo.$$
}";

            var expected = @"
interface IFoo
{
    void Foo();
}

class Bar : IFoo
{
     void IFoo.Foo(
}";

            VerifyProviderCommit(markup, "Foo()", expected, '(', "");
        }
    }
}
