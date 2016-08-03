﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Composition
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Microsoft.CodeAnalysis.DiagnosticComments.CodeFixes
    <ExportCodeFixProvider(LanguageNames.VisualBasic, Name:=PredefinedCodeFixProviderNames.RemoveDuplicateParamTag), [Shared]>
    Friend Class VisualBasicRemoveDocCommentNodeCodeFixProvider
        Inherits AbstractRemoveDocCommentNodeCodeFixProvider

        ''' <summary>
        ''' XML comment tag with identical attributes
        ''' </summary>
        Friend Const BC42305 As String = NameOf(BC42305)

        ''' <summary>
        ''' XML comment tag is not permitted on a 'sub' language element
        ''' </summary>
        Friend Const BC42306 As String = NameOf(BC42306)

        ''' <summary>
        ''' XML comment type parameter does not match a type parameter
        ''' </summary>
        Friend Const BC42307 As String = NameOf(BC42307)

        ''' <summary>
        ''' XML comment type parameter does not match a type parameter
        ''' </summary>
        Friend Const BC42317 As String = NameOf(BC42317)
        Friend ReadOnly Id As ImmutableArray(Of String) = ImmutableArray.Create(BC42305, BC42306, BC42307, BC42317)

        Public Overrides ReadOnly Property FixableDiagnosticIds As ImmutableArray(Of String)
            Get
                Return Id
            End Get
        End Property

        Protected Overrides ReadOnly Property DocCommentSignifierToken As String
            Get
                Return "'''"
            End Get
        End Property

        Protected Overrides Function GetRevisedDocCommentTrivia(docCommentText As String) As SyntaxTriviaList
            Return SyntaxFactory.ParseLeadingTrivia(docCommentText)
        End Function

        Protected Overrides Function GetDocCommentElementNode(fullDocComentNode As SyntaxNode, span As TextSpan) As SyntaxNode
            Return fullDocComentNode.FindNode(span)
        End Function
    End Class
End Namespace