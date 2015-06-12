// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Options;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.RenameTracking
{
    internal sealed partial class RenameTrackingTaggerProvider
    {
        /// <summary>
        /// Keeps track of the rename tracking state for a given text buffer by tracking its
        /// changes over time.
        /// </summary>
        private class StateMachine : ForegroundThreadAffinitizedObject
        {
            private readonly IInlineRenameService _inlineRenameService;
            private readonly IAsynchronousOperationListener _asyncListener;
            private readonly ITextBuffer _buffer;
            private readonly IDiagnosticAnalyzerService _diagnosticAnalyzerService;

            private int _refCount;

            public TrackingSession TrackingSession { get; private set; }
            public ITextBuffer Buffer { get { return _buffer; } }

            public event Action TrackingSessionUpdated = delegate { };
            public event Action<ITrackingSpan> TrackingSessionCleared = delegate { };

            public StateMachine(
                ITextBuffer buffer, 
                IInlineRenameService inlineRenameService, 
                IAsynchronousOperationListener asyncListener, 
                IDiagnosticAnalyzerService diagnosticAnalyzerService)
            {
                _buffer = buffer;
                _buffer.Changed += Buffer_Changed;
                _inlineRenameService = inlineRenameService;
                _asyncListener = asyncListener;
                _diagnosticAnalyzerService = diagnosticAnalyzerService;
            }

            private void Buffer_Changed(object sender, TextContentChangedEventArgs e)
            {
                AssertIsForeground();

                if (!_buffer.GetOption(InternalFeatureOnOffOptions.RenameTracking))
                {
                    // When disabled, ignore all text buffer changes and do not trigger retagging
                    return;
                }

                using (Logger.LogBlock(FunctionId.Rename_Tracking_BufferChanged, CancellationToken.None))
                {
                    // When the buffer changes, several things might be happening:
                    // 1. If a non-identifier character has been added or deleted, we stop tracking
                    //    completely.
                    // 2. Otherwise, if the changes are completely contained an existing session, then
                    //    continue that session.
                    // 3. Otherwise, we're starting a new tracking session. Find and track the span of
                    //    the relevant word in the foreground, and use a task to figure out whether the
                    //    original word was a renamable identifier or not.

                    if (e.Changes.Count != 1 || ShouldClearTrackingSession(e.Changes.Single()))
                    {
                        ClearTrackingSession();
                        return;
                    }

                    // The change is trackable. Figure out whether we should continue an existing
                    // session

                    var change = e.Changes.Single();

                    if (this.TrackingSession == null)
                    {
                        StartTrackingSession(e);
                        return;
                    }

                    // There's an existing session. Continue that session if the current change is
                    // contained inside the tracking span.

                    SnapshotSpan trackingSpanInNewSnapshot = this.TrackingSession.TrackingSpan.GetSpan(e.After);
                    if (trackingSpanInNewSnapshot.Contains(change.NewSpan))
                    {
                        // Continuing an existing tracking session. If there may have been a tag
                        // showing, then update the tags.
                        UpdateTrackingSessionIfRenamable();
                    }
                    else
                    {
                        StartTrackingSession(e);
                    }
                }
            }

            public void UpdateTrackingSessionIfRenamable()
            {
                AssertIsForeground();
                if (this.TrackingSession.IsDefinitelyRenamableIdentifier())
                {
                    this.TrackingSession.CheckNewIdentifier(this, _buffer.CurrentSnapshot);
                    TrackingSessionUpdated();
                }
            }

            private bool ShouldClearTrackingSession(ITextChange change)
            {
                AssertIsForeground();
                ISyntaxFactsService syntaxFactsService;
                if (!TryGetSyntaxFactsService(out syntaxFactsService))
                {
                    return true;
                }

                // The editor will replace virtual space with spaces and/or tabs when typing on a 
                // previously blank line. Trim these characters from the start of change.NewText. If 
                // the resulting change is empty (the user just typed a <space>), clear the session.
                var changedText = change.OldText + change.NewText.TrimStart(' ', '\t');
                if (changedText.IsEmpty())
                {
                    return true;
                }

                return changedText.Any(c => !IsTrackableCharacter(syntaxFactsService, c));
            }

            private void StartTrackingSession(TextContentChangedEventArgs eventArgs)
            {
                AssertIsForeground();
                ClearTrackingSession();

                if (_inlineRenameService.ActiveSession != null)
                {
                    return;
                }

                // Synchronously find the tracking span in the old document.

                var change = eventArgs.Changes.Single();
                var beforeText = eventArgs.Before.AsText();

                ISyntaxFactsService syntaxFactsService;
                if (!TryGetSyntaxFactsService(out syntaxFactsService))
                {
                    return;
                }

                int leftSidePosition = change.OldPosition;
                int rightSidePosition = change.OldPosition + change.OldText.Length;

                while (leftSidePosition > 0 && IsTrackableCharacter(syntaxFactsService, beforeText[leftSidePosition - 1]))
                {
                    leftSidePosition--;
                }

                while (rightSidePosition < beforeText.Length && IsTrackableCharacter(syntaxFactsService, beforeText[rightSidePosition]))
                {
                    rightSidePosition++;
                }

                var originalSpan = new Span(leftSidePosition, rightSidePosition - leftSidePosition);
                this.TrackingSession = new TrackingSession(this, new SnapshotSpan(eventArgs.Before, originalSpan), _asyncListener);
            }

            private bool IsTrackableCharacter(ISyntaxFactsService syntaxFactsService, char c)
            {
                // Allow identifier part characters at the beginning of strings (even if they are
                // not identifier start characters). If an intermediate name is not valid, the smart
                // tag will not be shown due to later checks. Also allow escape chars anywhere as
                // they might be in the middle of a complex edit.
                return syntaxFactsService.IsIdentifierPartCharacter(c) || syntaxFactsService.IsIdentifierEscapeCharacter(c);
            }

            public bool ClearTrackingSession()
            {
                AssertIsForeground();

                if (this.TrackingSession != null)
                {
                    // Disallow the existing TrackingSession from triggering IdentifierFound.
                    var previousTrackingSession = this.TrackingSession;
                    this.TrackingSession = null;

                    previousTrackingSession.Cancel();

                    // If there may have been a tag showing, then actually clear the tags.
                    if (previousTrackingSession.IsDefinitelyRenamableIdentifier())
                    {
                        TrackingSessionCleared(previousTrackingSession.TrackingSpan);
                    }

                    return true;
                }

                return false;
            }

            public bool ClearVisibleTrackingSession()
            {
                AssertIsForeground();

                if (this.TrackingSession != null && this.TrackingSession.IsDefinitelyRenamableIdentifier())
                {
                    var document = _buffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
                    if (document != null)
                    {
                        // When rename tracking is dismissed via escape, we no longer wish to
                        // provide a diagnostic/codefix, but nothing has changed in the workspace
                        // to trigger the diagnostic system to reanalyze, so we trigger it 
                        // manually.

                        _diagnosticAnalyzerService?.Reanalyze(
                            document.Project.Solution.Workspace, 
                            documentIds: SpecializedCollections.SingletonEnumerable(document.Id));
                    }

                    // Disallow the existing TrackingSession from triggering IdentifierFound.
                    var previousTrackingSession = this.TrackingSession;
                    this.TrackingSession = null;

                    previousTrackingSession.Cancel();
                    TrackingSessionCleared(previousTrackingSession.TrackingSpan);
                    return true;
                }

                return false;
            }

            public bool CanInvokeRename(out TrackingSession trackingSession, bool isSmartTagCheck = false, bool waitForResult = false, CancellationToken cancellationToken = default(CancellationToken))
            {
                // This needs to be able to run on a background thread for the diagnostic.

                trackingSession = this.TrackingSession;
                if (trackingSession == null)
                {
                    return false;
                }

                ISyntaxFactsService syntaxFactsService;
                IRenameTrackingLanguageHeuristicsService languageHeuristicsService;
                return TryGetSyntaxFactsService(out syntaxFactsService) && TryGetLanguageHeuristicsService(out languageHeuristicsService) &&
                    trackingSession.CanInvokeRename(syntaxFactsService, languageHeuristicsService, isSmartTagCheck, waitForResult, cancellationToken);
            }

            internal async Task<IEnumerable<Diagnostic>> GetDiagnostic(SyntaxTree tree, DiagnosticDescriptor diagnosticDescriptor, CancellationToken cancellationToken)
            {
                try
                {
                    // This can be called on a background thread. We are being asked whether a 
                    // lightbulb should be shown for the given document, but we only know about the 
                    // current state of the buffer. Compare the text to see if we should bail early.
                    // Even if the text is the same, the buffer may change on the UI thread during this
                    // method. If it does, we may give an incorrect response, but the diagnostics 
                    // engine will know that the document changed and not display the lightbulb anyway.

                    if (Buffer.AsTextContainer().CurrentText != await tree.GetTextAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return SpecializedCollections.EmptyEnumerable<Diagnostic>();
                    }

                    TrackingSession trackingSession;
                    if (CanInvokeRename(out trackingSession, waitForResult: true, cancellationToken: cancellationToken))
                    {
                        SnapshotSpan snapshotSpan = trackingSession.TrackingSpan.GetSpan(Buffer.CurrentSnapshot);
                        var textSpan = snapshotSpan.Span.ToTextSpan();

                        var builder = ImmutableDictionary.CreateBuilder<string, string>();
                        builder.Add(RenameTrackingDiagnosticAnalyzer.RenameFromPropertyKey, trackingSession.OriginalName);
                        builder.Add(RenameTrackingDiagnosticAnalyzer.RenameToPropertyKey, snapshotSpan.GetText());
                        var properties = builder.ToImmutable();

                        var diagnostic = Diagnostic.Create(diagnosticDescriptor,
                            tree.GetLocation(textSpan),
                            properties);

                        return SpecializedCollections.SingletonEnumerable(diagnostic);
                    }

                    return SpecializedCollections.EmptyEnumerable<Diagnostic>();
                }
                catch (Exception e) when (FatalError.ReportUnlessCanceled(e))
                {
                    throw ExceptionUtilities.Unreachable;
                }
            }

            public void RestoreTrackingSession(TrackingSession trackingSession)
            {
                AssertIsForeground();
                ClearTrackingSession();

                this.TrackingSession = trackingSession;
                TrackingSessionUpdated();
            }

            public void OnTrackingSessionUpdated(TrackingSession trackingSession)
            {
                AssertIsForeground();

                if (this.TrackingSession == trackingSession)
                {
                    TrackingSessionUpdated();
                }
            }

            private bool TryGetSyntaxFactsService(out ISyntaxFactsService syntaxFactsService)
            {
                // Can be called on a background thread

                syntaxFactsService = null;
                var document = _buffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
                if (document != null)
                {
                    syntaxFactsService = document.Project.LanguageServices.GetService<ISyntaxFactsService>();
                }

                return syntaxFactsService != null;
            }

            private bool TryGetLanguageHeuristicsService(out IRenameTrackingLanguageHeuristicsService languageHeuristicsService)
            {
                // Can be called on a background thread

                languageHeuristicsService = null;
                var document = _buffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
                if (document != null)
                {
                    languageHeuristicsService = document.Project.LanguageServices.GetService<IRenameTrackingLanguageHeuristicsService>();
                }

                return languageHeuristicsService != null;
            }

            public void Connect()
            {
                AssertIsForeground();
                _refCount++;
            }

            public void Disconnect()
            {
                AssertIsForeground();
                _refCount--;
                Contract.ThrowIfFalse(_refCount >= 0);

                if (_refCount == 0)
                {
                    this.Buffer.Properties.RemoveProperty(typeof(StateMachine));
                    this.Buffer.Changed -= Buffer_Changed;
                }
            }
        }
    }
}
