﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.Utilities.IntrinsicOperators

Imports Microsoft.CodeAnalysis.QuickInfo
Imports System.Collections.Immutable

Namespace Microsoft.CodeAnalysis.VisualBasic.QuickInfo

    <ExportQuickInfoElementProvider("Semantic", LanguageNames.VisualBasic), [Shared]>
    Friend Class VisualBasicSemanticQuickInfoElementProvider
        Inherits CommonSemanticQuickInfoElementProvider

        Public Sub New()
        End Sub

        Protected Overrides Async Function BuildElementAsync(
                document As Document,
                token As SyntaxToken,
                cancellationToken As CancellationToken) As Task(Of QuickInfoElement)

            Dim vbToken = CType(token, SyntaxToken)
            Dim parent = vbToken.Parent

            Dim predefinedCastExpression = TryCast(parent, PredefinedCastExpressionSyntax)
            If predefinedCastExpression IsNot Nothing AndAlso vbToken = predefinedCastExpression.Keyword Then
                Dim compilation = Await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(False)
                Dim documentation = New PredefinedCastExpressionDocumentation(predefinedCastExpression.Keyword.Kind, compilation)
                Return Await BuildContentForIntrinsicOperatorAsync(document, parent, documentation, Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
            End If

            Select Case vbToken.Kind
                Case SyntaxKind.AddHandlerKeyword
                    If TypeOf parent Is AddRemoveHandlerStatementSyntax Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New AddHandlerStatementDocumentation(), Glyph.Keyword, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.DimKeyword
                    If TypeOf parent Is FieldDeclarationSyntax Then
                        Return Await BuildContentAsync(document, token, DirectCast(parent, FieldDeclarationSyntax).Declarators, cancellationToken).ConfigureAwait(False)
                    ElseIf TypeOf parent Is LocalDeclarationStatementSyntax Then
                        Return Await BuildContentAsync(document, token, DirectCast(parent, LocalDeclarationStatementSyntax).Declarators, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.CTypeKeyword
                    If TypeOf parent Is CTypeExpressionSyntax Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New CTypeCastExpressionDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.DirectCastKeyword
                    If TypeOf parent Is DirectCastExpressionSyntax Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New DirectCastExpressionDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.GetTypeKeyword
                    If TypeOf parent Is GetTypeExpressionSyntax Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New GetTypeExpressionDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.GetXmlNamespaceKeyword
                    If TypeOf parent Is GetXmlNamespaceExpressionSyntax Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New GetXmlNamespaceExpressionDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.IfKeyword
                    If parent.Kind = SyntaxKind.BinaryConditionalExpression Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New BinaryConditionalExpressionDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                    ElseIf parent.Kind = SyntaxKind.TernaryConditionalExpression Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New TernaryConditionalExpressionDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.RemoveHandlerKeyword
                    If TypeOf parent Is AddRemoveHandlerStatementSyntax Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New RemoveHandlerStatementDocumentation(), Glyph.Keyword, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.TryCastKeyword
                    If TypeOf parent Is TryCastExpressionSyntax Then
                        Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New TryCastExpressionDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                    End If

                Case SyntaxKind.IdentifierToken
                    If SyntaxFacts.GetContextualKeywordKind(token.ToString()) = SyntaxKind.MidKeyword Then
                        If parent.Kind = SyntaxKind.MidExpression Then
                            Return Await BuildContentForIntrinsicOperatorAsync(document, parent, New MidAssignmentDocumentation(), Glyph.MethodPublic, cancellationToken).ConfigureAwait(False)
                        End If
                    End If
            End Select

            Return Await MyBase.BuildElementAsync(document, token, cancellationToken).ConfigureAwait(False)
        End Function

        Private Overloads Async Function BuildContentAsync(
                document As Document,
                token As SyntaxToken,
                declarators As SeparatedSyntaxList(Of VariableDeclaratorSyntax),
                cancellationToken As CancellationToken) As Task(Of QuickInfoElement)

            If declarators.Count = 0 Then
                Return Nothing
            End If

            Dim semantics = Await document.GetSemanticModelForNodeAsync(token.Parent, cancellationToken).ConfigureAwait(False)

            Dim types = declarators.SelectMany(Function(d) d.Names).Select(
                Function(n)
                    Dim symbol = semantics.GetDeclaredSymbol(n, cancellationToken)
                    If symbol Is Nothing Then
                        Return Nothing
                    End If

                    Return symbol.TypeSwitch(Function(local As ILocalSymbol) local.Type,
                                             Function(field As IFieldSymbol) field.Type)
                End Function).WhereNotNull().Distinct().ToList()

            If types.Count = 0 Then
                Return Nothing
            End If

            If types.Count > 1 Then
                'Dim contentBuilder = New List(Of TaggedText)
                'contentBuilder.AddText("mulitiple types") 'VBEditorResources.Multiple_Types)
                Return QuickInfoElement.Create(QuickInfoElementKinds.Text, text:=ImmutableArray.Create(New TaggedText(TextTags.Text, "multiple types")))
            End If

            Return Await CreateContentAsync(document.Project.Solution.Workspace, token, semantics, types, supportedPlatforms:=Nothing, cancellationToken:=cancellationToken).ConfigureAwait(False)
        End Function

        Private Async Function BuildContentForIntrinsicOperatorAsync(document As Document,
                                                                     expression As SyntaxNode,
                                                                     documentation As AbstractIntrinsicOperatorDocumentation,
                                                                     glyph As Glyph,
                                                                     cancellationToken As CancellationToken) As Task(Of QuickInfoElement)
            Dim builder = New List(Of SymbolDisplayPart)

            builder.AddRange(documentation.PrefixParts)

            Dim semanticModel = Await document.GetSemanticModelForNodeAsync(expression, cancellationToken).ConfigureAwait(False)

            Dim position = expression.SpanStart

            For i = 0 To documentation.ParameterCount - 1
                If i <> 0 Then
                    builder.AddPunctuation(",")
                    builder.AddSpace()
                End If

                Dim typeNameToBind = documentation.TryGetTypeNameParameter(expression, i)

                If typeNameToBind IsNot Nothing Then
                    ' We'll try to bind the type name 
                    Dim typeInfo = semanticModel.GetTypeInfo(typeNameToBind, cancellationToken)

                    If typeInfo.Type IsNot Nothing Then
                        builder.AddRange(typeInfo.Type.ToMinimalDisplayParts(semanticModel, position))
                        Continue For
                    End If
                End If

                builder.AddRange(documentation.GetParameterDisplayParts(i))
            Next

            builder.AddRange(documentation.GetSuffix(semanticModel, position, expression, cancellationToken))

            Return CreateQuickInfoDisplayElement(
                symbolGlyph:=Me.CreateSymbolGlyphElement(glyph),
                mainDescription:=QuickInfoElement.Create(QuickInfoElementKinds.Description, text:=builder.ToTaggedText()),
                documentation:=QuickInfoElement.Create(QuickInfoElementKinds.Documentation, text:=ImmutableArray.Create(New TaggedText(TextTags.Text, documentation.DocumentationText))))
        End Function
    End Class
End Namespace

