﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Roslyn.Utilities;
using static Microsoft.CodeAnalysis.CommandLine.CompilerServerLogger;
using static Microsoft.CodeAnalysis.CommandLine.NativeMethods;

namespace Microsoft.CodeAnalysis.CommandLine
{
    internal struct BuildPathsAlt
    {
        /// <summary>
        /// The path which contains the compiler binaries and response files.
        /// </summary>
        internal string ClientDirectory { get; }

        /// <summary>
        /// The path in which the compilation takes place.
        /// </summary>
        internal string WorkingDirectory { get; }

        /// <summary>
        /// The path which contains mscorlib.  This can be null when specified by the user or running in a 
        /// CoreClr environment.
        /// </summary>
        internal string SdkDirectory { get; }

        /// <summary>
        /// The temporary directory a compilation should use instead of <see cref="Path.GetTempPath"/>.  The latter
        /// relies on global state individual compilations should ignore.
        /// </summary>
        internal string TempDirectory { get; }

        internal BuildPathsAlt(string clientDir, string workingDir, string sdkDir, string tempDir)
        {
            ClientDirectory = clientDir;
            WorkingDirectory = workingDir;
            SdkDirectory = sdkDir;
            TempDirectory = tempDir;
        }
    }

    internal sealed class BuildServerConnection
    {
        // Spend up to 1s connecting to existing process (existing processes should be always responsive).
        internal const int TimeOutMsExistingProcess = 1000;

        // Spend up to 20s connecting to a new process, to allow time for it to start.
        internal const int TimeOutMsNewProcess = 20000;

        /// <summary>
        /// Determines if the compiler server is supported in this environment.
        /// </summary>
        internal static bool IsCompilerServerSupported(string tempPath)
        {
            var pipeName = GetPipeNameForPathOpt("");
            return pipeName != null && !IsPipePathTooLong(pipeName, tempPath);
        }

        public static Task<BuildResponse> RunServerCompilation(
            RequestLanguage language,
            string sharedCompilationId,
            List<string> arguments,
            BuildPathsAlt buildPaths,
            string keepAlive,
            string libEnvVariable,
            CancellationToken cancellationToken)
        {
            var pipeNameOpt = sharedCompilationId ?? GetPipeNameForPathOpt(buildPaths.ClientDirectory);

            return RunServerCompilationCore(
                language,
                arguments,
                buildPaths,
                pipeNameOpt,
                keepAlive,
                libEnvVariable,
                timeoutOverride: null,
                tryCreateServerFunc: TryCreateServerCore,
                cancellationToken: cancellationToken);
        }

