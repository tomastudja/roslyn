﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.GenerateEqualsAndGetHashCodeFromMembers
{
    internal static class GenerateEqualsAndGetHashCodeFromMembersOptions
    {
        public static readonly PerLanguageOption<bool> GenerateOperators = new PerLanguageOption<bool>(
            nameof(GenerateEqualsAndGetHashCodeFromMembersOptions),
            nameof(GenerateOperators), defaultValue: false,
            storageLocations: new RoamingProfileStorageLocation(
                $"TextEditor.%LANGUAGE%.Specific.{nameof(GenerateEqualsAndGetHashCodeFromMembersOptions)}.{nameof(GenerateOperators)}"));

        public static readonly PerLanguageOption<bool> ImplementIEquatable = new PerLanguageOption<bool>(
            nameof(GenerateEqualsAndGetHashCodeFromMembersOptions),
            nameof(ImplementIEquatable), defaultValue: false,
            storageLocations: new RoamingProfileStorageLocation(
                $"TextEditor.%LANGUAGE%.Specific.{nameof(GenerateEqualsAndGetHashCodeFromMembersOptions)}.{nameof(ImplementIEquatable)}"));
    }
}
