﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.Diagnostics.Public;

[ExportCSharpVisualBasicLspServiceFactory(typeof(PublicWorkspacePullDiagnosticsHandler)), Shared]
internal class PublicWorkspacePullDiagnosticHandlerFactory : ILspServiceFactory
{
    private readonly IDiagnosticAnalyzerService _analyzerService;
    private readonly EditAndContinueDiagnosticUpdateSource _editAndContinueDiagnosticUpdateSource;
    private readonly IGlobalOptionService _globalOptions;

    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public PublicWorkspacePullDiagnosticHandlerFactory(
        IDiagnosticAnalyzerService analyzerService,
        EditAndContinueDiagnosticUpdateSource editAndContinueDiagnosticUpdateSource,
        IGlobalOptionService globalOptions)
    {
        _analyzerService = analyzerService;
        _editAndContinueDiagnosticUpdateSource = editAndContinueDiagnosticUpdateSource;
        _globalOptions = globalOptions;
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
        => new PublicWorkspacePullDiagnosticsHandler(_analyzerService, _editAndContinueDiagnosticUpdateSource, _globalOptions);
}
