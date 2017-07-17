﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Reflection.Metadata
Imports System.Reflection.PortableExecutable
Imports Microsoft.Cci
Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.Emit
Imports Microsoft.CodeAnalysis.ExpressionEvaluator
Imports Microsoft.CodeAnalysis.ExpressionEvaluator.UnitTests
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.VisualBasic.Emit
Imports Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols.Metadata.PE
Imports Microsoft.CodeAnalysis.VisualBasic.UnitTests
Imports Microsoft.DiaSymReader
Imports Roslyn.Test.PdbUtilities
Imports Roslyn.Test.Utilities
Imports Xunit

Namespace Microsoft.CodeAnalysis.VisualBasic.ExpressionEvaluator.UnitTests

    Public Class ReferencedModulesTests
        Inherits ExpressionCompilerTestBase

        ''' <summary>
        ''' MakeAssemblyReferences should drop unreferenced assemblies.
        ''' </summary>
        <WorkItem(1141029, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1141029")>
        <Fact>
        Public Sub AssemblyDuplicateReferences()
            Const sourceA =
"Public Class A
End Class"
            Const sourceB =
"Public Class B 
    Public F As New A()
End Class"
            Const sourceC =
"Class C
    Public F As New B()
    Shared Sub M()
    End Sub
End Class"
            ' Assembly A, multiple versions, strong name.
            Dim assemblyNameA = ExpressionCompilerUtilities.GenerateUniqueName()
            Dim publicKeyA = ImmutableArray.CreateRange(Of Byte)({&H0, &H24, &H0, &H0, &H4, &H80, &H0, &H0, &H94, &H0, &H0, &H0, &H6, &H2, &H0, &H0, &H0, &H24, &H0, &H0, &H52, &H53, &H41, &H31, &H0, &H4, &H0, &H0, &H1, &H0, &H1, &H0, &HED, &HD3, &H22, &HCB, &H6B, &HF8, &HD4, &HA2, &HFC, &HCC, &H87, &H37, &H4, &H6, &H4, &HCE, &HE7, &HB2, &HA6, &HF8, &H4A, &HEE, &HF3, &H19, &HDF, &H5B, &H95, &HE3, &H7A, &H6A, &H28, &H24, &HA4, &HA, &H83, &H83, &HBD, &HBA, &HF2, &HF2, &H52, &H20, &HE9, &HAA, &H3B, &HD1, &HDD, &HE4, &H9A, &H9A, &H9C, &HC0, &H30, &H8F, &H1, &H40, &H6, &HE0, &H2B, &H95, &H62, &H89, &H2A, &H34, &H75, &H22, &H68, &H64, &H6E, &H7C, &H2E, &H83, &H50, &H5A, &HCE, &H7B, &HB, &HE8, &HF8, &H71, &HE6, &HF7, &H73, &H8E, &HEB, &H84, &HD2, &H73, &H5D, &H9D, &HBE, &H5E, &HF5, &H90, &HF9, &HAB, &HA, &H10, &H7E, &H23, &H48, &HF4, &HAD, &H70, &H2E, &HF7, &HD4, &H51, &HD5, &H8B, &H3A, &HF7, &HCA, &H90, &H4C, &HDC, &H80, &H19, &H26, &H65, &HC9, &H37, &HBD, &H52, &H81, &HF1, &H8B, &HCD})
            Dim compilationAS1 = CreateCompilation(
                New AssemblyIdentity(assemblyNameA, New Version(1, 1, 1, 1), cultureName:="", publicKeyOrToken:=publicKeyA, hasPublicKey:=True),
                {sourceA},
                references:={MscorlibRef},
                options:=TestOptions.DebugDll.WithDelaySign(True))
            Dim referenceAS1 = compilationAS1.EmitToImageReference()
            Dim identityAS1 = referenceAS1.GetAssemblyIdentity()
            Dim compilationAS2 = CreateCompilation(
                New AssemblyIdentity(assemblyNameA, New Version(2, 1, 1, 1), cultureName:="", publicKeyOrToken:=publicKeyA, hasPublicKey:=True),
                {sourceA},
                references:={MscorlibRef},
                options:=TestOptions.DebugDll.WithDelaySign(True))
            Dim referenceAS2 = compilationAS2.EmitToImageReference()
            Dim identityAS2 = referenceAS2.GetAssemblyIdentity()

            ' Assembly B, multiple versions, strong name.
            Dim assemblyNameB = ExpressionCompilerUtilities.GenerateUniqueName()
            Dim publicKeyB = ImmutableArray.CreateRange(Of Byte)({&H0, &H24, &H0, &H0, &H4, &H80, &H0, &H0, &H94, &H0, &H0, &H0, &H6, &H2, &H0, &H0, &H0, &H24, &H0, &H0, &H53, &H52, &H41, &H31, &H0, &H4, &H0, &H0, &H1, &H0, &H1, &H0, &HED, &HD3, &H22, &HCB, &H6B, &HF8, &HD4, &HA2, &HFC, &HCC, &H87, &H37, &H4, &H6, &H4, &HCE, &HE7, &HB2, &HA6, &HF8, &H4A, &HEE, &HF3, &H19, &HDF, &H5B, &H95, &HE3, &H7A, &H6A, &H28, &H24, &HA4, &HA, &H83, &H83, &HBD, &HBA, &HF2, &HF2, &H52, &H20, &HE9, &HAA, &H3B, &HD1, &HDD, &HE4, &H9A, &H9A, &H9C, &HC0, &H30, &H8F, &H1, &H40, &H6, &HE0, &H2B, &H95, &H62, &H89, &H2A, &H34, &H75, &H22, &H68, &H64, &H6E, &H7C, &H2E, &H83, &H50, &H5A, &HCE, &H7B, &HB, &HE8, &HF8, &H71, &HE6, &HF7, &H73, &H8E, &HEB, &H84, &HD2, &H73, &H5D, &H9D, &HBE, &H5E, &HF5, &H90, &HF9, &HAB, &HA, &H10, &H7E, &H23, &H48, &HF4, &HAD, &H70, &H2E, &HF7, &HD4, &H51, &HD5, &H8B, &H3A, &HF7, &HCA, &H90, &H4C, &HDC, &H80, &H19, &H26, &H65, &HC9, &H37, &HBD, &H52, &H81, &HF1, &H8B, &HCD})
            Dim compilationBS1 = CreateCompilation(
                New AssemblyIdentity(assemblyNameB, New Version(1, 1, 1, 1), cultureName:="", publicKeyOrToken:=publicKeyB, hasPublicKey:=True),
                {sourceB},
                references:={MscorlibRef, referenceAS1},
                options:=TestOptions.DebugDll.WithDelaySign(True))
            Dim referenceBS1 = compilationBS1.EmitToImageReference()
            Dim identityBS1 = referenceBS1.GetAssemblyIdentity()
            Dim compilationBS2 = CreateCompilation(
                New AssemblyIdentity(assemblyNameB, New Version(2, 2, 2, 1), cultureName:="", publicKeyOrToken:=publicKeyB, hasPublicKey:=True),
                {sourceB},
                references:={MscorlibRef, referenceAS2},
                options:=TestOptions.DebugDll.WithDelaySign(True))
            Dim referenceBS2 = compilationBS2.EmitToImageReference()
            Dim identityBS2 = referenceBS2.GetAssemblyIdentity()

            ' Assembly C, multiple versions, not strong name.
            Dim compilationCN1 = CreateCompilation(
                New AssemblyIdentity("C", New Version(1, 1, 1, 1)),
                {sourceC},
                references:={MscorlibRef, referenceBS1},
                options:=TestOptions.DebugDll)

            ' Duplicate assemblies, target module referencing BS1.
            WithRuntimeInstance(compilationCN1, {MscorlibRef, referenceAS1, referenceAS2, referenceBS2, referenceBS1, referenceBS2},
                Sub(runtime)
                    Dim typeBlocks As ImmutableArray(Of MetadataBlock) = Nothing
                    Dim methodBlocks As ImmutableArray(Of MetadataBlock) = Nothing
                    Dim moduleVersionId As Guid = Nothing
                    Dim symReader As ISymUnmanagedReader = Nothing
                    Dim typeToken = 0
                    Dim methodToken = 0
                    Dim localSignatureToken = 0
                    GetContextState(runtime, "C", typeBlocks, moduleVersionId, symReader, typeToken, localSignatureToken)
                    GetContextState(runtime, "C.M", methodBlocks, moduleVersionId, symReader, methodToken, localSignatureToken)

                    ' Compile expression with type context.
                    Dim context = EvaluationContext.CreateTypeContext(
                        Nothing,
                        typeBlocks,
                        moduleVersionId,
                        typeToken)

                    ' If VB supported extern aliases, the following would be true:
                    ' Assert.Equal(identityAS2, context.Compilation.GlobalNamespace.GetMembers("A").OfType(Of INamedTypeSymbol)().Single().ContainingAssembly.Identity)
                    ' Assert.Equal(identityBS2, context.Compilation.GlobalNamespace.GetMembers("B").OfType(Of INamedTypeSymbol)().Single().ContainingAssembly.Identity)

                    Dim errorMessage As String = Nothing
                    ' A is ambiguous since there were no explicit references to AS1 or AS2.
                    context.CompileExpression("New A()", errorMessage)
                    Assert.Equal(errorMessage, "error BC30554: 'A' is ambiguous.")
                    ' Ideally, B should be resolved to BS1.
                    context.CompileExpression("New B()", errorMessage)
                    Assert.Equal(errorMessage, "error BC30554: 'B' is ambiguous.")

                    ' Compile expression with method context.
                    Dim previous = New VisualBasicMetadataContext(typeBlocks, context)
                    context = EvaluationContext.CreateMethodContext(
                        previous,
                        methodBlocks,
                        MakeDummyLazyAssemblyReaders(),
                        symReader,
                        moduleVersionId,
                        methodToken,
                        methodVersion:=1,
                        ilOffset:=0,
                        localSignatureToken:=localSignatureToken)
                    Assert.Equal(previous.Compilation, context.Compilation) ' re-use type context compilation
                    ' Ideally, B should be resolved to BS1.
                    context.CompileExpression("New B()", errorMessage)
                    Assert.Equal(errorMessage, "error BC30554: 'B' is ambiguous.")

                End Sub)
        End Sub

        <Fact>
        Public Sub DuplicateTypesAndMethodsDifferentAssemblies()
            Const sourceA =
"Option Strict On
Imports System.Runtime.CompilerServices
Imports N
Namespace N
    Class C1
    End Class
    Public Module E
        <Extension>
        Public Function F(o As A) As A
            Return o
        End Function
    End Module
End Namespace
Class C2
End Class
Public Class A
    Public Shared Sub M()
        Dim x As New A()
        Dim y As Object = x.F()
    End Sub
End Class"
            Const sourceB =
"Option Strict On
Imports System.Runtime.CompilerServices
Imports N
Namespace N
    Class C1
    End Class
    Public Module E
        <Extension>
        Public Function F(o As A) As Integer
            Return 2
        End Function
    End Module
End Namespace
Class C2
End Class
Class B
    Shared Sub Main()
        Dim x As New A()
    End Sub
End Class"
            Dim compilationA = CreateCompilationWithReferences(
                MakeSources(sourceA),
                options:=TestOptions.DebugDll,
                references:={MscorlibRef, SystemRef, MsvbRef, SystemCoreRef})

            Dim moduleA = compilationA.ToModuleInstance()
            Dim identityA = compilationA.Assembly.Identity

            Dim moduleB = CreateCompilationWithReferences(
                MakeSources(sourceB),
                options:=TestOptions.DebugDll,
                references:={MscorlibRef, SystemRef, MsvbRef, SystemCoreRef, moduleA.GetReference()}).ToModuleInstance()

            Dim runtime = CreateRuntimeInstance(
            {
                MscorlibRef.ToModuleInstance(),
                SystemRef.ToModuleInstance(),
                MsvbRef.ToModuleInstance(),
                SystemCoreRef.ToModuleInstance(),
                moduleA,
                moduleB
            })

            Dim blocks As ImmutableArray(Of MetadataBlock) = Nothing
            Dim moduleVersionId As Guid = Nothing
            Dim symReader As ISymUnmanagedReader = Nothing
            Dim typeToken = 0
            Dim methodToken = 0
            Dim localSignatureToken = 0
            GetContextState(runtime, "B", blocks, moduleVersionId, symReader, typeToken, localSignatureToken)
            Dim contextFactory = CreateTypeContextFactory(moduleVersionId, typeToken)

            ' Duplicate type in namespace, at type scope.
            Dim testData As CompilationTestData = Nothing
            Dim errorMessage As String = Nothing
            ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "New N.C1()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
            Assert.Equal(errorMessage, "error BC30560: 'C1' is ambiguous in the namespace 'N'.")

            GetContextState(runtime, "B.Main", blocks, moduleVersionId, symReader, methodToken, localSignatureToken)
            contextFactory = CreateMethodContextFactory(moduleVersionId, symReader, methodToken, localSignatureToken)

            ' Duplicate type in namespace, at method scope.
            ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "New C1()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
            Assert.Equal(errorMessage, "error BC30560: 'C1' is ambiguous in the namespace 'N'.")

            ' Duplicate type in global namespace, at method scope.
            ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "New C2()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
            Assert.Equal(errorMessage, "error BC30554: 'C2' is ambiguous.")

            ' Duplicate extension method, at method scope.
            ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "x.F()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
            Assert.True(errorMessage.StartsWith("error BC30521: Overload resolution failed because no accessible 'F' is most specific for these arguments:"))

            ' Same tests as above but in library that does not directly reference duplicates.
            GetContextState(runtime, "A", blocks, moduleVersionId, symReader, typeToken, localSignatureToken)
            contextFactory = CreateTypeContextFactory(moduleVersionId, typeToken)

            ' Duplicate type in namespace, at type scope.
            ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "New N.C1()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
            Assert.Null(errorMessage)
            Dim methodData = testData.GetMethodData("<>x.<>m0")
            methodData.VerifyIL(
"{
// Code size        6 (0x6)
.maxstack  1
IL_0000:  newobj     ""Sub N.C1..ctor()""
IL_0005:  ret
}")
            Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityA.GetDisplayName())

            GetContextState(runtime, "A.M", blocks, moduleVersionId, symReader, methodToken, localSignatureToken)
            contextFactory = CreateMethodContextFactory(moduleVersionId, symReader, methodToken, localSignatureToken)

            ' Duplicate type in global namespace, at method scope.
            ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "New C2()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
            Assert.Null(errorMessage)
            methodData = testData.GetMethodData("<>x.<>m0")
            methodData.VerifyIL(
"{
// Code size        6 (0x6)
.maxstack  1
.locals init (A V_0, //x
            Object V_1) //y
IL_0000:  newobj     ""Sub C2..ctor()""
IL_0005:  ret
}")
            Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityA.GetDisplayName())

            ' Duplicate extension method, at method scope.
            ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "x.F()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
            Assert.Null(errorMessage)
            methodData = testData.GetMethodData("<>x.<>m0")
            methodData.VerifyIL(
"{
// Code size        7 (0x7)
.maxstack  1
.locals init (A V_0, //x
            Object V_1) //y
IL_0000:  ldloc.0
IL_0001:  call       ""Function N.E.F(A) As A""
IL_0006:  ret
}")
            Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityA.GetDisplayName())
        End Sub

        <Fact>
        Public Sub DuplicateTypesAndMethodsDifferentAssemblies_2()
            Const sourceA =
"Namespace N
    Public Module M
        Public Function F()
            Return 1
        End Function
    End Module
End Namespace"
            Const sourceB =
"Imports N
Class C
    Shared Sub M()
        F()
    End Sub
End Class"
            Dim referenceA1 = CreateCompilation(
                New AssemblyIdentity("A", New Version(1, 1, 1, 1)),
                {sourceA},
                options:=TestOptions.DebugDll,
                references:={MscorlibRef, SystemRef, MsvbRef}).EmitToImageReference()

            Dim referenceA2 = CreateCompilation(
                New AssemblyIdentity("A", New Version(2, 1, 1, 2)),
                {sourceA},
                options:=TestOptions.DebugDll,
                references:={MscorlibRef, SystemRef, MsvbRef}).EmitToImageReference()

            Dim compilationB = CreateCompilation(
                New AssemblyIdentity("B", New Version(1, 1, 1, 1)),
                {sourceB},
                options:=TestOptions.DebugDll,
                references:={MscorlibRef, referenceA1})

            WithRuntimeInstance(compilationB, {MscorlibRef, SystemRef, MsvbRef, referenceA1, referenceA2},
                Sub(runtime)

                    Dim blocks As ImmutableArray(Of MetadataBlock) = Nothing
                    Dim moduleVersionId As Guid = Nothing
                    Dim symReader As ISymUnmanagedReader = Nothing
                    Dim typeToken = 0
                    Dim methodToken = 0
                    Dim localSignatureToken = 0
                    GetContextState(runtime, "C.M", blocks, moduleVersionId, symReader, methodToken, localSignatureToken)

                    Dim context = EvaluationContext.CreateMethodContext(
                        Nothing,
                        blocks,
                        MakeDummyLazyAssemblyReaders(),
                        symReader,
                        moduleVersionId,
                        methodToken,
                        methodVersion:=1,
                        ilOffset:=0,
                        localSignatureToken:=localSignatureToken)
                    Dim errorMessage As String = Nothing
                    context.CompileExpression("F()", errorMessage)
                    Assert.Equal(errorMessage, "error BC30562: 'F' is ambiguous between declarations in Modules 'N.M, N.M'.")

                    Dim testData As New CompilationTestData()
                    Dim contextFactory = CreateMethodContextFactory(moduleVersionId, symReader, methodToken, localSignatureToken)
                    ExpressionCompilerTestHelpers.CompileExpressionWithRetry(blocks, "F()", ImmutableArray(Of [Alias]).Empty, contextFactory, getMetaDataBytesPtr:=Nothing, errorMessage:=errorMessage, testData:=testData)
                    Assert.Null(errorMessage)
                    testData.GetMethodData("<>x.<>m0").VerifyIL(
"{
// Code size        6 (0x6)
.maxstack  1
IL_0000:  call       ""Function N.M.F() As Object""
IL_0005:  ret
}")
                End Sub)
        End Sub

        <WorkItem(1170032, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/1170032")>
        <Fact>
        Public Sub DuplicateTypesInMscorlib()
            Const sourceConsole =
"Namespace System
    Public Class Console
    End Class
End Namespace"
            Const sourceObjectModel =
"Namespace System.Collections.ObjectModel
    Public Class ReadOnlyDictionary(Of K, V)
    End Class
End Namespace"
            Const source =
"Class C
    Shared Sub Main()
        Dim t = GetType(System.Console)
        Dim o = DirectCast(Nothing, System.Collections.ObjectModel.ReadOnlyDictionary(Of Object, Object))
    End Sub
End Class"
            Dim systemConsoleComp = CreateCompilationWithMscorlib({sourceConsole}, options:=TestOptions.DebugDll, assemblyName:="System.Console")
            Dim systemConsoleRef = systemConsoleComp.EmitToImageReference()
            Dim systemObjectModelComp = CreateCompilationWithMscorlib({sourceObjectModel}, options:=TestOptions.DebugDll, assemblyName:="System.ObjectModel")
            Dim systemObjectModelRef = systemObjectModelComp.EmitToImageReference()
            Dim identityObjectModel = systemObjectModelRef.GetAssemblyIdentity()

            ' At runtime System.Runtime.dll contract assembly is replaced
            ' by mscorlib.dll and System.Runtime.dll facade assemblies;
            ' System.Console.dll and System.ObjectModel.dll are not replaced.

            ' Test different ordering of modules containing duplicates:
            ' { System.Console, mscorlib } and { mscorlib, System.ObjectModel }.
            Dim contractReferences = ImmutableArray.Create(systemConsoleRef, SystemRuntimePP7Ref, systemObjectModelRef)
            Dim runtimeReferences = ImmutableArray.Create(systemConsoleRef, MscorlibFacadeRef, SystemRuntimeFacadeRef, systemObjectModelRef)

            ' Verify the compiler reports duplicate types with facade assemblies.
            Dim compilation = CreateCompilationWithReferences(MakeSources(source), references:=runtimeReferences, options:=TestOptions.DebugDll)
            compilation.VerifyDiagnostics(
                Diagnostic(ERRID.ERR_AmbiguousInNamespace2, "System.Console").WithArguments("Console", "System").WithLocation(3, 25),
                Diagnostic(ERRID.ERR_AmbiguousInNamespace2, "System.Collections.ObjectModel.ReadOnlyDictionary(Of Object, Object)").WithArguments("ReadOnlyDictionary", "System.Collections.ObjectModel").WithLocation(4, 37))

            ' EE should not report duplicate type when the original source
            ' is compiled with contract assemblies and the EE expression
            ' is compiled with facade assemblies.
            compilation = CreateCompilationWithReferences(MakeSources(source), references:=contractReferences, options:=TestOptions.DebugDll)

            WithRuntimeInstance(compilation, runtimeReferences,
                Sub(runtime)
                    Dim context = CreateMethodContext(runtime, "C.Main")
                    Dim errorMessage As String = Nothing
                    ' { System.Console, mscorlib }
                    Dim testData = New CompilationTestData()
                    context.CompileExpression("GetType(System.Console)", errorMessage, testData)
                    Dim methodData = testData.GetMethodData("<>x.<>m0")
                    methodData.VerifyIL(
"{
  // Code size       11 (0xb)
  .maxstack  1
  .locals init (System.Type V_0, //t
                System.Collections.ObjectModel.ReadOnlyDictionary(Of Object, Object) V_1) //o
  IL_0000:  ldtoken    ""System.Console""
  IL_0005:  call       ""Function System.Type.GetTypeFromHandle(System.RuntimeTypeHandle) As System.Type""
  IL_000a:  ret
}")
                    ' { mscorlib, System.ObjectModel }
                    testData = New CompilationTestData()
                    context.CompileExpression("DirectCast(Nothing, System.Collections.ObjectModel.ReadOnlyDictionary(Of Object, Object))", errorMessage, testData)
                    methodData = testData.GetMethodData("<>x.<>m0")
                    methodData.VerifyIL(
"{
  // Code size        2 (0x2)
  .maxstack  1
  .locals init (System.Type V_0, //t
                System.Collections.ObjectModel.ReadOnlyDictionary(Of Object, Object) V_1) //o
  IL_0000:  ldnull
  IL_0001:  ret
}")
                    Assert.Equal(methodData.Method.ReturnType.ContainingAssembly.ToDisplayString(), identityObjectModel.GetDisplayName())
                End Sub)
        End Sub

        Private Const CorLibAssemblyName = "System.Private.CoreLib"

        ' An assembly with the expected corlib name and with System.Object should
        ' be considered the corlib, even with references to external assemblies.
        <WorkItem(13275, "https://github.com/dotnet/roslyn/issues/13275")>
        <Fact>
        Public Sub CorLibWithAssemblyReferences()
            Const sourceLib =
"Public Class Private1
End Class
Public Class Private2
End Class"
            Dim compLib = CreateCompilationWithMscorlib(
                {sourceLib},
                options:=TestOptions.ReleaseDll,
                assemblyName:="System.Private.Library")
            compLib.VerifyDiagnostics()
            Dim refLib = compLib.EmitToImageReference()

            Const sourceCorLib =
"Imports System.Runtime.CompilerServices
<Assembly: TypeForwardedTo(GetType(Private2))>
Namespace System
    Public Class [Object]
        Public Function F() As Private1
            Return Nothing
        End Function
    End Class
    Public Class Void
        Inherits [Object]
    End Class
End Namespace"
            ' Create a custom corlib with a reference to compilation
            ' above and a reference to the actual mscorlib.
            Dim compCorLib = CreateCompilation(
                {Parse(sourceCorLib)},
                options:=TestOptions.ReleaseDll,
                references:={MscorlibRef, refLib},
                assemblyName:=CorLibAssemblyName)
            compCorLib.VerifyDiagnostics()
            Dim objectType = compCorLib.SourceAssembly.GlobalNamespace.GetMember(Of NamedTypeSymbol)("System.Object")
            Assert.NotNull(objectType.BaseType)

            Dim peBytes As ImmutableArray(Of Byte) = Nothing
            Dim pdbBytes As ImmutableArray(Of Byte) = Nothing
            ExpressionCompilerTestHelpers.EmitCorLibWithAssemblyReferences(
                compCorLib,
                Nothing,
                Function(moduleBuilder, emitOptions) New PEAssemblyBuilderWithAdditionalReferences(moduleBuilder, emitOptions, objectType),
                peBytes,
                pdbBytes)

            Using reader As New PEReader(peBytes)
                Dim metadata = reader.GetMetadata()
                Dim [module] = metadata.ToModuleMetadata(ignoreAssemblyRefs:=True)
                Dim metadataReader = metadata.ToMetadataReader()
                Dim moduleInstance = Microsoft.CodeAnalysis.ExpressionEvaluator.UnitTests.ModuleInstance.Create(metadata, metadataReader.GetModuleVersionIdOrThrow())

                ' Verify the module declares System.Object.
                Assert.True(metadataReader.DeclaresTheObjectClass())
                ' Verify the PEModule has no assembly references.
                Assert.Equal(0, [module].Module.ReferencedAssemblies.Length)
                ' Verify the underlying metadata has the expected assembly references.
                Dim actualReferences = metadataReader.AssemblyReferences.Select(Function(r) metadataReader.GetString(metadataReader.GetAssemblyReference(r).Name)).ToImmutableArray()
                AssertEx.Equal({"mscorlib", "System.Private.Library"}, actualReferences)

                Const source =
"Class C
    Shared Sub M()
    End Sub
End Class"
                Dim comp = CreateCompilation(
                    {Parse(source)},
                    options:=TestOptions.ReleaseDll,
                    references:={refLib, AssemblyMetadata.Create([module]).GetReference()})
                comp.VerifyDiagnostics()

                Using runtime = RuntimeInstance.Create({comp.ToModuleInstance(), moduleInstance})
                    Dim errorMessage As String = Nothing
                    Dim context = CreateMethodContext(runtime, "C.M")

                    ' Valid expression.
                    Dim testData = New CompilationTestData()
                    context.CompileExpression("New Object()", errorMessage, testData)
                    Assert.Null(errorMessage)
                    Dim methodData = testData.GetMethodData("<>x.<>m0")
                    methodData.VerifyIL(
"{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  newobj     ""Sub Object..ctor()""
  IL_0005:  ret
}")

                    ' Invalid expression: System.Int32 is not defined in corlib above.
                    testData = New CompilationTestData()
                    context.CompileExpression("1", errorMessage, testData)
                    Assert.Equal("error BC30002: Type 'System.Int32' is not defined.", errorMessage)

                    ' Invalid expression: type in method signature from missing referenced assembly.
                    testData = New CompilationTestData()
                    context.CompileExpression("(New Object()).F()", errorMessage, testData)
                    Assert.Equal("error BC30657: 'F' has a return type that is not supported or parameter types that are not supported.", errorMessage)

                    ' Invalid expression: type forwarded to missing referenced assembly.
                    testData = New CompilationTestData()
                    context.CompileExpression("New Private2()", errorMessage, testData)
                    Assert.Equal("error BC30002: Type 'Private2' is not defined.", errorMessage)
                End Using
            End Using
        End Sub

        Private Shared Function CreateTypeContextFactory(
            moduleVersionId As Guid,
            typeToken As Integer) As ExpressionCompiler.CreateContextDelegate

            Return Function(blocks, useReferencedModulesOnly)
                       Dim compilation = If(useReferencedModulesOnly, blocks.ToCompilationReferencedModulesOnly(moduleVersionId), blocks.ToCompilation())
                       Return EvaluationContext.CreateTypeContext(
                           compilation,
                           moduleVersionId,
                           typeToken)
                   End Function
        End Function

        Private Shared Function CreateMethodContextFactory(
            moduleVersionId As Guid,
            symReader As ISymUnmanagedReader,
            methodToken As Integer,
            localSignatureToken As Integer) As ExpressionCompiler.CreateContextDelegate

            Return Function(blocks, useReferencedModulesOnly)
                       Dim compilation = If(useReferencedModulesOnly, blocks.ToCompilationReferencedModulesOnly(moduleVersionId), blocks.ToCompilation())
                       Return EvaluationContext.CreateMethodContext(
                            compilation,
                            MakeDummyLazyAssemblyReaders(),
                            symReader,
                            moduleVersionId,
                            methodToken,
                            methodVersion:=1,
                            ilOffset:=0,
                            localSignatureToken:=localSignatureToken)
                   End Function
        End Function

        Private NotInheritable Class PEAssemblyBuilderWithAdditionalReferences
            Inherits PEModuleBuilder
            Implements IAssemblyReference

            Private ReadOnly _builder As CommonPEModuleBuilder
            Private ReadOnly _objectType As NamespaceTypeDefinitionNoBase

            Friend Sub New(builder As CommonPEModuleBuilder, emitOptions As EmitOptions, objectType As INamespaceTypeDefinition)
                MyBase.New(DirectCast(builder.CommonSourceModule, SourceModuleSymbol), emitOptions, builder.OutputKind, builder.SerializationProperties, builder.ManifestResources)

                _builder = builder
                _objectType = New NamespaceTypeDefinitionNoBase(objectType)
            End Sub

            Friend Overrides Iterator Function GetTopLevelTypesCore(context As EmitContext) As IEnumerable(Of INamespaceTypeDefinition)
                For Each t In MyBase.GetTopLevelTypesCore(context)
                    Yield If(t Is _objectType.UnderlyingType, _objectType, t)
                Next
            End Function

            Public Overrides ReadOnly Property CurrentGenerationOrdinal As Integer
                Get
                    Return _builder.CurrentGenerationOrdinal
                End Get
            End Property

            Public Overrides ReadOnly Property SourceAssemblyOpt As ISourceAssemblySymbolInternal
                Get
                    Return _builder.SourceAssemblyOpt
                End Get
            End Property

            Public Overrides Function GetFiles(context As EmitContext) As IEnumerable(Of IFileReference)
                Return _builder.GetFiles(context)
            End Function

            Protected Overrides Sub AddEmbeddedResourcesFromAddedModules(builder As ArrayBuilder(Of Cci.ManagedResource), diagnostics As DiagnosticBag)
            End Sub

            Friend Overrides ReadOnly Property AllowOmissionOfConditionalCalls As Boolean
                Get
                    Return True
                End Get
            End Property

            Private ReadOnly Property Identity As AssemblyIdentity Implements IAssemblyReference.Identity
                Get
                    Return DirectCast(_builder, IAssemblyReference).Identity
                End Get
            End Property

            Private ReadOnly Property AssemblyVersionPattern As Version Implements IAssemblyReference.AssemblyVersionPattern
                Get
                    Return DirectCast(_builder, IAssemblyReference).AssemblyVersionPattern
                End Get
            End Property
        End Class

    End Class

End Namespace
