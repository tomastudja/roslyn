﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.VisualStudio.Text;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Classification
{
    internal partial class SyntacticClassificationTaggerProvider
    {
        internal partial class TagComputer
        {
            /// <summary>
            /// it is a helper class that encapsulates logic on holding onto last classification result
            /// </summary>
            private class LastLineCache : ForegroundThreadAffinitizedObject
            {
                // this helper class is primarily to improve active typing perf. don't bother to cache
                // something very big. 
                private const int MaxClassificationNumber = 32;

                // mutating state
                private SnapshotSpan _span;
                private List<ClassifiedSpan> _classifications;

                public LastLineCache(IThreadingContext threadingContext) : base(threadingContext)
                    => this.Clear();

                private void Clear()
                {
                    this.AssertIsForeground();

                    _span = default;
                    ClassificationUtilities.ReturnClassifiedSpanList(_classifications);
                    _classifications = null;
                }

                public bool TryUseCache(SnapshotSpan span, out List<ClassifiedSpan> classifications)
                {
                    this.AssertIsForeground();

                    // currently, it is using SnapshotSpan even though holding onto it could be
                    // expensive. reason being it should be very soon sync-ed to latest snapshot.
                    if (_classifications != null && _span.Equals(span))
                    {
                        classifications = _classifications;
                        return true;
                    }

                    this.Clear();
                    classifications = null;
                    return false;
                }

                public void Update(SnapshotSpan span, List<ClassifiedSpan> classifications)
                {
                    this.AssertIsForeground();
                    this.Clear();

                    if (classifications.Count < MaxClassificationNumber)
                    {
                        _span = span;
                        _classifications = classifications;
                    }
                }
            }
        }
    }
}
