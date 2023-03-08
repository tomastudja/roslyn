﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.LanguageServices.DocumentOutline
{
    internal sealed partial class DocumentOutlineViewModel
    {
        /// <summary>
        /// Represents the state machine of possible changes that we can encounter in our queue.
        /// </summary>
        private sealed record ViewModelStateDataChange(
            // symbol user is searching for, null if no search is active
            string? SearchText,
            // asks to set the expand or collapse state for all items
            bool? ShouldExpand);
    }
}
