﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.IO
Imports System.Text
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.VisualStudio.LanguageServices.Implementation
Imports Microsoft.Win32
Imports Roslyn.Test.Utilities

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests
    Public Class AnalyzerDependencyCheckerTests
        Inherits TestBase

        Private Shared s_msbuildDirectory As String
        Private Shared ReadOnly Property MSBuildDirectory As String
            Get
                If s_msbuildDirectory Is Nothing Then
                    Dim key = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\MSBuild\ToolsVersions\14.0", False)

                    If key IsNot Nothing Then
                        Dim toolsPath = key.GetValue("MSBuildToolsPath")
                        If toolsPath IsNot Nothing Then
                            s_msbuildDirectory = toolsPath.ToString()
                        End If
                    End If
                End If

                Return s_msbuildDirectory
            End Get
        End Property

        Private Shared s_CSharpCompilerExecutable As String = Path.Combine(MSBuildDirectory, "csc.exe")
        Private Shared s_mscorlibDisplayName As String = "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"

        Private Shared Function GetIgnorableAssemblyLists() As IEnumerable(Of IIgnorableAssemblyList)
            Dim mscorlib As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName(s_mscorlibDisplayName, mscorlib)

            Return {New IgnorableAssemblyIdentityList({mscorlib})}
        End Function

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest1()
            ' Dependency Graph:
            '   A

            Using directory = New DisposableDirectory(Temp)
                Dim library = BuildLibrary(directory, "public class A { }", "A")

                Dim dependencyChecker = New AnalyzerDependencyChecker({library}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.Conflicts)
            End Using

        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest2()
            ' Dependency graph:
            '   A --> B

            Dim sourceA = "
public class A
{
    void M()
    {
        B b = new B();
    }
}"

            Dim sourceB = "public class B { }"

            Using directory = New DisposableDirectory(Temp)
                Dim libraryB = BuildLibrary(directory, sourceB, "B")
                Dim libraryA = BuildLibrary(directory, sourceA, "A", "B")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.Conflicts)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest3()
            ' Dependency graph:
            '   A --> B
            '     \
            '      -> C

            Dim sourceA = "
public class A
{
    void M()
    {
        B b = new B();
        C c = new C();
    }
}"

            Dim sourceB = "public class B { }"
            Dim sourceC = "public class C { }"

            Using directory = New DisposableDirectory(Temp)
                Dim libraryC = BuildLibrary(directory, sourceC, "C")
                Dim libraryB = BuildLibrary(directory, sourceB, "B")
                Dim libraryA = BuildLibrary(directory, sourceA, "A", "B", "C")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.Conflicts)
                Assert.Empty(results.MissingDependencies)
            End Using

        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest4()
            ' Dependency graph:
            '   A --> B
            '   C --> D

            Dim sourceA = "
public class A
{
    void M()
    {
        B b = new B();
    }
}"
            Dim sourceB = "public class B { }"

            Dim sourceC = "
public class C
{
    void M()
    {
        C c = new C();
    }
}"
            Dim sourceD = "public class D { }"

            Using directory = New DisposableDirectory(Temp)
                Dim libraryB = BuildLibrary(directory, sourceB, "B")
                Dim libraryA = BuildLibrary(directory, sourceA, "A", "B")
                Dim libraryD = BuildLibrary(directory, sourceD, "D")
                Dim libraryC = BuildLibrary(directory, sourceC, "C", "D")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC, libraryD}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.Conflicts)
                Assert.Empty(results.MissingDependencies)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest5()
            ' Dependency graph:
            '   Directory 1:
            '     A --> B
            '   Directory 2:
            '     C --> D

            Dim sourceA = "
public class A
{
    void M()
    {
        B b = new B();
    }
}"
            Dim sourceB = "public class B { }"

            Dim sourceC = "
public class C
{
    void M()
    {
        C c = new C();
    }
}"
            Dim sourceD = "public class D { }"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryB = BuildLibrary(directory1, sourceB, "B")
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "B")
                Dim libraryD = BuildLibrary(directory2, sourceD, "D")
                Dim libraryC = BuildLibrary(directory2, sourceC, "C", "D")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC, libraryD}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.Conflicts)
                Assert.Empty(results.MissingDependencies)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest6()
            ' Dependency graph:
            ' A -
            '    \
            '     -> C
            '    /
            ' B -

            Dim sourceA = "
