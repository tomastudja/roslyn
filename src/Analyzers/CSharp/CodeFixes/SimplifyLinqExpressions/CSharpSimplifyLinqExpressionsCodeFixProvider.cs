﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeAnalysis.CSharp.SimplifyLinqExpressions
{
    internal sealed class CSharpSimplifyLinqExpressionsCodeFixProvider : SyntaxEditorBasedCodeFixProvider
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]

        public CSharpSimplifyLinqExpressionsCodeFixProvider()
        {
        }
        public sealed override ImmutableArray<string> FixableDiagnosticIds
           => ImmutableArray.Create(IDEDiagnosticIds.SimplifyLinqExpressionsDiagnosticId);

        internal override CodeFixCategory CodeFixCategory => CodeFixCategory.CodeQuality;

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            context.RegisterCodeFix(new MyCodeAction(
                c => FixAsync(context.Document, context.Diagnostics.First(), c)), context.Diagnostics);
            return Task.CompletedTask;
        }

        protected override async Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CancellationToken cancellationToken)
        {
            var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            foreach (var diagnostic in diagnostics)
            {
                var node = editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan);
                RemoveWhere(model, editor, node);
            }
        }

        private static void RemoveWhere(SemanticModel model, SyntaxEditor editor, SyntaxNode node)
        {
            if (!node.IsKind(SyntaxKind.InvocationExpression))
            {
                var memberAccess = (MemberAccessExpressionSyntax)node;

                // Retrieve the lambda expression from the node
                var lambda = ((InvocationExpressionSyntax)memberAccess.Expression).ArgumentList;

                // Get the data or object the query is being called on
                var t = model.GetOperation(memberAccess.Expression) as Operations.IInvocationOperation;
                var r = model.GetOperation(memberAccess.Expression).Children;
                var y = model.GetOperation(memberAccess.Expression).Children.FirstOrDefault();
                var x = model.GetOperation(memberAccess.Expression).Children.FirstOrDefault().Syntax;
                var objectNodeSyntax = model.GetOperation(memberAccess.Expression).Children.FirstOrDefault().Syntax;
                SyntaxNode newNode;
                if (objectNodeSyntax.IsKind(SyntaxKind.InvocationExpression) || objectNodeSyntax.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                {
                    newNode = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    (ExpressionSyntax)objectNodeSyntax, memberAccess.Name))
                                           .WithArgumentList(lambda);
                }
                else
                {
                    newNode = SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(((IdentifierNameSyntax)objectNodeSyntax).Identifier.Text),
                                        SyntaxFactory.IdentifierName(memberAccess.Name.Identifier.Text)))
                                .WithArgumentList(lambda);
                };
                editor.ReplaceNode(node.Parent, newNode);
            }
        }

        private class MyCodeAction : CustomCodeActions.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument)
                : base(AnalyzersResources.Simplify_collection_initialization, createChangedDocument, AnalyzersResources.Simplify_collection_initialization)
            {
            }
        }
    }
}
