' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Extensions
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.Simplification
Imports Roslyn.Utilities

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.Expansion
    Public MustInherit Class AbstractExpansionTest

        Protected Async Function TestAsync(definition As XElement, expected As XElement, Optional useLastProject As Boolean = False, Optional expandParameter As Boolean = False) As System.Threading.Tasks.Task
            Using workspace = Await TestWorkspace.CreateAsync(definition)
                Dim hostDocument = If(Not useLastProject, workspace.Documents.Single(), workspace.Documents.Last())

                If hostDocument.AnnotatedSpans.Count <> 1 Then
                    Assert.True(False, "Encountered unexpected span annotation -- only one of 'Expand' or 'ExpandAndSimplify' is legal")
                End If

                Dim document = If(Not useLastProject, workspace.CurrentSolution.Projects.Single(), workspace.CurrentSolution.Projects.Last()).Documents.Single()
                Dim languageServices = document.Project.LanguageServices

                Dim root = Await document.GetSyntaxRootAsync()

                If (hostDocument.AnnotatedSpans.ContainsKey("Expand")) Then
                    For Each span In hostDocument.AnnotatedSpans("Expand")
                        Dim node = GetExpressionSyntaxWithSameSpan(root.FindToken(span.Start).Parent, span.End)
                        root = root.ReplaceNode(node, Await Simplifier.ExpandAsync(node, document, expandInsideNode:=Nothing, expandParameter:=expandParameter))
                    Next
                ElseIf (hostDocument.AnnotatedSpans.ContainsKey("ExpandAndSimplify")) Then
                    For Each span In hostDocument.AnnotatedSpans("ExpandAndSimplify")
                        Dim node = GetExpressionSyntaxWithSameSpan(root.FindToken(span.Start).Parent, span.End)
                        root = root.ReplaceNode(node, Await Simplifier.ExpandAsync(node, document, expandInsideNode:=Nothing, expandParameter:=expandParameter))
                        document = document.WithSyntaxRoot(root)
                        document = Await Simplifier.ReduceAsync(document, Simplifier.Annotation)
                        root = Await document.GetSyntaxRootAsync()
                    Next
                End If

                document = document.WithSyntaxRoot(root)
                document = Await Formatter.FormatAsync(document)

                Dim actualText = (Await document.GetTextAsync()).ToString()

                Assert.Equal(expected.NormalizedValue.Trim(), actualText.Trim())
            End Using
        End Function

        Private Function GetExpressionSyntaxWithSameSpan(node As SyntaxNode, spanEnd As Integer) As SyntaxNode
            While Not node Is Nothing And Not node.Parent Is Nothing And node.Parent.SpanStart = node.SpanStart
                node = node.Parent
                If node.Span.End = spanEnd Then
                    Exit While
                End If
            End While
            Return node
        End Function

    End Class
End Namespace