public class A
{
    void M()
    {
        C c = new C();
    }
}"

            Dim sourceB = "
public class B
{
    void M()
    {
        C c = new C();
    }
}"
            Dim sourceC = "public class C { }"

            Using directory = New DisposableDirectory(Temp)
                Dim libraryC = BuildLibrary(directory, sourceC, "C")
                Dim libraryA = BuildLibrary(directory, sourceA, "A", "C")
                Dim libraryB = BuildLibrary(directory, sourceB, "B", "C")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.Conflicts)
                Assert.Empty(results.MissingDependencies)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest7()
            ' Dependency graph:
            '   Directory 1:
            '     A --> C
            '   Directory 2:
            '     B --> C

            Dim sourceA = "
public class A
{
    void M()
    {
        C c = new C();
    }
}"

            Dim sourceB = "
public class B
{
    void M()
    {
        C c = new C();
    }
}"
            Dim sourceC = "public class C { }"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryC1 = BuildLibrary(directory1, sourceC, "C")
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "C")
                Dim libraryC2 = directory2.CreateFile("C.dll").CopyContentFrom(libraryC1).Path
                Dim libraryB = BuildLibrary(directory2, sourceB, "B", "C")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC1, libraryC2}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.Conflicts)
                Assert.Empty(results.MissingDependencies)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest8()
            ' Dependency graph:
            '   Directory 1:
            '     A --> C
            '   Directory 2:
            '     B --> C'

            Dim sourceA = "
public class A
{
    void M()
    {
        C c = new C();
    }
}"

            Dim sourceB = "
public class B
{
    void M()
    {
        C c = new C();
    }
}"

            Dim sourceC = "
public class C
{
    public static string Field = ""Assembly C"";
}"

            Dim sourceCPrime = "
public class C
{
    public static string Field = ""Assembly C Prime"";
}"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryC = BuildLibrary(directory1, sourceC, "C")
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "C")
                Dim libraryCPrime = BuildLibrary(directory2, sourceCPrime, "C")
                Dim libraryB = BuildLibrary(directory2, sourceB, "B", "C")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC, libraryCPrime}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Dim conflicts = results.Conflicts

                Assert.Equal(expected:=1, actual:=conflicts.Length)

                Dim analyzer1FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath1)
                Dim analyzer2FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath2)

                Assert.Equal(expected:="C.dll", actual:=analyzer1FileName)
                Assert.Equal(expected:="C.dll", actual:=analyzer2FileName)
                Assert.Equal(expected:=New AssemblyIdentity("C"), actual:=conflicts(0).Identity)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest9()
            ' Dependency graph:
            '   Directory 1:
            '     A --> C --> D
            '   Directory 2:
            '     B --> C --> D'

            Dim sourceA = "
public class A
{
    void M()
    {
        C c = new C();
    }
}"

            Dim sourceB = "
public class B
{
    void M()
    {
        C c = new C();
    }
}"

            Dim sourceC = "
public class C
{
    void M()
    {
        D d = new D();
    }
}"

            Dim sourceD = "
public class D
{
    public static string Field = ""Assembly D"";
}"

            Dim sourceDPrime = "
public class D
{
    public static string Field = ""Assembly D Prime"";
}"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryD = BuildLibrary(directory1, sourceD, "D")
                Dim libraryDPrime = BuildLibrary(directory2, sourceDPrime, "D")
                Dim libraryC1 = BuildLibrary(directory1, sourceC, "C", "D")
                Dim libraryC2 = directory2.CreateFile("C.dll").CopyContentFrom(libraryC1).Path
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "C")
                Dim libraryB = BuildLibrary(directory2, sourceB, "B", "C")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC1, libraryC2, libraryD, libraryDPrime}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Dim conflicts = results.Conflicts
                Assert.Equal(expected:=1, actual:=conflicts.Length)

                Dim analyzer1FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath1)
                Dim analyzer2FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath2)

                Assert.Equal(expected:="D.dll", actual:=analyzer1FileName)
                Assert.Equal(expected:="D.dll", actual:=analyzer2FileName)
                Assert.Equal(expected:=New AssemblyIdentity("D"), actual:=conflicts(0).Identity)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest10()
            ' Dependency graph:
            '   Directory 1:
            '     A --> C --> E
            '   Directory 2:
            '     B --> D --> E'

            Dim sourceA = "
