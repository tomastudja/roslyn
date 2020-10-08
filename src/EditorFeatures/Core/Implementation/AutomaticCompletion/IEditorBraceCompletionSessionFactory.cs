﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Threading;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Editor.Implementation.AutomaticCompletion
{
    internal interface IEditorBraceCompletionSessionFactory : ILanguageService
    {
        IEditorBraceCompletionSession TryCreateSession(Document document, int openingPosition, char openingBrace, CancellationToken cancellationToken);
    }
}
