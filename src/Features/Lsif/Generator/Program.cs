﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.LanguageServerIndexFormat.Generator.Logging;
using Microsoft.CodeAnalysis.LanguageServerIndexFormat.Generator.Writing;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Roslyn.Utilities;
using CompilerInvocationsReader = Microsoft.Build.Logging.StructuredLogger.CompilerInvocationsReader;

namespace Microsoft.CodeAnalysis.LanguageServerIndexFormat.Generator
{
    internal static class Program
    {
        public static Task Main(string[] args)
        {
            var solution = new CliOption<FileInfo>("--solution") { Description = "input solution file" }.AcceptExistingOnly();
            var project = new CliOption<FileInfo>("--project") { Description = "input project file" }.AcceptExistingOnly();
            var compilerInvocation = new CliOption<FileInfo>("--compiler-invocation") { Description = "path to a .json file that contains the information for a csc/vbc invocation" }.AcceptExistingOnly();
            var binLog = new CliOption<FileInfo>("--binlog") { Description = "path to a MSBuild binlog that csc/vbc invocations will be extracted from" }.AcceptExistingOnly();
            var output = new CliOption<string?>("--output") { Description = "file to write the LSIF output to, instead of the console", DefaultValueFactory = _ => null };
            output.AcceptLegalFilePathsOnly();
            var outputFormat = new CliOption<LsifFormat>("--output-format") { Description = "format of LSIF output", DefaultValueFactory = _ => LsifFormat.Line };
            var log = new CliOption<string?>("--log") { Description = "file to write a log to", DefaultValueFactory = _ => null };
            log.AcceptLegalFilePathsOnly();

            var generateCommand = new CliRootCommand("generates an LSIF file")
            {
                solution,
                project,
                compilerInvocation,
                binLog,
                output,
                outputFormat,
                log
            };

            generateCommand.SetAction((parseResult, cancellationToken) =>
            {
                return GenerateAsync(
                    solution: parseResult.GetValue(solution),
                    project: parseResult.GetValue(project),
                    compilerInvocation: parseResult.GetValue(compilerInvocation),
                    binLog: parseResult.GetValue(binLog),
                    outputFileName: parseResult.GetValue(output),
                    outputFormat: parseResult.GetValue(outputFormat),
                    logFileName: parseResult.GetValue(log),
                    cancellationToken);
            });

            return generateCommand.Parse(args).InvokeAsync(CancellationToken.None);
        }

        private static async Task GenerateAsync(
            FileInfo? solution,
            FileInfo? project,
            FileInfo? compilerInvocation,
            FileInfo? binLog,
            string? outputFileName,
            LsifFormat outputFormat,
            string? logFileName,
            CancellationToken cancellationToken)
        {
            // If we have an output file, we'll write to that, else we'll use Console.Out
            using var outputFile = outputFileName != null ? new StreamWriter(outputFileName, append: false, Encoding.UTF8) : null;
            TextWriter outputWriter;

            if (outputFile is null)
            {
                Console.OutputEncoding = Encoding.UTF8;
                outputWriter = Console.Out;
            }
            else
            {
                outputWriter = outputFile;
            }

            ILsifJsonWriter lsifWriter = outputFormat switch
            {
                LsifFormat.Json => new JsonModeLsifJsonWriter(outputWriter),
                LsifFormat.Line => new LineModeLsifJsonWriter(outputWriter),
                _ => throw new NotImplementedException()
            };

            using var logFile = logFileName is not null and not "stderr" ? new StreamWriter(logFileName) { AutoFlush = true } : null;

            ILogger logger;
            if (logFile is not null)
                logger = new PlainTextLogger(logFile);
            else if (logFileName == "stderr")
                logger = new LsifFormatLogger(Console.Error);
            else
                logger = NullLogger.Instance;

            var totalExecutionTime = Stopwatch.StartNew();

            try
            {
                // Exactly one of "solution", or "project" or "compilerInvocation" should be specified
                var fileInputs = new[] { solution, project, compilerInvocation, binLog };
                var nonNullFileInputs = fileInputs.Count(p => p is not null);

                if (nonNullFileInputs != 1)
                {
                    throw new Exception("Exactly one of either a solution path, project path or a compiler invocation path should be supplied.");
                }

                if (solution != null)
                {
                    LocateAndRegisterMSBuild(logger, solution.Directory);
                    await GenerateFromSolutionAsync(solution, lsifWriter, logger, cancellationToken);
                }
                else if (project != null)
                {
                    LocateAndRegisterMSBuild(logger, project.Directory);
                    await GenerateFromProjectAsync(project, lsifWriter, logger, cancellationToken);
                }
                else if (compilerInvocation != null)
                {
                    await GenerateFromCompilerInvocationAsync(compilerInvocation, lsifWriter, logger, cancellationToken);
                }
                else
                {
                    Contract.ThrowIfNull(binLog);

                    // If we're loading a binlog, we don't need to discover an MSBuild that matches the SDK or source that we're processing, since we're not running
                    // any MSBuild builds or tasks/targets in our process. Since we're reading a binlog, simply none of the SDK will be loaded. We might load analyzers
                    // or source generators from the SDK or user-built, but those must generally target netstandard2.0 so we don't really expect them to have problems loading
                    // on one version of the runtime versus another.
                    LocateAndRegisterMSBuild(logger, sourceDirectory: null);
                    await GenerateFromBinaryLogAsync(binLog, lsifWriter, logger, cancellationToken);
                }
            }
            catch (Exception e)
            {
                // If it failed, write out to the logs, but propagate the error too
                var message = "Unhandled exception: " + e.ToString();
                logger.LogCritical(e, message);
                // System.CommandLine is going to catch the exception and log it in standard error
                throw;
            }

            (lsifWriter as IDisposable)?.Dispose();
            logger.LogInformation($"Generation complete. Total execution time: {totalExecutionTime.Elapsed.ToDisplayString()}");
        }

