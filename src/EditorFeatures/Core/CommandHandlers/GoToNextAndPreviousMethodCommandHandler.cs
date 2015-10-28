﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Editor.Commands;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.SuggestionSupport;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CommandHandlers
{
    [ExportCommandHandler(PredefinedCommandHandlerNames.GoToNextAndPreviousMethod, ContentTypeNames.RoslynContentType)]
    internal class GoToNextAndPreviousMethodCommandHandler : ICommandHandler<GoToNextMethodCommandArgs>, ICommandHandler<GoToPreviousMethodCommandArgs>
    {
        private readonly IWaitIndicator _waitIndicator;
        private readonly IOutliningManagerService _outliningManagerService;

        [ImportingConstructor]
        public GoToNextAndPreviousMethodCommandHandler(IWaitIndicator waitIndicator, IOutliningManagerService outliningManagerService)
        {
            _waitIndicator = waitIndicator;
            _outliningManagerService = outliningManagerService;
        }

        public CommandState GetCommandState(GoToNextMethodCommandArgs args, Func<CommandState> nextHandler)
            => GetCommandState(args.SubjectBuffer, args.TextView, nextHandler);

        public void ExecuteCommand(GoToNextMethodCommandArgs args, Action nextHandler)
            => ExecuteCommand(nextHandler, args.SubjectBuffer, args.TextView, next: true);

        public CommandState GetCommandState(GoToPreviousMethodCommandArgs args, Func<CommandState> nextHandler)
            => GetCommandState(args.SubjectBuffer, args.TextView, nextHandler);

        public void ExecuteCommand(GoToPreviousMethodCommandArgs args, Action nextHandler)
            => ExecuteCommand(nextHandler, args.SubjectBuffer, args.TextView, next: false);

        private static CommandState GetCommandState(ITextBuffer subjectBuffer, ITextView textView, Func<CommandState> nextHandler)
        {
            var document = subjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
            var caretPoint = textView.GetCaretPoint(subjectBuffer);
            return IsAvailable(document, caretPoint) ? CommandState.Available : nextHandler();
        }

        private static bool IsAvailable(Document document, SnapshotPoint? caretPoint)
        {
            if (document?.SupportsSyntaxTree != true)
            {
                return false;
            }

            if (!caretPoint.HasValue)
            {
                return false;
            }

            var documentSupportsSuggestionService = document.Project.Solution.Workspace.Services.GetService<IDocumentSupportsSuggestionService>();
            return documentSupportsSuggestionService?.SupportsNavigationToAnyPosition(document) == true;
        }

        private void ExecuteCommand(Action nextHandler, ITextBuffer subjectBuffer, ITextView textView, bool next)
        {
            var document = subjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
            var caretPoint = textView.GetCaretPoint(subjectBuffer);
            if (!IsAvailable(document, caretPoint))
            {
                nextHandler();
                return;
            }

            int? targetPosition = null;
            var waitResult = _waitIndicator.Wait(EditorFeaturesResources.Navigating, allowCancel: true, action: waitContext =>
            {
                targetPosition = GetTargetPosition(document, caretPoint.Value.Position, next, waitContext.CancellationToken);
            });

            if (waitResult == WaitIndicatorResult.Canceled || targetPosition == null)
            {
                return;
            }

            textView.TryMoveCaretToAndEnsureVisible(new SnapshotPoint(subjectBuffer.CurrentSnapshot, targetPosition.Value), _outliningManagerService);
        }

        /// <summary>
        /// Internal for testing purposes.
        /// </summary>
        internal static int? GetTargetPosition(Document document, int caretPosition, bool next, CancellationToken cancellationToken)
        {
            var syntaxFactsService = document.GetLanguageService<ISyntaxFactsService>();
            if (syntaxFactsService == null)
            {
                return null;
            }

            var root = document.GetSyntaxRootAsync(cancellationToken).WaitAndGetResult(cancellationToken);
            var members = syntaxFactsService.GetMethodLevelMembers(root);
            if (members.Count == 0)
            {
                return null;
            }

            var spans = members.Select(m => m.Span).ToArray();
            var index = Array.BinarySearch(spans, new TextSpan(caretPosition, 0), PositionToTextSpanComparer.Instance);
            if (index >= 0)
            {
                // We're actually contained in a member, go to the next or previous.
                index = next ? index + 1 : index - 1;
            }
            else
            {
                // We're in between to members, ~index gives us the member we're before, so we'll just
                // advance to the start of it
                index = next ? ~index : ~index - 1;
            }

            // Wrap if necessary
            if (index >= members.Count)
            {
                index = 0;
            }
            else if (index < 0)
            {
                index = members.Count - 1;
            }

            // TODO: Better position within the node (e.g. attributes?)
            return members[index].Span.Start;
        }

        /// <summary>
        /// A custom comparer that returns true if two <see cref="TextSpan"/>'s intersect, and otherwise
        /// compares by <see cref="TextSpan.Start"/>.
        /// </summary>
        private class PositionToTextSpanComparer : IComparer
        {
            public static IComparer Instance { get; } = new PositionToTextSpanComparer();

            private PositionToTextSpanComparer() { }

            int IComparer.Compare(object x, object y)
            {
                var left = (TextSpan)x;
                var right = (TextSpan)y;

                if (left.IntersectsWith(right))
                {
                    return 0;
                }

                return left.Start - right.Start;
            }
        }
    }
}
