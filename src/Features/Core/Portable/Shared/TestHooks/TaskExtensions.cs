﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Shared.TestHooks
{
    internal static class TaskExtensions
    {
        public static Task CompletesAsyncOperation(this Task task, IAsyncToken asyncToken)
        {
            if (asyncToken is AsynchronousOperationListener.DiagnosticAsyncToken diagnosticToken)
            {
                diagnosticToken.AssociateWithTask(task);
            }

            return task.CompletesTrackingOperation(asyncToken);
        }

        public static Task CompletesTrackingOperation(this Task task, IDisposable token)
        {
            if (token == null || token == EmptyAsyncToken.Instance)
            {
                return task;
            }

            return task.SafeContinueWith(
                t => token.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
