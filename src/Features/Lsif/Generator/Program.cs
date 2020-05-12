﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.LanguageServerIndexFormat.Generator.Writing;
using Microsoft.CodeAnalysis.MSBuild;

namespace Microsoft.CodeAnalysis.LanguageServerIndexFormat.Generator
{
    internal static class Program
    {
        public static Task Main(string[] args)
        {
            var generateCommand = new RootCommand("generates an LSIF file")
            {
                new Option("--solution", "input solution file") { Argument = new Argument<FileInfo>().ExistingOnly() },
                new Option("--compiler-invocation", "path to a .json file that contains the information for a csc/vbc invocation") { Argument = new Argument<FileInfo>().ExistingOnly() },
                new Option("--output", "file to write the LSIF output to, instead of the console") { Argument = new Argument<string?>(defaultValue: () => null).LegalFilePathsOnly() },
                new Option("--output-format", "format of LSIF output") { Argument = new Argument<LsifFormat>(defaultValue: () => LsifFormat.Line) },
                new Option("--log", "file to write a log to") { Argument = new Argument<string?>(defaultValue: () => null).LegalFilePathsOnly() }
            };

            generateCommand.Handler = CommandHandler.Create((Func<FileInfo?, FileInfo?, string?, LsifFormat, string?, Task>)GenerateAsync);

            return generateCommand.InvokeAsync(args);
        }

        private static async Task GenerateAsync(FileInfo? solution, FileInfo? compilerInvocation, string? output, LsifFormat outputFormat, string? log)
        {
            // If we have an output file, we'll write to that, else we'll use Console.Out
            using StreamWriter? outputFile = output != null ? new StreamWriter(output) : null;
            TextWriter outputWriter = outputFile ?? Console.Out;

            using TextWriter logFile = log != null ? new StreamWriter(log) : TextWriter.Null;
            ILsifJsonWriter lsifWriter = outputFormat switch
            {
                LsifFormat.Json => new JsonModeLsifJsonWriter(outputWriter),
                LsifFormat.Line => new LineModeLsifJsonWriter(outputWriter),
                _ => throw new NotImplementedException()
            };

            try
            {
                // Exactly one of "solution" or "compilerInvocation" should be specified
                if (solution != null && compilerInvocation == null)
                {
                    await GenerateFromSolutionAsync(solution, lsifWriter, logFile);
                }
                else if (compilerInvocation != null && solution == null)
                {
                    await GenerateFromCompilerInvocationAsync(compilerInvocation, lsifWriter, logFile);
                }
                else
                {
                    throw new Exception("Exactly one of either a solution path or a compiler invocation path should be supplied.");
                }
            }
            catch (Exception e)
            {
                // If it failed, write out to the logs and error, but propagate the error too
                var message = "Unhandled exception: " + e.ToString();
                await logFile.WriteLineAsync(message);
                Console.Error.WriteLine(message);
                throw;
            }

            (lsifWriter as IDisposable)?.Dispose();
            await logFile.WriteLineAsync("Generation complete.");
        }

        private static async Task GenerateFromSolutionAsync(FileInfo solutionFile, ILsifJsonWriter lsifWriter, TextWriter logFile)
        {
            // Make sure we pick the highest version
            var msbuildInstance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(i => i.Version).FirstOrDefault();
            if (msbuildInstance == null)
            {
                throw new Exception("No MSBuild instances installed with Visual Studio could be found.");
            }
            else
            {
                await logFile.WriteLineAsync($"Using the MSBuild instance located at {msbuildInstance.MSBuildPath}.");
            }

            MSBuildLocator.RegisterInstance(msbuildInstance);

            await GenerateFromSolutionWithMSBuildLocatedAsync(solutionFile, lsifWriter, logFile);
        }

        // This method can't be loaded until we've registered MSBuild with MSBuildLocator, as otherwise
        // we load ILogger prematurely which breaks MSBuildLocator.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task GenerateFromSolutionWithMSBuildLocatedAsync(FileInfo solutionFile, ILsifJsonWriter lsifWriter, TextWriter logFile)
        {
            await logFile.WriteLineAsync($"Loading {solutionFile.FullName}...");

            var solutionLoadStopwatch = Stopwatch.StartNew();

            var msbuildWorkspace = MSBuildWorkspace.Create();
            var solution = await msbuildWorkspace.OpenSolutionAsync(solutionFile.FullName);

            await logFile.WriteLineAsync($"Load of the solution completed in {solutionLoadStopwatch.Elapsed.ToDisplayString()}.");
            var lsifGenerator = new Generator(lsifWriter);

            Stopwatch totalTimeInGenerationAndCompilationFetchStopwatch = Stopwatch.StartNew();
            TimeSpan totalTimeInGenerationPhase = TimeSpan.Zero;

            foreach (var project in solution.Projects)
            {
                if (project.SupportsCompilation && project.FilePath != null)
                {
                    var compilationCreationStopwatch = Stopwatch.StartNew();
                    var compilation = (await project.GetCompilationAsync())!;

                    await logFile.WriteLineAsync($"Fetch of compilation for {project.FilePath} completed in {compilationCreationStopwatch.Elapsed.ToDisplayString()}.");

                    var generationForProjectStopwatch = Stopwatch.StartNew();
                    lsifGenerator.GenerateForCompilation(compilation, project.FilePath, project.LanguageServices);
                    generationForProjectStopwatch.Stop();

                    totalTimeInGenerationPhase += generationForProjectStopwatch.Elapsed;

                    await logFile.WriteLineAsync($"Generation for {project.FilePath} completed in {generationForProjectStopwatch.Elapsed.ToDisplayString()}.");
                }
            }

            await logFile.WriteLineAsync($"Total time spent in the generation phase for all projects, excluding compilation fetch time: {totalTimeInGenerationPhase.ToDisplayString()}");
            await logFile.WriteLineAsync($"Total time spent in the generation phase for all projects, including compilation fetch time: {totalTimeInGenerationAndCompilationFetchStopwatch.Elapsed.ToDisplayString()}");
        }

        private static async Task GenerateFromCompilerInvocationAsync(FileInfo compilerInvocationFile, ILsifJsonWriter lsifWriter, TextWriter logFile)
        {
            await logFile.WriteLineAsync($"Processing compiler invocation from {compilerInvocationFile.FullName}...");

            var compilerInvocationLoadStopwatch = Stopwatch.StartNew();
            var compilerInvocation = await CompilerInvocation.CreateFromJsonAsync(File.ReadAllText(compilerInvocationFile.FullName));
            await logFile.WriteLineAsync($"Load of the project completed in {compilerInvocationLoadStopwatch.Elapsed.ToDisplayString()}.");

            var generationStopwatch = Stopwatch.StartNew();
            var lsifGenerator = new Generator(lsifWriter);

            lsifGenerator.GenerateForCompilation(compilerInvocation.Compilation, compilerInvocation.ProjectFilePath, compilerInvocation.LanguageServices);
            await logFile.WriteLineAsync($"Generation for {compilerInvocation.ProjectFilePath} completed in {generationStopwatch.Elapsed.ToDisplayString()}.");
        }
    }
}
