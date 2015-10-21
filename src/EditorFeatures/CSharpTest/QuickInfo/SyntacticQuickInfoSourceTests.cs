﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editor.CSharp.QuickInfo;
using Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.QuickInfo;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.UnitTests.QuickInfo;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.QuickInfo
{
    public class SyntacticQuickInfoSourceTests : AbstractQuickInfoSourceTests
    {
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void Brackets_1()
        {
            TestInMethod(@"
            if (true)
            {
            }$$
",
            ExpectedContent("if (true)\r\n{"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_0()
        {
            TestInMethod(@"
            if (true)
            {
                {
                }$$
            }
",
            ExpectedContent("{"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_1()
        {
            TestInMethod(@"
            while (true)
            {
                // some
                // comment
                {
                }$$
            }
",
            ExpectedContent(
@"// some
// comment
{"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_2()
        {
            TestInMethod(@"
            do
            {
                /* comment */
                {
                }$$
            }
            while (true);
",
            ExpectedContent(
@"/* comment */
{"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_3()
        {
            TestInMethod(@"
            if (true)
            {
            }
            else
            {
                {
                    // some
                    // comment
                }$$
            }
",
            ExpectedContent(
@"{
    // some
    // comment"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_4()
        {
            TestInMethod(@"
            using (var x = new X())
            {
                {
                    /* comment */
                }$$
            }
",
            ExpectedContent(
@"{
    /* comment */"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_5()
        {
            TestInMethod(@"
            foreach (var x in xs)
            {
                // above
                {
                    /* below */
                }$$
            }
",
            ExpectedContent(
@"// above
{"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_6()
        {
            TestInMethod(@"
            for (;;;)
            {
                /*************/

                // part 1

                // part 2
                {
                }$$
            }
",
            ExpectedContent(
@"/*************/

// part 1

// part 2
{"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_7()
        {
            TestInMethod(@"
            try
            {
                /*************/

                // part 1

                // part 2
                {
                }$$
            }
            catch { throw; }
",
            ExpectedContent(
@"/*************/

// part 1

// part 2
{"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_8()
        {
            TestInMethod(@"
            {
                /*************/

                // part 1

                // part 2
            }$$
",
            ExpectedContent(
@"{
    /*************/

    // part 1

    // part 2"));
        }

        [WorkItem(325, "https://github.com/dotnet/roslyn/issues/325")]
        [WpfFact, Trait(Traits.Feature, Traits.Features.QuickInfo)]
        public void ScopeBrackets_9()
        {
            TestInClass(@"
            int Property
            {
                set
                {
                    {
                    }$$
                }
            }
",
            ExpectedContent("{"));
        }

        private IQuickInfoProvider CreateProvider(TestWorkspace workspace)
        {
            return new SyntacticQuickInfoProvider(
                workspace.GetService<ITextBufferFactoryService>(),
                workspace.GetService<IContentTypeRegistryService>(),
                workspace.GetService<IProjectionBufferFactoryService>(),
                workspace.GetService<IEditorOptionsFactoryService>(),
                workspace.GetService<ITextEditorFactoryService>(),
                workspace.GetService<IGlyphService>(),
                workspace.GetService<ClassificationTypeMap>());
        }

        protected override void AssertNoContent(
            TestWorkspace workspace,
            Document document,
            int position)
        {
            var provider = CreateProvider(workspace);
            Assert.Null(provider.GetItemAsync(document, position, CancellationToken.None).Result);
        }

        protected override void AssertContentIs(
            TestWorkspace workspace,
            Document document,
            int position,
            string expectedContent,
            string expectedDocumentationComment = null)
        {
            var provider = CreateProvider(workspace);
            var state = provider.GetItemAsync(document, position, cancellationToken: CancellationToken.None).Result;
            Assert.NotNull(state);

            var viewHostingControl = (ViewHostingControl)((ElisionBufferDeferredContent)state.Content).Create();
            try
            {
                var actualContent = viewHostingControl.ToString();
                Assert.Equal(expectedContent, actualContent);
            }
            finally
            {
                viewHostingControl.TextView_TestOnly.Close();
            }
        }
    }
}
