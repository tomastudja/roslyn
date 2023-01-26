﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeLens;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using static Microsoft.CodeAnalysis.LanguageServer.Handler.CodeLens.CodeLensCache;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.CodeLens;
internal class CodeLensCache : ResolveCache<CodeLensCacheEntry>
{
    public CodeLensCache() : base(maxCacheSize: 3)
    {
    }

    internal record CodeLensCacheEntry(ImmutableArray<CodeLensMember> CodeLensMembers, TextDocumentIdentifier TextDocumentIdentifier);
}
