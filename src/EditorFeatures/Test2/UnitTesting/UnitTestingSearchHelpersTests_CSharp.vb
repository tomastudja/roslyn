﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
Imports Microsoft.CodeAnalysis.ExternalAccess.UnitTesting.Api
Imports Microsoft.CodeAnalysis.Remote.Testing

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.UnitTesting
    <UseExportProvider>
    Public Class UnitTestingSearchHelpersTests_CSharp
        Private Shared ReadOnly s_inProcessComposition As TestComposition = EditorTestCompositions.EditorFeatures
        Private Shared ReadOnly s_outOffProcessComposition As TestComposition = s_inProcessComposition.WithTestHostParts(TestHost.OutOfProcess)

        Private Shared Async Function TestCSharp(text As String, query As UnitTestingSearchQuery, host As TestHost) As Task
            Using workspace = TestWorkspace.CreateCSharp(text,
                composition:=If(host = TestHost.OutOfProcess, s_outOffProcessComposition, s_inProcessComposition))

                Dim project = workspace.CurrentSolution.Projects.Single()

                Dim actualLocations = Await UnitTestingSearchHelpers.GetSourceLocationsAsync(
                    project, query, cancellationToken:=Nothing)

                Dim expectedLocations = workspace.Documents.Single().SelectedSpans

                Assert.Equal(expectedLocations.Count, actualLocations.Length)

                For i = 0 To expectedLocations.Count - 1
                    Dim expected = expectedLocations(i)
                    Dim actual = actualLocations(i)

                    Assert.Equal(expected, actual.DocumentSpan.SourceSpan)
                Next
            End Using
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestType1(host As TestHost) As Task
            Await TestCSharp("
class [|Outer|]
{
}", UnitTestingSearchQuery.ForType("Outer"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestGenericType1(host As TestHost) As Task
            Await TestCSharp("
class [|Outer|]<T>
{
}", UnitTestingSearchQuery.ForType("Outer`1"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestGenericType2(host As TestHost) As Task
            Await TestCSharp("
class Outer<T>
{
}", UnitTestingSearchQuery.ForType("Outer"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestGenericType3(host As TestHost) As Task
            Await TestCSharp("
class Outer<T>
{
}", UnitTestingSearchQuery.ForType("Outer`2"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestNestedType1(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    class [|Inner|]
    {
    }
}", UnitTestingSearchQuery.ForType("Outer.Inner"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestNestedType2(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    class [|Inner|]
    {
    }
}", UnitTestingSearchQuery.ForType("Outer+Inner"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestNestedType3(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    class [|Inner|]<T>
    {
    }
}", UnitTestingSearchQuery.ForType("Outer+Inner`1"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestNestedType4(host As TestHost) As Task
            Await TestCSharp("
class Outer<T>
{
    class [|Inner|]
    {
    }
}", UnitTestingSearchQuery.ForType("Outer`1+Inner"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestNestedType5(host As TestHost) As Task
            Await TestCSharp("
class Outer<T>
{
    class [|Inner|]<U>
    {
    }
}", UnitTestingSearchQuery.ForType("Outer`1+Inner`1"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestTypeWithNamespace1(host As TestHost) As Task
            Await TestCSharp("
namespace N
{
    class [|Outer|]
    {
    }
}", UnitTestingSearchQuery.ForType("N.Outer"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestTypeWithNamespace2(host As TestHost) As Task
            Await TestCSharp("
namespace N
{
    class Outer
    {
    }
}", UnitTestingSearchQuery.ForType("Outer"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestTypeWithNamespace3(host As TestHost) As Task
            Await TestCSharp("
namespace N1.N2
{
    class [|Outer|]
    {
    }
}", UnitTestingSearchQuery.ForType("N1.N2.Outer"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestTypeWithNamespace4(host As TestHost) As Task
            Await TestCSharp("
namespace N1
{
    namespace N2
    {
        class [|Outer|]
        {
        }
    }
}", UnitTestingSearchQuery.ForType("N1.N2.Outer"), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod1(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void [|Goo|]() { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod2(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void Goo() { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=1, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod3(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void Goo() { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=0, methodParameterCount:=1), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod4(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void Goo<T>() { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod5(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void Goo(int i) { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod6(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void [|Goo|]<T>() { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=1, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod7(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void [|Goo|](int a) { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=0, methodParameterCount:=1), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod8(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void [|Goo|]<T>(int a) { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=1, methodParameterCount:=1), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod9(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    class Inner
    {
        void [|Goo|]() { }
    }
}", UnitTestingSearchQuery.ForMethod("Outer+Inner", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod10(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    class Inner<T>
    {
        void [|Goo|]() { }
    }
}", UnitTestingSearchQuery.ForMethod("Outer+Inner`1", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod11(host As TestHost) As Task
            Await TestCSharp("
class Outer<T>
{
    class Inner
    {
        void [|Goo|]() { }
    }
}", UnitTestingSearchQuery.ForMethod("Outer`1+Inner", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod12(host As TestHost) As Task
            Await TestCSharp("
class Outer<T>
{
    class Inner<U>
    {
        void [|Goo|]() { }
    }
}", UnitTestingSearchQuery.ForMethod("Outer`1+Inner`1", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestMethod13(host As TestHost) As Task
            Await TestCSharp("
namespace N1.N2
{
    class Outer
    {
        class Inner
        {
            void [|Goo|]() { }
        }
    }
}", UnitTestingSearchQuery.ForMethod("N1.N2.Outer+Inner", "Goo", methodArity:=0, methodParameterCount:=0), host)
        End Function

        <Theory, CombinatorialData>
        Public Async Function TestExtensionMethod1(host As TestHost) As Task
            Await TestCSharp("
class Outer
{
    void [|Goo|](this Outer o) { }
}", UnitTestingSearchQuery.ForMethod("Outer", "Goo", methodArity:=0, methodParameterCount:=1), host)
        End Function
    End Class
End Namespace
