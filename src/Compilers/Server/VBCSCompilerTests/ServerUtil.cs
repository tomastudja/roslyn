﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.CodeAnalysis.CompilerServer.UnitTests
{
    internal struct ServerStats
    {
        internal readonly int Connections;
        internal readonly int CompletedConnections;

        internal ServerStats(int connections, int completedConnections)
        {
            Connections = connections;
            CompletedConnections = completedConnections;
        }
    }

    internal sealed class ServerData : IDisposable
    {
        internal CancellationTokenSource CancellationTokenSource { get; }
        internal Task<ServerStats> ServerTask { get; }
        internal Task ListenTask { get; }
        internal string PipeName { get; }

        internal ServerData(CancellationTokenSource cancellationTokenSource, string pipeName, Task<ServerStats> serverTask, Task listenTask)
        {
            CancellationTokenSource = cancellationTokenSource;
            PipeName = pipeName;
            ServerTask = serverTask;
            ListenTask = listenTask;
        }

        internal async Task<ServerStats> Complete()
        {
            CancellationTokenSource.Cancel();
            return await ServerTask;
        }

        internal async Task Verify(int connections, int completed)
        {
            var stats = await Complete().ConfigureAwait(false);
            Assert.Equal(connections, stats.Connections);
            Assert.Equal(completed, stats.CompletedConnections);
        }

        public void Dispose()
        {
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                CancellationTokenSource.Cancel();
            }

            ServerTask.Wait();
        }
    }

    internal static class ServerUtil
    {
        internal static string DefaultClientDirectory { get; } = Path.GetDirectoryName(typeof(DesktopBuildClientTests).Assembly.Location);
        internal static string DefaultSdkDirectory { get; } = RuntimeEnvironment.GetRuntimeDirectory();

        internal static BuildPaths CreateBuildPaths(string workingDir)
        {
            return new BuildPaths(
                clientDir: DefaultClientDirectory,
                workingDir: workingDir,
                sdkDir: DefaultSdkDirectory);
        }

        internal static ServerData CreateServer(
            string pipeName = null,
            TimeSpan? timeout = null,
            ICompilerServerHost compilerServerHost = null,
            IClientConnectionHost clientConnectionHost = null)
        {
            pipeName = pipeName ?? Guid.NewGuid().ToString();
            compilerServerHost = compilerServerHost ?? new DesktopCompilerServerHost(DefaultClientDirectory, DefaultSdkDirectory);

            var serverStatsSource = new TaskCompletionSource<ServerStats>();
            var serverListenSource = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource();
            var thread = new Thread(_ =>
            {
                var listener = new TestableDiagnosticListener();
                listener.Listening += (sender, e) => { serverListenSource.TrySetResult(true); };
                try
                {
                    clientConnectionHost = clientConnectionHost ?? new NamedPipeClientConnectionHost(compilerServerHost, pipeName);

                    var mutexName = BuildProtocolConstants.GetServerMutexName(pipeName);
                    VBCSCompiler.Run(
                        mutexName,
                        clientConnectionHost,
                        listener,
                        timeout ?? TimeSpan.FromMilliseconds(-1),
                        cts.Token);
                }
                finally
                {
                    var serverStats = new ServerStats(connections: listener.ConnectionCount, completedConnections: listener.CompletedCount);
                    serverStatsSource.SetResult(serverStats);
                }
            });

            thread.Start();

            return new ServerData(cts, pipeName, serverStatsSource.Task, serverListenSource.Task);
        }

        /// <summary>
        /// Create a compiler server that fails all connections.
        /// </summary>
        internal static ServerData CreateServerFailsConnection(string pipeName = null)
        {
            pipeName = pipeName ?? Guid.NewGuid().ToString();

            var taskSource = new TaskCompletionSource<ServerStats>();
            var cts = new CancellationTokenSource();
            using (var mre = new ManualResetEvent(initialState: false))
            {
                var thread = new Thread(_ =>
                {
                    var mutexName = BuildProtocolConstants.GetServerMutexName(pipeName);
                    bool holdsMutex;
                    using (var serverMutex = new Mutex(initiallyOwned: true,
                                                       name: mutexName,
                                                       createdNew: out holdsMutex))
                    {
                        mre.Set();
                        if (!holdsMutex)
                        {
                            throw new InvalidOperationException("Mutex should be unique");
                        }

                        var connections = CreateServerFailsConnectionCore(pipeName, cts.Token).Result;
                        taskSource.SetResult(new ServerStats(connections: connections, completedConnections: 0));
                    }
                });

                thread.Start();

                // Can't exit until the mutex is acquired.  Otherwise the client can end up in a race 
                // condition trying to start the server.
                mre.WaitOne();
            }

            return new ServerData(cts, pipeName, taskSource.Task, Task.FromException(new Exception()));
        }

        internal static async Task<BuildResponse> Send(string pipeName, BuildRequest request)
        {
            using (var client = new NamedPipeClientStream(pipeName))
            {
                await client.ConnectAsync();
                await request.WriteAsync(client);
                return await BuildResponse.ReadAsync(client);
            }
        }

        internal static async Task<int> SendShutdown(string pipeName)
        {
            var response = await Send(pipeName, BuildRequest.CreateShutdown());
            return ((ShutdownBuildResponse)response).ServerProcessId;
        }

        private static async Task<int> CreateServerFailsConnectionCore(string pipeName, CancellationToken cancellationToken)
        {
            var connections = 0;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    using (var pipeStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        await pipeStream.WaitForConnectionAsync(cancellationToken);
                        connections++;
                    }
                }
            }
            catch (Exception)
            {
                // Exceptions are okay and expected here
            }

            return connections;
        }
    }
}