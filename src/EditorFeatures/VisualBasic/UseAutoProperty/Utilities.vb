﻿Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UseAutoProperty
    Friend Class Utilities
        Friend Shared Function GetNodeToRemove(identifier As ModifiedIdentifierSyntax) As SyntaxNode
            Dim declarator = DirectCast(identifier.Parent, VariableDeclaratorSyntax)
            If declarator.Names.Count > 1 Then
                ' more than one name in this declarator group.  just remove this name.
                Return identifier
            End If

            ' only name in this declarator group.  if our field has multiple declarator groups,
            ' just remove this one.  otherwise remove the field entirely.
            Dim field = DirectCast(declarator.Parent, FieldDeclarationSyntax)
            If field.Declarators.Count > 1 Then
                Return declarator
            End If

            Return field
        End Function
    End Class
End Namespace