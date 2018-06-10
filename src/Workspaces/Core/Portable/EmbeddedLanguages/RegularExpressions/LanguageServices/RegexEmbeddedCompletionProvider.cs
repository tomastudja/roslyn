﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.EmbeddedLanguages.Common;
using Microsoft.CodeAnalysis.EmbeddedLanguages.LanguageServices;
using Microsoft.CodeAnalysis.EmbeddedLanguages.VirtualChars;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using System;

namespace Microsoft.CodeAnalysis.EmbeddedLanguages.RegularExpressions.LanguageServices
{
    using static WorkspacesResources;
    using RegexToken = EmbeddedSyntaxToken<RegexKind>;

    internal class RegexEmbeddedCompletionProvider : IEmbeddedCompletionProvider
    {
        private readonly RegexEmbeddedLanguage _language;

        public RegexEmbeddedCompletionProvider(RegexEmbeddedLanguage language)
        {
            _language = language;
        }

        public bool ShouldTriggerCompletion(SourceText text, int caretPosition, EmbeddedCompletionTrigger trigger, OptionSet options)
        {
            if (trigger.Kind == EmbeddedCompletionTriggerKind.Invoke ||
                trigger.Kind == EmbeddedCompletionTriggerKind.InvokeAndCommitIfUnique)
            {
                return true;
            }

            if (trigger.Kind == EmbeddedCompletionTriggerKind.Insertion)
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
                case '[':  
                case '^':  // character class
                case '(':  // any group
                case '?':  // (?
                case '<':  // (?<
                case '=':  // (?<=
                case '\'': // (?'
                case '!':  // (?<!
                case '{':  // \p{
                case '+': case '-':
                case 'i': case 'I':
                case 'm': case 'M':
                case 'n': case 'N':
                case 's': case 'S':
                case 'x': case 'X': // (?options
                    return true;
            }

            return false;
        }

        public async Task ProvideCompletionsAsync(EmbeddedCompletionContext context)
        {
            if (!context.Options.GetOption(RegularExpressionsOptions.ProvideRegexCompletions, context.Document.Project.Language))
            {
                return;
            }

            var position = context.Position;
            var treeAndStringToken = await _language.TryGetTreeAndTokenAtPositionAsync(
                context.Document, position, context.CancellationToken).ConfigureAwait(false);
            if (treeAndStringToken == null)
            {
                return;
            }

            if (context.Trigger.Kind != EmbeddedCompletionTriggerKind.Invoke &&
                context.Trigger.Kind != EmbeddedCompletionTriggerKind.InvokeAndCommitIfUnique &&
                context.Trigger.Kind != EmbeddedCompletionTriggerKind.Insertion)
            {
                return;
            }

            // First, act as if the user just inserted the previous character.  This will cause us
            // to complete down to the set of relevant items based on that character. If we get
            // anything, we're done and can just show the user those items.  If we have no items to
            // add *and* the user was explicitly invoking completion, then just add the entire set
            // of suggestions to help the user out.
            var count = context.Items.Count;

            var (tree, stringToken) = treeAndStringToken.Value;
            ProvideCompletionsAfterInsertion(context, tree, stringToken);

            if (count != context.Items.Count)
            {
                // We added items.  Nothing else to do here.
                return;
            }

            if (context.Trigger.Kind == EmbeddedCompletionTriggerKind.Insertion)
            {
                // The user was typing a character, and we had nothing to add for them.  Just bail
                // out immediately as we cannot help in this circumstance.
                return;
            }

            // We added no items, but the user explicitly asked for completion.  Add all the
            // items we can to help them out.
            var inCharacterClass = false;

            var virtualChar = tree.Text.FirstOrNullable(vc => vc.Span.Contains(context.Position));
            if (virtualChar != null)
            {
                inCharacterClass = IsInCharacterClass(tree.Root, virtualChar.Value, inCharacterClass: false);
            }

            ProvideAnchorCompletions(context, stringToken, inCharacterClass);
            ProvideCharacterClassCompletions(context, stringToken, parentOpt: null);
            ProvideEscapeCompletions(context, stringToken, inCharacterClass, parentOpt: null);
        }

