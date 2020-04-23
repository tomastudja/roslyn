﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Rename.ConflictEngine;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Rename
{
    internal interface IRemoteRenamer
    {
        Task<SerializableRenameLocations> FindRenameLocationsAsync(
            PinnedSolutionInfo solutionInfo,
            SerializableSymbolAndProjectId symbol,
            SerializableRenameOptionSet options,
            CancellationToken cancellationToken);

        Task<SerializableConflictResolution> ResolveConflictsAsync(
            PinnedSolutionInfo solutionInfo,
            SerializableRenameLocations renameLocationSet,
            string replacementText,
            SerializableSymbolAndProjectId[] nonConflictSymbols,
            CancellationToken cancellationToken);
    }

    internal struct SerializableRenameOptionSet
    {
        public bool RenameOverloads;
        public bool RenameInStrings;
        public bool RenameInComments;
        public bool RenameFile;

        public static SerializableRenameOptionSet Dehydrate(RenameOptionSet optionSet)
            => new SerializableRenameOptionSet
            {
                RenameOverloads = optionSet.RenameOverloads,
                RenameInStrings = optionSet.RenameInStrings,
                RenameInComments = optionSet.RenameInComments,
                RenameFile = optionSet.RenameFile,
            };

        public RenameOptionSet Rehydrate()
            => new RenameOptionSet(RenameOverloads, RenameInStrings, RenameInComments, RenameFile);
    }

    internal class SerializableSearchResult
    {
        // We use arrays so we can represent default immutable arrays.

        public SerializableRenameLocation[] Locations;
        public SerializableReferenceLocation[] ImplicitLocations;
        public SerializableSymbolAndProjectId[] ReferencedSymbols;

        public static SerializableSearchResult Dehydrate(Solution solution, RenameLocations.SearchResult result, CancellationToken cancellationToken)
            => result == null ? null : new SerializableSearchResult
            {
                Locations = result.Locations?.Select(loc => SerializableRenameLocation.Dehydrate(loc)).ToArray(),
                ImplicitLocations = result.ImplicitLocations.IsDefault ? null : result.ImplicitLocations.Select(loc => SerializableReferenceLocation.Dehydrate(loc, cancellationToken)).ToArray(),
                ReferencedSymbols = result.ReferencedSymbols.IsDefault ? null : result.ReferencedSymbols.Select(s => SerializableSymbolAndProjectId.Dehydrate(solution, s, cancellationToken)).ToArray(),
            };

        public async Task<RenameLocations.SearchResult> RehydrateAsync(Solution solution, CancellationToken cancellationToken)
        {
            ImmutableHashSet<RenameLocation> locations = null;
            ImmutableArray<ReferenceLocation> implicitLocations = default;
            ImmutableArray<ISymbol> referencedSymbols = default;

            if (Locations != null)
            {
                using var _ = ArrayBuilder<RenameLocation>.GetInstance(Locations.Length, out var builder);
                foreach (var loc in Locations)
                    builder.Add(await loc.RehydrateAsync(solution, cancellationToken).ConfigureAwait(false));

                locations = builder.ToImmutableHashSet();
            }

            if (ImplicitLocations != null)
            {
                using var _ = ArrayBuilder<ReferenceLocation>.GetInstance(ImplicitLocations.Length, out var builder);
                foreach (var loc in ImplicitLocations)
                    builder.Add(await loc.RehydrateAsync(solution, cancellationToken).ConfigureAwait(false));

                implicitLocations = builder.ToImmutable();
            }

            if (ReferencedSymbols != null)
            {
                using var _ = ArrayBuilder<ISymbol>.GetInstance(ReferencedSymbols.Length, out var builder);
                foreach (var symbol in ReferencedSymbols)
                    builder.AddIfNotNull(await symbol.TryRehydrateAsync(solution, cancellationToken).ConfigureAwait(false));

                referencedSymbols = builder.ToImmutable();
            }

            return new RenameLocations.SearchResult(locations, implicitLocations, referencedSymbols);
        }
    }

    internal struct SerializableRenameLocation
    {
        public TextSpan Location;
        public DocumentId DocumentId;
        public CandidateReason CandidateReason;
        public bool IsRenamableAliasUsage;
        public bool IsRenamableAccessor;
        public TextSpan ContainingLocationForStringOrComment;
        public bool IsWrittenTo;

        public static SerializableRenameLocation Dehydrate(RenameLocation location)
            => new SerializableRenameLocation
            {
                Location = location.Location.SourceSpan,
                DocumentId = location.DocumentId,
                CandidateReason = location.CandidateReason,
                IsRenamableAliasUsage = location.IsRenamableAliasUsage,
                IsRenamableAccessor = location.IsRenamableAccessor,
                ContainingLocationForStringOrComment = location.ContainingLocationForStringOrComment,
                IsWrittenTo = location.IsWrittenTo,
            };

        public async Task<RenameLocation> RehydrateAsync(Solution solution, CancellationToken cancellation)
        {
            var document = solution.GetDocument(DocumentId);
            var tree = await document.GetSyntaxTreeAsync(cancellation).ConfigureAwait(false);

            return new RenameLocation(
                CodeAnalysis.Location.Create(tree, Location),
                DocumentId,
                CandidateReason,
                IsRenamableAliasUsage,
                IsRenamableAccessor,
                IsWrittenTo,
                ContainingLocationForStringOrComment);
        }
    }

    internal partial class RenameLocations
    {
        public SerializableRenameLocations Dehydrate(Solution solution, CancellationToken cancellationToken)
            => new SerializableRenameLocations
            {
                Symbol = SerializableSymbolAndProjectId.Dehydrate(solution, Symbol, cancellationToken),
                Options = SerializableRenameOptionSet.Dehydrate(Options),
                OriginalSymbolResult = SerializableSearchResult.Dehydrate(solution, _originalSymbolResult, cancellationToken),
                MergedResult = SerializableSearchResult.Dehydrate(solution, _mergedResult, cancellationToken),
                OverloadsResult = _overloadsResult.IsDefault ? null : _overloadsResult.Select(r => SerializableSearchResult.Dehydrate(solution, r, cancellationToken)).ToArray(),
                StringsResult = _stringsResult.IsDefault ? null : _stringsResult.Select(r => SerializableRenameLocation.Dehydrate(r)).ToArray(),
                CommentsResult = _commentsResult.IsDefault ? null : _commentsResult.Select(r => SerializableRenameLocation.Dehydrate(r)).ToArray(),
            };

        internal static async Task<RenameLocations> RehydrateAsync(Solution solution, SerializableRenameLocations locations, CancellationToken cancellationToken)
        {
            if (locations == null)
                return null;

            var symbol = await locations.Symbol.TryRehydrateAsync(solution, cancellationToken).ConfigureAwait(false);
            if (symbol == null)
                return null;

            ImmutableArray<SearchResult> overloadsResult = default;
            ImmutableArray<RenameLocation> stringsResult = default;
            ImmutableArray<RenameLocation> commentsResult = default;

            if (locations.OverloadsResult != null)
            {
                using var _ = ArrayBuilder<SearchResult>.GetInstance(locations.OverloadsResult.Length, out var builder);
                foreach (var res in locations.OverloadsResult)
                    builder.Add(await res.RehydrateAsync(solution, cancellationToken).ConfigureAwait(false));

                overloadsResult = builder.ToImmutable();
            }

            if (locations.StringsResult != null)
            {
                using var _ = ArrayBuilder<RenameLocation>.GetInstance(locations.StringsResult.Length, out var builder);
                foreach (var res in locations.StringsResult)
                    builder.Add(await res.RehydrateAsync(solution, cancellationToken).ConfigureAwait(false));

                stringsResult = builder.ToImmutable();
            }

            if (locations.CommentsResult != null)
            {
                using var _ = ArrayBuilder<RenameLocation>.GetInstance(locations.CommentsResult.Length, out var builder);
                foreach (var res in locations.CommentsResult)
                    builder.Add(await res.RehydrateAsync(solution, cancellationToken).ConfigureAwait(false));

                commentsResult = builder.ToImmutable();
            }

            return new RenameLocations(
                symbol,
                solution,
                locations.Options.Rehydrate(),
                await locations.OriginalSymbolResult.RehydrateAsync(solution, cancellationToken).ConfigureAwait(false),
                await locations.MergedResult.RehydrateAsync(solution, cancellationToken).ConfigureAwait(false),
                overloadsResult,
                stringsResult,
                commentsResult);
        }
    }

    internal class SerializableRenameLocations
    {
        public SerializableSymbolAndProjectId Symbol;
        public SerializableRenameOptionSet Options;
        public SerializableSearchResult OriginalSymbolResult;
        public SerializableSearchResult MergedResult;

        // We use arrays so we can represent default immutable arrays.
        public SerializableSearchResult[] OverloadsResult;
        public SerializableRenameLocation[] StringsResult;
        public SerializableRenameLocation[] CommentsResult;
    }

    internal class SerializableComplexifiedSpan
    {
        public TextSpan OriginalSpan;
        public TextSpan NewSpan;
        public ImmutableArray<(TextSpan oldSpan, TextSpan newSpan)> ModifiedSubSpans;

        public ComplexifiedSpan Rehydrate()
            => new ComplexifiedSpan(OriginalSpan, NewSpan, ModifiedSubSpans);

        public static SerializableComplexifiedSpan Dehydrate(ComplexifiedSpan span)
            => new SerializableComplexifiedSpan
            {
                OriginalSpan = span.OriginalSpan,
                NewSpan = span.NewSpan,
                ModifiedSubSpans = span.ModifiedSubSpans,
            };
    }

    internal class SerializableRelatedLocation
    {
        public TextSpan ConflictCheckSpan;
        public RelatedLocationType Type;
        public bool IsReference;
        public DocumentId DocumentId;
        public TextSpan ComplexifiedTargetSpan;

        public RelatedLocation Rehydrate()
            => new RelatedLocation(ConflictCheckSpan, DocumentId, Type, IsReference, ComplexifiedTargetSpan);

        public static SerializableRelatedLocation Dehydrate(RelatedLocation location)
            => new SerializableRelatedLocation
            {
                ConflictCheckSpan = location.ConflictCheckSpan,
                Type = location.Type,
                IsReference = location.IsReference,
                DocumentId = location.DocumentId,
                ComplexifiedTargetSpan = location.ComplexifiedTargetSpan,
            };
    }

    internal class SerializableConflictResolution
    {
        public string ErrorMessage;

        public bool ReplacementTextValid;

        public (DocumentId documentId, string newName) RenamedDocument;

        public ImmutableArray<DocumentId> DocumentIds;
        public ImmutableArray<SerializableRelatedLocation> RelatedLocations;
        public ImmutableArray<(DocumentId, ImmutableArray<TextChange>)> DocumentToTextChanges;
        public ImmutableArray<(DocumentId, ImmutableArray<(TextSpan oldSpan, TextSpan newSpan)>)> DocumentToModifiedSpansMap;
        public ImmutableArray<(DocumentId, ImmutableArray<SerializableComplexifiedSpan>)> DocumentToComplexifiedSpansMap;
        public ImmutableArray<(DocumentId, ImmutableArray<SerializableRelatedLocation>)> DocumentToRelatedLocationsMap;

        public async Task<ConflictResolution> RehydrateAsync(Solution oldSolution, CancellationToken cancellationToken)
        {
            if (ErrorMessage != null)
                return new ConflictResolution(ErrorMessage);

            return new ConflictResolution(
                oldSolution,
                await CreateNewSolutionAsync(oldSolution, cancellationToken).ConfigureAwait(false),
                ReplacementTextValid,
                RenamedDocument,
                DocumentIds,
                RelatedLocations.SelectAsArray(loc => loc.Rehydrate()),
                DocumentToModifiedSpansMap.ToImmutableDictionary(t => t.Item1, t => t.Item2),
                DocumentToComplexifiedSpansMap.ToImmutableDictionary(t => t.Item1, t => t.Item2.SelectAsArray(c => c.Rehydrate())),
                DocumentToRelatedLocationsMap.ToImmutableDictionary(t => t.Item1, t => t.Item2.SelectAsArray(c => c.Rehydrate())));
        }

        private async Task<Solution> CreateNewSolutionAsync(Solution oldSolution, CancellationToken cancellationToken)
        {
            var currentSolution = oldSolution;
            foreach (var (docId, textChanges) in this.DocumentToTextChanges)
            {
                var text = await oldSolution.GetDocument(docId).GetTextAsync(cancellationToken).ConfigureAwait(false);
                currentSolution = currentSolution.WithDocumentText(docId, text.WithChanges(textChanges));
            }

            return currentSolution;
        }
    }

    internal partial struct ConflictResolution
    {
        public async Task<SerializableConflictResolution> DehydrateAsync(CancellationToken cancellationToken)
        {
            if (ErrorMessage != null)
                return new SerializableConflictResolution { ErrorMessage = ErrorMessage };

            return new SerializableConflictResolution
            {
                ReplacementTextValid = ReplacementTextValid,
                RenamedDocument = _renamedDocument,
                DocumentIds = DocumentIds,
                RelatedLocations = RelatedLocations.SelectAsArray(loc => SerializableRelatedLocation.Dehydrate(loc)),
                DocumentToTextChanges = await GetDocumentToTextChangesAsync(cancellationToken).ConfigureAwait(false),
                DocumentToModifiedSpansMap = _documentToModifiedSpansMap.SelectAsArray(kvp => (kvp.Key, kvp.Value)),
                DocumentToComplexifiedSpansMap = _documentToComplexifiedSpansMap.SelectAsArray(kvp => (kvp.Key, kvp.Value.SelectAsArray(s => SerializableComplexifiedSpan.Dehydrate(s)))),
                DocumentToRelatedLocationsMap = _documentToRelatedLocationsMap.SelectAsArray(kvp => (kvp.Key, kvp.Value.SelectAsArray(s => SerializableRelatedLocation.Dehydrate(s)))),
            };
        }
    }
}
