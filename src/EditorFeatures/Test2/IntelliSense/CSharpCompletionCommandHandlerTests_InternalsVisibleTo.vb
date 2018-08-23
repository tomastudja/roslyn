﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Threading.Tasks

Namespace Microsoft.CodeAnalysis.Editor.UnitTests.IntelliSense

    <[UseExportProvider]>
    Public Class CSharpCompletionCommandHandlerTests_InternalsVisibleTo

        Public Shared ReadOnly Property AllCompletionImplementations() As IEnumerable(Of Object())
            Get
                Return TestStateFactory.GetAllCompletionImplementations()
            End Get
        End Property

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsOtherAssembliesOfSolution(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary2"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1", "ClassLibrary2", "ClassLibrary3"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsOtherAssemblyIfAttributeSuffixIsPresent(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionIsTriggeredWhenDoubleQuoteIsEntered(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo($$
                        </Document>
                    </Project>
                </Workspace>)
                Await state.AssertNoCompletionSession()
                state.SendTypeChars(""""c)
                Await state.AssertCompletionSession()
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionIsEmptyUntilDoubleQuotesAreEntered(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo$$
                        </Document>
                    </Project>
                </Workspace>)
                Await state.AssertNoCompletionSession()
                state.SendInvokeCompletionList()
                Await state.WaitForAsynchronousOperationsAsync()
                Assert.False(state.CompletionItemsContainsAny({"ClassLibrary1"}))
                state.SendTypeChars("("c)
                Await state.AssertNoCompletionSession()
                state.SendInvokeCompletionList()
                Await state.WaitForAsynchronousOperationsAsync()
                Assert.False(state.CompletionItemsContainsAny({"ClassLibrary1"}))
                state.SendTypeChars(""""c)
                Await state.AssertCompletionSession()
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionIsTriggeredWhenCharacterIsEnteredAfterOpeningDoubleQuote(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$")]
                        </Document>
                    </Project>
                </Workspace>)
                Await state.AssertNoCompletionSession()
                state.SendTypeChars("a"c)
                Await state.AssertCompletionSession()
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionIsNotTriggeredWhenCharacterIsEnteredThatIsNotRightBesideTheOpeniningDoubleQuote(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("a$$")]
                        </Document>
                    </Project>
                </Workspace>)
                Await state.AssertNoCompletionSession()
                state.SendTypeChars("b"c)
                Await state.AssertNoCompletionSession()
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionIsNotTriggeredWhenDoubleQuoteIsEnteredAtStartOfFile(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">$$
                        </Document>
                    </Project>
                </Workspace>)
                Await state.AssertNoCompletionSession()
                state.SendTypeChars("a"c)
                Await state.WaitForAsynchronousOperationsAsync()
                Assert.False(state.CompletionItemsContainsAny({"ClassLibrary1"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionIsNotTriggeredByArrayElementAccess(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs"><![CDATA[
namespace A
{
    public class C
    {
        public void M()
        {
            var d = new System.Collections.Generic.Dictionary<string, string>();
            var v = d$$;
        }
    }
}
]]>
                        </Document>
                    </Project>
                </Workspace>)
                Dim AssertNoCompletionAndCompletionDoesNotContainClassLibrary1 As Func(Of Task) =
                    Async Function()
                        Await state.AssertNoCompletionSession()
                        state.SendInvokeCompletionList()
                        Await state.WaitForAsynchronousOperationsAsync()
                        Assert.True(
                            state.CurrentCompletionPresenterSession Is Nothing OrElse
                            Not state.CompletionItemsContainsAny({"ClassLibrary1"}))
                    End Function
                Await AssertNoCompletionAndCompletionDoesNotContainClassLibrary1()
                state.SendTypeChars("["c)
                Await AssertNoCompletionAndCompletionDoesNotContainClassLibrary1()
                state.SendTypeChars(""""c)
                Await AssertNoCompletionAndCompletionDoesNotContainClassLibrary1()
            End Using
        End Function

        Private Async Function AssertCompletionListHasItems(completionImplementation As CompletionImplementation, code As String, hasItems As Boolean) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
using System.Runtime.CompilerServices;
using System.Reflection;
<%= code %>
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.WaitForAsynchronousOperationsAsync()
                If hasItems Then
                    Await state.AssertCompletionSession()
                    Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1"}))
                Else
                    Await state.AssertNoCompletionSession
                End If
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_AfterSingleDoubleQuoteAndClosing(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(""$$)]", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_AfterText(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(""Test$$)]", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfCursorIsInSecondParameter(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(""Test"", ""$$", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasNoItems_IfCursorIsClosingDoubleQuote1(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(""Test""$$", False)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasNoItems_IfCursorIsClosingDoubleQuote2(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(""""$$", False)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfNamedParameterIsPresent(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(""$$, AllInternalsVisible = true)]", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfNamedParameterAndNamedPositionalParametersArePresent(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(assemblyName: ""$$, AllInternalsVisible = true)]", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasNoItems_IfNumberIsEntered(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(1$$2)]", False)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasNoItems_IfNotInternalsVisibleToAttribute(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: AssemblyVersion(""$$"")]", False)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfOtherAttributeIsPresent1(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: AssemblyVersion(""1.0.0.0""), InternalsVisibleTo(""$$", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfOtherAttributeIsPresent2(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "[assembly: InternalsVisibleTo(""$$""), AssemblyVersion(""1.0.0.0"")]", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfOtherAttributesAreAhead(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "
                [assembly: AssemblyVersion(""1.0.0.0"")]
                [assembly: InternalsVisibleTo(""$$", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfOtherAttributesAreFollowing(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "
            [assembly: InternalsVisibleTo(""$$
            [assembly: AssemblyVersion(""1.0.0.0"")]
            [assembly: AssemblyCompany(""Test"")]", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function AssertCompletionListHasItems_IfNamespaceIsFollowing(completionImplementation As CompletionImplementation) As Task
            Await AssertCompletionListHasItems(completionImplementation, "
            [assembly: InternalsVisibleTo(""$$
            namespace A {            
                public class A { }
            }", True)
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionHasItemsIfInteralVisibleToIsReferencedByTypeAlias(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
[assembly: IVT("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionDoesNotContainCurrentAssembly(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.False(state.CompletionItemsContainsAny({"TestAssembly"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionInsertsAssemblyNameOnCommit(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1">
                    </Project>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document>
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("ClassLibrary1")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""ClassLibrary1"")]")
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionInsertsPublicKeyOnCommit(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1">
                        <CompilationOptions
                            CryptoKeyFile=<%= SigningTestHelpers.PublicKeyFile %>
                            StrongNameProvider=<%= SigningTestHelpers.s_defaultDesktopProvider.GetType().AssemblyQualifiedName %>/>
                    </Project>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document>
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("ClassLibrary1")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""ClassLibrary1, PublicKey=00240000048000009400000006020000002400005253413100040000010001002b986f6b5ea5717d35c72d38561f413e267029efa9b5f107b9331d83df657381325b3a67b75812f63a9436ceccb49494de8f574f8e639d4d26c0fcf8b0e9a1a196b80b6f6ed053628d10d027e032df2ed1d60835e5f47d32c9ef6da10d0366a319573362c821b5f8fa5abc5bb22241de6f666a85d82d6ba8c3090d01636bd2bb"")]")
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsPublicKeyIfKeyIsSpecifiedByAttribute(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1">
                        <CompilationOptions
                            StrongNameProvider=<%= SigningTestHelpers.s_defaultDesktopProvider.GetType().AssemblyQualifiedName %>/>
                        <Document>
                            [assembly: System.Reflection.AssemblyKeyFile("<%= SigningTestHelpers.PublicKeyFile.Replace("\", "\\") %>")]
                        </Document>
                    </Project>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document>
    [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("ClassLibrary1")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""ClassLibrary1, PublicKey=00240000048000009400000006020000002400005253413100040000010001002b986f6b5ea5717d35c72d38561f413e267029efa9b5f107b9331d83df657381325b3a67b75812f63a9436ceccb49494de8f574f8e639d4d26c0fcf8b0e9a1a196b80b6f6ed053628d10d027e032df2ed1d60835e5f47d32c9ef6da10d0366a319573362c821b5f8fa5abc5bb22241de6f666a85d82d6ba8c3090d01636bd2bb"")]")
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsPublicKeyIfDelayedSigningIsEnabled(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1">
                        <CompilationOptions
                            CryptoKeyFile=<%= SigningTestHelpers.PublicKeyFile %>
                            StrongNameProvider=<%= SigningTestHelpers.s_defaultDesktopProvider.GetType().AssemblyQualifiedName %>
                            DelaySign="True"/>
                    </Project>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document>
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("ClassLibrary1")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""ClassLibrary1, PublicKey=00240000048000009400000006020000002400005253413100040000010001002b986f6b5ea5717d35c72d38561f413e267029efa9b5f107b9331d83df657381325b3a67b75812f63a9436ceccb49494de8f574f8e639d4d26c0fcf8b0e9a1a196b80b6f6ed053628d10d027e032df2ed1d60835e5f47d32c9ef6da10d0366a319573362c821b5f8fa5abc5bb22241de6f666a85d82d6ba8c3090d01636bd2bb"")]")
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionListIsEmptyIfAttributeIsNotTheBCLAttribute(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: Test.InternalsVisibleTo("$$")]
namespace Test
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class InternalsVisibleToAttribute: System.Attribute
    {
        public InternalsVisibleToAttribute(string ignore)
        {

        }
    }
}
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertNoCompletionSession()
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsOnlyAssembliesThatAreNotAlreadyIVT(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary2"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ClassLibrary1")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ClassLibrary2")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.False(state.CompletionItemsContainsAny({"ClassLibrary1", "ClassLibrary2"}))
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary3"}))
            End Using
        End Function


        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsOnlyAssembliesThatAreNotAlreadyIVTIfAssemblyNameIsAConstant(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary2"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <MetadataReferenceFromSource Language="C#" CommonReferences="true">
                            <Document FilePath="ReferencedDocument.cs">
namespace A {
    public static class Constants
    {
        public const string AssemblyName1 = "ClassLibrary1";
    }
}
                            </Document>
                        </MetadataReferenceFromSource>
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(A.Constants.AssemblyName1)]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ClassLibrary2")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.False(state.CompletionItemsContainsAny({"ClassLibrary1", "ClassLibrary2"}))
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary3"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsOnlyAssembliesThatAreNotAlreadyIVTForDifferentSyntax(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary2"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary4"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary5"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary6"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary7"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary8"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary9"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
// Code comment
using System.Runtime.CompilerServices;
using System.Reflection;
using IVT = System.Runtime.CompilerServices.InternalsVisibleToAttribute;
// Code comment
[assembly: InternalsVisibleTo("ClassLibrary1", AllInternalsVisible = true)]
[assembly: InternalsVisibleTo(assemblyName: "ClassLibrary2", AllInternalsVisible = true)]
[assembly: AssemblyVersion("1.0.0.0"), InternalsVisibleTo("ClassLibrary3")]
[assembly: InternalsVisibleTo("ClassLibrary4"), AssemblyCopyright("Copyright")]
[assembly: AssemblyDescription("Description")]
[assembly: InternalsVisibleTo("ClassLibrary5")]
[assembly: InternalsVisibleTo("ClassLibrary6, PublicKey=00240000048000009400000006020000002400005253413100040000010001002b986f6b5ea5717d35c72d38561f413e267029efa9b5f107b9331d83df657381325b3a67b75812f63a9436ceccb49494de8f574f8e639d4d26c0fcf8b0e9a1a196b80b6f6ed053628d10d027e032df2ed1d60835e5f47d32c9ef6da10d0366a319573362c821b5f8fa5abc5bb22241de6f666a85d82d6ba8c3090d01636bd2bb")]
[assembly: InternalsVisibleTo("ClassLibrary" + "7")]
[assembly: IVT("ClassLibrary8")]
[assembly: InternalsVisibleTo("$$
namespace A {
    public class A { }
}
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.False(state.CompletionItemsContainsAny({"ClassLibrary1", "ClassLibrary2", "ClassLibrary3", "ClassLibrary4", "ClassLibrary5", "ClassLibrary6", "ClassLibrary7", "ClassLibrary8"}))
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary9"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsOnlyAssembliesThatAreNotAlreadyIVTWithSyntaxError(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
using System.Runtime.CompilerServices;
using System.Reflection;

[assembly: InternalsVisibleTo("ClassLibrary" + 1)] // Not a constant
[assembly: InternalsVisibleTo("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                ' ClassLibrary1 must be listed because the existing attribute argument can't be resolved to a constant.
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary1"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionContainsOnlyAssembliesThatAreNotAlreadyIVTWithMoreThanOneDocument(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="ClassLibrary2"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="OtherDocument.cs">
using System.Runtime.CompilerServices;
using System.Reflection;
[assembly: InternalsVisibleTo("ClassLibrary1")]
[assembly: AssemblyDescription("Description")]
                        </Document>
                        <Document FilePath="C.cs">
using System.Runtime.CompilerServices;
using System.Reflection;
[assembly: InternalsVisibleTo("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertCompletionSession()
                Assert.False(state.CompletionItemsContainsAny({"ClassLibrary1"}))
                Assert.True(state.CompletionItemsContainsAll({"ClassLibrary2"}))
            End Using
        End Function

        <MemberData(NameOf(AllCompletionImplementations))>
        <WpfTheory, Trait(Traits.Feature, Traits.Features.Completion)>
        Public Async Function CodeCompletionIgnoresUnsupportedProjectTypes(completionImplementation As CompletionImplementation) As Task
            Using state = TestStateFactory.CreateTestStateFromWorkspace(completionImplementation,
                <Workspace>
                    <Project Language="NoCompilation" AssemblyName="ClassLibrary1"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
using System.Runtime.CompilerServices;
using System.Reflection;
[assembly: InternalsVisibleTo("$$
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertNoCompletionSession()
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_1() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted.Assembly.Name"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Dotted.Assem$$bly")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted.Assembly.Name")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""Dotted.Assembly.Name"")]")
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_2() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted.Assembly.Name"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Dotted.Assem$$bly
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted.Assembly.Name")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""Dotted.Assembly.Name")
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_3() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted.Assembly.Name"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("  Dotted.Assem$$bly  ")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted.Assembly.Name")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""Dotted.Assembly.Name"")]")
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_4() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted1.Dotted2.Assembly.Dotted3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Dotted1.Dotted2.Assem$$bly.Dotted")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted1.Dotted2.Assembly.Dotted3")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""Dotted1.Dotted2.Assembly.Dotted3"")]")
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_Verbatim_1() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted1.Dotted2.Assembly.Dotted3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(@"Dotted1.Dotted2.Assem$$bly.Dotted")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted1.Dotted2.Assembly.Dotted3")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(@""Dotted1.Dotted2.Assembly.Dotted3"")]")
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_Verbatim_2() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted1.Dotted2.Assembly.Dotted3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(@"Dotted1.Dotted2.Assem$$bly
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted1.Dotted2.Assembly.Dotted3")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(@""Dotted1.Dotted2.Assembly.Dotted3")
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_EscapeSequence_1() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted1.Dotted2.Assembly.Dotted3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("\"Dotted1.Dotted2.Assem$$bly\"")]
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted1.Dotted2.Assembly.Dotted3")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""Dotted1.Dotted2.Assembly.Dotted3"")]")
            End Using
        End Function

        <WpfFact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem(29447, "https://github.com/dotnet/roslyn/pull/29447")>
        Public Async Function CodeCompletionReplacesExisitingAssemblyNameWithDots_OpenEnded() As Task
            Using state = TestState.CreateTestStateFromWorkspace(
                <Workspace>
                    <Project Language="C#" AssemblyName="Dotted1.Dotted2.Assembly.Dotted3"/>
                    <Project Language="C#" CommonReferences="true" AssemblyName="TestAssembly">
                        <Document FilePath="C.cs">
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Dotted1.Dotted2.Assem$$bly.Dotted4
                        </Document>
                    </Project>
                </Workspace>)
                state.SendInvokeCompletionList()
                Await state.AssertSelectedCompletionItem("Dotted1.Dotted2.Assembly.Dotted3")
                state.SendTab()
                Await state.WaitForAsynchronousOperationsAsync()
                state.AssertMatchesTextStartingAtLine(1, "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(""Dotted1.Dotted2.Assembly.Dotted3")
            End Using
        End Function
    End Class
End Namespace
