﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.CodeAnalysis.Editor.Margin.InheritanceChainMargin
{
    internal class InheritanceChainGlyphTagger : ITagger<InheritanceChainGlyphTag>
    {
        public IEnumerable<ITagSpan<InheritanceChainGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
