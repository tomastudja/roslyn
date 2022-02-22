﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.SpellCheck
{
    /// <summary>
    /// Service for individual languages to provide the regions of their code that should be spell checked.
    /// </summary>
    internal interface ISpellCheckSpanService : ILanguageService
    {
        Task<ImmutableArray<SpellCheckSpan>> GetSpansAsync(Document document, CancellationToken cancellationToken);
    }

    internal readonly record struct SpellCheckSpan(
        TextSpan TextSpan,
        SpellCheckKind Kind);

    internal enum SpellCheckKind
    {
        Identifier,
        Comment,
        String,
    }
}
