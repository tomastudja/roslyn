// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.RenameTracking
{
    internal sealed partial class RenameTrackingTaggerProvider
    {
        internal enum TriggerIdentifierKind
        {
            NotRenamable,
            RenamableDeclaration,
            RenamableReference,
        }

        /// <summary>
        /// Determines whether the original token was a renameable identifier on a background thread
        /// </summary>
        private class TrackingSession : ForegroundThreadAffinitizedObject
        {
            private static readonly Task<TriggerIdentifierKind> s_notRenamableTask = Task.FromResult(TriggerIdentifierKind.NotRenamable);
            private readonly Task<TriggerIdentifierKind> _isRenamableIdentifierTask;
            private readonly CancellationTokenSource _cancellationTokenSource;
            private readonly CancellationToken _cancellationToken;
            private readonly IAsynchronousOperationListener _asyncListener;

            private Task<bool> _newIdentifierBindsTask = SpecializedTasks.False;

            private readonly string _originalName;
            public string OriginalName { get { return _originalName; } }

            private readonly ITrackingSpan _trackingSpan;
            public ITrackingSpan TrackingSpan { get { return _trackingSpan; } }

            private bool _forceRenameOverloads;
            public bool ForceRenameOverloads { get { return _forceRenameOverloads; } }

            public TrackingSession(StateMachine stateMachine, SnapshotSpan snapshotSpan, IAsynchronousOperationListener asyncListener)
            {
                AssertIsForeground();

                _asyncListener = asyncListener;
                _trackingSpan = snapshotSpan.Snapshot.CreateTrackingSpan(snapshotSpan.Span, SpanTrackingMode.EdgeInclusive);
                _cancellationTokenSource = new CancellationTokenSource();
                _cancellationToken = _cancellationTokenSource.Token;

                if (snapshotSpan.Length > 0)
                {
                    // If the snapshotSpan is nonempty, then the session began with a change that
                    // was touching a word. Asynchronously determine whether that word was a
                    // renameable identifier. If it is, alert the state machine so it can trigger
                    // tagging.

                    _originalName = snapshotSpan.GetText();
                    _isRenamableIdentifierTask = Task.Factory.SafeStartNewFromAsync(
                        () => DetermineIfRenamableIdentifierAsync(snapshotSpan, initialCheck: true),
                        _cancellationToken,
                        TaskScheduler.Default);

                    var asyncToken = _asyncListener.BeginAsyncOperation(GetType().Name + ".UpdateTrackingSessionAfterIsRenamableIdentifierTask");

                    _isRenamableIdentifierTask.SafeContinueWith(
                        t => stateMachine.UpdateTrackingSessionIfRenamable(),
                        _cancellationToken,
                       TaskContinuationOptions.OnlyOnRanToCompletion,
                       ForegroundTaskScheduler).CompletesAsyncOperation(asyncToken);

                    QueueUpdateToStateMachine(stateMachine, _isRenamableIdentifierTask);
                }
                else
                {
                    // If the snapshotSpan is empty, that means text was added in a location that is
                    // not touching an existing word, which happens a fair amount when writing new
                    // code. In this case we already know that the user is not renaming an
                    // identifier.

                    _isRenamableIdentifierTask = s_notRenamableTask;
                }
            }

            private void QueueUpdateToStateMachine(StateMachine stateMachine, Task task)
            {
                var asyncToken = _asyncListener.BeginAsyncOperation($"{GetType().Name}.{nameof(QueueUpdateToStateMachine)}");

                task.SafeContinueWith(t =>
                   {
                       AssertIsForeground();
                       if (_isRenamableIdentifierTask.Result != TriggerIdentifierKind.NotRenamable)
                       {
                           stateMachine.OnTrackingSessionUpdated(this);
                       }
                   },
                   _cancellationToken,
                   TaskContinuationOptions.OnlyOnRanToCompletion,
                   ForegroundTaskScheduler).CompletesAsyncOperation(asyncToken);
            }

            internal void CheckNewIdentifier(StateMachine stateMachine, ITextSnapshot snapshot)
            {
                AssertIsForeground();

                _newIdentifierBindsTask = _isRenamableIdentifierTask.SafeContinueWithFromAsync(
                    async t => t.Result != TriggerIdentifierKind.NotRenamable &&
                               TriggerIdentifierKind.RenamableReference ==
                                   await DetermineIfRenamableIdentifierAsync(
                                       TrackingSpan.GetSpan(snapshot),
                                       initialCheck: false).ConfigureAwait(false),
                    _cancellationToken,
                    TaskContinuationOptions.OnlyOnRanToCompletion,
                    TaskScheduler.Default);

                QueueUpdateToStateMachine(stateMachine, _newIdentifierBindsTask);
            }

            internal bool IsDefinitelyRenamableIdentifier()
            {
                // This needs to be able to run on a background thread for the CodeFix
                return IsRenamableIdentifier(_isRenamableIdentifierTask, waitForResult: false, cancellationToken: CancellationToken.None);
            }

            public void Cancel()
            {
                AssertIsForeground();
                _cancellationTokenSource.Cancel();
            }

            private async Task<TriggerIdentifierKind> DetermineIfRenamableIdentifierAsync(SnapshotSpan snapshotSpan, bool initialCheck)
            {
                AssertIsBackground();
                var document = snapshotSpan.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
                if (document != null)
                {
                    var syntaxFactsService = document.Project.LanguageServices.GetService<ISyntaxFactsService>();
                    var syntaxTree = await document.GetSyntaxTreeAsync(_cancellationToken).ConfigureAwait(false);
                    var token = await syntaxTree.GetTouchingWordAsync(snapshotSpan.Start.Position, syntaxFactsService, _cancellationToken).ConfigureAwait(false);

                    // The OriginalName is determined with a simple textual check, so for a
                    // statement such as "Dim [x = 1" the textual check will return a name of "[x".
                    // The token found for "[x" is an identifier token, but only due to error 
                    // recovery (the "[x" is actually in the trailing trivia). If the OriginalName
                    // found through the textual check has a different length than the span of the 
                    // touching word, then we cannot perform a rename.
                    if (initialCheck && token.Span.Length != this.OriginalName.Length)
                    {
                        return TriggerIdentifierKind.NotRenamable;
                    }

                    var languageHeuristicsService = document.Project.LanguageServices.GetService<IRenameTrackingLanguageHeuristicsService>();
                    if (syntaxFactsService.IsIdentifier(token) && languageHeuristicsService.IsIdentifierValidForRenameTracking(token.Text))
                    {
                        var semanticModel = await document.GetSemanticModelForNodeAsync(token.Parent, _cancellationToken).ConfigureAwait(false);
                        var semanticFacts = document.GetLanguageService<ISemanticFactsService>();

                        var renameSymbolInfo = RenameUtilities.GetTokenRenameInfo(semanticFacts, semanticModel, token, _cancellationToken);
                        if (!renameSymbolInfo.HasSymbols)
                        {
                            return TriggerIdentifierKind.NotRenamable;
                        }

                        if (renameSymbolInfo.IsMemberGroup)
                        {
                            // This is a reference from a nameof expression. Allow the rename but set the RenameOverloads option
                            _forceRenameOverloads = true;

                            return await DetermineIfRenamableSymbolsAsync(renameSymbolInfo.Symbols, document, token).ConfigureAwait(false);
                        }
                        else
                        {
                            return await DetermineIfRenamableSymbolAsync(renameSymbolInfo.Symbols.Single(), document, token).ConfigureAwait(false);
                        }
                    }
                }

                return TriggerIdentifierKind.NotRenamable;
            }

            private async Task<TriggerIdentifierKind> DetermineIfRenamableSymbolsAsync(IEnumerable<ISymbol> symbols, Document document, SyntaxToken token)
            {
                foreach (var symbol in symbols)
                {
                    // Get the source symbol if possible
                    var sourceSymbol = await SymbolFinder.FindSourceDefinitionAsync(symbol, document.Project.Solution, _cancellationToken).ConfigureAwait(false) ?? symbol;

                    if (!sourceSymbol.Locations.All(loc => loc.IsInSource))
                    {
                        return TriggerIdentifierKind.NotRenamable;
                    }
                }

                return TriggerIdentifierKind.RenamableReference;
            }

            private async Task<TriggerIdentifierKind> DetermineIfRenamableSymbolAsync(ISymbol symbol, Document document, SyntaxToken token)
            {
                // Get the source symbol if possible
                var sourceSymbol = await SymbolFinder.FindSourceDefinitionAsync(symbol, document.Project.Solution, _cancellationToken).ConfigureAwait(false) ?? symbol;

                if (sourceSymbol.IsImplicitlyDeclared || !sourceSymbol.Locations.All(loc => loc.IsInSource))
                {
                    return TriggerIdentifierKind.NotRenamable;
                }

                return sourceSymbol.Locations.Any(loc => loc == token.GetLocation())
                        ? TriggerIdentifierKind.RenamableDeclaration
                        : TriggerIdentifierKind.RenamableReference;
            }

            internal bool CanInvokeRename(
                ISyntaxFactsService syntaxFactsService,
                IRenameTrackingLanguageHeuristicsService languageHeuristicsService,
                bool isSmartTagCheck,
                bool waitForResult,
                CancellationToken cancellationToken)
            {
                if (IsRenamableIdentifier(_isRenamableIdentifierTask, waitForResult, cancellationToken))
                {
                    var isRenamingDeclaration = _isRenamableIdentifierTask.Result == TriggerIdentifierKind.RenamableDeclaration;
                    var newName = TrackingSpan.GetText(TrackingSpan.TextBuffer.CurrentSnapshot);
                    var comparison = isRenamingDeclaration || syntaxFactsService.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                    if (!string.Equals(OriginalName, newName, comparison) &&
                        syntaxFactsService.IsValidIdentifier(newName) &&
                        languageHeuristicsService.IsIdentifierValidForRenameTracking(newName))
                    {
                        // At this point, we want to allow renaming if the user invoked Ctrl+. explicitly, but we
                        // want to avoid showing a smart tag if we're renaming a reference that binds to an existing
                        // symbol.
                        if (!isSmartTagCheck || isRenamingDeclaration || !NewIdentifierDefinitelyBindsToReference())
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private bool NewIdentifierDefinitelyBindsToReference()
            {
                return _newIdentifierBindsTask.Status == TaskStatus.RanToCompletion && _newIdentifierBindsTask.Result;
            }
        }
    }
}
