﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Options;

namespace RunTests
{
    internal enum Display
    {
        None,
        All,
        Succeeded,
        Failed,
    }

    internal class Options
    {
        /// <summary>
        /// Use HTML output files.
        /// </summary>
        public bool IncludeHtml { get; set; }

        /// <summary>
        /// Display the results files.
        /// </summary>
        public Display Display { get; set; }

        /// <summary>
        /// Trait string to pass to xunit.
        /// </summary>
        public string? Trait { get; set; }

        /// <summary>
        /// The no-trait string to pass to xunit.
        /// </summary>
        public string? NoTrait { get; set; }

        public string Configuration { get; set; }

        /// <summary>
        /// The set of target frameworks that should be probed for test assemblies.
        /// </summary>
        public List<string> TargetFrameworks { get; set; } = new List<string>();

        public List<string> IncludeFilter { get; set; } = new List<string>();

        public List<string> ExcludeFilter { get; set; } = new List<string>();

        public string ArtifactsDirectory { get; }

        /// <summary>
        /// Time after which the runner should kill the xunit process and exit with a failure.
        /// </summary>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Retry tests on failure 
        /// </summary>
        public bool Retry { get; set; }

        /// <summary>
        /// Whether or not to collect dumps on crashes and timeouts.
        /// </summary>
        public bool CollectDumps { get; set; }

        /// <summary>
        /// The path to procdump.exe
        /// </summary>
        public string? ProcDumpFilePath { get; set; }

        /// <summary>
        /// Disable partitioning and parallelization across test assemblies.
        /// </summary>
        public bool Sequential { get; set; }

        /// <summary>
        /// Path to the dotnet executable we should use for running dotnet test
        /// </summary>
        public string DotnetFilePath { get; set; }

        /// <summary>
        /// Directory to hold all of the xml files created as test results.
        /// </summary>
        public string TestResultsDirectory { get; set; }

        /// <summary>
        /// Directory to hold dump files and other log files created while running tests.
        /// </summary>
        public string LogFilesDirectory { get; set; }

        public string Platform { get; set; }

        public Options(
            string dotnetFilePath,
            string artifactsDirectory,
            string configuration,
            string testResultsDirectory,
            string logFilesDirectory,
            string platform)
        {
            DotnetFilePath = dotnetFilePath;
            ArtifactsDirectory = artifactsDirectory;
            Configuration = configuration;
            TestResultsDirectory = testResultsDirectory;
            LogFilesDirectory = logFilesDirectory;
            Platform = platform;
        }

        internal static Options? Parse(string[] args)
        {
            string? dotnetFilePath = null;
            var platform = "x64";
            var includeHtml = false;
            var targetFrameworks = new List<string>();
            var configuration = "Debug";
            var includeFilter = new List<string>();
            var excludeFilter = new List<string>();
            var sequential = false;
            var retry = false;
            string? traits = null;
            string? noTraits = null;
            int? timeout = null;
            string? resultFileDirectory = null;
            string? logFileDirectory = null;
            var display = Display.None;
            var collectDumps = false;
            string? procDumpFilePath = null;
            string? artifactsPath = null;
            var optionSet = new OptionSet()
            {
                { "dotnet=", "Path to dotnet", (string s) => dotnetFilePath = s },
                { "configuration=", "Configuration to test: Debug or Release", (string s) => configuration = s },
                { "tfm=", "Target framework to test", (string s) => targetFrameworks.Add(s) },
                { "include=", "Expression for including unit test dlls: default *.UnitTests.dll", (string s) => includeFilter.Add(s) },
                { "exclude=", "Expression for excluding unit test dlls: default is empty", (string s) => excludeFilter.Add(s) },
                { "platform=", "Platform to test: x86 or x64", (string s) => platform = s },
                { "html", "Include HTML file output", o => includeHtml = o is object },
                { "sequential", "Run tests sequentially", o => sequential = o is object },
                { "traits=", "xUnit traits to include (semicolon delimited)", (string s) => traits = s },
                { "notraits=", "xUnit traits to exclude (semicolon delimited)", (string s) => noTraits = s },
                { "timeout=", "Minute timeout to limit the tests to", (int i) => timeout = i },
                { "out=", "Test result file directory", (string s) => resultFileDirectory = s },
                { "logs=", "Log file directory", (string s) => logFileDirectory = s },
                { "display=", "Display", (Display d) => display = d },
                { "artifactspath=", "Path to the artifacts directory", (string s) => artifactsPath = s },
                { "procdumppath=", "Path to procdump", (string s) => procDumpFilePath = s },
                { "collectdumps", "Whether or not to gather dumps on timeouts and crashes", o => collectDumps = o is object },
                { "retry", "Retry failed test a few times", o => retry = o is object },
            };

            List<string> assemblyList;
            try
            {
                assemblyList = optionSet.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine($"Error parsing command line arguments: {e.Message}");
                optionSet.WriteOptionDescriptions(Console.Out);
                return null;
            }

            artifactsPath ??= TryGetArtifactsPath();
            dotnetFilePath ??= TryGetDotNetPath();
            if (includeFilter.Count == 0)
            {
                includeFilter.Add(".*UnitTests.*");
            }

            if (targetFrameworks.Count == 0)
            {
                targetFrameworks.Add("net472");
            }

            if (artifactsPath is null || !Directory.Exists(artifactsPath))
            {
                Console.WriteLine($"Did not find artifacts directory at {artifactsPath}");
                return null;
            }
            resultFileDirectory ??= Path.Combine(artifactsPath, "TestResults");

            if (dotnetFilePath is null || !File.Exists(dotnetFilePath))
            {
                Console.WriteLine($"Did not find 'dotnet' at {dotnetFilePath}");
                return null;
            }

            if (retry && includeHtml)
            {
                Console.WriteLine($"Cannot specify both --retry and --html");
                return null;
            }

            if (procDumpFilePath is { } && !collectDumps)
            {
                Console.WriteLine($"procdumppath was specified without collectdumps hence it will not be used");
            }

            if (logFileDirectory is null)
            {
                logFileDirectory = resultFileDirectory;
            }

            return new Options(
                dotnetFilePath: dotnetFilePath,
                artifactsDirectory: artifactsPath,
                configuration: configuration,
                testResultsDirectory: resultFileDirectory,
                logFilesDirectory: logFileDirectory,
                platform: platform)
            {
                TargetFrameworks = targetFrameworks,
                IncludeFilter = includeFilter,
                ExcludeFilter = excludeFilter,
                Display = display,
                ProcDumpFilePath = procDumpFilePath,
                CollectDumps = collectDumps,
                Sequential = sequential,
                IncludeHtml = includeHtml,
                Trait = traits,
                NoTrait = noTraits,
                Timeout = timeout is { } t ? TimeSpan.FromMinutes(t) : null,
                Retry = retry,
            };

            static string? TryGetArtifactsPath()
            {
                var path = AppContext.BaseDirectory;
                while (path is object && Path.GetFileName(path) != "artifacts")
                {
                    path = Path.GetDirectoryName(path);
                }

                return path;
            }

            static string? TryGetDotNetPath()
            {
                var dir = RuntimeEnvironment.GetRuntimeDirectory();
                var programName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";

                while (dir != null && !File.Exists(Path.Combine(dir, programName)))
                {
                    dir = Path.GetDirectoryName(dir);
                }

                return dir == null ? null : Path.Combine(dir, programName);
            }
        }
    }
}