        private static void LocateAndRegisterMSBuild(ILogger logger, DirectoryInfo? sourceDirectory)
        {
            // Make sure we pick the highest version
            var options = VisualStudioInstanceQueryOptions.Default;

            if (sourceDirectory != null)
                options.WorkingDirectory = sourceDirectory.FullName;

            var msBuildInstance = MSBuildLocator.QueryVisualStudioInstances(options).OrderByDescending(i => i.Version).FirstOrDefault();
            if (msBuildInstance == null)
            {
                throw new Exception($"No MSBuild instances could be found; discovery types being used: {options.DiscoveryTypes}.");
            }
            else
            {
                logger.LogInformation($"Using the MSBuild instance located at {msBuildInstance.MSBuildPath}.");
            }

            MSBuildLocator.RegisterInstance(msBuildInstance);
        }

        private static async Task GenerateFromProjectAsync(
            FileInfo projectFile, ILsifJsonWriter lsifWriter, ILogger logger, CancellationToken cancellationToken)
        {
            await GenerateWithMSBuildWorkspaceAsync(
                projectFile, lsifWriter, logger,
                async (workspace, cancellationToken) =>
                {
                    var project = await workspace.OpenProjectAsync(projectFile.FullName, cancellationToken: cancellationToken);
                    return project.Solution;
                },
                cancellationToken);
        }

        private static async Task GenerateFromSolutionAsync(
            FileInfo solutionFile, ILsifJsonWriter lsifWriter, ILogger logger, CancellationToken cancellationToken)
        {
            await GenerateWithMSBuildWorkspaceAsync(
                solutionFile, lsifWriter, logger,
                (workspace, cancellationToken) => workspace.OpenSolutionAsync(solutionFile.FullName, cancellationToken: cancellationToken),
                cancellationToken);
        }

