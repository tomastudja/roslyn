﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Editor.UnitTests.SplitComment

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.SplitComment
    <UseExportProvider>
    Public Class SplitCommentCommandHandlerTests
        Inherits AbstractSplitCommentCommandHandlerTests

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitStartOfComment()
            TestHandled(
"Module Program
    Sub Main(args As String())
        '[||]Test Comment
    End Sub
End Module
",
"Module Program
    Sub Main(args As String())
        '
        ' Test Comment
    End Sub
End Module
")
        End Sub

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitMiddleOfComment()
            TestHandled(
"Module Program
    Sub Main(args As String())
        ' Test [||]Comment
    End Sub
End Module
",
"Module Program
    Sub Main(args As String())
        ' Test
        ' Comment
    End Sub
End Module
")
        End Sub

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitEndOfComment()
            TestNotHandled(
"Module Program
    Sub Main(args As String())
        ' Test Comment[||]
    End Sub
End Module
")
        End Sub

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitCommentOutOfMethod()
            TestHandled(
"Module Program
    Sub Main(args As String())
        
    End Sub
    ' Test [||]Comment
End Module
",
"Module Program
    Sub Main(args As String())
        
    End Sub
    ' Test
    ' Comment
End Module
")
        End Sub

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitCommentOutOfModule()
            TestHandled(
"Module Program
    Sub Main(args As String())
        
    End Sub
End Module
' Test [||]Comment
",
"Module Program
    Sub Main(args As String())
        
    End Sub
End Module
' Test
' Comment
")
        End Sub

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitCommentOutOfClass()
            TestHandled(
"Class Program
    Public Shared Sub Main(args As String())
        
    End Sub
End Class
' Test [||]Comment
",
"Class Program
    Public Shared Sub Main(args As String())
        
    End Sub
End Class
' Test
' Comment
")
        End Sub

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitCommentOutOfNamespace()
            TestHandled(
"Namespace TestNamespace
    Module Program
        Sub Main(args As String())

        End Sub
    End Module
End Namespace
' Test [||]Comment
",
"Namespace TestNamespace
    Module Program
        Sub Main(args As String())

        End Sub
    End Module
End Namespace
' Test
' Comment
")
        End Sub

        <WorkItem(38516, "https://github.com/dotnet/roslyn/issues/38516")>
        <WpfFact, Trait(Traits.Feature, Traits.Features.SplitComment)>
        Public Sub TestSplitCommentWithLineContinuation()
            TestHandled(
"Module Program
    Sub Main(args As String())
        Dim X As Integer _ ' Comment [||]is here
                       = 4
    End Sub
End Module
",
"Module Program
    Sub Main(args As String())
        Dim X As Integer _ ' Comment
 _ ' is here
                       = 4
    End Sub
End Module
")
        End Sub
    End Class
End Namespace
