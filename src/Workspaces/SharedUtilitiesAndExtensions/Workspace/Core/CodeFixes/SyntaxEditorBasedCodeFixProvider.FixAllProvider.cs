﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.CodeFixes
{
    internal partial class SyntaxEditorBasedCodeFixProvider : CodeFixProvider
    {
        /// <summary>
        /// A simple implementation of <see cref="FixAllProvider"/> that takes care of collecting
        /// all the diagnostics and fixes all documents in parallel.  The only functionality a 
        /// subclass needs to provide is how each document will apply all the fixes to all the 
        /// diagnostics in that document.
        /// </summary>
        internal sealed class SyntaxEditorBasedFixAllProvider : DocumentBasedFixAllProvider
        {
            private readonly SyntaxEditorBasedCodeFixProvider _codeFixProvider;

            public SyntaxEditorBasedFixAllProvider(SyntaxEditorBasedCodeFixProvider codeFixProvider)
                => _codeFixProvider = codeFixProvider;

            protected override string CodeActionTitle => "";

            protected override string GetCodeActionTitle(FixAllContext context)
                => FixAllContextHelper.GetDefaultFixAllTitle(context);

            protected override async Task<SyntaxNode?> FixAllInDocumentAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
            {
                var newDocument = await FixDocumentAsync(document, diagnostics, fixAllContext).ConfigureAwait(false);

                // If we didn't get a new document, or the doc was unchanged, pass back null to indicate there are no
                // fixes for this file.
                if (newDocument == null || newDocument == document)
                    return null;

                return await newDocument.GetSyntaxRootAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            }

            private async Task<Document> FixDocumentAsync(
                Document document, ImmutableArray<Diagnostic> diagnostics, FixAllContext fixAllContext)
            {
                var model = await document.GetRequiredSemanticModelAsync(fixAllContext.CancellationToken).ConfigureAwait(false);

                // Ensure that diagnostics for this document are always in document location
                // order.  This provides a consistent and deterministic order for fixers
                // that want to update a document.
                // Also ensure that we do not pass in duplicates by invoking Distinct.
                // See https://github.com/dotnet/roslyn/issues/31381, that seems to be causing duplicate diagnostics.
                var filteredDiagnostics = diagnostics.Distinct()
                                                     .WhereAsArray(d => _codeFixProvider.IncludeDiagnosticDuringFixAll(d, document, model, fixAllContext.CodeActionEquivalenceKey, fixAllContext.CancellationToken))
                                                     .Sort((d1, d2) => d1.Location.SourceSpan.Start - d2.Location.SourceSpan.Start);

                // PERF: Do not invoke FixAllAsync on the code fix provider if there are no diagnostics to be fixed.
                if (filteredDiagnostics.Length == 0)
                {
                    return document;
                }

                return await _codeFixProvider.FixAllAsync(document, filteredDiagnostics, fixAllContext.CancellationToken).ConfigureAwait(false);
            }
        }
    }
}
