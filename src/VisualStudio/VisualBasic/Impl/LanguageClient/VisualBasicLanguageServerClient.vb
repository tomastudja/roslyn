﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.ComponentModel.Composition
Imports Microsoft.CodeAnalysis.Editor
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.Remote
Imports Microsoft.CodeAnalysis.Shared.TestHooks
Imports Microsoft.VisualStudio.LanguageServer.Client
Imports Microsoft.VisualStudio.LanguageServices.Implementation.LanguageService
Imports Microsoft.VisualStudio.Utilities

Namespace Microsoft.VisualStudio.LanguageServices.VisualBasic.LanguageClient

    ' currently, platform doesn't allow multiple content types
    ' to be associated with 1 ILanguageClient forcing us to
    ' create multiple ILanguageClients for each content type
    <ContentType(ContentTypeNames.VisualBasicContentType)>
    <Export(GetType(ILanguageClient))>
    <ExportMetadata("Capabilities", "WorkspaceStreamingSymbolProvider")>
    Friend Class VisualBasicLanguageServerClient
        Inherits AbstractLanguageServerClient

        <ImportingConstructor>
        <Obsolete(MefConstruction.ImportingConstructorMessage, True)>
        Public Sub New(workspace As VisualStudioWorkspace,
                       eventListener As LanguageServerClientEventListener,
                       listenerProvider As IAsynchronousOperationListenerProvider)
            MyBase.New(workspace,
                       eventListener,
                       listenerProvider,
                       WellKnownServiceHubServices.VisualBasicLanguageServer,
                       "ManagedLanguage.IDE.VisualBasicLanguageServer")
        End Sub

        Public Overrides ReadOnly Property Name As String
            Get
                Return BasicVSResources.Visual_Basic_language_server_client
            End Get
        End Property
    End Class
End Namespace
