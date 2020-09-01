﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServices.Implementation.TaskList;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
{
    internal sealed class VisualStudioProject
    {
        private static readonly ImmutableArray<MetadataReferenceProperties> s_defaultMetadataReferenceProperties = ImmutableArray.Create(default(MetadataReferenceProperties));

        private readonly VisualStudioWorkspaceImpl _workspace;
        private readonly HostDiagnosticUpdateSource _hostDiagnosticUpdateSource;

        /// <summary>
        /// Provides dynamic source files for files added through <see cref="AddDynamicSourceFile" />.
        /// </summary>
        private readonly ImmutableArray<Lazy<IDynamicFileInfoProvider, FileExtensionsMetadata>> _dynamicFileInfoProviders;

        /// <summary>
        /// A gate taken for all mutation of any mutable field in this type.
        /// </summary>
        /// <remarks>This is, for now, intentionally pessimistic. There are no doubt ways that we could allow more to run in parallel,
        /// but the current tradeoff is for simplicity of code and "obvious correctness" than something that is subtle, fast, and wrong.</remarks>
        private readonly object _gate = new object();

        /// <summary>
        /// The number of active batch scopes. If this is zero, we are not batching, non-zero means we are batching.
        /// </summary>
        private int _activeBatchScopes = 0;

        private readonly List<(string path, MetadataReferenceProperties properties)> _metadataReferencesAddedInBatch = new List<(string path, MetadataReferenceProperties properties)>();
        private readonly List<(string path, MetadataReferenceProperties properties)> _metadataReferencesRemovedInBatch = new List<(string path, MetadataReferenceProperties properties)>();
        private readonly List<ProjectReference> _projectReferencesAddedInBatch = new List<ProjectReference>();
        private readonly List<ProjectReference> _projectReferencesRemovedInBatch = new List<ProjectReference>();

        private readonly Dictionary<string, VisualStudioAnalyzer> _analyzerPathsToAnalyzers = new Dictionary<string, VisualStudioAnalyzer>();
        private readonly List<VisualStudioAnalyzer> _analyzersAddedInBatch = new List<VisualStudioAnalyzer>();
        private readonly List<VisualStudioAnalyzer> _analyzersRemovedInBatch = new List<VisualStudioAnalyzer>();

        private readonly List<Func<Solution, Solution>> _projectPropertyModificationsInBatch = new List<Func<Solution, Solution>>();

        private string _assemblyName;
        private string _displayName;
        private string? _filePath;
        private CompilationOptions? _compilationOptions;
        private ParseOptions? _parseOptions;
        private bool _hasAllInformation = true;
        private string? _compilationOutputAssemblyFilePath;
        private string? _outputFilePath;
        private string? _outputRefFilePath;
        private string? _defaultNamespace;

        /// <summary>
        /// If this project is the 'primary' project the project system cares about for a group of Roslyn projects that
        /// correspond to different configurations of a single project system project. <see langword="true"/> by
        /// default.
        /// </summary>
        internal bool IsPrimary { get; set; } = true;

        // Actual property values for 'RunAnalyzers' and 'RunAnalyzersDuringLiveAnalysis' properties from the project file.
        // Both these properties can be used to configure running analyzers, with RunAnalyzers overriding RunAnalyzersDuringLiveAnalysis.
        private bool? _runAnalyzersPropertyValue;
        private bool? _runAnalyzersDuringLiveAnalysisPropertyValue;

        // Effective boolean value to determine if analyzers should be executed based on _runAnalyzersPropertyValue and _runAnalyzersDuringLiveAnalysisPropertyValue.
        private bool _runAnalyzers = true;

        private readonly Dictionary<string, ImmutableArray<MetadataReferenceProperties>> _allMetadataReferences = new Dictionary<string, ImmutableArray<MetadataReferenceProperties>>();

        /// <summary>
        /// The file watching tokens for the documents in this project. We get the tokens even when we're in a batch, so the files here
        /// may not be in the actual workspace yet.
        /// </summary>
        private readonly Dictionary<DocumentId, FileChangeWatcher.IFileWatchingToken> _documentFileWatchingTokens = new Dictionary<DocumentId, FileChangeWatcher.IFileWatchingToken>();

        /// <summary>
        /// A file change context used to watch source files, additional files, and analyzer config files for this project. It's automatically set to watch the user's project
        /// directory so we avoid file-by-file watching.
        /// </summary>
        private readonly FileChangeWatcher.IContext _documentFileChangeContext;

        /// <summary>
        /// track whether we have been subscribed to <see cref="IDynamicFileInfoProvider.Updated"/> event
        /// </summary>
        private readonly HashSet<IDynamicFileInfoProvider> _eventSubscriptionTracker = new HashSet<IDynamicFileInfoProvider>();

        /// <summary>
        /// Map of the original dynamic file path to the <see cref="DynamicFileInfo.FilePath"/> that was associated with it.
        ///
        /// For example, the key is something like Page.cshtml which is given to us from the project system calling
        /// <see cref="AddDynamicSourceFile(string, ImmutableArray{string})"/>. The value of the map is a generated file that
        /// corresponds to the original path, say Page.g.cs. If we were given a file by the project system but no
        /// <see cref="IDynamicFileInfoProvider"/> provided a file for it, we will record the value as null so we still can track
        /// the addition of the .cshtml file for a later call to <see cref="RemoveDynamicSourceFile(string)"/>.
        ///
        /// The workspace snapshot will only have a document with  <see cref="DynamicFileInfo.FilePath"/> (the value) but not the
        /// original dynamic file path (the key).
        /// </summary>
        private readonly Dictionary<string, string?> _dynamicFilePathMaps = new Dictionary<string, string?>();

        private readonly BatchingDocumentCollection _sourceFiles;
        private readonly BatchingDocumentCollection _additionalFiles;
        private readonly BatchingDocumentCollection _analyzerConfigFiles;

        public ProjectId Id { get; }
        public string Language { get; }

        internal VisualStudioProject(
            VisualStudioWorkspaceImpl workspace,
            ImmutableArray<Lazy<IDynamicFileInfoProvider, FileExtensionsMetadata>> dynamicFileInfoProviders,
            HostDiagnosticUpdateSource hostDiagnosticUpdateSource,
            ProjectId id,
            string displayName,
            string language,
            string assemblyName,
            CompilationOptions? compilationOptions,
            string? filePath,
            ParseOptions? parseOptions)
        {
            _workspace = workspace;
            _dynamicFileInfoProviders = dynamicFileInfoProviders;
            _hostDiagnosticUpdateSource = hostDiagnosticUpdateSource;

            Id = id;
            Language = language;
            _displayName = displayName;

            var fileExtensionToWatch = language switch { LanguageNames.CSharp => ".cs", LanguageNames.VisualBasic => ".vb", _ => null };

            if (filePath != null && fileExtensionToWatch != null)
            {
                // Since we have a project directory, we'll just watch all the files under that path; that'll avoid extra overhead of
                // having to add explicit file watches everywhere.
                var projectDirectoryToWatch = new FileChangeWatcher.WatchedDirectory(Path.GetDirectoryName(filePath), fileExtensionToWatch);
                _documentFileChangeContext = _workspace.FileChangeWatcher.CreateContext(projectDirectoryToWatch);
            }
            else
            {
                _documentFileChangeContext = workspace.FileChangeWatcher.CreateContext();
            }

            _documentFileChangeContext.FileChanged += DocumentFileChangeContext_FileChanged;

            _sourceFiles = new BatchingDocumentCollection(
                this,
                documentAlreadyInWorkspace: (s, d) => s.ContainsDocument(d),
                documentAddAction: (w, d) => w.OnDocumentAdded(d),
                documentRemoveAction: (w, documentId) => w.OnDocumentRemoved(documentId),
                documentTextLoaderChangedAction: (w, d, loader) => w.OnDocumentTextLoaderChanged(d, loader));

            _additionalFiles = new BatchingDocumentCollection(this,
                (s, d) => s.ContainsAdditionalDocument(d),
                (w, d) => w.OnAdditionalDocumentAdded(d),
                (w, documentId) => w.OnAdditionalDocumentRemoved(documentId),
                documentTextLoaderChangedAction: (w, d, loader) => w.OnAdditionalDocumentTextLoaderChanged(d, loader));

            _analyzerConfigFiles = new BatchingDocumentCollection(this,
                (s, d) => s.ContainsAnalyzerConfigDocument(d),
                (w, d) => w.OnAnalyzerConfigDocumentAdded(d),
                (w, documentId) => w.OnAnalyzerConfigDocumentRemoved(documentId),
                documentTextLoaderChangedAction: (w, d, loader) => w.OnAnalyzerConfigDocumentTextLoaderChanged(d, loader));

            _assemblyName = assemblyName;
            _compilationOptions = compilationOptions;
            _filePath = filePath;
            _parseOptions = parseOptions;
        }

        private void ChangeProjectProperty<T>(ref T field, T newValue, Func<Solution, Solution> withNewValue)
        {
            lock (_gate)
            {
                // If nothing is changing, we can skip entirely
                if (object.Equals(field, newValue))
                {
                    return;
                }

                field = newValue;

                if (_activeBatchScopes > 0)
                {
                    _projectPropertyModificationsInBatch.Add(withNewValue);
                }
                else
                {
                    _workspace.ApplyChangeToWorkspace(Id, withNewValue);
                }
            }
        }

        private void ChangeProjectOutputPath(ref string? field, string? newValue, Func<Solution, Solution> withNewValue)
        {
            lock (_gate)
            {
                // Skip if nothing changing
                if (field == newValue)
                {
                    return;
                }

                if (field != null)
                {
                    _workspace.RemoveProjectOutputPath(Id, field);
                }

                if (newValue != null)
                {
                    _workspace.AddProjectOutputPath(Id, newValue);
                }

                ChangeProjectProperty(ref field, newValue, withNewValue);
            }
        }

        public string AssemblyName
        {
            get => _assemblyName;
            set => ChangeProjectProperty(ref _assemblyName, value, s => s.WithProjectAssemblyName(Id, value));
        }

        // The property could be null if this is a non-C#/VB language and we don't have one for it. But we disallow assigning null, because C#/VB cannot end up null
        // again once they already had one.
        [DisallowNull]
        public CompilationOptions? CompilationOptions
        {
            get => _compilationOptions;
            set => ChangeProjectProperty(ref _compilationOptions, value, s => s.WithProjectCompilationOptions(Id, value));
        }

        // The property could be null if this is a non-C#/VB language and we don't have one for it. But we disallow assigning null, because C#/VB cannot end up null
        // again once they already had one.
        [DisallowNull]
        public ParseOptions? ParseOptions
        {
            get => _parseOptions;
            set => ChangeProjectProperty(ref _parseOptions, value, s => s.WithProjectParseOptions(Id, value));
        }

        /// <summary>
        /// The path to the output in obj.
        /// </summary>
        internal string? CompilationOutputAssemblyFilePath
        {
            get => _compilationOutputAssemblyFilePath;
            set => ChangeProjectOutputPath(
                       ref _compilationOutputAssemblyFilePath,
                       value,
                       s => s.WithProjectCompilationOutputInfo(Id, s.GetRequiredProject(Id).CompilationOutputInfo.WithAssemblyPath(value)));
        }

        public string? OutputFilePath
        {
            get => _outputFilePath;
            set => ChangeProjectOutputPath(ref _outputFilePath, value, s => s.WithProjectOutputFilePath(Id, value));
        }

        public string? OutputRefFilePath
        {
            get => _outputRefFilePath;
            set => ChangeProjectOutputPath(ref _outputRefFilePath, value, s => s.WithProjectOutputRefFilePath(Id, value));
        }

        public string? FilePath
        {
            get => _filePath;
            set => ChangeProjectProperty(ref _filePath, value, s => s.WithProjectFilePath(Id, value));
        }

        public string DisplayName
        {
            get => _displayName;
            set => ChangeProjectProperty(ref _displayName, value, s => s.WithProjectName(Id, value));
        }

        // internal to match the visibility of the Workspace-level API -- this is something
        // we use but we haven't made officially public yet.
        internal bool HasAllInformation
        {
            get => _hasAllInformation;
            set => ChangeProjectProperty(ref _hasAllInformation, value, s => s.WithHasAllInformation(Id, value));
        }

        internal bool? RunAnalyzers
        {
            get => _runAnalyzersPropertyValue;
            set
            {
                _runAnalyzersPropertyValue = value;
                UpdateRunAnalyzers();
            }
        }

        internal bool? RunAnalyzersDuringLiveAnalysis
        {
            get => _runAnalyzersDuringLiveAnalysisPropertyValue;
            set
            {
                _runAnalyzersDuringLiveAnalysisPropertyValue = value;
                UpdateRunAnalyzers();
            }
        }

        private void UpdateRunAnalyzers()
        {
            // Property RunAnalyzers overrides RunAnalyzersDuringLiveAnalysis, and default when both properties are not set is 'true'.
            var runAnalyzers = _runAnalyzersPropertyValue ?? _runAnalyzersDuringLiveAnalysisPropertyValue ?? true;
            ChangeProjectProperty(ref _runAnalyzers, runAnalyzers, s => s.WithRunAnalyzers(Id, runAnalyzers));
        }

        /// <summary>
        /// The default namespace of the project.
        /// </summary>
        /// <remarks>
        /// In C#, this is defined as the value of "rootnamespace" msbuild property. Right now VB doesn't 
        /// have the concept of "default namespace", but we conjure one in workspace by assigning the value
        /// of the project's root namespace to it. So various features can choose to use it for their own purpose.
        /// 
        /// In the future, we might consider officially exposing "default namespace" for VB project
        /// (e.g.through a "defaultnamespace" msbuild property)
        /// </remarks>
        internal string? DefaultNamespace
        {
            get => _defaultNamespace;
            set => ChangeProjectProperty(ref _defaultNamespace, value, s => s.WithProjectDefaultNamespace(Id, value));
        }

        /// <summary>
        /// The max language version supported for this project, if applicable. Useful to help indicate what 
        /// language version features should be suggested to a user, as well as if they can be upgraded. 
        /// </summary>
        internal string? MaxLangVersion
        {
            set => _workspace.SetMaxLanguageVersion(Id, value);
        }

        #region Batching

        public BatchScope CreateBatchScope()
        {
            lock (_gate)
            {
                _activeBatchScopes++;
                return new BatchScope(this);
            }
        }

        public sealed class BatchScope : IDisposable
        {
            private readonly VisualStudioProject _project;

            /// <summary>
            /// Flag to control if this has already been disposed. Not a boolean only so it can be used with Interlocked.CompareExchange.
            /// </summary>
            private volatile int _disposed = 0;

            internal BatchScope(VisualStudioProject visualStudioProject)
                => _project = visualStudioProject;

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    _project.OnBatchScopeDisposed();
                }
            }
        }

        private void OnBatchScopeDisposed()
        {
            lock (_gate)
            {
                _activeBatchScopes--;

                if (_activeBatchScopes > 0)
                {
                    return;
                }

                var documentFileNamesAdded = ImmutableArray.CreateBuilder<string>();
                var documentsToOpen = new List<(DocumentId documentId, SourceTextContainer textContainer)>();
                var additionalDocumentsToOpen = new List<(DocumentId documentId, SourceTextContainer textContainer)>();
                var analyzerConfigDocumentsToOpen = new List<(DocumentId documentId, SourceTextContainer textContainer)>();

                _workspace.ApplyBatchChangeToWorkspace(solution =>
                {
                    var solutionChanges = new SolutionChangeAccumulator(startingSolution: solution);

                    _sourceFiles.UpdateSolutionForBatch(
                        solutionChanges,
                        documentFileNamesAdded,
                        documentsToOpen,
                        (s, documents) => s.AddDocuments(documents),
                        WorkspaceChangeKind.DocumentAdded,
                        (s, ids) => s.RemoveDocuments(ids),
                        WorkspaceChangeKind.DocumentRemoved);

                    _additionalFiles.UpdateSolutionForBatch(
                        solutionChanges,
                        documentFileNamesAdded,
                        additionalDocumentsToOpen,
                        (s, documents) =>
                        {
                            foreach (var document in documents)
                            {
                                s = s.AddAdditionalDocument(document);
                            }

                            return s;
                        },
                        WorkspaceChangeKind.AdditionalDocumentAdded,
                        (s, ids) => s.RemoveAdditionalDocuments(ids),
                        WorkspaceChangeKind.AdditionalDocumentRemoved);

                    _analyzerConfigFiles.UpdateSolutionForBatch(
                        solutionChanges,
                        documentFileNamesAdded,
                        analyzerConfigDocumentsToOpen,
                        (s, documents) => s.AddAnalyzerConfigDocuments(documents),
                        WorkspaceChangeKind.AnalyzerConfigDocumentAdded,
                        (s, ids) => s.RemoveAnalyzerConfigDocuments(ids),
                        WorkspaceChangeKind.AnalyzerConfigDocumentRemoved);

                    // Metadata reference removing. Do this before adding in case this removes a project reference that
                    // we are also going to add in the same batch. This could happen if case is changing, or we're targeting
                    // a different output path (say bin vs. obj vs. ref).
                    foreach (var (path, properties) in _metadataReferencesRemovedInBatch)
                    {
                        var projectReference = _workspace.TryRemoveConvertedProjectReference_NoLock(Id, path, properties);

                        if (projectReference != null)
                        {
                            solutionChanges.UpdateSolutionForProjectAction(
                                Id,
                                solutionChanges.Solution.RemoveProjectReference(Id, projectReference));
                        }
                        else
                        {
                            // TODO: find a cleaner way to fetch this
                            var metadataReference = _workspace.CurrentSolution.GetRequiredProject(Id).MetadataReferences.Cast<PortableExecutableReference>()
                                                                                    .Single(m => m.FilePath == path && m.Properties == properties);

                            _workspace.FileWatchedReferenceFactory.StopWatchingReference(metadataReference);

                            solutionChanges.UpdateSolutionForProjectAction(
                                Id,
                                newSolution: solutionChanges.Solution.RemoveMetadataReference(Id, metadataReference));
                        }
                    }

                    ClearAndZeroCapacity(_metadataReferencesRemovedInBatch);

                    // Metadata reference adding...
                    if (_metadataReferencesAddedInBatch.Count > 0)
                    {
                        var projectReferencesCreated = new List<ProjectReference>();
                        var metadataReferencesCreated = new List<MetadataReference>();

                        foreach (var (path, properties) in _metadataReferencesAddedInBatch)
                        {
                            var projectReference = _workspace.TryCreateConvertedProjectReference_NoLock(Id, path, properties);

                            if (projectReference != null)
                            {
                                projectReferencesCreated.Add(projectReference);
                            }
                            else
                            {
                                var metadataReference = _workspace.FileWatchedReferenceFactory.CreateReferenceAndStartWatchingFile(path, properties);
                                metadataReferencesCreated.Add(metadataReference);
                            }
                        }

                        solutionChanges.UpdateSolutionForProjectAction(
                            Id,
                            solutionChanges.Solution.AddProjectReferences(Id, projectReferencesCreated)
                                                    .AddMetadataReferences(Id, metadataReferencesCreated));

                        ClearAndZeroCapacity(_metadataReferencesAddedInBatch);
                    }

                    // Project reference adding...
                    solutionChanges.UpdateSolutionForProjectAction(
                        Id,
                        newSolution: solutionChanges.Solution.AddProjectReferences(Id, _projectReferencesAddedInBatch));
                    ClearAndZeroCapacity(_projectReferencesAddedInBatch);

                    // Project reference removing...
                    foreach (var projectReference in _projectReferencesRemovedInBatch)
                    {
                        solutionChanges.UpdateSolutionForProjectAction(
                            Id,
                            newSolution: solutionChanges.Solution.RemoveProjectReference(Id, projectReference));
                    }

                    ClearAndZeroCapacity(_projectReferencesRemovedInBatch);

                    // Analyzer reference adding...
                    solutionChanges.UpdateSolutionForProjectAction(
                        Id,
                        newSolution: solutionChanges.Solution.AddAnalyzerReferences(Id, _analyzersAddedInBatch.Select(a => a.GetReference())));
                    ClearAndZeroCapacity(_analyzersAddedInBatch);

                    // Analyzer reference removing...
                    foreach (var analyzerReference in _analyzersRemovedInBatch)
                    {
                        solutionChanges.UpdateSolutionForProjectAction(
                            Id,
                            newSolution: solutionChanges.Solution.RemoveAnalyzerReference(Id, analyzerReference.GetReference()));
                    }

                    ClearAndZeroCapacity(_analyzersRemovedInBatch);

                    // Other property modifications...
                    foreach (var propertyModification in _projectPropertyModificationsInBatch)
                    {
                        solutionChanges.UpdateSolutionForProjectAction(
                            Id,
                            propertyModification(solutionChanges.Solution));
                    }

                    ClearAndZeroCapacity(_projectPropertyModificationsInBatch);

                    return solutionChanges;
                });

                foreach (var (documentId, textContainer) in documentsToOpen)
                {
                    _workspace.ApplyChangeToWorkspace(w => w.OnDocumentOpened(documentId, textContainer));
                }

                foreach (var (documentId, textContainer) in additionalDocumentsToOpen)
                {
                    _workspace.ApplyChangeToWorkspace(w => w.OnAdditionalDocumentOpened(documentId, textContainer));
                }

                foreach (var (documentId, textContainer) in analyzerConfigDocumentsToOpen)
                {
                    _workspace.ApplyChangeToWorkspace(w => w.OnAnalyzerConfigDocumentOpened(documentId, textContainer));
                }

                // Check for those files being opened to start wire-up if necessary
                _workspace.QueueCheckForFilesBeingOpen(documentFileNamesAdded.ToImmutable());
            }
        }

        #endregion

        #region Source File Addition/Removal

        public void AddSourceFile(string fullPath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular, ImmutableArray<string> folders = default)
            => _sourceFiles.AddFile(fullPath, sourceCodeKind, folders);

        /// <summary>
        /// Adds a source file to the project from a text container (eg, a Visual Studio Text buffer)
        /// </summary>
        /// <param name="textContainer">The text container that contains this file.</param>
        /// <param name="fullPath">The file path of the document.</param>
        /// <param name="sourceCodeKind">The kind of the source code.</param>
        /// <param name="folders">The names of the logical nested folders the document is contained in.</param>
        /// <param name="designTimeOnly">Whether the document is used only for design time (eg. completion) or also included in a compilation.</param>
        /// <param name="documentServiceProvider">A <see cref="IDocumentServiceProvider"/> associated with this document</param>
        public DocumentId AddSourceTextContainer(
            SourceTextContainer textContainer,
            string fullPath,
            SourceCodeKind sourceCodeKind = SourceCodeKind.Regular,
            ImmutableArray<string> folders = default,
            bool designTimeOnly = false,
            IDocumentServiceProvider? documentServiceProvider = null)
        {
            return _sourceFiles.AddTextContainer(textContainer, fullPath, sourceCodeKind, folders, designTimeOnly, documentServiceProvider);
        }

        public bool ContainsSourceFile(string fullPath)
            => _sourceFiles.ContainsFile(fullPath);

        public void RemoveSourceFile(string fullPath)
            => _sourceFiles.RemoveFile(fullPath);

        public void RemoveSourceTextContainer(SourceTextContainer textContainer)
            => _sourceFiles.RemoveTextContainer(textContainer);

        #endregion

        #region Additional File Addition/Removal

        // TODO: should AdditionalFiles have source code kinds?
        public void AddAdditionalFile(string fullPath, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular)
            => _additionalFiles.AddFile(fullPath, sourceCodeKind, folders: default);

        public bool ContainsAdditionalFile(string fullPath)
            => _additionalFiles.ContainsFile(fullPath);

        public void RemoveAdditionalFile(string fullPath)
            => _additionalFiles.RemoveFile(fullPath);

        #endregion

        #region Analyzer Config File Addition/Removal

        public void AddAnalyzerConfigFile(string fullPath)
        {
            // TODO: do we need folders for analyzer config files?
            _analyzerConfigFiles.AddFile(fullPath, SourceCodeKind.Regular, folders: default);
        }

        public bool ContainsAnalyzerConfigFile(string fullPath)
            => _analyzerConfigFiles.ContainsFile(fullPath);

        public void RemoveAnalyzerConfigFile(string fullPath)
            => _analyzerConfigFiles.RemoveFile(fullPath);

        #endregion

        #region Non Source File Addition/Removal

        public void AddDynamicSourceFile(string dynamicFilePath, ImmutableArray<string> folders)
        {
            DynamicFileInfo? fileInfo = null;
            IDynamicFileInfoProvider? providerForFileInfo = null;

            var extension = FileNameUtilities.GetExtension(dynamicFilePath)?.TrimStart('.');
            if (extension?.Length == 0)
            {
                fileInfo = null;
            }
            else
            {
                foreach (var provider in _dynamicFileInfoProviders)
                {
                    // skip unrelated providers
                    if (!provider.Metadata.Extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Don't get confused by _filePath and filePath.
                    // VisualStudioProject._filePath points to csproj/vbproj of the project
                    // and the parameter filePath points to dynamic file such as ASP.NET .g.cs files.
                    // 
                    // Also, provider is free-threaded. so fine to call Wait rather than JTF.
                    fileInfo = provider.Value.GetDynamicFileInfoAsync(
                        projectId: Id, projectFilePath: _filePath, filePath: dynamicFilePath, CancellationToken.None).WaitAndGetResult_CanCallOnBackground(CancellationToken.None);

                    if (fileInfo != null)
                    {
                        fileInfo = FixUpDynamicFileInfo(fileInfo, dynamicFilePath);
                        providerForFileInfo = provider.Value;
                        break;
                    }
                }
            }

            lock (_gate)
            {
                if (_dynamicFilePathMaps.ContainsKey(dynamicFilePath))
                {
                    // TODO: if we have a duplicate, we are not calling RemoveDynamicFileInfoAsync since we
                    // don't want to call with that under a lock. If we are getting duplicates we have bigger problems
                    // at that point since our workspace is generally out of sync with the project system.
                    // Given we're taking this as a late fix prior to a release, I don't think it's worth the added
                    // risk to handle a case that wasn't handled before either.
                    throw new ArgumentException($"{dynamicFilePath} has already been added to this project.");
                }

                // Record the mapping from the dynamic file path to the source file it generated. We will record
                // 'null' if no provider was able to produce a source file for this input file. That could happen
                // if the provider (say ASP.NET Razor) doesn't recognize the file, or the wrong type of file
                // got passed through the system. That's not a failure from the project system's perspective:
                // adding dynamic files is a hint at best that doesn't impact it.
                _dynamicFilePathMaps.Add(dynamicFilePath, fileInfo?.FilePath);

                if (fileInfo != null)
                {
                    // If fileInfo is not null, that means we found a provider so this should be not-null as well
                    // since we had to go through the earlier assignment.
                    Contract.ThrowIfNull(providerForFileInfo);
                    _sourceFiles.AddDynamicFile(providerForFileInfo, fileInfo, folders);
                }
            }
        }

        private static DynamicFileInfo FixUpDynamicFileInfo(DynamicFileInfo fileInfo, string filePath)
        {
            // we might change contract and just throw here. but for now, we keep existing contract where one can return null for DynamicFileInfo.FilePath.
            // In this case we substitute the file being generated from so we still have some path.
            if (string.IsNullOrEmpty(fileInfo.FilePath))
            {
                return new DynamicFileInfo(filePath, fileInfo.SourceCodeKind, fileInfo.TextLoader, fileInfo.DesignTimeOnly, fileInfo.DocumentServiceProvider);
            }

            return fileInfo;
        }

        public void RemoveDynamicSourceFile(string dynamicFilePath)
        {
            IDynamicFileInfoProvider provider;

            lock (_gate)
            {
                if (!_dynamicFilePathMaps.TryGetValue(dynamicFilePath, out var sourceFilePath))
                {
                    throw new ArgumentException($"{dynamicFilePath} wasn't added by a previous call to {nameof(AddDynamicSourceFile)}");
                }

                _dynamicFilePathMaps.Remove(dynamicFilePath);

                // If we got a null path back, it means we never had a source file to add. In that case,
                // we're done
                if (sourceFilePath == null)
                {
                    return;
                }

                provider = _sourceFiles.RemoveDynamicFile(sourceFilePath);
            }

            // provider is free-threaded. so fine to call Wait rather than JTF
            provider.RemoveDynamicFileInfoAsync(
                projectId: Id, projectFilePath: _filePath, filePath: dynamicFilePath, CancellationToken.None).Wait(CancellationToken.None);
        }

        private void OnDynamicFileInfoUpdated(object sender, string dynamicFilePath)
        {
            string? fileInfoPath;

            lock (_gate)
            {
                if (!_dynamicFilePathMaps.TryGetValue(dynamicFilePath, out fileInfoPath))
                {
                    // given file doesn't belong to this project. 
                    // this happen since the event this is handling is shared between all projects
                    return;
                }
            }

            if (fileInfoPath != null)
            {
                _sourceFiles.ProcessFileChange(dynamicFilePath, fileInfoPath);
            }
        }

        #endregion

        #region Analyzer Addition/Removal

        public void AddAnalyzerReference(string fullPath)
        {
            CompilerPathUtilities.RequireAbsolutePath(fullPath, nameof(fullPath));

            var visualStudioAnalyzer = new VisualStudioAnalyzer(
                fullPath,
                _hostDiagnosticUpdateSource,
                Id,
                Language);

            lock (_gate)
            {
                if (_analyzerPathsToAnalyzers.ContainsKey(fullPath))
                {
                    throw new ArgumentException($"'{fullPath}' has already been added to this project.", nameof(fullPath));
                }

                _analyzerPathsToAnalyzers.Add(fullPath, visualStudioAnalyzer);

                if (_activeBatchScopes > 0)
                {
                    _analyzersAddedInBatch.Add(visualStudioAnalyzer);
                }
                else
                {
                    _workspace.ApplyChangeToWorkspace(w => w.OnAnalyzerReferenceAdded(Id, visualStudioAnalyzer.GetReference()));
                }
            }
        }

        public void RemoveAnalyzerReference(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                throw new ArgumentException("message", nameof(fullPath));
            }

            lock (_gate)
            {
                if (!_analyzerPathsToAnalyzers.TryGetValue(fullPath, out var visualStudioAnalyzer))
                {
                    throw new ArgumentException($"'{fullPath}' is not an analyzer of this project.", nameof(fullPath));
                }

                _analyzerPathsToAnalyzers.Remove(fullPath);

                if (_activeBatchScopes > 0)
                {
                    _analyzersRemovedInBatch.Add(visualStudioAnalyzer);
                }
                else
                {
                    _workspace.ApplyChangeToWorkspace(w => w.OnAnalyzerReferenceRemoved(Id, visualStudioAnalyzer.GetReference()));
                }
            }
        }

        #endregion

        private void DocumentFileChangeContext_FileChanged(object sender, string fullFilePath)
        {
            _sourceFiles.ProcessFileChange(fullFilePath);
            _additionalFiles.ProcessFileChange(fullFilePath);
            _analyzerConfigFiles.ProcessFileChange(fullFilePath);
        }

        #region Metadata Reference Addition/Removal

        public void AddMetadataReference(string fullPath, MetadataReferenceProperties properties)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                throw new ArgumentException($"{nameof(fullPath)} isn't a valid path.", nameof(fullPath));
            }

            lock (_gate)
            {
                if (ContainsMetadataReference(fullPath, properties))
                {
                    throw new InvalidOperationException("The metadata reference has already been added to the project.");
                }

                _allMetadataReferences.MultiAdd(fullPath, properties, s_defaultMetadataReferenceProperties);

                if (_activeBatchScopes > 0)
                {
                    if (!_metadataReferencesRemovedInBatch.Remove((fullPath, properties)))
                    {
                        _metadataReferencesAddedInBatch.Add((fullPath, properties));
                    }
                }
                else
                {
                    _workspace.ApplyChangeToWorkspace(w =>
                    {
                        var projectReference = _workspace.TryCreateConvertedProjectReference_NoLock(Id, fullPath, properties);

                        if (projectReference != null)
                        {
                            w.OnProjectReferenceAdded(Id, projectReference);
                        }
                        else
                        {
                            var metadataReference = _workspace.FileWatchedReferenceFactory.CreateReferenceAndStartWatchingFile(fullPath, properties);
                            w.OnMetadataReferenceAdded(Id, metadataReference);
                        }
                    });
                }
            }
        }

        public bool ContainsMetadataReference(string fullPath, MetadataReferenceProperties properties)
        {
            lock (_gate)
            {
                return GetPropertiesForMetadataReference(fullPath).Contains(properties);
            }
        }

        /// <summary>
        /// Returns the properties being used for the current metadata reference added to this project. May return multiple properties if
        /// the reference has been added multiple times with different properties.
        /// </summary>
        public ImmutableArray<MetadataReferenceProperties> GetPropertiesForMetadataReference(string fullPath)
        {
            lock (_gate)
            {
                return _allMetadataReferences.TryGetValue(fullPath, out var list) ? list : ImmutableArray<MetadataReferenceProperties>.Empty;
            }
        }

        public void RemoveMetadataReference(string fullPath, MetadataReferenceProperties properties)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                throw new ArgumentException($"{nameof(fullPath)} isn't a valid path.", nameof(fullPath));
            }

            lock (_gate)
            {
                if (!ContainsMetadataReference(fullPath, properties))
                {
                    throw new InvalidOperationException("The metadata reference does not exist in this project.");
                }

                _allMetadataReferences.MultiRemove(fullPath, properties);

                if (_activeBatchScopes > 0)
                {
                    if (!_metadataReferencesAddedInBatch.Remove((fullPath, properties)))
                    {
                        _metadataReferencesRemovedInBatch.Add((fullPath, properties));
                    }
                }
                else
                {
                    _workspace.ApplyChangeToWorkspace(w =>
                    {
                        var projectReference = _workspace.TryRemoveConvertedProjectReference_NoLock(Id, fullPath, properties);

                        // If this was converted to a project reference, we have now recorded the removal -- let's remove it here too
                        if (projectReference != null)
                        {
                            w.OnProjectReferenceRemoved(Id, projectReference);
                        }
                        else
                        {
                            // TODO: find a cleaner way to fetch this
                            var metadataReference = w.CurrentSolution.GetRequiredProject(Id).MetadataReferences.Cast<PortableExecutableReference>()
                                                                                            .Single(m => m.FilePath == fullPath && m.Properties == properties);

                            _workspace.FileWatchedReferenceFactory.StopWatchingReference(metadataReference);
                            w.OnMetadataReferenceRemoved(Id, metadataReference);
                        }
                    });
                }
            }
        }

        #endregion

        #region Project Reference Addition/Removal

        public void AddProjectReference(ProjectReference projectReference)
        {
            if (projectReference == null)
            {
                throw new ArgumentNullException(nameof(projectReference));
            }

            lock (_gate)
            {
                if (ContainsProjectReference(projectReference))
                {
                    throw new ArgumentException("The project reference has already been added to the project.");
                }

                if (_activeBatchScopes > 0)
                {
                    if (!_projectReferencesRemovedInBatch.Remove(projectReference))
                    {
                        _projectReferencesAddedInBatch.Add(projectReference);
                    }
                }
                else
                {
                    _workspace.ApplyChangeToWorkspace(w => w.OnProjectReferenceAdded(Id, projectReference));
                }
            }
        }

        public bool ContainsProjectReference(ProjectReference projectReference)
        {
            if (projectReference == null)
            {
                throw new ArgumentNullException(nameof(projectReference));
            }

            lock (_gate)
            {
                if (_projectReferencesRemovedInBatch.Contains(projectReference))
                {
                    return false;
                }

                if (_projectReferencesAddedInBatch.Contains(projectReference))
                {
                    return true;
                }

                return _workspace.CurrentSolution.GetRequiredProject(Id).AllProjectReferences.Contains(projectReference);
            }
        }

        public IReadOnlyList<ProjectReference> GetProjectReferences()
        {
            lock (_gate)
            {
                // If we're not batching, then this is cheap: just fetch from the workspace and we're done
                var projectReferencesInWorkspace = _workspace.CurrentSolution.GetRequiredProject(Id).AllProjectReferences;

                if (_activeBatchScopes == 0)
                {
                    return projectReferencesInWorkspace;
                }

                // Not, so we get to compute a new list instead
                var newList = projectReferencesInWorkspace.ToList();
                newList.AddRange(_projectReferencesAddedInBatch);
                newList.RemoveAll(p => _projectReferencesRemovedInBatch.Contains(p));

                return newList;
            }
        }

        public void RemoveProjectReference(ProjectReference projectReference)
        {
            if (projectReference == null)
            {
                throw new ArgumentNullException(nameof(projectReference));
            }

            lock (_gate)
            {
                if (_activeBatchScopes > 0)
                {
                    if (!_projectReferencesAddedInBatch.Remove(projectReference))
                    {
                        _projectReferencesRemovedInBatch.Add(projectReference);
                    }
                }
                else
                {
                    _workspace.ApplyChangeToWorkspace(w => w.OnProjectReferenceRemoved(Id, projectReference));
                }
            }
        }

        #endregion

        public void RemoveFromWorkspace()
        {
            _documentFileChangeContext.Dispose();

            lock (_gate)
            {
                // clear tracking to external components
                foreach (var provider in _eventSubscriptionTracker)
                {
                    provider.Updated -= OnDynamicFileInfoUpdated;
                }

                _eventSubscriptionTracker.Clear();
            }

            IReadOnlyList<MetadataReference>? remainingMetadataReferences = null;

            _workspace.ApplyChangeToWorkspace(w =>
            {
                // Acquire the remaining metadata references inside the workspace lock. This is critical
                // as another project being removed at the same time could result in project to project
                // references being converted to metadata references (or vice versa) and we might either
                // miss stopping a file watcher or might end up double-stopping a file watcher.
                remainingMetadataReferences = w.CurrentSolution.GetRequiredProject(Id).MetadataReferences;
                w.OnProjectRemoved(Id);
            });

            Contract.ThrowIfNull(remainingMetadataReferences);

            foreach (PortableExecutableReference reference in remainingMetadataReferences)
            {
                _workspace.FileWatchedReferenceFactory.StopWatchingReference(reference);
            }
        }

        /// <summary>
        /// Adds an additional output path that can be used for automatic conversion of metadata references to P2P references.
        /// Any projects with metadata references to the path given here will be converted to project-to-project references.
        /// </summary>
        public void AddOutputPath(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException($"{nameof(outputPath)} isn't a valid path.", nameof(outputPath));
            }

            _workspace.AddProjectOutputPath(Id, outputPath);
        }

        /// <summary>
        /// Removes an additional output path that was added by <see cref="AddOutputPath(string)"/>.
        /// </summary>
        public void RemoveOutputPath(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                throw new ArgumentException($"{nameof(outputPath)} isn't a valid path.", nameof(outputPath));
            }

            _workspace.RemoveProjectOutputPath(Id, outputPath);
        }

        public void ReorderSourceFiles(ImmutableArray<string> filePaths)
            => _sourceFiles.ReorderFiles(filePaths);

        /// <summary>
        /// Clears a list and zeros out the capacity. The lists we use for batching are likely to get large during an initial load, but after
        /// that point should never get that large again.
        /// </summary>
        private static void ClearAndZeroCapacity<T>(List<T> list)
        {
            list.Clear();
            list.Capacity = 0;
        }

        /// <summary>
        /// Clears a list and zeros out the capacity. The lists we use for batching are likely to get large during an initial load, but after
        /// that point should never get that large again.
        /// </summary>
        private static void ClearAndZeroCapacity<T>(ImmutableArray<T>.Builder list)
        {
            list.Clear();
            list.Capacity = 0;
        }

        /// <summary>
        /// Helper class to manage collections of source-file like things; this exists just to avoid duplicating all the logic for regular source files
        /// and additional files.
        /// </summary>
        /// <remarks>This class should be free-threaded, and any synchronization is done via <see cref="VisualStudioProject._gate"/>.
        /// This class is otehrwise free to operate on private members of <see cref="_project"/> if needed.</remarks>
        private sealed class BatchingDocumentCollection
        {
            private readonly VisualStudioProject _project;

            /// <summary>
            /// The map of file paths to the underlying <see cref="DocumentId"/>. This document may exist in <see cref="_documentsAddedInBatch"/> or has been
            /// pushed to the actual workspace.
            /// </summary>
            private readonly Dictionary<string, DocumentId> _documentPathsToDocumentIds = new Dictionary<string, DocumentId>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// A map of explicitly-added "always open" <see cref="SourceTextContainer"/> and their associated <see cref="DocumentId"/>. This does not contain
            /// any regular files that have been open.
            /// </summary>
            private IBidirectionalMap<SourceTextContainer, DocumentId> _sourceTextContainersToDocumentIds = BidirectionalMap<SourceTextContainer, DocumentId>.Empty;

            /// <summary>
            /// The map of <see cref="DocumentId"/> to <see cref="IDynamicFileInfoProvider"/> whose <see cref="DynamicFileInfo"/> got added into <see cref="Workspace"/>
            /// </summary>
            private readonly Dictionary<DocumentId, IDynamicFileInfoProvider> _documentIdToDynamicFileInfoProvider = new Dictionary<DocumentId, IDynamicFileInfoProvider>();

            /// <summary>
            /// The current list of documents that are to be added in this batch.
            /// </summary>
            private readonly ImmutableArray<DocumentInfo>.Builder _documentsAddedInBatch = ImmutableArray.CreateBuilder<DocumentInfo>();

            /// <summary>
            /// The current list of documents that are being removed in this batch. Once the document is in this list, it is no longer in <see cref="_documentPathsToDocumentIds"/>.
            /// </summary>
            private readonly List<DocumentId> _documentsRemovedInBatch = new List<DocumentId>();

            /// <summary>
            /// The current list of document file paths that will be ordered in a batch.
            /// </summary>
            private ImmutableList<DocumentId>? _orderedDocumentsInBatch = null;

            private readonly Func<Solution, DocumentId, bool> _documentAlreadyInWorkspace;
            private readonly Action<Workspace, DocumentInfo> _documentAddAction;
            private readonly Action<Workspace, DocumentId> _documentRemoveAction;
            private readonly Action<Workspace, DocumentId, TextLoader> _documentTextLoaderChangedAction;

            public BatchingDocumentCollection(VisualStudioProject project,
                Func<Solution, DocumentId, bool> documentAlreadyInWorkspace,
                Action<Workspace, DocumentInfo> documentAddAction,
                Action<Workspace, DocumentId> documentRemoveAction,
                Action<Workspace, DocumentId, TextLoader> documentTextLoaderChangedAction)
            {
                _project = project;
                _documentAlreadyInWorkspace = documentAlreadyInWorkspace;
                _documentAddAction = documentAddAction;
                _documentRemoveAction = documentRemoveAction;
                _documentTextLoaderChangedAction = documentTextLoaderChangedAction;
            }

            public DocumentId AddFile(string fullPath, SourceCodeKind sourceCodeKind, ImmutableArray<string> folders)
            {
                if (string.IsNullOrEmpty(fullPath))
                {
                    throw new ArgumentException($"{nameof(fullPath)} isn't a valid path.", nameof(fullPath));
                }

                var documentId = DocumentId.CreateNewId(_project.Id, fullPath);
                var textLoader = new FileTextLoader(fullPath, defaultEncoding: null);
                var documentInfo = DocumentInfo.Create(
                    documentId,
                    FileNameUtilities.GetFileName(fullPath),
                    folders: folders.IsDefault ? null : (IEnumerable<string>)folders,
                    sourceCodeKind: sourceCodeKind,
                    loader: textLoader,
                    filePath: fullPath,
                    isGenerated: false);

                lock (_project._gate)
                {
                    if (_documentPathsToDocumentIds.ContainsKey(fullPath))
                    {
                        throw new ArgumentException($"'{fullPath}' has already been added to this project.", nameof(fullPath));
                    }

                    // If we have an ordered document ids batch, we need to add the document id to the end of it as well.
                    _orderedDocumentsInBatch = _orderedDocumentsInBatch?.Add(documentId);

                    _documentPathsToDocumentIds.Add(fullPath, documentId);
                    _project._documentFileWatchingTokens.Add(documentId, _project._documentFileChangeContext.EnqueueWatchingFile(fullPath));

                    if (_project._activeBatchScopes > 0)
                    {
                        _documentsAddedInBatch.Add(documentInfo);
                    }
                    else
                    {
                        _project._workspace.ApplyChangeToWorkspace(w => _documentAddAction(w, documentInfo));
                        _project._workspace.QueueCheckForFilesBeingOpen(ImmutableArray.Create(fullPath));
                    }
                }

                return documentId;
            }

            public DocumentId AddTextContainer(SourceTextContainer textContainer, string fullPath, SourceCodeKind sourceCodeKind, ImmutableArray<string> folders, bool designTimeOnly, IDocumentServiceProvider? documentServiceProvider)
            {
                if (textContainer == null)
                {
                    throw new ArgumentNullException(nameof(textContainer));
                }

                var documentId = DocumentId.CreateNewId(_project.Id, fullPath);
                var textLoader = new SourceTextLoader(textContainer, fullPath);
                var documentInfo = DocumentInfo.Create(
                    documentId,
                    FileNameUtilities.GetFileName(fullPath),
                    folders: folders.NullToEmpty(),
                    sourceCodeKind: sourceCodeKind,
                    loader: textLoader,
                    filePath: fullPath,
                    isGenerated: false,
                    designTimeOnly: designTimeOnly,
                    documentServiceProvider: documentServiceProvider);

                lock (_project._gate)
                {
                    if (_sourceTextContainersToDocumentIds.ContainsKey(textContainer))
                    {
                        throw new ArgumentException($"{nameof(textContainer)} is already added to this project.", nameof(textContainer));
                    }

                    if (fullPath != null)
                    {
                        if (_documentPathsToDocumentIds.ContainsKey(fullPath))
                        {
                            throw new ArgumentException($"'{fullPath}' has already been added to this project.");
                        }

                        _documentPathsToDocumentIds.Add(fullPath, documentId);
                    }

                    _sourceTextContainersToDocumentIds = _sourceTextContainersToDocumentIds.Add(textContainer, documentInfo.Id);

                    if (_project._activeBatchScopes > 0)
                    {
                        _documentsAddedInBatch.Add(documentInfo);
                    }
                    else
                    {
                        _project._workspace.ApplyChangeToWorkspace(w =>
                        {
                            _project._workspace.AddDocumentToDocumentsNotFromFiles(documentInfo.Id);
                            _documentAddAction(w, documentInfo);
                            w.OnDocumentOpened(documentInfo.Id, textContainer);
                        });
                    }
                }

                return documentId;
            }

            public void AddDynamicFile(IDynamicFileInfoProvider fileInfoProvider, DynamicFileInfo fileInfo, ImmutableArray<string> folders)
            {
                var documentInfo = CreateDocumentInfoFromFileInfo(fileInfo, folders.NullToEmpty());

                // Generally, DocumentInfo.FilePath can be null, but we always have file paths for dynamic files.
                Contract.ThrowIfNull(documentInfo.FilePath);
                var documentId = documentInfo.Id;

                lock (_project._gate)
                {
                    var filePath = documentInfo.FilePath;
                    if (_documentPathsToDocumentIds.ContainsKey(filePath))
                    {
                        throw new ArgumentException($"'{filePath}' has already been added to this project.", nameof(filePath));
                    }

                    // If we have an ordered document ids batch, we need to add the document id to the end of it as well.
                    _orderedDocumentsInBatch = _orderedDocumentsInBatch?.Add(documentId);

                    _documentPathsToDocumentIds.Add(filePath, documentId);

                    _documentIdToDynamicFileInfoProvider.Add(documentId, fileInfoProvider);

                    if (_project._eventSubscriptionTracker.Add(fileInfoProvider))
                    {
                        // subscribe to the event when we use this provider the first time
                        fileInfoProvider.Updated += _project.OnDynamicFileInfoUpdated;
                    }

                    if (_project._activeBatchScopes > 0)
                    {
                        _documentsAddedInBatch.Add(documentInfo);
                    }
                    else
                    {
                        // right now, assumption is dynamically generated file can never be opened in editor
                        _project._workspace.ApplyChangeToWorkspace(w => _documentAddAction(w, documentInfo));
                    }
                }
            }

            public IDynamicFileInfoProvider RemoveDynamicFile(string fullPath)
            {
                if (string.IsNullOrEmpty(fullPath))
                {
                    throw new ArgumentException($"{nameof(fullPath)} isn't a valid path.", nameof(fullPath));
                }

                lock (_project._gate)
                {
                    if (!_documentPathsToDocumentIds.TryGetValue(fullPath, out var documentId) ||
                        !_documentIdToDynamicFileInfoProvider.TryGetValue(documentId, out var fileInfoProvider))
                    {
                        throw new ArgumentException($"'{fullPath}' is not a dynamic file of this project.");
                    }

                    _documentIdToDynamicFileInfoProvider.Remove(documentId);

                    RemoveFileInternal(documentId, fullPath);

                    return fileInfoProvider;
                }
            }

            public void RemoveFile(string fullPath)
            {
                if (string.IsNullOrEmpty(fullPath))
                {
                    throw new ArgumentException($"{nameof(fullPath)} isn't a valid path.", nameof(fullPath));
                }

                lock (_project._gate)
                {
                    if (!_documentPathsToDocumentIds.TryGetValue(fullPath, out var documentId))
                    {
                        throw new ArgumentException($"'{fullPath}' is not a source file of this project.");
                    }

                    _project._documentFileChangeContext.StopWatchingFile(_project._documentFileWatchingTokens[documentId]);
                    _project._documentFileWatchingTokens.Remove(documentId);

                    RemoveFileInternal(documentId, fullPath);
                }
            }

            private void RemoveFileInternal(DocumentId documentId, string fullPath)
            {
                _orderedDocumentsInBatch = _orderedDocumentsInBatch?.Remove(documentId);
                _documentPathsToDocumentIds.Remove(fullPath);

                // There are two cases:
                // 
                // 1. This file is actually been pushed to the workspace, and we need to remove it (either
                //    as a part of the active batch or immediately)
                // 2. It hasn't been pushed yet, but is contained in _documentsAddedInBatch
                if (_documentAlreadyInWorkspace(_project._workspace.CurrentSolution, documentId))
                {
                    if (_project._activeBatchScopes > 0)
                    {
                        _documentsRemovedInBatch.Add(documentId);
                    }
                    else
                    {
                        _project._workspace.ApplyChangeToWorkspace(w => _documentRemoveAction(w, documentId));
                    }
                }
                else
                {
                    for (var i = 0; i < _documentsAddedInBatch.Count; i++)
                    {
                        if (_documentsAddedInBatch[i].Id == documentId)
                        {
                            _documentsAddedInBatch.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            public void RemoveTextContainer(SourceTextContainer textContainer)
            {
                if (textContainer == null)
                {
                    throw new ArgumentNullException(nameof(textContainer));
                }

                lock (_project._gate)
                {
                    if (!_sourceTextContainersToDocumentIds.TryGetValue(textContainer, out var documentId))
                    {
                        throw new ArgumentException($"{nameof(textContainer)} is not a text container added to this project.");
                    }

                    _sourceTextContainersToDocumentIds = _sourceTextContainersToDocumentIds.RemoveKey(textContainer);

                    // if the TextContainer had a full path provided, remove it from the map.
                    var entry = _documentPathsToDocumentIds.Where(kv => kv.Value == documentId).FirstOrDefault();
                    if (entry.Key != null)
                    {
                        _documentPathsToDocumentIds.Remove(entry.Key);
                    }

                    // There are two cases:
                    // 
                    // 1. This file is actually been pushed to the workspace, and we need to remove it (either
                    //    as a part of the active batch or immediately)
                    // 2. It hasn't been pushed yet, but is contained in _documentsAddedInBatch
                    if (_project._workspace.CurrentSolution.GetDocument(documentId) != null)
                    {
                        if (_project._activeBatchScopes > 0)
                        {
                            _documentsRemovedInBatch.Add(documentId);
                        }
                        else
                        {
                            _project._workspace.ApplyChangeToWorkspace(w =>
                            {
                                // Just pass null for the filePath, since this document is immediately being removed
                                // anyways -- whatever we set won't really be read since the next change will
                                // come through.
                                // TODO: Can't we just remove the document without closing it?
                                w.OnDocumentClosed(documentId, new SourceTextLoader(textContainer, filePath: null));
                                _documentRemoveAction(w, documentId);
                                _project._workspace.RemoveDocumentToDocumentsNotFromFiles(documentId);
                            });
                        }
                    }
                    else
                    {
                        for (var i = 0; i < _documentsAddedInBatch.Count; i++)
                        {
                            if (_documentsAddedInBatch[i].Id == documentId)
                            {
                                _documentsAddedInBatch.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }

            public bool ContainsFile(string fullPath)
            {
                if (string.IsNullOrEmpty(fullPath))
                {
                    throw new ArgumentException($"{nameof(fullPath)} isn't a valid path.", nameof(fullPath));
                }

                lock (_project._gate)
                {
                    return _documentPathsToDocumentIds.ContainsKey(fullPath);
                }
            }

            public void ProcessFileChange(string filePath)
                => ProcessFileChange(filePath, filePath);

            /// <summary>
            /// Process file content changes
            /// </summary>
            /// <param name="projectSystemFilePath">filepath given from project system</param>
            /// <param name="workspaceFilePath">filepath used in workspace. it might be different than projectSystemFilePath. ex) dynamic file</param>
            public void ProcessFileChange(string projectSystemFilePath, string workspaceFilePath)
            {
                lock (_project._gate)
                {
                    if (_documentPathsToDocumentIds.TryGetValue(workspaceFilePath, out var documentId))
                    {
                        // We create file watching prior to pushing the file to the workspace in batching, so it's
                        // possible we might see a file change notification early. In this case, toss it out. Since
                        // all adds/removals of documents for this project happen under our lock, it's safe to do this
                        // check without taking the main workspace lock

                        if (_documentsAddedInBatch.Any(d => d.Id == documentId))
                        {
                            return;
                        }

                        _documentIdToDynamicFileInfoProvider.TryGetValue(documentId, out var fileInfoProvider);

                        _project._workspace.ApplyChangeToWorkspace(w =>
                        {
                            if (w.IsDocumentOpen(documentId))
                            {
                                return;
                            }

                            if (fileInfoProvider == null)
                            {
                                var textLoader = new FileTextLoader(projectSystemFilePath, defaultEncoding: null);
                                _documentTextLoaderChangedAction(w, documentId, textLoader);
                            }
                            else
                            {
                                // we do not expect JTF to be used around this code path. and contract of fileInfoProvider is it being real free-threaded
                                // meaning it can't use JTF to go back to UI thread.
                                // so, it is okay for us to call regular ".Result" on a task here.
                                var fileInfo = fileInfoProvider.GetDynamicFileInfoAsync(
                                    _project.Id, _project._filePath, projectSystemFilePath, CancellationToken.None).WaitAndGetResult_CanCallOnBackground(CancellationToken.None);

                                // Right now we're only supporting dynamic files as actual source files, so it's OK to call GetDocument here
                                var document = w.CurrentSolution.GetRequiredDocument(documentId);

                                var documentInfo = DocumentInfo.Create(
                                    document.Id,
                                    document.Name,
                                    document.Folders,
                                    document.SourceCodeKind,
                                    loader: fileInfo.TextLoader,
                                    document.FilePath,
                                    document.State.Attributes.IsGenerated,
                                    document.State.Attributes.DesignTimeOnly,
                                    documentServiceProvider: fileInfo.DocumentServiceProvider);

                                w.OnDocumentReloaded(documentInfo);
                            }
                        });
                    }
                }
            }

            public void ReorderFiles(ImmutableArray<string> filePaths)
            {
                if (filePaths.IsEmpty)
                {
                    throw new ArgumentOutOfRangeException("The specified files are empty.", nameof(filePaths));
                }

                lock (_project._gate)
                {
                    if (_documentPathsToDocumentIds.Count != filePaths.Length)
                    {
                        throw new ArgumentException("The specified files do not equal the project document count.", nameof(filePaths));
                    }

                    var documentIds = ImmutableList.CreateBuilder<DocumentId>();

                    foreach (var filePath in filePaths)
                    {
                        if (_documentPathsToDocumentIds.TryGetValue(filePath, out var documentId))
                        {
                            documentIds.Add(documentId);
                        }
                        else
                        {
                            throw new InvalidOperationException($"The file '{filePath}' does not exist in the project.");
                        }
                    }

                    if (_project._activeBatchScopes > 0)
                    {
                        _orderedDocumentsInBatch = documentIds.ToImmutable();
                    }
                    else
                    {
                        _project._workspace.ApplyBatchChangeToWorkspace(solution =>
                        {
                            var solutionChanges = new SolutionChangeAccumulator(solution);
                            solutionChanges.UpdateSolutionForProjectAction(
                                _project.Id,
                                solutionChanges.Solution.WithProjectDocumentsOrder(_project.Id, documentIds.ToImmutable()));
                            return solutionChanges;
                        });
                    }
                }
            }

            internal void UpdateSolutionForBatch(
                SolutionChangeAccumulator solutionChanges,
                ImmutableArray<string>.Builder documentFileNamesAdded,
                List<(DocumentId documentId, SourceTextContainer textContainer)> documentsToOpen,
                Func<Solution, ImmutableArray<DocumentInfo>, Solution> addDocuments,
                WorkspaceChangeKind addDocumentChangeKind,
                Func<Solution, ImmutableArray<DocumentId>, Solution> removeDocuments,
                WorkspaceChangeKind removeDocumentChangeKind)
            {
                // Document adding...
                solutionChanges.UpdateSolutionForDocumentAction(
                    newSolution: addDocuments(solutionChanges.Solution, _documentsAddedInBatch.ToImmutable()),
                    changeKind: addDocumentChangeKind,
                    documentIds: _documentsAddedInBatch.Select(d => d.Id));

                foreach (var documentInfo in _documentsAddedInBatch)
                {
                    Contract.ThrowIfNull(documentInfo.FilePath, "We shouldn't be adding documents without file paths.");
                    documentFileNamesAdded.Add(documentInfo.FilePath);

                    if (_sourceTextContainersToDocumentIds.TryGetKey(documentInfo.Id, out var textContainer))
                    {
                        documentsToOpen.Add((documentInfo.Id, textContainer));
                    }
                }

                ClearAndZeroCapacity(_documentsAddedInBatch);

                // Document removing...
                solutionChanges.UpdateSolutionForRemovedDocumentAction(removeDocuments(solutionChanges.Solution, _documentsRemovedInBatch.ToImmutableArray()),
                    removeDocumentChangeKind,
                    _documentsRemovedInBatch);

                ClearAndZeroCapacity(_documentsRemovedInBatch);

                // Update project's order of documents.
                if (_orderedDocumentsInBatch != null)
                {
                    solutionChanges.UpdateSolutionForProjectAction(
                        _project.Id,
                        solutionChanges.Solution.WithProjectDocumentsOrder(_project.Id, _orderedDocumentsInBatch));
                    _orderedDocumentsInBatch = null;
                }
            }

            private DocumentInfo CreateDocumentInfoFromFileInfo(DynamicFileInfo fileInfo, ImmutableArray<string> folders)
            {
                Contract.ThrowIfTrue(folders.IsDefault);

                // we use this file path for editorconfig. 
                var filePath = fileInfo.FilePath;

                var name = FileNameUtilities.GetFileName(filePath);
                var documentId = DocumentId.CreateNewId(_project.Id, filePath);

                var textLoader = fileInfo.TextLoader;
                var documentServiceProvider = fileInfo.DocumentServiceProvider;

                return DocumentInfo.Create(
                    documentId,
                    name,
                    folders: folders,
                    sourceCodeKind: fileInfo.SourceCodeKind,
                    loader: textLoader,
                    filePath: filePath,
                    isGenerated: false,
                    designTimeOnly: true,
                    documentServiceProvider: documentServiceProvider);
            }

            private sealed class SourceTextLoader : TextLoader
            {
                private readonly SourceTextContainer _textContainer;
                private readonly string? _filePath;

                public SourceTextLoader(SourceTextContainer textContainer, string? filePath)
                {
                    _textContainer = textContainer;
                    _filePath = filePath;
                }

                public override Task<TextAndVersion> LoadTextAndVersionAsync(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
                    => Task.FromResult(TextAndVersion.Create(_textContainer.CurrentText, VersionStamp.Create(), _filePath));
            }
        }
    }
}
