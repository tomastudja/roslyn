﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Debugging;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using System.IO;
using System.Text;

namespace Microsoft.CodeAnalysis.EditAndContinue
{
    /// <summary>
    /// Encapsulates access to the last committed solution.
    /// We don't want to expose the solution directly since access to documents must be gated by out-of-sync checks.
    /// </summary>
    internal sealed class CommittedSolution
    {
        private readonly DebuggingSession _debuggingSession;

        private Solution _solution;

        internal enum DocumentState
        {
            None = 0,

            /// <summary>
            /// The current document content does not match the content the module was compiled with.
            /// This document state may change to <see cref="MatchesBuildOutput"/> or <see cref="DesignTimeOnly"/>.
            /// </summary>
            OutOfSync = 1,

            /// <summary>
            /// It hasn't been possible to determine whether the current document content does matches the content 
            /// the module was compiled with due to error while reading the PDB or the source file.
            /// This document state may change to <see cref="MatchesBuildOutput"/> or <see cref="DesignTimeOnly"/>.
            /// </summary>
            Indeterminate = 2,

            /// <summary>
            /// The document is not compiled into the module. It's only included in the project
            /// to support design-time features such as completion, etc.
            /// This is a final state. Once a document is in this state it won't switch to a different one.
            /// </summary>
            DesignTimeOnly = 3,

            /// <summary>
            /// The current document content matches the content the built module was compiled with.
            /// This is a final state. Once a document is in this state it won't switch to a different one.
            /// </summary>
            MatchesBuildOutput = 4
        }

        /// <summary>
        /// Implements workaround for https://github.com/dotnet/project-system/issues/5457.
        /// 
        /// When debugging is started we capture the current solution snapshot.
        /// The documents in this snapshot might not match exactly to those that the compiler used to build the module 
        /// that's currently loaded into the debuggee. This is because there is no reliable synchronization between
        /// the (design-time) build and Roslyn workspace. Although Roslyn uses file-watchers to watch for changes in 
        /// the files on disk, the file-changed events raised by the build might arrive to Roslyn after the debugger
        /// has attached to the debuggee and EnC service captured the solution.
        /// 
        /// Ideally, the Project System would notify Roslyn at the end of each build what the content of the source
        /// files generated by various targets is. Roslyn would then apply these changes to the workspace and 
        /// the EnC service would capture a solution snapshot that includes these changes.
        /// 
        /// Since this notification is currently not available we check the current content of source files against
        /// the corresponding checksums stored in the PDB. Documents for which we have not observed source file content 
        /// that maches the PDB checksum are considered <see cref="DocumentState.OutOfSync"/>. 
        /// 
        /// Some documents in the workspace are added for design-time-only purposes and are not part of the compilation
        /// from which the assembly is built. These documents won't have a record in the PDB and will be tracked as 
        /// <see cref="DocumentState.DesignTimeOnly"/>.
        /// 
        /// A document state can only change from <see cref="DocumentState.OutOfSync"/> to <see cref="DocumentState.MatchesBuildOutput"/>.
        /// Once a document state is <see cref="DocumentState.MatchesBuildOutput"/> or <see cref="DocumentState.DesignTimeOnly"/>
        /// it will never change.
        /// </summary>
        private readonly Dictionary<DocumentId, DocumentState> _documentState;

        private readonly object _guard = new();

        public CommittedSolution(DebuggingSession debuggingSession, Solution solution)
        {
            _solution = solution;
            _debuggingSession = debuggingSession;
            _documentState = new Dictionary<DocumentId, DocumentState>();
        }

        // test only
        internal void Test_SetDocumentState(DocumentId documentId, DocumentState state)
        {
            lock (_guard)
            {
                _documentState[documentId] = state;
            }
        }

        public bool HasNoChanges(Solution solution)
            => _solution == solution;

        public Project? GetProject(ProjectId id)
            => _solution.GetProject(id);

        public ImmutableArray<DocumentId> GetDocumentIdsWithFilePath(string path)
            => _solution.GetDocumentIdsWithFilePath(path);

