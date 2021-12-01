﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.CodeAnalysis.EditorConfig;
using static Microsoft.CodeAnalysis.EditorConfig.LanguageConstants;

internal static class LanguageExtensions
{
    public static Language GetLanguageFromFilePath(this string filePath)
    {
        var language = default(Language);
        var fileExtension = Path.GetExtension(filePath);
        if (fileExtension == DefaultCSharpExtension)
        {
            language = Language.CSharp;
        }

        if (fileExtension == DefaultVisualBasicExtension)
        {
            language = Language.VisualBasic;
        }

        return language;
    }
}
