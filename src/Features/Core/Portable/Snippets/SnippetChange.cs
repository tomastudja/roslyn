﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Snippets
{
    /// <summary>
    /// Encapsulates the information that makes up a Snippet.
    /// </summary>
    internal readonly struct SnippetChange
    {
        /// <summary>
        /// The primary text change.
        /// This will be the change that has associated renaming and tab stops.
        /// </summary>
        public readonly TextChange? MainTextChange;

        /// <summary>
        /// The TextChange's associated with introducing a snippet into a document
        /// </summary>
        public readonly ImmutableArray<TextChange> TextChanges;

        /// <summary>
        /// The position that the cursor should end up on
        /// </summary>
        public readonly int? CursorPosition;

        public readonly Dictionary<string, List<TextSpan>> RenameAndLocationsMap;

        public SnippetChange(
            TextChange? mainTextChange,
            ImmutableArray<TextChange> textChanges,
            int? cursorPosition
            Dictionary<string, List<TextSpan>>)
        {
            if (textChanges.IsEmpty || mainTextChange is null)
            {
                throw new ArgumentException($"{ textChanges.Length } must not be empty");
            }

            MainTextChange = mainTextChange;
            TextChanges = textChanges;
            CursorPosition = cursorPosition;
        }
    }
}
