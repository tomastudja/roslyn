﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Completion.Providers;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Completion.Providers
{
    [ExportCompletionProvider(nameof(UnnamedSymbolCompletionProvider), LanguageNames.CSharp), Shared]
    [ExtensionOrder(After = nameof(SymbolCompletionProvider))]
    internal partial class UnnamedSymbolCompletionProvider : LSPCompletionProvider
    {
        /// <summary>
        /// CompletionItems for indexers/operators should be sorted below other suggestions like methods or properties
        /// of the type.  We accomplish this by placing a character known to be greater than all other normal identifier
        /// characters as the start of our item's name. this doesn't affect what we insert though as all derived
        /// providers have specialized logic for what they need to do.
        /// </summary> 
        private const string SortingPrefix = "\uFFFD";

        /// <summary>
        /// Used to store what sort of unnamed symbol a completion item represents.
        /// </summary>
        internal const string KindName = "Kind";
        internal const string IndexerKindName = "Indexer";
        internal const string OperatorKindName = "Operator";
        internal const string ConversionKindName = "Conversion";

        /// <summary>
        /// Used to store the doc comment for some operators/conversions.  This is because some of them will be
        /// synthesized, so there will be no symbol we can recover after the fact in <see cref="GetDescriptionAsync"/>.
        /// </summary>
        private const string DocumentationCommentXmlName = "DocumentationCommentXml";

        [ImportingConstructor]
        [System.Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public UnnamedSymbolCompletionProvider()
        {
        }

        public override ImmutableHashSet<char> TriggerCharacters => ImmutableHashSet.Create('.');

        public override bool IsInsertionTrigger(SourceText text, int insertedCharacterPosition, OptionSet options)
            => text[insertedCharacterPosition] == '.';

        /// <summary>
        /// We keep operators sorted in a specific order.  We don't want to sort them alphabetically, but instead want
        /// to keep things like <c>==</c> and <c>!=</c> together.
        /// </summary>
        private static string SortText(int sortingGroupIndex, string sortTextSymbolPart)
            => $"{SortingPrefix}{sortingGroupIndex:000}_{sortTextSymbolPart}";

        /// <summary>
        /// Gets the relevant tokens and expression of interest surrounding the immediately preceding <c>.</c> (dot).
        /// </summary>
        private static (SyntaxToken dotLikeToken, ExpressionSyntax expression) GetDotAndExpression(SyntaxNode root, int position)
        {
            var tokenOnLeft = root.FindTokenOnLeftOfPosition(position, includeSkipped: true);
            var dotToken = tokenOnLeft.GetPreviousTokenIfTouchingWord(position);

            if (!CompletionUtilities.TreatAsDot(dotToken, position - 1))
                return default;

            ExpressionSyntax? expression;
            if (dotToken.Kind() == SyntaxKind.DotToken)
            {
                expression = dotToken.Parent as ExpressionSyntax;
            }
            else if (dotToken.Kind() == SyntaxKind.DotDotToken)
            {
                expression = (dotToken.Parent as RangeExpressionSyntax)?.LeftOperand;
            }
            else
            {
                return default;
            }

            if (expression == null)
                return default;

            // don't want to trigger after a number.  All other cases after dot are ok.
            if (dotToken.GetPreviousToken().Kind() == SyntaxKind.NumericLiteralToken)
                return default;

            return (dotToken, expression);
        }

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            var cancellationToken = context.CancellationToken;
            var document = context.Document;
            var position = context.Position;

            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var dotAndExpr = GetDotAndExpression(root, position);
            if (dotAndExpr == default)
                return;

            var recommender = document.GetRequiredLanguageService<IRecommendationService>();

            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
            var recommendedSymbols = recommender.GetRecommendedSymbolsAtPosition(document.Project.Solution.Workspace, semanticModel, position, options, cancellationToken);

            AddUnnamedSymbols(context, position, semanticModel, recommendedSymbols.UnnamedSymbols, cancellationToken);
        }

        private void AddUnnamedSymbols(
            CompletionContext context, int position, SemanticModel semanticModel, ImmutableArray<ISymbol> unnamedSymbols, CancellationToken cancellationToken)
        {
            var indexers = unnamedSymbols.WhereAsArray(s => s.IsIndexer());

            // Add one 'this[]' entry for all the indexers this type may have.
            AddIndexers(context, indexers);

            // Group all the related operators and add a single completion entry per group.
            var operators = unnamedSymbols.WhereAsArray(s => s.IsUserDefinedOperator());
            var operatorGroups = operators.GroupBy(op => op.Name);

            foreach (var opGroup in operatorGroups)
                AddOperatorGroup(context, opGroup.Key, opGroup);

            foreach (var symbol in unnamedSymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (symbol.IsConversion())
                    AddConversion(context, semanticModel, position, (IMethodSymbol)symbol);
            }
        }

        internal override Task<CompletionChange> GetChangeAsync(
            Document document,
            CompletionItem item,
            TextSpan completionListSpan,
            char? commitKey,
            bool disallowAddingImports,
            CancellationToken cancellationToken)
        {
            var properties = item.Properties;
            var kind = properties[KindName];
            return kind switch
            {
                IndexerKindName => GetIndexerChangeAsync(document, item, cancellationToken),
                OperatorKindName => GetOperatorChangeAsync(document, item, cancellationToken),
                ConversionKindName => GetConversionChangeAsync(document, item, cancellationToken),
                _ => throw ExceptionUtilities.UnexpectedValue(kind),
            };
        }

        public override async Task<CompletionDescription?> GetDescriptionAsync(
            Document document,
            CompletionItem item,
            CancellationToken cancellationToken)
        {
            var properties = item.Properties;
            var kind = properties[KindName];
            return kind switch
            {
                IndexerKindName => await GetIndexerDescriptionAsync(document, item, cancellationToken).ConfigureAwait(false),
                OperatorKindName => await GetOperatorDescriptionAsync(document, item, cancellationToken).ConfigureAwait(false),
                ConversionKindName => await GetConversionDescriptionAsync(document, item, cancellationToken).ConfigureAwait(false),
                _ => throw ExceptionUtilities.UnexpectedValue(kind),
            };
        }

        private static async Task<CompletionChange> ReplaceDotAndTokenAfterWithTextAsync(
            Document document,
            CompletionItem item,
            string text,
            bool removeConditionalAccess,
            int positionOffset,
            CancellationToken cancellationToken)
        {
            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var position = SymbolCompletionItem.GetContextPosition(item);

            var (dotToken, _) = GetDotAndExpression(root, position);

            var replacementStart = GetReplacementStart(removeConditionalAccess, dotToken);
            var newPosition = replacementStart + text.Length + positionOffset;

            var tokenOnLeft = root.FindTokenOnLeftOfPosition(position, includeSkipped: true);
            return CompletionChange.Create(
                new TextChange(TextSpan.FromBounds(replacementStart, tokenOnLeft.Span.End), text),
                newPosition);
        }

        private static int GetReplacementStart(bool removeConditionalAccess, SyntaxToken dotToken)
        {
            var replacementStart = dotToken.SpanStart;
            if (removeConditionalAccess)
            {
                if (dotToken.Parent is MemberBindingExpressionSyntax memberBinding &&
                    memberBinding.GetParentConditionalAccessExpression() is { } conditional)
                {
                    replacementStart = conditional.OperatorToken.SpanStart;
                }
            }

            return replacementStart;
        }
    }
}
