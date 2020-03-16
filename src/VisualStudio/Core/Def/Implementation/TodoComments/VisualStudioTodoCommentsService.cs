﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Common;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.TodoComments;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.TodoComments
{
    internal class VisualStudioTodoCommentsService
        : ForegroundThreadAffinitizedObject, ITodoCommentsService, ITodoCommentsServiceCallback, ITodoListProvider
    {
        private readonly VisualStudioWorkspaceImpl _workspace;
        private readonly EventListenerTracker<ITodoListProvider> _eventListenerTracker;

        private readonly ConcurrentDictionary<DocumentId, ImmutableArray<TodoCommentInfo>> _documentToInfos
            = new ConcurrentDictionary<DocumentId, ImmutableArray<TodoCommentInfo>>();

        /// <summary>
        /// Our connections to the remote OOP server. Created on demand when we startup and then
        /// kept around for the lifetime of this service.
        /// </summary>
        private KeepAliveSession? _keepAliveSession;

        /// <summary>
        /// Queue where we enqueue the information we get from OOP to process in batch in the future.
        /// </summary>
        private AsyncBatchingWorkQueue<DocumentAndComments> _workQueue = null!;

        public event EventHandler<TodoItemsUpdatedArgs>? TodoListUpdated;

        public VisualStudioTodoCommentsService(
            VisualStudioWorkspaceImpl workspace,
            IThreadingContext threadingContext,
            EventListenerTracker<ITodoListProvider> eventListenerTracker)
            : base(threadingContext)
        {
            _workspace = workspace;
            _eventListenerTracker = eventListenerTracker;

            _workspace.WorkspaceChanged += OnWorkspaceChanged;
        }

        private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
        {
            if (e.Kind == WorkspaceChangeKind.DocumentRemoved)
                _documentToInfos.TryRemove(e.DocumentId, out _);
        }

        void ITodoCommentsService.Start(CancellationToken cancellationToken)
            => _ = StartAsync(cancellationToken);

        private async Task StartAsync(CancellationToken cancellationToken)
        {
            // Have to catch all exceptions coming through here as this is called from a
            // fire-and-forget method and we want to make sure nothing leaks out.
            try
            {
                await StartWorkerAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is normal (during VS closing).  Just ignore.
            }
            catch (Exception e) when (FatalError.ReportWithoutCrash(e))
            {
                // Otherwise report a watson for any other exception.  Don't bring down VS.  This is
                // a BG service we don't want impacting the user experience.
            }
        }

        private async Task StartWorkerAsync(CancellationToken cancellationToken)
        {
            _workQueue = new AsyncBatchingWorkQueue<DocumentAndComments>(
                TimeSpan.FromSeconds(1),
                ProcessTodoCommentInfosAsync,
                cancellationToken);

            var client = await RemoteHostClient.TryGetClientAsync(_workspace, cancellationToken).ConfigureAwait(false);
            if (client == null)
                return;

            // Pass ourselves in as the callback target for the OOP service.  As it discovers
            // todo comments it will call back into us to notify VS about it.
            _keepAliveSession = await client.TryCreateKeepAliveSessionAsync(
                WellKnownServiceHubServices.RemoteTodoCommentsService,
                callbackTarget: this, cancellationToken).ConfigureAwait(false);
            if (_keepAliveSession == null)
                return;

            // Now that we've started, let the VS todo list know to start listening to us
            _eventListenerTracker.EnsureEventListener(_workspace, this);

            // Now kick off scanning in the OOP process.
            var success = await _keepAliveSession.TryInvokeAsync(
                nameof(IRemoteTodoCommentsService.ComputeTodoCommentsAsync),
                solution: null,
                arguments: Array.Empty<object>(),
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Callback from the OOP service back into us.
        /// </summary>
        public Task ReportTodoCommentsAsync(DocumentId documentId, List<TodoCommentInfo> infos, CancellationToken cancellationToken)
        {
            _workQueue.AddWork(new DocumentAndComments(documentId, infos.ToImmutableArray()));
            return Task.CompletedTask;
        }

        private Task ProcessTodoCommentInfosAsync(
            ImmutableArray<DocumentAndComments> docAndCommentsArray, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var _ = ArrayBuilder<DocumentAndComments>.GetInstance(out var filteredArray);
            AddFilteredInfos(docAndCommentsArray, filteredArray);

            foreach (var docAndComments in filteredArray)
            {
                var documentId = docAndComments.DocumentId;
                var newComments = docAndComments.Comments;

                var oldComments = _documentToInfos.TryGetValue(documentId, out var oldBoxedInfos)
                    ? oldBoxedInfos
                    : ImmutableArray<TodoCommentInfo>.Empty;

                // only one thread can be executing ProcessTodoCommentInfosAsync at a time,
                // so it's safe to remove/add here.
                _documentToInfos[documentId] = newComments;

                // If we have someone listening for updates, and our new items are different from
                // our old ones, then notify them of the change.
                if (this.TodoListUpdated != null && !oldComments.SequenceEqual(newComments))
                {
                    this.TodoListUpdated?.Invoke(
                        this, new TodoItemsUpdatedArgs(
                            documentId, _workspace, _workspace.CurrentSolution,
                            documentId.ProjectId, documentId, newComments));
                }
            }

            return Task.CompletedTask;
        }

        private void AddFilteredInfos(
            ImmutableArray<DocumentAndComments> array,
            ArrayBuilder<DocumentAndComments> filteredArray)
        {
            using var _ = PooledHashSet<DocumentId>.GetInstance(out var seenDocumentIds);

            // Walk the list of todo comments in reverse, and skip any items for a document once
            // we've already seen it once.  That way, we're only reporting the most up to date
            // information for a document, and we're skipping the stale information.
            for (var i = array.Length - 1; i >= 0; i--)
            {
                var info = array[i];
                if (seenDocumentIds.Add(info.DocumentId))
                    filteredArray.Add(info);
            }
        }

        public ImmutableArray<TodoCommentInfo> GetTodoItems(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
        {
            return _documentToInfos.TryGetValue(documentId, out var values)
                ? values
                : ImmutableArray<TodoCommentInfo>.Empty;
        }

        public IEnumerable<UpdatedEventArgs> GetTodoItemsUpdatedEventArgs(
            Workspace workspace, CancellationToken cancellationToken)
        {
            // Don't need to implement this.  OOP pushes all items over to VS.  So there's no need
            return SpecializedCollections.EmptyEnumerable<UpdatedEventArgs>();
        }
    }
}
