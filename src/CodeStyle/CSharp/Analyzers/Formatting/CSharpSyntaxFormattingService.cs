﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    internal class CSharpSyntaxFormattingService : AbstractSyntaxFormattingService
    {
        private readonly ImmutableList<IFormattingRule> _rules;

        public CSharpSyntaxFormattingService()
        {
            _rules = ImmutableList.Create<IFormattingRule>(
                new WrappingFormattingRule(),
                new SpacingFormattingRule(),
                new NewLineUserSettingFormattingRule(),
                new IndentUserSettingsFormattingRule(),
                new ElasticTriviaFormattingRule(),
                new EndOfFileTokenFormattingRule(),
                new StructuredTriviaFormattingRule(),
                new IndentBlockFormattingRule(),
                new SuppressFormattingRule(),
                new AnchorIndentationFormattingRule(),
                new QueryExpressionFormattingRule(),
                new TokenBasedFormattingRule(),
                new DefaultOperationProvider());
        }

        public override IEnumerable<IFormattingRule> GetDefaultFormattingRules()
        {
            return _rules;
        }

        protected override IFormattingResult CreateAggregatedFormattingResult(SyntaxNode node, IList<AbstractFormattingResult> results, SimpleIntervalTree<TextSpan> formattingSpans = null)
        {
            return new AggregatedFormattingResult(node, results, formattingSpans);
        }

        protected override AbstractFormattingResult Format(SyntaxNode node, OptionSet optionSet, IEnumerable<IFormattingRule> formattingRules, SyntaxToken token1, SyntaxToken token2, CancellationToken cancellationToken)
        {
            return new CSharpFormatEngine(node, optionSet, formattingRules, token1, token2).Format(cancellationToken);
        }
    }
}
