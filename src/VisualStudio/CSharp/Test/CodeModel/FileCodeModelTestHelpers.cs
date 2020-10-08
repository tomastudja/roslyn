﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Linq;
using System.Runtime.ExceptionServices;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel;
using Microsoft.VisualStudio.LanguageServices.Implementation.Interop;
using Microsoft.VisualStudio.LanguageServices.UnitTests;
using Microsoft.VisualStudio.LanguageServices.UnitTests.CodeModel.Mocks;
using static Microsoft.VisualStudio.LanguageServices.UnitTests.CodeModel.CodeModelTestHelpers;

namespace Microsoft.VisualStudio.LanguageServices.CSharp.UnitTests.CodeModel
{
    internal static class FileCodeModelTestHelpers
    {
        // If something is *really* wrong with our COM marshalling stuff, the creation of the CodeModel will probably
        // throw some sort of AV or other Very Bad exception. We still want to be able to catch them, so we can clean up
        // the workspace. If we don't, we leak the workspace and it'll take down the process when it throws in a
        // finalizer complaining we didn't clean it up. Catching AVs is of course not safe, but this is balancing
        // "probably not crash" as an improvement over "will crash when the finalizer throws."
        [HandleProcessCorruptedStateExceptions]
        public static (TestWorkspace workspace, VisualStudioWorkspace extraWorkspaceToDisposeButNotUse, EnvDTE.FileCodeModel fileCodeModel) CreateWorkspaceAndFileCodeModel(string file)
        {
            var workspace = TestWorkspace.CreateCSharp(file, composition: VisualStudioTestCompositions.LanguageServices);

            try
            {
                var project = workspace.CurrentSolution.Projects.Single();
                var document = project.Documents.Single().Id;

                var componentModel = new MockComponentModel(workspace.ExportProvider);
                var serviceProvider = new MockServiceProvider(componentModel);
                WrapperPolicy.s_ComWrapperFactory = MockComWrapperFactory.Instance;

                var visualStudioWorkspaceMock = new MockVisualStudioWorkspace(workspace);
                var threadingContext = workspace.ExportProvider.GetExportedValue<IThreadingContext>();
                var notificationService = workspace.ExportProvider.GetExportedValue<IForegroundNotificationService>();
                var listenerProvider = workspace.ExportProvider.GetExportedValue<AsynchronousOperationListenerProvider>();

                var state = new CodeModelState(
                    threadingContext,
                    serviceProvider,
                    project.LanguageServices,
                    visualStudioWorkspaceMock,
                    new ProjectCodeModelFactory(
                        visualStudioWorkspaceMock,
                        serviceProvider,
                        threadingContext,
                        notificationService,
                        listenerProvider));

                var codeModel = FileCodeModel.Create(state, null, document, new MockTextManagerAdapter()).Handle;

                return (workspace, visualStudioWorkspaceMock, codeModel);
            }
            catch
            {
                // We threw during creation of the FileCodeModel. Make sure we clean up our workspace or else we leak it
                workspace.Dispose();
                throw;
            }
        }
    }
}
