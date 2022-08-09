﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.SolutionCrawler;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Diagnostics.EngineV2
{
    internal partial class DiagnosticIncrementalAnalyzer
    {
        /// <summary>
        /// This type performs incremental analysis in presence of edits to only a single member inside a document.
        /// For typing scenarios where we are continuously editing a method body, we can optimize the full
        /// document diagnostic computation by doing the following:
        ///   1. Re-using all the old cached diagnostics outside the edited member node from a prior
        ///      document snapshot, but with updated diagnostic spans.
        ///      AND
        ///   2. Replacing all the old diagnostics for the edited member node in a prior document snapshot
        ///      with the newly computed diagnostics for this member node in the latest document snaphot.
        /// If we are unable to perform this incremental diagnostics update, we fallback to computing
        /// the diagnostics for the entire document.
        /// </summary>
        private sealed partial class IncrementalMemberEditAnalyzer
        {
            /// <summary>
            /// Weak reference to the last document snapshot for which full document diagnostics
            /// were computed and saved.
            /// </summary>
            private readonly WeakReference<Document?> _lastDocumentWithCachedDiagnostics = new(null);

            public void UpdateDocumentWithCachedDiagnostics(Document document)
                => _lastDocumentWithCachedDiagnostics.SetTarget(document);

            public async Task<ImmutableDictionary<DiagnosticAnalyzer, ImmutableArray<DiagnosticData>>> ComputeDiagnosticsAsync(
                DocumentAnalysisExecutor executor,
                ImmutableArray<StateSet> stateSets,
                VersionStamp version,
                Func<DiagnosticAnalyzer, DocumentAnalysisExecutor, CancellationToken, Task<ImmutableArray<DiagnosticData>>> computeAnalyzerDiagnosticsAsync,
                Func<DocumentAnalysisExecutor, CancellationToken, Task<ImmutableDictionary<DiagnosticAnalyzer, ImmutableArray<DiagnosticData>>>> computeDiagnosticsNonIncrementallyAsync,
                CancellationToken cancellationToken)
            {
                var analysisScope = executor.AnalysisScope;

                // We should be asked to perform incremental analysis only for full document diagnostics computation.
                Debug.Assert(!analysisScope.Span.HasValue);

                // Ensure that only the analyzers that support incremental span-based analysis are provided.
                Debug.Assert(stateSets.All(stateSet => stateSet.Analyzer.SupportsSpanBasedSemanticDiagnosticAnalysis()));

                var document = (Document)analysisScope.TextDocument;
                var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var changedMemberAndIdAndSpansAndDocument = await TryGetChangedMemberAsync(document, root, cancellationToken).ConfigureAwait(false);
                if (changedMemberAndIdAndSpansAndDocument == null)
                {
                    // This is not a member-edit scenario, so compute full document diagnostics
                    // without incremental analysis.
                    return await computeDiagnosticsNonIncrementallyAsync(executor, cancellationToken).ConfigureAwait(false);
                }

                var (changedMember, changedMemberId, newMemberSpans, oldDocument) = changedMemberAndIdAndSpansAndDocument.Value;

                try
                {
                    var oldDocumentVersion = await GetDiagnosticVersionAsync(oldDocument.Project, cancellationToken).ConfigureAwait(false);

                    using var _1 = ArrayBuilder<(DiagnosticAnalyzer, DocumentAnalysisData)>.GetInstance(out var spanBasedAnalyzers);
                    using var _2 = ArrayBuilder<(DiagnosticAnalyzer, DocumentAnalysisData)>.GetInstance(out var documentBasedAnalyzers);
                    var performSpanBasedAnalysisForCompilerAnalyzer = false;
                    foreach (var stateSet in stateSets)
                    {
                        // Check if we have existing cached diagnostics for this analyzer whose version matches the
                        // old document version. If so, we can perform span based incremental analysis for the changed member.
                        // Otherwise, we have to perform entire document analysis.
                        var state = stateSet.GetOrCreateActiveFileState(document.Id);
                        var existingData = state.GetAnalysisData(analysisScope.Kind);
                        if (oldDocumentVersion == existingData.Version)
                        {
                            spanBasedAnalyzers.Add((stateSet.Analyzer, existingData));

                            if (stateSet.Analyzer.IsCompilerAnalyzer())
                                performSpanBasedAnalysisForCompilerAnalyzer = true;
                        }
                        else
                        {
                            documentBasedAnalyzers.Add((stateSet.Analyzer, DocumentAnalysisData.Empty));
                        }
                    }

                    if (spanBasedAnalyzers.Count == 0)
                    {
                        // No incremental analysis to be performed.
                        return await computeDiagnosticsNonIncrementallyAsync(executor, cancellationToken).ConfigureAwait(false);
                    }

                    // Get or create the member spans for all member nodes in the old document.
                    var oldMemberSpans = await GetOrCreateMemberSpansAsync(oldDocument, oldDocumentVersion, cancellationToken).ConfigureAwait(false);

                    // We need to execute the span-based analyzers and document-based analyzers separately.
                    // However, we want to ensure that compiler analyzer executes before rest of the analyzers
                    // so that compiler's semantic diagnostics refresh before the analyzer diagnostics.
                    // So, based on whether we can execute the compiler analyzer in a span based fashion or not,
                    // we order the execution of span based and document based analyzers accordingly. 
                    using var _ = PooledDictionary<DiagnosticAnalyzer, ImmutableArray<DiagnosticData>>.GetInstance(out var builder);
                    if (performSpanBasedAnalysisForCompilerAnalyzer)
                    {
                        await ExecuteSpanBasedAnalyzersAsync(spanBasedAnalyzers, oldMemberSpans, builder).ConfigureAwait(false);
                        await ExecuteDocumentBasedAnalyzersAsync(documentBasedAnalyzers, oldMemberSpans, builder).ConfigureAwait(false);
                    }
                    else
                    {
                        await ExecuteDocumentBasedAnalyzersAsync(documentBasedAnalyzers, oldMemberSpans, builder).ConfigureAwait(false);
                        await ExecuteSpanBasedAnalyzersAsync(spanBasedAnalyzers, oldMemberSpans, builder).ConfigureAwait(false);
                    }

                    return builder.ToImmutableDictionary();
                }
                finally
                {
                    // Finally, save the current member spans in the latest document so that the
                    // diagnostic computation for any subsequent member-only edits can be done incrementally.
                    SaveMemberSpans(document.Id, version, newMemberSpans);
                }

                async Task ExecuteSpanBasedAnalyzersAsync(
                    ArrayBuilder<(DiagnosticAnalyzer, DocumentAnalysisData)> analyzersAndExistingData,
                    ImmutableArray<TextSpan> oldMemberSpans,
                    PooledDictionary<DiagnosticAnalyzer, ImmutableArray<DiagnosticData>> builder)
                {
                    executor = executor.With(analysisScope.WithSpan(changedMember.FullSpan));
                    await ExecuteAnalyzerAsync(executor, analyzersAndExistingData, oldMemberSpans, builder).ConfigureAwait(false);
                }

                async Task ExecuteDocumentBasedAnalyzersAsync(
                    ArrayBuilder<(DiagnosticAnalyzer, DocumentAnalysisData)> analyzersAndExistingData,
                    ImmutableArray<TextSpan> oldMemberSpans,
                    PooledDictionary<DiagnosticAnalyzer, ImmutableArray<DiagnosticData>> builder)
                {
                    executor = executor.With(analysisScope.WithSpan(null));
                    await ExecuteAnalyzerAsync(executor, analyzersAndExistingData, oldMemberSpans, builder).ConfigureAwait(false);
                }

                async Task ExecuteAnalyzerAsync(
                    DocumentAnalysisExecutor executor,
                    ArrayBuilder<(DiagnosticAnalyzer, DocumentAnalysisData)> analyzersAndExistingData,
                    ImmutableArray<TextSpan> oldMemberSpans,
                    PooledDictionary<DiagnosticAnalyzer, ImmutableArray<DiagnosticData>> builder)
                {
                    var analysisScope = executor.AnalysisScope;

                    Debug.Assert(changedMember != null);
                    Debug.Assert(analysisScope.Kind == AnalysisKind.Semantic);

                    foreach (var (analyzer, existingData) in analyzersAndExistingData)
                    {
                        var diagnostics = await computeAnalyzerDiagnosticsAsync(analyzer, executor, cancellationToken).ConfigureAwait(false);

                        // If we computed the diagnostics just for a span, then we are performing incremental analysis.
                        // We need to compute the full document diagnostics by re-using diagnostics outside the changed
                        // member and using the above computed latest diagnostics for the edited member span.
                        if (analysisScope.Span.HasValue)
                        {
                            Debug.Assert(analysisScope.Span.Value == changedMember.FullSpan);

                            diagnostics = await GetUpdatedDiagnosticsForMemberEditAsync(
                                diagnostics, existingData, analyzer,
                                executor, changedMember, changedMemberId,
                                oldMemberSpans, computeAnalyzerDiagnosticsAsync, cancellationToken).ConfigureAwait(false);
                        }

                        builder.Add(analyzer, diagnostics);
                    }
                }
            }

            private async Task<(SyntaxNode changedMember, int changedMemberId, ImmutableArray<TextSpan> memberSpans, Document lastDocument)?> TryGetChangedMemberAsync(
                Document document,
                SyntaxNode root,
                CancellationToken cancellationToken)
            {
                if (!_lastDocumentWithCachedDiagnostics.TryGetTarget(out var lastDocument)
                    || lastDocument?.Id != document.Id)
                {
                    return null;
                }

                var documentDifferenceService = document.Project.LanguageServices.GetRequiredService<IDocumentDifferenceService>();
                var differenceResult = await documentDifferenceService.GetDifferenceAsync(lastDocument, document, cancellationToken).ConfigureAwait(false);
                if (differenceResult?.ChangedMember is not { } changedMember)
                {
                    return null;
                }

                var syntaxFacts = document.Project.LanguageServices.GetRequiredService<ISyntaxFactsService>();
                var members = syntaxFacts.GetMethodLevelMembers(root);
                var memberSpans = members.SelectAsArray(member => member.FullSpan);
                var changedMemberId = members.IndexOf(changedMember);
                return (changedMember, changedMemberId, memberSpans, lastDocument);
            }

            private static async Task<ImmutableArray<DiagnosticData>> GetUpdatedDiagnosticsForMemberEditAsync(
                ImmutableArray<DiagnosticData> diagnostics,
                DocumentAnalysisData existingData,
                DiagnosticAnalyzer analyzer,
                DocumentAnalysisExecutor executor,
                SyntaxNode changedMember,
                int changedMemberId,
                ImmutableArray<TextSpan> oldMemberSpans,
                Func<DiagnosticAnalyzer, DocumentAnalysisExecutor, CancellationToken, Task<ImmutableArray<DiagnosticData>>> computeAnalyzerDiagnosticsAsync,
                CancellationToken cancellationToken)
            {
                // We are performing semantic span-based analysis for member-only edit scenario.
                // Instead of computing the analyzer diagnostics for the entire document,
                // we have computed the new diagnostics just for the edited member span.
                Debug.Assert(executor.AnalysisScope.Span.HasValue);
                Debug.Assert(executor.AnalysisScope.Span.Value == changedMember.FullSpan);
                Debug.Assert(diagnostics.All(d => !d.HasTextSpan || changedMember.FullSpan.IntersectsWith(d.GetTextSpan())));

                // We now try to get the new document diagnostics by performing an incremental update:
                //   1. Re-using all the old cached diagnostics outside the edited member node from a prior
                //      document snapshot, but with updated diagnostic spans.
                //      AND
                //   2. Replacing old diagnostics for the edited member node in a prior document snapshot
                //      with the new diagnostics for this member node in the latest document snaphot.
                // If we are unable to perform this incremental diagnostics update,
                // we fallback to computing the diagnostics for the entire document.
                if (TryGetUpdatedDocumentDiagnostics(existingData, oldMemberSpans, diagnostics, changedMember.SyntaxTree, changedMember, changedMemberId, out var updatedDiagnostics))
                {
#if DEBUG_INCREMENTAL_ANALYSIS
                    await ValidateMemberDiagnosticsAsync(executor, analyzer, updatedDiagnostics, cancellationToken).ConfigureAwait(false);
#endif
                    return updatedDiagnostics;
                }
                else
                {
                    // Incremental diagnostics update failed.
                    // Fallback to computing the diagnostics for the entire document.
                    var documentExecutor = executor.With(executor.AnalysisScope.WithSpan(null));
                    return await computeAnalyzerDiagnosticsAsync(analyzer, documentExecutor, cancellationToken).ConfigureAwait(false);
                }

#if DEBUG_INCREMENTAL_ANALYSIS
                static async Task ValidateMemberDiagnosticsAsync(DocumentAnalysisExecutor executor, DiagnosticAnalyzer analyzer, ImmutableArray<DiagnosticData> diagnostics, CancellationToken cancellationToken)
                {
                    executor = executor.With(executor.AnalysisScope.WithSpan(null));
                    var expected = await executor.ComputeDiagnosticsAsync(analyzer, cancellationToken).ConfigureAwait(false);
                    Debug.Assert(diagnostics.SetEquals(expected));
                }
#endif
            }

            private static bool TryGetUpdatedDocumentDiagnostics(
                DocumentAnalysisData existingData, ImmutableArray<TextSpan> oldMemberSpans, ImmutableArray<DiagnosticData> memberDiagnostics,
                SyntaxTree tree, SyntaxNode member, int memberId, out ImmutableArray<DiagnosticData> updatedDiagnostics)
            {
                // get old span
                var oldSpan = oldMemberSpans[memberId];

                // get old diagnostics
                var diagnostics = existingData.Items;

                // check quick exit cases
                if (diagnostics.Length == 0 && memberDiagnostics.Length == 0)
                {
                    updatedDiagnostics = diagnostics;
                    return true;
                }

                // simple case
                if (diagnostics.Length == 0 && memberDiagnostics.Length > 0)
                {
                    updatedDiagnostics = memberDiagnostics;
                    return true;
                }

                // regular case
                using var _ = ArrayBuilder<DiagnosticData>.GetInstance(out var resultBuilder);

                // update member location
                Contract.ThrowIfFalse(member.FullSpan.Start == oldSpan.Start);
                var delta = member.FullSpan.End - oldSpan.End;

                var replaced = false;
                foreach (var diagnostic in diagnostics)
                {
                    if (!diagnostic.HasTextSpan)
                    {
                        resultBuilder.Add(diagnostic);
                        continue;
                    }

                    var diagnosticSpan = diagnostic.GetTextSpan();
                    if (diagnosticSpan.Start < oldSpan.Start)
                    {
                        // Bail out if the diagnostic has any additional locations that we don't know how to handle.
                        if (diagnostic.AdditionalLocations.Any(l => l.SourceSpan.HasValue && l.SourceSpan.Value.Start >= oldSpan.Start))
                        {
                            updatedDiagnostics = default;
                            return false;
                        }

                        resultBuilder.Add(diagnostic);
                        continue;
                    }

                    if (!replaced)
                    {
                        resultBuilder.AddRange(memberDiagnostics);
                        replaced = true;
                    }

                    if (oldSpan.End <= diagnosticSpan.Start)
                    {
                        // Bail out if the diagnostic has any additional locations that we don't know how to handle.
                        if (diagnostic.AdditionalLocations.Any(l => l.SourceSpan.HasValue && oldSpan.End > l.SourceSpan.Value.Start))
                        {
                            updatedDiagnostics = default;
                            return false;
                        }

                        resultBuilder.Add(UpdateLocations(diagnostic, tree, delta));
                        continue;
                    }
                }

                // if it haven't replaced, replace it now
                if (!replaced)
                {
                    resultBuilder.AddRange(memberDiagnostics);
                }

                updatedDiagnostics = resultBuilder.ToImmutableArray();
                return true;

                static DiagnosticData UpdateLocations(DiagnosticData diagnostic, SyntaxTree tree, int delta)
                {
                    Debug.Assert(diagnostic.DataLocation != null);
                    var location = UpdateLocation(diagnostic.DataLocation);
                    var additionalLocations = diagnostic.AdditionalLocations.SelectAsArray(UpdateLocation);
                    return diagnostic.WithLocations(location, additionalLocations);

                    DiagnosticDataLocation UpdateLocation(DiagnosticDataLocation location)
                    {
                        // Do not need to update additional locations without source span
                        if (!location.SourceSpan.HasValue)
                            return location;

                        var diagnosticSpan = location.SourceSpan.Value;
                        var start = Math.Min(Math.Max(diagnosticSpan.Start + delta, 0), tree.Length);
                        var newSpan = new TextSpan(start, start >= tree.Length ? 0 : diagnosticSpan.Length);
                        return location.WithSpan(newSpan, tree);
                    }
                }
            }
        }
    }
}
