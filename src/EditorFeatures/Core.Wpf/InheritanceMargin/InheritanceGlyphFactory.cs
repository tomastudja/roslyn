﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.InheritanceMargin.MarginGlyph;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.InheritanceMargin
{
    internal sealed class InheritanceGlyphFactory : IGlyphFactory
    {
        private readonly IThreadingContext _threadingContext;
        private readonly IStreamingFindUsagesPresenter _streamingFindUsagesPresenter;
        private readonly IWaitIndicator _waitIndicator;

        public InheritanceGlyphFactory(
            IThreadingContext threadingContext,
            IStreamingFindUsagesPresenter streamingFindUsagesPresenter,
            IWaitIndicator waitIndicator)
        {
            _threadingContext = threadingContext;
            _streamingFindUsagesPresenter = streamingFindUsagesPresenter;
            _waitIndicator = waitIndicator;
        }

        public UIElement? GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            if (tag is InheritanceMarginTag inheritanceMarginTag)
            {
                var membersOnLine = inheritanceMarginTag.MembersOnLine;
                Contract.ThrowIfTrue(membersOnLine.IsEmpty);

                var solution = inheritanceMarginTag.Document.Project.Solution;
                if (membersOnLine.Length == 1)
                {
                    var viewModel = new SingleMemberMarginViewModel(inheritanceMarginTag);
                    return MarginGlyph.InheritanceMargin.CreateForSingleMember(_threadingContext, _streamingFindUsagesPresenter, _waitIndicator, solution ,viewModel);
                }
                else
                {
                    var viewModel = new MultipleMembersMarginViewModel(inheritanceMarginTag);
                    return MarginGlyph.InheritanceMargin.CreateForMultipleMembers(_threadingContext, _streamingFindUsagesPresenter, _waitIndicator, solution, viewModel);
                }
            }

            return null;
        }
    }
}
