﻿using System;
using System.Windows;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.QuickInfo;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.FindReferences;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using System.IO;
using System.Windows.Media;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem;
using Microsoft.VisualStudio.LanguageServices.Implementation.Extensions;
using Roslyn.Utilities;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.VisualStudio.LanguageServices.FindReferences
{
    internal partial class StreamingFindReferencesPresenter
    {
        private class ReferenceEntry
        {
            private readonly StreamingFindReferencesPresenter _presenter;
            private readonly VisualStudioWorkspaceImpl _workspace;

            private readonly RoslynDefinitionBucket _definitionBucket;
            private readonly SourceReferenceItem _sourceReferenceItem;

            private readonly object _boxedProjectGuid;
            private readonly SourceText _sourceText;
            private readonly TaggedTextAndHighlightSpan _taggedLineParts;
            private readonly TaggedTextAndHighlightSpan _taggedRegionParts;

            public ReferenceEntry(
                StreamingFindReferencesPresenter presenter,
                VisualStudioWorkspaceImpl workspace,
                RoslynDefinitionBucket definitionBucket,
                SourceReferenceItem sourceReferenceItem,
                Guid projectGuid,
                SourceText sourceText,
                TaggedTextAndHighlightSpan taggedLineParts,
                TaggedTextAndHighlightSpan taggedRegionParts)
            {
                _presenter = presenter;

                _workspace = workspace;
                _definitionBucket = definitionBucket;
                _sourceReferenceItem = sourceReferenceItem;

                _boxedProjectGuid = projectGuid;
                _sourceText = sourceText;
                _taggedLineParts = taggedLineParts;
                _taggedRegionParts = taggedRegionParts;
            }

            public bool TryGetValue(string keyName, out object content)
            {
                content = GetValue(keyName);
                return content != null;
            }

            //internal bool TryCreateColumnContent(string columnName, out FrameworkElement element)
            //{
            //    if (columnName == StandardTableKeyNames2.TextInlines)
            //    {
            //        var backgroundBrush = _presenter._formatMapService.GetEditorFormatMap("tooltip").GetProperties("MarkerFormatDefinition/HighlightedReference")["BackgroundColor"];

            //        var textBlock = _taggedParts.TaggedText.ToTextBlock(_presenter._typeMap,
            //            (run, taggedText, position) =>
            //            {
            //                if (position == _taggedParts.HighlightSpan.Start)
            //                {
            //                    run.SetValue(
            //                        System.Windows.Documents.TextElement.BackgroundProperty,
            //                        backgroundBrush);
            //                }
            //            });

            //        element = textBlock;
            //        return true;
            //    }

            //    element = null;
            //    return false;
            //}

            private DocumentLocation Location => _sourceReferenceItem.Location;
            private Document Document => Location.Document;
            private TextSpan SourceSpan => Location.SourceSpan;

            private object GetValue(string keyName)
            {
                switch (keyName)
                {
                case StandardTableKeyNames.DocumentName:
                    var projectFilePath = Document.Project.FilePath;
                    var documentPath = Document.FilePath;
                    var projectDirectory = Path.GetDirectoryName(projectFilePath);

                    if (documentPath.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        documentPath = documentPath.Substring(projectDirectory.Length);
                        documentPath = documentPath.TrimStart('\\', '/');
                    }

                    return documentPath;
                //                    return Document.FilePath;
                case StandardTableKeyNames.Line:
                    return _sourceText.Lines.GetLinePosition(SourceSpan.Start).Line;
                case StandardTableKeyNames.Column:
                    return _sourceText.Lines.GetLinePosition(SourceSpan.Start).Character;
                case StandardTableKeyNames.ProjectName:
                    return Location.Document.Project.Name;
                case StandardTableKeyNames.ProjectGuid:
                    return _boxedProjectGuid;

                case StandardTableKeyNames.Text:
                // case StandardTableKeyNames.FullText:
                    return _sourceText.Lines.GetLineFromPosition(SourceSpan.Start).ToString().Trim();

                case StandardTableKeyNames2.TextInlines:
                    return GetHighlightedInlines(_presenter, _taggedLineParts);

                case StandardTableKeyNames2.DefinitionIcon:
                    return _definitionBucket.DefinitionItem.Tags.GetGlyph().GetImageMoniker();

                case StandardTableKeyNames2.Definition:
                    return _definitionBucket;
                }

                return null;
            }

            private static IList<System.Windows.Documents.Inline> GetHighlightedInlines(
                StreamingFindReferencesPresenter presenter,
                TaggedTextAndHighlightSpan taggedTextAndHighlight,
                string classificationFormatMap = null)
            {
                var properties = presenter._formatMapService
                                          .GetEditorFormatMap("text")
                                          .GetProperties("MarkerFormatDefinition/HighlightedReference");
                var highlightBrush = properties["Background"] as Brush;

                var lineParts = taggedTextAndHighlight.TaggedText;
                var inlines = lineParts.ToInlines(
                    presenter._typeMap,
                    classificationFormatMap,
                    (run, taggedText, position) =>
                    {
                        if (highlightBrush != null)
                        {
                            if (position == taggedTextAndHighlight.HighlightSpan.Start)
                            {
                                run.SetValue(
                                    System.Windows.Documents.TextElement.BackgroundProperty,
                                    highlightBrush);
                            }
                        }
                    });

                return inlines;
            }

            internal bool TryCreateToolTip(string columnName, out object toolTip)
            {
                var highlightedInlines = GetHighlightedInlines(_presenter, _taggedRegionParts, "text");
                var textBlock = highlightedInlines.ToTextBlock(_presenter._typeMap, "text");

                TextOptions.SetTextFormattingMode(textBlock, TextFormattingMode.Ideal);
                var transform = new ScaleTransform(0.75, 0.75);
                transform.Freeze();
                textBlock.LayoutTransform = transform;

                toolTip = textBlock;
                return true;
            }
        }
    }
}