        // This method can't be loaded until we've registered MSBuild with MSBuildLocator, as otherwise
        // we load ILogger prematurely which breaks MSBuildLocator.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task GenerateWithMSBuildWorkspaceAsync(
            FileInfo solutionOrProjectFile,
            ILsifJsonWriter lsifWriter,
            ILogger logger,
            Func<MSBuildWorkspace, CancellationToken, Task<Solution>> openAsync,
            CancellationToken cancellationToken)
        {
            logger.LogInformation($"Loading {solutionOrProjectFile.FullName}...");

            var solutionLoadStopwatch = Stopwatch.StartNew();

            using var msbuildWorkspace = MSBuildWorkspace.Create(await Composition.CreateHostServicesAsync());
            msbuildWorkspace.WorkspaceFailed += (s, e) => logger.Log(e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure ? LogLevel.Error : LogLevel.Warning, "Problem while loading: " + e.Diagnostic.Message);

            var solution = await openAsync(msbuildWorkspace, cancellationToken);

            var options = GeneratorOptions.Default;

            logger.LogInformation($"Load completed in {solutionLoadStopwatch.Elapsed.ToDisplayString()}.");
            var lsifGenerator = Generator.CreateAndWriteCapabilitiesVertex(lsifWriter, logger);

            var totalTimeInGenerationAndCompilationFetchStopwatch = Stopwatch.StartNew();
            var totalTimeInGenerationPhase = TimeSpan.Zero;

            foreach (var project in solution.Projects)
            {
                if (project.SupportsCompilation && project.FilePath != null)
                {
                    var compilationCreationStopwatch = Stopwatch.StartNew();
                    var compilation = await project.GetRequiredCompilationAsync(cancellationToken);

                    logger.LogInformation($"Fetch of compilation for {project.FilePath} completed in {compilationCreationStopwatch.Elapsed.ToDisplayString()}.");

                    var generationForProjectStopwatch = Stopwatch.StartNew();
                    await lsifGenerator.GenerateForProjectAsync(project, options, cancellationToken);
                    generationForProjectStopwatch.Stop();

                    totalTimeInGenerationPhase += generationForProjectStopwatch.Elapsed;

                    logger.LogInformation($"Generation for {project.FilePath} completed in {generationForProjectStopwatch.Elapsed.ToDisplayString()}.");
                }
            }

            logger.LogInformation($"Total time spent in the generation phase for all projects, excluding compilation fetch time: {totalTimeInGenerationPhase.ToDisplayString()}");
            logger.LogInformation($"Total time spent in the generation phase for all projects, including compilation fetch time: {totalTimeInGenerationAndCompilationFetchStopwatch.Elapsed.ToDisplayString()}");
        }

        private static async Task GenerateFromCompilerInvocationAsync(
            FileInfo compilerInvocationFile, ILsifJsonWriter lsifWriter, ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Processing compiler invocation from {compilerInvocationFile.FullName}...");

            var compilerInvocationLoadStopwatch = Stopwatch.StartNew();
            var project = await CompilerInvocation.CreateFromJsonAsync(File.ReadAllText(compilerInvocationFile.FullName));
            logger.LogInformation($"Load of the project completed in {compilerInvocationLoadStopwatch.Elapsed.ToDisplayString()}.");

            var generationStopwatch = Stopwatch.StartNew();
            var lsifGenerator = Generator.CreateAndWriteCapabilitiesVertex(lsifWriter, logger);

            await lsifGenerator.GenerateForProjectAsync(project, GeneratorOptions.Default, cancellationToken);
            logger.LogInformation($"Generation for {project.FilePath} completed in {generationStopwatch.Elapsed.ToDisplayString()}.");
        }

        // This method can't be loaded until we've registered MSBuild with MSBuildLocator, as otherwise we might load a type prematurely.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task GenerateFromBinaryLogAsync(
            FileInfo binLog, ILsifJsonWriter lsifWriter, ILogger logger, CancellationToken cancellationToken)
        {
            logger.LogInformation($"Reading binlog {binLog.FullName}...");
            var msbuildInvocations = CompilerInvocationsReader.ReadInvocations(binLog.FullName).ToImmutableArray();

            logger.LogInformation($"Load of the binlog complete; {msbuildInvocations.Length} invocations were found.");

            var lsifGenerator = Generator.CreateAndWriteCapabilitiesVertex(lsifWriter, logger);
            using var workspace = new AdhocWorkspace(await Composition.CreateHostServicesAsync());

            foreach (var msbuildInvocation in msbuildInvocations)
            {
                var projectInfo = CommandLineProject.CreateProjectInfo(
                    Path.GetFileNameWithoutExtension(msbuildInvocation.ProjectFilePath),
                    msbuildInvocation.Language == Microsoft.Build.Logging.StructuredLogger.CompilerInvocation.CSharp ? LanguageNames.CSharp : LanguageNames.VisualBasic,
                    msbuildInvocation.CommandLineArguments,
                    msbuildInvocation.ProjectDirectory,
                    workspace)
                    .WithFilePath(msbuildInvocation.ProjectFilePath);

                workspace.OnProjectAdded(projectInfo);

                var project = workspace.CurrentSolution.Projects.Single();

                var generationStopwatch = Stopwatch.StartNew();
                await lsifGenerator.GenerateForProjectAsync(project, GeneratorOptions.Default, cancellationToken);
                logger.LogInformation($"Generation for {project.FilePath} completed in {generationStopwatch.Elapsed.ToDisplayString()}.");

                // Remove the project from the workspace; we reuse the same workspace object to ensure that some workspace-level services (like the IMetadataService
                // or IDocumentationProviderService) are kept around allowing their caches to be reused.
                workspace.OnProjectRemoved(project.Id);
            }
        }
    }
}
