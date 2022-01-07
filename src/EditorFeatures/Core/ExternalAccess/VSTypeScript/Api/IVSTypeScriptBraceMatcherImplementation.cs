﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.ExternalAccess.VSTypeScript.Api
{
    internal interface IVSTypeScriptBraceMatcherImplementation
    {
        Task<VSTypeScriptBraceMatchingResult?> FindBracesAsync(Document document, int position, CancellationToken cancellationToken);
    }

    internal readonly record struct VSTypeScriptBraceMatchingResult(TextSpan LeftSpan, TextSpan RightSpan);
}
