﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces

Namespace Microsoft.CodeAnalysis.Editor.CodeDefinitionWindow.UnitTests

    Public Class VisualBasicCodeDefinitionWindowTests
        Inherits AbstractCodeDefinitionWindowTests

        <Fact, Trait(Traits.Feature, Traits.Features.CodeDefinitionWindow)>
        Public Async Function ClassFromDefinition() As Task
            Const code As String = "
Class $$[|C|]
End Class"

            Await VerifyContextLocationInSameFile(code, "C")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeDefinitionWindow)>
        Public Async Function ClassFromReference() As Task
            Const code As String = "
Class [|C|]
    Shared Sub M()
        $$C.M()
    End Sub
End Class"

            Await VerifyContextLocationInSameFile(code, "C")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeDefinitionWindow)>
        Public Async Function MethodFromDefinition() As Task
            Const code As String = "
Class C
    Sub $$[|M|]()
    End Sub
End Class"

            Await VerifyContextLocationInSameFile(code, "Public Sub M()")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeDefinitionWindow)>
        Public Async Function MethodFromReference() As Task
            Const code As String = "
Class C
    Sub [|M|]()
        Me.$$M()
    End Sub
End Class"

            Await VerifyContextLocationInSameFile(code, "Public Sub M()")
        End Function

        Protected Overrides Function CreateWorkspaceAsync(code As String) As Task(Of TestWorkspace)
            Return TestWorkspace.CreateVisualBasicAsync(code)
        End Function
    End Class
End Namespace
