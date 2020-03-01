﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.CodeAnalysis.Editor.Implementation.SplitComment;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CSharp.SplitComment
{
    [Export(typeof(ICommandHandler))]
    [ContentType(ContentTypeNames.CSharpContentType)]
    [Name(nameof(SplitCommentCommandHandler))]
    [Order(After = PredefinedCompletionNames.CompletionCommandHandler)]
    internal partial class SplitCommentCommandHandler : AbstractSplitCommentCommandHandler
    {
        [ImportingConstructor]
        public SplitCommentCommandHandler(
            ITextUndoHistoryRegistry undoHistoryRegistry,
            IEditorOperationsFactoryService editorOperationsFactoryService)
        {
            _undoHistoryRegistry = undoHistoryRegistry;
            _editorOperationsFactoryService = editorOperationsFactoryService;
        }

        protected override bool LineContainsComment(ITextSnapshotLine line, int caretPosition)
        {
            var snapshot = line.Snapshot;
            var text = snapshot.GetText();
            _hasSpaceAfterComment = text.Contains(CommentSplitter.CommentCharacter + ' ');

            if (caretPosition > line.End.Position)
            {
                return false;
            }
            else
            {
                return text.Contains(CommentSplitter.CommentCharacter);
            }
        }

        protected override int? SplitComment(
           Document document, DocumentOptionSet options, int position, CancellationToken cancellationToken)
        {
            var useTabs = options.GetOption(FormattingOptions.UseTabs);
            var tabSize = options.GetOption(FormattingOptions.TabSize);
            var indentStyle = options.GetOption(FormattingOptions.SmartIndent, LanguageNames.CSharp);

            var root = document.GetSyntaxRootSynchronously(cancellationToken);
            var sourceText = root.SyntaxTree.GetText(cancellationToken);

            var splitter = CommentSplitter.Create(
                document, position, root, sourceText,
                useTabs, tabSize, indentStyle, _hasSpaceAfterComment, cancellationToken);

            return splitter?.TrySplit();
        }
    }
}
