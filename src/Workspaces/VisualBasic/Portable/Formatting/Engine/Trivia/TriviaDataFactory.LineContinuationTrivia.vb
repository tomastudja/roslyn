﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System
Imports System.Diagnostics
Imports System.Text
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.VisualBasic

#If CODE_STYLE Then
Imports OptionSet = Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
#Else
Imports Microsoft.CodeAnalysis.Options
#End If

Namespace Microsoft.CodeAnalysis.VisualBasic.Formatting
    Partial Friend Class TriviaDataFactory
        ''' <summary>   
        ''' represents a general trivia between two tokens. slightly more expensive than others since it
        ''' needs to calculate stuff unlike other cases
        ''' </summary>
        Private Class LineContinuationTrivia
            Inherits AbstractLineBreakTrivia

            Public Sub New(optionSet As OptionSet,
                           originalString As String,
                           indentation As Integer)
                MyBase.New(optionSet, originalString, 1, indentation, False)
            End Sub

            Protected Overrides Function CreateStringFromState() As String
                Contract.ThrowIfFalse(Me.SecondTokenIsFirstTokenOnLine)

                Dim builder = StringBuilderPool.Allocate()
                builder.Append(" "c)
                builder.Append(SyntaxFacts.GetText(SyntaxKind.LineContinuationTrivia))

                builder.AppendIndentationString(Me.Spaces, Me.OptionSet.GetOption(FormattingOptions.UseTabs, LanguageNames.VisualBasic), Me.OptionSet.GetOption(FormattingOptions.TabSize, LanguageNames.VisualBasic))
                Return StringBuilderPool.ReturnAndFree(builder)
            End Function

            Public Overrides Function WithIndentation(indentation As Integer,
                                                      context As FormattingContext,
                                                      formattingRules As ChainedFormattingRules,
                                                      cancellationToken As CancellationToken) As TriviaData
                If Me.Spaces = indentation Then
                    Return Me
                End If

                Return New LineContinuationTrivia(Me.OptionSet, Me._original, indentation)
            End Function
        End Class
    End Class
End Namespace
