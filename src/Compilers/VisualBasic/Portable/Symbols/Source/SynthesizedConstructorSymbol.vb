﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' This class represents a compiler generated parameterless constructor 
    ''' </summary>
    Partial Friend Class SynthesizedConstructorSymbol
        Inherits SynthesizedConstructorBase

        Private ReadOnly _debuggable As Boolean

        ''' <summary>
        ''' Initializes a new instance of the <see cref="SynthesizedConstructorSymbol" /> class.
        ''' </summary>
        ''' <param name="syntaxReference"></param>
        ''' <param name="container">The containing type for the synthesized constructor.</param>
        ''' <param name="isShared">if set to <c>true</c> if this is a shared constructor.</param>
        ''' <param name="isDebuggable">if set to <c>true</c> if this constructor will include debuggable initializers.</param>
        ''' <param name="binder">Binder to be used for error reporting, or Nothing.</param>
        ''' <param name="diagnostics">Diagnostic bag, or Nothing.</param>
        ''' <param name="voidType">Type symbol for no type.</param>
        Friend Sub New(
            syntaxReference As SyntaxReference,
            container As NamedTypeSymbol,
            isShared As Boolean,
            isDebuggable As Boolean,
            binder As Binder,
            diagnostics As DiagnosticBag,
            Optional voidType As TypeSymbol = Nothing
        )
            MyBase.New(syntaxReference, container, isShared, binder, diagnostics, voidType)
            Me._debuggable = isDebuggable
        End Sub

        Friend Overrides Sub AddSynthesizedAttributes(compilationState As ModuleCompilationState, ByRef attributes As ArrayBuilder(Of SynthesizedAttributeData))
            MyBase.AddSynthesizedAttributes(compilationState, attributes)

            ' Dev11 emits DebuggerNonUserCodeAttribute. This attribute is not needed since we don't emit any debug info for the constructor.
        End Sub

        ''' <summary>
        ''' The parameters forming part of this signature.
        ''' </summary>
        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                Return ImmutableArray(Of ParameterSymbol).Empty
            End Get
        End Property

        Friend Overrides ReadOnly Property GenerateDebugInfoImpl As Boolean
            Get
                ' NOTE: we need to have debug info in synthesized constructor to 
                '       support debug information on field/property initializers
                Return _debuggable
            End Get
        End Property

        Friend Overrides Function CalculateLocalSyntaxOffset(localPosition As Integer, localTree As SyntaxTree) As Integer
            ' although the containing type can be PE symbol, such a constructor doesn't 
            ' declare source locals And thus this method shouldn't be called.
            Dim containingType = DirectCast(Me.ContainingType, SourceMemberContainerTypeSymbol)
            Return containingType.CalculateSyntaxOffsetInSynthesizedConstructor(localPosition, localTree, IsShared)
        End Function
    End Class
End Namespace
