﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.SimplifyInterpolation;

namespace Microsoft.CodeAnalysis.CSharp.SimplifyInterpolation
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class CSharpSimplifyInterpolationDiagnosticAnalyzer : AbstractSimplifyInterpolationDiagnosticAnlayzer<
        InterpolationSyntax, ExpressionSyntax>
    {
    }
}