public class A
{
    void M()
    {
        C c = new C();
    }
}"

            Dim sourceB = "
public class B
{
    void M()
    {
        D d = new D();
    }
}"

            Dim sourceC = "
public class C
{
    void M()
    {
        E e = new E();
    }
}"

            Dim sourceD = "
public class D
{
    void M()
    {
        E e = new E();
    }
}"

            Dim sourceE = "
public class E
{
    public static string Field = ""Assembly E"";
}"

            Dim sourceEPrime = "
public class E
{
    public static string Field = ""Assembly D Prime"";
}"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryE = BuildLibrary(directory1, sourceE, "E")
                Dim libraryEPrime = BuildLibrary(directory2, sourceEPrime, "E")
                Dim libraryC = BuildLibrary(directory1, sourceC, "C", "E")
                Dim libraryD = BuildLibrary(directory2, sourceD, "D", "E")
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "C")
                Dim libraryB = BuildLibrary(directory2, sourceB, "B", "D")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC, libraryD, libraryE, libraryEPrime}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()
                Dim conflicts = results.Conflicts

                Assert.Equal(expected:=1, actual:=conflicts.Length)

                Dim analyzer1FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath1)
                Dim analyzer2FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath2)


                Assert.Equal(expected:="E.dll", actual:=analyzer1FileName)
                Assert.Equal(expected:="E.dll", actual:=analyzer2FileName)
                Assert.Equal(expected:=New AssemblyIdentity("E"), actual:=conflicts(0).Identity)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest11()
            ' Dependency graph:
            '   Directory 1:
            '     A --> B
            '   Directory 2:
            '     A --> B'

            Dim sourceA = "
public class A
{
    void M()
    {
        B b = new B();
    }
}"

            Dim sourceB = "
public class B
{
    public static string Field = ""Assembly B"";
}"

            Dim sourceBPrime = "
public class B
{
    public static string Field = ""Assembly B Prime"";
}"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryB = BuildLibrary(directory1, sourceB, "B")
                Dim libraryA1 = BuildLibrary(directory1, sourceA, "A", "B")

                Dim libraryBPrime = BuildLibrary(directory2, sourceBPrime, "B")
                Dim libraryA2 = directory2.CreateFile("A.dll").CopyContentFrom(libraryA1).Path

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA1, libraryA2, libraryB, libraryBPrime}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()
                Dim conflicts = results.Conflicts

                Assert.Equal(expected:=1, actual:=conflicts.Length)

                Dim analyzer1FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath1)
                Dim analyzer2FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath2)

                Assert.Equal(expected:="B.dll", actual:=analyzer1FileName)
                Assert.Equal(expected:="B.dll", actual:=analyzer2FileName)
                Assert.Equal(expected:=New AssemblyIdentity("B"), actual:=conflicts(0).Identity)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest12()
            ' Dependency graph:
            '   Directory 1:
            '     A --> B
            '   Directory 2:
            '     A' --> B'

            Dim sourceA = "
public class A
{
    public static string Field = ""Assembly A"";

    void M()
    {
        B b = new B();
    }
}"

            Dim sourceAPrime = "
public class A
{
    public static string Field = ""Assembly A Prime"";

    void M()
    {
        B b = new B();
    }
}"

            Dim sourceB = "
public class B
{
    public static string Field = ""Assembly B"";
}"

            Dim sourceBPrime = "
public class B
{
    public static string Field = ""Assembly B Prime"";
}"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryB = BuildLibrary(directory1, sourceB, "B")
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "B")

                Dim libraryBPrime = BuildLibrary(directory2, sourceBPrime, "B")
                Dim libraryAPrime = BuildLibrary(directory2, sourceAPrime, "A", "B")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryAPrime, libraryB, libraryBPrime}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()
                Dim conflicts = results.Conflicts

                Assert.Equal(expected:=2, actual:=conflicts.Length)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest13()
            ' Dependency graph:
            '   Directory 1:
            '     A  --> B
            '   Directory 2:
            '     A' --> B

            Dim sourceA = "
