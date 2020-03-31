﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Composition
Imports System.Diagnostics.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.UseIsNullCheck
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.UseIsNullCheck
    <ExportCodeFixProvider(LanguageNames.VisualBasic), [Shared]>
    Friend Class VisualBasicUseIsNullCheckForReferenceEqualsCodeFixProvider
        Inherits AbstractUseIsNullCheckForReferenceEqualsCodeFixProvider

        <ImportingConstructor>
        <SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification:="Used in test code: https://github.com/dotnet/roslyn/issues/42814")>
        Public Sub New()
        End Sub

        Protected Overrides Function GetIsNullTitle() As String
            Return VBFeaturesResources.Use_Is_Nothing_check
        End Function

        Protected Overrides Function GetIsNotNullTitle() As String
            Return VBFeaturesResources.Use_IsNot_Nothing_check
        End Function

        Protected Overrides Function CreateNullCheck(argument As SyntaxNode, isUnconstrainedGeneric As Boolean) As SyntaxNode
            Return SyntaxFactory.IsExpression(
                DirectCast(argument, ExpressionSyntax).Parenthesize(),
                SyntaxFactory.NothingLiteralExpression(SyntaxFactory.Token(SyntaxKind.NothingKeyword))).Parenthesize()
        End Function

        Protected Overrides Function CreateNotNullCheck(argument As SyntaxNode) As SyntaxNode
            Return SyntaxFactory.IsNotExpression(
                DirectCast(argument, ExpressionSyntax).Parenthesize(),
                SyntaxFactory.NothingLiteralExpression(SyntaxFactory.Token(SyntaxKind.NothingKeyword))).Parenthesize()
        End Function
    End Class
End Namespace
