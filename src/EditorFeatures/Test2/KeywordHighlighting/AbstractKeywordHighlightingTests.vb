' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Editor.Implementation.Highlighting
Imports Microsoft.CodeAnalysis.Editor.Shared.Extensions
Imports Microsoft.CodeAnalysis.Editor.Shared.Options
Imports Microsoft.CodeAnalysis.Editor.Tagging
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
Imports Microsoft.CodeAnalysis.Shared.Extensions
Imports Microsoft.CodeAnalysis.Shared.TestHooks
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.VisualStudio.Text
Imports Microsoft.VisualStudio.Text.Tagging
Imports Roslyn.Test.Utilities
Imports Roslyn.Utilities

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.KeywordHighlighting

    Public MustInherit Class AbstractKeywordHighlightingTests
        Protected Sub VerifyHighlights(test As XElement, Optional optionIsEnabled As Boolean = True)
            Using workspace = TestWorkspaceFactory.CreateWorkspace(test)
                Dim testDocument = workspace.Documents.Single(Function(d) d.CursorPosition.HasValue)
                Dim buffer = testDocument.TextBuffer
                Dim snapshot = testDocument.InitialTextSnapshot
                Dim caretPosition = testDocument.CursorPosition.Value
                Dim document As Document = workspace.CurrentSolution.Projects.First.Documents.First

                workspace.Options = workspace.Options.WithChangedOption(FeatureOnOffOptions.KeywordHighlighting, document.Project.Language, optionIsEnabled)

                Dim highlightingService = workspace.GetService(Of IHighlightingService)()
                Dim tagProducer = New HighlighterViewTaggerProvider(
                    highlightingService,
                    workspace.GetService(Of IForegroundNotificationService),
                    AggregateAsynchronousOperationListener.EmptyListeners)

                Dim snapshotSpans = {New DocumentSnapshotSpan(document, New SnapshotSpan(snapshot, 0, snapshot.Length))}
                Dim context = New AsynchronousTaggerContext(Of HighlightTag)(
                    snapshotSpans, New SnapshotPoint(snapshot, caretPosition), Nothing, CancellationToken.None)
                tagProducer.ProduceTagsAsync(context).Wait()

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
        End Sub

    End Class

End Namespace
