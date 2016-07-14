' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Xml.Linq
Imports Microsoft.CodeAnalysis.CodeRefactorings
Imports Microsoft.CodeAnalysis.Editor.UnitTests.MoveType
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
Imports Microsoft.CodeAnalysis.VisualBasic.CodeRefactorings.MoveType

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.CodeRefactorings.MoveType
    Public Class BasicMoveTypeTestsBase
        Inherits AbstractMoveTypeTestsBase

        Protected Overrides Function CreateCodeRefactoringProvider(workspace As Workspace) As CodeRefactoringProvider
            Return New MoveTypeCodeRefactoringProvider()
        End Function

        Protected Overrides Function CreateWorkspaceFromFileAsync(
            definition As String,
            ParseOptions As ParseOptions,
            CompilationOptions As CompilationOptions
        ) As Task(Of TestWorkspace)

            Return TestWorkspace.CreateVisualBasicAsync(
                definition,
                ParseOptions,
                If(CompilationOptions, New VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary)))
        End Function

        Protected Overrides Function GetLanguage() As String
            Return LanguageNames.VisualBasic
        End Function

        Protected Overrides Function GetScriptOptions() As ParseOptions
            Return TestOptions.Script
        End Function

        Protected Overloads Function TestRenameFileToMatchTypeAsync(originalCode As XElement, expectedDocumentName As String) As Task
            Return MyBase.TestRenameFileToMatchTypeAsync(originalCode.ConvertTestSourceTag(), expectedDocumentName)
        End Function

        Protected Overloads Function TestMoveTypeToNewFileAsync(
            originalCode As XElement,
            expectedSourceTextAfterRefactoring As XElement,
            expectedDocumentName As String,
            destinationDocumentText As XElement,
            Optional destinationDocumentContainers As IList(Of String) = Nothing,
            Optional expectedCodeAction As Boolean = True,
            Optional index As Integer = 0,
            Optional compareTokens As Boolean = True
        ) As Task

            Dim originalCodeText = originalCode.ConvertTestSourceTag()
            Dim expectedSourceText = expectedSourceTextAfterRefactoring.ConvertTestSourceTag()
            Dim expectedDestinationText = destinationDocumentText.ConvertTestSourceTag()

            Return MyBase.TestMoveTypeToNewFileAsync(
                originalCodeText,
                expectedSourceText,
                expectedDocumentName,
                expectedDestinationText,
                destinationDocumentContainers,
                expectedCodeAction,
                index,
                compareTokens)
        End Function
    End Class
End Namespace
