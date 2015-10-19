// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Editor.Commands;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;

namespace Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense.SignatureHelp
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

            var allProviders = GetProviders();
            if (allProviders == null)
            {
                return;
            }

            // Note: while we're doing this, we don't want to hear about buffer changes (since we
            // know they're going to happen).  So we disconnect and reconnect to the event
            // afterwards.  That way we can hear about changes to the buffer that don't happen
            // through us.
            this.TextView.TextBuffer.PostChanged -= OnTextViewBufferPostChanged;
            try
            {
                nextHandler();
            }
            finally
            {
                this.TextView.TextBuffer.PostChanged += OnTextViewBufferPostChanged;
            }

            // We only want to process typechar if it is a normal typechar and no one else is
            // involved.  i.e. if there was a typechar, but someone processed it and moved the caret
            // somewhere else then we don't want signature help.  Also, if a character was typed but
            // something intercepted and placed different text into the editor, then we don't want
            // to proceed. 
            //
            // Note: we do not want to pass along a text version here.  It is expected that multiple
            // version changes may happen when we call 'nextHandler' and we will still want to
            // proceed.  For example, if the user types "WriteL(", then that will involve two text
            // changes as completion commits that out to "WriteLine(".  But we still want to provide
            // sig help in this case.
            if (this.TextView.TypeCharWasHandledStrangely(this.SubjectBuffer, args.TypedChar))
            {
                // If we were computing anything, we stop.  We only want to process a typechar
                // if it was a normal character.
                DismissSessionIfActive();
                return;
            }

            // Separate the sig help providers into two buckets; one bucket for those that were triggered
            // by the typed character, and those that weren't.  To keep our queries to a minimum, we first
            // check with the textually triggered providers.  If none of those produced any sig help items
            // then we query the other providers to see if they can produce anything viable.  This takes
            // care of cases where the filtered set of providers didn't provide anything but one of the
            // other providers could still be valid, but doesn't explicitly treat the typed character as
            // a trigger character.
            var filteredProviders = FilterProviders(allProviders, args.TypedChar);
            var textuallyTriggeredProviders = filteredProviders.Item1;
            var untriggeredProviders = filteredProviders.Item2;
            var triggerInfo = new SignatureHelpTriggerInfo(SignatureHelpTriggerReason.TypeCharCommand, args.TypedChar);

            if (!IsSessionActive)
            {
                // No computation at all.  If this is not a trigger character, we just ignore it and
                // stay in this state.  Otherwise, if it's a trigger character, start up a new
                // computation and start computing the model in the background.
                if (textuallyTriggeredProviders.Any())
                {
                    // First create the session that represents that we now have a potential 
                    // signature help list. Then tell it to start computing.
                    StartSession(textuallyTriggeredProviders, triggerInfo);
                    return;
                }
                else
                {
                    // No need to do anything.  Just stay in the state where we have no session.
                    return;
                }
            }
            else
            {
                var computed = false;
                if (allProviders.Any(p => p.IsRetriggerCharacter(args.TypedChar)))
                {
                    // The user typed a character that might close the scope of the current model.
                    // In this case, we should requery all providers.
                    //
                    // e.g.     Math.Max(Math.Min(1,2)$$
                    sessionOpt.ComputeModel(allProviders, new SignatureHelpTriggerInfo(SignatureHelpTriggerReason.RetriggerCommand, triggerInfo.TriggerCharacter));
                    computed = true;
                }

                if (textuallyTriggeredProviders.Any())
                {
                    // The character typed was something like "(".  It can both filter a list if
                    // it was in a string like: Foo(bar, "(
                    //
                    // Or it can trigger a new list. Ask the computation to compute again.
                    sessionOpt.ComputeModel(textuallyTriggeredProviders, untriggeredProviders, triggerInfo);
                    computed = true;
                }

                if (!computed)
                {
                    // A character was typed and we haven't updated our model; do so now.
                    sessionOpt.ComputeModel(allProviders, new SignatureHelpTriggerInfo(SignatureHelpTriggerReason.RetriggerCommand));
                }
            }
        }

        private Tuple<List<ISignatureHelpProvider>, List<ISignatureHelpProvider>> FilterProviders(IList<ISignatureHelpProvider> providers, char ch)
        {
            AssertIsForeground();

            var matchedProviders = new List<ISignatureHelpProvider>();
            var unmatchedProviders = new List<ISignatureHelpProvider>();
            foreach (var provider in providers)
            {
                if (provider.IsTriggerCharacter(ch))
                {
                    matchedProviders.Add(provider);
                }
                else
                {
                    unmatchedProviders.Add(provider);
                }
            }

            return Tuple.Create(matchedProviders, unmatchedProviders);
        }
    }
}
