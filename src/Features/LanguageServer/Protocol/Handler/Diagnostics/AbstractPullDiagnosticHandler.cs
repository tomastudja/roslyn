﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.Diagnostics
{
    internal abstract class AbstractPullDiagnosticHandler<TDiagnosticsParams, TReport> : IRequestHandler<TDiagnosticsParams, TReport[]?>
        where TReport : DiagnosticReport
    {
        private readonly ILspSolutionProvider _solutionProvider;
        private readonly IDiagnosticService _diagnosticService;

        /// <summary>
        /// Lock to product <see cref="_documentIdToLastResultId"/> and <see cref="_nextResultId"/>.
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>
        /// Mapping of a document to the last result id we reported for it.
        /// </summary>
        private readonly Dictionary<(Workspace workspace, DocumentId documentId), string> _documentIdToLastResultId = new();

        /// <summary>
        /// The next available id to label results with.
        /// </summary>
        private long _nextResultId;

        protected AbstractPullDiagnosticHandler(
            ILspSolutionProvider solutionProvider,
            IDiagnosticService diagnosticService)
        {
            _solutionProvider = solutionProvider;
            _diagnosticService = diagnosticService;
            _diagnosticService.DiagnosticsUpdated += OnDiagnosticsUpdated;
        }

        public abstract TextDocumentIdentifier? GetTextDocumentIdentifier(TDiagnosticsParams diagnosticsParams);

        /// <summary>
        /// Gets the progress object to stream results to.
        /// </summary>
        protected abstract IProgress<TReport[]>? GetProgress(TDiagnosticsParams diagnosticsParams);

        /// <summary>
        /// Retrieve the previous results we reported.  Used so we can avoid resending data for unchanged files. Also
        /// used so we can report which documents were removed and can have all their diagnostics cleared.
        /// </summary>
        protected abstract DiagnosticParams[]? GetPreviousResults(TDiagnosticsParams diagnosticsParams);

        /// <summary>
        /// Returns all the documents that should be processed in the desired order to process them in.
        /// </summary>
        protected abstract ImmutableArray<Document> GetOrderedDocuments(RequestContext context);
        protected abstract TReport CreateReport(TextDocumentIdentifier? identifier, VSDiagnostic[]? diagnostics, string? resultId);

        private void OnDiagnosticsUpdated(object? sender, DiagnosticsUpdatedArgs updateArgs)
        {
            if (updateArgs.DocumentId == null)
                return;

            // Whenever we hear about changes to a document, drop the data we've stored for it.  We'll recompute it as
            // necessary on the next request.
            _documentIdToLastResultId.Remove((updateArgs.Workspace, updateArgs.DocumentId));
        }

        public async Task<TReport[]?> HandleRequestAsync(
            TDiagnosticsParams diagnosticsParams, RequestContext context, CancellationToken cancellationToken)
        {
            // The progress object we will stream reports to.
            var progress = GetProgress(diagnosticsParams);

            // The buffer we will add results to if our client doesn't support streaming results.
            using var _ = ArrayBuilder<TReport>.GetInstance(out var reports);

            // Get the set of results the request said were previously reported.  We can use this to determine both
            // what to skip, and what files we have to tell the client have been removed.
            var previousResults = GetPreviousResults(diagnosticsParams) ?? Array.Empty<DiagnosticParams>();

            var documentToPreviousResult = new Dictionary<Document, DiagnosticParams>();
            foreach (var previousResult in previousResults)
                AddPreviousResult(documentToPreviousResult, previousResult);

            // First, let the client know if any workspace documents have gone away.  That way it can remove those for
            // the user from squiggles or error-list.
            HandleRemovedDocuments(previousResults, progress, reports);

            // Next process each file in priority order. Determine if diagnostics are changed or unchanged since the
            // last time we notified the client.  Report back either to the client so they can update accordingly.
            foreach (var document in GetOrderedDocuments(context))
            {
                if (DiagnosticsAreUnchanged(documentToPreviousResult, document))
                {
                    // Nothing changed between the last request and this one.  Report a null-diagnostics, same-result-id
                    // response to the client to know they don't need to do anything.
                    var previousResult = documentToPreviousResult[document];
                    Report(progress, reports,
                        CreateReport(previousResult.TextDocument, diagnostics: null, previousResult.PreviousResultId));
                }
                else
                {
                    await ComputeAndReportCurrentDiagnosticsAsync(progress, reports, document, cancellationToken).ConfigureAwait(false);
                }
            }

            // If we had a progress object, then we will have been reporting to that.  Otherwise, take what we've been
            // collecting and return that.
            return progress != null ? null : reports.ToArray();
        }

        private async Task ComputeAndReportCurrentDiagnosticsAsync(
            IProgress<TReport[]>? progress,
            ArrayBuilder<TReport> reports,
            Document document,
            CancellationToken cancellationToken)
        {
            // Being asked about this document for the first time.  Or being asked again and we have different
            // diagnostics.  Compute and report the current diagnostics info for this document.

            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var _ = ArrayBuilder<VSDiagnostic>.GetInstance(out var diagnostics);
            foreach (var diagnostic in _diagnosticService.GetDiagnostics(document, includeSuppressedDiagnostics: false, cancellationToken))
                diagnostics.Add(ConvertDiagnostic(document, text, diagnostic));

            var report = RecordDiagnosticReport(document, diagnostics.ToArray());
            Report(progress, reports, report);
        }

        private void HandleRemovedDocuments(DiagnosticParams[]? previousResults, IProgress<TReport[]>? progress, ArrayBuilder<TReport> reports)
        {
            if (previousResults == null)
                return;

            foreach (var previousResult in previousResults)
            {
                var textDocument = previousResult.TextDocument;
                if (textDocument != null)
                {
                    var document = _solutionProvider.GetDocument(textDocument);
                    if (document == null)
                    {
                        // Client is asking server about a document that no longer exists (i.e. was removed/deleted from the
                        // workspace).  In that case we need to return an actual diagnostic report with `null` for the
                        // diagnostics to let the client know to dump that file entirely.
                        Report(progress, reports, CreateReport(textDocument, diagnostics: null, resultId: null));
                    }
                }
            }
        }

        private static void Report(IProgress<TReport[]>? progress, ArrayBuilder<TReport> reports, TReport report)
        {
            progress?.Report(new[] { report });
            reports.Add(report);
        }

        private bool DiagnosticsAreUnchanged(Dictionary<Document, DiagnosticParams> documentToPreviousResult, Document document)
        {
            lock (_gate)
            {
                var workspace = document.Project.Solution.Workspace;
                return documentToPreviousResult.TryGetValue(document, out var previousResult) &&
                       _documentIdToLastResultId.TryGetValue((workspace, document.Id), out var lastReportedResultId) &&
                       lastReportedResultId == previousResult.PreviousResultId;
            }
        }

        private TReport RecordDiagnosticReport(Document document, VSDiagnostic[] diagnostics)
        {
            lock (_gate)
            {
                // Keep track of the diagnostics we reported here so that we can short-circuit producing diagnostics for
                // the same diagnostic set in the future.
                var resultId = _nextResultId++.ToString();
                _documentIdToLastResultId[(document.Project.Solution.Workspace, document.Id)] = resultId;
                return CreateReport(ProtocolConversions.DocumentToTextDocumentIdentifier(document), diagnostics, resultId);
            }
        }

        protected void AddPreviousResult(
            Dictionary<Document, DiagnosticParams> documentToDiagnosticParams, DiagnosticParams previousResult)
        {
            if (previousResult.TextDocument != null && previousResult.PreviousResultId != null)
            {
                var document = _solutionProvider.GetDocument(previousResult.TextDocument);
                if (document != null)
                    documentToDiagnosticParams[document] = previousResult;
            }
        }
        public static VSDiagnostic ConvertDiagnostic(Document document, SourceText text, DiagnosticData diagnosticData)
        {
            Contract.ThrowIfNull(diagnosticData.Message, $"Got a document diagnostic that did not have a {nameof(diagnosticData.Message)}");
            Contract.ThrowIfNull(diagnosticData.DataLocation, $"Got a document diagnostic that did not have a {nameof(diagnosticData.DataLocation)}");

            var project = document.Project;
            return new VSDiagnostic
            {
                Code = diagnosticData.Id,
                Message = diagnosticData.Message,
                Severity = ProtocolConversions.DiagnosticSeverityToLspDiagnositcSeverity(diagnosticData.Severity),
                Range = ProtocolConversions.LinePositionToRange(DiagnosticData.GetLinePositionSpan(diagnosticData.DataLocation, text, useMapped: true)),
                Tags = ConvertTags(diagnosticData),
                DiagnosticType = diagnosticData.Category,
                Projects = new[]
                {
                    new ProjectAndContext
                    {
                        ProjectIdentifier = project.Id.Id.ToString(),
                        ProjectName = project.Name,
                    },
                },
            };
        }

        private static DiagnosticTag[] ConvertTags(DiagnosticData diagnosticData)
        {
            using var _ = ArrayBuilder<DiagnosticTag>.GetInstance(out var result);

            if (diagnosticData.Severity == DiagnosticSeverity.Hidden)
            {
                result.Add(VSDiagnosticTags.HiddenInEditor);
                result.Add(VSDiagnosticTags.HiddenInErrorList);
                result.Add(VSDiagnosticTags.SuppressEditorToolTip);
            }
            else
            {
                result.Add(VSDiagnosticTags.VisibleInErrorList);
            }

            foreach (var tag in diagnosticData.CustomTags)
            {
                switch (tag)
                {
                    case WellKnownDiagnosticTags.Unnecessary:
                        result.Add(DiagnosticTag.Unnecessary);
                        break;
                    case WellKnownDiagnosticTags.Build:
                        result.Add(VSDiagnosticTags.BuildError);
                        break;
                }
            }

            return result.ToArray();
        }
    }
}
