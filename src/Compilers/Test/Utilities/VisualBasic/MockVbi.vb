﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.IO
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.VisualBasic

Friend Class MockVbi
    Inherits VisualBasicCompiler

    Public Sub New(responseFile As String, workingDirectory As String, args As String())
        MyBase.New(VisualBasicCommandLineParser.ScriptRunner, responseFile, args, CreateBuildPaths(workingDirectory), Nothing, New DesktopAnalyzerAssemblyLoader())
    End Sub

    Public Shared Function CreateBuildPaths(workingDirectory As String) As BuildPaths
        Return New BuildPaths(
            clientDir:=Path.GetDirectoryName(GetType(VisualBasicCompiler).Assembly.Location),
            workingDir:=workingDirectory,
            sdkDir:=RuntimeEnvironment.GetRuntimeDirectory())
    End Function
End Class
