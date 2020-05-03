﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CodeAnalysis.Editor.Host;

namespace Microsoft.CodeAnalysis.ExternalAccess.VSTypeScript.Api
{
    internal enum VSTypeScriptWaitIndicatorResult
    {
        Canceled = WaitIndicatorResult.Canceled,
        Completed = WaitIndicatorResult.Completed
    }
}
