﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.DesignerAttribute;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Remote.Testing;
using Microsoft.CodeAnalysis.Serialization;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.UnitTests;
using Microsoft.VisualStudio.Threading;
using Roslyn.Test.Utilities;
using Roslyn.Test.Utilities.TestGenerators;
using Roslyn.Utilities;
using Xunit;

namespace Roslyn.VisualStudio.Next.UnitTests.Remote
{
    [UseExportProvider]
    [Trait(Traits.Feature, Traits.Features.RemoteHost)]
    public class ServiceHubServicesTests
    {
        private static TestWorkspace CreateWorkspace(Type[] additionalParts = null)
             => new(composition: FeaturesTestCompositions.Features.WithTestHostParts(TestHost.OutOfProcess).AddParts(additionalParts));

        [Fact]
        public async Task TestRemoteHostSynchronize()
        {
            var code = @"class Test { void Method() { } }";

            using var workspace = CreateWorkspace();
            workspace.InitializeDocuments(LanguageNames.CSharp, files: [code], openDocuments: false);

            using var client = await InProcRemoteHostClient.GetTestClientAsync(workspace).ConfigureAwait(false);

            var solution = workspace.CurrentSolution;

            await UpdatePrimaryWorkspace(client, solution);
            await VerifyAssetStorageAsync(client, solution);

            var remoteWorkpace = client.GetRemoteWorkspace();

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkpace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));
        }

        [Fact]
        public async Task TestRemoteHostTextSynchronize()
        {
            var code = @"class Test { void Method() { } }";

            using var workspace = CreateWorkspace();
            workspace.InitializeDocuments(LanguageNames.CSharp, files: [code], openDocuments: false);

            var client = await InProcRemoteHostClient.GetTestClientAsync(workspace).ConfigureAwait(false);

            var solution = workspace.CurrentSolution;

            // sync base solution
            await UpdatePrimaryWorkspace(client, solution);
            await VerifyAssetStorageAsync(client, solution);

            // get basic info
            var oldDocument = solution.Projects.First().Documents.First();
            var oldState = await oldDocument.State.GetStateChecksumsAsync(CancellationToken.None);
            var oldText = await oldDocument.GetTextAsync();

            // update text
            var newText = oldText.WithChanges(new TextChange(TextSpan.FromBounds(0, 0), "/* test */"));

            // sync
            await client.TryInvokeAsync<IRemoteAssetSynchronizationService>(
                (service, cancellationToken) => service.SynchronizeTextAsync(oldDocument.Id, oldState.Text, newText.GetTextChanges(oldText), cancellationToken),
                CancellationToken.None);

            // apply change to solution
            var newDocument = oldDocument.WithText(newText);
            var newState = await newDocument.State.GetStateChecksumsAsync(CancellationToken.None);

            // check that text already exist in remote side
            Assert.True(client.TestData.WorkspaceManager.SolutionAssetCache.TryGetAsset<SerializableSourceText>(newState.Text, out var serializableRemoteText));
            Assert.Equal(newText.ToString(), (await serializableRemoteText.GetTextAsync(CancellationToken.None)).ToString());
        }

        private static async Task<AssetProvider> GetAssetProviderAsync(Workspace workspace, Workspace remoteWorkspace, Solution solution, Dictionary<Checksum, object> map = null)
        {
            // make sure checksum is calculated
            await solution.State.GetChecksumAsync(CancellationToken.None);

            map ??= new Dictionary<Checksum, object>();
            await solution.AppendAssetMapAsync(map, CancellationToken.None);

            var sessionId = Checksum.Create(ImmutableArray.CreateRange(Guid.NewGuid().ToByteArray()));
            var storage = new SolutionAssetCache();
            var assetSource = new SimpleAssetSource(workspace.Services.GetService<ISerializerService>(), map);

            return new AssetProvider(sessionId, storage, assetSource, remoteWorkspace.Services.GetService<ISerializerService>());
        }

        [Fact]
        public async Task TestDesignerAttributes()
        {
            var source = @"[System.ComponentModel.DesignerCategory(""Form"")] class Test { }";

            using var workspace = CreateWorkspace();
            workspace.InitializeDocuments(LanguageNames.CSharp, files: [source], openDocuments: false);

            using var client = await InProcRemoteHostClient.GetTestClientAsync(workspace).ConfigureAwait(false);
            var remoteWorkspace = client.GetRemoteWorkspace();

            // Start solution crawler in the remote workspace:
            await client.TryInvokeAsync<IRemoteDiagnosticAnalyzerService>(
                (service, cancellationToken) => service.StartSolutionCrawlerAsync(cancellationToken),
                CancellationToken.None).ConfigureAwait(false);

            var cancellationTokenSource = new CancellationTokenSource();
            var solution = workspace.CurrentSolution;

            // Ensure remote workspace is in sync with normal workspace.
            var assetProvider = await GetAssetProviderAsync(workspace, remoteWorkspace, solution);
            var solutionChecksum = await solution.State.GetChecksumAsync(CancellationToken.None);
            await remoteWorkspace.UpdatePrimaryBranchSolutionAsync(assetProvider, solutionChecksum, solution.WorkspaceVersion, CancellationToken.None);

            var callback = new DesignerAttributeComputerCallback();

            using var connection = client.CreateConnection<IRemoteDesignerAttributeDiscoveryService>(callback);

            var invokeTask = connection.TryInvokeAsync(
                solution,
                (service, checksum, callbackId, cancellationToken) => service.DiscoverDesignerAttributesAsync(
                    callbackId, checksum, priorityDocument: null, useFrozenSnapshots: true, cancellationToken),
                cancellationTokenSource.Token);

            var infos = await callback.Infos;
            Assert.Equal(1, infos.Length);

            var info = infos[0];
            Assert.Equal("Form", info.Category);
            Assert.Equal(solution.Projects.Single().Documents.Single().Id, info.DocumentId);

            cancellationTokenSource.Cancel();

            Assert.True(await invokeTask);
        }

        private class DesignerAttributeComputerCallback : IDesignerAttributeDiscoveryService.ICallback
        {
            private readonly TaskCompletionSource<ImmutableArray<DesignerAttributeData>> _infosSource = new();

            public Task<ImmutableArray<DesignerAttributeData>> Infos => _infosSource.Task;

            public ValueTask ReportDesignerAttributeDataAsync(ImmutableArray<DesignerAttributeData> infos, CancellationToken cancellationToken)
            {
                _infosSource.SetResult(infos);
                return ValueTaskFactory.CompletedTask;
            }
        }

        [Fact]
        public async Task TestUnknownProject()
        {
            var workspace = CreateWorkspace([typeof(NoCompilationLanguageService)]);
            var solution = workspace.CurrentSolution.AddProject("unknown", "unknown", NoCompilationConstants.LanguageName).Solution;

            using var client = await InProcRemoteHostClient.GetTestClientAsync(workspace).ConfigureAwait(false);
            var remoteWorkspace = client.GetRemoteWorkspace();

            await UpdatePrimaryWorkspace(client, solution);
            await VerifyAssetStorageAsync(client, solution);

            // Only C# and VB projects are supported in Remote workspace.
            // See "RemoteSupportedLanguages.IsSupported"
            Assert.Empty(remoteWorkspace.CurrentSolution.Projects);

            // No serializable remote options affect options checksum, so the checksums should match.
            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));

            solution = solution.RemoveProject(solution.ProjectIds.Single());

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1365014")]
        public async Task TestRemoteHostSynchronizeIncrementalUpdate(bool applyInBatch)
        {
            using var workspace = CreateWorkspace();

            using var client = await InProcRemoteHostClient.GetTestClientAsync(workspace).ConfigureAwait(false);
            var remoteWorkspace = client.GetRemoteWorkspace();

            var solution = Populate(workspace.CurrentSolution);

            // verify initial setup
            await workspace.ChangeSolutionAsync(solution);
            solution = workspace.CurrentSolution;
            await UpdatePrimaryWorkspace(client, solution);
            await VerifyAssetStorageAsync(client, solution);

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));

            // incrementally update
            solution = await VerifyIncrementalUpdatesAsync(
                workspace, remoteWorkspace, client, solution, applyInBatch, csAddition: " ", vbAddition: " ");

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));

            // incrementally update
            solution = await VerifyIncrementalUpdatesAsync(
                workspace, remoteWorkspace, client, solution, applyInBatch, csAddition: "\r\nclass Addition { }", vbAddition: "\r\nClass VB\r\nEnd Class");

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));
        }

        [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/52578")]
        public async Task TestIncrementalUpdateHandlesReferenceReversal()
        {
            using var workspace = CreateWorkspace();

            using var client = await InProcRemoteHostClient.GetTestClientAsync(workspace).ConfigureAwait(false);
            var remoteWorkspace = client.GetRemoteWorkspace();

            var solution = workspace.CurrentSolution;
            solution = AddProject(solution, LanguageNames.CSharp, documents: Array.Empty<string>(), additionalDocuments: Array.Empty<string>(), p2pReferences: Array.Empty<ProjectId>());
            var projectId1 = solution.ProjectIds.Single();
            solution = AddProject(solution, LanguageNames.CSharp, documents: Array.Empty<string>(), additionalDocuments: Array.Empty<string>(), p2pReferences: Array.Empty<ProjectId>());
            var projectId2 = solution.ProjectIds.Where(id => id != projectId1).Single();

            var project1ToProject2 = new ProjectReference(projectId2);
            var project2ToProject1 = new ProjectReference(projectId1);

            // Start with projectId1 -> projectId2
            solution = solution.AddProjectReference(projectId1, project1ToProject2);

            // verify initial setup
            await UpdatePrimaryWorkspace(client, solution);
            await VerifyAssetStorageAsync(client, solution);

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));

            // reverse project references and incrementally update
            solution = solution.RemoveProjectReference(projectId1, project1ToProject2);
            solution = solution.AddProjectReference(projectId2, project2ToProject1);
            await workspace.ChangeSolutionAsync(solution);
            solution = workspace.CurrentSolution;
            await UpdatePrimaryWorkspace(client, solution);

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));

            // reverse project references again and incrementally update
            solution = solution.RemoveProjectReference(projectId2, project2ToProject1);
            solution = solution.AddProjectReference(projectId1, project1ToProject2);
            await workspace.ChangeSolutionAsync(solution);
            solution = workspace.CurrentSolution;
            await UpdatePrimaryWorkspace(client, solution);

            Assert.Equal(
                await solution.State.GetChecksumAsync(CancellationToken.None),
                await remoteWorkspace.CurrentSolution.State.GetChecksumAsync(CancellationToken.None));
        }

        [Fact]
        public async Task TestRemoteWorkspaceCircularReferences()
        {
            using var tempRoot = new TempRoot();

            var file = tempRoot.CreateDirectory().CreateFile("p1.dll");
            file.CopyContentFrom(typeof(object).Assembly.Location);

            var p1 = ProjectId.CreateNewId();
            var p2 = ProjectId.CreateNewId();

            var solutionInfo = SolutionInfo.Create(
                SolutionId.CreateNewId(), VersionStamp.Create(), "",
                new[]
                {
                        ProjectInfo.Create(
                            p1, VersionStamp.Create(), "p1", "p1", LanguageNames.CSharp, outputFilePath: file.Path,
                            projectReferences: new [] { new ProjectReference(p2) }),
                        ProjectInfo.Create(
                            p2, VersionStamp.Create(), "p2", "p2", LanguageNames.CSharp,
                            metadataReferences: new [] { MetadataReference.CreateFromFile(file.Path) })
                });

            using var remoteWorkspace = new RemoteWorkspace(FeaturesTestCompositions.RemoteHost.GetHostServices());

            // this shouldn't throw exception
            var (solution, updated) = await remoteWorkspace.GetTestAccessor().TryUpdateWorkspaceCurrentSolutionAsync(
                remoteWorkspace.GetTestAccessor().CreateSolutionFromInfo(solutionInfo), workspaceVersion: 1);
            Assert.True(updated);
            Assert.NotNull(solution);
        }

        [Fact]
        public async Task InProcAndRemoteWorkspaceAgreeOnContents()
        {
            using var localWorkspace = CreateWorkspace();

            // We'll use either a generator that produces a single tree, or no tree, to ensure we efficiently handle both cases
            var generator = new CallbackGenerator(onInit: _ => { }, onExecute: _ => { }, computeSource: () => (hintName: Guid.NewGuid().ToString(), source: Guid.NewGuid().ToString()));

            var analyzerReference = new TestGeneratorReference(generator);
            var project = AddEmptyProject(localWorkspace.CurrentSolution)
                .AddAnalyzerReference(analyzerReference);

            Assert.True(localWorkspace.SetCurrentSolution(_ => project.Solution, WorkspaceChangeKind.SolutionChanged));

            using var client = await InProcRemoteHostClient.GetTestClientAsync(localWorkspace);
            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);
            var remoteWorkspace = client.GetRemoteWorkspace();

            var localProject = localWorkspace.CurrentSolution.Projects.Single();
            var remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally
            var localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely
            var remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 1);
            var localGeneratedDocs1 = (await localProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();

            // Now make a trivial change to the workspace, resync and confirm things are the same.
            Assert.True(localWorkspace.SetCurrentSolution(_ => project.AddDocument("X.cs", SourceText.From("// X")).Project.Solution, WorkspaceChangeKind.SolutionChanged));

            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);

            localProject = localWorkspace.CurrentSolution.Projects.Single();
            remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally
            localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely
            remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 1);
            var localGeneratedDocs2 = (await localProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();

            // We should have generated different doc contents the second time around.
            Assert.NotEqual((await localGeneratedDocs1.Single().GetTextAsync()).ToString(), (await localGeneratedDocs2.Single().GetTextAsync()).ToString());
        }

        private static async Task AssertSourceGeneratedDocumentsAreSame(Project localProject, Project remoteProject, int expectedCount)
        {
            var localGeneratedDocs = (await localProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();
            var remoteGeneratedDocs = (await remoteProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();

            Assert.Equal(localGeneratedDocs.Length, remoteGeneratedDocs.Length);
            Assert.Equal(expectedCount, localGeneratedDocs.Length);

            for (var i = 0; i < expectedCount; i++)
            {
                var localDoc = localGeneratedDocs[i];
                var remoteDoc = remoteGeneratedDocs[i];

                Assert.Equal(localDoc.HintName, remoteDoc.HintName);
                Assert.Equal(localDoc.DocumentState.Id, remoteDoc.DocumentState.Id);

                var localText = await localDoc.GetTextAsync();
                var remoteText = await localDoc.GetTextAsync();
                Assert.Equal(localText.ToString(), remoteText.ToString());
                Assert.Equal(localText.Encoding, remoteText.Encoding);
                Assert.Equal(localText.ChecksumAlgorithm, remoteText.ChecksumAlgorithm);
            }
        }

        [Fact]
        public async Task InProcAndRemoteWorkspaceAgreeOnSourceTextHashAlgorithm()
        {
            using var localWorkspace = CreateWorkspace();

            var hashAlgorithm = SourceHashAlgorithm.Sha256;
            var generator = new CallbackGenerator(
                onInit: _ => { }, onExecute: _ => { },
                computeSourceText: () => (hintName: Guid.NewGuid().ToString(), SourceText.From(Guid.NewGuid().ToString(), Encoding.ASCII, hashAlgorithm)));

            var analyzerReference = new TestGeneratorReference(generator);
            var project = AddEmptyProject(localWorkspace.CurrentSolution)
                .AddAnalyzerReference(analyzerReference);

            Assert.True(localWorkspace.SetCurrentSolution(_ => project.Solution, WorkspaceChangeKind.SolutionChanged));

            using var client = await InProcRemoteHostClient.GetTestClientAsync(localWorkspace);
            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);
            var remoteWorkspace = client.GetRemoteWorkspace();

            var localProject = localWorkspace.CurrentSolution.Projects.Single();
            var remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally.  This should force them to run remotely as well. Which means we will generate on
            // the remote side and sync to the local side.
            var localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely.  This should be a no-op and will be unaffected by this flag.
            hashAlgorithm = SourceHashAlgorithm.Sha1;
            var remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 1);
            var localGeneratedDocs1 = (await localProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();

            // Now make a trivial change to the workspace, resync and confirm things are the same.
            Assert.True(localWorkspace.SetCurrentSolution(_ => project.AddDocument("X.cs", SourceText.From("// X")).Project.Solution, WorkspaceChangeKind.SolutionChanged));

            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);

            localProject = localWorkspace.CurrentSolution.Projects.Single();
            remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally
            localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely
            remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 1);
            var localGeneratedDocs2 = (await localProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();

            // We should have generated different doc contents the second time around.
            Assert.NotEqual((await localGeneratedDocs1.Single().GetTextAsync()).ChecksumAlgorithm, (await localGeneratedDocs2.Single().GetTextAsync()).ChecksumAlgorithm);
        }

        [Fact]
        public async Task InProcAndRemoteWorkspaceAgreeOnSourceTextEncoding()
        {
            using var localWorkspace = CreateWorkspace();

            var encoding = Encoding.ASCII;
            var generator = new CallbackGenerator(
                onInit: _ => { }, onExecute: _ => { },
                computeSourceText: () => (hintName: Guid.NewGuid().ToString(), SourceText.From(Guid.NewGuid().ToString(), encoding)));

            var analyzerReference = new TestGeneratorReference(generator);
            var project = AddEmptyProject(localWorkspace.CurrentSolution)
                .AddAnalyzerReference(analyzerReference);

            Assert.True(localWorkspace.SetCurrentSolution(_ => project.Solution, WorkspaceChangeKind.SolutionChanged));

            using var client = await InProcRemoteHostClient.GetTestClientAsync(localWorkspace);
            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);
            var remoteWorkspace = client.GetRemoteWorkspace();

            var localProject = localWorkspace.CurrentSolution.Projects.Single();
            var remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally.  This should force them to run remotely as well. Which means we will generate on
            // the remote side and sync to the local side.
            var localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely.  This should be a no-op and will be unaffected by this flag.
            encoding = Encoding.UTF8;
            var remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 1);
            var localGeneratedDocs1 = (await localProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();

            // Now make a trivial change to the workspace, resync and confirm things are the same.
            Assert.True(localWorkspace.SetCurrentSolution(_ => project.AddDocument("X.cs", SourceText.From("// X")).Project.Solution, WorkspaceChangeKind.SolutionChanged));

            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);

            localProject = localWorkspace.CurrentSolution.Projects.Single();
            remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally
            localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely
            remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 1);
            var localGeneratedDocs2 = (await localProject.GetSourceGeneratedDocumentsAsync()).ToImmutableArray();

            // We should have generated different doc contents the second time around.
            Assert.NotEqual((await localGeneratedDocs1.Single().GetTextAsync()).Encoding, (await localGeneratedDocs2.Single().GetTextAsync()).Encoding);
        }

        [Fact]
        public async Task InProcAndRemoteWorkspaceAgreeOnFilesGenerated()
        {
            using var localWorkspace = CreateWorkspace();

            // We'll use either a generator that produces a single tree, or no tree, to ensure we efficiently handle both cases
            var generateSource = true;
            var generator = new CallbackGenerator(onInit: _ => { }, onExecute: _ => { },
                computeSource: () => generateSource ? (hintName: Guid.NewGuid().ToString(), source: Guid.NewGuid().ToString()) : default);

            var analyzerReference = new TestGeneratorReference(generator);
            var project = AddEmptyProject(localWorkspace.CurrentSolution)
                .AddAnalyzerReference(analyzerReference);

            Assert.True(localWorkspace.SetCurrentSolution(_ => project.Solution, WorkspaceChangeKind.SolutionChanged));

            var client = await InProcRemoteHostClient.GetTestClientAsync(localWorkspace);
            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);
            var remoteWorkspace = client.GetRemoteWorkspace();

            var localProject = localWorkspace.CurrentSolution.Projects.Single();
            var remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally.  This should force them to run remotely as well. Which means we will generate on
            // the remote side and sync to the local side.
            var localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely.  This should be a no-op and will be unaffected by this flag.
            generateSource = false;
            var remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 1);
        }

        [Fact]
        public async Task InProcAndRemoteWorkspaceAgreeOnNoFilesGenerated()
        {
            using var localWorkspace = CreateWorkspace();

            // We'll use either a generator that produces a single tree, or no tree, to ensure we efficiently handle both cases
            var generateSource = false;
            var generator = new CallbackGenerator(onInit: _ => { }, onExecute: _ => { },
                computeSource: () => generateSource ? (hintName: Guid.NewGuid().ToString(), source: Guid.NewGuid().ToString()) : default);

            var analyzerReference = new TestGeneratorReference(generator);
            var project = AddEmptyProject(localWorkspace.CurrentSolution)
                .AddAnalyzerReference(analyzerReference);

            Assert.True(localWorkspace.SetCurrentSolution(_ => project.Solution, WorkspaceChangeKind.SolutionChanged));

            var client = await InProcRemoteHostClient.GetTestClientAsync(localWorkspace);
            await UpdatePrimaryWorkspace(client, localWorkspace.CurrentSolution);
            var remoteWorkspace = client.GetRemoteWorkspace();

            var localProject = localWorkspace.CurrentSolution.Projects.Single();
            var remoteProject = remoteWorkspace.CurrentSolution.Projects.Single();

            // Run generators locally.  This should force them to run remotely as well. Which means we will generate
            // nothing the remote side and sync that the local side.
            var localCompilation = await localProject.GetCompilationAsync();

            // Now run them remotely.  This should be a no-op and will be unaffected by this flag.
            generateSource = true;
            var remoteCompilation = await remoteProject.GetCompilationAsync();

            await AssertSourceGeneratedDocumentsAreSame(localProject, remoteProject, expectedCount: 0);
        }

        public static Project AddEmptyProject(Solution solution, string languageName = LanguageNames.CSharp, string name = "TestProject")
        {
            var id = ProjectId.CreateNewId();
            return solution.AddProject(
                ProjectInfo.Create(
                    id,
                    VersionStamp.Default,
                    name: name,
                    assemblyName: name,
                    language: languageName)).GetRequiredProject(id);
        }

        private static async Task<Solution> VerifyIncrementalUpdatesAsync(
            TestWorkspace localWorkspace,
            Workspace remoteWorkspace,
            RemoteHostClient client,
            Solution solution,
            bool applyInBatch,
            string csAddition,
            string vbAddition)
        {
            var remoteSolution = remoteWorkspace.CurrentSolution;
            var projectIds = solution.ProjectIds;

            for (var i = 0; i < projectIds.Count; i++)
            {
                var projectName = $"Project{i}";
                var project = solution.GetProject(projectIds[i]);
                var changedDocuments = new List<string>();

                var documentIds = project.DocumentIds;
                for (var j = 0; j < documentIds.Count; j++)
                {
                    var documentName = $"Document{j}";

                    var currentSolution = UpdateSolution(solution, projectName, documentName, csAddition, vbAddition);
                    changedDocuments.Add(documentName);

                    solution = currentSolution;

                    if (!applyInBatch)
                    {
                        await UpdateAndVerifyAsync();
                    }
                }

                if (applyInBatch)
                {
                    await UpdateAndVerifyAsync();
                }

                async Task UpdateAndVerifyAsync()
                {
                    var documentNames = changedDocuments.ToImmutableArray();
                    changedDocuments.Clear();

                    await localWorkspace.ChangeSolutionAsync(solution);
                    solution = localWorkspace.CurrentSolution;
                    await UpdatePrimaryWorkspace(client, solution);

                    var currentRemoteSolution = remoteWorkspace.CurrentSolution;
                    VerifyStates(remoteSolution, currentRemoteSolution, projectName, documentNames);

                    remoteSolution = currentRemoteSolution;

                    Assert.Equal(
                        await solution.State.GetChecksumAsync(CancellationToken.None),
                        await remoteSolution.State.GetChecksumAsync(CancellationToken.None));
                }
            }

            return solution;
        }

        private static void VerifyStates(Solution solution1, Solution solution2, string projectName, ImmutableArray<string> documentNames)
        {
            Assert.Equal(WorkspaceKind.RemoteWorkspace, solution1.WorkspaceKind);
            Assert.Equal(WorkspaceKind.RemoteWorkspace, solution2.WorkspaceKind);

            SetEqual(solution1.ProjectIds, solution2.ProjectIds);

            var (project, documents) = GetProjectAndDocuments(solution1, projectName, documentNames);

            var projectId = project.Id;
            var documentIds = documents.SelectAsArray(document => document.Id);

            var projectIds = solution1.ProjectIds;
            for (var i = 0; i < projectIds.Count; i++)
            {
                var currentProjectId = projectIds[i];

                var projectStateShouldSame = projectId != currentProjectId;
                Assert.Equal(projectStateShouldSame, object.ReferenceEquals(solution1.GetProject(currentProjectId).State, solution2.GetProject(currentProjectId).State));

                if (!projectStateShouldSame)
                {
                    SetEqual(solution1.GetProject(currentProjectId).DocumentIds, solution2.GetProject(currentProjectId).DocumentIds);

                    var documentIdsInProject = solution1.GetProject(currentProjectId).DocumentIds;
                    for (var j = 0; j < documentIdsInProject.Count; j++)
                    {
                        var currentDocumentId = documentIdsInProject[j];

                        var documentStateShouldSame = !documentIds.Contains(currentDocumentId);
                        Assert.Equal(documentStateShouldSame, object.ReferenceEquals(solution1.GetDocument(currentDocumentId).State, solution2.GetDocument(currentDocumentId).State));
                    }
                }
            }
        }

        private static async Task VerifyAssetStorageAsync(InProcRemoteHostClient client, Solution solution)
        {
            var map = await solution.GetAssetMapAsync(CancellationToken.None);

            var storage = client.TestData.WorkspaceManager.SolutionAssetCache;

            TestUtils.VerifyAssetStorage(map, storage);
        }

        private static Solution UpdateSolution(Solution solution, string projectName, string documentName, string csAddition, string vbAddition)
        {
            var (_, document) = GetProjectAndDocument(solution, projectName, documentName);

            return document.WithText(GetNewText(document, csAddition, vbAddition)).Project.Solution;
        }

        private static SourceText GetNewText(Document document, string csAddition, string vbAddition)
        {
            if (document.Project.Language == LanguageNames.CSharp)
            {
                return SourceText.From(document.State.GetTextSynchronously(CancellationToken.None).ToString() + csAddition);
            }

            return SourceText.From(document.State.GetTextSynchronously(CancellationToken.None).ToString() + vbAddition);
        }

        private static (Project project, Document document) GetProjectAndDocument(Solution solution, string projectName, string documentName)
        {
            var project = solution.Projects.First(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
            var document = project.Documents.First(d => string.Equals(d.Name, documentName, StringComparison.OrdinalIgnoreCase));

            return (project, document);
        }

        private static (Project project, ImmutableArray<Document> documents) GetProjectAndDocuments(Solution solution, string projectName, ImmutableArray<string> documentNames)
        {
            var project = solution.Projects.First(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
            var documents = documentNames.SelectAsArray(
                documentName => project.Documents.First(d => string.Equals(d.Name, documentName, StringComparison.OrdinalIgnoreCase)));

            return (project, documents);
        }

        private static async Task UpdatePrimaryWorkspace(RemoteHostClient client, Solution solution)
        {
            var workspaceVersion = solution.WorkspaceVersion;
            await client.TryInvokeAsync<IRemoteAssetSynchronizationService>(
                solution,
                async (service, solutionInfo, cancellationToken) => await service.SynchronizePrimaryWorkspaceAsync(solutionInfo, workspaceVersion, cancellationToken),
                CancellationToken.None);
        }

        private static Solution Populate(Solution solution)
        {
            solution = AddProject(solution, LanguageNames.CSharp,
            [
                "class CS { }",
                "class CS2 { }"
            ],
            [
                "cs additional file content"
            ], Array.Empty<ProjectId>());

            solution = AddProject(solution, LanguageNames.VisualBasic,
            [
                "Class VB\r\nEnd Class",
                "Class VB2\r\nEnd Class"
            ],
            [
                "vb additional file content"
            ], [solution.ProjectIds.First()]);

            solution = AddProject(solution, LanguageNames.CSharp,
            [
                "class Top { }"
            ],
            [
                "cs additional file content"
            ], solution.ProjectIds.ToArray());

            solution = AddProject(solution, LanguageNames.CSharp,
            [
                "class OrphanCS { }",
                "class OrphanCS2 { }"
            ],
            [
                "cs additional file content",
                "cs additional file content2"
            ], Array.Empty<ProjectId>());

            solution = AddProject(solution, LanguageNames.CSharp,
            [
                "class CS { }",
                "class CS2 { }",
                "class CS3 { }",
                "class CS4 { }",
                "class CS5 { }",
            ],
            [
                "cs additional file content"
            ], Array.Empty<ProjectId>());

            solution = AddProject(solution, LanguageNames.VisualBasic,
            [
                "Class VB\r\nEnd Class",
                "Class VB2\r\nEnd Class",
                "Class VB3\r\nEnd Class",
                "Class VB4\r\nEnd Class",
                "Class VB5\r\nEnd Class",
            ],
            [
                "vb additional file content"
            ], Array.Empty<ProjectId>());

            return solution;
        }

        private static Solution AddProject(Solution solution, string language, string[] documents, string[] additionalDocuments, ProjectId[] p2pReferences)
        {
            var projectName = $"Project{solution.ProjectIds.Count}";
            var project = solution.AddProject(projectName, $"{projectName}.dll", language)
                                  .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                                  .AddAnalyzerReference(new AnalyzerFileReference(typeof(object).Assembly.Location, new TestAnalyzerAssemblyLoader()));

            var projectId = project.Id;
            solution = project.Solution;

            for (var i = 0; i < documents.Length; i++)
            {
                var current = solution.GetProject(projectId);
                solution = current.AddDocument($"Document{i}", SourceText.From(documents[i])).Project.Solution;
            }

            for (var i = 0; i < additionalDocuments.Length; i++)
            {
                var current = solution.GetProject(projectId);
                solution = current.AddAdditionalDocument($"AdditionalDocument{i}", SourceText.From(additionalDocuments[i])).Project.Solution;
            }

            for (var i = 0; i < p2pReferences.Length; i++)
            {
                var current = solution.GetProject(projectId);
                solution = current.AddProjectReference(new ProjectReference(p2pReferences[i])).Solution;
            }

            return solution;
        }

        private static void SetEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var expectedSet = new HashSet<T>(expected);
            var result = expected.Count() == actual.Count() && expectedSet.SetEquals(actual);
            if (!result)
            {
                Assert.True(result);
            }
        }
    }
}
