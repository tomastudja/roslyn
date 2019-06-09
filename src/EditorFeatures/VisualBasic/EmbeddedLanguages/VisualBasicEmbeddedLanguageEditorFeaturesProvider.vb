﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis.Editing
Imports Microsoft.CodeAnalysis.Editor.EmbeddedLanguages
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.VisualBasic.EmbeddedLanguages.LanguageServices

Namespace Microsoft.CodeAnalysis.VisualBasic.Features.EmbeddedLanguages
    <ExportLanguageService(GetType(IEmbeddedLanguageEditorFeaturesProvider), LanguageNames.VisualBasic), [Shared]>
    Friend Class VisualBasicEmbeddedLanguageEditorFeaturesProvider
        Inherits AbstractEmbeddedLanguageEditorFeaturesProvider

        Public Shared Shadows Instance As New VisualBasicEmbeddedLanguageEditorFeaturesProvider()

        <ImportingConstructor>
        Public Sub New()
            MyBase.New(VisualBasicEmbeddedLanguagesProvider.Info)
        End Sub

        Friend Overrides Sub AddComment(editor As SyntaxEditor, stringLiteral As SyntaxToken, commentContents As String)
            EmbeddedLanguageUtilities.AddComment(editor, stringLiteral, commentContents)
        End Sub

        Friend Overrides Function EscapeText(text As String, token As SyntaxToken) As String
            Return EmbeddedLanguageUtilities.EscapeText(text, token)
        End Function
    End Class
End Namespace
