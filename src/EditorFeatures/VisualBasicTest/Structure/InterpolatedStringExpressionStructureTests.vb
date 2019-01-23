﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Structure
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Outlining
    Public Class InterpolatedStringExpressionStructureTests
        Inherits AbstractVisualBasicSyntaxNodeStructureProviderTests(Of InterpolatedStringExpressionSyntax)

        Friend Overrides Function CreateProvider() As AbstractSyntaxStructureProvider
            Return New InterpolatedStringExpressionStructureProvider()
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.Outlining)>
        Public Async Function TestMultiLineStringLiteral() As Task
            Const code = "
Class C
    Sub M()
        Dim v =
{|hint:{|textspan:$$$""
{123}
""|}|}
    End Sub
End Class
"

            Await VerifyBlockSpansAsync(code,
                Region("textspan", "hint", "...", autoCollapse:=True))
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.Outlining)>
        Public Async Function TestMissingOnIncompleteStringLiteral() As Task
            Const code = "
Class C
    Sub M()
        Dim v =
$$$""
    End Sub
End Class
"

            Await VerifyNoBlockSpansAsync(code)
        End Function
    End Class
End Namespace
