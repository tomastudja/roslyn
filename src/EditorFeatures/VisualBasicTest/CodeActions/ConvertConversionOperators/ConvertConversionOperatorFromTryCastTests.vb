﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports VerifyVB = Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions.VisualBasicCodeRefactoringVerifier(Of
    Microsoft.CodeAnalysis.VisualBasic.CodeRefactorings.ConvertConversionOperators.VisualBasicConvertConversionOperatorFromTryCastCodeRefactoringProvider)

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.CodeRefactorings.ConvertConversionOperators
    <Trait(Traits.Feature, Traits.Features.ConvertConversionOperators)>
    Public Class ConvertConversionOperatorFromTryCastTests

        <Fact>
        Public Async Function ConvertFromTryCastToCType() As Task
            Dim markup ="
Module Program
    Sub M()
        Dim x = TryCast(1[||], Object)
    End Sub
End Module
"

            Dim expected ="
Module Program
    Sub M()
        Dim x = CType(1, Object)
    End Sub
End Module
"

            Await New VerifyVB.Test With
            {
                .TestCode = markup,
                .FixedCode = expected
            }.RunAsync()
        End Function

        <Theory>
        <InlineData("TryCast(TryCast(1, [||]object), C)",
                    "TryCast(CType(1, object), C)")>
        <InlineData("TryCast(TryCast(1, object), [||]C)",
                    "CType(TryCast(1, object), C)")>
        Public Async Function ConvertFromTryCastNested(cTypeExpression As String, converted As String) As Task
            Dim markup ="
Public Class C
End Class

Module Program
    Sub M()
        Dim x = " + cTypeExpression + "
    End Sub
End Module
"

            Dim fixed ="
Public Class C
End Class

Module Program
    Sub M()
        Dim x = " + converted + "
    End Sub
End Module
"

            Await New VerifyVB.Test With
            {
                .TestCode = markup,
                .FixedCode = fixed
            }.RunAsync()
        End Function
    End Class
End Namespace
