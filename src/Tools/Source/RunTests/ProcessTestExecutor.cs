﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace RunTests
{
    internal sealed class ProcessTestExecutor
    {
        public TestExecutionOptions Options { get; }

        internal ProcessTestExecutor(TestExecutionOptions options)
        {
            Options = options;
        }

        public string GetCommandLineArguments(WorkItemInfo workItem, bool useSingleQuotes, Options options)
        {
            // http://www.gnu.org/software/bash/manual/html_node/Single-Quotes.html
            // Single quotes are needed in bash to avoid the need to escape characters such as backtick (`) which are found in metadata names.
            // Batch scripts don't need to worry about escaping backticks, but they don't support single quoted strings, so we have to use double quotes.
            // We also need double quotes when building an arguments string for Process.Start in .NET Core so that splitting/unquoting works as expected.
            var sep = useSingleQuotes ? "'" : @"""";

            var builder = new StringBuilder();
            builder.Append($@"test");

            var escapedAssemblyPaths = workItem.Filters.Keys.Select(assembly => $"{sep}{assembly.AssemblyPath}{sep}");
            builder.Append($@" {string.Join(" ", escapedAssemblyPaths)}");

            var filters = workItem.Filters.Values.SelectMany(filter => filter).Where(filter => !string.IsNullOrEmpty(filter.GetFilterString())).ToImmutableArray();
            if (filters.Length > 0 || !string.IsNullOrWhiteSpace(Options.TestFilter))
            {
                builder.Append($@" --filter {sep}");
                var any = false;
                foreach (var filter in filters)
                {
                    MaybeAddSeparator();
                    builder.Append(filter.GetFilterString());
                }
                builder.Append(sep);

                if (Options.TestFilter is object)
                {
                    MaybeAddSeparator();
                    builder.Append(Options.TestFilter);
                }

                void MaybeAddSeparator(char separator = '|')
                {
                    if (any)
                    {
                        builder.Append(separator);
                    }

                    any = true;
                }
            }

            builder.Append($@" --arch {options.Architecture}");
            builder.Append($@" --logger {sep}xunit;LogFilePath={GetResultsFilePath(workItem, options, "xml")}{sep}");

            if (Options.IncludeHtml)
            {
                builder.AppendFormat($@" --logger {sep}html;LogFileName={GetResultsFilePath(workItem, options, "html")}{sep}");
            }

            if (!Options.CollectDumps)
            {
                // The 'CollectDumps' option uses operating system features to collect dumps when a process crashes. We
                // only enable the test executor blame feature in remaining cases, as the latter relies on ProcDump and
                // interferes with automatic crash dump collection on Windows.
                builder.Append(" --blame-crash");
            }

            // The 25 minute timeout in integration tests accounts for the fact that VSIX deployment and/or experimental hive reset and
            // configuration can take significant time (seems to vary from ~10 seconds to ~15 minutes), and the blame
            // functionality cannot separate this configuration overhead from the first test which will eventually run.
            // https://github.com/dotnet/roslyn/issues/59851
            //
            // Helix timeout is 15 minutes as helix jobs fully timeout in 30minutes.  So in order to capture dumps we need the timeout
            // to be 2x shorter than the expected test run time (15min) in case only the last test hangs.
            var timeout = options.UseHelix ? "15minutes" : "25minutes";

            builder.Append($" --blame-hang-dump-type full --blame-hang-timeout {timeout}");

            return builder.ToString();
        }

        private string GetResultsFilePath(WorkItemInfo workItemInfo, Options options, string suffix = "xml")
        {
            var fileName = $"WorkItem_{workItemInfo.PartitionIndex}_{options.Architecture}_test_results.{suffix}";
            return Path.Combine(Options.TestResultsDirectory, fileName);
        }

        public async Task<TestResult> RunTestAsync(WorkItemInfo workItemInfo, Options options, CancellationToken cancellationToken)
        {
            var result = await RunTestAsyncInternal(workItemInfo, options, isRetry: false, cancellationToken);

            // For integration tests (TestVsi), we make one more attempt to re-run failed tests.
            if (Options.Retry && !HasBuiltInRetry(workItemInfo) && !Options.IncludeHtml && !result.Succeeded)
            {
                return await RunTestAsyncInternal(workItemInfo, options, isRetry: true, cancellationToken);
            }

            return result;

            static bool HasBuiltInRetry(WorkItemInfo workItemInfo)
            {
                // vs-extension-testing handles test retry internally.
                return workItemInfo.Filters.Keys.Any(key => key.AssemblyName == "Microsoft.VisualStudio.LanguageServices.New.IntegrationTests.dll");
            }
        }

        private async Task<TestResult> RunTestAsyncInternal(WorkItemInfo workItemInfo, Options options, bool isRetry, CancellationToken cancellationToken)
        {
            try
            {
                var commandLineArguments = GetCommandLineArguments(workItemInfo, useSingleQuotes: false, options);
                var resultsFilePath = GetResultsFilePath(workItemInfo, options);
                var resultsDir = Path.GetDirectoryName(resultsFilePath);
                var htmlResultsFilePath = Options.IncludeHtml ? GetResultsFilePath(workItemInfo, options, "html") : null;
                var processResultList = new List<ProcessResult>();
                ProcessInfo? procDumpProcessInfo = null;

                // NOTE: xUnit doesn't always create the log directory
                Directory.CreateDirectory(resultsDir!);

                // Define environment variables for processes started via ProcessRunner.
                var environmentVariables = new Dictionary<string, string>();
                Options.ProcDumpInfo?.WriteEnvironmentVariables(environmentVariables);

                if (isRetry && File.Exists(resultsFilePath))
                {
                    ConsoleUtil.WriteLine("Starting a retry. Tests which failed will run a second time to reduce flakiness.");
                    try
                    {
                        var doc = XDocument.Load(resultsFilePath);
                        foreach (var test in doc.XPathSelectElements("/assemblies/assembly/collection/test[@result='Fail']"))
                        {
                            ConsoleUtil.WriteLine($"  {test.Attribute("name")!.Value}: {test.Attribute("result")!.Value}");
                        }
                    }
                    catch
                    {
                        ConsoleUtil.WriteLine("  ...Failed to identify the list of specific failures.");
                    }

                    // Copy the results file path, since the new xunit run will overwrite it
                    var backupResultsFilePath = Path.ChangeExtension(resultsFilePath, ".old");
                    File.Copy(resultsFilePath, backupResultsFilePath, overwrite: true);

                    // If running the process with this varialbe added, we assume that this file contains 
                    // xml logs from the first attempt.
                    environmentVariables.Add("OutputXmlFilePath", backupResultsFilePath);
                }

                // NOTE: xUnit seems to have an occasional issue creating logs create
                // an empty log just in case, so our runner will still fail.
                File.Create(resultsFilePath).Close();

                var start = DateTime.UtcNow;
                var dotnetProcessInfo = ProcessRunner.CreateProcess(
                    ProcessRunner.CreateProcessStartInfo(
                        Options.DotnetFilePath,
                        commandLineArguments,
                        displayWindow: false,
                        captureOutput: true,
                        environmentVariables: environmentVariables),
                    lowPriority: false,
                    cancellationToken: cancellationToken);
                Logger.Log($"Create xunit process with id {dotnetProcessInfo.Id} for test {workItemInfo.DisplayName}");

                var xunitProcessResult = await dotnetProcessInfo.Result;
                var span = DateTime.UtcNow - start;

                Logger.Log($"Exit xunit process with id {dotnetProcessInfo.Id} for test {workItemInfo.DisplayName} with code {xunitProcessResult.ExitCode}");
                processResultList.Add(xunitProcessResult);
                if (procDumpProcessInfo != null)
                {
                    var procDumpProcessResult = await procDumpProcessInfo.Value.Result;
                    Logger.Log($"Exit procdump process with id {procDumpProcessInfo.Value.Id} for {dotnetProcessInfo.Id} for test {workItemInfo.DisplayName} with code {procDumpProcessResult.ExitCode}");
                    processResultList.Add(procDumpProcessResult);
                }

                if (xunitProcessResult.ExitCode != 0)
                {
                    // On occasion we get a non-0 output but no actual data in the result file.  The could happen
                    // if xunit manages to crash when running a unit test (a stack overflow could cause this, for instance).
                    // To avoid losing information, write the process output to the console.  In addition, delete the results
                    // file to avoid issues with any tool attempting to interpret the (potentially malformed) text.
                    var resultData = string.Empty;
                    try
                    {
                        resultData = File.ReadAllText(resultsFilePath).Trim();
                    }
                    catch
                    {
                        // Happens if xunit didn't produce a log file
                    }

                    if (resultData.Length == 0)
                    {
                        // Delete the output file.
                        File.Delete(resultsFilePath);
                        resultsFilePath = null;
                        htmlResultsFilePath = null;
                    }
                }

                Logger.Log($"Command line {workItemInfo.DisplayName} completed in {span.TotalSeconds} seconds: {Options.DotnetFilePath} {commandLineArguments}");
                var standardOutput = string.Join(Environment.NewLine, xunitProcessResult.OutputLines) ?? "";
                var errorOutput = string.Join(Environment.NewLine, xunitProcessResult.ErrorLines) ?? "";

                var testResultInfo = new TestResultInfo(
                    exitCode: xunitProcessResult.ExitCode,
                    resultsFilePath: resultsFilePath,
                    htmlResultsFilePath: htmlResultsFilePath,
                    elapsed: span,
                    standardOutput: standardOutput,
                    errorOutput: errorOutput);

                return new TestResult(
                    workItemInfo,
                    testResultInfo,
                    commandLineArguments,
                    processResults: ImmutableArray.CreateRange(processResultList));
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to run {workItemInfo.DisplayName} with {Options.DotnetFilePath}. {ex}");
            }
        }
    }
}
