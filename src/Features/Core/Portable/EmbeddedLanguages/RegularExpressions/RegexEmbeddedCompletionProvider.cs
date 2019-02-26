﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Completion.Providers;
using Microsoft.CodeAnalysis.EmbeddedLanguages.Common;
using Microsoft.CodeAnalysis.EmbeddedLanguages.RegularExpressions;
using Microsoft.CodeAnalysis.EmbeddedLanguages.VirtualChars;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Features.EmbeddedLanguages.RegularExpressions
{
    using static WorkspacesResources;
    using RegexToken = EmbeddedSyntaxToken<RegexKind>;

    internal partial class RegexEmbeddedCompletionProvider : CompletionProvider
    {
        private const string StartKey = nameof(StartKey);
        private const string LengthKey = nameof(LengthKey);
        private const string NewTextKey = nameof(NewTextKey);
        private const string NewPositionKey = nameof(NewPositionKey);
        private const string DescriptionKey = nameof(DescriptionKey);

        // Always soft-select these completion items.  Also, never filter down.
        private static readonly CompletionItemRules s_rules =
            CompletionItemRules.Default.WithSelectionBehavior(CompletionItemSelectionBehavior.SoftSelection)
                                       .WithFilterCharacterRule(CharacterSetModificationRule.Create(CharacterSetModificationKind.Replace, new char[] { }));

        private readonly RegexEmbeddedLanguageFeatures _language;

        public RegexEmbeddedCompletionProvider(RegexEmbeddedLanguageFeatures language)
        {
            _language = language;
        }

        public override bool ShouldTriggerCompletion(SourceText text, int caretPosition, CompletionTrigger trigger, OptionSet options)
        {
            if (trigger.Kind == CompletionTriggerKind.Invoke ||
                trigger.Kind == CompletionTriggerKind.InvokeAndCommitIfUnique)
            {
                return true;
            }

            if (trigger.Kind == CompletionTriggerKind.Insertion)
            {
                return IsTriggerCharacter(trigger.Character);
            }

            return false;
        }

        private bool IsTriggerCharacter(char ch)
        {
            switch (ch)
            {
                case '\\': // any escape
                case '[':  // character class
                case '(':  // any group
                case '{':  // \p{
                    return true;
            }

            return false;
        }

        public override async Task ProvideCompletionsAsync(CompletionContext context)
        {
            if (!context.Options.GetOption(RegularExpressionsOptions.ProvideRegexCompletions, context.Document.Project.Language))
            {
                return;
            }

            if (context.Trigger.Kind != CompletionTriggerKind.Invoke &&
                context.Trigger.Kind != CompletionTriggerKind.InvokeAndCommitIfUnique &&
                context.Trigger.Kind != CompletionTriggerKind.Insertion)
            {
                return;
            }

            var position = context.Position;
            var (tree, stringToken) = await _language.TryGetTreeAndTokenAtPositionAsync(
                context.Document, position, context.CancellationToken).ConfigureAwait(false);

            if (tree == null ||
                position <= stringToken.SpanStart ||
                position >= stringToken.Span.End)
            {
                return;
            }

            var embeddedContext = new EmbeddedCompletionContext(this, context, tree, stringToken);
            ProvideCompletions(embeddedContext);

            if (embeddedContext.Items.Count == 0)
            {
                return;
            }

            foreach (var embeddedItem in embeddedContext.Items)
            {
                var change = embeddedItem.Change;
                var textChange = change.TextChange;

                var properties = ImmutableDictionary.CreateBuilder<string, string>();
                properties.Add(StartKey, textChange.Span.Start.ToString());
                properties.Add(LengthKey, textChange.Span.Length.ToString());
                properties.Add(NewTextKey, textChange.NewText);
                properties.Add(DescriptionKey, embeddedItem.FullDescription);
                properties.Add(EmbeddedLanguageCompletionProvider.EmbeddedProviderName, this.Name);

                if (change.NewPosition != null)
                {
                    properties.Add(NewPositionKey, change.NewPosition.ToString());
                }

                // Keep everything sorted in the order we just produced the items in.
                var sortText = context.Items.Count.ToString("0000");
                context.AddItem(CompletionItem.Create(
                    displayText: embeddedItem.DisplayText,
                    sortText: sortText,
                    properties: properties.ToImmutable(),
                    rules: s_rules));
            }

            context.IsExclusive = true;
        }

        private void ProvideCompletions(EmbeddedCompletionContext context)
        {
            var position = context.Position;

            var tree = context.Tree;
            var stringToken = context.StringToken;

            // First, act as if the user just inserted the previous character.  This will cause us
            // to complete down to the set of relevant items based on that character. If we get
            // anything, we're done and can just show the user those items.  If we have no items to
            // add *and* the user was explicitly invoking completion, then just add the entire set
            // of suggestions to help the user out.
            var count = context.Items.Count;
            ProvideCompletionsAfterInsertion(context);

            if (count != context.Items.Count)
            {
                // We added items.  Nothing else to do here.
                return;
            }

            if (context.Trigger.Kind == CompletionTriggerKind.Insertion)
            {
                // The user was typing a character, and we had nothing to add for them.  Just bail
                // out immediately as we cannot help in this circumstance.
                return;
            }

            // We added no items, but the user explicitly asked for completion.  Add all the
            // items we can to help them out.
            var inCharacterClass = DetermineIfInCharacterClass(tree, context.Position);

            ProvideEscapeCompletions(context, inCharacterClass, parentOpt: null);

            if (!inCharacterClass)
            {
                ProvideTopLevelCompletions(context);
                ProvideCharacterClassCompletions(context, parentOpt: null);
                ProvideGroupingCompletions(context, parentOpt: null);
            }
        }

        private bool DetermineIfInCharacterClass(RegexTree tree, int pos)
        {
            var inCharacterClass = false;

            var virtualChar = tree.Text.FirstOrNullable(vc => vc.Span.Contains(pos));
            if (virtualChar != null)
            {
                inCharacterClass = IsInCharacterClass(tree.Root, virtualChar.Value, inCharacterClass: false);
            }

            return inCharacterClass;
        }

        private void ProvideTopLevelCompletions(EmbeddedCompletionContext context)
        {
            context.AddIfMissing("|", Regex_alternation_short, Regex_alternation_long, parentOpt: null);
            context.AddIfMissing("^", Regex_start_of_string_or_line_short, Regex_start_of_string_or_line_long, parentOpt: null);
            context.AddIfMissing("$", Regex_end_of_string_or_line_short, Regex_end_of_string_or_line_long, parentOpt: null);
            context.AddIfMissing(".", Regex_any_character_group_short, Regex_any_character_group_long, parentOpt: null);

            context.AddIfMissing("*", Regex_match_zero_or_more_times_short, Regex_match_zero_or_more_times_long, parentOpt: null);
            context.AddIfMissing("*?", Regex_match_zero_or_more_times_lazy_short, Regex_match_zero_or_more_times_lazy_long, parentOpt: null);

            context.AddIfMissing("+", Regex_match_one_or_more_times_short, Regex_match_one_or_more_times_long, parentOpt: null);
            context.AddIfMissing("+?", Regex_match_one_or_more_times_lazy_short, Regex_match_one_or_more_times_lazy_long, parentOpt: null);

            context.AddIfMissing("?", Regex_match_zero_or_one_time_short, Regex_match_zero_or_one_time_long, parentOpt: null);
            context.AddIfMissing("??", Regex_match_zero_or_one_time_lazy_short, Regex_match_zero_or_one_time_lazy_long, parentOpt: null);

            context.AddIfMissing("{n}", Regex_match_exactly_n_times_short, Regex_match_exactly_n_times_long, parentOpt: null, positionOffset: "{".Length, insertionText: "{}");
            context.AddIfMissing("{n}?", Regex_match_exactly_n_times_lazy_short, Regex_match_exactly_n_times_lazy_long, parentOpt: null, positionOffset: "{".Length, insertionText: "{}?");

            context.AddIfMissing("{n,}", Regex_match_at_least_n_times_short, Regex_match_at_least_n_times_long, parentOpt: null, positionOffset: "{".Length, insertionText: "{,}");
            context.AddIfMissing("{n,}?", Regex_match_at_least_n_times_lazy_short, Regex_match_at_least_n_times_lazy_long, parentOpt: null, positionOffset: "{".Length, insertionText: "{,}?");

            context.AddIfMissing("{m,n}", Regex_match_between_m_and_n_times_short, Regex_match_between_m_and_n_times_long, parentOpt: null, positionOffset: "{".Length, insertionText: "{,}");
            context.AddIfMissing("{m,n}?", Regex_match_between_m_and_n_times_lazy_short, Regex_match_between_m_and_n_times_lazy_long, parentOpt: null, positionOffset: "{".Length, insertionText: "{,}?");

            context.AddIfMissing("#", Regex_end_of_line_comment_short, Regex_end_of_line_comment_long, parentOpt: null);
        }

        private void ProvideCompletionsAfterInsertion(EmbeddedCompletionContext context)
        {
            var tree = context.Tree;
            var position = context.Position;
            var previousVirtualCharOpt = tree.Text.FirstOrNullable(vc => vc.Span.Contains(position - 1));
            if (previousVirtualCharOpt == null)
            {
                return;
            }

            var previousVirtualChar = previousVirtualCharOpt.Value;
            var result = FindToken(tree.Root, previousVirtualChar);
            if (result == null)
            {
                return;
            }

            var (parent, token) = result.Value;
            var inCharacterClass = IsInCharacterClass(tree.Root, previousVirtualChar, inCharacterClass: false);

            if (token.Kind == RegexKind.BackslashToken)
            {
                ProvideEscapeCompletions(context, inCharacterClass, parent);
                return;
            }

            // see if we have ```\p{```.  If so, offer property categories
            if (previousVirtualChar.Char == '{')
            {
                ProvideCompletionsIfInUnicodeCategory(context, tree, previousVirtualChar);
                return;
            }

            if (inCharacterClass)
            {
                // Nothing more to offer if we're in a character class.
                return;
            }

            switch (token.Kind)
            {
                case RegexKind.OpenBracketToken:
                    ProvideCharacterClassCompletions(context, parent);
                    return;
                case RegexKind.OpenParenToken:
                    ProvideGroupingCompletions(context, parent);
                    return;
            }
        }

        private void ProvideCompletionsIfInUnicodeCategory(
            EmbeddedCompletionContext context, RegexTree tree, VirtualChar previousVirtualChar)
        {
            var index = tree.Text.IndexOf(previousVirtualChar);
            if (index >= 2 &&
                tree.Text[index - 2].Char == '\\' &&
                tree.Text[index - 1].Char == 'p')
            {
                var slashChar = tree.Text[index - 1];
                var result = FindToken(tree.Root, slashChar);
                if (result == null)
                {
                    return;
                }

                var (parent, token) = result.Value;
                if (parent is RegexEscapeNode)
                {
                    ProvideEscapeCategoryCompletions(context);
                }
            }
        }

        private void ProvideGroupingCompletions(EmbeddedCompletionContext context, RegexNode parentOpt)
        {
            if (parentOpt != null && !(parentOpt is RegexGroupingNode))
            {
                return;
            }

            context.AddIfMissing($"(  {Regex_subexpression}  )", Regex_matched_subexpression_short, Regex_matched_subexpression_long, parentOpt, positionOffset: "(".Length, insertionText: "()");
            context.AddIfMissing($"(?<  {Regex_name}  >  {Regex_subexpression}  )", Regex_named_matched_subexpression_short, Regex_named_matched_subexpression_long, parentOpt, positionOffset: "(?<".Length, insertionText: "(?<>)");
            context.AddIfMissing($"(?<  {Regex_name1}  -  {Regex_name2}  >  {Regex_subexpression}  )", Regex_balancing_group_short, Regex_balancing_group_long, parentOpt, positionOffset: "(?<".Length, insertionText: "(?<->)");
            context.AddIfMissing($"(?:  {Regex_subexpression}  )", Regex_noncapturing_group_short, Regex_noncapturing_group_long, parentOpt, positionOffset: "(?:".Length, insertionText: "(?:)");
            context.AddIfMissing($"(?=  {Regex_subexpression}  )", Regex_zero_width_positive_lookahead_assertion_short, Regex_zero_width_positive_lookahead_assertion_long, parentOpt, positionOffset: "(?=".Length, insertionText: "(?=)");
            context.AddIfMissing($"(?!  {Regex_subexpression}  )", Regex_zero_width_negative_lookahead_assertion_short, Regex_zero_width_negative_lookahead_assertion_long, parentOpt, positionOffset: "(?!".Length, insertionText: "(?!)");
            context.AddIfMissing($"(?<=  {Regex_subexpression}  )", Regex_zero_width_positive_lookbehind_assertion_short, Regex_zero_width_positive_lookbehind_assertion_long, parentOpt, positionOffset: "(?<=".Length, insertionText: "(?<=)");
            context.AddIfMissing($"(?<!  {Regex_subexpression}  )", Regex_zero_width_negative_lookbehind_assertion_short, Regex_zero_width_negative_lookbehind_assertion_long, parentOpt, positionOffset: "(?<!".Length, insertionText: "(?<!)");
            context.AddIfMissing($"(?>  {Regex_subexpression}  )", Regex_nonbacktracking_subexpression_short, Regex_nonbacktracking_subexpression_long, parentOpt, positionOffset: "(?>".Length, insertionText: "(?>)");

            context.AddIfMissing($"(?(  {Regex_expression}  )  {Regex_yes}  |  {Regex_no}  )", Regex_conditional_expression_match_short, Regex_conditional_expression_match_long, parentOpt, positionOffset: "(?(".Length, insertionText: "(?()|)");
            context.AddIfMissing($"(?(  {Regex_name_or_number}  )  {Regex_yes}  |  {Regex_no}  )", Regex_conditional_group_match_short, Regex_conditional_group_match_long, parentOpt, positionOffset: "(?(".Length, insertionText: "(?()|)");

            context.AddIfMissing($"(?#  {Regex_comment}  )", Regex_inline_comment_short, Regex_inline_comment_long, parentOpt, positionOffset: "(?#".Length, insertionText: "(?#)");
            context.AddIfMissing($"(?imnsx-imnsx)", Regex_inline_options_short, Regex_inline_options_long, parentOpt, positionOffset: "(?".Length, insertionText: "(?)");
            context.AddIfMissing($"(?imnsx-imnsx:  {Regex_subexpression}  )", Regex_group_options_short, Regex_group_options_long, parentOpt, positionOffset: "(?".Length, insertionText: "(?:)");
        }

        private void ProvideCharacterClassCompletions(EmbeddedCompletionContext context, RegexNode parentOpt)
        {
            context.AddIfMissing($"[  {Regex_character_group}  ]", Regex_positive_character_group_short, Regex_positive_character_group_long, parentOpt, positionOffset: "[".Length, insertionText: "[]");
            context.AddIfMissing($"[  firstCharacter-lastCharacter  ]", Regex_positive_character_range_short, Regex_positive_character_range_long, parentOpt, positionOffset: "[".Length, insertionText: "[-]");
            context.AddIfMissing($"[^  {Regex_character_group}  ]", Regex_negative_character_group_short, Regex_negative_character_group_long, parentOpt, positionOffset: "[^".Length, insertionText: "[^]");
            context.AddIfMissing($"[^  firstCharacter-lastCharacter  ]", Regex_negative_character_group_short, Regex_negative_character_range_long, parentOpt, positionOffset: "[^".Length, insertionText: "[^-]");
            context.AddIfMissing($"[  {Regex_base_group}  -[  {Regex_excluded_group}  ]", Regex_character_class_subtraction_short, Regex_character_class_subtraction_long, parentOpt, positionOffset: "[".Length, insertionText: "[-[]]");
        }

        private void ProvideEscapeCategoryCompletions(EmbeddedCompletionContext context)
        {
            foreach (var (name, (shortDesc, longDesc)) in RegexCharClass.EscapeCategories)
            {
                var displayText = name;

                // There are some internal escape categories the regex engine has (like _xmlI).
                // Just filter out here so we only show the main documented regex categories.
                // Note: we still include those in RegexCharClass.EscapeCategories because we
                // don't want to report an error on code that does use these since the .net
                // regex engine will allow them.
                if (displayText.StartsWith("_"))
                {
                    continue;
                }

                var description = longDesc.Length > 0
                    ? longDesc
                    : string.Format(Regex_unicode_general_category_0, name);

                context.AddIfMissing(new RegexItem(
                    displayText, shortDesc, description,
                    change: CompletionChange.Create(
                        new TextChange(new TextSpan(context.Position, 0), name), newPosition: null)));
            }
        }

        private void ProvideEscapeCompletions(
            EmbeddedCompletionContext context, bool inCharacterClass, RegexNode parentOpt)
        {
            if (parentOpt != null && !(parentOpt is RegexEscapeNode))
            {
                return;
            }

            if (!inCharacterClass)
            {
                context.AddIfMissing(@"\A", Regex_start_of_string_only_short, Regex_start_of_string_only_long, parentOpt);
                context.AddIfMissing(@"\b", Regex_word_boundary_short, Regex_word_boundary_long, parentOpt);
                context.AddIfMissing(@"\B", Regex_non_word_boundary_short, Regex_non_word_boundary_long, parentOpt);
                context.AddIfMissing(@"\G", Regex_contiguous_matches_short, Regex_contiguous_matches_long, parentOpt);
                context.AddIfMissing(@"\z", Regex_end_of_string_only_short, Regex_end_of_string_only_long, parentOpt);
                context.AddIfMissing(@"\Z", Regex_end_of_string_or_before_ending_newline_short, Regex_end_of_string_or_before_ending_newline_long, parentOpt);

                context.AddIfMissing($@"\k<  {Regex_name_or_number}  >", Regex_named_backreference_short, Regex_named_backreference_long, parentOpt, @"\k<".Length, insertionText: @"\k<>");

                // Note: we intentionally do not add `\<>` to the list.  While supported by the 
                // .net regex engine, it is effectively deprecated and discouraged from use.  
                // Instead, it is recommended that `\k<>` is used instead.
                // 
                // context.AddIfMissing(@"\<>", "", "", parentOpt, @"\<".Length));

                context.AddIfMissing(@"\1-9", Regex_numbered_backreference_short, Regex_numbered_backreference_long, parentOpt, @"\".Length, @"\");
            }

            context.AddIfMissing(@"\a", Regex_bell_character_short, Regex_bell_character_long, parentOpt);
            context.AddIfMissing(@"\b", Regex_backspace_character_short, Regex_backspace_character_long, parentOpt);
            context.AddIfMissing(@"\e", Regex_escape_character_short, Regex_escape_character_long, parentOpt);
            context.AddIfMissing(@"\f", Regex_form_feed_character_short, Regex_form_feed_character_long, parentOpt);
            context.AddIfMissing(@"\n", Regex_new_line_character_short, Regex_new_line_character_long, parentOpt);
            context.AddIfMissing(@"\r", Regex_carriage_return_character_short, Regex_carriage_return_character_long, parentOpt);
            context.AddIfMissing(@"\t", Regex_tab_character_short, Regex_tab_character_long, parentOpt);
            context.AddIfMissing(@"\v", Regex_vertical_tab_character_short, Regex_vertical_tab_character_long, parentOpt);

            context.AddIfMissing(@"\x##", Regex_hexadecimal_escape_short, Regex_hexadecimal_escape_long, parentOpt, @"\x".Length, @"\x");
            context.AddIfMissing(@"\u####", Regex_unicode_escape_short, Regex_unicode_escape_long, parentOpt, @"\u".Length, @"\u");
            context.AddIfMissing(@"\cX", Regex_control_character_short, Regex_control_character_long, parentOpt, @"\c".Length, @"\c");

            context.AddIfMissing(@"\d", Regex_decimal_digit_character_short, Regex_decimal_digit_character_long, parentOpt);
            context.AddIfMissing(@"\D", Regex_non_digit_character_short, Regex_non_digit_character_long, parentOpt);
            context.AddIfMissing(@"\p{...}", Regex_unicode_category_short, Regex_unicode_category_long, parentOpt, @"\p".Length, @"\p");
            context.AddIfMissing(@"\P{...}", Regex_negative_unicode_category_short, Regex_negative_unicode_category_long, parentOpt, @"\P".Length, @"\P");
            context.AddIfMissing(@"\s", Regex_white_space_character_short, Regex_white_space_character_long, parentOpt);
            context.AddIfMissing(@"\S", Regex_non_white_space_character_short, Regex_non_white_space_character_long, parentOpt);
            context.AddIfMissing(@"\w", Regex_word_character_short, Regex_word_character_long, parentOpt);
            context.AddIfMissing(@"\W", Regex_non_word_character_short, Regex_non_word_character_long, parentOpt);
        }

        private RegexItem CreateItem(
            SyntaxToken stringToken, string displayText,
            string suffix, string description,
            EmbeddedCompletionContext context, RegexNode parentOpt,
            int? positionOffset, string insertionText)
        {
            var replacementStart = parentOpt != null
                ? parentOpt.GetSpan().Start
                : context.Position;

            var replacementSpan = TextSpan.FromBounds(replacementStart, context.Position);
            var newPosition = replacementStart + positionOffset;

            insertionText = insertionText ?? displayText;
            var escapedInsertionText = _language.EscapeText(insertionText, stringToken);

            if (escapedInsertionText != insertionText)
            {
                newPosition += escapedInsertionText.Length - insertionText.Length;
            }

            return new RegexItem(
                displayText, suffix, description,
                CompletionChange.Create(
                    new TextChange(replacementSpan, escapedInsertionText),
                    newPosition));
        }

        private (RegexNode parent, RegexToken Token)? FindToken(
            RegexNode parent, VirtualChar ch)
        {
            foreach (var child in parent)
            {
                if (child.IsNode)
                {
                    var result = FindToken(child.Node, ch);
                    if (result != null)
                    {
                        return result;
                    }
                }
                else
                {
                    if (child.Token.VirtualChars.Contains(ch))
                    {
                        return (parent, child.Token);
                    }
                }
            }

            return null;
        }

        private bool IsInCharacterClass(RegexNode parent, VirtualChar ch, bool inCharacterClass)
        {
            foreach (var child in parent)
            {
                if (child.IsNode)
                {
                    var result = IsInCharacterClass(child.Node, ch, inCharacterClass || parent is RegexBaseCharacterClassNode);
                    if (result)
                    {
                        return result;
                    }
                }
                else
                {
                    if (child.Token.VirtualChars.Contains(ch))
                    {
                        return inCharacterClass;
                    }
                }
            }

            return false;
        }

        public override Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey, CancellationToken cancellationToken)
        {
            // These values have always been added by us.
            var startString = item.Properties[StartKey];
            var lengthString = item.Properties[LengthKey];
            var newText = item.Properties[NewTextKey];

            // This value is optionally added in some cases and may not always be there.
            item.Properties.TryGetValue(NewPositionKey, out var newPositionString);

            return Task.FromResult(CompletionChange.Create(
                new TextChange(new TextSpan(int.Parse(startString), int.Parse(lengthString)), newText),
                newPositionString == null ? default(int?) : int.Parse(newPositionString)));
        }

        public override Task<CompletionDescription> GetDescriptionAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            if (!item.Properties.TryGetValue(DescriptionKey, out var description))
            {
                return SpecializedTasks.Default<CompletionDescription>();
            }

            return Task.FromResult(CompletionDescription.Create(
                ImmutableArray.Create(new TaggedText(TextTags.Text, description))));
        }
    }
}