        internal static async Task<BuildResponse> RunServerCompilationCore(
            RequestLanguage language,
            List<string> arguments,
            BuildPathsAlt buildPaths,
            string pipeName,
            string keepAlive,
            string libEnvVariable,
            int? timeoutOverride,
            Func<string, string, bool> tryCreateServerFunc,
            CancellationToken cancellationToken)
        {
            if (pipeName == null)
            {
                return new RejectedBuildResponse();
            }

            if (buildPaths.TempDirectory == null)
            {
                return new RejectedBuildResponse();
            }

            // early check for the build hash. If we can't find it something is wrong; no point even trying to go to the server
            if (string.IsNullOrWhiteSpace(BuildProtocolConstants.GetCommitHash()))
            {
                return new IncorrectHashBuildResponse();
            }

            var clientDir = buildPaths.ClientDirectory;
            var timeoutNewProcess = timeoutOverride ?? TimeOutMsNewProcess;
            var timeoutExistingProcess = timeoutOverride ?? TimeOutMsExistingProcess;
            Task<NamedPipeClientStream> pipeTask = null;
            Mutex clientMutex = null;
            var holdsMutex = false;
            try
            {
                try
                {
                    var clientMutexName = GetClientMutexName(pipeName);
                    clientMutex = new Mutex(initiallyOwned: true, name: clientMutexName, out holdsMutex);
                }
                catch
                {
                    // The Mutex constructor can throw in certain cases. One specific example is docker containers
                    // where the /tmp directory is restricted. In those cases there is no reliable way to execute
                    // the server and we need to fall back to the command line.
                    //
                    // Example: https://github.com/dotnet/roslyn/issues/24124
                    return new RejectedBuildResponse();
                }

                if (!holdsMutex)
                {
                    try
                    {
                        holdsMutex = clientMutex.WaitOne(timeoutNewProcess);

                        if (!holdsMutex)
                        {
                            return new RejectedBuildResponse();
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        holdsMutex = true;
                    }
                }

                // Check for an already running server
                var serverMutexName = GetServerMutexName(pipeName);
                bool wasServerRunning = WasServerMutexOpen(serverMutexName);
                var timeout = wasServerRunning ? timeoutExistingProcess : timeoutNewProcess;

                if (wasServerRunning || tryCreateServerFunc(clientDir, pipeName))
                {
                    pipeTask = TryConnectToServerAsync(pipeName, timeout, cancellationToken);
                }
            }
            finally
            {
                if (clientMutex != null)
                {
                    if (holdsMutex)
                    {
                        clientMutex.ReleaseMutex();
                    }
                    clientMutex.Dispose();
                }
            }

            if (pipeTask != null)
            {
                var pipe = await pipeTask.ConfigureAwait(false);
                if (pipe != null)
                {
                    var request = BuildRequest.Create(language,
                                                      buildPaths.WorkingDirectory,
                                                      buildPaths.TempDirectory,
                                                      BuildProtocolConstants.GetCommitHash(),
                                                      arguments,
                                                      keepAlive,
                                                      libEnvVariable);

                    return await TryCompile(pipe, request, cancellationToken).ConfigureAwait(false);
                }
            }

            return new RejectedBuildResponse();
        }

        /// <summary>
        /// Try to compile using the server. Returns a null-containing Task if a response
        /// from the server cannot be retrieved.
        /// </summary>
        private static async Task<BuildResponse> TryCompile(NamedPipeClientStream pipeStream,
                                                            BuildRequest request,
                                                            CancellationToken cancellationToken)
        {
            BuildResponse response;
            using (pipeStream)
            {
                // Write the request
                try
                {
                    Log("Begin writing request");
                    await request.WriteAsync(pipeStream, cancellationToken).ConfigureAwait(false);
                    Log("End writing request");
                }
                catch (Exception e)
                {
                    LogException(e, "Error writing build request.");
                    return new RejectedBuildResponse();
                }

                // Wait for the compilation and a monitor to detect if the server disconnects
                var serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Log("Begin reading response");

                var responseTask = BuildResponse.ReadAsync(pipeStream, serverCts.Token);
                var monitorTask = CreateMonitorDisconnectTask(pipeStream, "client", serverCts.Token);
                await Task.WhenAny(responseTask, monitorTask).ConfigureAwait(false);

                Log("End reading response");

                if (responseTask.IsCompleted)
                {
                    // await the task to log any exceptions
                    try
                    {
                        response = await responseTask.ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        LogException(e, "Error reading response");
                        response = new RejectedBuildResponse();
                    }
                }
                else
                {
                    Log("Server disconnect");
                    response = new RejectedBuildResponse();
                }

                // Cancel whatever task is still around
                serverCts.Cancel();
                Debug.Assert(response != null);
                return response;
            }
        }

        /// <summary>
        /// The IsConnected property on named pipes does not detect when the client has disconnected
        /// if we don't attempt any new I/O after the client disconnects. We start an async I/O here
        /// which serves to check the pipe for disconnection.
        /// </summary>
        internal static async Task CreateMonitorDisconnectTask(
            PipeStream pipeStream,
            string identifier = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var buffer = Array.Empty<byte>();

            while (!cancellationToken.IsCancellationRequested && pipeStream.IsConnected)
            {
                // Wait a tenth of a second before trying again
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);

                try
                {
                    Log($"Before poking pipe {identifier}.");
                    await pipeStream.ReadAsync(buffer, 0, 0, cancellationToken).ConfigureAwait(false);
                    Log($"After poking pipe {identifier}.");
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception e)
                {
                    // It is okay for this call to fail.  Errors will be reflected in the
                    // IsConnected property which will be read on the next iteration of the
                    LogException(e, $"Error poking pipe {identifier}.");
                }
            }
        }

