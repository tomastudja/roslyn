﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Snippets
{
    internal readonly struct RoslynLSPSnippetItem
    {
        /// <summary>
        /// The identifier in the snippet that needs to be renamed.
        /// Will be null in the case of the final tab stop location,
        /// the '$0' case.
        /// </summary>
        public readonly string? Identifier;

        /// <summary>
        /// The value associated with the identifier.
        /// EX: if (${1:true})
        ///     {$0
        ///     }
        /// The '1' and '0' are represented by this value.
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// Where we want the caret to end up as the final tab-stop location.
        /// If we can't find a caret position, we return null.
        /// </summary>
        public readonly int? CaretPosition;

        /// <summary>
        /// The spans associated with the identifier that will need to
        /// be converted into LSP formatted strings.
        /// </summary>
        public readonly ImmutableArray<TextSpan> PlaceHolderSpans;

        public RoslynLSPSnippetItem(string? identifier, int priority, int? caretPosition, ImmutableArray<TextSpan> placeholderSpans)
        {
            Identifier = identifier;
            Priority = priority;
            CaretPosition = caretPosition;
            PlaceHolderSpans = placeholderSpans;
        }
    }
}
