﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.UseCollectionInitializer;

namespace Microsoft.CodeAnalysis.CSharp.UseCollectionInitializer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class CSharpUseCollectionInitializerDiagnosticAnalyzer :
        AbstractUseCollectionInitializerDiagnosticAnalyzer<
            SyntaxKind,
            ExpressionSyntax,
            StatementSyntax,
            ObjectCreationExpressionSyntax,
            MemberAccessExpressionSyntax,
            InvocationExpressionSyntax,
            ExpressionStatementSyntax,
            VariableDeclaratorSyntax>
    {
        protected override bool FadeOutOperatorToken => true;

        protected override SyntaxKind GetObjectCreationSyntaxKind() => SyntaxKind.ObjectCreationExpression;

        protected override ISyntaxFactsService GetSyntaxFactsService() => CSharpSyntaxFactsService.Instance;
    }
}