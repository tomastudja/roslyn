﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Collections.Immutable
Imports System.Text
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.LanguageServices
Imports Microsoft.CodeAnalysis.PooledObjects
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.SyntaxFacts
Imports System.Diagnostics.CodeAnalysis

#If CODE_STYLE Then
Imports Microsoft.CodeAnalysis.Internal.Editing
#Else
Imports Microsoft.CodeAnalysis.Editing
#End If

Namespace Microsoft.CodeAnalysis.VisualBasic.LanguageServices
    Friend Class VisualBasicSyntaxFacts
        Inherits AbstractSyntaxFacts
        Implements ISyntaxFacts

        Public Shared ReadOnly Property Instance As New VisualBasicSyntaxFacts

        Protected Sub New()
        End Sub

        Public ReadOnly Property IsCaseSensitive As Boolean Implements ISyntaxFacts.IsCaseSensitive
            Get
                Return False
            End Get
        End Property

        Public ReadOnly Property StringComparer As StringComparer Implements ISyntaxFacts.StringComparer
            Get
                Return CaseInsensitiveComparison.Comparer
            End Get
        End Property

        Public ReadOnly Property ElasticMarker As SyntaxTrivia Implements ISyntaxFacts.ElasticMarker
            Get
                Return SyntaxFactory.ElasticMarker
            End Get
        End Property

        Public ReadOnly Property ElasticCarriageReturnLineFeed As SyntaxTrivia Implements ISyntaxFacts.ElasticCarriageReturnLineFeed
            Get
                Return SyntaxFactory.ElasticCarriageReturnLineFeed
            End Get
        End Property

        Public Overrides ReadOnly Property SyntaxKinds As ISyntaxKinds = VisualBasicSyntaxKinds.Instance Implements ISyntaxFacts.SyntaxKinds

        Public Function SupportsIndexingInitializer(options As ParseOptions) As Boolean Implements ISyntaxFacts.SupportsIndexingInitializer
            Return False
        End Function

        Public Function SupportsThrowExpression(options As ParseOptions) As Boolean Implements ISyntaxFacts.SupportsThrowExpression
            Return False
        End Function

        Public Function SupportsLocalFunctionDeclaration(options As ParseOptions) As Boolean Implements ISyntaxFacts.SupportsLocalFunctionDeclaration
            Return False
        End Function

        Public Function SupportsRecord(options As ParseOptions) As Boolean Implements ISyntaxFacts.SupportsRecord
            Return False
        End Function

        Public Function SupportsRecordStruct(options As ParseOptions) As Boolean Implements ISyntaxFacts.SupportsRecordStruct
            Return False
        End Function

        Public Function ParseToken(text As String) As SyntaxToken Implements ISyntaxFacts.ParseToken
            Return SyntaxFactory.ParseToken(text, startStatement:=True)
        End Function

        Public Function ParseLeadingTrivia(text As String) As SyntaxTriviaList Implements ISyntaxFacts.ParseLeadingTrivia
            Return SyntaxFactory.ParseLeadingTrivia(text)
        End Function

        Public Function EscapeIdentifier(identifier As String) As String Implements ISyntaxFacts.EscapeIdentifier
            Dim keywordKind = SyntaxFacts.GetKeywordKind(identifier)
            Dim needsEscaping = keywordKind <> SyntaxKind.None

            Return If(needsEscaping, "[" & identifier & "]", identifier)
        End Function

        Public Function IsVerbatimIdentifier(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsVerbatimIdentifier
            Return False
        End Function

        Public Function IsOperator(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsOperator
            Return (IsUnaryExpressionOperatorToken(CType(token.Kind, SyntaxKind)) AndAlso (TypeOf token.Parent Is UnaryExpressionSyntax OrElse TypeOf token.Parent Is OperatorStatementSyntax)) OrElse
                   (IsBinaryExpressionOperatorToken(CType(token.Kind, SyntaxKind)) AndAlso (TypeOf token.Parent Is BinaryExpressionSyntax OrElse TypeOf token.Parent Is OperatorStatementSyntax))
        End Function

        Public Function IsContextualKeyword(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsContextualKeyword
            Return token.IsContextualKeyword()
        End Function

        Public Function IsReservedKeyword(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsReservedKeyword
            Return token.IsReservedKeyword()
        End Function

        Public Function IsPreprocessorKeyword(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsPreprocessorKeyword
            Return token.IsPreprocessorKeyword()
        End Function

        Public Function IsPreProcessorDirectiveContext(syntaxTree As SyntaxTree, position As Integer, cancellationToken As CancellationToken) As Boolean Implements ISyntaxFacts.IsPreProcessorDirectiveContext
            Return syntaxTree.IsInPreprocessorDirectiveContext(position, cancellationToken)
        End Function

        Public Function TryGetCorrespondingOpenBrace(token As SyntaxToken, ByRef openBrace As SyntaxToken) As Boolean Implements ISyntaxFacts.TryGetCorrespondingOpenBrace

            If token.Kind = SyntaxKind.CloseBraceToken Then
                Dim tuples = token.Parent.GetBraces()
                openBrace = tuples.openBrace
                Return openBrace.Kind = SyntaxKind.OpenBraceToken
            End If

            Return False
        End Function

        Public Function IsEntirelyWithinStringOrCharOrNumericLiteral(syntaxTree As SyntaxTree, position As Integer, cancellationToken As CancellationToken) As Boolean Implements ISyntaxFacts.IsEntirelyWithinStringOrCharOrNumericLiteral
            If syntaxTree Is Nothing Then
                Return False
            End If

            Return syntaxTree.IsEntirelyWithinStringOrCharOrNumericLiteral(position, cancellationToken)
        End Function

        Public Function IsDirective(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDirective
            Return TypeOf node Is DirectiveTriviaSyntax
        End Function

        Public Function TryGetExternalSourceInfo(node As SyntaxNode, ByRef info As ExternalSourceInfo) As Boolean Implements ISyntaxFacts.TryGetExternalSourceInfo
            Select Case node.Kind
                Case SyntaxKind.ExternalSourceDirectiveTrivia
                    info = New ExternalSourceInfo(CInt(DirectCast(node, ExternalSourceDirectiveTriviaSyntax).LineStart.Value), False)
                    Return True

                Case SyntaxKind.EndExternalSourceDirectiveTrivia
                    info = New ExternalSourceInfo(Nothing, True)
                    Return True
            End Select

            Return False
        End Function

        Public Function IsDeclarationExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDeclarationExpression
            ' VB doesn't support declaration expressions
            Return False
        End Function

        Public Function IsAttributeName(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsAttributeName
            Return node.IsParentKind(SyntaxKind.Attribute) AndAlso
                DirectCast(node.Parent, AttributeSyntax).Name Is node
        End Function

        Public Function IsNameOfSimpleMemberAccessExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsNameOfSimpleMemberAccessExpression
            Dim vbNode = TryCast(node, ExpressionSyntax)
            Return vbNode IsNot Nothing AndAlso vbNode.IsSimpleMemberAccessExpressionName()
        End Function

        Public Function IsNameOfAnyMemberAccessExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsNameOfAnyMemberAccessExpression
            Dim memberAccess = TryCast(node?.Parent, MemberAccessExpressionSyntax)
            Return memberAccess IsNot Nothing AndAlso memberAccess.Name Is node
        End Function

        Public Function GetStandaloneExpression(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetStandaloneExpression
            Return SyntaxFactory.GetStandaloneExpression(TryCast(node, ExpressionSyntax))
        End Function

        Public Function GetRootConditionalAccessExpression(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetRootConditionalAccessExpression
            Return TryCast(node, ExpressionSyntax).GetRootConditionalAccessExpression()
        End Function

        Public Function IsNamedArgument(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsNamedArgument
            Dim arg = TryCast(node, SimpleArgumentSyntax)
            Return arg?.NameColonEquals IsNot Nothing
        End Function

        Public Function IsNameOfNamedArgument(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsNameOfNamedArgument
            Return node.CheckParent(Of SimpleArgumentSyntax)(Function(p) p.IsNamed AndAlso p.NameColonEquals.Name Is node)
        End Function

        Public Function GetNameOfParameter(node As SyntaxNode) As SyntaxToken? Implements ISyntaxFacts.GetNameOfParameter
            Return DirectCast(node, ParameterSyntax).Identifier?.Identifier
        End Function

        Public Function GetDefaultOfParameter(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetDefaultOfParameter
            Return DirectCast(node, ParameterSyntax).Default
        End Function

        Public Function GetParameterList(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetParameterList
            Return node.GetParameterList()
        End Function

        Public Function IsParameterList(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsParameterList
            Return node.IsKind(SyntaxKind.ParameterList)
        End Function

        Public Function ISyntaxFacts_HasIncompleteParentMember(node As SyntaxNode) As Boolean Implements ISyntaxFacts.HasIncompleteParentMember
            Return HasIncompleteParentMember(node)
        End Function

        Public Function GetIdentifierOfGenericName(genericName As SyntaxNode) As SyntaxToken Implements ISyntaxFacts.GetIdentifierOfGenericName
            Return DirectCast(genericName, GenericNameSyntax).Identifier
        End Function

        Public Function IsUsingDirectiveName(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsUsingDirectiveName
            Return node.IsParentKind(SyntaxKind.SimpleImportsClause) AndAlso
                   DirectCast(node.Parent, SimpleImportsClauseSyntax).Name Is node
        End Function

        Public Function IsDeconstructionAssignment(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDeconstructionAssignment
            Return False
        End Function

        Public Function IsDeconstructionForEachStatement(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDeconstructionForEachStatement
            Return False
        End Function

        Public Function IsStatement(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsStatement
            Return TypeOf node Is StatementSyntax
        End Function

        Public Function IsExecutableStatement(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsExecutableStatement
            Return TypeOf node Is ExecutableStatementSyntax
        End Function

        Public Function IsMethodBody(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsMethodBody
            Return TypeOf node Is MethodBlockBaseSyntax
        End Function

        Public Function GetExpressionOfReturnStatement(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetExpressionOfReturnStatement
            Return DirectCast(node, ReturnStatementSyntax).Expression
        End Function

        Public Function IsThisConstructorInitializer(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsThisConstructorInitializer
            If TypeOf token.Parent Is IdentifierNameSyntax AndAlso token.HasMatchingText(SyntaxKind.NewKeyword) Then
                Dim memberAccess = TryCast(token.Parent.Parent, MemberAccessExpressionSyntax)
                Return memberAccess.IsThisConstructorInitializer()
            End If

            Return False
        End Function

        Public Function IsBaseConstructorInitializer(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsBaseConstructorInitializer
            If TypeOf token.Parent Is IdentifierNameSyntax AndAlso token.HasMatchingText(SyntaxKind.NewKeyword) Then
                Dim memberAccess = TryCast(token.Parent.Parent, MemberAccessExpressionSyntax)
                Return memberAccess.IsBaseConstructorInitializer()
            End If

            Return False
        End Function

        Public Function IsQueryKeyword(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsQueryKeyword
            Select Case token.Kind()
                Case _
                    SyntaxKind.JoinKeyword,
                    SyntaxKind.IntoKeyword,
                    SyntaxKind.AggregateKeyword,
                    SyntaxKind.DistinctKeyword,
                    SyntaxKind.SkipKeyword,
                    SyntaxKind.TakeKeyword,
                    SyntaxKind.LetKeyword,
                    SyntaxKind.ByKeyword,
                    SyntaxKind.OrderKeyword,
                    SyntaxKind.WhereKeyword,
                    SyntaxKind.OnKeyword,
                    SyntaxKind.FromKeyword,
                    SyntaxKind.WhileKeyword,
                    SyntaxKind.SelectKeyword
                    Return TypeOf token.Parent Is QueryClauseSyntax
                Case SyntaxKind.GroupKeyword
                    Return (TypeOf token.Parent Is QueryClauseSyntax) OrElse (token.Parent.IsKind(SyntaxKind.GroupAggregation))
                Case SyntaxKind.EqualsKeyword
                    Return TypeOf token.Parent Is JoinConditionSyntax
                Case SyntaxKind.AscendingKeyword, SyntaxKind.DescendingKeyword
                    Return TypeOf token.Parent Is OrderingSyntax
                Case SyntaxKind.InKeyword
                    Return TypeOf token.Parent Is CollectionRangeVariableSyntax
                Case Else
                    Return False
            End Select
        End Function

        Public Function IsPredefinedType(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsPredefinedType
            Dim actualType As PredefinedType = PredefinedType.None
            Return TryGetPredefinedType(token, actualType) AndAlso actualType <> PredefinedType.None
        End Function

        Public Function IsPredefinedType(token As SyntaxToken, type As PredefinedType) As Boolean Implements ISyntaxFacts.IsPredefinedType
            Dim actualType As PredefinedType = PredefinedType.None
            Return TryGetPredefinedType(token, actualType) AndAlso actualType = type
        End Function

        Public Function TryGetPredefinedType(token As SyntaxToken, ByRef type As PredefinedType) As Boolean Implements ISyntaxFacts.TryGetPredefinedType
            type = GetPredefinedType(token)
            Return type <> PredefinedType.None
        End Function

        Private Shared Function GetPredefinedType(token As SyntaxToken) As PredefinedType
            Select Case token.Kind
                Case SyntaxKind.BooleanKeyword
                    Return PredefinedType.Boolean
                Case SyntaxKind.ByteKeyword
                    Return PredefinedType.Byte
                Case SyntaxKind.SByteKeyword
                    Return PredefinedType.SByte
                Case SyntaxKind.IntegerKeyword
                    Return PredefinedType.Int32
                Case SyntaxKind.UIntegerKeyword
                    Return PredefinedType.UInt32
                Case SyntaxKind.ShortKeyword
                    Return PredefinedType.Int16
                Case SyntaxKind.UShortKeyword
                    Return PredefinedType.UInt16
                Case SyntaxKind.LongKeyword
                    Return PredefinedType.Int64
                Case SyntaxKind.ULongKeyword
                    Return PredefinedType.UInt64
                Case SyntaxKind.SingleKeyword
                    Return PredefinedType.Single
                Case SyntaxKind.DoubleKeyword
                    Return PredefinedType.Double
                Case SyntaxKind.DecimalKeyword
                    Return PredefinedType.Decimal
                Case SyntaxKind.StringKeyword
                    Return PredefinedType.String
                Case SyntaxKind.CharKeyword
                    Return PredefinedType.Char
                Case SyntaxKind.ObjectKeyword
                    Return PredefinedType.Object
                Case SyntaxKind.DateKeyword
                    Return PredefinedType.DateTime
                Case Else
                    Return PredefinedType.None
            End Select
        End Function

        Public Function IsPredefinedOperator(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsPredefinedOperator
            Dim actualOp As PredefinedOperator = PredefinedOperator.None
            Return TryGetPredefinedOperator(token, actualOp) AndAlso actualOp <> PredefinedOperator.None
        End Function

        Public Function IsPredefinedOperator(token As SyntaxToken, op As PredefinedOperator) As Boolean Implements ISyntaxFacts.IsPredefinedOperator
            Dim actualOp As PredefinedOperator = PredefinedOperator.None
            Return TryGetPredefinedOperator(token, actualOp) AndAlso actualOp = op
        End Function

        Public Function TryGetPredefinedOperator(token As SyntaxToken, ByRef op As PredefinedOperator) As Boolean Implements ISyntaxFacts.TryGetPredefinedOperator
            op = GetPredefinedOperator(token)
            Return op <> PredefinedOperator.None
        End Function

        Private Shared Function GetPredefinedOperator(token As SyntaxToken) As PredefinedOperator
            Select Case token.Kind
                Case SyntaxKind.PlusToken, SyntaxKind.PlusEqualsToken
                    Return PredefinedOperator.Addition

                Case SyntaxKind.MinusToken, SyntaxKind.MinusEqualsToken
                    Return PredefinedOperator.Subtraction

                Case SyntaxKind.AndKeyword, SyntaxKind.AndAlsoKeyword
                    Return PredefinedOperator.BitwiseAnd

                Case SyntaxKind.OrKeyword, SyntaxKind.OrElseKeyword
                    Return PredefinedOperator.BitwiseOr

                Case SyntaxKind.AmpersandToken, SyntaxKind.AmpersandEqualsToken
                    Return PredefinedOperator.Concatenate

                Case SyntaxKind.SlashToken, SyntaxKind.SlashEqualsToken
                    Return PredefinedOperator.Division

                Case SyntaxKind.EqualsToken
                    Return PredefinedOperator.Equality

                Case SyntaxKind.XorKeyword
                    Return PredefinedOperator.ExclusiveOr

                Case SyntaxKind.CaretToken, SyntaxKind.CaretEqualsToken
                    Return PredefinedOperator.Exponent

                Case SyntaxKind.GreaterThanToken
                    Return PredefinedOperator.GreaterThan

                Case SyntaxKind.GreaterThanEqualsToken
                    Return PredefinedOperator.GreaterThanOrEqual

                Case SyntaxKind.LessThanGreaterThanToken
                    Return PredefinedOperator.Inequality

                Case SyntaxKind.BackslashToken, SyntaxKind.BackslashEqualsToken
                    Return PredefinedOperator.IntegerDivision

                Case SyntaxKind.LessThanLessThanToken, SyntaxKind.LessThanLessThanEqualsToken
                    Return PredefinedOperator.LeftShift

                Case SyntaxKind.LessThanToken
                    Return PredefinedOperator.LessThan

                Case SyntaxKind.LessThanEqualsToken
                    Return PredefinedOperator.LessThanOrEqual

                Case SyntaxKind.LikeKeyword
                    Return PredefinedOperator.Like

                Case SyntaxKind.NotKeyword
                    Return PredefinedOperator.Complement

                Case SyntaxKind.ModKeyword
                    Return PredefinedOperator.Modulus

                Case SyntaxKind.AsteriskToken, SyntaxKind.AsteriskEqualsToken
                    Return PredefinedOperator.Multiplication

                Case SyntaxKind.GreaterThanGreaterThanToken, SyntaxKind.GreaterThanGreaterThanEqualsToken
                    Return PredefinedOperator.RightShift

                Case Else
                    Return PredefinedOperator.None
            End Select
        End Function

        Public Function GetText(kind As Integer) As String Implements ISyntaxFacts.GetText
            Return SyntaxFacts.GetText(CType(kind, SyntaxKind))
        End Function

        Public Function IsIdentifierPartCharacter(c As Char) As Boolean Implements ISyntaxFacts.IsIdentifierPartCharacter
            Return SyntaxFacts.IsIdentifierPartCharacter(c)
        End Function

        Public Function IsIdentifierStartCharacter(c As Char) As Boolean Implements ISyntaxFacts.IsIdentifierStartCharacter
            Return SyntaxFacts.IsIdentifierStartCharacter(c)
        End Function

        Public Function IsIdentifierEscapeCharacter(c As Char) As Boolean Implements ISyntaxFacts.IsIdentifierEscapeCharacter
            Return c = "["c OrElse c = "]"c
        End Function

        Public Function IsValidIdentifier(identifier As String) As Boolean Implements ISyntaxFacts.IsValidIdentifier
            Dim token = SyntaxFactory.ParseToken(identifier)
            ' TODO: There is no way to get the diagnostics to see if any are actually errors?
            Return IsIdentifier(token) AndAlso Not token.ContainsDiagnostics AndAlso token.ToString().Length = identifier.Length
        End Function

        Public Function IsVerbatimIdentifier(identifier As String) As Boolean Implements ISyntaxFacts.IsVerbatimIdentifier
            Return IsValidIdentifier(identifier) AndAlso MakeHalfWidthIdentifier(identifier.First()) = "[" AndAlso MakeHalfWidthIdentifier(identifier.Last()) = "]"
        End Function

        Public Function IsTypeCharacter(c As Char) As Boolean Implements ISyntaxFacts.IsTypeCharacter
            Return c = "%"c OrElse
                   c = "&"c OrElse
                   c = "@"c OrElse
                   c = "!"c OrElse
                   c = "#"c OrElse
                   c = "$"c
        End Function

        Public Function IsStartOfUnicodeEscapeSequence(c As Char) As Boolean Implements ISyntaxFacts.IsStartOfUnicodeEscapeSequence
            Return False ' VB does not support identifiers with escaped unicode characters
        End Function

        Public Function IsLiteral(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsLiteral
            Select Case token.Kind()
                Case _
                        SyntaxKind.IntegerLiteralToken,
                        SyntaxKind.CharacterLiteralToken,
                        SyntaxKind.DecimalLiteralToken,
                        SyntaxKind.FloatingLiteralToken,
                        SyntaxKind.DateLiteralToken,
                        SyntaxKind.StringLiteralToken,
                        SyntaxKind.DollarSignDoubleQuoteToken,
                        SyntaxKind.DoubleQuoteToken,
                        SyntaxKind.InterpolatedStringTextToken,
                        SyntaxKind.TrueKeyword,
                        SyntaxKind.FalseKeyword,
                        SyntaxKind.NothingKeyword
                    Return True
            End Select

            Return False
        End Function

        Public Function IsStringLiteralOrInterpolatedStringLiteral(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsStringLiteralOrInterpolatedStringLiteral
            Return token.IsKind(SyntaxKind.StringLiteralToken, SyntaxKind.InterpolatedStringTextToken)
        End Function

        Public Function IsBindableToken(token As Microsoft.CodeAnalysis.SyntaxToken) As Boolean Implements ISyntaxFacts.IsBindableToken
            Return Me.IsWord(token) OrElse
                Me.IsLiteral(token) OrElse
                Me.IsOperator(token)
        End Function

        Public Function IsPointerMemberAccessExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsPointerMemberAccessExpression
            Return False
        End Function

        Public Sub GetNameAndArityOfSimpleName(node As SyntaxNode, ByRef name As String, ByRef arity As Integer) Implements ISyntaxFacts.GetNameAndArityOfSimpleName
            Dim simpleName = DirectCast(node, SimpleNameSyntax)
            name = simpleName.Identifier.ValueText
            arity = simpleName.Arity
        End Sub

        Public Function LooksGeneric(name As SyntaxNode) As Boolean Implements ISyntaxFacts.LooksGeneric
            Return name.IsKind(SyntaxKind.GenericName)
        End Function

        Public Function GetExpressionOfMemberAccessExpression(node As SyntaxNode, Optional allowImplicitTarget As Boolean = False) As SyntaxNode Implements ISyntaxFacts.GetExpressionOfMemberAccessExpression
            Return TryCast(node, MemberAccessExpressionSyntax)?.GetExpressionOfMemberAccessExpression(allowImplicitTarget)
        End Function

        Public Function GetTargetOfMemberBinding(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetTargetOfMemberBinding
            ' Member bindings are a C# concept.
            Return Nothing
        End Function

        Public Function GetNameOfMemberBindingExpression(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetNameOfMemberBindingExpression
            ' Member bindings are a C# concept.
            Return Nothing
        End Function

        Public Sub GetPartsOfElementAccessExpression(node As SyntaxNode, ByRef expression As SyntaxNode, ByRef argumentList As SyntaxNode) Implements ISyntaxFacts.GetPartsOfElementAccessExpression
            Dim invocation = TryCast(node, InvocationExpressionSyntax)
            If invocation IsNot Nothing Then
                expression = invocation?.Expression
                argumentList = invocation?.ArgumentList
                Return
            End If

            If node.Kind() = SyntaxKind.DictionaryAccessExpression Then
                GetPartsOfMemberAccessExpression(node, expression, argumentList)
                Return
            End If

            Throw ExceptionUtilities.UnexpectedValue(node.Kind())
        End Sub

        Public Function GetExpressionOfInterpolation(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetExpressionOfInterpolation
            Return DirectCast(node, InterpolationSyntax).Expression
        End Function

        Public Function IsInNamespaceOrTypeContext(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsInNamespaceOrTypeContext
            Return SyntaxFacts.IsInNamespaceOrTypeContext(node)
        End Function

        Public Function IsBaseTypeList(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsBaseTypeList
            Return TryCast(node, InheritsOrImplementsStatementSyntax) IsNot Nothing
        End Function

        Public Function IsInStaticContext(node As Microsoft.CodeAnalysis.SyntaxNode) As Boolean Implements ISyntaxFacts.IsInStaticContext
            Return node.IsInStaticContext()
        End Function

        Public Function GetExpressionOfArgument(node As Microsoft.CodeAnalysis.SyntaxNode) As Microsoft.CodeAnalysis.SyntaxNode Implements ISyntaxFacts.GetExpressionOfArgument
            Return DirectCast(node, ArgumentSyntax).GetArgumentExpression()
        End Function

        Public Function GetRefKindOfArgument(node As Microsoft.CodeAnalysis.SyntaxNode) As Microsoft.CodeAnalysis.RefKind Implements ISyntaxFacts.GetRefKindOfArgument
            ' TODO(cyrusn): Consider the method this argument is passed to, to determine this.
            Return RefKind.None
        End Function

        Public Function IsArgument(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsArgument
            Return TypeOf node Is ArgumentSyntax
        End Function

        Public Function IsSimpleArgument(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsSimpleArgument
            Dim argument = TryCast(node, ArgumentSyntax)
            Return argument IsNot Nothing AndAlso Not argument.IsNamed AndAlso Not argument.IsOmitted
        End Function

        Public Function IsInConstantContext(node As Microsoft.CodeAnalysis.SyntaxNode) As Boolean Implements ISyntaxFacts.IsInConstantContext
            Return node.IsInConstantContext()
        End Function

        Public Function IsInConstructor(node As Microsoft.CodeAnalysis.SyntaxNode) As Boolean Implements ISyntaxFacts.IsInConstructor
            Return node.GetAncestors(Of StatementSyntax).Any(Function(s) s.Kind = SyntaxKind.ConstructorBlock)
        End Function

        Public Function IsUnsafeContext(node As Microsoft.CodeAnalysis.SyntaxNode) As Boolean Implements ISyntaxFacts.IsUnsafeContext
            Return False
        End Function

        Public Function GetNameOfAttribute(node As SyntaxNode) As Microsoft.CodeAnalysis.SyntaxNode Implements ISyntaxFacts.GetNameOfAttribute
            Return DirectCast(node, AttributeSyntax).Name
        End Function

        Public Function IsAttributeNamedArgumentIdentifier(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsAttributeNamedArgumentIdentifier
            Dim identifierName = TryCast(node, IdentifierNameSyntax)
            Return identifierName.IsParentKind(SyntaxKind.NameColonEquals) AndAlso
                identifierName.Parent.IsParentKind(SyntaxKind.SimpleArgument) AndAlso
                identifierName.Parent.Parent.IsParentKind(SyntaxKind.ArgumentList) AndAlso
                identifierName.Parent.Parent.Parent.IsParentKind(SyntaxKind.Attribute)
        End Function

        Public Function GetContainingTypeDeclaration(root As SyntaxNode, position As Integer) As SyntaxNode Implements ISyntaxFacts.GetContainingTypeDeclaration
            If root Is Nothing Then
                Throw New ArgumentNullException(NameOf(root))
            End If

            If position < 0 OrElse position > root.Span.End Then
                Throw New ArgumentOutOfRangeException(NameOf(position))
            End If

            Return root.
                FindToken(position).
                GetAncestors(Of SyntaxNode)().
                FirstOrDefault(Function(n) TypeOf n Is TypeBlockSyntax OrElse TypeOf n Is DelegateStatementSyntax)
        End Function

        Public Function GetContainingVariableDeclaratorOfFieldDeclaration(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetContainingVariableDeclaratorOfFieldDeclaration
            If node Is Nothing Then
                Throw New ArgumentNullException(NameOf(node))
            End If

            Dim parent = node.Parent

            While node IsNot Nothing
                If node.Kind = SyntaxKind.VariableDeclarator AndAlso node.IsParentKind(SyntaxKind.FieldDeclaration) Then
                    Return node
                End If

                node = node.Parent
            End While

            Return Nothing
        End Function

        Public Function IsMemberInitializerNamedAssignmentIdentifier(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsMemberInitializerNamedAssignmentIdentifier
            Dim unused As SyntaxNode = Nothing
            Return IsMemberInitializerNamedAssignmentIdentifier(node, unused)
        End Function

        Public Function IsMemberInitializerNamedAssignmentIdentifier(
                node As SyntaxNode,
                ByRef initializedInstance As SyntaxNode) As Boolean Implements ISyntaxFacts.IsMemberInitializerNamedAssignmentIdentifier

            Dim identifier = TryCast(node, IdentifierNameSyntax)
            If identifier?.IsChildNode(Of NamedFieldInitializerSyntax)(Function(n) n.Name) Then
                ' .parent is the NamedField.
                ' .parent.parent is the ObjectInitializer.
                ' .parent.parent.parent will be the ObjectCreationExpression.
                initializedInstance = identifier.Parent.Parent.Parent
                Return True
            End If

            Return False
        End Function

        Public Function IsNameOfSubpattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsNameOfSubpattern
            Return False
        End Function

        Public Function IsPropertyPatternClause(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsPropertyPatternClause
            Return False
        End Function

        Public Function IsElementAccessExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsElementAccessExpression
            ' VB doesn't have a specialized node for element access.  Instead, it just uses an
            ' invocation expression or dictionary access expression.
            Return node.Kind = SyntaxKind.InvocationExpression OrElse node.Kind = SyntaxKind.DictionaryAccessExpression
        End Function

        Public Function IsTypeNamedVarInVariableOrFieldDeclaration(token As SyntaxToken, parent As SyntaxNode) As Boolean Implements ISyntaxFacts.IsTypeNamedVarInVariableOrFieldDeclaration
            Return False
        End Function

        Public Function IsIndexerMemberCRef(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsIndexerMemberCRef
            Return False
        End Function

        Public Function GetContainingMemberDeclaration(root As SyntaxNode, position As Integer, Optional useFullSpan As Boolean = True) As SyntaxNode Implements ISyntaxFacts.GetContainingMemberDeclaration
            Contract.ThrowIfNull(root, NameOf(root))
            Contract.ThrowIfTrue(position < 0 OrElse position > root.FullSpan.End, NameOf(position))

            Dim [end] = root.FullSpan.End
            If [end] = 0 Then
                ' empty file
                Return Nothing
            End If

            ' make sure position doesn't touch end of root
            position = Math.Min(position, [end] - 1)

            Dim node = root.FindToken(position).Parent
            While node IsNot Nothing
                If useFullSpan OrElse node.Span.Contains(position) Then

                    If TypeOf node Is MethodBlockBaseSyntax AndAlso Not TypeOf node.Parent Is PropertyBlockSyntax Then
                        Return node
                    End If

                    If TypeOf node Is MethodBaseSyntax AndAlso Not TypeOf node.Parent Is MethodBlockBaseSyntax Then
                        Return node
                    End If

                    If TypeOf node Is PropertyStatementSyntax AndAlso Not TypeOf node.Parent Is PropertyBlockSyntax Then
                        Return node
                    End If

                    If TypeOf node Is EventStatementSyntax AndAlso Not TypeOf node.Parent Is EventBlockSyntax Then
                        Return node
                    End If

                    If TypeOf node Is PropertyBlockSyntax OrElse
                       TypeOf node Is TypeBlockSyntax OrElse
                       TypeOf node Is EnumBlockSyntax OrElse
                       TypeOf node Is NamespaceBlockSyntax OrElse
                       TypeOf node Is EventBlockSyntax OrElse
                       TypeOf node Is FieldDeclarationSyntax Then
                        Return node
                    End If
                End If

                node = node.Parent
            End While

            Return Nothing
        End Function

        Public Function IsMethodLevelMember(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsMethodLevelMember

            ' Note: Derived types of MethodBaseSyntax are expanded explicitly, since PropertyStatementSyntax and
            ' EventStatementSyntax will NOT be parented by MethodBlockBaseSyntax.  Additionally, there are things
            ' like AccessorStatementSyntax and DelegateStatementSyntax that we never want to tread as method level
            ' members.

            If TypeOf node Is MethodStatementSyntax AndAlso Not TypeOf node.Parent Is MethodBlockBaseSyntax Then
                Return True
            End If

            If TypeOf node Is SubNewStatementSyntax AndAlso Not TypeOf node.Parent Is MethodBlockBaseSyntax Then
                Return True
            End If

            If TypeOf node Is OperatorStatementSyntax AndAlso Not TypeOf node.Parent Is MethodBlockBaseSyntax Then
                Return True
            End If

            If TypeOf node Is PropertyStatementSyntax AndAlso Not TypeOf node.Parent Is PropertyBlockSyntax Then
                Return True
            End If

            If TypeOf node Is EventStatementSyntax AndAlso Not TypeOf node.Parent Is EventBlockSyntax Then
                Return True
            End If

            If TypeOf node Is DeclareStatementSyntax Then
                Return True
            End If

            Return TypeOf node Is ConstructorBlockSyntax OrElse
                   TypeOf node Is MethodBlockSyntax OrElse
                   TypeOf node Is OperatorBlockSyntax OrElse
                   TypeOf node Is EventBlockSyntax OrElse
                   TypeOf node Is PropertyBlockSyntax OrElse
                   TypeOf node Is EnumMemberDeclarationSyntax OrElse
                   TypeOf node Is FieldDeclarationSyntax
        End Function

        Public Function GetMemberBodySpanForSpeculativeBinding(node As SyntaxNode) As TextSpan Implements ISyntaxFacts.GetMemberBodySpanForSpeculativeBinding
            Dim member = GetContainingMemberDeclaration(node, node.SpanStart)
            If member Is Nothing Then
                Return Nothing
            End If

            ' TODO: currently we only support method for now
            Dim method = TryCast(member, MethodBlockBaseSyntax)
            If method IsNot Nothing Then
                If method.BlockStatement Is Nothing OrElse method.EndBlockStatement Is Nothing Then
                    Return Nothing
                End If

                ' We don't want to include the BlockStatement or any trailing trivia up to and including its statement
                ' terminator in the span. Instead, we use the start of the first statement's leading trivia (if any) up
                ' to the start of the EndBlockStatement. If there aren't any statements in the block, we use the start
                ' of the EndBlockStatements leading trivia.

                Dim firstStatement = method.Statements.FirstOrDefault()
                Dim spanStart = If(firstStatement IsNot Nothing,
                                   firstStatement.FullSpan.Start,
                                   method.EndBlockStatement.FullSpan.Start)

                Return TextSpan.FromBounds(spanStart, method.EndBlockStatement.SpanStart)
            End If

            Return Nothing
        End Function

        Public Function ContainsInMemberBody(node As SyntaxNode, span As TextSpan) As Boolean Implements ISyntaxFacts.ContainsInMemberBody
            Dim method = TryCast(node, MethodBlockBaseSyntax)
            If method IsNot Nothing Then
                Return method.Statements.Count > 0 AndAlso ContainsExclusively(GetSyntaxListSpan(method.Statements), span)
            End If

            Dim [event] = TryCast(node, EventBlockSyntax)
            If [event] IsNot Nothing Then
                Return [event].Accessors.Count > 0 AndAlso ContainsExclusively(GetSyntaxListSpan([event].Accessors), span)
            End If

            Dim [property] = TryCast(node, PropertyBlockSyntax)
            If [property] IsNot Nothing Then
                Return [property].Accessors.Count > 0 AndAlso ContainsExclusively(GetSyntaxListSpan([property].Accessors), span)
            End If

            Dim field = TryCast(node, FieldDeclarationSyntax)
            If field IsNot Nothing Then
                Return field.Declarators.Count > 0 AndAlso ContainsExclusively(GetSeparatedSyntaxListSpan(field.Declarators), span)
            End If

            Dim [enum] = TryCast(node, EnumMemberDeclarationSyntax)
            If [enum] IsNot Nothing Then
                Return [enum].Initializer IsNot Nothing AndAlso ContainsExclusively([enum].Initializer.Span, span)
            End If

            Dim propStatement = TryCast(node, PropertyStatementSyntax)
            If propStatement IsNot Nothing Then
                Return propStatement.Initializer IsNot Nothing AndAlso ContainsExclusively(propStatement.Initializer.Span, span)
            End If

            Return False
        End Function

        Private Shared Function ContainsExclusively(outerSpan As TextSpan, innerSpan As TextSpan) As Boolean
            If innerSpan.IsEmpty Then
                Return outerSpan.Contains(innerSpan.Start)
            End If

            Return outerSpan.Contains(innerSpan)
        End Function

        Private Shared Function GetSyntaxListSpan(Of T As SyntaxNode)(list As SyntaxList(Of T)) As TextSpan
            Debug.Assert(list.Count > 0)
            Return TextSpan.FromBounds(list.First.SpanStart, list.Last.Span.End)
        End Function

        Private Shared Function GetSeparatedSyntaxListSpan(Of T As SyntaxNode)(list As SeparatedSyntaxList(Of T)) As TextSpan
            Debug.Assert(list.Count > 0)
            Return TextSpan.FromBounds(list.First.SpanStart, list.Last.Span.End)
        End Function

        Public Function GetTopLevelAndMethodLevelMembers(root As SyntaxNode) As List(Of SyntaxNode) Implements ISyntaxFacts.GetTopLevelAndMethodLevelMembers
            Dim list = New List(Of SyntaxNode)()
            AppendMembers(root, list, topLevel:=True, methodLevel:=True)
            Return list
        End Function

        Public Function GetMethodLevelMembers(root As SyntaxNode) As List(Of SyntaxNode) Implements ISyntaxFacts.GetMethodLevelMembers
            Dim list = New List(Of SyntaxNode)()
            AppendMembers(root, list, topLevel:=False, methodLevel:=True)
            Return list
        End Function

        Public Function GetMembersOfTypeDeclaration(typeDeclaration As SyntaxNode) As SyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetMembersOfTypeDeclaration
            Return DirectCast(typeDeclaration, TypeBlockSyntax).Members
        End Function

        Public Function IsTopLevelNodeWithMembers(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsTopLevelNodeWithMembers
            Return TypeOf node Is NamespaceBlockSyntax OrElse
                   TypeOf node Is TypeBlockSyntax OrElse
                   TypeOf node Is EnumBlockSyntax
        End Function

        Private Const s_dotToken As String = "."

        Public Function GetDisplayName(node As SyntaxNode, options As DisplayNameOptions, Optional rootNamespace As String = Nothing) As String Implements ISyntaxFacts.GetDisplayName
            If node Is Nothing Then
                Return String.Empty
            End If

            Dim pooled = PooledStringBuilder.GetInstance()
            Dim builder = pooled.Builder

            ' member keyword (if any)
            Dim memberDeclaration = TryCast(node, DeclarationStatementSyntax)
            If (options And DisplayNameOptions.IncludeMemberKeyword) <> 0 Then
                Dim keywordToken = memberDeclaration.GetMemberKeywordToken()
                If keywordToken <> Nothing AndAlso Not keywordToken.IsMissing Then
                    builder.Append(keywordToken.Text)
                    builder.Append(" "c)
                End If
            End If

            Dim names = ArrayBuilder(Of String).GetInstance()
            ' containing type(s)
            Dim parent = node.Parent
            While TypeOf parent Is TypeBlockSyntax
                names.Push(GetName(parent, options, containsGlobalKeyword:=False))
                parent = parent.Parent
            End While

            If (options And DisplayNameOptions.IncludeNamespaces) <> 0 Then
                ' containing namespace(s) in source (if any)
                Dim containsGlobalKeyword As Boolean = False
                While parent IsNot Nothing AndAlso parent.Kind() = SyntaxKind.NamespaceBlock
                    names.Push(GetName(parent, options, containsGlobalKeyword))
                    parent = parent.Parent
                End While
                ' root namespace (if any)
                If Not containsGlobalKeyword AndAlso Not String.IsNullOrEmpty(rootNamespace) Then
                    builder.Append(rootNamespace)
                    builder.Append(s_dotToken)
                End If
            End If

            While Not names.IsEmpty()
                Dim name = names.Pop()
                If name IsNot Nothing Then
                    builder.Append(name)
                    builder.Append(s_dotToken)
                End If
            End While

            names.Free()

            ' name (include generic type parameters)
            builder.Append(GetName(node, options, containsGlobalKeyword:=False))

            ' parameter list (if any)
            If (options And DisplayNameOptions.IncludeParameters) <> 0 Then
                builder.Append(memberDeclaration.GetParameterList())
            End If

            ' As clause (if any)
            If (options And DisplayNameOptions.IncludeType) <> 0 Then
                Dim asClause = memberDeclaration.GetAsClause()
                If asClause IsNot Nothing Then
                    builder.Append(" "c)
                    builder.Append(asClause)
                End If
            End If

            Return pooled.ToStringAndFree()
        End Function

        Private Shared Function GetName(node As SyntaxNode, options As DisplayNameOptions, ByRef containsGlobalKeyword As Boolean) As String
            Const missingTokenPlaceholder As String = "?"

            Select Case node.Kind()
                Case SyntaxKind.CompilationUnit
                    Return Nothing
                Case SyntaxKind.IdentifierName
                    Dim identifier = DirectCast(node, IdentifierNameSyntax).Identifier
                    Return If(identifier.IsMissing, missingTokenPlaceholder, identifier.Text)
                Case SyntaxKind.IncompleteMember
                    Return missingTokenPlaceholder
                Case SyntaxKind.NamespaceBlock
                    Dim nameSyntax = CType(node, NamespaceBlockSyntax).NamespaceStatement.Name
                    If nameSyntax.Kind() = SyntaxKind.GlobalName Then
                        containsGlobalKeyword = True
                        Return Nothing
                    Else
                        Return GetName(nameSyntax, options, containsGlobalKeyword)
                    End If
                Case SyntaxKind.QualifiedName
                    Dim qualified = CType(node, QualifiedNameSyntax)
                    If qualified.Left.Kind() = SyntaxKind.GlobalName Then
                        containsGlobalKeyword = True
                        Return GetName(qualified.Right, options, containsGlobalKeyword) ' don't use the Global prefix if specified
                    Else
                        Return GetName(qualified.Left, options, containsGlobalKeyword) + s_dotToken + GetName(qualified.Right, options, containsGlobalKeyword)
                    End If
            End Select

            Dim name As String = Nothing
            Dim memberDeclaration = TryCast(node, DeclarationStatementSyntax)
            If memberDeclaration IsNot Nothing Then
                Dim nameToken = memberDeclaration.GetNameToken()
                If nameToken <> Nothing Then
                    name = If(nameToken.IsMissing, missingTokenPlaceholder, nameToken.Text)
                    If (options And DisplayNameOptions.IncludeTypeParameters) <> 0 Then
                        Dim pooled = PooledStringBuilder.GetInstance()
                        Dim builder = pooled.Builder
                        builder.Append(name)
                        AppendTypeParameterList(builder, memberDeclaration.GetTypeParameterList())
                        name = pooled.ToStringAndFree()
                    End If
                End If
            End If

            Debug.Assert(name IsNot Nothing, "Unexpected node type " + node.Kind().ToString())
            Return name
        End Function

        Private Shared Sub AppendTypeParameterList(builder As StringBuilder, typeParameterList As TypeParameterListSyntax)
            If typeParameterList IsNot Nothing AndAlso typeParameterList.Parameters.Count > 0 Then
                builder.Append("(Of ")
                builder.Append(typeParameterList.Parameters(0).Identifier.Text)
                For i = 1 To typeParameterList.Parameters.Count - 1
                    builder.Append(", ")
                    builder.Append(typeParameterList.Parameters(i).Identifier.Text)
                Next

                builder.Append(")"c)
            End If
        End Sub

        Private Sub AppendMembers(node As SyntaxNode, list As List(Of SyntaxNode), topLevel As Boolean, methodLevel As Boolean)
            Debug.Assert(topLevel OrElse methodLevel)

            For Each member In node.GetMembers()
                If IsTopLevelNodeWithMembers(member) Then
                    If topLevel Then
                        list.Add(member)
                    End If

                    AppendMembers(member, list, topLevel, methodLevel)
                    Continue For
                End If

                If methodLevel AndAlso IsMethodLevelMember(member) Then
                    list.Add(member)
                End If
            Next
        End Sub

        Public Function TryGetBindableParent(token As SyntaxToken) As SyntaxNode Implements ISyntaxFacts.TryGetBindableParent
            Dim node = token.Parent
            While node IsNot Nothing
                Dim parent = node.Parent

                ' If this node is on the left side of a member access expression, don't ascend
                ' further or we'll end up binding to something else.
                Dim memberAccess = TryCast(parent, MemberAccessExpressionSyntax)
                If memberAccess IsNot Nothing Then
                    If memberAccess.Expression Is node Then
                        Exit While
                    End If
                End If

                ' If this node is on the left side of a qualified name, don't ascend
                ' further or we'll end up binding to something else.
                Dim qualifiedName = TryCast(parent, QualifiedNameSyntax)
                If qualifiedName IsNot Nothing Then
                    If qualifiedName.Left Is node Then
                        Exit While
                    End If
                End If

                ' If this node is the type of an object creation expression, return the
                ' object creation expression.
                Dim objectCreation = TryCast(parent, ObjectCreationExpressionSyntax)
                If objectCreation IsNot Nothing Then
                    If objectCreation.Type Is node Then
                        node = parent
                        Exit While
                    End If
                End If

                ' The inside of an interpolated string is treated as its own token so we
                ' need to force navigation to the parent expression syntax.
                If TypeOf node Is InterpolatedStringTextSyntax AndAlso TypeOf parent Is InterpolatedStringExpressionSyntax Then
                    node = parent
                    Exit While
                End If

                ' If this node is not parented by a name, we're done.
                Dim name = TryCast(parent, NameSyntax)
                If name Is Nothing Then
                    Exit While
                End If

                node = parent
            End While

            Return node
        End Function

        Public Function GetConstructors(root As SyntaxNode, cancellationToken As CancellationToken) As IEnumerable(Of SyntaxNode) Implements ISyntaxFacts.GetConstructors
            Dim compilationUnit = TryCast(root, CompilationUnitSyntax)
            If compilationUnit Is Nothing Then
                Return SpecializedCollections.EmptyEnumerable(Of SyntaxNode)()
            End If

            Dim constructors = New List(Of SyntaxNode)()
            AppendConstructors(compilationUnit.Members, constructors, cancellationToken)
            Return constructors
        End Function

        Private Sub AppendConstructors(members As SyntaxList(Of StatementSyntax), constructors As List(Of SyntaxNode), cancellationToken As CancellationToken)
            For Each member As StatementSyntax In members
                cancellationToken.ThrowIfCancellationRequested()

                Dim constructor = TryCast(member, ConstructorBlockSyntax)
                If constructor IsNot Nothing Then
                    constructors.Add(constructor)
                    Continue For
                End If

                Dim [namespace] = TryCast(member, NamespaceBlockSyntax)
                If [namespace] IsNot Nothing Then
                    AppendConstructors([namespace].Members, constructors, cancellationToken)
                End If

                Dim [class] = TryCast(member, ClassBlockSyntax)
                If [class] IsNot Nothing Then
                    AppendConstructors([class].Members, constructors, cancellationToken)
                End If

                Dim [struct] = TryCast(member, StructureBlockSyntax)
                If [struct] IsNot Nothing Then
                    AppendConstructors([struct].Members, constructors, cancellationToken)
                End If
            Next
        End Sub

        Public Function GetInactiveRegionSpanAroundPosition(tree As SyntaxTree, position As Integer, cancellationToken As CancellationToken) As TextSpan Implements ISyntaxFacts.GetInactiveRegionSpanAroundPosition
            Dim trivia = tree.FindTriviaToLeft(position, cancellationToken)
            If trivia.Kind = SyntaxKind.DisabledTextTrivia Then
                Return trivia.FullSpan
            End If

            Return Nothing
        End Function

        Public Function GetNameForArgument(argument As SyntaxNode) As String Implements ISyntaxFacts.GetNameForArgument
            If TryCast(argument, ArgumentSyntax)?.IsNamed Then
                Return DirectCast(argument, SimpleArgumentSyntax).NameColonEquals.Name.Identifier.ValueText
            End If

            Return String.Empty
        End Function

        Public Function GetNameForAttributeArgument(argument As SyntaxNode) As String Implements ISyntaxFacts.GetNameForAttributeArgument
            ' All argument types are ArgumentSyntax in VB.
            Return GetNameForArgument(argument)
        End Function

        Public Function IsLeftSideOfDot(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsLeftSideOfDot
            Return TryCast(node, ExpressionSyntax).IsLeftSideOfDot()
        End Function

        Public Function GetRightSideOfDot(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetRightSideOfDot
            Return If(TryCast(node, QualifiedNameSyntax)?.Right,
                      TryCast(node, MemberAccessExpressionSyntax)?.Name)
        End Function

        Public Function GetLeftSideOfDot(node As SyntaxNode, Optional allowImplicitTarget As Boolean = False) As SyntaxNode Implements ISyntaxFacts.GetLeftSideOfDot
            Return If(TryCast(node, QualifiedNameSyntax)?.Left,
                      TryCast(node, MemberAccessExpressionSyntax)?.GetExpressionOfMemberAccessExpression(allowImplicitTarget))
        End Function

        Public Function IsLeftSideOfExplicitInterfaceSpecifier(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsLeftSideOfExplicitInterfaceSpecifier
            Return IsLeftSideOfDot(node) AndAlso TryCast(node.Parent.Parent, ImplementsClauseSyntax) IsNot Nothing
        End Function

        Public Function IsLeftSideOfAssignment(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsLeftSideOfAssignment
            Return TryCast(node, ExpressionSyntax).IsLeftSideOfSimpleAssignmentStatement
        End Function

        Public Function IsLeftSideOfAnyAssignment(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsLeftSideOfAnyAssignment
            Return TryCast(node, ExpressionSyntax).IsLeftSideOfAnyAssignmentStatement
        End Function

        Public Function IsLeftSideOfCompoundAssignment(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsLeftSideOfCompoundAssignment
            Return TryCast(node, ExpressionSyntax).IsLeftSideOfCompoundAssignmentStatement
        End Function

        Public Function GetRightHandSideOfAssignment(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetRightHandSideOfAssignment
            Return DirectCast(node, AssignmentStatementSyntax).Right
        End Function

        Public Function IsInferredAnonymousObjectMemberDeclarator(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsInferredAnonymousObjectMemberDeclarator
            Return node.IsKind(SyntaxKind.InferredFieldInitializer)
        End Function

        Public Function IsOperandOfIncrementExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsOperandOfIncrementExpression
            Return False
        End Function

        Public Function IsOperandOfIncrementOrDecrementExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsOperandOfIncrementOrDecrementExpression
            Return False
        End Function

        Public Function GetContentsOfInterpolatedString(interpolatedString As SyntaxNode) As SyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetContentsOfInterpolatedString
            Return (TryCast(interpolatedString, InterpolatedStringExpressionSyntax)?.Contents).Value
        End Function

        Public Function IsNumericLiteral(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsNumericLiteral
            Return token.Kind = SyntaxKind.DecimalLiteralToken OrElse
                   token.Kind = SyntaxKind.FloatingLiteralToken OrElse
                   token.Kind = SyntaxKind.IntegerLiteralToken
        End Function

        Public Function IsVerbatimStringLiteral(token As SyntaxToken) As Boolean Implements ISyntaxFacts.IsVerbatimStringLiteral
            ' VB does not have verbatim strings
            Return False
        End Function

        Public Function GetArgumentsOfInvocationExpression(node As SyntaxNode) As SeparatedSyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetArgumentsOfInvocationExpression
            Dim argumentList = DirectCast(node, InvocationExpressionSyntax).ArgumentList
            Return If(argumentList Is Nothing, Nothing, GetArgumentsOfArgumentList(argumentList))
        End Function

        Public Function GetArgumentsOfObjectCreationExpression(node As SyntaxNode) As SeparatedSyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetArgumentsOfObjectCreationExpression
            Dim argumentList = DirectCast(node, ObjectCreationExpressionSyntax).ArgumentList
            Return If(argumentList Is Nothing, Nothing, GetArgumentsOfArgumentList(argumentList))
        End Function

        Public Function GetArgumentsOfArgumentList(node As SyntaxNode) As SeparatedSyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetArgumentsOfArgumentList
            Return DirectCast(node, ArgumentListSyntax).Arguments
        End Function

        Public Function ConvertToSingleLine(node As SyntaxNode, Optional useElasticTrivia As Boolean = False) As SyntaxNode Implements ISyntaxFacts.ConvertToSingleLine
            Return node.ConvertToSingleLine(useElasticTrivia)
        End Function

        Public Function IsDocumentationComment(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDocumentationComment
            Return node.IsKind(SyntaxKind.DocumentationCommentTrivia)
        End Function

        Public Function IsUsingOrExternOrImport(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsUsingOrExternOrImport
            Return node.IsKind(SyntaxKind.ImportsStatement)
        End Function

        Public Function IsGlobalAssemblyAttribute(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsGlobalAssemblyAttribute
            Return IsGlobalAttribute(node, SyntaxKind.AssemblyKeyword)
        End Function

        Public Function IsModuleAssemblyAttribute(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsGlobalModuleAttribute
            Return IsGlobalAttribute(node, SyntaxKind.ModuleKeyword)
        End Function

        Private Shared Function IsGlobalAttribute(node As SyntaxNode, attributeTarget As SyntaxKind) As Boolean
            If node.IsKind(SyntaxKind.Attribute) Then
                Dim attributeNode = CType(node, AttributeSyntax)
                If attributeNode.Target IsNot Nothing Then
                    Return attributeNode.Target.AttributeModifier.IsKind(attributeTarget)
                End If
            End If

            Return False
        End Function

        Public Function IsDeclaration(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDeclaration
            ' From the Visual Basic language spec:
            ' NamespaceMemberDeclaration  :=
            '    NamespaceDeclaration  |
            '    TypeDeclaration
            ' TypeDeclaration  ::=
            '    ModuleDeclaration  |
            '    NonModuleDeclaration
            ' NonModuleDeclaration  ::=
            '    EnumDeclaration  |
            '    StructureDeclaration  |
            '    InterfaceDeclaration  |
            '    ClassDeclaration  |
            '    DelegateDeclaration
            ' ClassMemberDeclaration  ::=
            '    NonModuleDeclaration  |
            '    EventMemberDeclaration  |
            '    VariableMemberDeclaration  |
            '    ConstantMemberDeclaration  |
            '    MethodMemberDeclaration  |
            '    PropertyMemberDeclaration  |
            '    ConstructorMemberDeclaration  |
            '    OperatorDeclaration
            Select Case node.Kind()
                ' Because fields declarations can define multiple symbols "Public a, b As Integer"
                ' We want to get the VariableDeclarator node inside the field declaration to print out the symbol for the name.
                Case SyntaxKind.VariableDeclarator
                    If (node.Parent.IsKind(SyntaxKind.FieldDeclaration)) Then
                        Return True
                    End If

                    Return False

                Case SyntaxKind.NamespaceStatement,
                     SyntaxKind.NamespaceBlock,
                     SyntaxKind.ModuleStatement,
                     SyntaxKind.ModuleBlock,
                     SyntaxKind.EnumStatement,
                     SyntaxKind.EnumBlock,
                     SyntaxKind.StructureStatement,
                     SyntaxKind.StructureBlock,
                     SyntaxKind.InterfaceStatement,
                     SyntaxKind.InterfaceBlock,
                     SyntaxKind.ClassStatement,
                     SyntaxKind.ClassBlock,
                     SyntaxKind.DelegateFunctionStatement,
                     SyntaxKind.DelegateSubStatement,
                     SyntaxKind.EventStatement,
                     SyntaxKind.EventBlock,
                     SyntaxKind.AddHandlerAccessorBlock,
                     SyntaxKind.RemoveHandlerAccessorBlock,
                     SyntaxKind.FieldDeclaration,
                     SyntaxKind.SubStatement,
                     SyntaxKind.SubBlock,
                     SyntaxKind.FunctionStatement,
                     SyntaxKind.FunctionBlock,
                     SyntaxKind.PropertyStatement,
                     SyntaxKind.PropertyBlock,
                     SyntaxKind.GetAccessorBlock,
                     SyntaxKind.SetAccessorBlock,
                     SyntaxKind.SubNewStatement,
                     SyntaxKind.ConstructorBlock,
                     SyntaxKind.OperatorStatement,
                     SyntaxKind.OperatorBlock
                    Return True
            End Select

            Return False
        End Function

        ' TypeDeclaration  ::=
        '    ModuleDeclaration  |
        '    NonModuleDeclaration
        ' NonModuleDeclaration  ::=
        '    EnumDeclaration  |
        '    StructureDeclaration  |
        '    InterfaceDeclaration  |
        '    ClassDeclaration  |
        '    DelegateDeclaration
        Public Function IsTypeDeclaration(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsTypeDeclaration
            Select Case node.Kind()
                Case SyntaxKind.EnumBlock,
                     SyntaxKind.StructureBlock,
                     SyntaxKind.InterfaceBlock,
                     SyntaxKind.ClassBlock,
                     SyntaxKind.ModuleBlock,
                     SyntaxKind.DelegateSubStatement,
                     SyntaxKind.DelegateFunctionStatement
                    Return True
            End Select

            Return False
        End Function

        Public Function IsSimpleAssignmentStatement(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsSimpleAssignmentStatement
            Return node.IsKind(SyntaxKind.SimpleAssignmentStatement)
        End Function

        Public Sub GetPartsOfAssignmentStatement(statement As SyntaxNode, ByRef left As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef right As SyntaxNode) Implements ISyntaxFacts.GetPartsOfAssignmentStatement
            ' VB only has assignment statements, so this can just delegate to that helper
            GetPartsOfAssignmentExpressionOrStatement(statement, left, operatorToken, right)
        End Sub

        Public Sub GetPartsOfAssignmentExpressionOrStatement(statement As SyntaxNode, ByRef left As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef right As SyntaxNode) Implements ISyntaxFacts.GetPartsOfAssignmentExpressionOrStatement
            Dim assignment = DirectCast(statement, AssignmentStatementSyntax)
            left = assignment.Left
            operatorToken = assignment.OperatorToken
            right = assignment.Right
        End Sub

        Public Function GetIdentifierOfSimpleName(node As SyntaxNode) As SyntaxToken Implements ISyntaxFacts.GetIdentifierOfSimpleName
            Return DirectCast(node, SimpleNameSyntax).Identifier
        End Function

        Public Function GetIdentifierOfVariableDeclarator(node As SyntaxNode) As SyntaxToken Implements ISyntaxFacts.GetIdentifierOfVariableDeclarator
            Return DirectCast(node, VariableDeclaratorSyntax).Names.Last().Identifier
        End Function

        Public Function GetIdentifierOfParameter(node As SyntaxNode) As SyntaxToken Implements ISyntaxFacts.GetIdentifierOfParameter
            Return DirectCast(node, ParameterSyntax).Identifier.Identifier
        End Function

        Public Function GetIdentifierOfTypeDeclaration(node As SyntaxNode) As SyntaxToken Implements ISyntaxFacts.GetIdentifierOfTypeDeclaration
            Select Case node.Kind()
                Case SyntaxKind.EnumStatement,
                     SyntaxKind.StructureStatement,
                     SyntaxKind.InterfaceStatement,
                     SyntaxKind.ClassStatement,
                     SyntaxKind.ModuleStatement
                    Return DirectCast(node, TypeStatementSyntax).Identifier

                Case SyntaxKind.DelegateSubStatement,
                     SyntaxKind.DelegateFunctionStatement
                    Return DirectCast(node, DelegateStatementSyntax).Identifier
            End Select

            Throw ExceptionUtilities.UnexpectedValue(node)
        End Function

        Public Function GetIdentifierOfIdentifierName(node As SyntaxNode) As SyntaxToken Implements ISyntaxFacts.GetIdentifierOfIdentifierName
            Return DirectCast(node, IdentifierNameSyntax).Identifier
        End Function

        Public Function IsDeclaratorOfLocalDeclarationStatement(declarator As SyntaxNode, localDeclarationStatement As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDeclaratorOfLocalDeclarationStatement
            Return DirectCast(localDeclarationStatement, LocalDeclarationStatementSyntax).Declarators.
                Contains(DirectCast(declarator, VariableDeclaratorSyntax))
        End Function

        Public Function AreEquivalent(token1 As SyntaxToken, token2 As SyntaxToken) As Boolean Implements ISyntaxFacts.AreEquivalent
            Return SyntaxFactory.AreEquivalent(token1, token2)
        End Function

        Public Function AreEquivalent(node1 As SyntaxNode, node2 As SyntaxNode) As Boolean Implements ISyntaxFacts.AreEquivalent
            Return SyntaxFactory.AreEquivalent(node1, node2)
        End Function

        Public Function IsExpressionOfForeach(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsExpressionOfForeach
            Return node IsNot Nothing AndAlso TryCast(node.Parent, ForEachStatementSyntax)?.Expression Is node
        End Function

        Public Function GetExpressionOfExpressionStatement(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetExpressionOfExpressionStatement
            Return DirectCast(node, ExpressionStatementSyntax).Expression
        End Function

        Public Function IsIsExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsIsExpression
            Return node.IsKind(SyntaxKind.TypeOfIsExpression)
        End Function

        Public Function WalkDownParentheses(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.WalkDownParentheses
            Return If(TryCast(node, ExpressionSyntax)?.WalkDownParentheses(), node)
        End Function

        Public Sub GetPartsOfTupleExpression(Of TArgumentSyntax As SyntaxNode)(
                node As SyntaxNode, ByRef openParen As SyntaxToken, ByRef arguments As SeparatedSyntaxList(Of TArgumentSyntax), ByRef closeParen As SyntaxToken) Implements ISyntaxFacts.GetPartsOfTupleExpression

            Dim tupleExpr = DirectCast(node, TupleExpressionSyntax)
            openParen = tupleExpr.OpenParenToken
            arguments = CType(CType(tupleExpr.Arguments, SeparatedSyntaxList(Of SyntaxNode)), SeparatedSyntaxList(Of TArgumentSyntax))
            closeParen = tupleExpr.CloseParenToken
        End Sub

        Public Function GetNextExecutableStatement(statement As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetNextExecutableStatement
            Return DirectCast(statement, StatementSyntax).GetNextStatement()?.FirstAncestorOrSelf(Of ExecutableStatementSyntax)
        End Function

        Private Function ISyntaxFacts_IsSingleLineCommentTrivia(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsSingleLineCommentTrivia
            Return MyBase.IsSingleLineCommentTrivia(trivia)
        End Function

        Private Function ISyntaxFacts_IsMultiLineCommentTrivia(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsMultiLineCommentTrivia
            Return MyBase.IsMultiLineCommentTrivia(trivia)
        End Function

        Private Function ISyntaxFacts_IsSingleLineDocCommentTrivia(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsSingleLineDocCommentTrivia
            Return MyBase.IsSingleLineDocCommentTrivia(trivia)
        End Function

        Private Function ISyntaxFacts_IsMultiLineDocCommentTrivia(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsMultiLineDocCommentTrivia
            Return MyBase.IsMultiLineDocCommentTrivia(trivia)
            Return False
        End Function

        Private Function ISyntaxFacts_IsShebangDirectiveTrivia(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsShebangDirectiveTrivia
            Return MyBase.IsShebangDirectiveTrivia(trivia)
        End Function

        Public Overrides Function IsPreprocessorDirective(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsPreprocessorDirective
            Return SyntaxFacts.IsPreprocessorDirective(trivia.Kind())
        End Function

        Public Function IsRegularComment(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsRegularComment
            Return trivia.Kind = SyntaxKind.CommentTrivia
        End Function

        Public Function IsDocumentationComment(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsDocumentationComment
            Return trivia.Kind = SyntaxKind.DocumentationCommentTrivia
        End Function

        Public Function IsElastic(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsElastic
            Return trivia.IsElastic()
        End Function

        Public Function IsPragmaDirective(trivia As SyntaxTrivia, ByRef isDisable As Boolean, ByRef isActive As Boolean, ByRef errorCodes As SeparatedSyntaxList(Of SyntaxNode)) As Boolean Implements ISyntaxFacts.IsPragmaDirective
            Return trivia.IsPragmaDirective(isDisable, isActive, errorCodes)
        End Function

        Protected Overrides Function ContainsInterleavedDirective(span As TextSpan, token As SyntaxToken, cancellationToken As CancellationToken) As Boolean
            Return token.ContainsInterleavedDirective(span, cancellationToken)
        End Function

        Private Function ISyntaxFacts_ContainsInterleavedDirective(node As SyntaxNode, cancellationToken As CancellationToken) As Boolean Implements ISyntaxFacts.ContainsInterleavedDirective
            Return ContainsInterleavedDirective(node, cancellationToken)
        End Function

        Private Function ISyntaxFacts_ContainsInterleavedDirective1(nodes As ImmutableArray(Of SyntaxNode), cancellationToken As CancellationToken) As Boolean Implements ISyntaxFacts.ContainsInterleavedDirective
            Return ContainsInterleavedDirective(nodes, cancellationToken)
        End Function

        Public Function IsDocumentationCommentExteriorTrivia(trivia As SyntaxTrivia) As Boolean Implements ISyntaxFacts.IsDocumentationCommentExteriorTrivia
            Return trivia.Kind() = SyntaxKind.DocumentationCommentExteriorTrivia
        End Function

        Public Function GetModifiers(node As SyntaxNode) As SyntaxTokenList Implements ISyntaxFacts.GetModifiers
            Return node.GetModifiers()
        End Function

        Public Function WithModifiers(node As SyntaxNode, modifiers As SyntaxTokenList) As SyntaxNode Implements ISyntaxFacts.WithModifiers
            Return node.WithModifiers(modifiers)
        End Function

        Public Function GetVariablesOfLocalDeclarationStatement(node As SyntaxNode) As SeparatedSyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetVariablesOfLocalDeclarationStatement
            Return DirectCast(node, LocalDeclarationStatementSyntax).Declarators
        End Function

        Public Function GetInitializerOfVariableDeclarator(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetInitializerOfVariableDeclarator
            Return DirectCast(node, VariableDeclaratorSyntax).Initializer
        End Function

        Public Function GetTypeOfVariableDeclarator(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetTypeOfVariableDeclarator
            Dim declarator = DirectCast(node, VariableDeclaratorSyntax)
            Return TryCast(declarator.AsClause, SimpleAsClauseSyntax)?.Type
        End Function

        Public Function GetValueOfEqualsValueClause(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetValueOfEqualsValueClause
            Return DirectCast(node, EqualsValueSyntax).Value
        End Function

        Public Function IsScopeBlock(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsScopeBlock
            ' VB has no equivalent of curly braces.
            Return False
        End Function

        Public Function IsExecutableBlock(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsExecutableBlock
            Return node.IsExecutableBlock()
        End Function

        Public Function GetExecutableBlockStatements(node As SyntaxNode) As IReadOnlyList(Of SyntaxNode) Implements ISyntaxFacts.GetExecutableBlockStatements
            Return node.GetExecutableBlockStatements()
        End Function

        Public Function FindInnermostCommonExecutableBlock(nodes As IEnumerable(Of SyntaxNode)) As SyntaxNode Implements ISyntaxFacts.FindInnermostCommonExecutableBlock
            Return nodes.FindInnermostCommonExecutableBlock()
        End Function

        Public Function IsStatementContainer(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsStatementContainer
            Return IsExecutableBlock(node)
        End Function

        Public Function GetStatementContainerStatements(node As SyntaxNode) As IReadOnlyList(Of SyntaxNode) Implements ISyntaxFacts.GetStatementContainerStatements
            Return GetExecutableBlockStatements(node)
        End Function

        Public Function IsConversionExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsConversionExpression
            Return node.Kind = SyntaxKind.CTypeExpression
        End Function

        Public Function IsCastExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsCastExpression
            Return node.Kind = SyntaxKind.DirectCastExpression
        End Function

        Public Sub GetPartsOfCastExpression(node As SyntaxNode, ByRef type As SyntaxNode, ByRef expression As SyntaxNode) Implements ISyntaxFacts.GetPartsOfCastExpression
            Dim cast = DirectCast(node, DirectCastExpressionSyntax)
            type = cast.Type
            expression = cast.Expression
        End Sub

        Public Function GetDeconstructionReferenceLocation(node As SyntaxNode) As Location Implements ISyntaxFacts.GetDeconstructionReferenceLocation
            Throw New NotImplementedException()
        End Function

        Public Function GetDeclarationIdentifierIfOverride(token As SyntaxToken) As SyntaxToken? Implements ISyntaxFacts.GetDeclarationIdentifierIfOverride
            If token.Kind() = SyntaxKind.OverridesKeyword Then
                Dim parent = token.Parent

                Select Case parent.Kind()
                    Case SyntaxKind.SubStatement, SyntaxKind.FunctionStatement
                        Dim method = DirectCast(parent, MethodStatementSyntax)
                        Return method.Identifier

                    Case SyntaxKind.PropertyStatement
                        Dim [property] = DirectCast(parent, PropertyStatementSyntax)
                        Return [property].Identifier
                End Select
            End If

            Return Nothing
        End Function

        Public Shadows Function SpansPreprocessorDirective(nodes As IEnumerable(Of SyntaxNode)) As Boolean Implements ISyntaxFacts.SpansPreprocessorDirective
            Return MyBase.SpansPreprocessorDirective(nodes)
        End Function

        Public Shadows Function SpansPreprocessorDirective(tokens As IEnumerable(Of SyntaxToken)) As Boolean
            Return MyBase.SpansPreprocessorDirective(tokens)
        End Function

        Public Function IsPostfixUnaryExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsPostfixUnaryExpression
            ' Does not exist in VB.
            Return False
        End Function

        Public Function IsMemberBindingExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsMemberBindingExpression
            ' Does not exist in VB.  VB represents a member binding as a MemberAccessExpression with null target.
            Return False
        End Function

        Public Function IsNameOfMemberBindingExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsNameOfMemberBindingExpression
            ' Does not exist in VB.  VB represents a member binding as a MemberAccessExpression with null target.
            Return False
        End Function

        Public Overrides Function GetAttributeLists(node As SyntaxNode) As SyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetAttributeLists
            Return node.GetAttributeLists()
        End Function

        Public Function IsUsingAliasDirective(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsUsingAliasDirective
            Dim importStatement = TryCast(node, ImportsStatementSyntax)

            If (importStatement IsNot Nothing) Then
                For Each importsClause In importStatement.ImportsClauses

                    If importsClause.Kind = SyntaxKind.SimpleImportsClause Then
                        Dim simpleImportsClause = DirectCast(importsClause, SimpleImportsClauseSyntax)

                        If simpleImportsClause.Alias IsNot Nothing Then
                            Return True
                        End If
                    End If
                Next
            End If

            Return False
        End Function

        Public Sub GetPartsOfUsingAliasDirective(
                node As SyntaxNode,
                ByRef globalKeyword As SyntaxToken,
                ByRef [alias] As SyntaxToken,
                ByRef name As SyntaxNode) Implements ISyntaxFacts.GetPartsOfUsingAliasDirective
            Dim importStatement = DirectCast(node, ImportsStatementSyntax)
            For Each importsClause In importStatement.ImportsClauses

                If importsClause.Kind = SyntaxKind.SimpleImportsClause Then
                    Dim simpleImportsClause = DirectCast(importsClause, SimpleImportsClauseSyntax)

                    If simpleImportsClause.Alias IsNot Nothing Then
                        globalKeyword = Nothing
                        [alias] = simpleImportsClause.Alias.Identifier
                        name = simpleImportsClause.Name
                        Return
                    End If
                End If
            Next

            Throw ExceptionUtilities.Unreachable
        End Sub

        Public Overrides Function IsParameterNameXmlElementSyntax(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsParameterNameXmlElementSyntax
            Dim xmlElement = TryCast(node, XmlElementSyntax)
            If xmlElement IsNot Nothing Then
                Dim name = TryCast(xmlElement.StartTag.Name, XmlNameSyntax)
                Return name?.LocalName.ValueText = DocumentationCommentXmlNames.ParameterElementName
            End If

            Return False
        End Function

        Public Overrides Function GetContentFromDocumentationCommentTriviaSyntax(trivia As SyntaxTrivia) As SyntaxList(Of SyntaxNode) Implements ISyntaxFacts.GetContentFromDocumentationCommentTriviaSyntax
            Dim documentationCommentTrivia = TryCast(trivia.GetStructure(), DocumentationCommentTriviaSyntax)
            If documentationCommentTrivia IsNot Nothing Then
                Return documentationCommentTrivia.Content
            End If

            Return Nothing
        End Function

        Friend Shared Function IsChildOf(node As SyntaxNode, kind As SyntaxKind) As Boolean
            Return node.Parent IsNot Nothing AndAlso node.Parent.IsKind(kind)
        End Function

        Friend Shared Function IsChildOfVariableDeclaration(node As SyntaxNode) As Boolean
            Return IsChildOf(node, SyntaxKind.FieldDeclaration) OrElse IsChildOf(node, SyntaxKind.LocalDeclarationStatement)
        End Function

        Private Shared Function GetDeclarationCount(nodes As IReadOnlyList(Of SyntaxNode)) As Integer
            Dim count As Integer = 0
            For i = 0 To nodes.Count - 1
                count = count + GetDeclarationCount(nodes(i))
            Next

            Return count
        End Function

        Friend Shared Function GetDeclarationCount(node As SyntaxNode) As Integer
            Select Case node.Kind
                Case SyntaxKind.FieldDeclaration
                    Return GetDeclarationCount(DirectCast(node, FieldDeclarationSyntax).Declarators)
                Case SyntaxKind.LocalDeclarationStatement
                    Return GetDeclarationCount(DirectCast(node, LocalDeclarationStatementSyntax).Declarators)
                Case SyntaxKind.VariableDeclarator
                    Return DirectCast(node, VariableDeclaratorSyntax).Names.Count
                Case SyntaxKind.AttributesStatement
                    Return GetDeclarationCount(DirectCast(node, AttributesStatementSyntax).AttributeLists)
                Case SyntaxKind.AttributeList
                    Return DirectCast(node, AttributeListSyntax).Attributes.Count
                Case SyntaxKind.ImportsStatement
                    Return DirectCast(node, ImportsStatementSyntax).ImportsClauses.Count
            End Select

            Return 1
        End Function

        Public Function SupportsNotPattern(options As ParseOptions) As Boolean Implements ISyntaxFacts.SupportsNotPattern
            Return False
        End Function

        Public Function IsIsPatternExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsIsPatternExpression
            Return False
        End Function

        Public Function IsAnyPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsAnyPattern
            Return False
        End Function

        Public Function IsAndPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsAndPattern
            Return False
        End Function

        Public Function IsBinaryPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsBinaryPattern
            Return False
        End Function

        Public Function IsConstantPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsConstantPattern
            Return False
        End Function

        Public Function IsDeclarationPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsDeclarationPattern
            Return False
        End Function

        Public Function IsNotPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsNotPattern
            Return False
        End Function

        Public Function IsOrPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsOrPattern
            Return False
        End Function

        Public Function IsParenthesizedPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsParenthesizedPattern
            Return False
        End Function

        Public Function IsRecursivePattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsRecursivePattern
            Return False
        End Function

        Public Function IsUnaryPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsUnaryPattern
            Return False
        End Function

        Public Function IsTypePattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsTypePattern
            Return False
        End Function

        Public Function IsVarPattern(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsVarPattern
            Return False
        End Function

        Public Sub GetPartsOfIsPatternExpression(node As SyntaxNode, ByRef left As SyntaxNode, ByRef isToken As SyntaxToken, ByRef right As SyntaxNode) Implements ISyntaxFacts.GetPartsOfIsPatternExpression
            Throw ExceptionUtilities.Unreachable
        End Sub

        Public Function GetExpressionOfConstantPattern(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetExpressionOfConstantPattern
            Throw ExceptionUtilities.Unreachable
        End Function

        Public Sub GetPartsOfParenthesizedPattern(node As SyntaxNode, ByRef openParen As SyntaxToken, ByRef pattern As SyntaxNode, ByRef closeParen As SyntaxToken) Implements ISyntaxFacts.GetPartsOfParenthesizedPattern
            Throw ExceptionUtilities.Unreachable
        End Sub

        Public Sub GetPartsOfBinaryPattern(node As SyntaxNode, ByRef left As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef right As SyntaxNode) Implements ISyntaxFacts.GetPartsOfBinaryPattern
            Throw ExceptionUtilities.Unreachable
        End Sub

        Public Sub GetPartsOfUnaryPattern(node As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef pattern As SyntaxNode) Implements ISyntaxFacts.GetPartsOfUnaryPattern
            Throw ExceptionUtilities.Unreachable
        End Sub

        Public Sub GetPartsOfDeclarationPattern(node As SyntaxNode, ByRef type As SyntaxNode, ByRef designation As SyntaxNode) Implements ISyntaxFacts.GetPartsOfDeclarationPattern
            Throw New NotImplementedException()
        End Sub

        Public Sub GetPartsOfRecursivePattern(node As SyntaxNode, ByRef type As SyntaxNode, ByRef positionalPart As SyntaxNode, ByRef propertyPart As SyntaxNode, ByRef designation As SyntaxNode) Implements ISyntaxFacts.GetPartsOfRecursivePattern
            Throw New NotImplementedException()
        End Sub

        Public Function GetTypeOfTypePattern(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetTypeOfTypePattern
            Throw New NotImplementedException()
        End Function

        Public Sub GetPartsOfInterpolationExpression(node As SyntaxNode, ByRef stringStartToken As SyntaxToken, ByRef contents As SyntaxList(Of SyntaxNode), ByRef stringEndToken As SyntaxToken) Implements ISyntaxFacts.GetPartsOfInterpolationExpression
            Dim interpolatedStringExpressionSyntax As InterpolatedStringExpressionSyntax = DirectCast(node, InterpolatedStringExpressionSyntax)
            stringStartToken = interpolatedStringExpressionSyntax.DollarSignDoubleQuoteToken
            contents = interpolatedStringExpressionSyntax.Contents
            stringEndToken = interpolatedStringExpressionSyntax.DoubleQuoteToken
        End Sub

        Public Function IsVerbatimInterpolatedStringExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsVerbatimInterpolatedStringExpression
            Return False
        End Function

#Region "IsXXX members"

        Public Function IsAnonymousFunctionExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsAnonymousFunctionExpression
            Return TypeOf node Is LambdaExpressionSyntax
        End Function

        Public Function IsBaseNamespaceDeclaration(<NotNullWhen(True)> node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsBaseNamespaceDeclaration
            Return TypeOf node Is NamespaceBlockSyntax
        End Function

        Public Function IsBinaryExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsBinaryExpression
            Return TypeOf node Is BinaryExpressionSyntax
        End Function

        Public Function IsLiteralExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsLiteralExpression
            Return TypeOf node Is LiteralExpressionSyntax
        End Function

        Public Function IsMemberAccessExpression(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsMemberAccessExpression
            Return TypeOf node Is MemberAccessExpressionSyntax
        End Function

        Public Function IsSimpleName(node As SyntaxNode) As Boolean Implements ISyntaxFacts.IsSimpleName
            Return TypeOf node Is SimpleNameSyntax
        End Function

#End Region

#Region "GetPartsOfXXX members"

        Public Sub GetPartsOfBinaryExpression(node As SyntaxNode, ByRef left As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef right As SyntaxNode) Implements ISyntaxFacts.GetPartsOfBinaryExpression
            Dim binaryExpression = DirectCast(node, BinaryExpressionSyntax)
            left = binaryExpression.Left
            operatorToken = binaryExpression.OperatorToken
            right = binaryExpression.Right
        End Sub

        Public Sub GetPartsOfCompilationUnit(node As SyntaxNode, ByRef [imports] As SyntaxList(Of SyntaxNode), ByRef attributeLists As SyntaxList(Of SyntaxNode), ByRef members As SyntaxList(Of SyntaxNode)) Implements ISyntaxFacts.GetPartsOfCompilationUnit
            Dim compilationUnit = DirectCast(node, CompilationUnitSyntax)
            [imports] = compilationUnit.Imports
            attributeLists = compilationUnit.Attributes
            members = compilationUnit.Members
        End Sub

        Public Sub GetPartsOfConditionalAccessExpression(node As SyntaxNode, ByRef expression As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef whenNotNull As SyntaxNode) Implements ISyntaxFacts.GetPartsOfConditionalAccessExpression
            Dim conditionalAccess = DirectCast(node, ConditionalAccessExpressionSyntax)
            expression = conditionalAccess.Expression
            operatorToken = conditionalAccess.QuestionMarkToken
            whenNotNull = conditionalAccess.WhenNotNull
        End Sub

        Public Sub GetPartsOfConditionalExpression(node As SyntaxNode, ByRef condition As SyntaxNode, ByRef whenTrue As SyntaxNode, ByRef whenFalse As SyntaxNode) Implements ISyntaxFacts.GetPartsOfConditionalExpression
            Dim conditionalExpression = DirectCast(node, TernaryConditionalExpressionSyntax)
            condition = conditionalExpression.Condition
            whenTrue = conditionalExpression.WhenTrue
            whenFalse = conditionalExpression.WhenFalse
        End Sub

        Public Sub GetPartsOfInvocationExpression(node As SyntaxNode, ByRef expression As SyntaxNode, ByRef argumentList As SyntaxNode) Implements ISyntaxFacts.GetPartsOfInvocationExpression
            Dim invocation = DirectCast(node, InvocationExpressionSyntax)
            expression = invocation.Expression
            argumentList = invocation.ArgumentList
        End Sub

        Public Sub GetPartsOfMemberAccessExpression(node As SyntaxNode, ByRef expression As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef name As SyntaxNode) Implements ISyntaxFacts.GetPartsOfMemberAccessExpression
            Dim memberAccess = DirectCast(node, MemberAccessExpressionSyntax)
            expression = memberAccess.Expression
            operatorToken = memberAccess.OperatorToken
            name = memberAccess.Name
        End Sub

        Public Sub GetPartsOfBaseNamespaceDeclaration(node As SyntaxNode, ByRef name As SyntaxNode, ByRef [imports] As SyntaxList(Of SyntaxNode), ByRef members As SyntaxList(Of SyntaxNode)) Implements ISyntaxFacts.GetPartsOfBaseNamespaceDeclaration
            Dim namespaceBlock = DirectCast(node, NamespaceBlockSyntax)
            name = namespaceBlock.NamespaceStatement.Name
            [imports] = Nothing
            members = namespaceBlock.Members
        End Sub

        Public Sub GetPartsOfObjectCreationExpression(node As SyntaxNode, ByRef type As SyntaxNode, ByRef argumentList As SyntaxNode, ByRef initializer As SyntaxNode) Implements ISyntaxFacts.GetPartsOfObjectCreationExpression
            Dim objectCreationExpression = DirectCast(node, ObjectCreationExpressionSyntax)
            type = objectCreationExpression.Type
            argumentList = objectCreationExpression.ArgumentList
            initializer = objectCreationExpression.Initializer
        End Sub

        Public Sub GetPartsOfParenthesizedExpression(node As SyntaxNode, ByRef openParen As SyntaxToken, ByRef expression As SyntaxNode, ByRef closeParen As SyntaxToken) Implements ISyntaxFacts.GetPartsOfParenthesizedExpression
            Dim parenthesizedExpression = DirectCast(node, ParenthesizedExpressionSyntax)
            openParen = parenthesizedExpression.OpenParenToken
            expression = parenthesizedExpression.Expression
            closeParen = parenthesizedExpression.CloseParenToken
        End Sub

        Public Sub GetPartsOfPrefixUnaryExpression(node As SyntaxNode, ByRef operatorToken As SyntaxToken, ByRef operand As SyntaxNode) Implements ISyntaxFacts.GetPartsOfPrefixUnaryExpression
            Dim unaryExpression = DirectCast(node, UnaryExpressionSyntax)
            operatorToken = unaryExpression.OperatorToken
            operand = unaryExpression.Operand
        End Sub

        Public Sub GetPartsOfQualifiedName(node As SyntaxNode, ByRef left As SyntaxNode, ByRef dotToken As SyntaxToken, ByRef right As SyntaxNode) Implements ISyntaxFacts.GetPartsOfQualifiedName
            Dim qualifiedName = DirectCast(node, QualifiedNameSyntax)
            left = qualifiedName.Left
            dotToken = qualifiedName.DotToken
            right = qualifiedName.Right
        End Sub

#End Region

#Region "GetXXXOfYYY members"

        Public Function GetExpressionOfAwaitExpression(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetExpressionOfAwaitExpression
            Return DirectCast(node, AwaitExpressionSyntax).Expression
        End Function

        Public Function GetExpressionOfThrowExpression(node As SyntaxNode) As SyntaxNode Implements ISyntaxFacts.GetExpressionOfThrowExpression
            ' ThrowExpression doesn't exist in VB
            Throw New NotImplementedException()
        End Function

#End Region
    End Class
End Namespace
