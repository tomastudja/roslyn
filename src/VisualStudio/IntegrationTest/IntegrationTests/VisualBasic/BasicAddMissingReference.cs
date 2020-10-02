﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.IntegrationTest.Utilities;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;
using ProjectUtils = Microsoft.VisualStudio.IntegrationTest.Utilities.Common.ProjectUtils;

namespace Roslyn.VisualStudio.IntegrationTests.VisualBasic
{
    [Collection(nameof(SharedIntegrationHostFixture))]
    public class BasicAddMissingReference : AbstractEditorTest
    {
        private const string FileInLibraryProject1 = @"Public Class Class1
    Inherits System.Windows.Forms.Form
    Public Sub goo()

    End Sub
End Class

Public Class class2
    Public Sub goo(ByVal x As System.Windows.Forms.Form)

    End Sub

    Public Event ee As System.Windows.Forms.ColumnClickEventHandler
End Class

Public Class class3
    Implements System.Windows.Forms.IButtonControl

    Public Property DialogResult() As System.Windows.Forms.DialogResult Implements System.Windows.Forms.IButtonControl.DialogResult
        Get

        End Get
        Set(ByVal Value As System.Windows.Forms.DialogResult)

        End Set
    End Property

    Public Sub NotifyDefault(ByVal value As Boolean) Implements System.Windows.Forms.IButtonControl.NotifyDefault

    End Sub

    Public Sub PerformClick() Implements System.Windows.Forms.IButtonControl.PerformClick

    End Sub
End Class
";
        private const string FileInLibraryProject2 = @"Public Class Class1
    Inherits System.Xml.XmlAttribute
    Sub New()
        MyBase.New(Nothing, Nothing, Nothing, Nothing)
    End Sub
    Sub goo()

    End Sub
    Public bar As ClassLibrary3.Class1
End Class
";
        private const string FileInLibraryProject3 = @"Public Class Class1
    Public Enum E
        E1
        E2
    End Enum

    Public Function Goo() As ADODB.Recordset
        Dim x As ADODB.Recordset = Nothing
        Return x
    End Function


End Class
";
        private const string FileInConsoleProject1 = @"Imports System.Data.XLinq

Module Module1

    Sub Main()
        'ERRID_UnreferencedAssembly3
        Dim y As New ClassLibrary1.class2
        y.goo(Nothing)

        'ERRID_UnreferencedAssemblyEvent3
        AddHandler y.ee, Nothing

        'ERRID_UnreferencedAssemblyBase3
        Dim x As New ClassLibrary1.Class1
        x.goo()
        'ERRID_UnreferencedAssemblyImplements3
        Dim z As New ClassLibrary1.class3
        Dim xxx = z.DialogResult
        'ERRID_SymbolFromUnreferencedProject3
        Dim a As New ClassLibrary2.Class1
        Dim c As Boolean = a.HasChildNodes()

        Dim d = a.bar
    End Sub

End Module
";

        private const string ClassLibrary1Name = "ClassLibrary1";
        private const string ClassLibrary2Name = "ClassLibrary2";
        private const string ClassLibrary3Name = "ClassLibrary3";
        private const string ConsoleProjectName = "ConsoleApplication1";

        protected override string LanguageName => LanguageNames.VisualBasic;

        public BasicAddMissingReference(VisualStudioInstanceFactory instanceFactory)
            : base(instanceFactory)
        {
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync().ConfigureAwait(true);
            VisualStudio.SolutionExplorer.CreateSolution("ReferenceErrors", solutionElement: XElement.Parse(
                "<Solution>" +
               $"   <Project ProjectName=\"{ClassLibrary1Name}\" ProjectTemplate=\"{WellKnownProjectTemplates.WinFormsApplication}\" Language=\"{LanguageNames.VisualBasic}\">" +
                "       <Document FileName=\"Class1.vb\"><![CDATA[" +
                FileInLibraryProject1 +
                "]]>" +
                "       </Document>" +
                "   </Project>" +
               $"   <Project ProjectName=\"{ClassLibrary2Name}\" ProjectReferences=\"{ClassLibrary3Name}\" ProjectTemplate=\"{WellKnownProjectTemplates.ClassLibrary}\" Language=\"{LanguageNames.VisualBasic}\">" +
                "       <Document FileName=\"Class1.vb\"><![CDATA[" +
               FileInLibraryProject2 +
                "]]>" +
                "       </Document>" +
                "   </Project>" +
               $"   <Project ProjectName=\"{ClassLibrary3Name}\" ProjectTemplate=\"{WellKnownProjectTemplates.ClassLibrary}\" Language=\"{LanguageNames.VisualBasic}\">" +
                "       <Document FileName=\"Class1.vb\"><![CDATA[" +
               FileInLibraryProject3 +
                "]]>" +
                "       </Document>" +
                "   </Project>" +
               $"   <Project ProjectName=\"{ConsoleProjectName}\" ProjectReferences=\"{ClassLibrary1Name};{ClassLibrary2Name}\" ProjectTemplate=\"{WellKnownProjectTemplates.ConsoleApplication}\" Language=\"{LanguageNames.VisualBasic}\">" +
                "       <Document FileName=\"Module1.vb\"><![CDATA[" +
               FileInConsoleProject1 +
                "]]>" +
                "       </Document>" +
               "   </Project>" +
                "</Solution>"));
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.AddMissingReference)]
        public void VerifyAvailableCodeActions()
        {
            var consoleProject = new ProjectUtils.Project(ConsoleProjectName);
            VisualStudio.SolutionExplorer.OpenFile(consoleProject, "Module1.vb");
            VisualStudio.Editor.PlaceCaret("y.goo", charsOffset: 1);
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Add reference to 'System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'.", applyFix: false);
            VisualStudio.Editor.PlaceCaret("x.goo", charsOffset: 1);
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Add reference to 'System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'.", applyFix: false);
            VisualStudio.Editor.PlaceCaret("z.DialogResult", charsOffset: 1);
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Add reference to 'System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'.", applyFix: false);
            VisualStudio.Editor.PlaceCaret("a.bar", charsOffset: 1);
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Add project reference to 'ClassLibrary3'.", applyFix: false);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.AddMissingReference)]
        public void InvokeSomeFixesInVisualBasicThenVerifyReferences()
        {
            var consoleProject = new ProjectUtils.Project(ConsoleProjectName);
            VisualStudio.SolutionExplorer.OpenFile(consoleProject, "Module1.vb");
            VisualStudio.Editor.PlaceCaret("y.goo", charsOffset: 1);
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Add reference to 'System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'.", applyFix: true);
            VisualStudio.SolutionExplorer.Verify.AssemblyReferencePresent(
               project: consoleProject,
               assemblyName: "System.Windows.Forms",
               assemblyVersion: "4.0.0.0",
               assemblyPublicKeyToken: "b77a5c561934e089");
            VisualStudio.Editor.PlaceCaret("a.bar", charsOffset: 1);
            VisualStudio.Editor.InvokeCodeActionList();
            VisualStudio.Editor.Verify.CodeAction("Add project reference to 'ClassLibrary3'.", applyFix: true);
            VisualStudio.SolutionExplorer.Verify.ProjectReferencePresent(
               project: consoleProject,
               referencedProjectName: ClassLibrary3Name);
        }
    }
}
