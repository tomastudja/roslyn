﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp
{
    class PatternVariableFinder : CSharpSyntaxWalker
    {
        private ArrayBuilder<DeclarationPatternSyntax> declarationPatterns;
        private ArrayBuilder<ExpressionSyntax> expressions = ArrayBuilder<ExpressionSyntax>.GetInstance();
        internal static ArrayBuilder<DeclarationPatternSyntax> FindPatternVariables(
            ExpressionSyntax expression = null,
            ImmutableArray<ExpressionSyntax> expressions = default(ImmutableArray<ExpressionSyntax>),
            ImmutableArray<PatternSyntax> patterns = default(ImmutableArray<PatternSyntax>))
        {
            var finder = s_poolInstance.Allocate();
            finder.declarationPatterns = ArrayBuilder<DeclarationPatternSyntax>.GetInstance();
            var expressionsToProcess = finder.expressions;
            Debug.Assert(expressionsToProcess.Count == 0);

            // push expressions onto the stack to be processed.
            if (expression != null) expressionsToProcess.Add(expression);
            if (!expressions.IsDefaultOrEmpty)
            {
                foreach (var subExpression in expressions)
                {
                    expressionsToProcess.Add(subExpression);
                }
            }
            if (!patterns.IsDefaultOrEmpty)
            {
                foreach (var pattern in patterns)
                {
                    finder.Visit(pattern);
                }
            }
            finder.VisitExpressions();

            var result = finder.declarationPatterns;
            finder.declarationPatterns = null;
            s_poolInstance.Free(finder);
            return result;
        }

        private void VisitExpressions()
        {
            // process expressions from the stack until none remain.
            while (expressions.Count != 0)
            {
                var e = expressions[expressions.Count - 1];
                expressions.RemoveLast();
                Visit(e);
            }
        }

        public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
        {
            declarationPatterns.Add(node);
            base.VisitDeclarationPattern(node);
        }
        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) { }
        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) { }
        public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) { }
        public override void VisitQueryExpression(QueryExpressionSyntax node) { }
        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            // push subexpressions onto the stack to be processed.
            expressions.Add(node.Left);
            expressions.Add(node.Right);
        }
        public override void VisitMatchExpression(MatchExpressionSyntax node)
        {
            Visit(node.Left);
        }

        #region pool
        private static readonly ObjectPool<PatternVariableFinder> s_poolInstance = CreatePool();

        public static ObjectPool<PatternVariableFinder> CreatePool()
        {
            return new ObjectPool<PatternVariableFinder>(() => new PatternVariableFinder(), 10);
        }
        #endregion
    }
}
