﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.InlineHints;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.InlineHints
{
    /// <summary>
    /// Key processor that allows us to toggle inline hints when a user hits ctrl-alt.
    /// </summary>
    [Export(typeof(IKeyProcessorProvider))]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [Name(nameof(InlineHintsKeyProcessorProvider))]
    [Order(Before = "default")]
    internal class InlineHintsKeyProcessorProvider : IKeyProcessorProvider
    {
        private static readonly ConditionalWeakTable<IWpfTextView, InlineHintsKeyProcessor> s_viewToProcessor = new();

        private readonly IGlobalOptionService _globalOptionService;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InlineHintsKeyProcessorProvider(IGlobalOptionService globalOptionService)
        {
            _globalOptionService = globalOptionService;
        }

        public KeyProcessor GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return s_viewToProcessor.GetValue(wpfTextView, v => new InlineHintsKeyProcessor(_globalOptionService, v));
        }

        private class InlineHintsKeyProcessor : KeyProcessor
        {
            private readonly IGlobalOptionService _globalOptionService;
            private readonly IWpfTextView _view;

            public InlineHintsKeyProcessor(IGlobalOptionService globalOptionService, IWpfTextView view)
            {
                _globalOptionService = globalOptionService;
                _view = view;
                _view.Closed += OnViewClosed;
                _view.LostAggregateFocus += OnLostFocus;
            }

            private static bool IsCtrlOrAlt(KeyEventArgs args)
                => args.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt;

            private Document? GetDocument()
            {
                var document =
                    _view.BufferGraph.GetTextBuffers(b => true)
                                     .Select(b => b.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges())
                                     .WhereNotNull()
                                     .FirstOrDefault();

                return document;
            }

            private void OnViewClosed(object sender, EventArgs e)
            {
                // Disconnect our callbacks.
                _view.Closed -= OnViewClosed;
                _view.LostAggregateFocus -= OnLostFocus;

                // Go back to off-mode just so we don't somehow get stuck in on-mode if the option was on when the view closed.
                ToggleOff();
            }

            private void OnLostFocus(object sender, EventArgs e)
            {
                // if focus is lost (which can happen for shortcuts that include ctrl-alt...) then go back to normal
                // inline-hint processing.
                ToggleOff();
            }

            public override void KeyDown(KeyEventArgs args)
            {
                base.KeyDown(args);

                // if this is either the ctrl or alt key, and only ctrl-alt is down, then toggle on. 
                // otherwise toggle off if anything else is pressed down.
                if (IsCtrlOrAlt(args))
                {
                    if (args.KeyboardDevice.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                    {
                        ToggleOn();
                        return;
                    }
                }

                ToggleOff();
            }

            public override void KeyUp(KeyEventArgs args)
            {
                base.KeyUp(args);

                // If we've lifted a key up, then turn off the inline hints.
                ToggleOff();
            }

            private void ToggleOn()
                => Toggle(on: true);

            private void ToggleOff()
                => Toggle(on: false);

            private void Toggle(bool on)
            {
                // Only relevant if this is a roslyn document.
                var document = GetDocument();
                if (document == null)
                    return;

                // No need to do anything if we're already in the requested state
                var state = _globalOptionService.GetOption(InlineHintsOptions.DisplayAllOverride);
                if (state == on)
                    return;

                // We can only enter the on-state if the user has the ctrl-alt feature enabled.  We can always enter the
                // off state though.
                on = on && _globalOptionService.GetOption(InlineHintsOptions.DisplayAllHintsWhilePressingCtrlAlt, document.Project.Language);
                _globalOptionService.RefreshOption(new OptionKey(InlineHintsOptions.DisplayAllOverride), on);
            }
        }
    }
}
