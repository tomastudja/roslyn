﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.ConvertAnonymousTypeToTuple
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpConvertAnonymousTypeToTupleCodeFixProvider)), Shared]
    internal class CSharpConvertAnonymousTypeToTupleCodeFixProvider : SyntaxEditorBasedCodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(IDEDiagnosticIds.ConvertAnonymousTypeToTupleDiagnosticId);

        public override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            context.RegisterCodeFix(
                new MyCodeAction(c => FixAllWithEditorAsync(context.Document,
                    e => FixInCurrentMember(context.Document, e, context.Diagnostics[0], c), c)),
                context.Diagnostics);

            return SpecializedTasks.EmptyTask;
        }

        private async Task FixInCurrentMember(
            Document document, SyntaxEditor editor,
            Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var creationNode = TryGetCreationNode(diagnostic, cancellationToken);
            if (creationNode == null)
            {
                Debug.Fail("We should always be able to find the anonymous creation we were invoked from.");
                return;
            }

            var anonymousType = semanticModel.GetTypeInfo(creationNode, cancellationToken).Type;
            if (anonymousType == null)
            {
                Debug.Fail("We should always be able to get an anonymous type for any anonymous creation node.");
                return;
            }

            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();
            var containingMember = creationNode.FirstAncestorOrSelf<SyntaxNode>(syntaxFacts.IsMethodLevelMember) ?? creationNode;

            var childCreationNodes = containingMember.DescendantNodesAndSelf()
                                                     .OfType<AnonymousObjectCreationExpressionSyntax>();
            foreach (var childCreation in childCreationNodes)
            {
                var childType = semanticModel.GetTypeInfo(childCreation, cancellationToken).Type;
                if (childType == null)
                {
                    Debug.Fail("We should always be able to get an anonymous type for any anonymous creation node.");
                    continue;
                }

                if (anonymousType.Equals(childType))
                {
                    ReplaceWithTuple(editor, childCreation);
                }
            }
        }

        protected override Task FixAllAsync(
            Document document, ImmutableArray<Diagnostic> diagnostics,
            SyntaxEditor editor, CancellationToken cancellationToken)
        {
            foreach (var diagnostic in diagnostics)
            {
                // We're doing a fix all, so we only have to fix this specific diagnostic.
                // Any other anonymous types will have their own diagnostic and can be 
                // fixed up.
                FixOne(editor, diagnostic, cancellationToken);
            }

            return SpecializedTasks.EmptyTask;
        }

        private void FixOne(
            SyntaxEditor editor, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var node = TryGetCreationNode(diagnostic, cancellationToken);
            if (node == null)
            {
                return;
            }

            ReplaceWithTuple(editor, node);
        }

        private static void ReplaceWithTuple(SyntaxEditor editor, AnonymousObjectCreationExpressionSyntax node)
            => editor.ReplaceNode(
                node, (current, _) =>
                {
                    var anonCreation = current as AnonymousObjectCreationExpressionSyntax;
                    if (anonCreation == null)
                    {
                        return current;
                    }

                    return ConvertToTuple(anonCreation).WithAdditionalAnnotations(Formatter.Annotation);
                });

        private static AnonymousObjectCreationExpressionSyntax TryGetCreationNode(Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            return diagnostic.Location.FindToken(cancellationToken).Parent as AnonymousObjectCreationExpressionSyntax;
        }

        private static TupleExpressionSyntax ConvertToTuple(AnonymousObjectCreationExpressionSyntax anonCreation)
            => SyntaxFactory.TupleExpression(
                    SyntaxFactory.Token(SyntaxKind.OpenParenToken).WithTriviaFrom(anonCreation.OpenBraceToken),
                    ConvertInitializers(anonCreation.Initializers),
                    SyntaxFactory.Token(SyntaxKind.CloseParenToken).WithTriviaFrom(anonCreation.CloseBraceToken))
                            .WithPrependedLeadingTrivia(anonCreation.GetLeadingTrivia());

        private static SeparatedSyntaxList<ArgumentSyntax> ConvertInitializers(
            SeparatedSyntaxList<AnonymousObjectMemberDeclaratorSyntax> initializers)
        {
            return SyntaxFactory.SeparatedList(
                initializers.Select(ConvertInitializer),
                initializers.GetSeparators());
        }

        private static ArgumentSyntax ConvertInitializer(AnonymousObjectMemberDeclaratorSyntax declarator)
            => SyntaxFactory.Argument(ConvertName(declarator.NameEquals), default, declarator.Expression)
                            .WithTriviaFrom(declarator);

        private static NameColonSyntax ConvertName(NameEqualsSyntax nameEquals)
            => nameEquals == null
                ? null
                : SyntaxFactory.NameColon(
                    nameEquals.Name,
                    SyntaxFactory.Token(SyntaxKind.ColonToken).WithTriviaFrom(nameEquals.EqualsToken));

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(Func<CancellationToken, Task<Document>> createChangedDocument)
                : base(FeaturesResources.Convert_anonymous_type_to_tuple, createChangedDocument, FeaturesResources.Convert_anonymous_type_to_tuple)
            {
            }
        }
    }
}
