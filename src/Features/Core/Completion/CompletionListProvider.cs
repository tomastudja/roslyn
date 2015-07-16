﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Completion
{
    /// <summary>
    /// Extensibility interface for clients who want to participate in completion inside an editor.
    /// </summary>
    internal abstract class CompletionListProvider
    {
        /// <summary>
        /// Implement to register the items and other details for a <see cref="CompletionList"/>
        /// </summary>
        public abstract Task ProduceCompletionListAsync(CompletionListContext context);

        /// <summary>
        /// Returns true if the character at the specific position in the text snapshot should
        /// trigger completion. Implementers of this will be called on the main UI thread and should
        /// only do minimal textual checks to determine if they should be presented.
        /// </summary>
        public abstract bool IsTriggerCharacter(SourceText text, int characterPosition, OptionSet options);

        /// <summary>
        /// The text change that will be made when this item is committed.  The text change includes
        /// both the span of text to replace (respective to the original document text when this
        /// completion item was created) and the text to replace it with.  The span will be adjusted
        /// automatically by the completion engine to fit on the current text using "EdgeInclusive"
        /// semantics.
        /// </summary>
        public virtual TextChange GetTextChange(CompletionItem selectedItem, char? ch = null, string textTypedSoFar = null)
        {
            return new TextChange(selectedItem.FilterSpan, selectedItem.DisplayText);
        }
    }
}
