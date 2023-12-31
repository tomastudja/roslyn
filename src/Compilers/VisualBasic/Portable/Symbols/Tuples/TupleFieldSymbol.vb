﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Globalization
Imports System.Threading
Imports Microsoft.CodeAnalysis

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Represents a non-element field of a tuple type (such as (int, byte).Rest)
    ''' that is backed by a real field within the tuple underlying type.
    ''' </summary>
    Friend Class TupleFieldSymbol
        Inherits WrappedFieldSymbol

        Protected ReadOnly _containingTuple As TupleTypeSymbol

        ''' <summary>
        ''' If this field represents a tuple element with index X, the field contains
        '''  2X      if this field represents a Default-named element
        '''  2X + 1  if this field represents a Friendly-named element
        ''' Otherwise, (-1 - [index in members array]);
        ''' </summary>
        Private ReadOnly _tupleElementIndex As Integer

        Public Overrides ReadOnly Property IsTupleField As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property TupleUnderlyingField As FieldSymbol
            Get
                Return Me._underlyingField
            End Get
        End Property

        ''' <summary>
        ''' If this is a field representing a tuple element,
        ''' returns the index of the element (zero-based).
        ''' Otherwise returns -1
        ''' </summary>
        Public Overrides ReadOnly Property TupleElementIndex As Integer
            Get
                If _tupleElementIndex < 0 Then
                    Return -1
                End If

                Return _tupleElementIndex >> 1
            End Get
        End Property

        Public Overrides ReadOnly Property IsDefaultTupleElement As Boolean
            Get
                ' not negative and even
                Return (_tupleElementIndex And ((1 << 31) Or 1)) = 0
            End Get
        End Property

        Public Overrides ReadOnly Property AssociatedSymbol As Symbol
            Get
                Return Me._containingTuple.GetTupleMemberSymbolForUnderlyingMember(Of Symbol)(Me._underlyingField.AssociatedSymbol)
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return Me._containingTuple
            End Get
        End Property

        Public Overrides ReadOnly Property CustomModifiers As ImmutableArray(Of CustomModifier)
            Get
                Return Me._underlyingField.CustomModifiers
            End Get
        End Property

        Public Overrides ReadOnly Property Type As TypeSymbol
            Get
                Return Me._underlyingField.Type
            End Get
        End Property

        Public Sub New(container As TupleTypeSymbol, underlyingField As FieldSymbol, tupleElementIndex As Integer)
            MyBase.New(underlyingField)

            Debug.Assert(container.UnderlyingNamedType.IsSameTypeIgnoringAll(underlyingField.ContainingType) OrElse TypeOf Me Is TupleVirtualElementFieldSymbol,
                                            "virtual fields should be represented by " + NameOf(TupleVirtualElementFieldSymbol))

            _containingTuple = container
            _tupleElementIndex = tupleElementIndex
        End Sub

        Public Overrides Function GetAttributes() As ImmutableArray(Of VisualBasicAttributeData)
            Return Me._underlyingField.GetAttributes()
        End Function

        Friend Overrides Function GetUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)
            Dim useSiteInfo As UseSiteInfo(Of AssemblySymbol) = MyBase.GetUseSiteInfo
            MyBase.MergeUseSiteInfo(useSiteInfo, Me._underlyingField.GetUseSiteInfo())
            Return useSiteInfo
        End Function

        Public Overrides Function GetHashCode() As Integer
            Return Hash.Combine(_containingTuple.GetHashCode(), _tupleElementIndex.GetHashCode())
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            Return Me.Equals(TryCast(obj, TupleFieldSymbol))
        End Function

        Public Overloads Function Equals(other As TupleFieldSymbol) As Boolean
            Return other IsNot Nothing AndAlso
                _tupleElementIndex = other._tupleElementIndex AndAlso
                TypeSymbol.Equals(_containingTuple, other._containingTuple, TypeCompareKind.ConsiderEverything)
        End Function

        Public NotOverridable Overrides ReadOnly Property IsRequired As Boolean
            Get
                Return _underlyingField.IsRequired
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Represents an element field of a tuple type (such as (int, byte).Item1)
    ''' that is backed by a real field with the same name within the tuple underlying type.
    ''' </summary>
    Friend Class TupleElementFieldSymbol
        Inherits TupleFieldSymbol

        Private ReadOnly _locations As ImmutableArray(Of Location)

        ' default tuple elements like Item1 Or Item20 could be provided by the user or
        ' otherwise implicitly declared by compiler
        Private ReadOnly _isImplicitlyDeclared As Boolean

        Private ReadOnly _correspondingDefaultField As TupleElementFieldSymbol

        Public Sub New(container As TupleTypeSymbol,
                       underlyingField As FieldSymbol,
                       tupleElementIndex As Integer,
                       location As Location,
                       isImplicitlyDeclared As Boolean,
                       correspondingDefaultFieldOpt As TupleElementFieldSymbol)

            MyBase.New(container, underlyingField, If(correspondingDefaultFieldOpt Is Nothing, tupleElementIndex << 1, (tupleElementIndex << 1) + 1))

            Me._locations = If((location Is Nothing), ImmutableArray(Of Location).Empty, ImmutableArray.Create(Of Location)(location))
            Me._isImplicitlyDeclared = isImplicitlyDeclared

            Debug.Assert(correspondingDefaultFieldOpt Is Nothing = Me.IsDefaultTupleElement)
            Debug.Assert(correspondingDefaultFieldOpt Is Nothing OrElse correspondingDefaultFieldOpt.IsDefaultTupleElement)

            _correspondingDefaultField = If(correspondingDefaultFieldOpt, Me)
        End Sub

        Public Overrides ReadOnly Property Locations As ImmutableArray(Of Location)
            Get
                Return Me._locations
            End Get
        End Property

        Public Overrides ReadOnly Property DeclaringSyntaxReferences As ImmutableArray(Of SyntaxReference)
            Get
                Return If(_isImplicitlyDeclared,
                    ImmutableArray(Of SyntaxReference).Empty,
                    GetDeclaringSyntaxReferenceHelper(Of VisualBasicSyntaxNode)(_locations))
            End Get
        End Property

        Public Overrides ReadOnly Property IsImplicitlyDeclared As Boolean
            Get
                Return _isImplicitlyDeclared
            End Get
        End Property

        Friend Overrides ReadOnly Property TypeLayoutOffset As Integer?
            Get
                Dim flag As Boolean = Me._underlyingField.ContainingType IsNot Me._containingTuple.TupleUnderlyingType
                Dim result As Integer?
                If flag Then
                    result = Nothing
                Else
                    result = MyBase.TypeLayoutOffset
                End If
                Return result
            End Get
        End Property

        Public Overrides ReadOnly Property AssociatedSymbol As Symbol
            Get
                Dim flag As Boolean = Me._underlyingField.ContainingType IsNot Me._containingTuple.TupleUnderlyingType
                Dim result As Symbol
                If flag Then
                    result = Nothing
                Else
                    result = MyBase.AssociatedSymbol
                End If
                Return result
            End Get
        End Property

        Public Overrides ReadOnly Property CorrespondingTupleField As FieldSymbol
            Get
                Return _correspondingDefaultField
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Represents an element field of a tuple type (such as (int a, byte b).a, or (int a, byte b).b)
    ''' that is backed by a real field with a different name within the tuple underlying type.
    ''' </summary>
    Friend NotInheritable Class TupleVirtualElementFieldSymbol
        Inherits TupleElementFieldSymbol

        Private ReadOnly _name As String
        Private ReadOnly _cannotUse As Boolean ' With LanguageVersion 15, we will produce named elements that should not be used

        Public Sub New(container As TupleTypeSymbol,
                       underlyingField As FieldSymbol,
                       name As String,
                       cannotUse As Boolean,
                       tupleElementOrdinal As Integer,
                       location As Location,
                       isImplicitlyDeclared As Boolean,
                       correspondingDefaultFieldOpt As TupleElementFieldSymbol)

            MyBase.New(container, underlyingField, tupleElementOrdinal, location, isImplicitlyDeclared, correspondingDefaultFieldOpt)

            Debug.Assert(name <> Nothing)
            Debug.Assert(name <> underlyingField.Name OrElse Not container.UnderlyingNamedType.Equals(underlyingField.ContainingType),
                                "fields that map directly to underlying should not be represented by " + NameOf(TupleVirtualElementFieldSymbol))

            Me._name = name
            Me._cannotUse = cannotUse
        End Sub

        Friend Overrides Function GetUseSiteInfo() As UseSiteInfo(Of AssemblySymbol)
            If _cannotUse Then
                Return New UseSiteInfo(Of AssemblySymbol)(ErrorFactory.ErrorInfo(ERRID.ERR_TupleInferredNamesNotAvailable, _name,
                                                                                 New VisualBasicRequiredLanguageVersion(LanguageVersion.VisualBasic15_3)))
            End If

            Return MyBase.GetUseSiteInfo()
        End Function

        Public Overrides ReadOnly Property Name As String
            Get
                Return Me._name
            End Get
        End Property

        Friend Overrides ReadOnly Property TypeLayoutOffset As Integer?
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property AssociatedSymbol As Symbol
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property IsVirtualTupleField As Boolean
            Get
                Return True
            End Get
        End Property
    End Class
End Namespace
