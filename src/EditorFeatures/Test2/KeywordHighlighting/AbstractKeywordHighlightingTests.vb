﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Editor.Implementation.Highlighting
Imports Microsoft.CodeAnalysis.Editor.Shared.Extensions
Imports Microsoft.CodeAnalysis.Editor.Shared.Options
Imports Microsoft.CodeAnalysis.Editor.Shared.Utilities
Imports Microsoft.CodeAnalysis.Editor.Tagging
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
Imports Microsoft.CodeAnalysis.Highlighting
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.Shared.TestHooks
Imports Microsoft.VisualStudio.Text
Imports Roslyn.Utilities

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.KeywordHighlighting

    <[UseExportProvider]>
    Public MustInherit Class AbstractKeywordHighlightingTests
        Protected Async Function VerifyHighlightsAsync(test As XElement, Optional optionIsEnabled As Boolean = True) As Tasks.Task
            Using workspace = TestWorkspace.Create(test)
                Dim testDocument = workspace.Documents.Single(Function(d) d.CursorPosition.HasValue)
                Dim snapshot = testDocument.GetTextBuffer().CurrentSnapshot
                Dim caretPosition = testDocument.CursorPosition.Value
                Dim document As Document = workspace.CurrentSolution.Projects.First.Documents.First
                Dim globalOptions = workspace.GetService(Of IGlobalOptionService)

                globalOptions.SetGlobalOption(New OptionKey(FeatureOnOffOptions.KeywordHighlighting, document.Project.Language), optionIsEnabled)

                WpfTestRunner.RequireWpfFact($"{NameOf(AbstractKeywordHighlightingTests)}.{NameOf(Me.VerifyHighlightsAsync)} creates asynchronous taggers")

                Dim tagProducer = New HighlighterViewTaggerProvider(
                    workspace.GetService(Of IThreadingContext),
                    workspace.GetService(Of IHighlightingService)(),
                    globalOptions,
                    AsynchronousOperationListenerProvider.NullProvider)

                Dim context = New TaggerContext(Of KeywordHighlightTag)(document, snapshot, New SnapshotPoint(snapshot, caretPosition))
                Await tagProducer.GetTestAccessor().ProduceTagsAsync(context)

                Dim producedTags = From tag In context.tagSpans
                                   Order By tag.Span.Start
                                   Select (tag.Span.Span.ToTextSpan().ToString())

                Dim expectedTags As New List(Of String)

                For Each hostDocument In workspace.Documents
                    For Each span In hostDocument.SelectedSpans
                        expectedTags.Add(span.ToString())
                    Next
                Next

                AssertEx.Equal(expectedTags, producedTags)
            End Using
        End Function

    End Class

End Namespace
