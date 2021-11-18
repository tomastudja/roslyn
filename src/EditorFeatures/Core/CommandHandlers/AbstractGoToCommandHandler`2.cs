﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Editor.Tagging;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CommandHandlers;

internal abstract class AbstractGoToCommandHandler<TLanguageService, TCommandArgs> : ICommandHandler<TCommandArgs>
    where TLanguageService : class, ILanguageService
    where TCommandArgs : EditorCommandArgs
{
    private readonly IThreadingContext _threadingContext;
    private readonly IStreamingFindUsagesPresenter _streamingPresenter;
    private readonly IUIThreadOperationExecutor _uiThreadOperationExecutor;
    private readonly IAsynchronousOperationListener _listener;

    /// <summary>
    /// The current go-to command that is in progress.  Tracked so that if we issue multiple find-impl commands that
    /// they properly run after each other.  This is necessary so none of them accidentally stomp on one that is
    /// still in progress and is interacting with the UI.  Only valid to read or write to this on the UI thread.
    /// </summary>
    private Task _inProgressCommand = Task.CompletedTask;

    /// <summary>
    /// CancellationToken governing the current <see cref="_inProgressCommand"/>.  Only valid to read or write to
    /// this on the UI thread.
    /// </summary>
    /// <remarks>
    /// Cancellation is complicated with this feature.  There are two things that can cause us to cancel.  The first
    /// is if the user kicks off another actual go-to-impl command.  In that case, we just attempt to cancel the
    /// prior command (if it is still running), then wait for it to complete, then run our command.  The second is
    /// if we have switched over to the streaming presenter and then the user starts some other command (like FAR)
    /// that takes over the presenter.  In that case, the presenter will notify us that it has be repurposed and we
    /// will also cancel this source.
    /// </remarks>
    private CancellationTokenSource _cancellationTokenSource = new();

    public AbstractGoToCommandHandler(
        IThreadingContext threadingContext,
        IStreamingFindUsagesPresenter streamingPresenter,
        IUIThreadOperationExecutor uiThreadOperationExecutor,
        IAsynchronousOperationListener listener)
    {
        _threadingContext = threadingContext;
        _streamingPresenter = streamingPresenter;
        _uiThreadOperationExecutor = uiThreadOperationExecutor;
        _listener = listener;
    }

    public abstract string DisplayName { get; }
    protected abstract string ScopeDescription { get; }
    protected abstract FunctionId FunctionId { get; }

    protected abstract Task FindActionAsync(Document document, int caretPosition, IFindUsagesContext context, CancellationToken cancellationToken);

    private static TLanguageService? GetService(ITextBuffer buffer)
    {
        var document = buffer.AsTextContainer().GetOpenDocumentInCurrentContext();
        return document?.GetLanguageService<TLanguageService>();
    }

    public CommandState GetCommandState(TCommandArgs args)
    {
        var service = GetService(args.SubjectBuffer);
        return service != null
            ? CommandState.Available
            : CommandState.Unspecified;
    }

    public bool ExecuteCommand(TCommandArgs args, CommandExecutionContext context)
    {
        // Should only be called on the UI thread.
        Contract.ThrowIfFalse(_threadingContext.HasMainThread);

        var subjectBuffer = args.SubjectBuffer;
        var caret = args.TextView.GetCaretPoint(subjectBuffer);
        if (!caret.HasValue)
            return false;

        var document = subjectBuffer.AsTextContainer().GetOpenDocumentInCurrentContext();
        if (document == null)
            return false;

        var service = GetService(subjectBuffer);
        if (service == null)
            return false;

        var position = caret.Value.Position;
        var snapshot = subjectBuffer.CurrentSnapshot;

        // cancel any prior find-refs that might be in progress.
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource = new();

        // we're going to return immediately from ExecuteCommand and kick off our own async work to invoke the
        // operation. Once this returns, the editor will close the threaded wait dialog it created.
        _inProgressCommand = ExecuteCommandAsync(document.Project.Solution.Workspace, snapshot, position, _cancellationTokenSource);
        return true;
    }

    private async Task ExecuteCommandAsync(
        Workspace workspace,
        ITextSnapshot snapshot,
        int position,
        CancellationTokenSource cancellationTokenSource)
    {
        // Should only be called on the UI thread.
        Contract.ThrowIfFalse(_threadingContext.HasMainThread);
        try
        {
            // Make an tracking token so that integration tests can wait until we're complete.
            using var token = _listener.BeginAsyncOperation($"{this.GetType().Name}.{nameof(ExecuteCommandAsync)}");

            // Only start running once the previous command has finished.  That way we don't have results from both
            // potentially interleaving with each other.  Note: this should ideally always be fast as long as the
            // prior task respects cancellation.
            await _inProgressCommand.ConfigureAwait(false);
            await this.ExecuteCommandWorkerAsync(workspace, snapshot, position, cancellationTokenSource).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (FatalError.ReportAndCatch(ex))
        {
        }
    }

    private async Task ExecuteCommandWorkerAsync(
        Workspace workspace,
        ITextSnapshot textSnapshot,
        int position,
        CancellationTokenSource cancellationTokenSource)
    {
        // Switch to the BG immediately so we can keep as much work off the UI thread.
        await TaskScheduler.Default;

        // We kick off the work to find the impl/base in the bg.  If we get the results for it within 1.5 seconds,
        // we then either navigate directly to it (in the case of one result), or we show all the results in the
        // presenter (in the case of multiple).
        //
        // However, if the results don't come back in 1.5 seconds, we just pop open the presenter and continue the
        // search there.  That way the user is not blocked and can go do other work if they want.

        // We create our own context object, simply to capture all the definitions reported by the individual
        // TLanguageService.  Once we get the results back we'll then decide what to do with them.  If we get only a
        // single result back, then we'll just go directly to it.  Otherwise, we'll present the results in the
        // IStreamingFindUsagesPresenter.
        var findContext = new SwappableFindUsagesContext();

        var cancellationToken = cancellationTokenSource.Token;
        var delayTask = Task.Delay(TaggerDelay.OnIdle.ComputeTimeDelay(), cancellationToken);
        var findTask = Task.Run(() => FindResultsAsync(workspace, textSnapshot, position, findContext, cancellationToken), cancellationToken);

        var firstFinishedTask = await Task.WhenAny(delayTask, findTask).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            // we bailed out because another command was issued.  Immediately stop everything we're doing and return
            // back so the next operation can run.
            return;
        }

        if (firstFinishedTask == findTask)
        {
            // We completed the search within 1.5 seconds.  If we had at least one result then Navigate to it directly
            // (if there is just one) or present them all if there are many.
            var definitions = await findContext.GetDefinitionsAsync(cancellationToken).ConfigureAwait(false);
            if (definitions.Length > 0)
            {
                var title = await findContext.GetSearchTitleAsync(cancellationToken).ConfigureAwait(false);
                await _streamingPresenter.TryNavigateToOrPresentItemsAsync(
                    _threadingContext,
                    workspace,
                    title ?? this.DisplayName,
                    definitions,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        // We either got no results, or 1.5 has passed and we didn't figure out the symbols to navigate to or
        // present.  So pop up the presenter to show the user that we're involved in a longer search, without
        // blocking them.
        await PresentResultsInStreamingPresenterAsync(cancellationTokenSource, findContext, findTask, cancellationToken).ConfigureAwait(false);
    }

    private async Task PresentResultsInStreamingPresenterAsync(
        CancellationTokenSource cancellationTokenSource,
        SwappableFindUsagesContext findContext,
        Task findTask,
        CancellationToken cancellationToken)
    {
        await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        var (presenterContext, presenterCancellationToken) = _streamingPresenter.StartSearch(this.DisplayName, supportsReferences: false);

        try
        {
            await TaskScheduler.Default;

            // Now, tell our find-context (which has been collecting intermediary results) to swap over to using the
            // actual presenter context.  It will push all results it's been collecting into that, and from that
            // point onwards will just forward any new results directly to the presenter.
            await findContext.SwapAsync(presenterContext, cancellationToken).ConfigureAwait(false);

            // Hook up the presenter's cancellation token to our overall governing cancellation token.  In other
            // words, if something else decides to present in the presenter (like a find-refs call) we'll hear about
            // that and can cancel all our work.
            presenterCancellationToken.Register(() => cancellationTokenSource.Cancel());

            // now actuall wait for the find work to be done.
            await findTask.ConfigureAwait(false);
        }
        finally
        {
            // Ensure that once we pop up the presenter, we always make sure to force it to the completed stage in
            // case some other find operation happens (either through this handler or another handler using the
            // presenter) and we don't actually finish the search.
            await presenterContext.OnCompletedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task FindResultsAsync(
        Workspace workspace, ITextSnapshot textSnapshot, int position, IFindUsagesContext findContext, CancellationToken cancellationToken)
    {
        using (Logger.LogBlock(FunctionId, KeyValueLogMessage.Create(LogType.UserAction), cancellationToken))
        {
            // Let the user know in the FAR window if this is taking a long time because we're waiting for the
            // solution to be ready.
            var isFullyLoaded = await workspace.Services.GetRequiredService<IWorkspaceStatusService>().IsFullyLoadedAsync(cancellationToken).ConfigureAwait(false);
            if (!isFullyLoaded)
            {
                await findContext.ReportInformationalMessageAsync(
                    EditorFeaturesResources.The_results_may_be_incomplete_due_to_the_solution_still_loading_projects, cancellationToken).ConfigureAwait(false);
            }

            await findContext.SetSearchTitleAsync(this.DisplayName, cancellationToken).ConfigureAwait(false);

            var document = textSnapshot.GetOpenDocumentInCurrentContextWithChanges();

            // We were able to find the doc prior to loading the workspace (or else we would not have the service).
            // So we better be able to find it afterwards.
            Contract.ThrowIfNull(document);
            await FindActionAsync(document, position, findContext, cancellationToken).ConfigureAwait(false);
        }
    }
}
