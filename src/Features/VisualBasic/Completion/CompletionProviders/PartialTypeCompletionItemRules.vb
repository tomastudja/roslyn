﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.Completion
Imports Microsoft.CodeAnalysis.Completion.Providers
Imports Microsoft.CodeAnalysis.Text

Namespace Microsoft.CodeAnalysis.VisualBasic.Completion.Providers
    Friend Class PartialTypeCompletionItemRules
        Inherits CompletionItemRules

        Public Shared ReadOnly Property Instance As New PartialTypeCompletionItemRules()

        Public Overrides Function GetTextChange(selectedItem As CompletionItem, Optional ch As Char? = Nothing, Optional textTypedSoFar As String = Nothing) As Result(Of TextChange)
            Dim symbolItem = DirectCast(selectedItem, SymbolCompletionItem)
            Return New TextChange(symbolItem.FilterSpan, symbolItem.InsertionText)
        End Function
    End Class
End Namespace