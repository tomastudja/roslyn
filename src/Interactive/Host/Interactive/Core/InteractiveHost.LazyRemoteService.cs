﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ErrorReporting;
using Roslyn.Utilities;
using StreamJsonRpc;

namespace Microsoft.CodeAnalysis.Interactive
{
    internal partial class InteractiveHost
    {
        private sealed class LazyRemoteService
        {
            private readonly AsyncLazy<InitializedRemoteService> _lazyInitializedService;
            private readonly CancellationTokenSource _cancellationSource;

            public readonly InteractiveHostOptions Options;
            public readonly InteractiveHost Host;
            public readonly bool SkipInitialization;
            public readonly int InstanceId;

            public LazyRemoteService(InteractiveHost host, InteractiveHostOptions options, int instanceId, bool skipInitialization)
            {
                _lazyInitializedService = new AsyncLazy<InitializedRemoteService>(TryStartAndInitializeProcessAsync, cacheResult: true);
                _cancellationSource = new CancellationTokenSource();
                InstanceId = instanceId;
                Options = options;
                Host = host;
                SkipInitialization = skipInitialization;
            }

            public void Dispose()
            {
                // Cancel the creation of the process if it is in progress.
                // If it is the cancellation will clean up all resources allocated during the creation.
                _cancellationSource.Cancel();

                // If the value has been calculated already, dispose the service.
                if (_lazyInitializedService.TryGetValue(out var initializedService))
                {
                    initializedService.Service?.Dispose();
                }
            }

            internal Task<InitializedRemoteService> GetInitializedServiceAsync()
                => _lazyInitializedService.GetValueAsync(_cancellationSource.Token);

            internal InitializedRemoteService? TryGetInitializedService()
                => _lazyInitializedService.TryGetValue(out var service) ? service : default;

            private async Task<InitializedRemoteService> TryStartAndInitializeProcessAsync(CancellationToken cancellationToken)
            {
                try
                {
                    Host.ProcessStarting?.Invoke(Options.InitializationFile != null);

                    var remoteService = await TryStartProcessAsync(Options.GetHostPath(), Options.Culture, cancellationToken).ConfigureAwait(false);
                    if (remoteService == null)
                    {
                        return default;
                    }

                    if (SkipInitialization)
                    {
                        return new InitializedRemoteService(remoteService, new RemoteExecutionResult(success: true));
                    }

                    bool initializing = true;
                    cancellationToken.Register(() =>
                    {
                        if (initializing)
                        {
                            // kill the process without triggering auto-reset:
                            remoteService.Dispose();
                        }
                    });

                    // try to execute initialization script:
                    var isRestarting = InstanceId > 1;
                    var initializationResult = await InvokeRemoteAsync<RemoteExecutionResult>(remoteService, nameof(Service.InitializeContextAsync), Options.InitializationFile, isRestarting).ConfigureAwait(false);
                    initializing = false;
                    if (!initializationResult.Success)
                    {
                        Host.ReportProcessExited(remoteService.Process);
                        remoteService.Dispose();

                        return default;
                    }

                    // Hook up a handler that initiates restart when the process exits.
                    // Note that this is just so that we restart the process as soon as we see it dying and it doesn't need to be 100% bullet-proof.
                    // If we don't receive the "process exited" event we will restart the process upon the next remote operation.
                    remoteService.HookAutoRestartEvent();

                    return new InitializedRemoteService(remoteService, initializationResult);
                }
                catch (Exception e) when (FatalError.ReportUnlessCanceled(e))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            private async Task<RemoteService?> TryStartProcessAsync(string hostPath, CultureInfo culture, CancellationToken cancellationToken)
            {
                int currentProcessId = Process.GetCurrentProcess().Id;
                var pipeName = typeof(InteractiveHost).FullName + Guid.NewGuid();

                var newProcess = new Process
                {
                    StartInfo = new ProcessStartInfo(hostPath)
                    {
                        Arguments = pipeName + " " + currentProcessId,
                        WorkingDirectory = Host._initialWorkingDirectory,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardErrorEncoding = Encoding.UTF8,
                        StandardOutputEncoding = Encoding.UTF8
                    },

                    // enables Process.Exited event to be raised:
                    EnableRaisingEvents = true
                };

                newProcess.Start();

                Host.InteractiveHostProcessCreated?.Invoke(newProcess);

                int newProcessId = -1;
                try
                {
                    newProcessId = newProcess.Id;
                }
                catch
                {
                    newProcessId = 0;
                }

                var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                JsonRpc jsonRpc;

                void ProcessExitedBeforeEstablishingConnection(object sender, EventArgs e)
                    => _cancellationSource.Cancel();

                // Connecting the named pipe client would hang if the process exits before the connection is established,
                // as the client waits for the server to become available. We signal the cancellation token to abort.
                newProcess.Exited += ProcessExitedBeforeEstablishingConnection;

                try
                {
                    if (!CheckAlive(newProcess, hostPath))
                    {
                        return null;
                    }

                    await clientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);

                    jsonRpc = JsonRpc.Attach(clientStream);

                    await jsonRpc.InvokeWithCancellationAsync(
                        nameof(Service.InitializeAsync),
                        new object[] { Host._replServiceProviderType.AssemblyQualifiedName, culture.Name },
                        cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    if (CheckAlive(newProcess, hostPath))
                    {
                        RemoteService.InitiateTermination(newProcess, newProcessId);
                    }

                    return null;
                }
                finally
                {
                    newProcess.Exited -= ProcessExitedBeforeEstablishingConnection;
                }

                return new RemoteService(Host, newProcess, newProcessId, jsonRpc);
            }
            private bool CheckAlive(Process process, string hostPath)
            {
                bool alive = process.IsAlive();
                if (!alive)
                {
                    string errorString = process.StandardError.ReadToEnd();

                    Host.WriteOutputInBackground(
                        isError: true,
                        string.Format(InteractiveHostResources.Failed_to_launch_0_process_exit_code_colon_1_with_output_colon, hostPath, process.ExitCode),
                        errorString);
                }

                return alive;
            }
        }
    }
}
