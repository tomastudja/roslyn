﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Tagging
{
    internal partial class AbstractAsynchronousTaggerProvider<TTag>
    {
        private sealed partial class TagSource : ForegroundThreadAffinitizedObject
        {
            /// <summary>
            /// If we get more than this many differences, then we just issue it as a single change
            /// notification.  The number has been completely made up without any data to support it.
            /// 
            /// Internal for testing purposes.
            /// </summary>
            private const int CoalesceDifferenceCount = 10;

            #region Fields that can be accessed from either thread

            private readonly AbstractAsynchronousTaggerProvider<TTag> _dataSource;

            /// <summary>
            /// async operation notifier
            /// </summary>
            private readonly IAsynchronousOperationListener _asyncListener;

            private readonly CancellationTokenSource _disposalTokenSource = new();

            /// <summary>
            /// Work queue that collects event notifications and kicks off the work to process them.
            /// The actual value here is not relevant and will always be <see langword="null"/>.
            /// </summary>
            private readonly AsyncBatchingWorkQueue<object?> _eventWorkQueue;

            /// <summary>
            /// Work queue that collects requests to remove tags and kicks off the work to issue them.
            /// </summary>
            private readonly AsyncBatchingWorkQueue<NormalizedSnapshotSpanCollection> _tagsRemovedWorkQueue;

            /// <summary>
            /// Work queue that collects requests to remove tags and kicks off the work to issue them.
            /// </summary>
            private readonly AsyncBatchingWorkQueue<NormalizedSnapshotSpanCollection> _tagsAddedWorkQueue;

            #endregion

            #region Fields that can only be accessed from the foreground thread

            private readonly ITextView _textViewOpt;
            private readonly ITextBuffer _subjectBuffer;

            /// <summary>
            /// Our tagger event source that lets us know when we should call into the tag producer for
            /// new tags.
            /// </summary>
            private readonly ITaggerEventSource _eventSource;

            /// <summary>
            /// accumulated text changes since last tag calculation
            /// </summary>
            private TextChangeRange? _accumulatedTextChanges_doNotAccessDirectly;
            private ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> _cachedTagTrees_doNotAccessDirectly = ImmutableDictionary.Create<ITextBuffer, TagSpanIntervalTree<TTag>>();
            private object? _state_doNotAccessDirecty;

            /// <summary>
            /// Keep track of if we are processing the first <see cref="ITagger{T}.GetTags"/> request.  If our provider returns 
            /// <see langword="true"/> for <see cref="AbstractAsynchronousTaggerProvider{TTag}.ComputeInitialTagsSynchronously"/>,
            /// then we'll want to synchronously block then and only then for tags.
            /// </summary>
            private bool _firstTagsRequest = true;

            #endregion

            public TagSource(
                ITextView textViewOpt,
                ITextBuffer subjectBuffer,
                AbstractAsynchronousTaggerProvider<TTag> dataSource,
                IAsynchronousOperationListener asyncListener)
                : base(dataSource.ThreadingContext)
            {
                this.AssertIsForeground();
                if (dataSource.SpanTrackingMode == SpanTrackingMode.Custom)
                    throw new ArgumentException("SpanTrackingMode.Custom not allowed.", "spanTrackingMode");

                _subjectBuffer = subjectBuffer;
                _textViewOpt = textViewOpt;
                _dataSource = dataSource;
                _asyncListener = asyncListener;

                _eventWorkQueue = new AsyncBatchingWorkQueue<object?>(
                    _dataSource.EventChangeDelay.ComputeTimeDelay(),
                    ProcessEventsAsync,
                    EqualityComparer<object?>.Default,
                    asyncListener,
                    _disposalTokenSource.Token);

                _tagsRemovedWorkQueue = new AsyncBatchingWorkQueue<NormalizedSnapshotSpanCollection>(
                    TaggerDelay.NearImmediate.ComputeTimeDelay(),
                    ProcessTagsRemovedAsync,
                    equalityComparer: null,
                    asyncListener,
                    _disposalTokenSource.Token);

                _tagsAddedWorkQueue = new AsyncBatchingWorkQueue<NormalizedSnapshotSpanCollection>(
                    _dataSource.AddedTagNotificationDelay.ComputeTimeDelay(),
                    ProcessTagsAddedAsync,
                    equalityComparer: null,
                    asyncListener,
                    _disposalTokenSource.Token);

                DebugRecordInitialStackTrace();

                _eventSource = CreateEventSource();

                Connect();

                // Start computing the initial set of tags immediately.  We want to get the UI
                // to a complete state as soon as possible.
                _eventWorkQueue.AddWork(item: null);
            }

            private ITaggerEventSource CreateEventSource()
            {
                var eventSource = _dataSource.CreateEventSource(_textViewOpt, _subjectBuffer);

                // If there are any options specified for this tagger, then also hook up event
                // notifications for when those options change.
                var optionChangedEventSources =
                    _dataSource.Options.Concat<IOption>(_dataSource.PerLanguageOptions)
                        .Select(o => TaggerEventSources.OnOptionChanged(_subjectBuffer, o)).ToList();

                if (optionChangedEventSources.Count == 0)
                {
                    // No options specified for this tagger.  So just keep the event source as is.
                    return eventSource;
                }

                optionChangedEventSources.Add(eventSource);
                return TaggerEventSources.Compose(optionChangedEventSources);
            }

            private TextChangeRange? AccumulatedTextChanges
            {
                get
                {
                    this.AssertIsForeground();
                    return _accumulatedTextChanges_doNotAccessDirectly;
                }

                set
                {
                    this.AssertIsForeground();
                    _accumulatedTextChanges_doNotAccessDirectly = value;
                }
            }

            private ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> CachedTagTrees
            {
                get
                {
                    this.AssertIsForeground();
                    return _cachedTagTrees_doNotAccessDirectly;
                }

                set
                {
                    this.AssertIsForeground();
                    _cachedTagTrees_doNotAccessDirectly = value;
                }
            }

            private object? State
            {
                get
                {
                    this.AssertIsForeground();
                    return _state_doNotAccessDirecty;
                }

                set
                {
                    this.AssertIsForeground();
                    _state_doNotAccessDirecty = value;
                }
            }

            private void Connect()
            {
                this.AssertIsForeground();

                _eventSource.Changed += OnEventSourceChanged;

                if (_dataSource.TextChangeBehavior.HasFlag(TaggerTextChangeBehavior.TrackTextChanges))
                {
                    _subjectBuffer.Changed += OnSubjectBufferChanged;
                }

                if (_dataSource.CaretChangeBehavior.HasFlag(TaggerCaretChangeBehavior.RemoveAllTagsOnCaretMoveOutsideOfTag))
                {
                    if (_textViewOpt == null)
                    {
                        throw new ArgumentException(
                            nameof(_dataSource.CaretChangeBehavior) + " can only be specified for an " + nameof(IViewTaggerProvider));
                    }

                    _textViewOpt.Caret.PositionChanged += OnCaretPositionChanged;
                }

                // Tell the interaction object to start issuing events.
                _eventSource.Connect();
            }

            private void Disconnect()
            {
                _workQueue.AssertIsForeground();
                _workQueue.CancelCurrentWork(remainCancelled: true);

                // Tell the interaction object to stop issuing events.
                _eventSource.Disconnect();

                if (_dataSource.CaretChangeBehavior.HasFlag(TaggerCaretChangeBehavior.RemoveAllTagsOnCaretMoveOutsideOfTag))
                {
                    _textViewOpt.Caret.PositionChanged -= OnCaretPositionChanged;
                }

                if (_dataSource.TextChangeBehavior.HasFlag(TaggerTextChangeBehavior.TrackTextChanges))
                {
                    _subjectBuffer.Changed -= OnSubjectBufferChanged;
                }

                _eventSource.Changed -= OnEventSourceChanged;
            }

            private void RaiseTagsChanged(ITextBuffer buffer, DiffResult difference)
            {
                this.AssertIsForeground();
                if (difference.Count == 0)
                {
                    // nothing changed.
                    return;
                }

                OnTagsChangedForBuffer(SpecializedCollections.SingletonCollection(
                    new KeyValuePair<ITextBuffer, DiffResult>(buffer, difference)),
                    initialTags: false);
            }
        }
    }
}
