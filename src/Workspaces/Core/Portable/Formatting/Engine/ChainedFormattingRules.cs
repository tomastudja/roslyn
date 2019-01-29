﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Formatting.Rules;
using Microsoft.CodeAnalysis.Options;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Formatting
{
    internal class ChainedFormattingRules
    {
        private readonly List<AbstractFormattingRule> _formattingRules;
        private readonly OptionSet _optionSet;

        // Operation func caches
        //
        // we cache funcs to all operations kind to make sure we don't allocate any heap memory
        // during invocation of each method with continuation style.
        //
        // each of these operations will be called hundreds of thousands times during formatting,
        // make sure it doesn't allocate any memory during invocations.
        private readonly ActionCache<SuppressOperation> _suppressWrappingFuncCache;
        private readonly ActionCache<AnchorIndentationOperation> _anchorFuncCache;
        private readonly ActionCache<IndentBlockOperation> _indentFuncCache;
        private readonly ActionCache<AlignTokensOperation> _alignFuncCache;
        private readonly OperationCache<AdjustNewLinesOperation> _newLinesFuncCache;
        private readonly OperationCache<AdjustSpacesOperation> _spaceFuncCache;

        public ChainedFormattingRules(IEnumerable<AbstractFormattingRule> formattingRules, OptionSet set)
        {
            Contract.ThrowIfNull(formattingRules);
            Contract.ThrowIfNull(set);

            _formattingRules = formattingRules.ToList();
            _optionSet = set;

            // cache all funcs to reduce heap allocations
            _suppressWrappingFuncCache = new ActionCache<SuppressOperation>(
                (int index, List<SuppressOperation> list, SyntaxNode node, in NextAction<SuppressOperation> next) => _formattingRules[index].AddSuppressOperations(list, node, _optionSet, in next),
                this.AddContinuedOperations);

            _anchorFuncCache = new ActionCache<AnchorIndentationOperation>(
                (int index, List<AnchorIndentationOperation> list, SyntaxNode node, in NextAction<AnchorIndentationOperation> next) => _formattingRules[index].AddAnchorIndentationOperations(list, node, _optionSet, in next),
                this.AddContinuedOperations);

            _indentFuncCache = new ActionCache<IndentBlockOperation>(
                (int index, List<IndentBlockOperation> list, SyntaxNode node, in NextAction<IndentBlockOperation> next) => _formattingRules[index].AddIndentBlockOperations(list, node, _optionSet, in next),
                this.AddContinuedOperations);

            _alignFuncCache = new ActionCache<AlignTokensOperation>(
                (int index, List<AlignTokensOperation> list, SyntaxNode node, in NextAction<AlignTokensOperation> next) => _formattingRules[index].AddAlignTokensOperations(list, node, _optionSet, in next),
                this.AddContinuedOperations);

            _newLinesFuncCache = new OperationCache<AdjustNewLinesOperation>(
                (int index, SyntaxToken token1, SyntaxToken token2, in NextOperation<AdjustNewLinesOperation> next) => _formattingRules[index].GetAdjustNewLinesOperation(token1, token2, _optionSet, in next),
                this.GetContinuedOperations);

            _spaceFuncCache = new OperationCache<AdjustSpacesOperation>(
                (int index, SyntaxToken token1, SyntaxToken token2, in NextOperation<AdjustSpacesOperation> next) => _formattingRules[index].GetAdjustSpacesOperation(token1, token2, _optionSet, in next),
                this.GetContinuedOperations);
        }

        public void AddSuppressOperations(List<SuppressOperation> list, SyntaxNode currentNode)
        {
            AddContinuedOperations(0, list, currentNode, _suppressWrappingFuncCache);
        }

        public void AddAnchorIndentationOperations(List<AnchorIndentationOperation> list, SyntaxNode currentNode)
        {
            AddContinuedOperations(0, list, currentNode, _anchorFuncCache);
        }

        public void AddIndentBlockOperations(List<IndentBlockOperation> list, SyntaxNode currentNode)
        {
            AddContinuedOperations(0, list, currentNode, _indentFuncCache);
        }

        public void AddAlignTokensOperations(List<AlignTokensOperation> list, SyntaxNode currentNode)
        {
            AddContinuedOperations(0, list, currentNode, _alignFuncCache);
        }

        public AdjustNewLinesOperation GetAdjustNewLinesOperation(SyntaxToken previousToken, SyntaxToken currentToken)
        {
            return GetContinuedOperations(0, previousToken, currentToken, _newLinesFuncCache);
        }

        public AdjustSpacesOperation GetAdjustSpacesOperation(SyntaxToken previousToken, SyntaxToken currentToken)
        {
            return GetContinuedOperations(0, previousToken, currentToken, _spaceFuncCache);
        }

        private void AddContinuedOperations<TArg1>(int index, List<TArg1> arg1, SyntaxNode node, in ActionCache<TArg1> actionCache)
        {
            // If we have no remaining handlers to execute, then we'll execute our last handler
            if (index >= _formattingRules.Count)
            {
                return;
            }
            else
            {
                // Call the handler at the index, passing a continuation that will come back to here with index + 1
                var continuation = new NextAction<TArg1>(index + 1, node, actionCache);
                actionCache.NextOperation(index, arg1, node, in continuation);
                return;
            }
        }

        private TResult GetContinuedOperations<TResult>(int index, SyntaxToken token1, SyntaxToken token2, in OperationCache<TResult> funcCache)
        {
            // If we have no remaining handlers to execute, then we'll execute our last handler
            if (index >= _formattingRules.Count)
            {
                return default;
            }
            else
            {
                // Call the handler at the index, passing a continuation that will come back to here with index + 1
                var continuation = new NextOperation<TResult>(index + 1, token1, token2, funcCache);
                return funcCache.NextOperation(index, token1, token2, in continuation);
            }
        }
    }
}
