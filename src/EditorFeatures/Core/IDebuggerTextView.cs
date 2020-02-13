﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.CodeAnalysis.Editor
{
    internal interface IDebuggerTextView
    {
        bool IsImmediateWindow { get; }

        void HACK_StartCompletionSession(IIntellisenseSession editorSessionOpt);

        uint StartBufferUpdate();
        void EndBufferUpdate(uint cookie);
    }
}
