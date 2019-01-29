﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.Formatting.Rules
{
    /// <summary>
    /// Provide a custom formatting operation provider that can intercept/filter/replace default formatting operations.
    /// </summary>
    /// <remarks>All methods defined in this class can be called concurrently. Must be thread-safe.</remarks>
    internal abstract class AbstractFormattingRule
    {
        /// <summary>
        /// Returns SuppressWrappingIfOnSingleLineOperations under a node either by itself or by
        /// filtering/replacing operations returned by NextOperation
        /// </summary>
        public virtual void AddSuppressOperations(List<SuppressOperation> list, SyntaxNode node, OptionSet optionSet, in NextAction<SuppressOperation> nextOperation)
        {
            nextOperation.Invoke(list);
        }

        /// <summary>
        /// returns AnchorIndentationOperations under a node either by itself or by filtering/replacing operations returned by NextOperation
        /// </summary>
        public virtual void AddAnchorIndentationOperations(List<AnchorIndentationOperation> list, SyntaxNode node, OptionSet optionSet, in NextAction<AnchorIndentationOperation> nextOperation)
        {
            nextOperation.Invoke(list);
        }

        /// <summary>
        /// returns IndentBlockOperations under a node either by itself or by filtering/replacing operations returned by NextOperation
        /// </summary>
        public virtual void AddIndentBlockOperations(List<IndentBlockOperation> list, SyntaxNode node, OptionSet optionSet, in NextAction<IndentBlockOperation> nextOperation)
        {
            nextOperation.Invoke(list);
        }

        /// <summary>
        /// returns AlignTokensOperations under a node either by itself or by filtering/replacing operations returned by NextOperation
        /// </summary>
        public virtual void AddAlignTokensOperations(List<AlignTokensOperation> list, SyntaxNode node, OptionSet optionSet, in NextAction<AlignTokensOperation> nextOperation)
        {
            nextOperation.Invoke(list);
        }

        /// <summary>
        /// returns AdjustNewLinesOperation between two tokens either by itself or by filtering/replacing a operation returned by NextOperation
        /// </summary>
        public virtual AdjustNewLinesOperation GetAdjustNewLinesOperation(SyntaxToken previousToken, SyntaxToken currentToken, OptionSet optionSet, in NextOperation<AdjustNewLinesOperation> nextOperation)
        {
            return nextOperation.Invoke();
        }

        /// <summary>
        /// returns AdjustSpacesOperation between two tokens either by itself or by filtering/replacing a operation returned by NextOperation
        /// </summary>
        public virtual AdjustSpacesOperation GetAdjustSpacesOperation(SyntaxToken previousToken, SyntaxToken currentToken, OptionSet optionSet, in NextOperation<AdjustSpacesOperation> nextOperation)
        {
            return nextOperation.Invoke();
        }
    }
}
