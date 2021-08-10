﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.Xaml;
using Microsoft.CodeAnalysis.Experiments;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServices.Implementation.LanguageClient;
using Microsoft.VisualStudio.LanguageServices.Xaml.LanguageServer;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Xaml
{
    /// <summary>
    /// Experimental XAML Language Server Client used everywhere when
    /// <see cref="StringConstants.EnableLspIntelliSense"/> experiment is turned on.
    /// </summary>
    [ContentType(ContentTypeNames.XamlContentType)]
    [Export(typeof(ILanguageClient))]
    internal class XamlInProcLanguageClient : AbstractInProcLanguageClient
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, true)]
        public XamlInProcLanguageClient(
            XamlRequestDispatcherFactory xamlDispatcherFactory,
            VisualStudioWorkspace workspace,
            IDiagnosticService diagnosticService,
            IAsynchronousOperationListenerProvider listenerProvider,
            ILspWorkspaceRegistrationService lspWorkspaceRegistrationService,
            ILspLoggerFactory lspLoggerFactory,
            IThreadingContext threadingContext)
            : base(xamlDispatcherFactory, workspace, diagnosticService, listenerProvider, lspWorkspaceRegistrationService, lspLoggerFactory, threadingContext, diagnosticsClientName: null)
        {
        }

        /// <summary>
        /// Gets the name of the language client (displayed in yellow bars).
        /// When updating the string of Name, please make sure to update the same string in Microsoft.VisualStudio.LanguageServer.Client.ExperimentalSnippetSupport.AllowList
        /// </summary>
        public override string Name => "XAML Language Server Client (Experimental)";

        public override ServerCapabilities GetCapabilities(ClientCapabilities clientCapabilities)
        {
            var experimentationService = Workspace.Services.GetRequiredService<IExperimentationService>();
            var isLspExperimentEnabled = experimentationService.IsExperimentEnabled(StringConstants.EnableLspIntelliSense);

            return isLspExperimentEnabled ? XamlCapabilities.Current : XamlCapabilities.None;
        }
    }
}
