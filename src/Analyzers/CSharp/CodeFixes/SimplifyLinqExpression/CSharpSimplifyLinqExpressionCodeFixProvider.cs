﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.SimplifyLinqExpression;

namespace Microsoft.CodeAnalysis.CSharp.SimplifyLinqExpression
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    internal sealed class CSharpSimplifyLinqExpressionCodeFixProvider : AbstractSimplifyLinqExpressionCodeFixProvider<InvocationExpressionSyntax, SimpleNameSyntax, ExpressionSyntax>
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public CSharpSimplifyLinqExpressionCodeFixProvider()
        {
        }

        protected override SyntaxNode[] GetArguments(SyntaxNode argument)
            // this is handling both the Enumerable.Where(source, filter) case and the source.Where(filter) case
            => argument.Parent?.Parent?.Parent is ArgumentListSyntax argumentList
                ? argumentList.Arguments.ToArray()
                : new[] { argument };

        protected override ExpressionSyntax GetExpression(ExpressionSyntax expression)
            => ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)expression).Expression).Expression;

        protected override SimpleNameSyntax GetName(InvocationExpressionSyntax invocationExpression)
            => invocationExpression.Expression.GetRightmostName();
    }
}
