﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.PersistentStorage;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    internal sealed partial class SyntaxTreeIndex : IObjectWritable
    {
        private const string PersistenceName = "<SyntaxTreeIndex>";
        private static readonly Checksum SerializationFormatChecksum = Checksum.Create("23");

        public readonly Checksum? Checksum;

        private static Task<SyntaxTreeIndex?> LoadAsync(Document document, Checksum checksum, CancellationToken cancellationToken)
            => LoadAsync(document.Project.Solution.Workspace, DocumentKey.ToDocumentKey(document), checksum, GetStringTable(document.Project), cancellationToken);

        public static async Task<SyntaxTreeIndex?> LoadAsync(
            Workspace workspace, DocumentKey documentKey, Checksum? checksum, StringTable stringTable, CancellationToken cancellationToken)
        {
            try
            {
                var persistentStorageService = (IChecksummedPersistentStorageService)workspace.Services.GetRequiredService<IPersistentStorageService>();

                var storage = await persistentStorageService.GetStorageAsync(
                    workspace, documentKey.Project.Solution, checkBranchId: false, cancellationToken).ConfigureAwait(false);
                await using var _ = storage.ConfigureAwait(false);

                // attempt to load from persisted state
                using var stream = await storage.ReadStreamAsync(documentKey, PersistenceName, checksum, cancellationToken).ConfigureAwait(false);
                using var reader = ObjectReader.TryGetReader(stream, cancellationToken: cancellationToken);
                if (reader != null)
                    return ReadFrom(stringTable, reader, checksum);
            }
            catch (Exception e) when (IOUtilities.IsNormalIOException(e))
            {
                // Storage APIs can throw arbitrary exceptions.
            }

            return null;
        }

        public static async Task<Checksum> GetChecksumAsync(
            Document document, CancellationToken cancellationToken)
        {
            // Since we build the SyntaxTreeIndex from a SyntaxTree, we need our checksum to change
            // any time the SyntaxTree could have changed.  Right now, that can only happen if the
            // text of the document changes, or the ParseOptions change.  So we get the checksums
            // for both of those, and merge them together to make the final checksum.
            //
            // We also want the checksum to change any time our serialization format changes.  If
            // the format has changed, all previous versions should be invalidated.
            var project = document.Project;
            var parseOptionsChecksum = project.State.GetParseOptionsChecksum();

            var documentChecksumState = await document.State.GetStateChecksumsAsync(cancellationToken).ConfigureAwait(false);
            var textChecksum = documentChecksumState.Text;

            return Checksum.Create(textChecksum, parseOptionsChecksum, SerializationFormatChecksum);
        }

        private async Task<bool> SaveAsync(
            Document document, CancellationToken cancellationToken)
        {
            var solution = document.Project.Solution;
            var persistentStorageService = (IChecksummedPersistentStorageService)solution.Workspace.Services.GetRequiredService<IPersistentStorageService>();

            try
            {
                var storage = await persistentStorageService.GetStorageAsync(solution, checkBranchId: false, cancellationToken).ConfigureAwait(false);
                await using var _ = storage.ConfigureAwait(false);
                using var stream = SerializableBytes.CreateWritableStream();

                using (var writer = new ObjectWriter(stream, leaveOpen: true, cancellationToken))
                {
                    WriteTo(writer);
                }

                stream.Position = 0;
                return await storage.WriteStreamAsync(document, PersistenceName, stream, this.Checksum, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (IOUtilities.IsNormalIOException(e))
            {
                // Storage APIs can throw arbitrary exceptions.
            }

            return false;
        }

        private static async Task<bool> PrecalculatedAsync(
            Document document, Checksum checksum, CancellationToken cancellationToken)
        {
            var solution = document.Project.Solution;
            var persistentStorageService = (IChecksummedPersistentStorageService)solution.Workspace.Services.GetRequiredService<IPersistentStorageService>();

            // check whether we already have info for this document
            try
            {
                var storage = await persistentStorageService.GetStorageAsync(solution, checkBranchId: false, cancellationToken).ConfigureAwait(false);
                await using var _ = storage.ConfigureAwait(false);
                // Check if we've already stored a checksum and it matches the checksum we 
                // expect.  If so, we're already precalculated and don't have to recompute
                // this index.  Otherwise if we don't have a checksum, or the checksums don't
                // match, go ahead and recompute it.
                return await storage.ChecksumMatchesAsync(document, PersistenceName, checksum, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (IOUtilities.IsNormalIOException(e))
            {
                // Storage APIs can throw arbitrary exceptions.
            }

            return false;
        }

        bool IObjectWritable.ShouldReuseInSerialization => true;

        public void WriteTo(ObjectWriter writer)
        {
            _literalInfo.WriteTo(writer);
            _identifierInfo.WriteTo(writer);
            _contextInfo.WriteTo(writer);
            _declarationInfo.WriteTo(writer);
            _extensionMethodInfo.WriteTo(writer);
        }

        private static SyntaxTreeIndex? ReadFrom(
            StringTable stringTable, ObjectReader reader, Checksum? checksum)
        {
            var literalInfo = LiteralInfo.TryReadFrom(reader);
            var identifierInfo = IdentifierInfo.TryReadFrom(reader);
            var contextInfo = ContextInfo.TryReadFrom(reader);
            var declarationInfo = DeclarationInfo.TryReadFrom(stringTable, reader);
            var extensionMethodInfo = ExtensionMethodInfo.TryReadFrom(reader);

            if (literalInfo == null || identifierInfo == null || contextInfo == null || declarationInfo == null || extensionMethodInfo == null)
            {
                return null;
            }

            return new SyntaxTreeIndex(
                checksum, literalInfo.Value, identifierInfo.Value, contextInfo.Value, declarationInfo.Value, extensionMethodInfo.Value);
        }
    }
}
