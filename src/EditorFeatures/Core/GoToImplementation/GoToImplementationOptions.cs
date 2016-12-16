﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Editor.GoToImplementation
{
    internal static class GoToImplementationOptions
    {
        public static readonly PerLanguageOption<bool> Enabled = new PerLanguageOption<bool>(
            nameof(GoToImplementationOptions), nameof(Enabled), defaultValue: true);
    }
}