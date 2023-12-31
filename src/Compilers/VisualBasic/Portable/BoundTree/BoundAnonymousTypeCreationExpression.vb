﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic

    Partial Friend Class BoundAnonymousTypeCreationExpression

        Public Overrides ReadOnly Property ExpressionSymbol As Symbol
            Get
                Dim type = Me.Type
                Debug.Assert(type IsNot Nothing)

                If type.IsErrorType Then
                    Return Nothing
                End If

                Debug.Assert(type.IsAnonymousType)
                Return DirectCast(type, NamedTypeSymbol).InstanceConstructors(0)
            End Get
        End Property

    End Class

End Namespace
