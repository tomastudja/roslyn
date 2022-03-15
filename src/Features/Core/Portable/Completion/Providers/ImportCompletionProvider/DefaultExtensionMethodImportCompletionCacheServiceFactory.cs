﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.CodeAnalysis.Completion.Providers
{
    /// <summary>
    /// We don't use PE cache from the service, so just pass in type `object` for PE entries.
    /// </summary>
    [ExportWorkspaceServiceFactory(typeof(IImportCompletionCacheService<ExtensionMethodImportCompletionCacheEntry, object>), ServiceLayer.Default), Shared]
    internal sealed class DefaultExtensionMethodImportCompletionCacheServiceFactory : AbstractImportCompletionCacheServiceFactory<ExtensionMethodImportCompletionCacheEntry, object>
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public DefaultExtensionMethodImportCompletionCacheServiceFactory() : base(CancellationToken.None)
        {
        }
    }
}
