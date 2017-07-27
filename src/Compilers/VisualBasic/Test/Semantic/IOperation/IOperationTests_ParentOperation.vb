﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Roslyn.Test.Utilities

Namespace Microsoft.CodeAnalysis.VisualBasic.UnitTests.Semantics
    Partial Public Class IOperationTests
        Inherits SemanticModelTestBase

        <Fact>
        Public Sub TestParentOperations()
            Dim sourceCode = TestResource.AllInOneVisualBasicCode

            Dim fileName = "a.vb"
            Dim syntaxTree = Parse(sourceCode, fileName, options:=Nothing)

            Dim compilation = CreateCompilationWithMscorlib45AndVBRuntime({syntaxTree}, DefaultVbReferences.Concat({ValueTupleRef, SystemRuntimeFacadeRef}))
            Dim tree = (From t In compilation.SyntaxTrees Where t.FilePath = fileName).Single()
            Dim model = compilation.GetSemanticModel(tree)

            ' visit tree top down to gather child to parent map
            Dim parentMap = GetParentOperationsMap(model)

            ' go through all foundings to see whether parent Is correct
            For Each kv In parentMap
                Dim child = kv.Key
                Dim parent = kv.Value

                ' check parent property returns same parent we gathered by walking down operation tree
                Assert.Equal(child.Parent, parent)

                ' check SearchparentOperation return same parent
                Assert.Equal(DirectCast(child, Operation).SearchParentOperation(), parent)
            Next
        End Sub
    End Class
End Namespace

