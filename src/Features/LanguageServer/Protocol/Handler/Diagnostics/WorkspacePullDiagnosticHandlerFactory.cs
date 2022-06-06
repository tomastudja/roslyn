﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.Diagnostics
{
    [ExportCSharpVisualBasicLspServiceFactory(typeof(WorkspacePullDiagnosticHandler)), Shared]
    internal class WorkspacePullDiagnosticHandlerFactory : ILspServiceFactory
    {
        private readonly IDiagnosticService _diagnosticService;
        private readonly EditAndContinueDiagnosticUpdateSource _editAndContinueDiagnosticUpdateSource;
        private readonly IGlobalOptionService _globalOptions;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public WorkspacePullDiagnosticHandlerFactory(
            IDiagnosticService diagnosticService,
            EditAndContinueDiagnosticUpdateSource editAndContinueDiagnosticUpdateSource,
            IGlobalOptionService globalOptions)
        {
            _diagnosticService = diagnosticService;
            _editAndContinueDiagnosticUpdateSource = editAndContinueDiagnosticUpdateSource;
            _globalOptions = globalOptions;
        }

        public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
            => new WorkspacePullDiagnosticHandler(_diagnosticService, _editAndContinueDiagnosticUpdateSource, _globalOptions);
    }
}
