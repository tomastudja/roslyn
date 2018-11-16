﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem.Extensions;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
{
    internal sealed partial class VisualStudioProjectTracker
    {
        private readonly Workspace _workspace;
        private readonly VisualStudioProjectFactory _projectFactory;
        internal IThreadingContext ThreadingContext { get; }

        [Obsolete("This is a compatibility shim for Live Share; please do not use it.")]
        internal ImmutableArray<AbstractProject> ImmutableProjects => _projects.Values.ToImmutableArray();

        internal HostWorkspaceServices WorkspaceServices => _workspace.Services;

        [Obsolete("This is a compatibility shim for TypeScript and Live Share; please do not use it.")]
        private readonly Dictionary<ProjectId, AbstractProject> _projects = new Dictionary<ProjectId, AbstractProject>();

        [Obsolete("This is a compatibility shim; please do not use it.")]
        public VisualStudioProjectTracker(Workspace workspace, VisualStudioProjectFactory projectFactory, IThreadingContext threadingContext)
        {
            _workspace = workspace;
            _projectFactory = projectFactory;
            ThreadingContext = threadingContext;
            DocumentProvider = new DocumentProvider();
        }

        [Obsolete("This is a compatibility shim for Live Share; please do not use it.")]
        public VisualStudioProjectTracker(IServiceProvider serviceProvider, Workspace workspace)
        {
            _workspace = workspace;
            ThreadingContext = serviceProvider.GetMefService<IThreadingContext>();

            // This is used by Live Share to target their own workspace which is not the standard VS workspace; as a result this shouldn't have a project factory
            // at all, because that's only targeting the VisualStudioWorkspace.
            _projectFactory = null;

            // We don't set DocumentProvider, because Live Share creates their own and then sets it later.
        }

        [Obsolete("This is a compatibility shim for TypeScript; please do not use it.")]
        public DocumentProvider DocumentProvider { get; set; }

        public Workspace Workspace => _workspace;

        [Obsolete("This is a compatibility shim for Live Share; please do not use it.")]
        public void InitializeProviders(DocumentProvider documentProvider, VisualStudioMetadataReferenceManager metadataReferenceProvider, VisualStudioRuleSetManager ruleSetFileProvider)
        {
            DocumentProvider = documentProvider;
        }

        [Obsolete("This is a compatibility shim for Live Share; please do not use it.")]
        public void StartPushingToWorkspaceAndNotifyOfOpenDocuments(IEnumerable<AbstractProject> projects)
        {
            // This shim doesn't expect to get actual things passed to it
            Debug.Assert(!projects.Any());
        }

        /*
          
        private void FinishLoad()
        {
            // Check that the set of analyzers is complete and consistent.
            GetAnalyzerDependencyCheckingService()?.ReanalyzeSolutionForConflicts();
        }

        private AnalyzerDependencyCheckingService GetAnalyzerDependencyCheckingService()
        {
            var componentModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));

            return componentModel.GetService<AnalyzerDependencyCheckingService>();
        }

        */

        public ProjectId GetOrCreateProjectIdForPath(string filePath, string projectDisplayName)
        {
            // HACK: to keep F# working, we will ensure we return the ProjectId if there is a project that matches this path. Otherwise, we'll just return
            // a random ProjectId, which is sufficient for their needs. They'll simply observe there is no project with that ID, and then go and create a
            // new project. Then they call this function again, and fetch the real ID.
            return _workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == filePath)?.Id ?? ProjectId.CreateNewId("ProjectNotFound");
        }

        [Obsolete("This is a compatibility shim for TypeScript and F#; please do not use it.")]
        public AbstractProject GetProject(ProjectId projectId)
        {
            // HACK: if we have a TypeScript project, they expect to return the real thing deriving from AbstractProject
            if (_projects.TryGetValue(projectId, out var typeScriptProject))
            {
                return typeScriptProject;
            }

            // HACK: to keep F# working, we will ensure that if there is a project with that ID, we will return a non-null value, otherwise we'll return null.
            // It doesn't actually matter *what* the project is, so we'll just return something silly
            var project = _workspace.CurrentSolution.GetProject(projectId);

            if (project != null)
            {
                return new StubProject(this, project);
            }
            else
            {
                return null;
            }
        }

        internal bool TryGetProjectByBinPath(string filePath, out AbstractProject project)
        {
            var projectsWithBinPath = _workspace.CurrentSolution.Projects.Where(p => string.Equals(p.OutputFilePath, filePath, StringComparison.OrdinalIgnoreCase)).ToList();

            if (projectsWithBinPath.Count == 1)
            {
                project = new StubProject(this, projectsWithBinPath[0]);
                return true;
            }
            else
            {
                project = null;
                return false;
            }
        }

        private sealed class StubProject : AbstractProject
        {
            private readonly ProjectId _id;

            public StubProject(VisualStudioProjectTracker projectTracker, Project project)
                : base(projectTracker, null, project.Name + "_Stub", project.FilePath, null, project.Language, Guid.Empty, null, null, null, null)
            {
                _id = project.Id;
            }

            public override ProjectId Id => _id;
        }

        [Obsolete("This is a compatibility shim for TypeScript and Live Share; please do not use it.")]
        public void AddProject(AbstractProject project)
        {
            if (_projectFactory != null)
            {
                project.VisualStudioProject = _projectFactory.CreateAndAddToWorkspace(project.ProjectSystemName, project.Language);
                project.UpdateVisualStudioProjectProperties();
            }
            else
            {
                // We don't have an ID, so make something up
                project.ExplicitId = ProjectId.CreateNewId(project.ProjectSystemName);
                Workspace.OnProjectAdded(ProjectInfo.Create(project.ExplicitId, VersionStamp.Create(), project.ProjectSystemName, project.ProjectSystemName, project.Language));
            }

            _projects[project.Id] = project;
        }

        [Obsolete("This is a compatibility shim for TypeScript; please do not use it.")]
        public bool ContainsProject(AbstractProject project)
        {
            // This will be set as long as the project has been added and not since removed
            return _projects.Values.Contains(project);
        }

        [Obsolete("This is a compatibility shim for TypeScript; please do not use it.")]
        public void RemoveProject(AbstractProject project)
        {
            _projects.Remove(project.Id);

            if (project.ExplicitId != null)
            {
                Workspace.OnProjectRemoved(project.ExplicitId);
            }
            else
            {
                project.VisualStudioProject.RemoveFromWorkspace();
                project.VisualStudioProject = null;
            }
        }
    }
}
