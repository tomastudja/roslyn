﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Xml.Linq;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.IntegrationTest.Utilities.InProcess;
using ProjectUtils = Microsoft.VisualStudio.IntegrationTest.Utilities.Common.ProjectUtils;

namespace Microsoft.VisualStudio.IntegrationTest.Utilities.OutOfProcess
{
    public partial class SolutionExplorer_OutOfProc : OutOfProcComponent
    {
        public Verifier Verify { get; }

        private readonly SolutionExplorer_InProc _inProc;
        private readonly VisualStudioInstance _instance;

        public SolutionExplorer_OutOfProc(VisualStudioInstance visualStudioInstance)
            : base(visualStudioInstance)
        {
            _instance = visualStudioInstance;
            _inProc = CreateInProcComponent<SolutionExplorer_InProc>(visualStudioInstance);
            Verify = new Verifier(this);
        }

        public void CloseSolution(bool saveFirst = false)
            => _inProc.CloseSolution(saveFirst);

        /// <summary>
        /// The full file path to the solution file.
        /// </summary>
        public string SolutionFileFullPath => _inProc.SolutionFileFullPath;

        /// <summary>
        /// Creates and loads a new solution in the host process, optionally saving the existing solution if one exists.
        /// </summary>
        public void CreateSolution(string solutionName, bool saveExistingSolutionIfExists = false)
            => _inProc.CreateSolution(solutionName, saveExistingSolutionIfExists);

        public void CreateSolution(string solutionName, XElement solutionElement)
            => _inProc.CreateSolution(solutionName, solutionElement.ToString());

        public void OpenSolution(string path, bool saveExistingSolutionIfExists = false)
            => _inProc.OpenSolution(path, saveExistingSolutionIfExists);

        public void AddProject(ProjectUtils.Project projectName, string projectTemplate, string languageName)
            => _inProc.AddProject(projectName.Name, projectTemplate, languageName);

        public void AddProjectReference(ProjectUtils.Project fromProjectName, ProjectUtils.ProjectReference toProjectName)
        {
           _inProc.AddProjectReference(fromProjectName.Name, toProjectName.Name);
            _instance.Workspace.WaitForAsyncOperations(FeatureAttribute.Workspace);
        }

        public void RemoveProjectReference(ProjectUtils.Project projectName, ProjectUtils.ProjectReference projectReferenceName)
        {
            _inProc.RemoveProjectReference(projectName.Name, projectReferenceName.Name);
            _instance.Workspace.WaitForAsyncOperations(FeatureAttribute.Workspace);
        }

        public void AddMetadataReference(ProjectUtils.AssemblyReference fullyQualifiedAssemblyName, ProjectUtils.Project projectName)
        {
            _inProc.AddMetadataReference(fullyQualifiedAssemblyName.Name, projectName.Name);
            _instance.Workspace.WaitForAsyncOperations(FeatureAttribute.Workspace);
        }

        public void RemoveMetadataReference(ProjectUtils.AssemblyReference assemblyName, ProjectUtils.Project projectName)
        {
            _inProc.RemoveMetadataReference(assemblyName.Name, projectName.Name);
            _instance.Workspace.WaitForAsyncOperations(FeatureAttribute.Workspace);
        }

        public void CleanUpOpenSolution()
            => _inProc.CleanUpOpenSolution();

        public void AddFile(ProjectUtils.Project project, string fileName, string contents = null, bool open = false)
            => _inProc.AddFile(project.Name, fileName, contents, open);

        public void SetFileContents(ProjectUtils.Project project, string fileName, string contents)
            => _inProc.SetFileContents(project.Name, fileName, contents);

        public string GetFileContents(ProjectUtils.Project project, string fileName)
            => _inProc.GetFileContents(project.Name, fileName);

        public void BuildSolution(bool waitForBuildToFinish)
            => _inProc.BuildSolution(waitForBuildToFinish);

        public void OpenFileWithDesigner(ProjectUtils.Project project, string fileName)
            => _inProc.OpenFileWithDesigner(project.Name, fileName);

        public void OpenFile(ProjectUtils.Project project, string fileName)
            => _inProc.OpenFile(project.Name, fileName);

        public void CloseFile(ProjectUtils.Project project, string fileName, bool saveFile)
            => _inProc.CloseFile(project.Name, fileName, saveFile);

        public void SaveFile(ProjectUtils.Project project, string fileName)
            => _inProc.SaveFile(project.Name, fileName);

        public void ReloadProject(ProjectUtils.Project project)
            => _inProc.ReloadProject(project.Name);

        public void RestoreNuGetPackages()
            => _inProc.RestoreNuGetPackages();

        public void SaveAll()
            => _inProc.SaveAll();

        public void ShowOutputWindow()
            => _inProc.ShowOutputWindow();

        public void UnloadProject(ProjectUtils.Project project)
            => _inProc.UnloadProject(project.Name);

        public string[] GetProjectReferences(ProjectUtils.Project project)
            => _inProc.GetProjectReferences(project.Name);

        public string[] GetAssemblyReferences(ProjectUtils.Project project)
            => _inProc.GetAssemblyReferences(project.Name);

        public void SelectItem(string itemName)
            => _inProc.SelectItem(itemName);

        public void ClearBuildOutputWindowPane()
            => _inProc.ClearBuildOutputWindowPane();

        public void WaitForBuildToFinish()
            => _inProc.WaitForBuildToFinish();

        public void EditProjectFile(ProjectUtils.Project project)
            => _inProc.EditProjectFile(project.Name);
    }
}