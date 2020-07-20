﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Execution;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Serialization
{
    /// <summary>
    /// serialize and deserialize objects to stream.
    /// some of these could be moved into actual object, but putting everything here is a bit easier to find I believe.
    /// </summary>
    internal partial class SerializerService
    {
        public void SerializeSourceText(ITemporaryStorageWithName? storage, SourceText text, ObjectWriter writer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteInt32((int)text.ChecksumAlgorithm);
            writer.WriteEncoding(text.Encoding);

            // TODO: refactor this part in its own abstraction (Bits) that has multiple sub types
            //       rather than using enums
            if (storage != null && storage.Name != null)
            {
                writer.WriteInt32((int)SerializationKinds.MemoryMapFile);
                writer.WriteString(storage.Name);
                writer.WriteInt64(storage.Offset);
                writer.WriteInt64(storage.Size);
                return;
            }

            writer.WriteInt32((int)SerializationKinds.Bits);
            text.WriteTo(writer, cancellationToken);
        }

        private SourceText DeserializeSourceText(ObjectReader reader, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // REVIEW: why IDE services doesnt care about checksumAlgorithm?
            _ = (SourceHashAlgorithm)reader.ReadInt32();
            var encoding = (Encoding)reader.ReadValue();

            var kind = (SerializationKinds)reader.ReadInt32();
            if (kind == SerializationKinds.MemoryMapFile)
            {
                var storage2 = (ITemporaryStorageService2)_storageService;

                var name = reader.ReadString();
                var offset = reader.ReadInt64();
                var size = reader.ReadInt64();

                var storage = storage2.AttachTemporaryTextStorage(name, offset, size, encoding, cancellationToken);

                return storage.ReadText(cancellationToken);
            }

            Contract.ThrowIfFalse(kind == SerializationKinds.Bits);
            return SourceTextExtensions.ReadFrom(_textService, reader, encoding, cancellationToken);
        }

        public void SerializeCompilationOptions(CompilationOptions options, ObjectWriter writer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var language = options.Language;

            // TODO: once compiler team adds ability to serialize compilation options to ObjectWriter directly, we won't need this.
            writer.WriteString(language);

            var service = GetOptionsSerializationService(language);
            service.WriteTo(options, writer, cancellationToken);
        }

        private CompilationOptions DeserializeCompilationOptions(ObjectReader reader, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var language = reader.ReadString();

            var service = GetOptionsSerializationService(language);
            return service.ReadCompilationOptionsFrom(reader, cancellationToken);
        }

        public void SerializeParseOptions(ParseOptions options, ObjectWriter writer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var language = options.Language;

            // TODO: once compiler team adds ability to serialize parse options to ObjectWriter directly, we won't need this.
            writer.WriteString(language);

            var service = GetOptionsSerializationService(language);
            service.WriteTo(options, writer, cancellationToken);
        }

        private ParseOptions DeserializeParseOptions(ObjectReader reader, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var language = reader.ReadString();

            var service = GetOptionsSerializationService(language);
            return service.ReadParseOptionsFrom(reader, cancellationToken);
        }

        public void SerializeProjectReference(ProjectReference reference, ObjectWriter writer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            reference.ProjectId.WriteTo(writer);
            writer.WriteValue(reference.Aliases.ToArray());
            writer.WriteBoolean(reference.EmbedInteropTypes);
        }

        private static ProjectReference DeserializeProjectReference(ObjectReader reader, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectId = ProjectId.ReadFrom(reader);
            var aliases = reader.ReadArray<string>();
            var embedInteropTypes = reader.ReadBoolean();

            return new ProjectReference(projectId, aliases.ToImmutableArrayOrEmpty(), embedInteropTypes);
        }

        public void SerializeMetadataReference(MetadataReference reference, ObjectWriter writer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteTo(reference, writer, cancellationToken);
        }

        private MetadataReference DeserializeMetadataReference(ObjectReader reader, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadMetadataReferenceFrom(reader, cancellationToken);
        }

        public void SerializeAnalyzerReference(AnalyzerReference reference, ObjectWriter writer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteTo(reference, writer, cancellationToken);
        }

        private AnalyzerReference DeserializeAnalyzerReference(ObjectReader reader, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ReadAnalyzerReferenceFrom(reader, cancellationToken);
        }
    }
}
