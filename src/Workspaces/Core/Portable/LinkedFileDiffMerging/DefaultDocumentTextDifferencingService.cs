﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis
{
    [ExportWorkspaceService(typeof(IDocumentTextDifferencingService), ServiceLayer.Default), Shared]
    internal class DefaultDocumentTextDifferencingService : IDocumentTextDifferencingService
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Incorrectly used in production code: https://github.com/dotnet/roslyn/issues/42839")]
        public DefaultDocumentTextDifferencingService()
        {
        }

        public Task<ImmutableArray<TextChange>> GetTextChangesAsync(Document oldDocument, Document newDocument, CancellationToken cancellationToken)
            => GetTextChangesAsync(oldDocument, newDocument, TextDifferenceTypes.Word, cancellationToken);

        public async Task<ImmutableArray<TextChange>> GetTextChangesAsync(Document oldDocument, Document newDocument, TextDifferenceTypes preferredDifferenceType, CancellationToken cancellationToken)
        {
            var changes = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken).ConfigureAwait(false);
            return changes.ToImmutableArray();
        }
    }
}
