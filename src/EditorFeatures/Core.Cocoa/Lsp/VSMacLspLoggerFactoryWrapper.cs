﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.ExternalAccess.VSMac.API;
using StreamJsonRpc;

namespace Microsoft.CodeAnalysis.EditorFeatures.Cocoa.Lsp;

/// <summary>
/// Wraps the external access <see cref="IVSMacLspLoggerFactory"/> and exports it
/// as an <see cref="IRoslynLspLoggerFactory"/> for inclusion in the vsmac composition.
/// </summary>
[Export(typeof(IRoslynLspLoggerFactory)), Shared]
internal class VSMacLspLoggerFactoryWrapper : IRoslynLspLoggerFactory
{
    private readonly IVSMacLspLoggerFactory _loggerFactory;

    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public VSMacLspLoggerFactoryWrapper(IVSMacLspLoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public async Task<IRoslynLspLogger> CreateLoggerAsync(string serverTypeName, JsonRpc jsonRpc, CancellationToken cancellationToken)
    {
        var vsMacLogger = await _loggerFactory.CreateLoggerAsync(serverTypeName, jsonRpc, cancellationToken).ConfigureAwait(false);
        return new VSMacLspLoggerWrapper(vsMacLogger);
    }
}

internal class VSMacLspLoggerWrapper : IRoslynLspLogger
{
    private readonly IVSMacLspLogger _logger;

    public VSMacLspLoggerWrapper(IVSMacLspLogger logger)
    {
        _logger = logger;
    }

    public Task LogErrorAsync(string message, params object[] @params)
    {
        _logger.TraceError(message);

        return Task.CompletedTask;
    }

    public Task LogExceptionAsync(Exception exception, string? message = null, params object[] @params)
    {
        _logger.TraceException(exception);
        return Task.CompletedTask;
    }

    public Task LogInformationAsync(string message, params object[] @params)
    {
        _logger.TraceInformation(message);
        return Task.CompletedTask;
    }

    public Task LogStartContextAsync(string message, params object[] @params)
    {
        _logger.TraceStart(message);
        return Task.CompletedTask;
    }

    public Task LogEndContextAsync(string message, params object[] @params)
    {
        _logger.TraceStop(message);
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message, params object[] @params)
    {
        _logger.TraceWarning(message);
        return Task.CompletedTask;
    }
}
