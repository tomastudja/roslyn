﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.ConvertTypeOfToNameOf
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.ConvertTypeOfToNameOf
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Friend NotInheritable Class VisualBasicConvertTypeOfToNameOfDiagnosticAnalyzer
        Inherits AbstractConvertTypeOfToNameOfDiagnosticAnalyzer

        Protected Overrides Function IsValidTypeofAction(context As OperationAnalysisContext) As Boolean
            Dim node As SyntaxNode
            Dim compilation As Compilation
            Dim isValidLanguage As Boolean
            Dim IsValidType As Boolean
            Dim IsParentValid As Boolean

            node = context.Operation.Syntax
            compilation = context.Compilation
            isValidLanguage = DirectCast(compilation, VisualBasicCompilation).LanguageVersion >= LanguageVersion.VisualBasic14
            IsValidType = node.IsKind(SyntaxKind.GetTypeExpression)
            IsParentValid = node.Parent.GetType() Is GetType(MemberAccessExpressionSyntax)

            Return isValidLanguage And IsValidType And IsParentValid
        End Function

        Protected Overrides Function LanguageReportDiagnostic(location As Location, cSharpDescriptor As DiagnosticDescriptor, visualBasicDescriptor As DiagnosticDescriptor, options As CompilationOptions) As Diagnostic
            Return DiagnosticHelper.Create(visualBasicDescriptor,
                                           location,
                                           visualBasicDescriptor.GetEffectiveSeverity(options),
                                           additionalLocations:=Nothing,
                                           properties:=Nothing)
        End Function
    End Class
End Namespace
