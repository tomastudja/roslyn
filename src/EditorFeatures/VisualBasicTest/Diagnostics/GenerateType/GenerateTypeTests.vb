' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Option Strict Off
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.CodeStyle
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Extensions
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.Simplification
Imports Microsoft.CodeAnalysis.VisualBasic.CodeFixes.GenerateType
Imports Microsoft.CodeAnalysis.VisualBasic.Diagnostics

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Diagnostics.GenerateType
    Public Class GenerateTypeTests
        Inherits AbstractVisualBasicDiagnosticProviderBasedUserDiagnosticTest

        Friend Overrides Function CreateDiagnosticProviderAndFixer(workspace As Workspace) As Tuple(Of DiagnosticAnalyzer, CodeFixProvider)
            Return New Tuple(Of DiagnosticAnalyzer, CodeFixProvider)(Nothing, New GenerateTypeCodeFixProvider())
        End Function

        Protected Overrides Function MassageActions(actions As IList(Of CodeAction)) As IList(Of CodeAction)
            Return FlattenActions(actions)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeParameterFromArgumentInferT() As Task
            Await TestAsync(
"Module Program
    Sub Main()
        Dim f As [|Foo(Of Integer)|]
    End Sub
End Module",
"Module Program
    Sub Main()
        Dim f As Foo(Of Integer)
    End Sub
End Module
Friend Class Foo(Of T)
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateClassFromTypeParameter() As Task
            Await TestAsync(
"Class C
    Dim emp As List(Of [|Employee|])
End Class",
"Class C
    Dim emp As List(Of Employee)
    Private Class Employee
    End Class
End Class",
index:=2)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateClassFromFieldDeclarationIntoSameType() As Task
            Await TestAsync(
"Class C
    dim f as [|Foo|]
End Class",
"Class C
    dim f as Foo
    Private Class Foo
    End Class
End Class",
index:=2)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateClassFromFieldDeclarationIntoSameNamespace() As Task
            Await TestAsync(
"Class C
    dim f as [|Foo|]
End Class",
"Class C
    dim f as Foo
End Class
Friend Class Foo
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestMissingOnLowercaseName() As Task
            Await TestMissingAsync(
"Class C
    dim f as [|foo|]
End Class")
        End Function

        <WorkItem(539716, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539716")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateClassFromFullyQualifiedFieldIntoSameNamespace() As Task
            Await TestAsync(
"Namespace NS
    Class Foo
        Private x As New NS.[|Bar|]
    End Class
End Namespace",
"Namespace NS
    Class Foo
        Private x As New NS.Bar
    End Class
    Friend Class Bar
    End Class
End Namespace",
index:=1,
parseOptions:=Nothing) ' Namespaces not supported in script
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateClassWithCtorFromObjectCreation() As Task
            Await TestAsync(
"Class C
    Dim f As Foo = New [|Foo|]()
End Class",
"Class C
    Dim f As Foo = New Foo()
    Private Class Foo
        Public Sub New()
        End Sub
    End Class
End Class",
index:=2)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestCreateException() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Throw New [|Foo|]()
    End Sub
End Module",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.Serialization
Module Program
    Sub Main(args As String())
        Throw New Foo()
    End Sub
End Module
<Serializable> Friend Class Foo
    Inherits Exception
    Public Sub New()
    End Sub
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
    Public Sub New(message As String, innerException As Exception)
        MyBase.New(message, innerException)
    End Sub
    Protected Sub New(info As SerializationInfo, context As StreamingContext)
        MyBase.New(info, context)
    End Sub
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestCreateFieldDelegatingConstructor() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Call New [|Foo|](1, ""blah"")
    End Sub
End Module",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Call New Foo(1, ""blah"")
    End Sub
End Module
Friend Class Foo
    Private v1 As Integer
    Private v2 As String
    Public Sub New(v1 As Integer, v2 As String)
        Me.v1 = v1
        Me.v2 = v2
    End Sub
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestCreateBaseDelegatingConstructor() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Dim d As B = New [|D|](4)
    End Sub
End Module
Class B
    Protected Sub New(value As Integer)
    End Sub
End Class",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Dim d As B = New D(4)
    End Sub
End Module
Friend Class D
    Inherits B
    Public Sub New(value As Integer)
        MyBase.New(value)
    End Sub
End Class
Class B
    Protected Sub New(value As Integer)
    End Sub
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateIntoNamespace() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Namespace Outer
    Module Program
        Sub Main(args As String())
            Call New [|Blah|]()
        End Sub
    End Module
End Namespace",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Namespace Outer
    Module Program
        Sub Main(args As String())
            Call New Blah()
        End Sub
    End Module
    Friend Class Blah
        Public Sub New()
        End Sub
    End Class
End Namespace",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateAssignmentToBaseField() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(i As Integer)
        Dim d As B = New [|D|](i)
    End Sub
End Module
Class B
    Protected i As Integer
End Class",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(i As Integer)
        Dim d As B = New D(i)
    End Sub
End Module
Friend Class D
    Inherits B
    Public Sub New(i As Integer)
        Me.i = i
    End Sub
End Class
Class B
    Protected i As Integer
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateGenericType() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Class Outer(Of M)
    Sub Main(i As Integer)
        Call New [|Foo(Of M)|]
    End Sub
End Class",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Class Outer(Of M)
    Sub Main(i As Integer)
        Call New Foo(Of M)
    End Sub
End Class
Friend Class Foo(Of M)
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateIntoClass() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Class Outer(Of M)
    Sub Main(i As Integer)
        Call New [|Foo(Of M)|]
    End Sub
End Class",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Class Outer(Of M)
    Sub Main(i As Integer)
        Call New Foo(Of M)
    End Sub
    Private Class Foo(Of M)
    End Class
End Class",
index:=2)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateIntoClassFromFullyQualifiedInvocation() As Task
            Await TestAsync(
"Class Program
    Sub Test()
        Dim d = New [|Program.Foo|]()
    End Sub
End Class",
"Class Program
    Sub Test()
        Dim d = New Program.Foo()
    End Sub
    Private Class Foo
        Public Sub New()
        End Sub
    End Class
End Class")
        End Function

        <WorkItem(5776, "DevDiv_Projects/Roslyn")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateIntoNamespaceFromFullyQualifiedInvocation() As Task
            Await TestAsync(
"Namespace Foo
    Class Program
        Sub Test()
            Dim d = New [|Foo.Bar|]()
        End Sub
    End Class
End Namespace",
"Namespace Foo
    Class Program
        Sub Test()
            Dim d = New Foo.Bar()
        End Sub
    End Class
    Friend Class Bar
        Public Sub New()
        End Sub
    End Class
End Namespace",
index:=1,
parseOptions:=Nothing) ' Namespaces not supported in script
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestInSecondConstraintClause() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Class Program(Of T As {Foo, [|IBar|]})
End Class",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Class Program(Of T As {Foo, IBar})
End Class
Friend Interface IBar
End Interface",
index:=1)
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateIntoNewNamespace() As Task
            Await TestAddDocument(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Class Program
    Sub Main()
        Call New Foo.[|Bar|]()
    End Sub
End Class",
"Namespace Foo
    Friend Class Bar
        Public Sub New()
        End Sub
    End Class
End Namespace",
expectedContainers:={"Foo"},
expectedDocumentName:="Bar.vb")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateIntoGlobalNamespaceNewFile() As Task
            Await TestAddDocument(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Dim x As New [|Foo|]
    End Sub
End Module",
"Friend Class Foo
End Class",
expectedContainers:=Array.Empty(Of String)(),
expectedDocumentName:="Foo.vb")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeThatImplementsInterface1() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Dim d As [|IFoo|] = New Foo()
    End Sub
End Module",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Dim d As IFoo = New Foo()
    End Sub
End Module
Friend Interface IFoo
End Interface",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeThatImplementsInterface2() As Task
            Await TestAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Dim d As IFoo = New [|Foo|]()
    End Sub
End Module
Friend Interface IFoo
End Interface",
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        Dim d As IFoo = New Foo()
    End Sub
End Module
Friend Class Foo
    Implements IFoo
End Class
Friend Interface IFoo
End Interface",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeWithNamedArguments() As Task
            Await TestAsync(
"Class Program
    Sub Test()
        Dim x = New [|Bar|](value:=7)
    End Sub
End Class",
"Class Program
    Sub Test()
        Dim x = New Bar(value:=7)
    End Sub
End Class
Friend Class Bar
    Private value As Integer
    Public Sub New(value As Integer)
        Me.value = value
    End Sub
End Class",
index:=1)
        End Function

        <WorkItem(539730, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539730")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNotIntoType() As Task
            Await TestActionCountAsync(
"Class Program
    Inherits [|Temp|]
    Sub Test()
    End Sub
End Class",
count:=3)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateClassFromReturnType() As Task
            Await TestAsync(
"Class Foo
    Function F() As [|Bar|]
    End Function
End Class",
"Class Foo
    Function F() As Bar
    End Function
End Class
Public Class Bar
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateClassWhereKeywordBecomesTypeName() As Task
            Await TestAsync(
"Class Foo
    Dim x As New [|[Class]|]
End Class",
"Class Foo
    Dim x As New [Class]
End Class
Friend Class [Class]
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNegativeTestGenerateClassFromEscapedType() As Task
            Await TestAsync(
"Class Foo
    Dim x as New [|[Bar]|]
End Class",
"Class Foo
    Dim x as New [Bar]
End Class
Friend Class Bar
End Class",
index:=1)
        End Function

        <WorkItem(539716, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539716")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeIntoContainingNamespace() As Task
            Await TestAsync(
"Namespace NS
    Class Foo
        Dim x As New NS.[|Bar|]
    End Class
End Namespace",
"Namespace NS
    Class Foo
        Dim x As New NS.Bar
    End Class
    Friend Class Bar
    End Class
End Namespace",
index:=1,
parseOptions:=Nothing) ' Namespaces not supported in script
        End Function

        <WorkItem(539736, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539736")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeIntoContainingModule() As Task
            Await TestAsync(
"Module M
    Dim x As [|C|]
End Module",
"Module M
    Dim x As C
    Private Class C
    End Class
End Module",
index:=2)
        End Function

        <WorkItem(539737, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539737")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateInterfaceInImplementsStatement() As Task
            Await TestAsync(
"Class C
    Implements [|D|]
End Class",
"Class C
    Implements D
End Class
Friend Interface D
End Interface",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAbsenceOfGenerateIntoInvokingTypeForConstraintList() As Task
            Await TestActionCountAsync(
"Class EmployeeList(Of T As [|Employee|])
End Class",
count:=3,
parseOptions:=TestOptions.Regular)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestMissingOnImportsDirective() As Task
            Await TestMissingAsync(
"Imports [|System|]")
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNoContainersInNewType() As Task
            Await TestAddDocument(
"Class Base
    Sub Main
        Dim p = New [|Derived|]()
    End Sub
End Class",
"Friend Class Derived
    Public Sub New()
    End Sub
End Class",
expectedContainers:=Array.Empty(Of String)(),
expectedDocumentName:="Derived.vb")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNotOfferedInsideBinaryExpressions() As Task
            Await TestMissingAsync(
"Class Base
    Sub Main
        Dim a = 1 + [|Foo|]
    End Sub
End Class")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNotOfferedIfLeftSideOfDotIsNotAName() As Task
            Await TestMissingAsync(
"Module Program
    Sub Main(args As String())
        Call 1.[|T|]
    End Sub
End Module")
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNotOfferedIfLeftFromDotIsNotAName() As Task
            Await TestMissingAsync(
"Class C1
    Sub Foo
        Me.[|Foo|] = 3
    End Sub
End Class")
        End Function

        <WorkItem(539786, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539786")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestMissingOnAssignedVariable() As Task
            Await TestMissingAsync(
"Imports System
Imports System.Collections.Generic
Imports System.Linq
Module Program
    Sub Main(args As String())
        [|B|] = 10
    End Sub
End Module")
        End Function

        <WorkItem(539757, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539757")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestArrayInference1() As Task
            Await TestAsync(
"Class Base
    Sub Main
        Dim p() As Base = New [|Derived|](10) {}
    End Sub
End Class",
"Class Base
    Sub Main
        Dim p() As Base = New Derived(10) {}
    End Sub
End Class
Friend Class Derived
    Inherits Base
End Class",
index:=1)
        End Function

        <WorkItem(539757, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539757")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestArrayInference2() As Task
            Await TestAsync(
"Class Base
    Sub Main
        Dim p As Base() = New [|Derived|](10) {}
    End Sub
End Class",
"Class Base
    Sub Main
        Dim p As Base() = New Derived(10) {}
    End Sub
End Class
Friend Class Derived
    Inherits Base
End Class",
index:=1)
        End Function

        <WorkItem(539757, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539757")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestArrayInference3() As Task
            Await TestAsync(
"Class Base
    Sub Main
        Dim p As Base = New [|Derived|](10) {}
    End Sub
End Class",
"Class Base
    Sub Main
        Dim p As Base = New Derived(10) {}
    End Sub
End Class
Friend Class Derived
End Class",
index:=1)
        End Function

        <WorkItem(539749, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539749")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestMatchWithDifferentArity() As Task
            Await TestAsync(
"Class Program
    Private Sub Main()
        Dim f As [|Foo(Of Integer)|]
    End Sub
End Class
Class Foo
End Class",
"Class Program
    Private Sub Main()
        Dim f As Foo(Of Integer)
    End Sub
End Class
Friend Class Foo(Of T)
End Class
Class Foo
End Class",
index:=1)
        End Function

        <WorkItem(540504, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/540504")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNoUnavailableTypeParameters1() As Task
            Await TestAsync(
"Class C(Of T1, T2)
    Sub M(x As T1, y As T2)
        Dim a As Test = New [|Test|](x, y)
    End Sub
End Class",
"Class C(Of T1, T2)
    Sub M(x As T1, y As T2)
        Dim a As Test = New Test(x, y)
    End Sub
End Class
Friend Class Test
    Private x As Object
    Private y As Object
    Public Sub New(x As Object, y As Object)
        Me.x = x
        Me.y = y
    End Sub
End Class",
index:=1)
        End Function

        <WorkItem(540534, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/540534")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestMultipleTypeParamsInConstructor1() As Task
            Await TestAsync(
"Class C(Of T1, T2)
    Sub M(x As T1, y As T2)
        Dim a As Test(Of T1, T2) = New [|Test(Of T1, T2)|](x, y)
    End Sub
End Class",
"Class C(Of T1, T2)
    Sub M(x As T1, y As T2)
        Dim a As Test(Of T1, T2) = New Test(Of T1, T2)(x, y)
    End Sub
End Class
Friend Class Test(Of T1, T2)
    Private x As T1
    Private y As T2
    Public Sub New(x As T1, y As T2)
        Me.x = x
        Me.y = y
    End Sub
End Class",
index:=1)
        End Function

        <WorkItem(540644, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/540644")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateWithVoidArg() As Task
            Await TestAsync(
"Module Program
    Sub Main(args As String())
        Dim x As C = New [|C|](M())
    End Sub
    Sub M()
    End Sub
End Module",
"Module Program
    Sub Main(args As String())
        Dim x As C = New C(M())
    End Sub
    Sub M()
    End Sub
End Module
Friend Class C
    Private v As Object
    Public Sub New(v As Object)
        Me.v = v
    End Sub
End Class",
index:=1)
        End Function

        <WorkItem(539735, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/539735")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestInAsClause() As Task
            Await TestAsync(
"Class D
    Sub M()
        Dim x As New [|C|](4)
    End Sub
End Class",
"Class D
    Sub M()
        Dim x As New C(4)
    End Sub
End Class
Friend Class C
    Private v As Integer
    Public Sub New(v As Integer)
        Me.v = v
    End Sub
End Class",
index:=1)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNotOnConstructorToActualType() As Task
            Await TestMissingAsync(
"Class C
    Sub Test()
        Dim x As Integer = 1
        Dim obj As New [|C|](x)
    End Sub
End Class")
        End Function

        <WorkItem(540986, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/540986")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateAttribute1() As Task
            Await TestAsync(
"<[|AttClass|]()>
Class C
End Class",
"Imports System
<AttClass()>
Class C
End Class
Friend Class AttClassAttribute
    Inherits Attribute
End Class",
index:=1)
        End Function

        <WorkItem(540986, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/540986")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateAttribute2() As Task
            Await TestAsync(
"Imports System
<[|AttClass|]()>
Class C
End Class",
"Imports System
<AttClass()>
Class C
End Class
Friend Class AttClassAttribute
    Inherits Attribute
End Class",
index:=1)
        End Function

        <WorkItem(541607, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/541607")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNotOnDictionaryAccess() As Task
            Await TestMissingAsync(
"Imports System
Imports System.Collections
Imports System.Collections.Generic
Public Class A
    Public Sub Foo()
        Dim Table As Hashtable = New Hashtable()
        Table![|Orange|] = ""A fruit"" 
 Table(""Broccoli"") = ""A vegetable"" 
 Console.WriteLine(Table!Orange)
    End Sub
End Class")
        End Function

        <WorkItem(542392, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/542392")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityConstraint1() As Task
            Await TestAsync(
"Imports System.Runtime.CompilerServices
Module StringExtensions
    <Extension()>
    Public Sub Print(ByVal aString As String, x As [|C|])
        Console.WriteLine(aString)
    End Sub
End Module",
"Imports System.Runtime.CompilerServices
Module StringExtensions
    <Extension()>
    Public Sub Print(ByVal aString As String, x As C)
        Console.WriteLine(aString)
    End Sub
    Public Class C
    End Class
End Module",
index:=2)
        End Function

        <WorkItem(542836, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/542836")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNewLineAfterNestedType() As Task
            Await TestAsync(
<Text>Class A
    Sub Main()
        Dim x As A()() = New [|HERE|]()
    End Sub
End Class</Text>.NormalizedValue,
<Text>Class A
    Sub Main()
        Dim x As A()() = New HERE()
    End Sub

    Private Class HERE
        Public Sub New()
        End Sub
    End Class
End Class</Text>.NormalizedValue,
index:=2,
compareTokens:=False)
        End Function

        <WorkItem(543290, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/543290")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNestedType() As Task
            Await TestAsync(
"Option Explicit Off
Module Program
    Sub Main(args As String())
        Dim i = 2
        Dim r As New i.[|Extension|]
    End Sub
    Public Class i
    End Class
End Module",
"Option Explicit Off
Module Program
    Sub Main(args As String())
        Dim i = 2
        Dim r As New i.Extension
    End Sub
    Public Class i
        Friend Class Extension
        End Class
    End Class
End Module")
        End Function

        <WorkItem(543397, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/543397")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestNewModule() As Task
            Await TestMissingAsync(
"Module Program
    Sub Main
        Dim f As New [|Program|]
    End Sub
End Module")
        End Function

        <WorkItem(545363, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/545363")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestInHiddenNamespace1() As Task
            Await TestExactActionSetOfferedAsync(
<text>
#ExternalSource ("Default.aspx", 1)
Class Program
    Sub Main(args As String())
        Dim f As New [|Foo|]()
    End Sub
End Class
#End ExternalSource
</text>.NormalizedValue,
{String.Format(FeaturesResources.Generate_0_1_in_new_file, "class", "Foo", FeaturesResources.Global_Namespace), String.Format(FeaturesResources.Generate_nested_0_1, "class", "Foo", "Program"), FeaturesResources.Generate_new_type})
        End Function

        <WorkItem(545363, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/545363")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestInHiddenNamespace2() As Task
            Await TestExactActionSetOfferedAsync(
<text>
#ExternalSource ("Default.aspx", 1)
Class Program
    Sub Main(args As String())
        Dim f As New [|Foo|]()
    End Sub
End Class

Class Bar
End Class
#End ExternalSource
</text>.NormalizedValue,
{String.Format(FeaturesResources.Generate_0_1_in_new_file, "class", "Foo", FeaturesResources.Global_Namespace),
String.Format(FeaturesResources.Generate_0_1, "class", "Foo", FeaturesResources.Global_Namespace),
String.Format(FeaturesResources.Generate_nested_0_1, "class", "Foo"), FeaturesResources.Generate_new_type})
        End Function

        <WorkItem(545363, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/545363")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestInHiddenNamespace3() As Task
            Await TestAsync(
<text>
#ExternalSource ("Default.aspx", 1)
Class Program
    Sub Main(args As String())
        Dim f As New [|Foo|]()
    End Sub
End Class

Class Bar
End Class
#End ExternalSource
</text>.NormalizedValue,
<text>
#ExternalSource ("Default.aspx", 1)
Class Program
    Sub Main(args As String())
        Dim f As New Foo()
    End Sub
End Class

Friend Class Foo
    Public Sub New()
    End Sub
End Class

Class Bar
End Class
#End ExternalSource
</text>.NormalizedValue,
index:=1)
        End Function

        <WorkItem(546852, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/546852")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAnonymousMethodArgument() As Task
            Await TestAsync(
"Module Program
    Sub Main()
        Dim c = New [|C|](Function() x)
    End Sub
End Module",
"Imports System
Module Program
    Sub Main()
        Dim c = New C(Function() x)
    End Sub
End Module
Friend Class C
    Private p As Func(Of Object)
    Public Sub New(p As Func(Of Object))
        Me.p = p
    End Sub
End Class",
index:=1)
        End Function

        <WorkItem(546851, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/546851")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestOmittedArguments() As Task
            Await TestAsync(
"Imports System
Module Program
    Sub Main()
        Dim x = New [|C|](,)
    End Sub
End Module",
"Imports System
Module Program
    Sub Main()
        Dim x = New C(,)
    End Sub
End Module
Friend Class C
    Private p1 As Object
    Private p2 As Object
    Public Sub New(p1 As Object, p2 As Object)
        Me.p1 = p1
        Me.p2 = p2
    End Sub
End Class",
index:=1)
        End Function

        <WorkItem(1003618, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1003618")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeThatBindsToNamespace() As Task
            Await TestAsync(
"Imports System
[|<System>|]
Module Program
    Sub Main()
    End Sub
End Module",
"Imports System
<System>
Module Program
    Sub Main()
    End Sub
End Module
Friend Class SystemAttribute
    Inherits Attribute
End Class",
index:=1)
        End Function

        <WorkItem(821277, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/821277")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestTooFewTypeArgument() As Task
            Await TestAsync(
<text>
Class Program
    Sub Main(args As String())
        Dim f As [|AA|]
    End Sub
End Class

Class AA(Of T)
End Class
</text>.NormalizedValue,
<text>
Class Program
    Sub Main(args As String())
        Dim f As AA
    End Sub
End Class

Friend Class AA
End Class

Class AA(Of T)
End Class
</text>.NormalizedValue,
index:=1,
compareTokens:=False)
        End Function

        <WorkItem(821277, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/821277")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestTooMoreTypeArgument() As Task
            Await TestAsync(
<text>
Class Program
    Sub Main(args As String())
        Dim f As [|AA(Of Integer, Integer)|]
    End Sub
End Class

Class AA(Of T)
End Class
</text>.NormalizedValue,
<text>
Class Program
    Sub Main(args As String())
        Dim f As AA(Of Integer, Integer)
    End Sub
End Class

Friend Class AA(Of T1, T2)
End Class

Class AA(Of T)
End Class
</text>.NormalizedValue,
index:=1,
compareTokens:=False)
        End Function

        <WorkItem(942568, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/942568")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeWithPreferIntrinsicPredefinedKeywordFalse() As Task
            Await TestAsync(
<text>
Class Program
    Sub M(args As Integer)
        Dim f = new [|T(args)|]
    End Sub
End Class
</text>.NormalizedValue,
<text>
Class Program
    Sub M(args As Integer)
        Dim f = new T(args)
    End Sub
End Class

Friend Class T
    Private args As System.Int32

    Public Sub New(args As System.Int32)
        Me.args = args
    End Sub
End Class
</text>.NormalizedValue,
index:=1,
compareTokens:=False,
options:=[Option](CodeStyleOptions.PreferIntrinsicPredefinedTypeKeywordInDeclaration, False, NotificationOption.Error))
        End Function

        <WorkItem(869506, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/869506")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeOutsideCurrentProject() As Task
            Dim initial = <Workspace>
                              <Project Language="Visual Basic" AssemblyName="Assembly1" CommonReferences="true">
                                  <ProjectReference>Assembly2</ProjectReference>
                                  <Document FilePath="Test1.vb">
Class Program
    Sub Main()
        Dim f As [|A.B.C$$|].D
    End Sub
End Class

Namespace A
End Namespace</Document>
                              </Project>
                              <Project Language="Visual Basic" AssemblyName="Assembly2" CommonReferences="true">
                                  <Document FilePath="Test2.cs">
Namespace A
    Public Class B
    End Class
End Namespace</Document>
                              </Project>
                          </Workspace>.ToString()

            Dim expected = <Text>
Namespace A
    Public Class B
        Public Class C
        End Class
    End Class
End Namespace</Text>.NormalizedValue

            Await TestAsync(initial, expected, compareTokens:=False)
        End Function

        <WorkItem(940003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/940003")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestWithProperties1() As Task
            Await TestAsync(
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|](x:=1, y:=""Hello"") With {.Name = ""John"", .Age = Date.Today}
    End Sub
End Module",
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|](x:=1, y:=""Hello"") With {.Name = ""John"", .Age = Date.Today}
    End Sub
End Module
Friend Class Customer
    Private x As Integer
    Private y As String
    Public Sub New(x As Integer, y As String)
        Me.x = x
        Me.y = y
    End Sub
    Public Property Name As String
    Public Property Age As Date
End Class",
index:=1)
        End Function

        <WorkItem(940003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/940003")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestWithProperties2() As Task
            Await TestAsync(
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|](x:=1, y:=""Hello"") With {.Name = Nothing, .Age = Date.Today}
    End Sub
End Module",
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|](x:=1, y:=""Hello"") With {.Name = Nothing, .Age = Date.Today}
    End Sub
End Module
Friend Class Customer
    Private x As Integer
    Private y As String
    Public Sub New(x As Integer, y As String)
        Me.x = x
        Me.y = y
    End Sub
    Public Property Name As Object
    Public Property Age As Date
End Class",
index:=1)
        End Function

        <WorkItem(940003, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/940003")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestWithProperties3() As Task
            Await TestAsync(
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|](x:=1, y:=""Hello"") With {.Name = Foo, .Age = Date.Today}
    End Sub
End Module",
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|](x:=1, y:=""Hello"") With {.Name = Foo, .Age = Date.Today}
    End Sub
End Module
Friend Class Customer
    Private x As Integer
    Private y As String
    Public Sub New(x As Integer, y As String)
        Me.x = x
        Me.y = y
    End Sub
    Public Property Name As Object
    Public Property Age As Date
End Class",
index:=1)
        End Function

        <WorkItem(1082031, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1082031")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestWithProperties4() As Task
            Await TestAsync(
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|] With {.Name = ""John"", .Age = Date.Today}
    End Sub
End Module",
"Imports System
Module Program
    Sub Main()
        Dim c As New [|Customer|] With {.Name = ""John"", .Age = Date.Today}
    End Sub
End Module
Friend Class Customer
    Public Property Name As String
    Public Property Age As Date
End Class",
index:=1)
        End Function

        <WorkItem(1032176, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1032176")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestWithNameOf() As Task
            Await TestAsync(
"Imports System
Module Program
    Sub Main()
        Dim x = nameof([|Z|])
    End Sub
End Module",
"Imports System
Module Program
    Sub Main()
        Dim x = nameof([|Z|])
    End Sub
End Module
Friend Class Z
End Class",
index:=1)
        End Function

        <WorkItem(1032176, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1032176")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestWithNameOf2() As Task
            Await TestAsync(
"Imports System
Class Program
    Sub Main()
        Dim x = nameof([|Z|])
    End Sub
End Class",
"Imports System
Class Program
    Sub Main()
        Dim x = nameof([|Z|])
    End Sub
    Private Class Z
    End Class
End Class",
index:=2)
        End Function

        <WorkItem(1032176, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1032176")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestWithNameOf3() As Task
            Await TestAsync(
"Imports System
Class Program
    Sub Main()
        Dim x = nameof([|Program.Z|])
    End Sub
End Class",
"Imports System
Class Program
    Sub Main()
        Dim x = nameof([|Program.Z|])
    End Sub
    Private Class Z
    End Class
End Class",
index:=0)
        End Function

        <WorkItem(1065647, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1065647")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForNestedType() As Task
            Await TestAsync(
"Public Interface I
    Sub Foo(a As [|X.Y.Z|])
End Interface
Public Class X
End Class",
"Public Interface I
    Sub Foo(a As X.Y.Z)
End Interface
Public Class X
    Public Class Y
    End Class
End Class",
index:=0)
        End Function

        <WorkItem(1130905, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1130905")>
        <Fact(), Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeInImports() As Task
            Await TestAsync(
"Imports [|Fizz|]",
"Friend Class Fizz
End Class
")
        End Function

        <WorkItem(1130905, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1130905")>
        <Fact(), Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestGenerateTypeInImports2() As Task
            Await TestAsync(
"Imports [|Fizz|]",
"Imports Fizz
Friend Class Fizz
End Class",
index:=1)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B|]()
End Class",
"Public Class B
    Public Sub New()
    End Sub
End Class",
index:=0)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields2() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B|]()
End Class",
"Public Class A
    Public B As New B()
End Class

Public Class B
    Public Sub New()
    End Sub
End Class",
index:=1)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields3() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B|]()
End Class",
"Public Class A
    Public B As New B()
    Public Class B
        Public Sub New()
        End Sub
    End Class
End Class",
index:=2)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields4() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B|]
End Class",
"Public Class B
End Class",
index:=0)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields5() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B|]
End Class",
"Public Class A
    Public B As New B
End Class

Public Class B
End Class",
index:=1)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields6() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B|]
End Class",
"Public Class A
    Public B As New B
    Public Class B
    End Class
End Class",
index:=2)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields7() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B(Of Integer)|]
End Class",
"Public Class B(Of T)
End Class",
index:=0)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields8() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B(Of Integer)|]
End Class",
"Public Class A
    Public B As New B(Of Integer)
End Class

Public Class B(Of T)
End Class",
index:=1)
        End Function

        <WorkItem(1107929, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1107929")>
        <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateType)>
        Public Async Function TestAccessibilityForPublicFields9() As Task
            Await TestAsync(
"Public Class A
    Public B As New [|B(Of Integer)|]
End Class",
"Public Class A
    Public B As New B(Of Integer)
    Public Class B(Of T)
    End Class
End Class",
index:=2)
        End Function

        Public Class AddImportTestsWithAddImportDiagnosticProvider
            Inherits AbstractVisualBasicDiagnosticProviderBasedUserDiagnosticTest

            Friend Overrides Function CreateDiagnosticProviderAndFixer(workspace As Workspace) As Tuple(Of DiagnosticAnalyzer, CodeFixProvider)
                Return Tuple.Create(Of DiagnosticAnalyzer, CodeFixProvider)(
                    New VisualBasicUnboundIdentifiersDiagnosticAnalyzer(),
                    New GenerateTypeCodeFixProvider())
            End Function

            Protected Overrides Function MassageActions(actions As IList(Of CodeAction)) As IList(Of CodeAction)
                Return FlattenActions(actions)
            End Function

            <WorkItem(829970, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/829970")>
            <Fact, Trait(Traits.Feature, Traits.Features.CodeActionsAddImport)>
            Public Async Function TestUnknownIdentifierInAttributeSyntaxWithoutTarget() As Task
                Await TestAsync(
"Module Program
    <[|Extension|]>
End Module",
"Imports System
Module Program
    <Extension>
End Module
Friend Class ExtensionAttribute
    Inherits Attribute
End Class",
index:=1)
            End Function
        End Class
    End Class
End Namespace
