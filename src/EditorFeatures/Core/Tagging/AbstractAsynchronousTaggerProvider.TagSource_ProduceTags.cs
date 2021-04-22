﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Internal.Log;
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
        /// <summary>
        /// <para>The <see cref="TagSource"/> is the core part of our asynchronous
        /// tagging infrastructure. It is the coordinator between <see cref="ProduceTagsAsync(TaggerContext{TTag})"/>s,
        /// <see cref="ITaggerEventSource"/>s, and <see cref="ITagger{T}"/>s.</para>
        /// 
        /// <para>The <see cref="TagSource"/> is the type that actually owns the
        /// list of cached tags. When an <see cref="ITaggerEventSource"/> says tags need to be recomputed,
        /// the tag source starts the computation and calls <see cref="ProduceTagsAsync(TaggerContext{TTag})"/> to build
        /// the new list of tags. When that's done, the tags are stored in <see cref="CachedTagTrees"/>. The 
        /// tagger, when asked for tags from the editor, then returns the tags that are stored in 
        /// <see cref="CachedTagTrees"/></para>
        /// 
        /// <para>There is a one-to-many relationship between <see cref="TagSource"/>s
        /// and <see cref="ITagger{T}"/>s. Special cases, like reference highlighting (which processes multiple
        /// subject buffers at once) have their own providers and tag source derivations.</para>
        /// </summary>
        private partial class TagSource
        {
            private void OnEventSourceChanged(object sender, TaggerEventArgs e)
            {
                // First, cancel any previous requests (either still queued, or started).  We no longer
                // want to continue it if new changes have come in.
                _workQueue.CancelCurrentWork();
                RegisterNotification(
                    () => RecomputeTagsForeground(initialTags: false, synchronous: false),
                    (int)_dataSource.EventChangeDelay.ComputeTimeDelay().TotalMilliseconds,
                    GetCancellationToken(initialTags: false));
            }

            private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
            {
                this.AssertIsForeground();

                Debug.Assert(_dataSource.CaretChangeBehavior.HasFlag(TaggerCaretChangeBehavior.RemoveAllTagsOnCaretMoveOutsideOfTag));

                var caret = _dataSource.GetCaretPoint(_textViewOpt, _subjectBuffer);
                if (caret.HasValue)
                {
                    // If it changed position and we're still in a tag, there's nothing more to do
                    var currentTags = TryGetTagIntervalTreeForBuffer(caret.Value.Snapshot.TextBuffer);
                    if (currentTags != null && currentTags.GetIntersectingSpans(new SnapshotSpan(caret.Value, 0)).Count > 0)
                    {
                        // Caret is inside a tag.  No need to do anything.
                        return;
                    }
                }

                RemoveAllTags();
            }

            private void RemoveAllTags()
            {
                this.AssertIsForeground();

                var oldTagTrees = this.CachedTagTrees;
                this.CachedTagTrees = ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>>.Empty;

                var snapshot = _subjectBuffer.CurrentSnapshot;
                var oldTagTree = GetTagTree(snapshot, oldTagTrees);

                // everything from old tree is removed.
                RaiseTagsChanged(snapshot.TextBuffer, new DiffResult(added: null, removed: oldTagTree.GetSpans(snapshot).Select(s => s.Span)));
            }

            private void OnSubjectBufferChanged(object sender, TextContentChangedEventArgs e)
            {
                this.AssertIsForeground();
                UpdateTagsForTextChange(e);
                AccumulateTextChanges(e);
            }

            private void AccumulateTextChanges(TextContentChangedEventArgs contentChanged)
            {
                this.AssertIsForeground();
                var contentChanges = contentChanged.Changes;
                var count = contentChanges.Count;

                switch (count)
                {
                    case 0:
                        return;

                    case 1:
                        // PERF: Optimize for the simple case of typing on a line.
                        {
                            var c = contentChanges[0];
                            var textChangeRange = new TextChangeRange(new TextSpan(c.OldSpan.Start, c.OldSpan.Length), c.NewLength);
                            this.AccumulatedTextChanges = this.AccumulatedTextChanges == null
                                ? textChangeRange
                                : this.AccumulatedTextChanges.Accumulate(SpecializedCollections.SingletonEnumerable(textChangeRange));
                        }

                        break;

                    default:
                        var textChangeRanges = new TextChangeRange[count];
                        for (var i = 0; i < count; i++)
                        {
                            var c = contentChanges[i];
                            textChangeRanges[i] = new TextChangeRange(new TextSpan(c.OldSpan.Start, c.OldSpan.Length), c.NewLength);
                        }

                        this.AccumulatedTextChanges = this.AccumulatedTextChanges.Accumulate(textChangeRanges);
                        break;
                }
            }

            private void UpdateTagsForTextChange(TextContentChangedEventArgs e)
            {
                this.AssertIsForeground();

                if (_dataSource.TextChangeBehavior.HasFlag(TaggerTextChangeBehavior.RemoveAllTags))
                {
                    this.RemoveAllTags();
                    return;
                }

                // Don't bother going forward if we're not going adjust any tags based on edits.
                if (_dataSource.TextChangeBehavior.HasFlag(TaggerTextChangeBehavior.RemoveTagsThatIntersectEdits))
                {
                    RemoveTagsThatIntersectEdit(e);
                    return;
                }
            }

            private void RemoveTagsThatIntersectEdit(TextContentChangedEventArgs e)
            {
                if (!e.Changes.Any())
                {
                    return;
                }

                var buffer = e.After.TextBuffer;
                if (!this.CachedTagTrees.TryGetValue(buffer, out var treeForBuffer))
                {
                    return;
                }

                var tagsToRemove = e.Changes.SelectMany(c => treeForBuffer.GetIntersectingSpans(new SnapshotSpan(e.After, c.NewSpan)));
                if (!tagsToRemove.Any())
                {
                    return;
                }

                var allTags = treeForBuffer.GetSpans(e.After).ToList();
                var newTagTree = new TagSpanIntervalTree<TTag>(
                    buffer,
                    treeForBuffer.SpanTrackingMode,
                    allTags.Except(tagsToRemove, comparer: this));

                var snapshot = e.After;

                this.CachedTagTrees = this.CachedTagTrees.SetItem(snapshot.TextBuffer, newTagTree);

                // Not sure why we are diffing when we already have tagsToRemove. is it due to _tagSpanComparer might return
                // different result than GetIntersectingSpans?
                //
                // treeForBuffer basically points to oldTagTrees. case where oldTagTrees not exist is already taken cared by
                // CachedTagTrees.TryGetValue.
                var difference = ComputeDifference(snapshot, newTagTree, treeForBuffer);

                RaiseTagsChanged(snapshot.TextBuffer, difference);
            }

            private TagSpanIntervalTree<TTag> GetTagTree(ITextSnapshot snapshot, ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> tagTrees)
            {
                return tagTrees.TryGetValue(snapshot.TextBuffer, out var tagTree)
                    ? tagTree
                    : new TagSpanIntervalTree<TTag>(snapshot.TextBuffer, _dataSource.SpanTrackingMode);
            }

            /// <summary>
            /// Called on the foreground thread.  Passed a boolean to say if we're computing the
            /// initial set of tags or not.  If we're computing the initial set of tags, we lower
            /// all our delays so that we can get results to the screen as quickly as possible.
            /// 
            /// This gives a good experience when a document is opened as the document appears
            /// complete almost immediately.  Once open though, our normal delays come into play
            /// so as to not cause a flashy experience.
            /// </summary>
            private void RecomputeTagsForeground(bool initialTags, bool synchronous)
            {
                this.AssertIsForeground();
                Contract.ThrowIfTrue(synchronous && !initialTags, "synchronous computation of tags is only allowed for the initial computation");

                using (Logger.LogBlock(FunctionId.Tagger_TagSource_RecomputeTags, CancellationToken.None))
                {
                    // Stop any existing work we're currently engaged in
                    _workQueue.CancelCurrentWork();

                    var cancellationToken = GetCancellationToken(initialTags);
                    var spansToTag = GetSpansAndDocumentsToTag();

                    // Make a copy of all the data we need while we're on the foreground.  Then
                    // pass it along everywhere needed.  Finally, once new tags have been computed,
                    // then we update our state again on the foreground.
                    var caretPosition = _dataSource.GetCaretPoint(_textViewOpt, _subjectBuffer);
                    var textChangeRange = this.AccumulatedTextChanges;
                    var oldTagTrees = this.CachedTagTrees;
                    var oldState = this.State;

                    if (synchronous)
                    {
                        this.ThreadingContext.JoinableTaskFactory.Run(
                            () => this.RecomputeTagsAsync(
                                oldState, caretPosition, textChangeRange, spansToTag, oldTagTrees, initialTags, cancellationToken));
                    }
                    else
                    {
                        _workQueue.EnqueueBackgroundTask(
                            ct => this.RecomputeTagsAsync(
                                oldState, caretPosition, textChangeRange, spansToTag, oldTagTrees, initialTags, ct),
                            GetType().Name + ".RecomputeTags", cancellationToken);
                    }
                }
            }

            /// <summary>
            /// Get's the cancellation token that will control the processing of this set of
            /// tags. If this is the initial set of tags, we have a single cancellation token
            /// that can't be interrupted *unless* the entire tagger is shut down.  If this
            /// is anything after the initial set of tags, then we'll control things with a
            /// cancellation token that is triggered every time we hear about new changes.
            /// 
            /// This is a 'kick the can down the road' approach whereby we keep delaying
            /// producing tags (and updating the UI) until a reasonable pause has happened.
            /// This approach helps prevent flashing in the UI.
            /// </summary>
            private CancellationToken GetCancellationToken(bool initialTags)
                => initialTags
                    ? _initialComputationCancellationTokenSource.Token
                    : _workQueue.CancellationToken;

            private ImmutableArray<DocumentSnapshotSpan> GetSpansAndDocumentsToTag()
            {
                this.AssertIsForeground();

                // TODO: Update to tag spans from all related documents.

                var snapshotToDocumentMap = new Dictionary<ITextSnapshot, Document>();
                var spansToTag = _dataSource.GetSpansToTag(_textViewOpt, _subjectBuffer);

                var spansAndDocumentsToTag = spansToTag.Select(span =>
                {
                    if (!snapshotToDocumentMap.TryGetValue(span.Snapshot, out var document))
                    {
                        CheckSnapshot(span.Snapshot);

                        document = span.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
                        snapshotToDocumentMap[span.Snapshot] = document;
                    }

                    // document can be null if the buffer the given span is part of is not part of our workspace.
                    return new DocumentSnapshotSpan(document, span);
                }).ToImmutableArray();

                return spansAndDocumentsToTag;
            }

            [Conditional("DEBUG")]
            private static void CheckSnapshot(ITextSnapshot snapshot)
            {
                var container = snapshot.TextBuffer.AsTextContainer();
                if (Workspace.TryGetWorkspace(container, out _))
                {
                    // if the buffer is part of our workspace, it must be the latest.
                    Debug.Assert(snapshot.Version.Next == null, "should be on latest snapshot");
                }
            }

            private ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> ConvertToTagTrees(
                ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> oldTagTrees,
                ISet<ITextBuffer> buffersToTag,
                ILookup<ITextBuffer, ITagSpan<TTag>> newTagsByBuffer,
                IEnumerable<DocumentSnapshotSpan> spansTagged)
            {
                var spansToInvalidateByBuffer = spansTagged.ToLookup(
                    keySelector: span => span.SnapshotSpan.Snapshot.TextBuffer,
                    elementSelector: span => span.SnapshotSpan);

                // Walk through each relevant buffer and decide what the interval tree should be
                // for that buffer.  In general this will work by keeping around old tags that
                // weren't in the range that was re-tagged, and merging them with the new tags
                // produced for the range that was re-tagged.
                var newTagTrees = ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>>.Empty;
                foreach (var buffer in buffersToTag)
                {
                    var newTagTree = ComputeNewTagTree(oldTagTrees, buffer, newTagsByBuffer[buffer], spansToInvalidateByBuffer[buffer]);
                    if (newTagTree != null)
                    {
                        newTagTrees = newTagTrees.Add(buffer, newTagTree);
                    }
                }

                return newTagTrees;
            }

            private TagSpanIntervalTree<TTag> ComputeNewTagTree(
                ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> oldTagTrees,
                ITextBuffer textBuffer,
                IEnumerable<ITagSpan<TTag>> newTags,
                IEnumerable<SnapshotSpan> spansToInvalidate)
            {
                var noNewTags = newTags.IsEmpty();
                var noSpansToInvalidate = spansToInvalidate.IsEmpty();
                oldTagTrees.TryGetValue(textBuffer, out var oldTagTree);

                if (oldTagTree == null)
                {
                    if (noNewTags)
                    {
                        // We have no new tags, and no old tags either.  No need to store anything
                        // for this buffer.
                        return null;
                    }

                    // If we don't have any old tags then we just need to return the new tags.
                    return new TagSpanIntervalTree<TTag>(textBuffer, _dataSource.SpanTrackingMode, newTags);
                }

                // If we don't have any new tags, and there was nothing to invalidate, then we can 
                // keep whatever old tags we have without doing any additional work.
                if (noNewTags && noSpansToInvalidate)
                {
                    return oldTagTree;
                }

                // We either have some new tags, or we have some tags to invalidate.
                // First, determine which of the old tags we want to keep around.
                var snapshot = noNewTags ? spansToInvalidate.First().Snapshot : newTags.First().Span.Snapshot;
                var oldTagsToKeep = noSpansToInvalidate
                    ? oldTagTree.GetSpans(snapshot)
                    : GetNonIntersectingTagSpans(spansToInvalidate, oldTagTree);

                // Then union those with the new tags to produce the final tag tree.
                var finalTags = oldTagsToKeep.Concat(newTags);
                return new TagSpanIntervalTree<TTag>(textBuffer, _dataSource.SpanTrackingMode, finalTags);
            }

            private IEnumerable<ITagSpan<TTag>> GetNonIntersectingTagSpans(IEnumerable<SnapshotSpan> spansToInvalidate, TagSpanIntervalTree<TTag> oldTagTree)
            {
                var snapshot = spansToInvalidate.First().Snapshot;

                var tagSpansToInvalidate = new List<ITagSpan<TTag>>(
                    spansToInvalidate.SelectMany(ss => oldTagTree.GetIntersectingSpans(ss)));

                return oldTagTree.GetSpans(snapshot).Except(tagSpansToInvalidate, comparer: this);
            }

            private async Task RecomputeTagsAsync(
                object oldState,
                SnapshotPoint? caretPosition,
                TextChangeRange? textChangeRange,
                ImmutableArray<DocumentSnapshotSpan> spansToTag,
                ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> oldTagTrees,
                bool initialTags,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var context = new TaggerContext<TTag>(
                    oldState, spansToTag, caretPosition, textChangeRange, oldTagTrees, cancellationToken);
                await ProduceTagsAsync(context).ConfigureAwait(false);

                ProcessContext(oldTagTrees, context, initialTags);
            }

            private bool ShouldSkipTagProduction()
            {
                var options = _dataSource.Options ?? SpecializedCollections.EmptyEnumerable<Option2<bool>>();
                var perLanguageOptions = _dataSource.PerLanguageOptions ?? SpecializedCollections.EmptyEnumerable<PerLanguageOption2<bool>>();

                return options.Any(option => !_subjectBuffer.GetFeatureOnOffOption(option)) ||
                       perLanguageOptions.Any(option => !_subjectBuffer.GetFeatureOnOffOption(option));
            }

            private Task ProduceTagsAsync(TaggerContext<TTag> context)
            {
                if (ShouldSkipTagProduction())
                {
                    // If the feature is disabled, then just produce no tags.
                    return Task.CompletedTask;
                }

                return _dataSource.ProduceTagsAsync(context);
            }

            private void ProcessContext(
                ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> oldTagTrees,
                TaggerContext<TTag> context,
                bool initialTags)
            {
                var buffersToTag = context.SpansToTag.Select(dss => dss.SnapshotSpan.Snapshot.TextBuffer).ToSet();

                // Ignore any tag spans reported for any buffers we weren't interested in.
                var newTagsByBuffer = context.tagSpans.Where(ts => buffersToTag.Contains(ts.Span.Snapshot.TextBuffer))
                                                      .ToLookup(t => t.Span.Snapshot.TextBuffer);

                var newTagTrees = ConvertToTagTrees(oldTagTrees, buffersToTag, newTagsByBuffer, context._spansTagged);
                ProcessNewTagTrees(
                    context.SpansToTag, oldTagTrees, newTagTrees,
                    context.State, initialTags, context.CancellationToken);
            }

            private void ProcessNewTagTrees(
                ImmutableArray<DocumentSnapshotSpan> spansToTag,
                ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> oldTagTrees,
                ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> newTagTrees,
                object newState,
                bool initialTags,
                CancellationToken cancellationToken)
            {
                var bufferToChanges = new Dictionary<ITextBuffer, DiffResult>();
                using (Logger.LogBlock(FunctionId.Tagger_TagSource_ProcessNewTags, cancellationToken))
                {
                    foreach (var (latestBuffer, latestSpans) in newTagTrees)
                    {
                        var snapshot = spansToTag.First(s => s.SnapshotSpan.Snapshot.TextBuffer == latestBuffer).SnapshotSpan.Snapshot;

                        if (oldTagTrees.TryGetValue(latestBuffer, out var previousSpans))
                        {
                            var difference = ComputeDifference(snapshot, latestSpans, previousSpans);
                            bufferToChanges[latestBuffer] = difference;
                        }
                        else
                        {
                            // It's a new buffer, so report all spans are changed
                            bufferToChanges[latestBuffer] = new DiffResult(added: latestSpans.GetSpans(snapshot).Select(t => t.Span), removed: null);
                        }
                    }

                    foreach (var (oldBuffer, previousSpans) in oldTagTrees)
                    {
                        if (!newTagTrees.ContainsKey(oldBuffer))
                        {
                            // This buffer disappeared, so let's notify that the old tags are gone
                            bufferToChanges[oldBuffer] = new DiffResult(added: null, removed: previousSpans.GetSpans(oldBuffer.CurrentSnapshot).Select(t => t.Span));
                        }
                    }
                }

                if (_workQueue.IsForeground())
                {
                    // If we're on the foreground already, we can just update our internal state directly.
                    UpdateStateAndReportChanges(newTagTrees, bufferToChanges, newState, initialTags);
                }
                else if (initialTags)
                {
                    // If this is the initial set of tags, we fast-track a notification about whatever initial tags we
                    // computed.  This way the UI is updated quickly for that initial set, and we don't have to wait a
                    // potentially very long time as the foreground-thread-queue makes it way to our notification.
                    //
                    // Do this in a fire and forget manner, but ensure we notify the test harness of this so that it
                    // doesn't try to acquire tag results prior to this work finishing.
                    var asyncToken = this._asyncListener.BeginAsyncOperation(nameof(ProcessNewTagTrees));
                    _ = Task.Run(async () =>
                    {
                        await this.ThreadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                        UpdateStateAndReportChanges(newTagTrees, bufferToChanges, newState, initialTags);
                    }, CancellationToken.None).CompletesAsyncOperation(asyncToken); // TODO: What should the cancellation behavior be here? passing CancellationToken.None for now
                }
                else
                {
                    // Otherwise report back on the foreground to update the state and let our clients know about the
                    // change.  This will go to the end of the foreground processing queue.  This will normally process
                    // quickly once VS is loaded, but it may take some time initially when VS is loading and the UI
                    // thread is highly occupied.  This helps ensure that we don't oversaturate the UI during a very
                    // contended period of time.
                    RegisterNotification(() => UpdateStateAndReportChanges(
                        newTagTrees, bufferToChanges, newState, initialTags),
                        delay: 0,
                        cancellationToken: cancellationToken);
                }
            }

            private void UpdateStateAndReportChanges(
                ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> newTagTrees,
                Dictionary<ITextBuffer, DiffResult> bufferToChanges,
                object newState,
                bool initialTags)
            {
                this.AssertIsForeground();

                // Now that we're back on the UI thread, we can safely update our state with
                // what we've computed.  There is no concern with race conditions now.  For 
                // example, say that another change happened between the time when we 
                // registered for UpdateStateAndReportChanges and now.  If we processed that
                // notification (on the UI thread) first, then our cancellation token would 
                // have been triggered, and the foreground notification service would not 
                // call into this method. 
                // 
                // If, instead, we did get called into, then we will update our instance state.
                // Then when the foreground notification service runs RecomputeTagsForeground
                // it will see that state and use it as the new basis on which to compute diffs
                // and whatnot.
                this.CachedTagTrees = newTagTrees;
                this.AccumulatedTextChanges = null;
                this.State = newState;

                // Note: we're raising changes here on the UI thread.  However, this doesn't actually
                // mean we'll be notifying the editor.  Instead, these will be batched up in the 
                // AsynchronousTagger's BatchChangeNotifier.  If we tell it about enough changes
                // to a file, it will coalesce them into one large change to keep chattiness with
                // the editor down.
                OnTagsChangedForBuffer(bufferToChanges, initialTags);
            }

            /// <summary>
            /// Return all the spans that appear in only one of <paramref name="latestTree"/> or <paramref name="previousTree"/>.
            /// </summary>
            private static DiffResult ComputeDifference(
                ITextSnapshot snapshot,
                TagSpanIntervalTree<TTag> latestTree,
                TagSpanIntervalTree<TTag> previousTree)
            {
                var latestSpans = latestTree.GetSpans(snapshot);
                var previousSpans = previousTree.GetSpans(snapshot);

                using var addedPool = SharedPools.Default<List<SnapshotSpan>>().GetPooledObject();
                using var removedPool = SharedPools.Default<List<SnapshotSpan>>().GetPooledObject();
                using var latestEnumerator = latestSpans.GetEnumerator();
                using var previousEnumerator = previousSpans.GetEnumerator();

                var added = addedPool.Object;
                var removed = removedPool.Object;

                var latest = NextOrDefault(latestEnumerator);
                var previous = NextOrDefault(previousEnumerator);

                while (latest != null && previous != null)
                {
                    var latestSpan = latest.Span;
                    var previousSpan = previous.Span;

                    if (latestSpan.Start < previousSpan.Start)
                    {
                        added.Add(latestSpan);
                        latest = NextOrDefault(latestEnumerator);
                    }
                    else if (previousSpan.Start < latestSpan.Start)
                    {
                        removed.Add(previousSpan);
                        previous = NextOrDefault(previousEnumerator);
                    }
                    else
                    {
                        // If the starts are the same, but the ends are different, report the larger
                        // region to be conservative.
                        if (previousSpan.End > latestSpan.End)
                        {
                            removed.Add(previousSpan);
                            latest = NextOrDefault(latestEnumerator);
                        }
                        else if (latestSpan.End > previousSpan.End)
                        {
                            added.Add(latestSpan);
                            previous = NextOrDefault(previousEnumerator);
                        }
                        else
                        {
                            if (!EqualityComparer<TTag>.Default.Equals(latest.Tag, previous.Tag))
                                added.Add(latestSpan);

                            latest = NextOrDefault(latestEnumerator);
                            previous = NextOrDefault(previousEnumerator);
                        }
                    }
                }

                while (latest != null)
                {
                    added.Add(latest.Span);
                    latest = NextOrDefault(latestEnumerator);
                }

                while (previous != null)
                {
                    removed.Add(previous.Span);
                    previous = NextOrDefault(previousEnumerator);
                }

                return new DiffResult(added, removed);

                static ITagSpan<TTag> NextOrDefault(IEnumerator<ITagSpan<TTag>> enumerator)
                    => enumerator.MoveNext() ? enumerator.Current : null;
            }

            /// <summary>
            /// Returns the TagSpanIntervalTree containing the tags for the given buffer. If no tags
            /// exist for the buffer at all, null is returned.
            /// </summary>
            private TagSpanIntervalTree<TTag> TryGetTagIntervalTreeForBuffer(ITextBuffer buffer)
            {
                this.AssertIsForeground();

                // If this is the first time we're being asked for tags, and we're a tagger that
                // requires the initial tags be available synchronously on this call, and the 
                // computation of tags hasn't completed yet, then force the tags to be computed
                // now on this thread.  The singular use case for this is Outlining which needs
                // those tags synchronously computed for things like Metadata-as-Source collapsing.
                if (_firstTagsRequest &&
                    _dataSource.ComputeInitialTagsSynchronously(buffer) &&
                    !this.CachedTagTrees.TryGetValue(buffer, out _))
                {
                    this.RecomputeTagsForeground(initialTags: true, synchronous: true);
                }

                _firstTagsRequest = false;

                // We're on the UI thread, so it's safe to access these variables.
                this.CachedTagTrees.TryGetValue(buffer, out var tags);
                return tags;
            }

            public IEnumerable<ITagSpan<TTag>> GetTags(NormalizedSnapshotSpanCollection requestedSpans)
            {
                this.AssertIsForeground();
                if (requestedSpans.Count == 0)
                    return SpecializedCollections.EmptyEnumerable<ITagSpan<TTag>>();

                var buffer = requestedSpans.First().Snapshot.TextBuffer;
                var tags = this.TryGetTagIntervalTreeForBuffer(buffer);

                return tags == null
                    ? SpecializedCollections.EmptyEnumerable<ITagSpan<TTag>>()
                    : tags.GetIntersectingTagSpans(requestedSpans);
            }
        }
    }
}