public class A
{
    public static string Field = ""Assembly A"";

    void M()
    {
        B b = new B();
    }
}"

            Dim sourceAPrime = "
public class A
{
    public static string Field = ""Assembly A Prime"";

    void M()
    {
        B b = new B();
    }
}"

            Dim sourceB = "
public class B
{
    public static string Field = ""Assembly B"";
}"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp)
                Dim libraryB1 = BuildLibrary(directory1, sourceB, "B")
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "B")

                Dim libraryB2 = directory2.CreateFile("B.dll").CopyContentFrom(libraryB1).Path
                Dim libraryAPrime = BuildLibrary(directory2, sourceAPrime, "A", "B")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryAPrime, libraryB1, libraryB2}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()
                Dim conflicts = results.Conflicts

                Assert.Equal(expected:=1, actual:=conflicts.Length)

                Dim analyzer1FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath1)
                Dim analyzer2FileName As String = Path.GetFileName(conflicts(0).AnalyzerFilePath2)

                Assert.Equal(expected:="A.dll", actual:=analyzer1FileName)
                Assert.Equal(expected:="A.dll", actual:=analyzer2FileName)
                Assert.Equal(expected:=New AssemblyIdentity("A"), actual:=conflicts(0).Identity)
            End Using
        End Sub

        <Fact, WorkItem(1064914)>
        Public Sub ConflictsTest14()
            ' Dependency graph:
            '   Directory 1:
            '     A --> D
            '   Directory 2:
            '     B --> D'
            '   Directory 3:
            '     C --> D''

            Dim sourceA = "
public class A
{
    void M()
    {
        D d = new D();
    }
}"

            Dim sourceB = "
public class B
{
    void M()
    {
        D d = new D();
    }
}"

            Dim sourceC = "
public class C
{
    void M()
    {
        D d = new D();
    }
}"

            Dim sourceD = "
public class D
{
    public static string Field = ""Assembly D"";
}"

            Dim sourceDPrime = "
public class D
{
    public static string Field = ""Assembly D Prime"";
}"

            Dim sourceDPrimePrime = "
