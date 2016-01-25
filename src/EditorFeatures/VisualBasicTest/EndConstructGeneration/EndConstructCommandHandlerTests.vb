' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Editor.Commands
Imports Microsoft.CodeAnalysis.Editor.Implementation.EndConstructGeneration
Imports Microsoft.CodeAnalysis.Editor.VisualBasic.EndConstructGeneration
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.VisualStudio.Text
Imports Microsoft.VisualStudio.Text.Editor
Imports Moq
Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.EndConstructGeneration
    Public Class EndConstructCommandHandlerTests
        Private _endConstructServiceMock As New Mock(Of IEndConstructGenerationService)
        Private _featureOptions As New Mock(Of IOptionService)(MockBehavior.Strict)
        Private _textViewMock As New Mock(Of ITextView)
        Private _textBufferMock As New Mock(Of ITextBuffer)

#If False Then
        ' TODO(jasonmal): Figure out how to enable these tests.
        <WpfFact, Trait(Traits.Feature, Traits.Features.EndConstructGeneration)>
        Public Sub ServiceNotCompletingShouldCallNextHandler()
            _endConstructServiceMock.Setup(Function(s) s.TryDo(It.IsAny(Of ITextView), It.IsAny(Of ITextBuffer), It.IsAny(Of Char))).Returns(False)
            _featureOptions.Setup(Function(s) s.GetOption(FeatureOnOffOptions.EndConstruct)).Returns(True)

            Dim nextHandlerCalled = False
            Dim handler As New EndConstructCommandHandler(_featureOptions.Object, _endConstructServiceMock.Object)
            handler.ExecuteCommand_ReturnKeyCommandHandler(New ReturnKeyCommandArgs(_textViewMock.Object, _textBufferMock.Object), Sub() nextHandlerCalled = True)

            Assert.True(nextHandlerCalled)
        End Sub

        <WpfFact, Trait(Traits.Feature, Traits.Features.EndConstructGeneration)>
        Public Sub ServiceCompletingShouldCallNextHandler()
            _endConstructServiceMock.Setup(Function(s) s.TryDo(It.IsAny(Of ITextView), It.IsAny(Of ITextBuffer), It.IsAny(Of Char))).Returns(True)
            _featureOptions.Setup(Function(s) s.GetOption(FeatureOnOffOptions.EndConstruct)).Returns(True)

            Dim nextHandlerCalled = False
            Dim handler As New EndConstructCommandHandler(_featureOptions.Object, _endConstructServiceMock.Object)
            handler.ExecuteCommand_ReturnKeyCommandHandler(New ReturnKeyCommandArgs(_textViewMock.Object, _textBufferMock.Object), Sub() nextHandlerCalled = True)

            Assert.False(nextHandlerCalled)
        End Sub
#End If

        <WpfFact, Trait(Traits.Feature, Traits.Features.EndConstructGeneration)>
        <WorkItem(544556)>
        Public Async Function EndConstruct_AfterCodeCleanup() As Threading.Tasks.Task
            Dim code = <code>Class C
    Sub Main(args As String())
        Dim z = 1
        Dim y = 2
        If z &gt;&lt; y Then 
    End Sub
End Class</code>.Value.Replace(vbLf, vbCrLf)

            Dim expected = <code>Class C
    Sub Main(args As String())
        Dim z = 1
        Dim y = 2
        If z &lt;&gt; y Then 

        End If
    End Sub
End Class</code>.Value.Replace(vbLf, vbCrLf)

            Await VerifyAppliedAfterReturnUsingCommandHandlerAsync(code, {4, -1}, expected, {5, 12})
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.EndConstructGeneration)>
        <WorkItem(546798)>
        Public Async Function EndConstruct_AfterCodeCleanup_FormatOnlyTouched() As Threading.Tasks.Task
            Dim code = <code>Class C1
    Sub M1()
        System.Diagnostics. _Debug.Assert(True)
    End Sub
End Class</code>.Value.Replace(vbLf, vbCrLf)

            Dim expected = <code>Class C1
    Sub M1()
        System.Diagnostics. _
            Debug.Assert(True)
    End Sub
End Class</code>.Value.Replace(vbLf, vbCrLf)

            Await VerifyAppliedAfterReturnUsingCommandHandlerAsync(code, {2, 29}, expected, {3, 12})
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.EndConstructGeneration)>
        <WorkItem(531347)>
        Public Async Function EndConstruct_AfterCodeCleanup_FormatOnly_WhenContainsDiagnostics() As Threading.Tasks.Task
            Dim code = <code>Module Program
    Sub Main(args As String())
        Dim a
        'Comment
        Dim b
        Dim c
    End Sub
End Module</code>.Value.Replace(vbLf, vbCrLf)

            Dim expected = <code>Module Program
    Sub Main(args As String())
        Dim a
        'Comment
        Dim b

        Dim c
    End Sub
End Module</code>.Value.Replace(vbLf, vbCrLf)

            Await VerifyAppliedAfterReturnUsingCommandHandlerAsync(code, {4, -1}, expected, {5, 8})
        End Function

        <WorkItem(628656)>
        <WpfFact, Trait(Traits.Feature, Traits.Features.EndConstructGeneration)>
        Public Async Function EndConstruct_NotOnLineFollowingToken() As Threading.Tasks.Task
            Await VerifyStatementEndConstructNotAppliedAsync(
                text:="Class C

",
                caret:={2, 0})
        End Function
    End Class
End Namespace
