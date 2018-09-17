﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Options;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    public static partial class CSharpFormattingOptions
    {
        internal static bool DetermineIfSpaceOptionIsSet(string value, SpacingWithinParenthesesOption parenthesesSpacingOption)
            => (from v in value.Split(',').Select(v => v.Trim())
                let option = ConvertToSpacingOption(v)
                where option.HasValue && option.Value == parenthesesSpacingOption
                select option)
                .Any();

        private static SpacingWithinParenthesesOption? ConvertToSpacingOption(string value)
            => s_spacingWithinParenthesisOptionsEditorConfigMap.GetValueOrDefault(value);

        private static string GetSpacingWithParenthesesEditorConfigString(OptionSet optionSet)
        {
            List<string> editorConfigStringBuilderOpt = null;
            foreach (var kvp in SpacingWithinParenthesisOptionsMap)
            {
                var value = optionSet.GetOption(kvp.Key);
                if (value)
                {
                    editorConfigStringBuilderOpt = editorConfigStringBuilderOpt ?? new List<string>(SpacingWithinParenthesisOptionsMap.Count);
                    Debug.Assert(s_spacingWithinParenthesisOptionsEditorConfigMap.ContainsValue(kvp.Value));
                    editorConfigStringBuilderOpt.Add(s_spacingWithinParenthesisOptionsEditorConfigMap.GetKeyOrDefault(kvp.Value));
                }
            }

            if (editorConfigStringBuilderOpt == null)
            {
                // No spacing within parenthesis option set.
                return "false";
            }
            else
            {
                return string.Join(",", editorConfigStringBuilderOpt.Order());
            }
        }

        internal static BinaryOperatorSpacingOptions ParseEditorConfigSpacingAroundBinaryOperator(string binaryOperatorSpacingValue)
            => s_binaryOperatorSpacingOptionsEditorConfigMap.TryGetValue(binaryOperatorSpacingValue, out var value) ? value : BinaryOperatorSpacingOptions.Single;

        private static string GetSpacingAroundBinaryOperatorEditorConfigString(BinaryOperatorSpacingOptions value)
            => s_binaryOperatorSpacingOptionsEditorConfigMap.TryGetKey(value, out string key) ? key : null;

        internal static LabelPositionOptions ParseEditorConfigLabelPositioning(string labelIndentationValue)
            => s_labelPositionOptionsEditorConfigMap.TryGetValue(labelIndentationValue, out var value) ? value : LabelPositionOptions.NoIndent;
        private static string GetLabelPositionOptionEditorConfigString(LabelPositionOptions value)
            => s_labelPositionOptionsEditorConfigMap.TryGetKey(value, out string key) ? key : null;

        internal static bool DetermineIfNewLineOptionIsSet(string value, NewLineOption optionName)
        {
            var values = value.Split(',');

            if (values.Any(s => s.Trim() == "all"))
            {
                return true;
            }

            if (values.Any(s => s.Trim() == "none"))
            {
                return false;
            }

            return (from v in values
                    let option = ConvertToNewLineOption(v)
                    where option.HasValue && option.Value == optionName
                    select option)
                    .Any();
        }

        private static NewLineOption? ConvertToNewLineOption(string value)
            => s_newLineOptionsEditorConfigMap.GetValueOrDefault(value);
        private static string GetNewLineOptionEditorConfigString(OptionSet optionSet)
        {
            List<string> editorConfigStringBuilderOpt = null;
            foreach (var kvp in NewLineOptionsMap)
            {
                var value = optionSet.GetOption(kvp.Key);
                if (value)
                {
                    editorConfigStringBuilderOpt = editorConfigStringBuilderOpt ?? new List<string>(NewLineOptionsMap.Count);
                    Debug.Assert(s_newLineOptionsEditorConfigMap.ContainsValue(kvp.Value));
                    editorConfigStringBuilderOpt.Add(s_newLineOptionsEditorConfigMap.GetKeyOrDefault(kvp.Value));
                }
            }

            if (editorConfigStringBuilderOpt == null)
            {
                // No NewLine option set.
                return "none";
            }
            else if (editorConfigStringBuilderOpt.Count == s_newLineOptionsMapBuilder.Count)
            {
                // All NewLine options set.
                return "all";
            }
            else
            {
                return string.Join(",", editorConfigStringBuilderOpt.Order());
            }
        }

        internal static bool DetermineIfIgnoreSpacesAroundVariableDeclarationIsSet(string value)
            => value.Trim() == "ignore";

        internal enum SpacingWithinParenthesesOption
        {
            Expressions,
            TypeCasts,
            ControlFlowStatements
        }

        internal enum NewLineOption
        {
            Types,
            Methods,
            Properties,
            Indexers,
            Events,
            AnonymousMethods,
            ControlBlocks,
            AnonymousTypes,
            ObjectCollectionsArrayInitializers,
            Lambdas,
            LocalFunction,
            Accessors
        }
    }
}
