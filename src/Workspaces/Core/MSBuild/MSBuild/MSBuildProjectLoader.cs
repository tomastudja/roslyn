﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MSBuild.Logging;
using Roslyn.Utilities;
using MSB = Microsoft.Build;

namespace Microsoft.CodeAnalysis.MSBuild
{
    /// <summary>
    /// An API for loading msbuild project files.
    /// </summary>
    public partial class MSBuildProjectLoader
    {
        // the workspace that the projects and solutions are intended to be loaded into.
        private readonly Workspace _workspace;

        // used to protect access to the following mutable state
        private readonly NonReentrantLock _dataGuard = new NonReentrantLock();
        private ImmutableDictionary<string, string> _properties;
        private readonly Dictionary<string, string> _extensionToLanguageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Create a new instance of an <see cref="MSBuildProjectLoader"/>.
        /// </summary>
        public MSBuildProjectLoader(Workspace workspace, ImmutableDictionary<string, string> properties = null)
        {
            _workspace = workspace;
            _properties = properties ?? ImmutableDictionary<string, string>.Empty;
        }

        /// <summary>
        /// The MSBuild properties used when interpreting project files.
        /// These are the same properties that are passed to msbuild via the /property:&lt;n&gt;=&lt;v&gt; command line argument.
        /// </summary>
        public ImmutableDictionary<string, string> Properties
        {
            get { return _properties; }
        }

        /// <summary>
        /// Determines if metadata from existing output assemblies is loaded instead of opening referenced projects.
        /// If the referenced project is already opened, the metadata will not be loaded.
        /// If the metadata assembly cannot be found the referenced project will be opened instead.
        /// </summary>
        public bool LoadMetadataForReferencedProjects { get; set; } = false;

        /// <summary>
        /// Determines if unrecognized projects are skipped when solutions or projects are opened.
        /// 
        /// A project is unrecognized if it either has 
        ///   a) an invalid file path, 
        ///   b) a non-existent project file,
        ///   c) has an unrecognized file extension or 
        ///   d) a file extension associated with an unsupported language.
        /// 
        /// If unrecognized projects cannot be skipped a corresponding exception is thrown.
        /// </summary>
        public bool SkipUnrecognizedProjects { get; set; } = true;

        /// <summary>
        /// Associates a project file extension with a language name.
        /// </summary>
        public void AssociateFileExtensionWithLanguage(string projectFileExtension, string language)
        {
            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }

            if (projectFileExtension == null)
            {
                throw new ArgumentNullException(nameof(projectFileExtension));
            }

            using (_dataGuard.DisposableWait())
            {
                _extensionToLanguageMap[projectFileExtension] = language;
            }
        }

        private const string SolutionDirProperty = "SolutionDir";

