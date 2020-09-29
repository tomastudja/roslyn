﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.ExternalAccess.Razor
{
    internal interface IRazorDocumentExcerptService
    {
        Task<RazorExcerptResult?> TryExcerptAsync(Document document, TextSpan span, RazorExcerptMode mode, CancellationToken cancellationToken);
    }
}
