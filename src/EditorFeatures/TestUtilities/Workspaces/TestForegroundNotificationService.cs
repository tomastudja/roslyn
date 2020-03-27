﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.UnitTests
{
    internal class TestForegroundNotificationService : IForegroundNotificationService
    {
        private readonly object _gate = new object();
        private readonly List<Task> _tasks = new List<Task>();
        private readonly TaskQueue _queue = new TaskQueue(AsynchronousOperationListenerProvider.NullListener, TaskScheduler.Default);

        public void RegisterNotification(Func<bool> action, IAsyncToken asyncToken, CancellationToken cancellationToken = default)
        {
            RegisterNotification(action, 0, asyncToken, cancellationToken);
        }

#pragma warning disable CS0618 // Type or member is obsolete (ScheduleTaskInProgress: https://github.com/dotnet/roslyn/issues/42742)
        public void RegisterNotification(Func<bool> action, int delayInMS, IAsyncToken asyncToken, CancellationToken cancellationToken = default)
        {
            Task task;
            lock (_gate)
            {
                task = _queue.ScheduleTaskInProgress(() => Execute_NoLock(action, asyncToken, cancellationToken), cancellationToken);
                _tasks.Add(task);
            }

            task.Wait(cancellationToken);
        }

        private void Execute_NoLock(Func<bool> action, IAsyncToken asyncToken, CancellationToken cancellationToken)
        {
            if (action())
            {
                asyncToken.Dispose();
            }
            else
            {
                _tasks.Add(_queue.ScheduleTaskInProgress(() => Execute_NoLock(action, asyncToken, cancellationToken), cancellationToken));
            }
        }

        public void RegisterNotification(Action action, IAsyncToken asyncToken, CancellationToken cancellationToken = default)
        {
            RegisterNotification(action, 0, asyncToken, cancellationToken);
        }

        public void RegisterNotification(Action action, int delayInMS, IAsyncToken asyncToken, CancellationToken cancellationToken = default)
        {
            Task task;
            lock (_gate)
            {
                task = _queue.ScheduleTaskInProgress(action, cancellationToken).CompletesAsyncOperation(asyncToken);

                _tasks.Add(task);
            }

            task.Wait(cancellationToken);
        }
#pragma warning restore
    }
}
