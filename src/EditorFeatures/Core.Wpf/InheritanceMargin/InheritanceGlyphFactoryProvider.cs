﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.InheritanceMargin
{
    [Export(typeof(IGlyphFactoryProvider))]
    [Name(nameof(InheritanceGlyphFactoryProvider))]
    [ContentType(ContentTypeNames.CSharpContentType)]
    [ContentType(ContentTypeNames.VisualBasicContentType)]
    [TagType(typeof(InheritanceMarginTag))]
    [Order(After = "VsTextMarker")]
    internal class InheritanceGlyphFactoryProvider : IGlyphFactoryProvider
    {
        private readonly IThreadingContext _threadingContext;
        private readonly IStreamingFindUsagesPresenter _streamingFindUsagesPresenter;
        private readonly IWaitIndicator _waitIndicator;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InheritanceGlyphFactoryProvider(
            IThreadingContext threadingContext,
            IStreamingFindUsagesPresenter streamingFindUsagesPresenter,
            IWaitIndicator waitIndicator)
        {
            _threadingContext = threadingContext;
            _streamingFindUsagesPresenter = streamingFindUsagesPresenter;
            _waitIndicator = waitIndicator;
        }

        public IGlyphFactory GetGlyphFactory(IWpfTextView view, IWpfTextViewMargin margin)
        {
            return new InheritanceGlyphFactory(_threadingContext, _streamingFindUsagesPresenter, _waitIndicator);
        }
    }
}
