﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Editor.UnitTests
{
    public partial class SettingsUpdaterTests
    {
        private class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            public static TestAnalyzerConfigOptions Instance = new TestAnalyzerConfigOptions();

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = null;
                return false;
            }
        }
    }
}
