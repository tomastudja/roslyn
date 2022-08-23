﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CommonLanguageServerProtocol.Framework;

/// <summary>
/// A placeholder type to help handle Notification messages.
/// </summary>
internal record VoidReturn
{
    public static VoidReturn Instance = new();
}

internal class QueueItem<TRequestType, TResponseType, RequestContextType> : IQueueItem<RequestContextType>
{
    private readonly ILspLogger _logger;

    private readonly TRequestType _request;
    private readonly IMethodHandler _handler;

    /// <summary>
    /// A task completion source representing the result of this queue item's work.
    /// This is the task that the client is waiting on.
    /// </summary>
    private readonly TaskCompletionSource<TResponseType> _completionSource = new();

    public bool MutatesDocumentState { get; }

    public string MethodName { get; }

    public object? TextDocument { get; }

    public QueueItem(
        bool mutatesSolutionState,
        string methodName,
        object? textDocument,
        TRequestType request,
        IMethodHandler handler,
        ILspLogger logger,
        CancellationToken cancellationToken)
    {
        // Set the tcs state to cancelled if the token gets cancelled outside of our callback (for example the server shutting down).
        cancellationToken.Register(() => _completionSource.TrySetCanceled(cancellationToken));

        _handler = handler;
        _logger = logger;
        _request = request;

        MutatesDocumentState = mutatesSolutionState;
        MethodName = methodName;
        TextDocument = textDocument;
    }

    public static (IQueueItem<RequestContextType>, Task<TResponseType>) Create(
        bool mutatesSolutionState,
        string methodName,
        object? textDocument,
        TRequestType request,
        IMethodHandler handler,
        ILspLogger logger,
        CancellationToken cancellationToken)
    {
        var queueItem = new QueueItem<TRequestType, TResponseType, RequestContextType>(
            mutatesSolutionState,
            methodName,
            textDocument,
            request,
            handler,
            logger,
            cancellationToken);

        return (queueItem, queueItem._completionSource.Task);
    }

    /// <summary>
    /// Processes the queued request. Exceptions will be sent to the task completion source
    /// representing the task that the client is waiting for, then re-thrown so that
    /// the queue can correctly handle them depending on the type of request.
    /// </summary>
    /// <param name="context">The context for the request. If null the request will return emediatly. The context may be null when for example document context could not be resolved.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The result of the request.</returns>
    public async Task StartRequestAsync(RequestContextType? context, CancellationToken cancellationToken)
    {
        await _logger.LogStartContextAsync($"{MethodName}", cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context is null)
            {
                // If we weren't able to get a corresponding context for this request (for example, we
                // couldn't map a doc request to a particular Document, or we couldn't find an appropriate
                // Workspace for a global operation), then just immediately complete the request with a
                // 'null' response.  Note: the lsp spec was checked to ensure that 'null' is valid for all
                // the requests this could happen for.  However, this assumption may not hold in the future.
                // If that turns out to be the case, we could defer to the individual handler to decide
                // what to do.
                await _logger.LogWarningAsync($"Could not get request context for {MethodName}", cancellationToken).ConfigureAwait(false);
                _completionSource.TrySetException(new InvalidOperationException($"Unable to create request context for {MethodName}"));
            }
            else
            {
                if (_handler is IRequestHandler<TRequestType, TResponseType, RequestContextType> requestHandler)
                {
                    var result = await requestHandler.HandleRequestAsync(_request, context, cancellationToken).ConfigureAwait(false);

                    _completionSource.TrySetResult(result);
                }
                else if (_handler is INotificationHandler<TRequestType, RequestContextType> notificationHandler)
                {
                    await notificationHandler.HandleNotificationAsync(_request, context, cancellationToken).ConfigureAwait(false);

                    _completionSource.TrySetResult((TResponseType)(object)VoidReturn.Instance);
                }
                else if (_handler is INotificationHandler<RequestContextType> parameterlessNotificationHandler)
                {
                    await parameterlessNotificationHandler.HandleNotificationAsync(context, cancellationToken).ConfigureAwait(false);

                    _completionSource.TrySetResult((TResponseType)(object)VoidReturn.Instance);
                }
                else
                {
                    throw new NotImplementedException($"Unrecognized {nameof(IMethodHandler)} implementation {_handler.GetType().Name}");
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            // Record logs + metrics on cancellation.
            await _logger.LogInformationAsync($"{MethodName} - Canceled", cancellationToken).ConfigureAwait(false);

            _completionSource.TrySetCanceled(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            // Record logs and metrics on the exception.
            // It's important that this can NEVER throw, or the queue will hang.
            await _logger.LogExceptionAsync(ex, cancellationToken: cancellationToken).ConfigureAwait(false);

            _completionSource.TrySetException(ex);
        }
        finally
        {
            await _logger.LogEndContextAsync($"{MethodName}", cancellationToken).ConfigureAwait(false);
        }

        // Return the result of this completion source to the caller
        // so it can decide how to handle the result / exception.
        await _completionSource.Task.ConfigureAwait(false);
    }
}
