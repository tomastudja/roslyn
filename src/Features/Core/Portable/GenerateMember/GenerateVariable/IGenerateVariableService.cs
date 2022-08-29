﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.GenerateMember.GenerateVariable
{
    internal interface IGenerateVariableService : ILanguageService
    {
        Task<ImmutableArray<CodeAction>> GenerateVariableAsync(Document document, SyntaxNode node, CodeAndImportGenerationOptionsProvider fallbackOptions, CancellationToken cancellationToken);
    }
}
