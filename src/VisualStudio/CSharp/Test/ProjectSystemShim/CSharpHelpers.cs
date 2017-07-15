﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// <auto-generated/>

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.LanguageServices.CSharp.ProjectSystemShim;
using Microsoft.VisualStudio.LanguageServices.CSharp.ProjectSystemShim.Interop;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem.CPS;
using Microsoft.VisualStudio.LanguageServices.UnitTests.ProjectSystemShim.Framework;
using Microsoft.VisualStudio.Shell.Interop;

namespace Roslyn.VisualStudio.CSharp.UnitTests.ProjectSystemShim
{
    internal static class CSharpHelpers
    {
        public static CSharpProjectShimWithServices CreateCSharpProject(TestEnvironment environment, string projectName)
        {
            var projectBinPath = Path.GetTempPath();
            var hierarchy = environment.CreateHierarchy(projectName, projectBinPath, "CSharp");

            return new CSharpProjectShimWithServices(
                new MockCSharpProjectRoot(hierarchy),
                environment.ProjectTracker,
                reportExternalErrorCreatorOpt: null,
                projectSystemName: projectName,
                hierarchy: hierarchy,
                serviceProvider: environment.ServiceProvider,
                visualStudioWorkspaceOpt: null,
                hostDiagnosticUpdateSourceOpt: null,
                commandLineParserServiceOpt: new CSharpCommandLineParserService());
        }

        public static CPSProject CreateCSharpCPSProject(TestEnvironment environment, string projectName, params string[] commandLineArguments)
        {
            return CreateCSharpCPSProject(environment, projectName, projectGuid: Guid.NewGuid(), commandLineArguments: commandLineArguments);
        }

        public static CPSProject CreateCSharpCPSProject(TestEnvironment environment, string projectName, Guid projectGuid, params string[] commandLineArguments)
        {
            var projectFilePath = Path.GetTempPath();
            var binOutputPath = GetOutputPathFromArguments(commandLineArguments) ?? Path.Combine(projectFilePath, projectName + ".dll");

            return CreateCSharpCPSProject(environment, projectName, projectFilePath, binOutputPath, projectGuid, commandLineArguments);
        }

        public static CPSProject CreateCSharpCPSProject(TestEnvironment environment, string projectName, string binOutputPath, params string[] commandLineArguments)
        {
            var projectFilePath = Path.GetTempPath();
            return CreateCSharpCPSProject(environment, projectName, projectFilePath, binOutputPath, projectGuid: Guid.NewGuid(), commandLineArguments: commandLineArguments);
        }

        public static CPSProject CreateCSharpCPSProject(TestEnvironment environment, string projectName, string projectFilePath, string binOutputPath, Guid projectGuid, params string[] commandLineArguments)
        {
            var hierarchy = environment.CreateHierarchy(projectName, projectFilePath, "CSharp");
            
            var cpsProject = CPSProjectFactory.CreateCPSProject(
                environment.ProjectTracker,
                environment.ServiceProvider,
                hierarchy,
                projectName,
                projectFilePath,
                projectGuid,
                LanguageNames.CSharp,
                new TestCSharpCommandLineParserService(),
                binOutputPath);

            var commandLineForOptions = string.Join(" ", commandLineArguments);
            cpsProject.SetOptions(commandLineForOptions);

            return cpsProject;
        }

        private static string GetOutputPathFromArguments(string[] commandLineArguments)
        {
            const string outPrefix = "/out:";
            string outputPath = null;
            foreach (var arg in commandLineArguments)
            {
                var index = arg.IndexOf(outPrefix);
                if (index >= 0)
                {
                    outputPath = arg.Substring(index + outPrefix.Length);
                }
            }

            return outputPath;
        }

        private sealed class TestCSharpCommandLineParserService : ICommandLineParserService
        {
            public CommandLineArguments Parse(IEnumerable<string> arguments, string baseDirectory, bool isInteractive, string sdkDirectory)
            {
                if (baseDirectory == null || !Directory.Exists(baseDirectory))
                {
                    baseDirectory = Path.GetTempPath();
                }

                return CSharpCommandLineParser.Default.Parse(arguments, baseDirectory, sdkDirectory);
            }
        }

        private class MockCSharpProjectRoot : ICSharpProjectRoot
        {
            private IVsHierarchy _hierarchy;

            public MockCSharpProjectRoot(IVsHierarchy hierarchy)
            {
                _hierarchy = hierarchy;
            }

            int ICSharpProjectRoot.BelongsToProject(string pszFileName)
            {
                throw new NotImplementedException();
            }

            string ICSharpProjectRoot.BuildPerConfigCacheFileName()
            {
                throw new NotImplementedException();
            }

            bool ICSharpProjectRoot.CanCreateFileCodeModel(string pszFile)
            {
                throw new NotImplementedException();
            }

            void ICSharpProjectRoot.ConfigureCompiler(ICSCompiler compiler, ICSInputSet inputSet, bool addSources)
            {
                throw new NotImplementedException();
            }

            object ICSharpProjectRoot.CreateFileCodeModel(string pszFile, ref Guid riid)
            {
                throw new NotImplementedException();
            }

            string ICSharpProjectRoot.GetActiveConfigurationName()
            {
                throw new NotImplementedException();
            }

            string ICSharpProjectRoot.GetFullProjectName()
            {
                throw new NotImplementedException();
            }

            int ICSharpProjectRoot.GetHierarchyAndItemID(string pszFile, out IVsHierarchy ppHier, out uint pItemID)
            {
                ppHier = _hierarchy;

                // Each item should have it's own ItemID, but for simplicity we'll just hard-code a value of
                // no particular significance.
                pItemID = 42;

                return VSConstants.S_OK;
            }

            void ICSharpProjectRoot.GetHierarchyAndItemIDOptionallyInProject(string pszFile, out IVsHierarchy ppHier, out uint pItemID, bool mustBeInProject)
            {
                throw new NotImplementedException();
            }

            string ICSharpProjectRoot.GetProjectLocation()
            {
                throw new NotImplementedException();
            }

            object ICSharpProjectRoot.GetProjectSite(ref Guid riid)
            {
                throw new NotImplementedException();
            }

            void ICSharpProjectRoot.SetProjectSite(ICSharpProjectSite site)
            {
                throw new NotImplementedException();
            }
        }
    }
}
