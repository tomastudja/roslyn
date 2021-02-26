﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Composition
Imports System.Diagnostics.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.ConvertToInterpolatedString
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.ConvertToInterpolatedString
    <ExportCodeRefactoringProvider(LanguageNames.VisualBasic, Name:=PredefinedCodeRefactoringProviderNames.ExtractMethod), [Shared]>
    Friend Class VisualBasicConvertConcatenationToInterpolatedStringRefactoringProvider
        Inherits AbstractConvertConcatenationToInterpolatedStringRefactoringProvider(Of ExpressionSyntax)

        <ImportingConstructor>
        <SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification:="Used in test code: https://github.com/dotnet/roslyn/issues/42814")>
        Public Sub New()
        End Sub

        Protected Overrides Function SupportsConstantInterpolatedStrings(document As Document) As Boolean
            Return False
        End Function

        Protected Overrides Function GetTextWithoutQuotes(text As String, isVerbatim As Boolean, isCharacterLiteral As Boolean) As String
            If isCharacterLiteral Then
                Return text.Substring("'".Length, text.Length - "''C".Length)
            Else
                Return text.Substring("'".Length, text.Length - "''".Length)
            End If
        End Function
    End Class
End Namespace
