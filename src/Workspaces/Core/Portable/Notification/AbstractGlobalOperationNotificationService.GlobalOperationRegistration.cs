﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CodeAnalysis.Internal.Log;

namespace Microsoft.CodeAnalysis.Notification;

internal partial class AbstractGlobalOperationNotificationService
{
    private class GlobalOperationRegistration : IDisposable
    {
        private readonly AbstractGlobalOperationNotificationService _service;

        public GlobalOperationRegistration(AbstractGlobalOperationNotificationService service)
            => _service = service;

        public void Dispose()
        {
            // Inform any listeners that we're finished.
            _service.Done(this);
        }
    }
}
