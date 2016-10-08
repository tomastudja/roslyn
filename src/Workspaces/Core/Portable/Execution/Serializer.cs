﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Execution;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Serialization
{
    /// <summary>
    /// serialize and deserialize objects to straem.
    /// some of these could be moved into actual object, but putting everything here is a bit easier to find I believe.
    /// 
    /// also, consider moving this serializer to use C# BOND serializer 
    /// https://github.com/Microsoft/bond
    /// </summary>
    internal partial class Serializer
    {
        private readonly HostWorkspaceServices _workspaceServices;
        private readonly IReferenceSerializationService _hostSerializationService;
        private readonly ConcurrentDictionary<string, IOptionsSerializationService> _lazyLanguageSerializationService;

        public Serializer(HostWorkspaceServices workspaceServices)
        {
            _workspaceServices = workspaceServices;
            _hostSerializationService = _workspaceServices.GetService<IReferenceSerializationService>();

            _lazyLanguageSerializationService = new ConcurrentDictionary<string, IOptionsSerializationService>(concurrencyLevel: 2, capacity: _workspaceServices.SupportedLanguages.Count());
        }

        public Checksum CreateChecksum(object value, CancellationToken cancellationToken)
        {
            var kind = value.GetWellKnownSynchronizationKinds();

            using (Logger.LogBlock(FunctionId.Serializer_CreateChecksum, kind, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (value is IHasChecksum)
                {
                    return ((IHasChecksum)value).Checksum;
                }

                switch (kind)
                {
                    case WellKnownSynchronizationKinds.Null:
                        return Checksum.Null;

                    case WellKnownSynchronizationKinds.SolutionInfo:
                    case WellKnownSynchronizationKinds.ProjectInfo:
                    case WellKnownSynchronizationKinds.DocumentInfo:
                    case WellKnownSynchronizationKinds.CompilationOptions:
                    case WellKnownSynchronizationKinds.ParseOptions:
                    case WellKnownSynchronizationKinds.ProjectReference:
                        return Checksum.Create(value, kind, this);

                    case WellKnownSynchronizationKinds.MetadataReference:
                        return Checksum.Create(kind, _hostSerializationService.CreateChecksum((MetadataReference)value, cancellationToken));

                    case WellKnownSynchronizationKinds.AnalyzerReference:
                        return Checksum.Create(kind, _hostSerializationService.CreateChecksum((AnalyzerReference)value, cancellationToken));

                    case WellKnownSynchronizationKinds.SourceText:
                        return Checksum.Create(kind, new Checksum(((SourceText)value).GetChecksum()));

                    default:
                        // object that is not part of solution is not supported since we don't know what inputs are required to
                        // serialize it
                        throw ExceptionUtilities.UnexpectedValue(kind);
                }
            }
        }

        public void Serialize(object value, ObjectWriter writer, CancellationToken cancellationToken)
        {
            var kind = value.GetWellKnownSynchronizationKinds();

            using (Logger.LogBlock(FunctionId.Serializer_Serialize, kind, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (value is ChecksumWithChildren)
                {
                    SerializeChecksumWithChildren((ChecksumWithChildren)value, writer, cancellationToken);
                    return;
                }

                switch (kind)
                {
                    case WellKnownSynchronizationKinds.Null:
                        // do nothing
                        return;

                    case WellKnownSynchronizationKinds.SolutionInfo:
                        SerializeSerializedSolutionInfo((SerializedSolutionInfo)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.ProjectInfo:
                        SerializeSerializedProjectInfo((SerializedProjectInfo)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.DocumentInfo:
                        SerializeSerializedDocumentInfo((SerializedDocumentInfo)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.CompilationOptions:
                        SerializeCompilationOptions((CompilationOptions)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.ParseOptions:
                        SerializeParseOptions((ParseOptions)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.ProjectReference:
                        SerializeProjectReference((ProjectReference)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.MetadataReference:
                        SerializeMetadataReference((MetadataReference)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.AnalyzerReference:
                        SerializeAnalyzerReference((AnalyzerReference)value, writer, cancellationToken);
                        return;

                    case WellKnownSynchronizationKinds.SourceText:
                        SerializeSourceText(storage: null, text: (SourceText)value, writer: writer, cancellationToken: cancellationToken);
                        return;

                    default:
                        // object that is not part of solution is not supported since we don't know what inputs are required to
                        // serialize it
                        throw ExceptionUtilities.UnexpectedValue(kind);
                }
            }
        }

        public T Deserialize<T>(string kind, ObjectReader reader, CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.Serializer_Deserialize, kind, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                switch (kind)
                {
                    case WellKnownSynchronizationKinds.Null:
                        return default(T);

                    case WellKnownSynchronizationKinds.SolutionState:
                    case WellKnownSynchronizationKinds.ProjectState:
                    case WellKnownSynchronizationKinds.DocumentState:
                    case WellKnownSynchronizationKinds.Projects:
                    case WellKnownSynchronizationKinds.Documents:
                    case WellKnownSynchronizationKinds.TextDocuments:
                    case WellKnownSynchronizationKinds.ProjectReferences:
                    case WellKnownSynchronizationKinds.MetadataReferences:
                    case WellKnownSynchronizationKinds.AnalyzerReferences:
                        return (T)(object)DeserializeChecksumWithChildren(reader, cancellationToken);

                    case WellKnownSynchronizationKinds.SolutionInfo:
                        return (T)(object)DeserializeSerializedSolutionInfo(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.ProjectInfo:
                        return (T)(object)DeserializeSerializedProjectInfo(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.DocumentInfo:
                        return (T)(object)DeserializeSerializedDocumentInfo(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.CompilationOptions:
                        return (T)(object)DeserializeCompilationOptions(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.ParseOptions:
                        return (T)(object)DeserializeParseOptions(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.ProjectReference:
                        return (T)(object)DeserializeProjectReference(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.MetadataReference:
                        return (T)(object)DeserializeMetadataReference(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.AnalyzerReference:
                        return (T)(object)DeserializeAnalyzerReference(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.SourceText:
                        return (T)(object)DeserializeSourceText(reader, cancellationToken);
                    case WellKnownSynchronizationKinds.OptionSet:
                        return (T)(object)DeserializeOptionSet(reader, cancellationToken);

                    default:
                        throw ExceptionUtilities.UnexpectedValue(kind);
                }
            }
        }

        private string GetLanguageName(object value)
        {
            // for given object, we need to figure out which language the object belong to. 
            // we can't blindly get language service since that will bring in language specific dlls.
            foreach (var languageName in _workspaceServices.SupportedLanguages)
            {
                IOptionsSerializationService service;
                if (_lazyLanguageSerializationService.TryGetValue(languageName, out service))
                {
                    if (service.Owns(value))
                    {
                        return languageName;
                    }

                    continue;
                }

                // this should be only reached once per language value actually belong to
                var mefWorkspaceServices = _workspaceServices as MefWorkspaceServices;
                if (mefWorkspaceServices != null)
                {
                    MefLanguageServices languageServices;
                    if (!mefWorkspaceServices.TryGetLanguageServices(languageName, out languageServices))
                    {
                        // this is a bit fragile since it depends on implementation detail but there is no other way
                        // to figure out which language a type belong to without loading other languages
                        //
                        // if a language's language services is not created yet, then it means that language is not loaded
                        continue;
                    }
                }

                service = GetOptionsSerializationService(languageName);
                if (service.Owns(value))
                {
                    return languageName;
                }
            }

            // shouldn't reach here
            throw ExceptionUtilities.UnexpectedValue(value);
        }

        private IOptionsSerializationService GetOptionsSerializationService(string languageName)
        {
            return _lazyLanguageSerializationService.GetOrAdd(languageName, n => _workspaceServices.GetLanguageServices(n).GetService<IOptionsSerializationService>());
        }
    }

    // TODO: convert this to sub class rather than using enum with if statement.
    internal enum SerializationKinds
    {
        Bits,
        FilePath,
        MemoryMapFile
    }
}
