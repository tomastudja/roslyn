﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis.Editor.UnitTests;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.LanguageServices.LiveShare;
using Microsoft.VisualStudio.LiveShare.LanguageServices;
using Roslyn.Test.Utilities;
using Xunit;
using RoslynHandler = Microsoft.CodeAnalysis.LanguageServer.Handler.IRequestHandler;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.LiveShare
{
    [UseExportProvider]
    public class LiveShareRequestHandlersTests
    {
        // For now we're just testing that the right liveshare handlers are exported.
        // This ensures that for shims the right handler is found from roslyn handlers.
        // Functionality will be tested in the code analysis language server layer.
        [Fact]
        public void TestLiveShareRequestHandlersExported()
        {
            var solution = CreateTestSolution();

            var workspace = (TestWorkspace)solution.Workspace;
            var handlers = workspace.ExportProvider.GetExportedValues<ILspRequestHandler>(LiveShareConstants.RoslynContractName).ToArray();


            Assert.Equal(21, handlers.Length);
        }

        private static Solution CreateTestSolution()
        {
            // Get all the liveshare request handlers in this assembly.
            var liveShareRequestHelperTypes = DesktopTestHelpers.GetAllTypesImplementingGivenInterface(
                    typeof(InitializeHandler).Assembly, typeof(ILspRequestHandler));
            // Get all of the roslyn request helpers in M.CA.LanguageServer
            var roslynRequestHelperTypes = DesktopTestHelpers.GetAllTypesImplementingGivenInterface(
                    typeof(RoslynHandler).Assembly, typeof(RoslynHandler));
            var exportProviderFactory = ExportProviderCache.GetOrCreateExportProviderFactory(
                TestExportProvider.EntireAssemblyCatalogWithCSharpAndVisualBasic.WithParts(liveShareRequestHelperTypes).WithParts(roslynRequestHelperTypes));

            var exportProvider = exportProviderFactory.CreateExportProvider();

            using var workspace = TestWorkspace.CreateCSharp(string.Empty, exportProvider: exportProvider);
            return workspace.CurrentSolution;
        }
    }
}