        public bool ContainsDocument(DocumentId documentId)
            => _solution.ContainsDocument(documentId);

        /// <summary>
        /// Captures the content of a file that is about to be overwritten by saving an open document,
        /// if the document is currently out-of-sync and the content of the file matches the PDB.
        /// If we didn't capture the content before the save we might never be able to find a document
        /// snapshot that matches the PDB.
        /// </summary>
        public Task OnSourceFileUpdatedAsync(DocumentId documentId, CancellationToken cancellationToken)
            => GetDocumentAndStateAsync(documentId, cancellationToken, reloadOutOfSyncDocument: true);

        /// <summary>
        /// Returns a document snapshot for given <see cref="DocumentId"/> whose content exactly matches
        /// the source file used to compile the binary currently loaded in the debuggee. Returns null
        /// if it fails to find a document snapshot whose content hash maches the one recorded in the PDB.
        /// 
        /// The result is cached and the next lookup uses the cached value, including failures unless <paramref name="reloadOutOfSyncDocument"/> is true.
        /// </summary>
        public async Task<(Document? Document, DocumentState State)> GetDocumentAndStateAsync(DocumentId documentId, CancellationToken cancellationToken, bool reloadOutOfSyncDocument = false)
        {
            Document? document;

            lock (_guard)
            {
                document = _solution.GetDocument(documentId);
                if (document == null)
                {
                    return (null, DocumentState.None);
                }

                if (_documentState.TryGetValue(documentId, out var documentState))
                {
                    switch (documentState)
                    {
                        case DocumentState.MatchesBuildOutput:
                            return (document, documentState);

                        case DocumentState.DesignTimeOnly:
                            return (null, documentState);

                        case DocumentState.OutOfSync:
                            if (reloadOutOfSyncDocument)
                            {
                                break;
                            }

                            return (null, documentState);

                        case DocumentState.Indeterminate:
                            // Previous attempt resulted in a read error. Try again.
                            break;

                        case DocumentState.None:
                            throw ExceptionUtilities.Unreachable;
                    }
                }

                if (!PathUtilities.IsAbsolute(document.FilePath))
                {
                    return (null, DocumentState.DesignTimeOnly);
                }
            }

            var sourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

            var (matchingSourceText, pdbHasDocument) = await Task.Run(
                () => TryGetPdbMatchingSourceText(document.FilePath, sourceText.Encoding, document.Project),
                cancellationToken).ConfigureAwait(false);

            lock (_guard)
            {
                // only listed document states can be changed:
                if (_documentState.TryGetValue(documentId, out var documentState) &&
                    documentState != DocumentState.OutOfSync &&
                    documentState != DocumentState.Indeterminate)
                {
                    return (document, documentState);
                }

                DocumentState newState;
                Document? matchingDocument;

                if (pdbHasDocument == null)
                {
                    // Unable to determine due to error reading the PDB or the source file.
                    return (document, DocumentState.Indeterminate);
                }

                if (pdbHasDocument == false)
                {
                    // Source file is not listed in the PDB (e.g. WPF .g.i.cs files).
                    matchingDocument = null;
                    newState = DocumentState.DesignTimeOnly;
                }
                else if (matchingSourceText != null)
                {
                    if (sourceText.ContentEquals(matchingSourceText))
                    {
                        matchingDocument = document;
                    }
                    else
                    {
                        _solution = _solution.WithDocumentText(documentId, matchingSourceText, PreservationMode.PreserveValue);
                        matchingDocument = _solution.GetDocument(documentId);
                    }

                    newState = DocumentState.MatchesBuildOutput;
                }
                else
                {
                    matchingDocument = null;
                    newState = DocumentState.OutOfSync;
                }

                _documentState[documentId] = newState;
                return (matchingDocument, newState);
            }
        }

        public void CommitSolution(Solution solution)
        {
            lock (_guard)
            {
                _solution = solution;
            }
        }

