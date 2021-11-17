﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.UnitTests;
using Microsoft.CodeAnalysis.UnitTests.TestFiles;
using Microsoft.CodeAnalysis.VisualBasic;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.MSBuild.UnitTests
{
    public class NetCoreTests : MSBuildWorkspaceTestBase
    {
        private readonly TempDirectory _nugetCacheDir;

        public NetCoreTests()
        {
            _nugetCacheDir = SolutionDirectory.CreateDirectory(".packages");
        }

        private void RunDotNet(string arguments)
        {
            Assert.NotNull(DotNetSdkMSBuildInstalled.SdkPath);

            var environmentVariables = new Dictionary<string, string>()
            {
                ["NUGET_PACKAGES"] = _nugetCacheDir.Path
            };

            var dotNetExeName = "dotnet" + (Path.DirectorySeparatorChar == '/' ? "" : ".exe");
            var exePath = Path.Combine(DotNetSdkMSBuildInstalled.SdkPath, dotNetExeName);

            var restoreResult = ProcessUtilities.Run(
                exePath, arguments,
                workingDirectory: SolutionDirectory.Path,
                additionalEnvironmentVars: environmentVariables);

            Assert.True(restoreResult.ExitCode == 0, $"{exePath} failed with exit code {restoreResult.ExitCode}: {restoreResult.Output}");
        }

        private void DotNetRestore(string solutionOrProjectFileName)
        {
            var arguments = $@"msbuild ""{solutionOrProjectFileName}"" /t:restore /bl:{Path.Combine(SolutionDirectory.Path, "restore.binlog")}";
            RunDotNet(arguments);
        }

        private void DotNetBuild(string solutionOrProjectFileName, string configuration = null)
        {
            var arguments = $@"msbuild ""{solutionOrProjectFileName}"" /bl:{Path.Combine(SolutionDirectory.Path, "build.binlog")}";

            if (configuration != null)
            {
                arguments += $" /p:Configuration={configuration}";
            }

            RunDotNet(arguments);
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProject_NetCoreApp()
        {
            CreateFiles(GetNetCoreAppFiles());

            var projectFilePath = GetSolutionFileName("Project.csproj");

            DotNetRestore("Project.csproj");

            using (var workspace = CreateMSBuildWorkspace())
            {
                var project = await workspace.OpenProjectAsync(projectFilePath);

                // Assert that there is a single project loaded.
                Assert.Single(workspace.CurrentSolution.ProjectIds);

                // Assert that the project does not have any diagnostics in Program.cs
                var document = project.Documents.First(d => d.Name == "Program.cs");
                var semanticModel = await document.GetSemanticModelAsync();
                var diagnostics = semanticModel.GetDiagnostics();
                Assert.Empty(diagnostics);
            }
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProjectTwice_NetCoreAppAndLibrary()
        {
            CreateFiles(GetNetCoreAppAndLibraryFiles());

            var projectFilePath = GetSolutionFileName(@"Project\Project.csproj");
            var libraryFilePath = GetSolutionFileName(@"Library\Library.csproj");

            DotNetRestore(@"Project\Project.csproj");

            using var workspace = CreateMSBuildWorkspace();
            var libraryProject = await workspace.OpenProjectAsync(libraryFilePath);

            // Assert that there is a single project loaded.
            Assert.Single(workspace.CurrentSolution.ProjectIds);

            // Assert that the project does not have any diagnostics in Class1.cs
            var document = libraryProject.Documents.First(d => d.Name == "Class1.cs");
            var semanticModel = await document.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();
            Assert.Empty(diagnostics);

            var project = await workspace.OpenProjectAsync(projectFilePath);

            // Assert that there are only two projects opened.
            Assert.Equal(2, workspace.CurrentSolution.ProjectIds.Count);

            // Assert that there is a project reference between Project.csproj and Library.csproj
            var projectReference = Assert.Single(project.ProjectReferences);

            var projectRefId = projectReference.ProjectId;
            Assert.Equal(libraryProject.Id, projectRefId);
            Assert.Equal(libraryProject.FilePath, workspace.CurrentSolution.GetProject(projectRefId).FilePath);
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProjectTwice_NetCoreAppAndTwoLibraries()
        {
            CreateFiles(GetNetCoreAppAndTwoLibrariesFiles());

            var projectFilePath = GetSolutionFileName(@"Project\Project.csproj");
            var library1FilePath = GetSolutionFileName(@"Library1\Library1.csproj");
            var library2FilePath = GetSolutionFileName(@"Library2\Library2.csproj");

            DotNetRestore(@"Project\Project.csproj");
            DotNetRestore(@"Library2\Library2.csproj");

            // Warning: Found project reference without a matching metadata reference: Library1.csproj
            using var workspace = CreateMSBuildWorkspace(throwOnWorkspaceFailed: false);
            var project = await workspace.OpenProjectAsync(projectFilePath);

            // Assert that there is are two projects loaded (Project.csproj references Library1.csproj).
            Assert.Equal(2, workspace.CurrentSolution.ProjectIds.Count);

            // Assert that the project does not have any diagnostics in Program.cs
            var document = project.Documents.First(d => d.Name == "Program.cs");
            var semanticModel = await document.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();
            Assert.Empty(diagnostics);

            var library2 = await workspace.OpenProjectAsync(library2FilePath);

            // Assert that there are now three projects loaded (Library2.csproj also references Library1.csproj)
            Assert.Equal(3, workspace.CurrentSolution.ProjectIds.Count);

            // Assert that there is a project reference between Project.csproj and Library1.csproj
            AssertSingleProjectReference(project, library1FilePath);

            // Assert that there is a project reference between Library2.csproj and Library1.csproj
            AssertSingleProjectReference(library2, library1FilePath);

            static void AssertSingleProjectReference(Project project, string projectRefFilePath)
            {
                var projectReference = Assert.Single(project.ProjectReferences);

                var projectRefId = projectReference.ProjectId;
                Assert.Equal(projectRefFilePath, project.Solution.GetProject(projectRefId).FilePath);
            }
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProject_NetCoreMultiTFM()
        {
            CreateFiles(GetNetCoreMultiTFMFiles());

            var projectFilePath = GetSolutionFileName("Project.csproj");

            DotNetRestore("Project.csproj");

            using (var workspace = CreateMSBuildWorkspace())
            {
                await workspace.OpenProjectAsync(projectFilePath);

                // Assert that three projects have been loaded, one for each TFM.
                Assert.Equal(3, workspace.CurrentSolution.ProjectIds.Count);

                var projectPaths = new HashSet<string>();
                var outputFilePaths = new HashSet<string>();

                foreach (var project in workspace.CurrentSolution.Projects)
                {
                    projectPaths.Add(project.FilePath);
                    outputFilePaths.Add(project.OutputFilePath);
                }

                // Assert that the three projects share the same file path
                Assert.Single(projectPaths);

                // Assert that the three projects have different output file paths
                Assert.Equal(3, outputFilePaths.Count);

                // Assert that none of the projects have any diagnostics in Program.cs
                foreach (var project in workspace.CurrentSolution.Projects)
                {
                    var document = project.Documents.First(d => d.Name == "Program.cs");
                    var semanticModel = await document.GetSemanticModelAsync();
                    var diagnostics = semanticModel.GetDiagnostics();
                    Assert.Empty(diagnostics);
                }
            }
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProject_NetCoreMultiTFM_ExtensionWithConditionOnTFM()
        {
            CreateFiles(GetNetCoreMultiTFMFiles_ExtensionWithConditionOnTFM());

            var projectFilePath = GetSolutionFileName("Project.csproj");

            DotNetRestore("Project.csproj");

            using (var workspace = CreateMSBuildWorkspace())
            {
                await workspace.OpenProjectAsync(projectFilePath);

                // Assert that three projects have been loaded, one for each TFM.
                Assert.Equal(3, workspace.CurrentSolution.ProjectIds.Count);

                // Assert the TFM is accessible from project extensions.
                // The test project extension sets the default namespace based on the TFM.  
                foreach (var project in workspace.CurrentSolution.Projects)
                {
                    switch (project.Name)
                    {
                        case "Project(net6)":
                            Assert.Equal("Project.NetCore", project.DefaultNamespace);
                            break;

                        case "Project(netstandard2.0)":
                            Assert.Equal("Project.NetStandard", project.DefaultNamespace);
                            break;

                        case "Project(net5)":
                            Assert.Equal("Project.NetFramework", project.DefaultNamespace);
                            break;

                        default:
                            Assert.True(false, $"Unexpected project: {project.Name}");
                            break;
                    }
                }
            }
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProject_NetCoreMultiTFM_ProjectReference()
        {
            CreateFiles(GetNetCoreMultiTFMFiles_ProjectReference());

            // Restoring for Project.csproj should also restore Library.csproj
            DotNetRestore(@"Project\Project.csproj");

            var projectFilePath = GetSolutionFileName(@"Project\Project.csproj");

            await AssertNetCoreMultiTFMProject(projectFilePath);
        }

        private static async Task AssertNetCoreMultiTFMProject(string projectFilePath)
        {
            using (var workspace = CreateMSBuildWorkspace())
            {
                await workspace.OpenProjectAsync(projectFilePath);

                // Assert that four projects have been loaded, one for each TFM.
                Assert.Equal(4, workspace.CurrentSolution.ProjectIds.Count);

                var projectPaths = new HashSet<string>();
                var outputFilePaths = new HashSet<string>();

                foreach (var project in workspace.CurrentSolution.Projects)
                {
                    projectPaths.Add(project.FilePath);
                    outputFilePaths.Add(project.OutputFilePath);
                }

                // Assert that there are two project file path among the four projects
                Assert.Equal(2, projectPaths.Count);

                // Assert that the four projects each have different output file paths
                Assert.Equal(4, outputFilePaths.Count);

                var expectedNames = new HashSet<string>()
                {
                    "Project(net6)",
                    "Project(net5)",
                    "Library(netstandard2",
                    "Library(net5)"
                };

                var actualNames = new HashSet<string>();

                foreach (var project in workspace.CurrentSolution.Projects)
                {
                    var dotIndex = project.Name.IndexOf('.');
                    var projectName = dotIndex >= 0
                        ? project.Name.Substring(0, dotIndex)
                        : project.Name;

                    actualNames.Add(projectName);
                    var fileName = PathUtilities.GetFileName(project.FilePath);

                    Document document;

                    switch (fileName)
                    {
                        case "Project.csproj":
                            document = project.Documents.First(d => d.Name == "Program.cs");
                            break;

                        case "Library.csproj":
                            document = project.Documents.First(d => d.Name == "Class1.cs");
                            break;

                        default:
                            Assert.True(false, $"Encountered unexpected project: {project.FilePath}");
                            return;
                    }

                    // Assert that none of the projects have any diagnostics in their primary .cs file.
                    var semanticModel = await document.GetSemanticModelAsync();
                    var diagnostics = semanticModel.GetDiagnostics();
                    Assert.Empty(diagnostics);
                }

                Assert.True(actualNames.SetEquals(expectedNames), $"Project names differ!{Environment.NewLine}Actual: {{{actualNames.Join(",")}}}{Environment.NewLine}Expected: {{{expectedNames.Join(",")}}}");

                // Verify that the projects reference the correct TFMs
                var projects = workspace.CurrentSolution.Projects.Where(p => p.FilePath.EndsWith("Project.csproj"));
                foreach (var project in projects)
                {
                    var projectReference = Assert.Single(project.ProjectReferences);

                    var referencedProject = workspace.CurrentSolution.GetProject(projectReference.ProjectId);

                    if (project.OutputFilePath.Contains("net6"))
                    {
                        Assert.Contains("net5", referencedProject.OutputFilePath);
                    }
                    else if (project.OutputFilePath.Contains("net5"))
                    {
                        Assert.Contains("net5", referencedProject.OutputFilePath);
                    }
                    else
                    {
                        Assert.True(false, "OutputFilePath with expected TFM not found.");
                    }
                }
            }
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenSolution_NetCoreMultiTFMWithProjectReferenceToFSharp()
        {
            CreateFiles(GetNetCoreMultiTFMFiles_ProjectReferenceToFSharp());

            var solutionFilePath = GetSolutionFileName("Solution.sln");

            DotNetRestore("Solution.sln");

            using (var workspace = CreateMSBuildWorkspace())
            {
                var solution = await workspace.OpenSolutionAsync(solutionFilePath);

                var projects = solution.Projects.ToArray();

                Assert.Equal(2, projects.Length);

                foreach (var project in projects)
                {
                    Assert.StartsWith("csharplib", project.Name);
                    Assert.Empty(project.ProjectReferences);
                    Assert.Single(project.AllProjectReferences);
                }
            }
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProject_ReferenceConfigurationSpecificMetadata()
        {
            var files = GetBaseFiles()
                .WithFile(@"Solution.sln", Resources.SolutionFiles.Issue30174_Solution)
                .WithFile(@"InspectedLibrary\InspectedLibrary.csproj", Resources.ProjectFiles.CSharp.Issue30174_InspectedLibrary)
                .WithFile(@"InspectedLibrary\InspectedClass.cs", Resources.SourceFiles.CSharp.Issue30174_InspectedClass)
                .WithFile(@"ReferencedLibrary\ReferencedLibrary.csproj", Resources.ProjectFiles.CSharp.Issue30174_ReferencedLibrary)
                .WithFile(@"ReferencedLibrary\SomeMetadataAttribute.cs", Resources.SourceFiles.CSharp.Issue30174_SomeMetadataAttribute);

            CreateFiles(files);

            DotNetRestore("Solution.sln");
            DotNetBuild("Solution.sln", configuration: "Release");

            var projectFilePath = GetSolutionFileName(@"InspectedLibrary\InspectedLibrary.csproj");

            using var workspace = CreateMSBuildWorkspace(throwOnWorkspaceFailed: true, ("Configuration", "Release"));
            workspace.LoadMetadataForReferencedProjects = true;

            var project = await workspace.OpenProjectAsync(projectFilePath);

            Assert.Empty(project.ProjectReferences);
            Assert.Empty(workspace.Diagnostics);

            var compilation = await project.GetCompilationAsync();
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProject_OverrideTFM()
        {
            CreateFiles(GetNetCoreAppAndLibraryFiles());

            var projectFilePath = GetSolutionFileName(@"Library\Library.csproj");

            DotNetRestore(@"Library\Library.csproj");

            // Override the TFM properties defined in the file
            using (var workspace = CreateMSBuildWorkspace(throwOnWorkspaceFailed: true, (PropertyNames.TargetFramework, ""), (PropertyNames.TargetFrameworks, "net6;net5")))
            {
                await workspace.OpenProjectAsync(projectFilePath);

                // Assert that two projects have been loaded, one for each TFM.
                Assert.Equal(2, workspace.CurrentSolution.ProjectIds.Count);

                Assert.Contains(workspace.CurrentSolution.Projects, p => p.Name == "Library(net6)");
                Assert.Contains(workspace.CurrentSolution.Projects, p => p.Name == "Library(net5)");
            }
        }

        [ConditionalFact(typeof(DotNetSdkMSBuildInstalled))]
        [Trait(Traits.Feature, Traits.Features.MSBuildWorkspace)]
        [Trait(Traits.Feature, Traits.Features.NetCore)]
        public async Task TestOpenProject_VBNetCoreAppWithGlobalImportAndLibrary()
        {
            CreateFiles(GetVBNetCoreAppWithGlobalImportAndLibraryFiles());

            var vbProjectFilePath = GetSolutionFileName(@"VBProject\VBProject.vbproj");
            var libraryFilePath = GetSolutionFileName(@"Library\Library.csproj");

            DotNetRestore(@"Library\Library.csproj");
            DotNetRestore(@"VBProject\VBProject.vbproj");

            // Warning:Found project reference without a matching metadata reference: Library.csproj
            using var workspace = CreateMSBuildWorkspace(throwOnWorkspaceFailed: false);
            var project = await workspace.OpenProjectAsync(vbProjectFilePath);

            // Assert that there is are two projects loaded (VBProject.vbproj references Library.csproj).
            Assert.Equal(2, workspace.CurrentSolution.ProjectIds.Count);

            // Assert that there is a project reference between VBProject.vbproj and Library.csproj
            AssertSingleProjectReference(project, libraryFilePath);

            // Assert that the project does not have any diagnostics in Program.vb
            var document = project.Documents.First(d => d.Name == "Program.vb");
            var semanticModel = await document.GetSemanticModelAsync();
            var diagnostics = semanticModel.GetDiagnostics();
            Assert.Empty(diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning));

            var compilation = await project.GetCompilationAsync();
            var option = compilation.Options as VisualBasicCompilationOptions;
            Assert.Contains("LibraryHelperClass = Library.MyHelperClass", option.GlobalImports.Select(i => i.Name));

            static void AssertSingleProjectReference(Project project, string projectRefFilePath)
            {
                var projectReference = Assert.Single(project.ProjectReferences);

                var projectRefId = projectReference.ProjectId;
                Assert.Equal(projectRefFilePath, project.Solution.GetProject(projectRefId).FilePath);
            }
        }
    }
}