        /// <summary>
        /// Connect to the pipe for a given directory and return it.
        /// Throws on cancellation.
        /// </summary>
        /// <param name="pipeName">Name of the named pipe to connect to.</param>
        /// <param name="timeoutMs">Timeout to allow in connecting to process.</param>
        /// <param name="cancellationToken">Cancellation token to cancel connection to server.</param>
        /// <returns>
        /// An open <see cref="NamedPipeClientStream"/> to the server process or null on failure.
        /// </returns>
        internal static async Task<NamedPipeClientStream> TryConnectToServerAsync(
            string pipeName,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            NamedPipeClientStream pipeStream;
            try
            {
                // If the pipe path would be too long, there cannot be a server at the other end.
                // We're not using a saved temp path here because pipes are created with
                // Path.GetTempPath() in corefx NamedPipeClientStream and we want to replicate that behavior.
                if (IsPipePathTooLong(pipeName, Path.GetTempPath()))
                {
                    return null;
                }

                // Machine-local named pipes are named "\\.\pipe\<pipename>".
                // We use the SHA1 of the directory the compiler exes live in as the pipe name.
                // The NamedPipeClientStream class handles the "\\.\pipe\" part for us.
                Log("Attempt to open named pipe '{0}'", pipeName);

                pipeStream = NamedPipeUtil.CreateClient(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                cancellationToken.ThrowIfCancellationRequested();

                Log("Attempt to connect named pipe '{0}'", pipeName);
                try
                {
                    // NamedPipeClientStream.ConnectAsync on the "full" framework has a bug where it
                    // tries to move potentially expensive work (actually connecting to the pipe) to
                    // a background thread with Task.Factory.StartNew. However, that call will merely
                    // queue the work onto the TaskScheduler associated with the "current" Task which
                    // does not guarantee it will be processed on a background thread and this could
                    // lead to a hang.
                    // To avoid this, we first force ourselves to a background thread using Task.Run.
                    // This ensures that the Task created by ConnectAsync will run on the default
                    // TaskScheduler (i.e., on a threadpool thread) which was the intent all along.
                    await Task.Run(() => pipeStream.ConnectAsync(timeoutMs, cancellationToken)).ConfigureAwait(false);
                }
                catch (Exception e) when (e is IOException || e is TimeoutException)
                {
                    // Note: IOException can also indicate timeout. From docs:
                    // TimeoutException: Could not connect to the server within the
                    //                   specified timeout period.
                    // IOException: The server is connected to another client and the
                    //              time-out period has expired.

                    Log($"Connecting to server timed out after {timeoutMs} ms");
                    return null;
                }
                Log("Named pipe '{0}' connected", pipeName);

                cancellationToken.ThrowIfCancellationRequested();

                // Verify that we own the pipe.
                if (!NamedPipeUtil.CheckPipeConnectionOwnership(pipeStream))
                {
                    Log("Owner of named pipe is incorrect");
                    return null;
                }

                return pipeStream;
            }
            catch (Exception e) when (!(e is TaskCanceledException || e is OperationCanceledException))
            {
                LogException(e, "Exception while connecting to process");
                return null;
            }
        }

        internal static (string processFilePath, string commandLineArguments, string toolFilePath) GetServerProcessInfo(string clientDir, string pipeName)
        {
            var serverPathWithoutExtension = Path.Combine(clientDir, "VBCSCompiler");
            var commandLineArgs = $@"""-pipename:{pipeName}""";
            return RuntimeHostInfo.GetProcessInfo(serverPathWithoutExtension, commandLineArgs);
        }

        internal static bool TryCreateServerCore(string clientDir, string pipeName)
        {
            var serverInfo = GetServerProcessInfo(clientDir, pipeName);

            if (!File.Exists(serverInfo.toolFilePath))
            {
                return false;
            }

            if (PlatformInformation.IsWindows)
            {
                // As far as I can tell, there isn't a way to use the Process class to
                // create a process with no stdin/stdout/stderr, so we use P/Invoke.
                // This code was taken from MSBuild task starting code.

                STARTUPINFO startInfo = new STARTUPINFO();
                startInfo.cb = Marshal.SizeOf(startInfo);
                startInfo.hStdError = InvalidIntPtr;
                startInfo.hStdInput = InvalidIntPtr;
                startInfo.hStdOutput = InvalidIntPtr;
                startInfo.dwFlags = STARTF_USESTDHANDLES;
                uint dwCreationFlags = NORMAL_PRIORITY_CLASS | CREATE_NO_WINDOW;

                PROCESS_INFORMATION processInfo;

                Log("Attempting to create process '{0}'", serverInfo.processFilePath);

                var builder = new StringBuilder($@"""{serverInfo.processFilePath}"" {serverInfo.commandLineArguments}");

                bool success = CreateProcess(
                    lpApplicationName: null,
                    lpCommandLine: builder,
                    lpProcessAttributes: NullPtr,
                    lpThreadAttributes: NullPtr,
                    bInheritHandles: false,
                    dwCreationFlags: dwCreationFlags,
                    lpEnvironment: NullPtr, // Inherit environment
                    lpCurrentDirectory: clientDir,
                    lpStartupInfo: ref startInfo,
                    lpProcessInformation: out processInfo);

                if (success)
                {
                    Log("Successfully created process with process id {0}", processInfo.dwProcessId);
                    CloseHandle(processInfo.hProcess);
                    CloseHandle(processInfo.hThread);
                }
                else
                {
                    Log("Failed to create process. GetLastError={0}", Marshal.GetLastWin32Error());
                }
                return success;
            }
            else
            {
                try
                {
                    var startInfo = new ProcessStartInfo()
                    {
                        FileName = serverInfo.processFilePath,
                        Arguments = serverInfo.commandLineArguments,
                        UseShellExecute = false,
                        WorkingDirectory = clientDir,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    Process.Start(startInfo);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <returns>
        /// Null if not enough information was found to create a valid pipe name.
        /// </returns>
        internal static string GetPipeNameForPathOpt(string compilerExeDirectory)
        {
            var basePipeName = GetBasePipeName(compilerExeDirectory);

            // Prefix with username and elevation
            bool isAdmin = false;
            if (PlatformInformation.IsWindows)
            {
                var currentIdentity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(currentIdentity);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            var userName = Environment.UserName;
            if (userName == null)
            {
                return null;
            }

            return GetPipeNameCore(userName, isAdmin, basePipeName);
        }

        internal static string GetPipeNameCore(string userName, bool isAdmin, string basePipeName)
        {
            var pipeName = $"{userName}.{(isAdmin ? 'T' : 'F')}.{basePipeName}";

            // The pipe name is passed between processes as a command line argument as a 
            // quoted value. Unfortunately we can't use ProcessStartInfo.ArgumentList as 
            // we still target net472 (API only available on CoreClr + netstandard). To 
            // make the problem approachable we remove the troublesome characters.
            //
            // This does mean if two users on the same machine are building simultaneously
            // and the user names differ only be a " or / and a _ then there will be a 
            // conflict. That seems rather obscure though.
            return pipeName
                .Replace('"', '_')
                .Replace('\\', '_');
        }

        /// <summary>
        /// Check if our constructed path is too long. On some Unix machines the pipe is a
        /// real file in the temp directory, and there is a limit on how long the path can
        /// be. This will never be true on Windows.
        /// </summary>
        internal static bool IsPipePathTooLong(string pipeName, string tempPath)
        {
            if (PlatformInformation.IsUnix)
            {
                // This is the maximum path length of Unix Domain Sockets on a number of systems.
                // Since CoreFX implements named pipes using Unix Domain Sockets, if we exceed this
                // length than the pipe will fail.
                // This number is considered the smallest known max length according to
                // http://man7.org/linux/man-pages/man7/unix.7.html
                const int MaxPipePathLength = 92;
                const int PrefixLength = 11; // "CoreFxPipe_".Length
                return (tempPath.Length + PrefixLength + pipeName.Length) > MaxPipePathLength;
            }
            return false;
        }

        internal static string GetBasePipeName(string compilerExeDirectory)
        {
            // Normalize away trailing slashes.  File APIs include / exclude this with no 
            // discernable pattern.  Easiest to normalize it here vs. auditing every caller
            // of this method.
            compilerExeDirectory = compilerExeDirectory.TrimEnd(Path.DirectorySeparatorChar);

            string basePipeName;
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(compilerExeDirectory));
                basePipeName = Convert.ToBase64String(bytes)
                    .Substring(0, 10) // We only have ~50 total characters on Mac, so strip this down
                    .Replace("/", "_")
                    .Replace("=", string.Empty);
            }

            return basePipeName;
        }

        internal static bool WasServerMutexOpen(string mutexName)
        {
            try
            {
                Mutex mutex;
                var open = Mutex.TryOpenExisting(mutexName, out mutex);
                if (open)
                {
                    mutex.Dispose();
                    return true;
                }
            }
            catch
            {
                // In the case an exception occured trying to open the Mutex then 
                // the assumption is that it's not open. 
                return false;
            }

            return false;
        }

        internal static string GetServerMutexName(string pipeName)
        {
            return $"{pipeName}.server";
        }

        internal static string GetClientMutexName(string pipeName)
        {
            return $"{pipeName}.client";
        }

        /// <summary>
        /// Gets the value of the temporary path for the current environment assuming the working directory
        /// is <paramref name="workingDir"/>.  This function must emulate <see cref="Path.GetTempPath"/> as 
        /// closely as possible.
        /// </summary>
        public static string GetTempPath(string workingDir)
        {
            if (PlatformInformation.IsUnix)
            {
                // Unix temp path is fine: it does not use the working directory
                // (it uses ${TMPDIR} if set, otherwise, it returns /tmp)
                return Path.GetTempPath();
            }

            var tmp = Environment.GetEnvironmentVariable("TMP");
            if (Path.IsPathRooted(tmp))
            {
                return tmp;
            }

            var temp = Environment.GetEnvironmentVariable("TEMP");
            if (Path.IsPathRooted(temp))
            {
                return temp;
            }

            if (!string.IsNullOrEmpty(workingDir))
            {
                if (!string.IsNullOrEmpty(tmp))
                {
                    return Path.Combine(workingDir, tmp);
                }

                if (!string.IsNullOrEmpty(temp))
                {
                    return Path.Combine(workingDir, temp);
                }
            }

            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            if (Path.IsPathRooted(userProfile))
            {
                return userProfile;
            }

            return Environment.GetEnvironmentVariable("SYSTEMROOT");
        }
    }
}
