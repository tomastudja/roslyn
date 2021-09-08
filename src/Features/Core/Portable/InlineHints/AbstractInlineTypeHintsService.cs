﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.InlineHints
{
    internal abstract class AbstractInlineTypeHintsService : IInlineTypeHintsService
    {
        private static readonly SymbolDisplayFormat s_minimalTypeStyle = new SymbolDisplayFormat(
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        protected abstract TypeHint? TryGetTypeHint(
            SemanticModel semanticModel, SyntaxNode node,
            bool displayAllOverride,
            bool forImplicitVariableTypes,
            bool forLambdaParameterTypes,
            bool forImplicitObjectCreation,
            CancellationToken cancellationToken);

        public async Task<ImmutableArray<InlineHint>> GetInlineHintsAsync(
            Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            var displayAllOverride = options.GetOption(InlineHintsOptions.DisplayAllOverride);
            var enabledForTypes = options.GetOption(InlineHintsOptions.EnabledForTypes);
            if (!enabledForTypes && !displayAllOverride)
                return ImmutableArray<InlineHint>.Empty;

            var forImplicitVariableTypes = enabledForTypes && options.GetOption(InlineHintsOptions.ForImplicitVariableTypes);
            var forLambdaParameterTypes = enabledForTypes && options.GetOption(InlineHintsOptions.ForLambdaParameterTypes);
            var forImplicitObjectCreation = enabledForTypes && options.GetOption(InlineHintsOptions.ForImplicitObjectCreation);
            if (!forImplicitVariableTypes && !forLambdaParameterTypes && !forImplicitObjectCreation && !displayAllOverride)
                return ImmutableArray<InlineHint>.Empty;

            var anonymousTypeService = document.GetRequiredLanguageService<IAnonymousTypeDisplayService>();
            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            using var _1 = ArrayBuilder<InlineHint>.GetInstance(out var result);

            foreach (var node in root.DescendantNodes(n => n.Span.IntersectsWith(textSpan)))
            {
                var hintOpt = TryGetTypeHint(
                    semanticModel, node,
                    displayAllOverride,
                    forImplicitVariableTypes,
                    forLambdaParameterTypes,
                    forImplicitObjectCreation,
                    cancellationToken);
                if (hintOpt == null)
                    continue;

                var (type, span, insertSpan, prefix, suffix) = hintOpt.Value;

                using var _2 = ArrayBuilder<SymbolDisplayPart>.GetInstance(out var finalParts);
                finalParts.AddRange(prefix);

                var parts = type.ToDisplayParts(s_minimalTypeStyle);
                AddParts(anonymousTypeService, finalParts, parts, semanticModel, span.Start);

                // If we have nothing to show, then don't bother adding this hint.
                if (finalParts.All(p => string.IsNullOrWhiteSpace(p.ToString())))
                    continue;

                finalParts.AddRange(suffix);
                var taggedText = finalParts.ToTaggedText();
                var displayString = GetDisplayStringFromParts(taggedText);

                var textChange = insertSpan is null
                    ? (TextChange?)null
                    : new TextChange(insertSpan.Value, displayString);

                result.Add(new InlineHint(
                    span, taggedText, textChange,
                    InlineHintHelpers.GetDescriptionFunction(span.Start, type.GetSymbolKey(cancellationToken: cancellationToken))));
            }

            return result.ToImmutable();
        }

        private static string GetDisplayStringFromParts(ImmutableArray<TaggedText> taggedTexts)
        {
            var displayString = PooledStringBuilder.GetInstance();
            if (taggedTexts.Length == 1)
            {
                var first = taggedTexts.First();
                var trimBoth = first.Text.Trim();
                return trimBoth;
            }
            else if (taggedTexts.Length >= 2)
            {
                var first = taggedTexts.First();
                var trimStart = first.Text.TrimStart();
                displayString.Builder.Append(trimStart);

                for (var i = 1; i < taggedTexts.Length - 1; i++)
                {
                    displayString.Builder.Append(taggedTexts[i].Text);
                }

                var last = taggedTexts.Last();
                var trimEnd = last.Text.TrimEnd();
                displayString.Builder.Append(trimEnd);
            }

            return displayString.ToStringAndFree();
        }

        private void AddParts(
            IAnonymousTypeDisplayService anonymousTypeService,
            ArrayBuilder<SymbolDisplayPart> finalParts,
            ImmutableArray<SymbolDisplayPart> parts,
            SemanticModel semanticModel,
            int position,
            HashSet<INamedTypeSymbol>? seenSymbols = null)
        {
            seenSymbols ??= new();

            foreach (var part in parts)
            {
                if (part.Symbol is INamedTypeSymbol { IsAnonymousType: true } anonymousType)
                {
                    if (seenSymbols.Add(anonymousType))
                    {
                        var anonymousParts = anonymousTypeService.GetAnonymousTypeParts(anonymousType, semanticModel, position);
                        AddParts(anonymousTypeService, finalParts, anonymousParts, semanticModel, position, seenSymbols);
                        seenSymbols.Remove(anonymousType);
                    }
                    else
                    {
                        finalParts.Add(new SymbolDisplayPart(SymbolDisplayPartKind.Text, symbol: null, "..."));
                    }
                }
                else
                {
                    finalParts.Add(part);
                }
            }
        }
    }
}
