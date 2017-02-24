﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.CodeAnalysis.Editor.Commands
{
    /// <summary>
    /// Arguments for a character being typed.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal class TypeCharCommandArgs : CommandArgs
    {
        private readonly char _typedChar;

        public TypeCharCommandArgs(ITextView textView, ITextBuffer subjectBuffer, char typedChar)
            : base(textView, subjectBuffer)
        {
            _typedChar = typedChar;
        }

        /// <summary>
        /// The character that was typed.
        /// </summary>
        public char TypedChar => _typedChar;
    }
}
