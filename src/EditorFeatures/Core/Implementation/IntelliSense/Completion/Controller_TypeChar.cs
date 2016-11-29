﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Editor.Commands;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Options;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.Completion
{
    internal partial class Controller
    {
        CommandState ICommandHandler<TypeCharCommandArgs>.GetCommandState(TypeCharCommandArgs args, Func<CommandState> nextHandler)
        {
            AssertIsForeground();

            // We just defer to the editor here.  We do not interfere with typing normal characters.
            return nextHandler();
        }

        void ICommandHandler<TypeCharCommandArgs>.ExecuteCommand(TypeCharCommandArgs args, Action nextHandler)
        {
            AssertIsForeground();

            // When a character is typed it is *always* sent through to the editor.  This way the
            // editor always represents what would have been typed had completion not been involved
            // at this point.  That means that if we decide to commit, then undo'ing the commit will
            // return you to the code that you would have typed if completion was not up.
            //
            // The steps we follow for commit are as follows:
            //
            //      1) send the commit character through to the buffer.
            //      2) open a transaction.
            //          2a) roll back the text to before the text was sent through
            //          2b) commit the item.
            //          2c) send the commit character through again.*
            //          2d) commit the transaction.
            //
            // 2c is very important.  it makes sure that post our commit all our normal features
            // run depending on what got typed.  For example if the commit character was (
            // then brace completion may run.  If it was ; then formatting may run.  But, importantly
            // this code doesn't need to know anything about that.  Furthermore, because that code
            // runs within this transaction, then the user can always undo and get to what the code
            // would have been if completion was not involved.
            //
            // 2c*: note sending the commit character through to the buffer again can be controlled
            // by the completion item.  For example, completion items that want to totally handle
            // what gets output into the buffer can ask for this not to happen.  An example of this
            // is override completion.  If the user types "override Method(" then we'll want to 
            // spit out the entire method and *not* also spit out "(" again.

            // In order to support 2a (rolling back), we capture hte state of the buffer before
            // we send the character through.  We then just apply the edits in reverse order to
            // roll us back.
            var initialTextSnapshot = this.SubjectBuffer.CurrentSnapshot;

            var initialCaretPosition = GetCaretPointInViewBuffer();

            // Note: while we're doing this, we don't want to hear about buffer changes (since we
            // know they're going to happen).  So we disconnect and reconnect to the event
            // afterwards.  That way we can hear about changes to the buffer that don't happen
            // through us.

            // Automatic Brace Completion may also move the caret, so unsubscribe from that too
            this.TextView.TextBuffer.PostChanged -= OnTextViewBufferPostChanged;
            this.TextView.Caret.PositionChanged -= OnCaretPositionChanged;

            // In Venus/Razor, the user might be typing on the buffer's seam. This means that,
            // depending on the character typed, the character may not go into our buffer.
            var isOnSeam = IsOnSeam();

            try
            {
                nextHandler();
            }
            finally
            {
                this.TextView.TextBuffer.PostChanged += OnTextViewBufferPostChanged;
                this.TextView.Caret.PositionChanged += OnCaretPositionChanged;
            }

            // We only want to process typechar if it is a normal typechar and no one else is
            // involved.  i.e. if there was a typechar, but someone processed it and moved the caret
            // somewhere else then we don't want completion.  Also, if a character was typed but
            // something intercepted and placed different text into the editor, then we don't want
            // to proceed. 
            if (this.TextView.TypeCharWasHandledStrangely(this.SubjectBuffer, args.TypedChar))
            {
                if (sessionOpt != null)
                {
                    // If we're on a seam (razor) with a computation, and the user types a character 
                    // that goes into the other side of the seam, the character may be a commit character.
                    // If it's a commit character, just commit without trying to check caret position,
                    // since the caret is no longer in our buffer.
                    if (isOnSeam && this.CommitIfCommitCharacter(args.TypedChar, initialTextSnapshot, nextHandler))
                    {
                        return;
                    }

                    if (_autoBraceCompletionChars.Contains(args.TypedChar) &&
                             this.SubjectBuffer.GetFeatureOnOffOption(InternalFeatureOnOffOptions.AutomaticPairCompletion) &&
                             this.CommitIfCommitCharacter(args.TypedChar, initialTextSnapshot, nextHandler))
                    {
                        // I don't think there is any better way than this. if typed char is one of auto brace completion char,
                        // we don't do multiple buffer change check
                        return;
                    }

                    // If we were computing anything, we stop.  We only want to process a typechar
                    // if it was a normal character.
                    this.StopModelComputation();
                }

                return;
            }

            var completionService = this.GetCompletionService();
            if (completionService == null)
            {
                return;
            }

            var options = GetOptions();
            Contract.ThrowIfNull(options);

            var isTextuallyTriggered = IsTextualTriggerCharacter(completionService, args.TypedChar, options);
            var trigger = CompletionTrigger.CreateInsertionTrigger(args.TypedChar);

            if (sessionOpt == null)
            {
                // No current session.  start one if necessary.
                ExecuteTypeCharWithNoSession(completionService, isTextuallyTriggered, trigger);
            }
            else
            {
                ExecuteTypeCharWithSession(args, nextHandler, initialTextSnapshot, initialCaretPosition, completionService, options, isTextuallyTriggered, trigger);
            }
        }

        private void ExecuteTypeCharWithSession(
            TypeCharCommandArgs args,
            Action nextHandler,
            ITextSnapshot initialTextSnapshot,
            SnapshotPoint initialCaretPosition,
            CompletionService completionService,
            OptionSet options,
            bool isTextuallyTriggered,
            CompletionTrigger trigger)
        {

            sessionOpt.UpdateModelTrackingSpan(initialCaretPosition);

            // If the session is up, it may be in one of many states.  It may know nothing
            // (because it is currently computing the list of completions).  Or it may have a
            // list of completions that it has filtered. 

            // If the user types something which is absolutely known to be a filter character
            // then we can just proceed without blocking.
            if (IsPotentialFilterCharacter(args))
            {
                if (isTextuallyTriggered)
                {
                    // The character typed was something like "a".  It can both filter a list if
                    // we have computed one, or it can trigger a new list.  Ask the computation
                    // to compute again. If nothing has been computed, then it will try to
                    // compute again, otherwise it will just ignore this request.
                    sessionOpt.ComputeModel(completionService, trigger, _roles, options);
                }

                // Now filter whatever result we have.
                sessionOpt.FilterModel(
                    CompletionFilterReason.TypeChar,
                    recheckCaretPosition: false,
                    dismissIfEmptyAllowed: true,
                    filterState: null);
                return;
            }

            // It wasn't a trigger or filter character. At this point, we make our determination 
            // on what to do based on what has actually been computed and what's being typed. 
            // In order to do this, we need to know what the actual completion items are.  If we 
            // want these, we have to block.  We will do this if the language allows this.  
            // Generally, the language will allow this if completions can be computed quickly.  
            // If they can't, then we will not block and we will stop computed immediately.

            if (sessionOpt.InitialUnfilteredModel == null)
            {
                // We haven't finished computing completion items.  Block if the language wants
                // us to.  Otherwise, dismiss the session
                var result = Workspace.TryGetWorkspace(this.SubjectBuffer.AsTextContainer(), out var workspace);
                Debug.Assert(result, "We successfully got the completion service.  We must be able to get to the workspace.");

                if (!options.GetOption(CompletionOptions.BlockForCompletionItems, completionService.Language))
                {
                    // Language doesn't want us to block.  Just stop our existing session as we 
                    // don't have enough data on what to do.  Restart the session if appropriate
                    // for the character typed.
                    DismissSessionAndStartAnotherIfTriggered(completionService, isTextuallyTriggered, trigger);
                    return;
                }
            }

            // We've either computed the items, or the language is fine with blocking.  
            // Block to get the model and determine what we should so.

            // What they type may end up filtering, committing, or else will dismiss.
            //
            // For example, we may filter in cases like this: "Color."
            //
            // "Color" will have already filtered the list down to some things like
            // "Color", "Color.Red", "Color.Blue", etc.  When we process the 'dot', we
            // actually want to filter some more.  But we can't know that ahead of time until
            // we have computed the list of completions.
            var model = sessionOpt.WaitForModel();
            if (this.IsFilterCharacter(args.TypedChar, model))
            {
                // Known to be a filter character for the currently selected item.  So just 
                // filter the session.
                sessionOpt.FilterModel(CompletionFilterReason.TypeChar,
                    recheckCaretPosition: false,
                    dismissIfEmptyAllowed: true,
                    filterState: null);
                return;
            }

            // It wasn't a filter character.  We'll either commit what's selected, or we'll
            // dismiss the completion list.  
            this.CommitIfCommitCharacter(args.TypedChar, model, initialTextSnapshot, nextHandler);

            // If the character was a commit character, then we will have committed the list
            // and dismissed the session.  If it wasn't a commit character, then we don't know
            // what to do with this character, and we want to dismiss the session.  So, at this
            // point we always want hte session dismissed. 'DismissSessionAndStartAnotherIfTriggered'
            // will ensure that happens.

            // The character may have committed *and* should start a new session.  i.e. 
            // hittins "Sys<dot>".  <dot> should commit "System" and start a new session.
            DismissSessionAndStartAnotherIfTriggered(completionService, isTextuallyTriggered, trigger);
        }

        private void DismissSessionAndStartAnotherIfTriggered(CompletionService completionService, bool isTextuallyTriggered, CompletionTrigger trigger)
        {
            DismissSessionIfActive();

            if (isTextuallyTriggered)
            {
                // First create the session that represents that we now have a potential
                // completion list.
                StartNewModelComputation(
                    completionService, trigger, filterItems: true, dismissIfEmptyAllowed: true);
            }
        }

        private void ExecuteTypeCharWithNoSession(CompletionService completionService, bool isTextuallyTriggered, CompletionTrigger trigger)
        {
            // No computation at all.  If this is not a trigger character, we just ignore it and
            // stay in this state.  Otherwise, if it's a trigger character, start up a new
            // computation and start computing the model in the background.
            if (isTextuallyTriggered)
            {
                // First create the session that represents that we now have a potential
                // completion list.  Then tell it to start computing.
                StartNewModelComputation(completionService, trigger, filterItems: true, dismissIfEmptyAllowed: true);
                return;
            }
            else
            {
                // No need to do anything.  Just stay in the state where we have no session.
                return;
            }
        }

        private bool IsOnSeam()
        {
            var caretPoint = TextView.Caret.Position.Point;
            var point1 = caretPoint.GetPoint(this.SubjectBuffer, PositionAffinity.Predecessor);
            var point2 = caretPoint.GetPoint(this.SubjectBuffer, PositionAffinity.Successor);
            if (point1.HasValue && point1 != point2)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// A potential filter character is something that can filter a completion lists and is
        /// *guaranteed* to not be a commit character.
        /// </summary>
        private static bool IsPotentialFilterCharacter(TypeCharCommandArgs args)
        {
            // TODO(cyrusn): Actually use the right unicode categories here.
            return char.IsLetter(args.TypedChar)
                || char.IsNumber(args.TypedChar)
                || args.TypedChar == '_';
        }

        private Document GetDocument()
        {
            return this.SubjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges();
        }

        private CompletionHelper GetCompletionHelper()
        {
            var document = GetDocument();
            if (document != null)
            {
                return CompletionHelper.GetHelper(document);
            }

            return null;
        }

        private bool IsTextualTriggerCharacter(CompletionService completionService, char ch, OptionSet options)
        {
            AssertIsForeground();

            // Note: When this function is called we've already guaranteed that
            // TypeCharWasHandledStrangely returned false.  That means we know that the caret is in
            // our buffer, and is after the character just typed.

            var caretPosition = this.TextView.GetCaretPoint(this.SubjectBuffer).Value;
            var previousPosition = caretPosition - 1;
            Contract.ThrowIfFalse(this.SubjectBuffer.CurrentSnapshot[previousPosition] == ch);

            var trigger = CompletionTrigger.CreateInsertionTrigger(ch);
            return completionService.ShouldTriggerCompletion(previousPosition.Snapshot.AsText(), caretPosition, trigger, _roles, options);
        }

        private bool IsCommitCharacter(char ch, Model model)
        {
            AssertIsForeground();

            if (model == null || model.IsSoftSelection)
            {
                return false;
            }

            if (model.SelectedItem == model.SuggestionModeItem)
            {
                return char.IsLetterOrDigit(ch);
            }

            var completionService = GetCompletionService();
            if (completionService == null)
            {
                return false;
            }

            var textTypedSoFar = GetTextTypedSoFar(model, model.SelectedItem);
            return IsCommitCharacter(
                completionService.GetRules(), model.SelectedItem, ch, textTypedSoFar);
        }

        /// <summary>
        /// Internal for testing purposes only.
        /// </summary>
        internal static bool IsCommitCharacter(
            CompletionRules completionRules, CompletionItem item, char ch, string textTypedSoFar)
        {
            // First see if the item has any specifc commit rules it wants followed.
            foreach (var rule in item.Rules.CommitCharacterRules)
            {
                switch (rule.Kind)
                {
                    case CharacterSetModificationKind.Add:
                        if (rule.Characters.Contains(ch))
                        {
                            return true;
                        }
                        continue;

                    case CharacterSetModificationKind.Remove:
                        if (rule.Characters.Contains(ch))
                        {
                            return false;
                        }
                        continue;

                    case CharacterSetModificationKind.Replace:
                        return rule.Characters.Contains(ch);
                }
            }

            // general rule: if the filtering text exactly matches the start of the item then it must be a filter character
            if (TextTypedSoFarMatchesItem(item, ch, textTypedSoFar))
            {
                return false;
            }

            // Fall back to the default rules for this language's completion service.
            return completionRules.DefaultCommitCharacters.IndexOf(ch) >= 0;
        }

        private bool IsFilterCharacter(char ch, Model model)
        {
            AssertIsForeground();

            if (model == null)
            {
                return false;
            }

            if (model.SelectedItem == model.SuggestionModeItem)
            {
                return char.IsLetterOrDigit(ch);
            }

            var textTypedSoFar = GetTextTypedSoFar(model, model.SelectedItem);
            return IsFilterCharacter(model.SelectedItem, ch, textTypedSoFar);
        }

        private static bool TextTypedSoFarMatchesItem(CompletionItem item, char ch, string textTypedSoFar)
        {
            if (textTypedSoFar.Length > 0)
            {
                return item.DisplayText.StartsWith(textTypedSoFar, StringComparison.CurrentCultureIgnoreCase) ||
                       item.FilterText.StartsWith(textTypedSoFar, StringComparison.CurrentCultureIgnoreCase);
            }

            return false;
        }

        private static bool IsFilterCharacter(CompletionItem item, char ch, string textTypedSoFar)
        {
            // First see if the item has any specific filter rules it wants followed.
            foreach (var rule in item.Rules.FilterCharacterRules)
            {
                switch (rule.Kind)
                {
                    case CharacterSetModificationKind.Add:
                        if (rule.Characters.Contains(ch))
                        {
                            return true;
                        }
                        continue;

                    case CharacterSetModificationKind.Remove:
                        if (rule.Characters.Contains(ch))
                        {
                            return false;
                        }
                        continue;

                    case CharacterSetModificationKind.Replace:
                        return rule.Characters.Contains(ch);
                }
            }

            // general rule: if the filtering text exactly matches the start of the item then it must be a filter character
            if (TextTypedSoFarMatchesItem(item, ch, textTypedSoFar))
            {
                return true;
            }

            return false;
        }

        private string GetTextTypedSoFar(Model model, CompletionItem selectedItem)
        {
            var textSnapshot = this.TextView.TextSnapshot;
            var viewSpan = model.GetViewBufferSpan(selectedItem.Span);
            var filterText = model.GetCurrentTextInSnapshot(
                viewSpan, textSnapshot, GetCaretPointInViewBuffer());
            return filterText;
        }

        private bool CommitIfCommitCharacter(
            char ch, ITextSnapshot initialTextSnapshot, Action nextHandler)
        {
            return CommitIfCommitCharacter(ch, sessionOpt.WaitForModel(), initialTextSnapshot, nextHandler);
        }

        private bool CommitIfCommitCharacter(
            char ch, Model model, ITextSnapshot initialTextSnapshot, Action nextHandler)
        {
            AssertIsForeground();

            // Note: this function is called after the character has already been inserted into the
            // buffer.

            if (!IsCommitCharacter(ch, model))
            {
                return false;
            }

            // We only call CommitOnTypeChar if ch was a commit character.  And we only know if ch
            // was commit character if we had a selected item.
            Contract.ThrowIfNull(model);

            this.Commit(
                model.SelectedItem, model, ch,
                initialTextSnapshot, nextHandler);
            return true;
        }
    }
}