public class D
{
    public static string Field = ""Assembly D Prime Prime"";
}"

            Using directory1 = New DisposableDirectory(Temp), directory2 = New DisposableDirectory(Temp), directory3 = New DisposableDirectory(Temp)
                Dim libraryD = BuildLibrary(directory1, sourceD, "D")
                Dim libraryA = BuildLibrary(directory1, sourceA, "A", "D")

                Dim libraryDPrime = BuildLibrary(directory2, sourceDPrime, "D")
                Dim libraryB = BuildLibrary(directory2, sourceB, "B", "D")

                Dim libraryDPrimePrime = BuildLibrary(directory3, sourceDPrimePrime, "D")
                Dim libraryC = BuildLibrary(directory3, sourceC, "C", "D")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA, libraryB, libraryC, libraryD, libraryDPrime, libraryDPrimePrime}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Equal(expected:=3, actual:=results.Conflicts.Length)
            End Using
        End Sub

        <Fact>
        Public Sub MissingTest1()
            ' Dependency Graph:
            '   A

            Using directory = New DisposableDirectory(Temp)
                Dim library = BuildLibrary(directory, "public class A { }", "A")

                Dim dependencyChecker = New AnalyzerDependencyChecker({library}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()

                Assert.Empty(results.MissingDependencies)
            End Using
        End Sub

        <Fact>
        Public Sub MissingTest2()
            ' Dependency graph:
            '   A --> B*

            Dim sourceA = "
public class A
{
    void M()
    {
        B b = new B();
    }
}"

            Dim sourceB = "public class B { }"

            Using directory = New DisposableDirectory(Temp)
                Dim libraryB = BuildLibrary(directory, sourceB, "B")
                Dim libraryA = BuildLibrary(directory, sourceA, "A", "B")

                Dim dependencyChecker = New AnalyzerDependencyChecker({libraryA}, GetIgnorableAssemblyLists())
                Dim results = dependencyChecker.Run()
                Dim missingDependencies = results.MissingDependencies

                Assert.Equal(expected:=1, actual:=missingDependencies.Count)

                Dim analyzerFileName As String = Path.GetFileName(missingDependencies(0).AnalyzerPath)
                Assert.Equal(expected:="A.dll", actual:=analyzerFileName)
                Assert.Equal(expected:=New AssemblyIdentity("B"), actual:=missingDependencies(0).DependencyIdentity)
            End Using
        End Sub

        <Fact, WorkItem(3020, "https://github.com/dotnet/roslyn/issues/3020")>
        Public Sub IgnorableAssemblyIdentityList_IncludesItem()
            Dim mscorlib1 As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName(s_mscorlibDisplayName, mscorlib1)

            Dim ignorableAssemblyList = New IgnorableAssemblyIdentityList({mscorlib1})

            Dim mscorlib2 As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName(s_mscorlibDisplayName, mscorlib2)

            Assert.True(ignorableAssemblyList.Includes(mscorlib2))
        End Sub

        <Fact, WorkItem(3020, "https://github.com/dotnet/roslyn/issues/3020")>
        Public Sub IgnorableAssemblyIdentityList_DoesNotIncludeItem()
            Dim mscorlib As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName(s_mscorlibDisplayName, mscorlib)

            Dim ignorableAssemblyList = New IgnorableAssemblyIdentityList({mscorlib})

            Dim alpha As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName("Alpha, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", alpha)

            Assert.False(ignorableAssemblyList.Includes(alpha))
        End Sub

        <Fact, WorkItem(3020, "https://github.com/dotnet/roslyn/issues/3020")>
        Public Sub IgnorableAssemblyNamePrefixList_IncludesItem_Prefix()
            Dim ignorableAssemblyList = New IgnorableAssemblyNamePrefixList("Alpha")

            Dim alphaBeta As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName("Alpha.Beta, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", alphaBeta)

            Assert.True(ignorableAssemblyList.Includes(alphaBeta))
        End Sub

        <Fact, WorkItem(3020, "https://github.com/dotnet/roslyn/issues/3020")>
        Public Sub IgnorableAssemblyNamePrefixList_IncludesItem_WholeName()
            Dim ignorableAssemblyList = New IgnorableAssemblyNamePrefixList("Alpha")

            Dim alpha As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName("Alpha, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", alpha)

            Assert.True(ignorableAssemblyList.Includes(alpha))
        End Sub

        <Fact, WorkItem(3020, "https://github.com/dotnet/roslyn/issues/3020")>
        Public Sub IgnorableAssemblyNamePrefixList_DoesNotIncludeItem()
            Dim ignorableAssemblyList = New IgnorableAssemblyNamePrefixList("Beta")

            Dim alpha As AssemblyIdentity = Nothing
            AssemblyIdentity.TryParseDisplayName("Alpha, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", alpha)

            Assert.False(ignorableAssemblyList.Includes(alpha))
        End Sub

        Private Function BuildLibrary(directory As DisposableDirectory, fileContents As String, libraryName As String, ParamArray referenceNames As String()) As String
            Dim sourceFile = directory.CreateFile(libraryName + ".cs").WriteAllText(fileContents).Path
            Dim tempOut = Path.Combine(directory.Path, libraryName + ".out")
            Dim libraryOut = Path.Combine(directory.Path, libraryName + ".dll")

            Dim sb = New StringBuilder
            For Each name In referenceNames
                sb.Append(" /r:")
                sb.Append(Path.Combine(directory.Path, name + ".dll"))
            Next

            Dim references = sb.ToString()

            Dim arguments = $"/C ""{s_CSharpCompilerExecutable}"" /nologo /t:library /out:{libraryOut} {references} {sourceFile} > {tempOut}"

            Dim output = ProcessUtilities.RunAndGetOutput("cmd", arguments, expectedRetCode:=0)

            Return libraryOut
        End Function
    End Class
End Namespace

