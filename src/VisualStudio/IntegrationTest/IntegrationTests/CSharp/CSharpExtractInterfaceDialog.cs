﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.IntegrationTest.Utilities;
using Microsoft.VisualStudio.IntegrationTest.Utilities.OutOfProcess;
using Roslyn.Test.Utilities;
using Xunit;
using ProjectUtils = Microsoft.VisualStudio.IntegrationTest.Utilities.Common.ProjectUtils;

namespace Roslyn.VisualStudio.IntegrationTests.CSharp
{
    [Collection(nameof(SharedIntegrationHostFixture))]
    public class CSharpExtractInterfaceDialog : AbstractEditorTest
    {
        protected override string LanguageName => LanguageNames.CSharp;

        private ExtractInterfaceDialog_OutOfProc ExtractInterfaceDialog => VisualStudio.ExtractInterfaceDialog;

        public CSharpExtractInterfaceDialog(VisualStudioInstanceFactory instanceFactory)
            : base(instanceFactory, nameof(CSharpExtractInterfaceDialog))
        {
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsExtractInterface)]
        public void VerifyCancellation()
        {
            SetUpEditor(@"class C$$
{
    public void M() { }
}
");
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Extract Interface...",
                applyFix: true,
                blockUntilComplete: false);

            ExtractInterfaceDialog.VerifyOpen();
            ExtractInterfaceDialog.ClickCancel();
            ExtractInterfaceDialog.VerifyClosed();

            VisualStudio.Editor.Verify.TextContains(@"class C
{
    public void M() { }
}
");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsExtractInterface)]
        public void CheckFileName()
        {
            SetUpEditor(@"class C$$
{
    public void M() { }
}
");
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Extract Interface...",
                applyFix: true,
                blockUntilComplete: false);

            ExtractInterfaceDialog.VerifyOpen();

            var targetFileName = ExtractInterfaceDialog.GetTargetFileName();
            Assert.Equal(expected: "IC.cs", actual: targetFileName);

            ExtractInterfaceDialog.ClickCancel();
            ExtractInterfaceDialog.VerifyClosed();
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsExtractInterface)]
        public void VerifySelectAndDeselectAllButtons()
        {
            SetUpEditor(@"class C$$
{
    public void M1() { }
    public void M2() { }
}
");

            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Extract Interface...",
                applyFix: true,
                blockUntilComplete: false);

            ExtractInterfaceDialog.VerifyOpen();

            var selectedItems = ExtractInterfaceDialog.GetSelectedItems();
            Assert.Equal(
                expected: new[] { "M1()", "M2()" },
                actual: selectedItems);

            ExtractInterfaceDialog.ClickDeselectAll();

            selectedItems = ExtractInterfaceDialog.GetSelectedItems();
            Assert.Empty(selectedItems);

            ExtractInterfaceDialog.ClickSelectAll();

            selectedItems = ExtractInterfaceDialog.GetSelectedItems();
            Assert.Equal(
                expected: new[] { "M1()", "M2()" },
                actual: selectedItems);

            ExtractInterfaceDialog.ClickCancel();
            ExtractInterfaceDialog.VerifyClosed();
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsExtractInterface)]
        public void OnlySelectedItemsAreGenerated()
        {
            SetUpEditor(@"class C$$
{
    public void M1() { }
    public void M2() { }
}
");

            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Extract Interface...",
                applyFix: true,
                blockUntilComplete: false);

            ExtractInterfaceDialog.VerifyOpen();
            ExtractInterfaceDialog.ClickDeselectAll();
            ExtractInterfaceDialog.ToggleItem("M2()");
            ExtractInterfaceDialog.ClickOK();
            ExtractInterfaceDialog.VerifyClosed();

            var project = new ProjectUtils.Project(ProjectName);
            VisualStudio.SolutionExplorer.OpenFile(project, "Class1.cs");
            VisualStudio.Editor.Verify.TextContains(@"class C : IC
{
    public void M1() { }
    public void M2() { }
}
");

            VisualStudio.SolutionExplorer.OpenFile(project, "IC.cs");
            VisualStudio.Editor.Verify.TextContains(@"interface IC
{
    void M2();
}");
        }
    }
}