        private void ProvideAnchorCompletions(
            EmbeddedCompletionContext context, SyntaxToken stringToken, bool inCharacterClass)
        {
            if (inCharacterClass)
            {
                return;
            }

            AddIfMissing(context, CreateItem(stringToken, "^", regex_start_of_string_or_line_short, regex_start_of_string_or_line_long, context, parentOpt: null));
            AddIfMissing(context, CreateItem(stringToken, "$", regex_end_of_string_or_line_short, regex_end_of_string_or_line_long, context, parentOpt: null));
        }

        private void ProvideCompletionsAfterInsertion(
            EmbeddedCompletionContext context, RegexTree tree, SyntaxToken stringToken)
        {
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
                ProvideEscapeCompletions(context, stringToken, inCharacterClass, parent);
                return;
            }

            if (token.Kind == RegexKind.OpenBracketToken)
            {
                ProvideCharacterClassCompletions(context, stringToken, parent);
                return;
            }

            // see if we have ```\p{```.  If so, offer property categories
            if (previousVirtualChar.Char == '{')
            {
                var index = tree.Text.IndexOf(previousVirtualChar);
                if (index >= 2 && 
                    tree.Text[index - 2].Char == '\\' &&
                    tree.Text[index - 1].Char == 'p')
                {
                    var slashChar = tree.Text[index - 1];
                    result = FindToken(tree.Root, slashChar);
                    if (result == null)
                    {
                        return;
                    }

                    (parent, token) = result.Value;
                    if (parent is RegexEscapeNode)
                    {
                        ProvideEscapeCategoryCompletions(context);
                    }
                }

                // ProvideEscapeCategoryCompletions(context);
                return;
            }

            if (inCharacterClass)
            {
                // Nothing more to offer if we're in a character class.
                return;
            }

