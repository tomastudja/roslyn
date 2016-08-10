﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;

namespace Microsoft.CodeAnalysis.CodeFixes.Async
{
    internal abstract partial class AbstractAddAsyncAwaitCodeFixProvider : AbstractAsyncCodeFix
    {
        protected abstract Task<IList<DescriptionAndNode>> GetDescriptionsAndNodesAsync(
            SyntaxNode root, SyntaxNode oldNode, SemanticModel semanticModel, Diagnostic diagnostic, Document document, CancellationToken cancellationToken);

        protected override async Task<IList<CodeAction>> GetCodeActionsAsync(
            SyntaxNode root, SyntaxNode node, Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var data = await this.GetDescriptionsAndNodesAsync(root, node, semanticModel, diagnostic, document, cancellationToken).ConfigureAwait(false);
            if (data == null)
            {
                return null;
            }

            var result = new List<CodeAction>();

            foreach (var item in data)
            {
                var action = new MyCodeAction(
                    item.Description,
                    c => Task.FromResult(document.WithSyntaxRoot(item.Node)));
                result.Add(action);
            }

            return result;
        }

        protected static bool TryGetExpressionType(
            SyntaxNode expression,
            SemanticModel semanticModel,
            out INamedTypeSymbol returnType)
        {
            var typeInfo = semanticModel.GetTypeInfo(expression);
            returnType = typeInfo.Type as INamedTypeSymbol;
            return returnType != null;
        }

        protected static bool TryGetTaskType(SemanticModel semanticModel, out INamedTypeSymbol taskType)
        {
            var compilation = semanticModel.Compilation;
            taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            return taskType != null;
        }

        private class MyCodeAction : CodeAction.DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument) :
                base(title, createChangedDocument)
            {
            }
        }

        protected struct DescriptionAndNode
        {
            public readonly string Description;
            public readonly SyntaxNode Node;

            public DescriptionAndNode(string description, SyntaxNode node)
            {
                Description = description;
                Node = node;
            }
        }
    }
}