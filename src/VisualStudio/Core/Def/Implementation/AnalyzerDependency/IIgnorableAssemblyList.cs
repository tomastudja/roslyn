﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;

namespace Microsoft.VisualStudio.LanguageServices.Implementation
{
    internal interface IIgnorableAssemblyList
    {
        bool Includes(AssemblyIdentity assemblyIdentity);
    }
}
