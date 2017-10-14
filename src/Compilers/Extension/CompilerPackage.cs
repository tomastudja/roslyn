﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Roslyn.Compilers.Extension
{
    [ProvideAutoLoad(UIContextGuids.SolutionExists)]
    public sealed class CompilerPackage : Package
    {
        protected override void Initialize()
        {
            base.Initialize();

            var packagePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            string localRegistryRoot;
            var reg = (ILocalRegistry2)this.GetService(typeof(SLocalRegistry));
            reg.GetLocalRegistryRoot(out localRegistryRoot);
            var registryParts = localRegistryRoot.Split('\\');

            // Is it a valid Hive looks similar to:  
            //  'Software\Microsoft\VisualStudio\14.0'  'Software\Microsoft\VisualStudio\14.0Roslyn'  'Software\Microsoft\VSWinExpress\14.0'
            if (registryParts.Length >= 4)
            {
                var skuName = registryParts[2];
                var hiveName = registryParts[3];
                var roslynHive = string.Format(@"{0}.{1}", registryParts[2], registryParts[3]);

                WriteMSBuildFiles(packagePath, roslynHive);

                try
                {
                    Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.DisableMarkDirty = true;
                    Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.SetGlobalProperty("RoslynHive", roslynHive);
                }
                finally
                {
                    Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.DisableMarkDirty = false;
                }
            }
        }

        private void WriteMSBuildFiles(string packagePath, string hiveName)
        {
            // A map of the file name to the content we need to ensure exists in the file
            var filesToWrite = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // The props we want to be included as early as possible since we want our tasks to be used and
            // to ensure our setting of targets path happens early enough
            filesToWrite.Add(GetMSBuildRelativePath($@"Imports\Microsoft.Common.props\ImportBefore\Roslyn.Compilers.Extension.{hiveName}.props"),
                $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup Condition=""'$(RoslynHive)' == '{hiveName}'"">
    <CSharpCoreTargetsPath>{packagePath}\Microsoft.CSharp.Core.targets</CSharpCoreTargetsPath>
    <VisualBasicCoreTargetsPath>{packagePath}\Microsoft.VisualBasic.Core.targets</VisualBasicCoreTargetsPath>
  </PropertyGroup> 

  <UsingTask TaskName=""Microsoft.CodeAnalysis.BuildTasks.Csc"" AssemblyFile=""{packagePath}\Microsoft.Build.Tasks.CodeAnalysis.dll"" Condition=""'$(RoslynHive)' == '{hiveName}'"" />
  <UsingTask TaskName=""Microsoft.CodeAnalysis.BuildTasks.Vbc"" AssemblyFile=""{packagePath}\Microsoft.Build.Tasks.CodeAnalysis.dll"" Condition=""'$(RoslynHive)' == '{hiveName}'"" />
</Project>");

            // This targets content we want to be included later since the project file might touch UseSharedCompilation
            var targetsContent =
                    $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <!-- If we're not using the compiler server, set ToolPath/Exe to direct to the exes in this package -->
  <PropertyGroup Condition=""'$(RoslynHive)' == '{hiveName}' and '$(UseSharedCompilation)' == 'false'"">
    <CscToolPath>{packagePath}</CscToolPath>
    <CscToolExe>csc.exe</CscToolExe>
    <VbcToolPath>{packagePath}</VbcToolPath>
    <VbcToolExe>vbc.exe</VbcToolExe>
  </PropertyGroup>
</Project>";

            filesToWrite.Add(GetMSBuildRelativePath($@"Microsoft.CSharp.targets\ImportBefore\Roslyn.Compilers.Extension.{hiveName}.targets"), targetsContent);
            filesToWrite.Add(GetMSBuildRelativePath($@"Microsoft.VisualBasic.targets\ImportBefore\Roslyn.Compilers.Extension.{hiveName}.targets"), targetsContent);

            // First we want to ensure any Roslyn files with our hive name that we aren't writing -- this is probably
            // leftovers from older extensions
            var msbuildDirectory = new DirectoryInfo(GetMSBuildPath());
            if (msbuildDirectory.Exists)
            {
                foreach (var file in msbuildDirectory.EnumerateFiles($"*Roslyn*{hiveName}*", SearchOption.AllDirectories))
                {
                    if (!filesToWrite.ContainsKey(file.FullName))
                    {
                        file.Delete();
                    }
                }
            }

            try
            {
                foreach (var fileAndContents in filesToWrite)
                {
                    var parentDirectory = new DirectoryInfo(Path.GetDirectoryName(fileAndContents.Key));
                    parentDirectory.Create();

                    // If we already know the file has the same contents, then we can skip
                    if (File.Exists(fileAndContents.Key) && File.ReadAllText(fileAndContents.Key) == fileAndContents.Value)
                    {
                        continue;
                    }

                    File.WriteAllText(fileAndContents.Key, fileAndContents.Value);
                }
            }
            catch (Exception e)
            {
                var msg =
$@"{e.Message}

To reload the Roslyn compiler package, close Visual Studio and any MSBuild processes, then restart Visual Studio.";

                VsShellUtilities.ShowMessageBox(
                    this,
                    msg,
                    null,
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }


        private string GetMSBuildVersionString()
        {
            var dte = (DTE)GetService(typeof(SDTE));
            var parts = dte.Version.Split('.');
            if (parts.Length == 0)
            {
                throw GetBadVisualStudioVersionException(dte.Version);
            }

            switch (parts[0])
            {
                case "14":
                    return "14.0";
                case "15":
                    return "15.0";
                default:
                    throw GetBadVisualStudioVersionException(dte.Version);
            }
        }

        private static Exception GetBadVisualStudioVersionException(string version)
        {
            return new Exception($"Unrecoginzed Visual Studio Version: {version}");
        }

        private string GetMSBuildPath()
        {
            var version = GetMSBuildVersionString();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, $@"Microsoft\MSBuild\{version}");
        }

        private string GetMSBuildRelativePath(string relativePath)
        {
            return Path.Combine(GetMSBuildPath(), relativePath);
        }
    }
}