        private void SetSolutionProperties(string solutionFilePath)
        {
            // When MSBuild is building an individual project, it doesn't define $(SolutionDir).
            // However when building an .sln file, or when working inside Visual Studio,
            // $(SolutionDir) is defined to be the directory where the .sln file is located.
            // Some projects out there rely on $(SolutionDir) being set (although the best practice is to
            // use MSBuildProjectDirectory which is always defined).
            if (!string.IsNullOrEmpty(solutionFilePath))
            {
                var solutionDirectory = Path.GetDirectoryName(solutionFilePath);
                if (!solutionDirectory.EndsWith(@"\", StringComparison.Ordinal))
                {
                    solutionDirectory += @"\";
                }

                if (Directory.Exists(solutionDirectory))
                {
                    _properties = _properties.SetItem(SolutionDirProperty, solutionDirectory);
                }
            }
        }

        /// <summary>
        /// Loads the <see cref="SolutionInfo"/> for the specified solution file, including all projects referenced by the solution file and 
        /// all the projects referenced by the project files.
        /// </summary>
        public async Task<SolutionInfo> LoadSolutionInfoAsync(
            string solutionFilePath,
            CancellationToken cancellationToken = default)
        {
            if (solutionFilePath == null)
            {
                throw new ArgumentNullException(nameof(solutionFilePath));
            }

            var absoluteSolutionPath = this.GetAbsoluteSolutionPath(solutionFilePath, Directory.GetCurrentDirectory());
            using (_dataGuard.DisposableWait(cancellationToken))
            {
                this.SetSolutionProperties(absoluteSolutionPath);
            }

            VersionStamp version = default;

            var solutionFile = MSB.Construction.SolutionFile.Parse(absoluteSolutionPath);
            var reportMode = this.SkipUnrecognizedProjects ? ReportMode.Log : ReportMode.Throw;

            // a list to accumulate all the loaded projects
            var loadedProjects = new LoadState(null);

            // load all the projects
            foreach (var project in solutionFile.ProjectsInOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (project.ProjectType != MSB.Construction.SolutionProjectType.SolutionFolder)
                {
                    var projectAbsolutePath = TryGetAbsolutePath(project.AbsolutePath, reportMode);
                    if (projectAbsolutePath != null)
                    {
                        if (TryGetLoaderFromProjectPath(projectAbsolutePath, reportMode, out var loader))
                        {
                            // projects get added to 'loadedProjects' as side-effect
                            // never prefer metadata when loading solution, all projects get loaded if they can.
                            var tmp = await GetOrLoadProjectAsync(projectAbsolutePath, loader, preferMetadata: false, loadedProjects: loadedProjects, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }

            // construct workspace from loaded project infos
            return SolutionInfo.Create(SolutionId.CreateNewId(debugName: absoluteSolutionPath), version, absoluteSolutionPath, loadedProjects.Projects);
        }

        internal string GetAbsoluteSolutionPath(string path, string baseDirectory)
        {
            string absolutePath;

            try
            {
                absolutePath = GetAbsolutePath(path, baseDirectory);
            }
            catch (Exception)
            {
                throw new InvalidOperationException(string.Format(WorkspacesResources.Invalid_solution_file_path_colon_0, path));
            }

            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(string.Format(WorkspacesResources.Solution_file_not_found_colon_0, absolutePath));
            }

            return absolutePath;
        }

        /// <summary>
        /// Loads the <see cref="ProjectInfo"/> from the specified project file and all referenced projects.
        /// The first <see cref="ProjectInfo"/> in the result corresponds to the specified project file.
        /// </summary>
        public async Task<ImmutableArray<ProjectInfo>> LoadProjectInfoAsync(
            string projectFilePath,
            ImmutableDictionary<string, ProjectId> projectPathToProjectIdMap = null,
            CancellationToken cancellationToken = default)
        {
            if (projectFilePath == null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            this.TryGetAbsoluteProjectPath(projectFilePath, Directory.GetCurrentDirectory(), ReportMode.Throw, out var fullPath);
            this.TryGetLoaderFromProjectPath(projectFilePath, ReportMode.Throw, out var loader);

            var loadedProjects = new LoadState(projectPathToProjectIdMap);

            var id = await this.LoadProjectAsync(fullPath, loader, this.LoadMetadataForReferencedProjects, loadedProjects, cancellationToken).ConfigureAwait(false);

            var result = loadedProjects.Projects.Reverse().ToImmutableArray();
            Debug.Assert(result[0].Id == id);
            return result;
        }

        private async Task<ProjectId> GetOrLoadProjectAsync(string projectFilePath, IProjectFileLoader loader, bool preferMetadata, LoadState loadedProjects, CancellationToken cancellationToken)
        {
            var projectId = loadedProjects.GetProjectId(projectFilePath);
            if (projectId == null)
            {
                projectId = await this.LoadProjectAsync(projectFilePath, loader, preferMetadata, loadedProjects, cancellationToken).ConfigureAwait(false);
            }

            return projectId;
        }

        private async Task<ProjectId> LoadProjectAsync(string projectFilePath, IProjectFileLoader loader, bool preferMetadata, LoadState loadedProjects, CancellationToken cancellationToken)
        {
            Debug.Assert(projectFilePath != null);
            Debug.Assert(loader != null);

            var projectId = loadedProjects.GetOrCreateProjectId(projectFilePath);
            var projectName = Path.GetFileNameWithoutExtension(projectFilePath);

            var projectFile = await loader.LoadProjectFileAsync(projectFilePath, _properties, cancellationToken).ConfigureAwait(false);

            // If there were any failures during load, we won't be able to build the project. So, bail early with an empty project.
            if (projectFile.Log.HasFailure)
            {
                ReportDiagnosticLog(projectFile.Log);

                loadedProjects.Add(CreateEmptyProjectInfo(projectId, projectFilePath, loader.Language));
                return projectId;
            }

            var projectFileInfo = await projectFile.GetProjectFileInfoAsync(cancellationToken).ConfigureAwait(false);

            // If any diagnostics were logged during build, we'll carry on and try to produce a meaningful project.
            if (!projectFileInfo.Log.IsEmpty)
            {
                ReportDiagnosticLog(projectFileInfo.Log);
            }

            var outputFilePath = projectFileInfo.OutputFilePath;
            var projectDirectory = Path.GetDirectoryName(projectFilePath);

            var version = GetProjectVersion(projectFilePath);

            // translate information from command line args
            var commandLineParser = _workspace.Services.GetLanguageServices(loader.Language).GetService<ICommandLineParserService>();
            var metadataService = _workspace.Services.GetService<IMetadataService>();
            var analyzerService = _workspace.Services.GetService<IAnalyzerService>();

            var commandLineArgs = commandLineParser.Parse(
                arguments: projectFileInfo.CommandLineArgs,
                baseDirectory: projectDirectory,
                isInteractive: false,
                sdkDirectory: RuntimeEnvironment.GetRuntimeDirectory());

            // we only support file paths in /r command line arguments
            var resolver = new WorkspaceMetadataFileReferenceResolver(metadataService, new RelativePathResolver(commandLineArgs.ReferencePaths, commandLineArgs.BaseDirectory));
            var metadataReferences = commandLineArgs.ResolveMetadataReferences(resolver);

            var analyzerLoader = analyzerService.GetLoader();
            foreach (var path in commandLineArgs.AnalyzerReferences.Select(r => r.FilePath))
            {
                if (File.Exists(path))
                {
                    analyzerLoader.AddDependencyLocation(path);
                }
            }

            var analyzerReferences = commandLineArgs.ResolveAnalyzerReferences(analyzerLoader);

            var defaultEncoding = commandLineArgs.Encoding;

            // docs & additional docs
            var docFileInfos = projectFileInfo.Documents.ToImmutableArrayOrEmpty();
            var additionalDocFileInfos = projectFileInfo.AdditionalDocuments.ToImmutableArrayOrEmpty();

            // check for duplicate documents
            var allDocFileInfos = docFileInfos.AddRange(additionalDocFileInfos);
            CheckDocuments(allDocFileInfos, projectFilePath, projectId);

            var docs = new List<DocumentInfo>();
            foreach (var docFileInfo in docFileInfos)
            {
                GetDocumentNameAndFolders(docFileInfo.LogicalPath, out var name, out var folders);

                docs.Add(DocumentInfo.Create(
                    DocumentId.CreateNewId(projectId, debugName: docFileInfo.FilePath),
                    name,
                    folders,
                    projectFile.GetSourceCodeKind(docFileInfo.FilePath),
                    new FileTextLoader(docFileInfo.FilePath, defaultEncoding),
                    docFileInfo.FilePath,
                    docFileInfo.IsGenerated));
            }

            var additionalDocs = new List<DocumentInfo>();
            foreach (var docFileInfo in additionalDocFileInfos)
            {
                GetDocumentNameAndFolders(docFileInfo.LogicalPath, out var name, out var folders);

                additionalDocs.Add(DocumentInfo.Create(
                    DocumentId.CreateNewId(projectId, debugName: docFileInfo.FilePath),
                    name,
                    folders,
                    SourceCodeKind.Regular,
                    new FileTextLoader(docFileInfo.FilePath, defaultEncoding),
                    docFileInfo.FilePath,
                    docFileInfo.IsGenerated));
            }

            // project references
            var resolvedReferences = await ResolveProjectReferencesAsync(
                projectId, projectFilePath, projectFileInfo.ProjectReferences, preferMetadata, loadedProjects, cancellationToken).ConfigureAwait(false);

            // add metadata references for project refs converted to metadata refs
            metadataReferences = metadataReferences.Concat(resolvedReferences.MetadataReferences)
                .Where(m => !(m is UnresolvedMetadataReference));

            // if the project file loader couldn't figure out an assembly name, make one using the project's file path.
            var assemblyName = commandLineArgs.CompilationName;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = GetAssemblyNameFromProjectPath(projectFilePath);
            }

            // make sure that doc-comments at least get parsed.
            var parseOptions = commandLineArgs.ParseOptions;
            if (parseOptions.DocumentationMode == DocumentationMode.None)
            {
                parseOptions = parseOptions.WithDocumentationMode(DocumentationMode.Parse);
            }

            // add all the extra options that are really behavior overrides
            var compOptions = commandLineArgs.CompilationOptions
                    .WithXmlReferenceResolver(new XmlFileResolver(projectDirectory))
                    .WithSourceReferenceResolver(new SourceFileResolver(ImmutableArray<string>.Empty, projectDirectory))
                    // TODO: https://github.com/dotnet/roslyn/issues/4967
                    .WithMetadataReferenceResolver(new WorkspaceMetadataFileReferenceResolver(metadataService, new RelativePathResolver(ImmutableArray<string>.Empty, projectDirectory)))
                    .WithStrongNameProvider(new DesktopStrongNameProvider(commandLineArgs.KeyFileSearchPaths))
                    .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

            loadedProjects.Add(
                ProjectInfo.Create(
                    projectId,
                    version,
                    projectName,
                    assemblyName,
                    loader.Language,
                    projectFilePath,
                    outputFilePath,
                    compilationOptions: compOptions,
                    parseOptions: parseOptions,
                    documents: docs,
                    projectReferences: resolvedReferences.ProjectReferences,
                    metadataReferences: metadataReferences,
                    analyzerReferences: analyzerReferences,
                    additionalDocuments: additionalDocs,
                    isSubmission: false,
                    hostObjectType: null));

            return projectId;
        }

        private static string GetMsbuildFailedMessage(string projectFilePath, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Format(WorkspaceMSBuildResources.Msbuild_failed_when_processing_the_file_0, projectFilePath);
            }
            else
            {
                return string.Format(WorkspaceMSBuildResources.Msbuild_failed_when_processing_the_file_0_with_message_1, projectFilePath, message);
            }
        }

        private static VersionStamp GetProjectVersion(string projectFilePath)
        {
            if (!string.IsNullOrEmpty(projectFilePath) && File.Exists(projectFilePath))
            {
                return VersionStamp.Create(File.GetLastWriteTimeUtc(projectFilePath));
            }
            else
            {
                return VersionStamp.Create();
            }
        }

        private ProjectInfo CreateEmptyProjectInfo(ProjectId projectId, string projectFilePath, string language)
        {
            var languageService = _workspace.Services.GetLanguageServices(language);
            var parseOptions = languageService.GetService<ISyntaxTreeFactoryService>().GetDefaultParseOptions();
            var compilationOptions = languageService.GetService<ICompilationFactoryService>().GetDefaultCompilationOptions();
            var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
            var version = GetProjectVersion(projectFilePath);

            return ProjectInfo.Create(
                projectId,
                version,
                projectName,
                assemblyName: GetAssemblyNameFromProjectPath(projectFilePath),
                language: language,
                filePath: projectFilePath,
                outputFilePath: string.Empty,
                compilationOptions: compilationOptions,
                parseOptions: parseOptions,
                documents: SpecializedCollections.EmptyEnumerable<DocumentInfo>(),
                projectReferences: SpecializedCollections.EmptyEnumerable<ProjectReference>(),
                metadataReferences: SpecializedCollections.EmptyEnumerable<MetadataReference>(),
                analyzerReferences: SpecializedCollections.EmptyEnumerable<AnalyzerReference>(),
                additionalDocuments: SpecializedCollections.EmptyEnumerable<DocumentInfo>(),
                isSubmission: false,
                hostObjectType: null);
        }

        private static string GetAssemblyNameFromProjectPath(string projectFilePath)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(projectFilePath);

            // if this is still unreasonable, use a fixed name.
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = "assembly";
            }

            return assemblyName;
        }

        private static readonly char[] s_directorySplitChars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        private static void GetDocumentNameAndFolders(string logicalPath, out string name, out ImmutableArray<string> folders)
        {
            var pathNames = logicalPath.Split(s_directorySplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (pathNames.Length > 0)
            {
                if (pathNames.Length > 1)
                {
                    folders = pathNames.Take(pathNames.Length - 1).ToImmutableArray();
                }
                else
                {
                    folders = ImmutableArray.Create<string>();
                }

                name = pathNames[pathNames.Length - 1];
            }
            else
            {
                name = logicalPath;
                folders = ImmutableArray.Create<string>();
            }
        }

        private void CheckDocuments(ImmutableArray<DocumentFileInfo> docs, string projectFilePath, ProjectId projectId)
        {
            var paths = new HashSet<string>();
            foreach (var doc in docs)
            {
                if (paths.Contains(doc.FilePath))
                {
                    _workspace.OnWorkspaceFailed(new ProjectDiagnostic(WorkspaceDiagnosticKind.Warning, string.Format(WorkspacesResources.Duplicate_source_file_0_in_project_1, doc.FilePath, projectFilePath), projectId));
                }

                paths.Add(doc.FilePath);
            }
        }

        private class ResolvedReferences
        {
            public readonly List<ProjectReference> ProjectReferences = new List<ProjectReference>();
            public readonly List<MetadataReference> MetadataReferences = new List<MetadataReference>();
        }

        private async Task<ResolvedReferences> ResolveProjectReferencesAsync(
            ProjectId thisProjectId,
            string thisProjectPath,
            IReadOnlyList<ProjectFileReference> projectFileReferences,
            bool preferMetadata,
            LoadState loadedProjects,
            CancellationToken cancellationToken)
        {
            var resolvedReferences = new ResolvedReferences();
            var reportMode = this.SkipUnrecognizedProjects ? ReportMode.Log : ReportMode.Throw;

            foreach (var projectFileReference in projectFileReferences)
            {
                if (TryGetAbsoluteProjectPath(projectFileReference.Path, Path.GetDirectoryName(thisProjectPath), reportMode, out var fullPath))
                {
                    // if the project is already loaded, then just reference the one we have
                    var existingProjectId = loadedProjects.GetProjectId(fullPath);
                    if (existingProjectId != null)
                    {
                        resolvedReferences.ProjectReferences.Add(new ProjectReference(existingProjectId, projectFileReference.Aliases));
                        continue;
                    }

                    TryGetLoaderFromProjectPath(fullPath, ReportMode.Ignore, out var loader);

                    // get metadata if preferred or if loader is unknown
                    if (preferMetadata || loader == null)
                    {
                        var projectMetadata = await this.GetProjectMetadata(fullPath, projectFileReference.Aliases, _properties, cancellationToken).ConfigureAwait(false);
                        if (projectMetadata != null)
                        {
                            resolvedReferences.MetadataReferences.Add(projectMetadata);
                            continue;
                        }
                    }

                    // must load, so we really need loader
                    if (TryGetLoaderFromProjectPath(fullPath, reportMode, out loader))
                    {
                        // load the project
                        var projectId = await this.GetOrLoadProjectAsync(fullPath, loader, preferMetadata, loadedProjects, cancellationToken).ConfigureAwait(false);

                        // If that other project already has a reference on us, this will cause a circularity.
                        // This check doesn't need to be in the "already loaded" path above, since in any circularity this path
                        // must be taken at least once.
                        if (loadedProjects.ProjectAlreadyReferencesProject(projectId, targetProject: thisProjectId))
                        {
                            // We'll try to make this metadata if we can
                            var projectMetadata = await this.GetProjectMetadata(fullPath, projectFileReference.Aliases, _properties, cancellationToken).ConfigureAwait(false);
                            if (projectMetadata != null)
                            {
                                resolvedReferences.MetadataReferences.Add(projectMetadata);
                            }
                            continue;
                        }
                        else
                        {
                            resolvedReferences.ProjectReferences.Add(new ProjectReference(projectId, projectFileReference.Aliases));
                            continue;
                        }
                    }
                }
                else
                {
                    fullPath = projectFileReference.Path;
                }

                // cannot find metadata and project cannot be loaded, so leave a project reference to a non-existent project.
                var id = loadedProjects.GetOrCreateProjectId(fullPath);
                resolvedReferences.ProjectReferences.Add(new ProjectReference(id, projectFileReference.Aliases));
            }

            return resolvedReferences;
        }

        /// <summary>
        /// Gets a MetadataReference to a project's output assembly.
        /// </summary>
        private async Task<MetadataReference> GetProjectMetadata(string projectFilePath, ImmutableArray<string> aliases, IDictionary<string, string> globalProperties, CancellationToken cancellationToken)
        {
            // use loader service to determine output file for project if possible
            string outputFilePath = null;

            try
            {
                outputFilePath = await ProjectFileLoader.GetOutputFilePathAsync(projectFilePath, globalProperties, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _workspace.OnWorkspaceFailed(new WorkspaceDiagnostic(WorkspaceDiagnosticKind.Failure, e.Message));
            }

            if (outputFilePath != null && File.Exists(outputFilePath))
            {
                if (Workspace.TestHookStandaloneProjectsDoNotHoldReferences)
                {
                    var documentationService = _workspace.Services.GetService<IDocumentationProviderService>();
                    var docProvider = documentationService.GetDocumentationProvider(outputFilePath);
                    var metadata = AssemblyMetadata.CreateFromImage(File.ReadAllBytes(outputFilePath));

                    return metadata.GetReference(
                        documentation: docProvider,
                        aliases: aliases,
                        display: outputFilePath);
                }
                else
                {
                    var metadataService = _workspace.Services.GetService<IMetadataService>();
                    return metadataService.GetReference(outputFilePath, new MetadataReferenceProperties(MetadataImageKind.Assembly, aliases));
                }
            }

            return null;
        }

        private string TryGetAbsolutePath(string path, ReportMode mode)
        {
            try
            {
                path = Path.GetFullPath(path);
            }
            catch (Exception)
            {
                ReportFailure(mode, string.Format(WorkspacesResources.Invalid_project_file_path_colon_0, path));
                return null;
            }

            if (!File.Exists(path))
            {
                ReportFailure(
                    mode,
                    string.Format(WorkspacesResources.Project_file_not_found_colon_0, path),
                    msg => new FileNotFoundException(msg));
                return null;
            }

            return path;
        }

        internal bool TryGetLoaderFromProjectPath(string projectFilePath, out IProjectFileLoader loader)
        {
            return TryGetLoaderFromProjectPath(projectFilePath, ReportMode.Ignore, out loader);
        }

        private bool TryGetLoaderFromProjectPath(string projectFilePath, ReportMode mode, out IProjectFileLoader loader)
        {
            using (_dataGuard.DisposableWait())
            {
                // otherwise try to figure it out from extension
                var extension = Path.GetExtension(projectFilePath);
                if (extension.Length > 0 && extension[0] == '.')
                {
                    extension = extension.Substring(1);
                }

                if (_extensionToLanguageMap.TryGetValue(extension, out var language))
                {
                    if (_workspace.Services.SupportedLanguages.Contains(language))
                    {
                        loader = _workspace.Services.GetLanguageServices(language).GetService<IProjectFileLoader>();
                    }
                    else
                    {
                        loader = null;
                        this.ReportFailure(mode, string.Format(WorkspacesResources.Cannot_open_project_0_because_the_language_1_is_not_supported, projectFilePath, language));
                        return false;
                    }
                }
                else
                {
                    loader = ProjectFileLoader.GetLoaderForProjectFileExtension(_workspace, extension);

                    if (loader == null)
                    {
                        this.ReportFailure(mode, string.Format(WorkspacesResources.Cannot_open_project_0_because_the_file_extension_1_is_not_associated_with_a_language, projectFilePath, Path.GetExtension(projectFilePath)));
                        return false;
                    }
                }

                // since we have both C# and VB loaders in this same library, it no longer indicates whether we have full language support available.
                if (loader != null)
                {
                    language = loader.Language;

                    // check for command line parser existing... if not then error.
                    var commandLineParser = _workspace.Services.GetLanguageServices(language).GetService<ICommandLineParserService>();
                    if (commandLineParser == null)
                    {
                        loader = null;
                        this.ReportFailure(mode, string.Format(WorkspacesResources.Cannot_open_project_0_because_the_language_1_is_not_supported, projectFilePath, language));
                        return false;
                    }
                }

                return loader != null;
            }
        }

        private bool TryGetAbsoluteProjectPath(string path, string baseDirectory, ReportMode mode, out string absolutePath)
        {
            try
            {
                absolutePath = GetAbsolutePath(path, baseDirectory);
            }
            catch (Exception)
            {
                ReportFailure(mode, string.Format(WorkspacesResources.Invalid_project_file_path_colon_0, path));
                absolutePath = null;
                return false;
            }

            if (!File.Exists(absolutePath))
            {
                ReportFailure(
                    mode,
                    string.Format(WorkspacesResources.Project_file_not_found_colon_0, absolutePath),
                    msg => new FileNotFoundException(msg));
                return false;
            }

            return true;
        }

        private static string GetAbsolutePath(string path, string baseDirectoryPath)
        {
            return Path.GetFullPath(FileUtilities.ResolveRelativePath(path, baseDirectoryPath) ?? path);
        }

        private enum ReportMode
        {
            Throw,
            Log,
            Ignore
        }

        private void ReportFailure(ReportMode mode, string message, Func<string, Exception> createException = null)
        {
            switch (mode)
            {
                case ReportMode.Throw:
                    if (createException != null)
                    {
                        throw createException(message);
                    }
                    else
                    {
                        throw new InvalidOperationException(message);
                    }

                case ReportMode.Log:
                    _workspace.OnWorkspaceFailed(new WorkspaceDiagnostic(WorkspaceDiagnosticKind.Failure, message));
                    break;

                case ReportMode.Ignore:
                default:
                    break;
            }
        }

        private void ReportDiagnosticLog(DiagnosticLog log)
        {
            foreach (var logItem in log)
            {
                ReportFailure(ReportMode.Log, GetMsbuildFailedMessage(logItem.ProjectFilePath, logItem.ToString()));
            }
        }
    }
}
