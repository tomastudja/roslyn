﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.CodeAnalysis.ConvertLinq.ConvertForEachToLinqQuery
{
    internal abstract class AbstractConvertForEachToLinqQueryProvider<TForEachStatement, TStatement> : CodeRefactoringProvider
        where TForEachStatement : TStatement
        where TStatement : SyntaxNode
    {
        /// <summary>
        /// Parses the forEachStatement until a child node cannot be converted into a query clause.
        /// </summary>
        /// <param name="forEachStatement"></param>
        /// <returns>
        /// 1) extended nodes that can be converted into clauses
        /// 2) identifiers introduced in nodes that can be converted
        /// 3) statements that cannot be converted into clauses
        /// 4) trailing comments to be added to the end
        /// </returns>
        protected abstract ForEachInfo<TForEachStatement, TStatement> CreateForEachInfo(
            TForEachStatement forEachStatement, SemanticModel semanticModel);

        /// <summary>
        /// Tries to build a specific converter that covers e.g. Count, ToList, yield return or other similar cases.
        /// </summary>
        protected abstract bool TryBuildSpecificConverter(
            ForEachInfo<TForEachStatement, TStatement> forEachInfo,
            SemanticModel semanticModel,
            TStatement statementCannotBeConverted,
            CancellationToken cancellationToken,
            out IConverter<TForEachStatement, TStatement> converter);

        /// <summary>
        /// Creates a default converter where foreach is joined with some children statements but other children statements are kept unmodified.
        /// Example:
        /// foreach(...)
        /// {
        ///     if (condition)
        ///     {
        ///        doSomething();
        ///     }
        /// }
        /// is converted to
        /// foreach(... where condition)
        /// {
        ///        doSomething(); 
        /// }
        /// </summary>
        protected abstract IConverter<TForEachStatement, TStatement> CreateDefaultConverter(
            ForEachInfo<TForEachStatement, TStatement> forEachInfo);

        protected abstract SyntaxNode AddLinqUsing(
            IConverter<TForEachStatement, TStatement> converter, SemanticModel semanticModel, SyntaxNode root);

        public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var document = context.Document;
            var cancellationToken = context.CancellationToken;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var forEachStatement = root.FindNode(context.Span) as TForEachStatement;
            if (forEachStatement == null)
            {
                return;
            }

            if (forEachStatement.ContainsDiagnostics)
            {
                return;
            }

            // Do not try to refactor queries with conditional compilation in them.
            if (forEachStatement.ContainsDirectives)
            {
                return;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (!TryBuildConverter(forEachStatement, semanticModel, cancellationToken, out var converter))
            {
                return;
            }

            if (semanticModel.GetDiagnostics(forEachStatement.Span, cancellationToken)
                .Any(diagnostic => diagnostic.DefaultSeverity == DiagnosticSeverity.Error))
            {
                return;
            }

            context.RegisterRefactoring(
                new ForEachToLinqQueryCodeAction(
                    FeaturesResources.Convert_to_query, 
                    c => ApplyConversion(converter, document, c)));
        }

        private Task<Document> ApplyConversion(
            IConverter<TForEachStatement, TStatement> converter, 
            Document document, 
            CancellationToken cancellationToken)
        {
            var editor = new SyntaxEditor(converter.ForEachInfo.SemanticModel.SyntaxTree.GetRoot(cancellationToken), document.Project.Solution.Workspace);
            converter.Convert(editor, cancellationToken);
            var newRoot = editor.GetChangedRoot();
            var rootWithLinqUsing = AddLinqUsing(converter, converter.ForEachInfo.SemanticModel, newRoot);
            return Task.FromResult(document.WithSyntaxRoot(rootWithLinqUsing));
        }

        /// <summary>
        /// Tries to build either a specific or a default converter.
        /// </summary>
        private bool TryBuildConverter(
            TForEachStatement forEachStatement,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            out IConverter<TForEachStatement, TStatement> converter)
        {
            var forEachInfo = CreateForEachInfo(forEachStatement, semanticModel);

            if (forEachInfo.Statements.Length == 1 &&
                TryBuildSpecificConverter(forEachInfo, semanticModel, forEachInfo.Statements.Single(), cancellationToken, out converter))
            {
                return true;
            }

            // No sense to convert a single foreach to foreach over the same collection.
            if (forEachInfo.ConvertingExtendedNodes.Length >= 1)
            {
                converter = CreateDefaultConverter(forEachInfo);
                return true;
            }

            converter = default;
            return false;
        }

        private class ForEachToLinqQueryCodeAction : CodeAction.DocumentChangeAction
        {
            public ForEachToLinqQueryCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument)
                : base(title, createChangedDocument)
            {
            }
        }
    }
}
