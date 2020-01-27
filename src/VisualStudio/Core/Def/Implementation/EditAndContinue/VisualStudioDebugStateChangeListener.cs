﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#nullable enable

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Debugging;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Clr;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.UI.Interfaces;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.EditAndContinue
{
    [Export(typeof(IDebugStateChangeListener))]
    internal sealed class VisualStudioDebugStateChangeListener : IDebugStateChangeListener
    {
        private readonly Workspace _workspace;
        private readonly IDebuggingWorkspaceService _debuggingService;

        // EnC service or null if EnC is disabled for the debug session.
        private IEditAndContinueWorkspaceService? _encService;

        [ImportingConstructor]
        public VisualStudioDebugStateChangeListener(VisualStudioWorkspace workspace)
        {
            _workspace = workspace;
            _debuggingService = workspace.Services.GetRequiredService<IDebuggingWorkspaceService>();
        }

        /// <summary>
        /// Called by the debugger when a debugging session starts and managed debugging is being used.
        /// </summary>
        public void StartDebugging(DebugSessionOptions options)
        {
            _debuggingService.OnBeforeDebuggingStateChanged(DebuggingState.Design, DebuggingState.Run);

            if ((options & DebugSessionOptions.EditAndContinueDisabled) == 0)
            {
                _encService = _workspace.Services.GetRequiredService<IEditAndContinueWorkspaceService>();
                _encService.StartDebuggingSession();
            }
            else
            {
                _encService = null;
            }
        }

        public void EnterBreakState()
        {
            _debuggingService.OnBeforeDebuggingStateChanged(DebuggingState.Run, DebuggingState.Break);
            _encService?.StartEditSession();
        }

        public void ExitBreakState()
        {
            _debuggingService.OnBeforeDebuggingStateChanged(DebuggingState.Break, DebuggingState.Run);
            _encService?.EndEditSession();
        }

        public void StopDebugging()
        {
            _debuggingService.OnBeforeDebuggingStateChanged(DebuggingState.Run, DebuggingState.Design);
            _encService?.EndDebuggingSession();
        }
    }
}
