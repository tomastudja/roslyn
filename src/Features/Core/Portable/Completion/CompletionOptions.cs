﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Completion
{
    internal static class CompletionOptions
    {
        internal const string FeatureName = "Completion";

        // This is serialized by the Visual Studio-specific LanguageSettingsSerializer
        public static readonly PerLanguageOption<bool> HideAdvancedMembers = new PerLanguageOption<bool>(FeatureName, "HideAdvancedMembers", defaultValue: false);
        
        // This is serialized by the Visual Studio-specific LanguageSettingsSerializer
        public static readonly PerLanguageOption<bool> TriggerOnTyping = new PerLanguageOption<bool>(FeatureName, "TriggerOnTyping", defaultValue: true);

        public static readonly PerLanguageOption<bool> TriggerOnTypingLetters = new PerLanguageOption<bool>(FeatureName, nameof(TriggerOnTypingLetters), defaultValue: true,
            persistences: new RoamingProfilePersistence("TextEditor.%LANGUAGE%.Specific.TriggerOnTypingLetters"));
        public static readonly PerLanguageOption<bool?> TriggerOnDeletion = new PerLanguageOption<bool?>(FeatureName, nameof(TriggerOnDeletion), defaultValue: null,
            persistences: new RoamingProfilePersistence("TextEditor.%LANGUAGE%.Specific.TriggerOnDeletion"));

        public static readonly PerLanguageOption<EnterKeyRule> EnterKeyBehavior =
            new PerLanguageOption<EnterKeyRule>(FeatureName, nameof(EnterKeyBehavior), defaultValue: EnterKeyRule.Default,
                persistences: new RoamingProfilePersistence("TextEditor.%LANGUAGE%.Specific.EnterKeyBehavior"));

        public static readonly PerLanguageOption<SnippetsRule> SnippetsBehavior =
            new PerLanguageOption<SnippetsRule>(FeatureName, nameof(SnippetsBehavior), defaultValue: SnippetsRule.Default,
                persistences: new RoamingProfilePersistence("TextEditor.%LANGUAGE%.Specific.SnippetsBehavior"));

        // Dev15 options
        public static readonly PerLanguageOption<bool> ShowCompletionItemFilters = new PerLanguageOption<bool>(FeatureName, nameof(ShowCompletionItemFilters), defaultValue: true,
            persistences: new RoamingProfilePersistence("TextEditor.%LANGUAGE%.Specific.ShowCompletionItemFilters"));

        public static readonly PerLanguageOption<bool> HighlightMatchingPortionsOfCompletionListItems = new PerLanguageOption<bool>(FeatureName, nameof(HighlightMatchingPortionsOfCompletionListItems), defaultValue: true,
            persistences: new RoamingProfilePersistence("TextEditor.%LANGUAGE%.Specific.HighlightMatchingPortionsOfCompletionListItems"));

        public static IEnumerable<PerLanguageOption<bool>> GetDev15CompletionOptions()
        {
            yield return ShowCompletionItemFilters;
            yield return HighlightMatchingPortionsOfCompletionListItems;
        }
    }

    internal static class CompletionControllerOptions
    {
        internal const string ControllerFeatureName = "CompletionController";

        public static readonly Option<bool> AlwaysShowBuilder = new Option<bool>(ControllerFeatureName, "AlwaysShowBuilder", defaultValue: false);
        public static readonly Option<bool> FilterOutOfScopeLocals = new Option<bool>(ControllerFeatureName, "FilterOutOfScopeLocals", defaultValue: true);
        public static readonly Option<bool> ShowXmlDocCommentCompletion = new Option<bool>(ControllerFeatureName, "ShowXmlDocCommentCompletion", defaultValue: true);
    }
}
