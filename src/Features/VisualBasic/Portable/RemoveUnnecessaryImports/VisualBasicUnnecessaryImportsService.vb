﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Composition
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.RemoveUnnecessaryImports

Namespace Microsoft.CodeAnalysis.VisualBasic.RemoveUnnecessaryImports
    <ExportLanguageService(GetType(IUnnecessaryImportsService), LanguageNames.VisualBasic), [Shared]>
    Friend Class VisualBasicUnnecessaryImportsService
        Inherits AbstractVisualBasicRemoveUnnecessaryImportsService

    End Class
End Namespace
