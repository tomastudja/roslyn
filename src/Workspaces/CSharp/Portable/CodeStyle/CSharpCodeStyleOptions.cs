// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.CSharp.CodeStyle
{
    internal static class CSharpCodeStyleOptions
    {
        // TODO: get sign off on public api changes.
        public static readonly Option<bool> UseVarWhenDeclaringLocals = new Option<bool>(nameof(CodeStyleOptions), nameof(UseVarWhenDeclaringLocals), defaultValue: true,
            storageLocations: new RoamingProfileStorageLocation("TextEditor.CSharp.Specific.UseVarWhenDeclaringLocals"));

        public static readonly Option<CodeStyleOption<bool>> UseImplicitTypeForIntrinsicTypes = new Option<CodeStyleOption<bool>>(nameof(CodeStyleOptions), nameof(UseImplicitTypeForIntrinsicTypes), defaultValue: CodeStyleOption<bool>.Default,
            storageLocations: new RoamingProfileStorageLocation("TextEditor.CSharp.Specific.UseImplicitTypeForIntrinsicTypes"));

        public static readonly Option<CodeStyleOption<bool>> UseImplicitTypeWhereApparent = new Option<CodeStyleOption<bool>>(nameof(CodeStyleOptions), nameof(UseImplicitTypeWhereApparent), defaultValue: CodeStyleOption<bool>.Default,
            storageLocations: new RoamingProfileStorageLocation("TextEditor.CSharp.Specific.UseImplicitTypeWhereApparent"));

        public static readonly Option<CodeStyleOption<bool>> UseImplicitTypeWherePossible = new Option<CodeStyleOption<bool>>(nameof(CodeStyleOptions), nameof(UseImplicitTypeWherePossible), defaultValue: CodeStyleOption<bool>.Default,
            storageLocations: new RoamingProfileStorageLocation("TextEditor.CSharp.Specific.UseImplicitTypeWherePossible"));

        public static readonly Option<CodeStyleOption<bool>> PreferConditionalDelegateCall = new Option<CodeStyleOption<bool>>(nameof(CodeStyleOptions), nameof(PreferConditionalDelegateCall), defaultValue: CodeStyleOptions.TrueWithSuggestionEnforcement,
            storageLocations: new RoamingProfileStorageLocation("TextEditor.CSharp.Specific.PreferConditionalDelegateCall"));

        public static readonly Option<CodeStyleOption<bool>> PreferExpressionBodiedConstructors = new Option<CodeStyleOption<bool>>(
            nameof(CodeStyleOptions), nameof(PreferExpressionBodiedConstructors), defaultValue: CodeStyleOptions.FalseWithNoneEnforcement,
            storageLocations: new RoamingProfileStorageLocation($"TextEditor.CSharp.Specific.{nameof(PreferExpressionBodiedConstructors)}"));

        public static readonly Option<CodeStyleOption<bool>> PreferExpressionBodiedMethods = new Option<CodeStyleOption<bool>>(
            nameof(CodeStyleOptions), nameof(PreferExpressionBodiedMethods), defaultValue: CodeStyleOptions.FalseWithNoneEnforcement,
            storageLocations: new RoamingProfileStorageLocation($"TextEditor.CSharp.Specific.{nameof(PreferExpressionBodiedMethods)}"));

        public static readonly Option<CodeStyleOption<bool>> PreferExpressionBodiedOperators = new Option<CodeStyleOption<bool>>(
            nameof(CodeStyleOptions), nameof(PreferExpressionBodiedOperators), defaultValue: CodeStyleOptions.FalseWithNoneEnforcement,
            storageLocations: new RoamingProfileStorageLocation($"TextEditor.CSharp.Specific.{nameof(PreferExpressionBodiedOperators)}"));

        public static readonly Option<CodeStyleOption<bool>> PreferExpressionBodiedProperties = new Option<CodeStyleOption<bool>>(
            nameof(CodeStyleOptions), nameof(PreferExpressionBodiedProperties), defaultValue: CodeStyleOptions.TrueWithNoneEnforcement,
            storageLocations: new RoamingProfileStorageLocation($"TextEditor.CSharp.Specific.{nameof(PreferExpressionBodiedProperties)}"));

        public static readonly Option<CodeStyleOption<bool>> PreferExpressionBodiedIndexers = new Option<CodeStyleOption<bool>>(
            nameof(CodeStyleOptions), nameof(PreferExpressionBodiedIndexers), defaultValue: CodeStyleOptions.TrueWithNoneEnforcement,
            storageLocations: new RoamingProfileStorageLocation($"TextEditor.CSharp.Specific.{nameof(PreferExpressionBodiedIndexers)}"));

        public static readonly Option<CodeStyleOption<bool>> PreferExpressionBodiedAccessors = new Option<CodeStyleOption<bool>>(
            nameof(CodeStyleOptions), nameof(PreferExpressionBodiedAccessors), defaultValue: CodeStyleOptions.TrueWithNoneEnforcement,
            storageLocations: new RoamingProfileStorageLocation($"TextEditor.CSharp.Specific.{nameof(PreferExpressionBodiedAccessors)}"));
    }
}