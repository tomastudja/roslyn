// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

// { [ file name ], [ ngen ? true | false ] }
var IDEFiles = new Dictionary<string, bool>()
{
    { "CSharpInteractive.rsp", true },
    { "Esent.Interop.dll", true },
    { "InteractiveHost.exe", true },
    { "Microsoft.CodeAnalysis.CSharp.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.EditorFeatures.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator.ExpressionCompiler.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator.ResultProvider.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.Features.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.InteractiveEditorFeatures.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.Workspaces.dll", true },
    { "Microsoft.CodeAnalysis.dll", true },
    { "Microsoft.CodeAnalysis.EditorFeatures.dll", true },
    { "Microsoft.CodeAnalysis.EditorFeatures.Text.dll", true },
    { "Microsoft.CodeAnalysis.Elfie.dll", true },
    { "Microsoft.CodeAnalysis.ExpressionEvaluator.ExpressionCompiler.dll", true },
    { "Microsoft.CodeAnalysis.ExpressionEvaluator.ResultProvider.dll", true },
    { "Microsoft.CodeAnalysis.Features.dll", true },
    { "Microsoft.CodeAnalysis.InteractiveEditorFeatures.dll", true },
    { "Microsoft.CodeAnalysis.InteractiveFeatures.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.Scripting.dll", true },
    { "Microsoft.CodeAnalysis.Scripting.dll", true },
    { "Microsoft.CodeAnalysis.Workspaces.dll", true },
    { "Microsoft.CodeAnalysis.Workspaces.Desktop.dll", true },
    { "Microsoft.CodeAnalysis.VisualBasic.dll", true },
    { "Microsoft.CodeAnalysis.VisualBasic.EditorFeatures.dll", true },
    { "Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator.ExpressionCompiler.dll", true },
    { "Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator.ResultProvider.dll", true },
    { "Microsoft.CodeAnalysis.VisualBasic.Features.dll", true },
    { "Microsoft.CodeAnalysis.VisualBasic.Workspaces.dll", true },
    { "Microsoft.DiaSymReader.dll", false },
    { "Microsoft.DiaSymReader.PortablePDB.dll", false },
    { "Microsoft.VisualStudio.CSharp.Repl.dll", true },
    { "Microsoft.VisualStudio.InteractiveServices.dll", true },
    { "Microsoft.VisualStudio.InteractiveWindow.dll", true },
    { "Microsoft.VisualStudio.LanguageServices.CSharp.dll", true },
    { "Microsoft.VisualStudio.LanguageServices.dll", true },
    { "Microsoft.VisualStudio.LanguageServices.Implementation.dll", true },
    { "Microsoft.VisualStudio.LanguageServices.SolutionExplorer.dll", true },
    { "Microsoft.VisualStudio.LanguageServices.Telemetry.dll", true },
    { "Microsoft.VisualStudio.LanguageServices.VisualBasic.dll", true },
    { "Microsoft.VisualStudio.VsInteractiveWindow.dll", true },
    { "System.Collections.Immutable.dll", true },
    { "System.Composition.Convention.dll", false },
    { "System.Composition.Hosting.dll", false },
    { "System.Composition.TypedParts.dll", false },
    { "System.IO.FileSystem.dll", false },
    { "System.IO.FileSystem.Primitives.dll", false },
    { "System.Reflection.Metadata.dll", true }
};

// { [ file name ], [ ngen ? true | false ] }
var MSBuildFiles = new Dictionary<string, bool>()
{
    { "csc.exe", true },
    { "csc.exe.config", false },
    { "csc.rsp", false },
    { "csi.exe", true },
    { "csi.rsp", false },
    { "Microsoft.Build.Tasks.CodeAnalysis.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.dll", true },
    { "Microsoft.CodeAnalysis.CSharp.Scripting.dll", true },
    { "Microsoft.CodeAnalysis.dll", true },
    { "Microsoft.CodeAnalysis.Scripting.dll", true },
    { "Microsoft.CodeAnalysis.VisualBasic.dll", true },
    { "Microsoft.CSharp.Core.targets", false },
    { "Microsoft.VisualBasic.Core.targets", false },
    { "System.Collections.Immutable.dll", true },
    { "System.Reflection.Metadata.dll", true },
    { "vbc.exe", true },
    { "vbc.exe.config", false },
    { "vbc.rsp", false },
    { "VBCSCompiler.exe", true },
    { "VBCSCompiler.exe.config", false }
};