            switch (token.Kind)
            {
                case RegexKind.OpenParenToken:
                case RegexKind.QuestionToken:
                case RegexKind.LessThanToken:
                case RegexKind.EqualsToken:
                case RegexKind.SingleQuoteToken:
                case RegexKind.ExclamationToken:
                    // ProvideGroupingCompletions(context);
                    return;
                case RegexKind.OptionsToken:
                    // ProvideOptionsCompletions(context, optionsT);
                    return;
            }
        }

        private void ProvideCharacterClassCompletions(
            EmbeddedCompletionContext context, SyntaxToken stringToken, RegexNode parentOpt)
        {
            AddIfMissing(context, CreateItem(stringToken, "[]", regex_positive_character_group_short, regex_positive_character_group_long, context, parentOpt, positionOffset: "[".Length));
            AddIfMissing(context, CreateItem(stringToken, "[^]", regex_negative_character_group_short, regex_negative_character_group_long, context, parentOpt, positionOffset: "[^".Length));
        }

        private void ProvideEscapeCategoryCompletions(EmbeddedCompletionContext context)
        {
            foreach (var (name, (shortDesc, longDesc)) in RegexCharClass.EscapeCategories)
            {
                var displayText = name;
                if (shortDesc != "")
                {
                    displayText += "  -  " + shortDesc;
                }

                AddIfMissing(context, new EmbeddedCompletionItem(
                    displayText, longDesc.Length > 0 ? longDesc : shortDesc,
                    new EmbeddedCompletionChange(
                        new TextChange(new TextSpan(context.Position, 0), name), newPosition: null)));
            }
        }

        private void ProvideEscapeCompletions(
            EmbeddedCompletionContext context, SyntaxToken stringToken,
            bool inCharacterClass, RegexNode parentOpt)
        {
            if (parentOpt != null && !(parentOpt is RegexEscapeNode))
            {
                return;
            }

            if (!inCharacterClass)
            {
                AddIfMissing(context, CreateItem(stringToken, @"\A", regex_start_of_string_only_short, regex_start_of_string_only_long, context, parentOpt));
                AddIfMissing(context, CreateItem(stringToken, @"\b", regex_word_boundary_short, regex_word_boundary_long, context, parentOpt));
                AddIfMissing(context, CreateItem(stringToken, @"\B", regex_non_word_boundary_short, regex_non_word_boundary_long, context, parentOpt));
                AddIfMissing(context, CreateItem(stringToken, @"\G", regex_contiguous_matches_short, regex_contiguous_matches_long, context, parentOpt));
                AddIfMissing(context, CreateItem(stringToken, @"\z", regex_end_of_string_only_short, regex_end_of_string_only_long, context, parentOpt));
                AddIfMissing(context, CreateItem(stringToken, @"\Z", regex_end_of_string_or_before_ending_newline_short, regex_end_of_string_or_before_ending_newline_long, context, parentOpt));

                AddIfMissing(context, CreateItem(stringToken, @"\k<>", "", "", context, parentOpt, @"\k<".Length));
                AddIfMissing(context, CreateItem(stringToken, @"\<>", "", "", context, parentOpt, @"\<".Length));
                AddIfMissing(context, CreateItem(stringToken, @"\1-9", "", "", context, parentOpt, @"\".Length, @"\"));
            }

            AddIfMissing(context, CreateItem(stringToken, @"\a", regex_bell_character_short, regex_bell_character_long, context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\b", regex_backspace_character_short, regex_backspace_character_long, context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\e", regex_escape_character_short, regex_escape_character_long, context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\f", regex_form_feed_character_short, regex_form_feed_character_long, context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\n", regex_new_line_character_short, regex_new_line_character_long, context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\r", regex_carriage_return_character_short, regex_carriage_return_character_long, context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\t", regex_tab_character_short, regex_tab_character_long, context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\v", regex_vertical_tab_character_short, regex_vertical_tab_character_long, context, parentOpt));

            AddIfMissing(context, CreateItem(stringToken, @"\x##", regex_hexadecimal_escape_short, regex_hexadecimal_escape_long, context, parentOpt, @"\x".Length, @"\x"));
            AddIfMissing(context, CreateItem(stringToken, @"\u####", regex_unicode_escape_short, regex_unicode_escape_long, context, parentOpt, @"\u".Length, @"\u"));
            AddIfMissing(context, CreateItem(stringToken, @"\cX", regex_control_character_short, regex_control_character_long, context, parentOpt, @"\c".Length, @"\c"));

            AddIfMissing(context, CreateItem(stringToken, @"\d", "", "", context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\D", "", "", context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\p{}", "", "", context, parentOpt, @"\p".Length, @"\p"));
            AddIfMissing(context, CreateItem(stringToken, @"\P{}", "", "", context, parentOpt, @"\P".Length, @"\P"));
            AddIfMissing(context, CreateItem(stringToken, @"\s", "", "", context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\S", "", "", context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\w", "", "", context, parentOpt));
            AddIfMissing(context, CreateItem(stringToken, @"\W", "", "", context, parentOpt));
        }

        private void AddIfMissing(EmbeddedCompletionContext context, EmbeddedCompletionItem item)
        {
            if (context.Names.Add(item.DisplayText))
            {
                context.Items.Add(item);
            }
        }

        private EmbeddedCompletionItem CreateItem(
            SyntaxToken stringToken, string displayText, 
            string shortDescription, string longDescription,
            EmbeddedCompletionContext context, RegexNode parentOpt, 
            int? positionOffset = null, string insertionText = null)
        {
            var replacementStart = parentOpt != null
                ? parentOpt.GetSpan().Start
                : context.Position;

            var replacementSpan = TextSpan.FromBounds(replacementStart, context.Position);
            var newPosition = replacementStart + positionOffset;

            insertionText = insertionText ?? displayText;
            var escapedInsertionText = _language.EscapeText(insertionText, stringToken);
            if (shortDescription != "")
            {
                displayText += "  -  " + shortDescription;
            }

            if (escapedInsertionText != insertionText)
            {
                newPosition += escapedInsertionText.Length - insertionText.Length;
            }

            return new EmbeddedCompletionItem(
                displayText, longDescription, 
                new EmbeddedCompletionChange(
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
                    var result = IsInCharacterClass(child.Node, ch, inCharacterClass || child.Node is RegexBaseCharacterClassNode);
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
    }
}
