﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Composition
Imports System.Threading
Imports Microsoft.CodeAnalysis.Classification
Imports Microsoft.CodeAnalysis.Editor.Implementation.Classification
Imports Microsoft.CodeAnalysis.Editor.Shared.Extensions
Imports Microsoft.CodeAnalysis.Editor.Shared.Utilities
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
Imports Microsoft.CodeAnalysis.Host
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Shared.TestHooks
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.UnitTests
Imports Microsoft.VisualStudio.Text
Imports Microsoft.VisualStudio.Text.Classification
Imports Microsoft.VisualStudio.Text.Tagging
Imports Roslyn.Utilities

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.Classification
    <[UseExportProvider]>
    Public Class ClassificationTests
        <Fact, WorkItem(65926, "https://github.com/dotnet/roslyn/issues/65926")>
        Public Async Function TestEmbeddedClassifications1() As Task
            Using workspace = TestWorkspace.Create(
                <Workspace>
                    <Project Language="C#" AssemblyName="TestAssembly" CommonReferences="true">
                        <Document>
                        using System.Text.RegularExpressions;

                        class C
                        {
                            private Regex re = new Regex("()");
                        }
                        </Document>
                    </Project>
                </Workspace>)

                Dim document = workspace.CurrentSolution.Projects.Single().Documents.Single()
                Dim text = Await document.GetTextAsync()

                Dim spans = Await ClassifierHelper.GetClassifiedSpansAsync(
                    document, New TextSpan(0, text.Length), ClassificationOptions.Default, CancellationToken.None)

                Assert.Equal(
"(text, '<spaces>', [0..26))
(keyword, 'using', [26..31))
(text, '<spaces>', [31..32))
(namespace name, 'System', [32..38))
(operator, '.', [38..39))
(namespace name, 'Text', [39..43))
(operator, '.', [43..44))
(namespace name, 'RegularExpressions', [44..62))
(punctuation, ';', [62..63))
(text, '<spaces>', [63..91))
(keyword, 'class', [91..96))
(text, '<spaces>', [96..97))
(class name, 'C', [97..98))
(text, '<spaces>', [98..124))
(punctuation, '{', [124..125))
(text, '<spaces>', [125..155))
(keyword, 'private', [155..162))
(text, '<spaces>', [162..163))
(class name, 'Regex', [163..168))
(text, '<spaces>', [168..169))
(field name, 're', [169..171))
(text, '<spaces>', [171..172))
(operator, '=', [172..173))
(text, '<spaces>', [173..174))
(keyword, 'new', [174..177))
(text, '<spaces>', [177..178))
(class name, 'Regex', [178..183))
(punctuation, '(', [183..184))
(string, '""', [184..185))
(regex - grouping, '(', [185..186))
(regex - grouping, ')', [186..187))
(string, '""', [187..188))
(punctuation, ')', [188..189))
(punctuation, ';', [189..190))
(text, '<spaces>', [190..216))
(punctuation, '}', [216..217))", String.Join(vbCrLf, spans.Select(Function(s) ToTestString(text, s))))
            End Using
        End Function

        Private Shared Function ToTestString(text As SourceText, span As ClassifiedSpan) As String
            Dim subText = text.ToString(span.TextSpan)
            Return $"({span.ClassificationType}, '{If(subText.Trim() = "", "<spaces>",
                subText)}', {span.TextSpan})"
        End Function

        <WpfFact, WorkItem(13753, "https://github.com/dotnet/roslyn/issues/13753")>
        Public Async Function TestSemanticClassificationWithoutSyntaxTree() As Task
            Dim workspaceDefinition =
            <Workspace>
                <Project Language="NoCompilation" AssemblyName="TestAssembly" CommonReferencesPortable="true">
                    <Document>
                        var x = {}; // e.g., TypeScript code or anything else that doesn't support compilations
                    </Document>
                </Project>
            </Workspace>

            Dim composition = EditorTestCompositions.EditorFeatures.AddParts(
                GetType(NoCompilationContentTypeDefinitions),
                GetType(NoCompilationContentTypeLanguageService),
                GetType(NoCompilationEditorClassificationService))

            Using workspace = TestWorkspace.Create(workspaceDefinition, composition:=composition)
                Dim listenerProvider = workspace.ExportProvider.GetExportedValue(Of IAsynchronousOperationListenerProvider)

                Dim provider = New SemanticClassificationViewTaggerProvider(
                    workspace.GetService(Of IThreadingContext),
                    workspace.GetService(Of ClassificationTypeMap),
                    workspace.GetService(Of IGlobalOptionService),
                    visibilityTracker:=Nothing,
                    listenerProvider)

                Dim buffer = workspace.Documents.First().GetTextBuffer()
                Dim tagger = provider.CreateTagger(Of IClassificationTag)(
                    workspace.Documents.First().GetTextView(),
                    buffer)

                Using edit = buffer.CreateEdit()
                    edit.Insert(0, " ")
                    edit.Apply()
                End Using

                Using DirectCast(tagger, IDisposable)
                    Await listenerProvider.GetWaiter(FeatureAttribute.Classification).ExpeditedWaitAsync()

                    ' Note: we don't actually care what results we get back.  We're just
                    ' verifying that we don't crash because the SemanticViewTagger ends up
                    ' calling SyntaxTree/SemanticModel code.
                    tagger.GetTags(New NormalizedSnapshotSpanCollection(
                        New SnapshotSpan(buffer.CurrentSnapshot, New Span(0, 1))))
                End Using
            End Using
        End Function

        <WpfFact>
        Public Sub TestFailOverOfMissingClassificationType()
            Dim exportProvider = EditorTestCompositions.EditorFeatures.ExportProviderFactory.CreateExportProvider()

            Dim typeMap = exportProvider.GetExportedValue(Of ClassificationTypeMap)
            Dim formatMap = exportProvider.GetExportedValue(Of IClassificationFormatMapService).GetClassificationFormatMap("tooltip")

            Dim classifiedText = New ClassifiedText("UnknownClassificationType", "dummy")
            Dim run = classifiedText.ToRun(formatMap, typeMap)

            Assert.NotNull(run)
        End Sub

        <WpfFact, WorkItem(13753, "https://github.com/dotnet/roslyn/issues/13753")>
        Public Async Function TestWrongDocument() As Task
            Dim workspaceDefinition =
            <Workspace>
                <Project Language="NoCompilation" AssemblyName="NoCompilationAssembly" CommonReferencesPortable="true">
                    <Document>
                        var x = {}; // e.g., TypeScript code or anything else that doesn't support compilations
                    </Document>
                </Project>
                <Project Language="C#" AssemblyName="CSharpAssembly" CommonReferencesPortable="true">
                </Project>
            </Workspace>

            Dim composition = EditorTestCompositions.EditorFeatures.AddParts(
                GetType(NoCompilationContentTypeLanguageService),
                GetType(NoCompilationContentTypeDefinitions))

            Using workspace = TestWorkspace.Create(workspaceDefinition, composition:=composition)
                Dim project = workspace.CurrentSolution.Projects.First(Function(p) p.Language = LanguageNames.CSharp)
                Dim classificationService = project.Services.GetService(Of IClassificationService)()

                Dim wrongDocument = workspace.CurrentSolution.Projects.First(Function(p) p.Language = "NoCompilation").Documents.First()
                Dim text = Await wrongDocument.GetTextAsync(CancellationToken.None)

                ' make sure we don't crash with wrong document
                Dim result = New ArrayBuilder(Of ClassifiedSpan)()
                Await classificationService.AddSyntacticClassificationsAsync(wrongDocument, New TextSpan(0, text.Length), result, CancellationToken.None)
                Await classificationService.AddSemanticClassificationsAsync(wrongDocument, New TextSpan(0, text.Length), options:=Nothing, result, CancellationToken.None)
            End Using
        End Function

        <ExportLanguageService(GetType(IClassificationService), NoCompilationConstants.LanguageName, ServiceLayer.Test), [Shared], PartNotDiscoverable>
        Private Class NoCompilationEditorClassificationService
            Implements IClassificationService

            <ImportingConstructor>
            <Obsolete(MefConstruction.ImportingConstructorMessage, True)>
            Public Sub New()
            End Sub

            Public Sub AddLexicalClassifications(text As SourceText, textSpan As TextSpan, result As ArrayBuilder(Of ClassifiedSpan), cancellationToken As CancellationToken) Implements IClassificationService.AddLexicalClassifications
            End Sub

            Public Sub AddSyntacticClassifications(services As SolutionServices, root As SyntaxNode, textSpan As TextSpan, result As ArrayBuilder(Of ClassifiedSpan), cancellationToken As CancellationToken) Implements IClassificationService.AddSyntacticClassifications
            End Sub

            Public Function AddSemanticClassificationsAsync(document As Document, textSpan As TextSpan, options As ClassificationOptions, result As ArrayBuilder(Of ClassifiedSpan), cancellationToken As CancellationToken) As Task Implements IClassificationService.AddSemanticClassificationsAsync
                Return Task.CompletedTask
            End Function

            Public Function AddSyntacticClassificationsAsync(document As Document, textSpan As TextSpan, result As ArrayBuilder(Of ClassifiedSpan), cancellationToken As CancellationToken) As Task Implements IClassificationService.AddSyntacticClassificationsAsync
                Return Task.CompletedTask
            End Function

            Public Function AdjustStaleClassification(text As SourceText, classifiedSpan As ClassifiedSpan) As ClassifiedSpan Implements IClassificationService.AdjustStaleClassification
            End Function

            Public Function ComputeSyntacticChangeRangeAsync(oldDocument As Document, newDocument As Document, timeout As TimeSpan, cancellationToken As CancellationToken) As ValueTask(Of TextChangeRange?) Implements IClassificationService.ComputeSyntacticChangeRangeAsync
                Return New ValueTask(Of TextChangeRange?)
            End Function

            Public Function ComputeSyntacticChangeRange(services As SolutionServices, oldRoot As SyntaxNode, newRoot As SyntaxNode, timeout As TimeSpan, cancellationToken As CancellationToken) As TextChangeRange? Implements IClassificationService.ComputeSyntacticChangeRange
                Return Nothing
            End Function

            Public Function AddEmbeddedLanguageClassificationsAsync(document As Document, textSpan As TextSpan, options As ClassificationOptions, result As ArrayBuilder(Of ClassifiedSpan), cancellationToken As CancellationToken) As Task Implements IClassificationService.AddEmbeddedLanguageClassificationsAsync
                Return Task.CompletedTask
            End Function
        End Class
    End Class
End Namespace
