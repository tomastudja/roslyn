﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Tagging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Tagging
{
    internal class TaggerContext<TTag> where TTag : ITag
    {
        private readonly ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> _existingTags;

        internal IEnumerable<DocumentSnapshotSpan> _spansTagged;
        internal ImmutableArray<ITagSpan<TTag>>.Builder tagSpans = ImmutableArray.CreateBuilder<ITagSpan<TTag>>();

        public IEnumerable<DocumentSnapshotSpan> SpansToTag { get; }
        public SnapshotPoint? CaretPosition { get; }

        /// <summary>
        /// The text that has changed between the last successfull tagging and this new request to
        /// produce tags.  In order to be passed this value, <see cref="TaggerTextChangeBehavior.TrackTextChanges"/> 
        /// must be specified in <see cref="IAsynchronousTaggerDataSource{TTag}.TextChangeBehavior"/>.
        /// </summary>
        public TextChangeRange? TextChangeRange { get; }
        public CancellationToken CancellationToken { get; }

        // For testing only.
        internal TaggerContext(
            IEnumerable<DocumentSnapshotSpan> spansToTag,
            SnapshotPoint? caretPosition,
            TextChangeRange? textChangeRange,
            CancellationToken cancellationToken) 
            : this(spansToTag, caretPosition, textChangeRange, null, cancellationToken)
        {
        }

        internal TaggerContext(
            IEnumerable<DocumentSnapshotSpan> spansToTag,
            SnapshotPoint? caretPosition,
            TextChangeRange? textChangeRange,
            ImmutableDictionary<ITextBuffer, TagSpanIntervalTree<TTag>> existingTags,
            CancellationToken cancellationToken)
        {
            this.SpansToTag = spansToTag;
            this.CaretPosition = caretPosition;
            this.TextChangeRange = textChangeRange;
            this.CancellationToken = cancellationToken;

            _spansTagged = spansToTag;
            _existingTags = existingTags;
        }

        public void AddTag(ITagSpan<TTag> tag)
        {
            tagSpans.Add(tag);
        }

        /// <summary>
        /// Used to allow taggers to indicate what spans were actually tagged.  This is useful 
        /// when the tagger decides to tag a different span than the entire file.  If a sub-span
        /// of a document is tagged then the tagger infrastructure will keep previously computed
        /// tags from before and after the sub-span and merge them with the newly produced tags.
        /// </summary>
        public void SetSpansTagged(IEnumerable<DocumentSnapshotSpan> spansTagged)
        {
            if (spansTagged == null)
            {
                throw new ArgumentNullException(nameof(spansTagged));
            }

            this._spansTagged = spansTagged;
        }

        public IEnumerable<ITagSpan<TTag>> GetExistingTags(SnapshotSpan span)
        {
            TagSpanIntervalTree<TTag> tree;
            return _existingTags != null && _existingTags.TryGetValue(span.Snapshot.TextBuffer, out tree)
                ? tree.GetIntersectingSpans(span)
                : SpecializedCollections.EmptyEnumerable<ITagSpan<TTag>>();
        }
    }
}