﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.FileHeaders

Namespace Microsoft.CodeAnalysis.VisualBasic.FileHeaders
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Friend Class VisualBasicFileHeaderDiagnosticAnalyzer
        Inherits AbstractFileHeaderDiagnosticAnalyzer

        Protected Overrides Function ParseFileHeader(root As SyntaxNode) As FileHeader
            Return FileHeaderHelpers.ParseFileHeader(root)
        End Function
    End Class
End Namespace