        private (SourceText? Source, bool? HasDocument) TryGetPdbMatchingSourceText(string sourceFilePath, Encoding? encoding, Project project)
        {
            var hasDocument = TryReadSourceFileChecksumFromPdb(sourceFilePath, project, out var symChecksum, out var algorithm);
            if (hasDocument != true)
            {
                return (Source: null, hasDocument);
            }

            try
            {
                using var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

                // We must use the encoding of the document as determined by the IDE (the editor).
                // This might differ from the encoding that the compiler chooses, so if we just relied on the compiler we 
                // might end up updating the committed solution with a document that has a different encoding than 
                // the one that's in the workspace, resulting in false document changes when we compare the two.
                var sourceText = SourceText.From(fileStream, encoding, checksumAlgorithm: algorithm);
                var fileChecksum = sourceText.GetChecksum();

                if (fileChecksum.SequenceEqual(symChecksum))
                {
                    return (sourceText, hasDocument);
                }

                EditAndContinueWorkspaceService.Log.Write("Checksum differs for source file '{0}'", sourceFilePath);
                return (Source: null, hasDocument);
            }
            catch (Exception e)
            {
                EditAndContinueWorkspaceService.Log.Write("Error calculating checksum for source file '{0}': '{1}'", sourceFilePath, e.Message);
                return (Source: null, HasDocument: null);
            }
        }

        /// <summary>
        /// Returns true if the PDB contains a document record for given <paramref name="sourceFilePath"/>,
        /// in which case <paramref name="checksum"/> and <paramref name="algorithm"/> contain its checksum.
        /// False if the document is not found in the PDB.
        /// Null if it can't be determined because the PDB is not available or an error occurred while reading the PDB.
        /// </summary>
        private bool? TryReadSourceFileChecksumFromPdb(string sourceFilePath, Project project, out ImmutableArray<byte> checksum, out SourceHashAlgorithm algorithm)
        {
            checksum = default;
            algorithm = default;

            try
            {
                var compilationOutputs = _debuggingSession.GetCompilationOutputs(project);

                DebugInformationReaderProvider? debugInfoReaderProvider;
                try
                {
                    debugInfoReaderProvider = compilationOutputs.OpenPdb();
                }
                catch (Exception e)
                {
                    EditAndContinueWorkspaceService.Log.Write("Source '{0}' doesn't match output PDB: error opening PDB '{1}': {2}", sourceFilePath, compilationOutputs.PdbDisplayPath, e.Message);
                    debugInfoReaderProvider = null;
                }

                if (debugInfoReaderProvider == null)
                {
                    EditAndContinueWorkspaceService.Log.Write("Source '{0}' doesn't match output PDB: PDB '{1}' not found", sourceFilePath, compilationOutputs.PdbDisplayPath);
                    return null;
                }

                try
                {
                    var debugInfoReader = debugInfoReaderProvider.CreateEditAndContinueMethodDebugInfoReader();
                    if (!debugInfoReader.TryGetDocumentChecksum(sourceFilePath, out checksum, out var algorithmId))
                    {
                        EditAndContinueWorkspaceService.Log.Write("Source '{0}' doesn't match output PDB: no document", sourceFilePath);
                        return false;
                    }

                    algorithm = SourceHashAlgorithms.GetSourceHashAlgorithm(algorithmId);
                    if (algorithm == SourceHashAlgorithm.None)
                    {
                        // This can only happen if the PDB was post-processed by a misbehaving tool.
                        EditAndContinueWorkspaceService.Log.Write("Source '{0}' doesn't match PDB: unknown checksum alg", sourceFilePath);
                    }

                    return true;
                }
                catch (Exception e)
                {
                    EditAndContinueWorkspaceService.Log.Write("Source '{0}' doesn't match output PDB: error reading symbols: {1}", sourceFilePath, e.Message);
                }
                finally
                {
                    debugInfoReaderProvider.Dispose();
                }
            }
            catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e))
            {
                EditAndContinueWorkspaceService.Log.Write("Source '{0}' doesn't match PDB: unexpected exception: {1}", sourceFilePath, e.Message);
            }

            return null;
        }
    }
}
