﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.LanguageServices.Implementation.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.ValueTracking
{
    internal class ValueTrackingTreeItemViewModel : TreeViewItemBase
    {
        private readonly string _sourceText;
        private readonly Glyph _glyph;
        private readonly IGlyphService _glyphService;

        protected ValueTrackingTreeViewModel TreeViewModel { get; }
        protected TextSpan TextSpan { get; }
        protected LineSpan LineSpan { get; }
        protected IThreadingContext ThreadingContext { get; }
        protected DocumentId DocumentId { get; }
        protected Workspace Workspace { get; }

        public int LineNumber => LineSpan.Start + 1; // LineSpan is 0 indexed, editors are not
        public ObservableCollection<TreeViewItemBase> ChildItems { get; } = new();

        public string FileName { get; }

        public ImageSource GlyphImage => _glyph.GetImageSource(_glyphService);

        public ImmutableArray<ClassifiedSpan> ClassifiedSpans { get; }

        public IList<Inline> Inlines
        {
            get
            {
                if (ClassifiedSpans.IsDefaultOrEmpty)
                {
                    return new List<Inline>();
                }

                var classifiedTexts = ClassifiedSpans.SelectAsArray(
                   cs =>
                   {
                       var adjustedStart = cs.TextSpan.Start - TextSpan.Start;
                       return new ClassifiedText(cs.ClassificationType, _sourceText.Substring(adjustedStart, cs.TextSpan.Length));
                   });

                var spanStartPosition = TextSpan.Start - ClassifiedSpans[0].TextSpan.Start;
                var spanEndPosition = TextSpan.End - ClassifiedSpans[0].TextSpan.End;

                return classifiedTexts.ToInlines(
                    TreeViewModel.ClassificationFormatMap,
                    TreeViewModel.ClassificationTypeMap,
                    (run, classifiedText, position) =>
                    {
                        if (TreeViewModel.HighlightBrush is not null)
                        {
                            if (position >= spanStartPosition && position <= spanEndPosition)
                            {
                                run.SetValue(
                                    TextElement.BackgroundProperty,
                                    TreeViewModel.HighlightBrush);
                            }
                        }
                    });
            }
        }

        public ValueTrackingTreeItemViewModel(
            TextSpan textSpan,
            SourceText sourceText,
            DocumentId documentId,
            string fileName,
            Glyph glyph,
            ImmutableArray<ClassifiedSpan> classifiedSpans,
            ValueTrackingTreeViewModel treeViewModel,
            IGlyphService glyphService,
            IThreadingContext threadingContext,
            Workspace workspace,
            ImmutableArray<ValueTrackingTreeItemViewModel> children = default)
        {
            FileName = fileName;
            TextSpan = textSpan;

            ClassifiedSpans = classifiedSpans;
            TreeViewModel = treeViewModel;
            ThreadingContext = threadingContext;

            _sourceText = sourceText.ToString(TextSpan);
            _glyph = glyph;
            _glyphService = glyphService;
            Workspace = workspace;
            DocumentId = documentId;

            if (!children.IsDefaultOrEmpty)
            {
                foreach (var child in children)
                {
                    ChildItems.Add(child);
                }
            }

            sourceText.GetLineAndOffset(textSpan.Start, out var lineStart, out var _);
            sourceText.GetLineAndOffset(textSpan.End, out var lineEnd, out var _);
            LineSpan = LineSpan.FromBounds(lineStart, lineEnd);
        }

        public virtual void Select()
        {
            var navigationService = Workspace.Services.GetService<IDocumentNavigationService>();
            if (navigationService is null)
            {
                return;
            }

            // While navigating do not activate the tab, which will change focus from the tool window
            var options = Workspace.Options
                .WithChangedOption(new OptionKey(NavigationOptions.PreferProvisionalTab), true)
                .WithChangedOption(new OptionKey(NavigationOptions.ActivateTab), false);

            navigationService.TryNavigateToLineAndOffset(Workspace, DocumentId, LineSpan.Start, 0, options, ThreadingContext.DisposalToken);
        }
    }
}
