﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.Semantics;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Root type for representing the abstract semantics of C# and VB statements and expressions.
    /// </summary>
    internal abstract class Operation : IOperation
    {
        private readonly SemanticModel _semanticModel;

        // this will be lazily initialized. this will be initialized only once
        // but once initialized, will never change
        private IOperation _parentDoNotAccessDirectly;

        public Operation(OperationKind kind, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, Optional<object> constantValue)
        {
            _semanticModel = semanticModel;

            Kind = kind;
            Syntax = syntax;
            Type = type;
            ConstantValue = constantValue;
        }

        /// <summary>
        /// IOperation that has this operation as a child
        /// </summary>
        public IOperation Parent
        {
            get
            {
                if (_parentDoNotAccessDirectly == null)
                {
                    Interlocked.CompareExchange(ref _parentDoNotAccessDirectly, SearchParentOperation(), null);
                }

                return _parentDoNotAccessDirectly;
            }
        }

        /// <summary>
        /// Identifies the kind of the operation.
        /// </summary>
        public OperationKind Kind { get; }

        /// <summary>
        /// Syntax that was analyzed to produce the operation.
        /// </summary>
        public SyntaxNode Syntax { get; }

        /// <summary>
        /// Result type of the operation, or null if the operation does not produce a result.
        /// </summary>
        public ITypeSymbol Type { get; }

        /// <summary>
        /// If the operation is an expression that evaluates to a constant value, <see cref="Optional{Object}.HasValue"/> is true and <see cref="Optional{Object}.Value"/> is the value of the expression. Otherwise, <see cref="Optional{Object}.HasValue"/> is false.
        /// </summary>
        public Optional<object> ConstantValue { get; }

        public abstract IEnumerable<IOperation> Children { get; }

        public abstract void Accept(OperationVisitor visitor);

        public abstract TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument);

        protected void SetParentOperation(IOperation operation)
        {
            var result = Interlocked.CompareExchange(ref _parentDoNotAccessDirectly, operation, null);
#if DEBUG
            if (result == null)
            {
                // tree must belong to same semantic model
                Debug.Assert(((Operation)operation)._semanticModel == _semanticModel);

                // confirm explicitly given parent is same as what we would have found.
                var found = SearchParentOperation();
                Debug.Assert(operation == found);
            }
#endif
        }

        public static IOperation CreateOperationNone(SemanticModel semanticModel, SyntaxNode node, Optional<object> constantValue, Func<ImmutableArray<IOperation>> getChildren)
        {
            return new NoneOperation(semanticModel, node, constantValue, getChildren);
        }

        public static T SetParentOperation<T>(T operation, IOperation parent) where T : IOperation
        {
            // operation can be null
            if (operation == null)
            {
                return operation;
            }

            // explicit cast is not allowed, so using "as" instead
            (operation as Operation).SetParentOperation(parent);
            return operation;
        }

        public static ImmutableArray<T> SetParentOperation<T>(ImmutableArray<T> operations, IOperation parent) where T : IOperation
        {
            // check quick bail out case first
            if (operations.Length == 0)
            {
                // no element
                return operations;
            }

            // race is okay. paneltiy is going through a loop one more time
            // explicit cast is not allowed, so using "as" instead
            if ((operations[0] as Operation)._parentDoNotAccessDirectly != null)
            {
                // already initialized
                return operations;
            }

            for (var i = 0; i < operations.Length; i++)
            {
                // go through slowest path
                SetParentOperation(operations[i], parent);
            }

            return operations;
        }

        private class NoneOperation : Operation
        {
            private readonly Func<ImmutableArray<IOperation>> _getChildren;

            public NoneOperation(SemanticModel semanticMode, SyntaxNode node, Optional<object> constantValue, Func<ImmutableArray<IOperation>> getChildren) :
                base(OperationKind.None, semanticMode, node, type: null, constantValue: constantValue)
            {
                _getChildren = getChildren;
            }

            public override void Accept(OperationVisitor visitor)
            {
                visitor.VisitNoneOperation(this);
            }

            public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
            {
                return visitor.VisitNoneOperation(this, argument);
            }

            public override IEnumerable<IOperation> Children
            {
                get
                {
                    foreach (var child in _getChildren().NullToEmpty().WhereNotNull())
                    {
                        yield return Operation.SetParentOperation(child, this);
                    }
                }
            }
        }

        private static readonly ObjectPool<Queue<IOperation>> s_queuePool =
            new ObjectPool<Queue<IOperation>>(() => new Queue<IOperation>(), 10);

        private IOperation WalkDownOperationToFindParent(
            HashSet<IOperation> operationAlreadyProcessed, IOperation root, TextSpan span)
        {
            void EnqueueChildOperations(Queue<IOperation> queue, IOperation parent)
            {
                // children can return null
                foreach (var o in parent.Children.WhereNotNull())
                {
                    queue.Enqueue(o);
                }
            }

            var operationQueue = s_queuePool.Allocate();

            try
            {
                EnqueueChildOperations(operationQueue, root);

                // walk down the tree to find parent operation
                // every operation returned by the queue should already have Parent operation set
                while (operationQueue.Count > 0)
                {
                    var operation = operationQueue.Dequeue();

                    if (!operationAlreadyProcessed.Add(operation))
                    {
                        // don't process IOperation we already processed otherwise,
                        // we can walk down same tree multiple times
                        continue;
                    }

                    if (operation == this)
                    {
                        // parent found
                        return operation.Parent;
                    }

                    // It can't filter visiting children by node span since IOperation
                    // might have children which belong to completely different sub tree of
                    // syntax tree

                    // queue children so that we can do breadth first search
                    EnqueueChildOperations(operationQueue, operation);
                }

                return null;
            }
            finally
            {
                operationQueue.Clear();
                s_queuePool.Free(operationQueue);
            }
        }

        private IOperation SearchParentOperation()
        {
            var operationAlreadyProcessed = PooledHashSet<IOperation>.GetInstance();

            var targetNode = Syntax;

            // start from current node since one node can have multiple operations mapped to
            var currentCandidate = targetNode;

            try
            {
                while (currentCandidate != null)
                {
                    Debug.Assert(currentCandidate.FullSpan.IntersectsWith(targetNode.FullSpan));

                    // get operation
                    var tree = _semanticModel.GetOperationInternal(currentCandidate);
                    if (tree != null)
                    {
                        // walk down operation tree to see whether this tree contains parent of this operation
                        var parent = WalkDownOperationToFindParent(operationAlreadyProcessed, tree, targetNode.FullSpan);
                        if (parent != null)
                        {
                            return parent;
                        }
                    }

                    // move up the tree
                    currentCandidate = currentCandidate.Parent;
                }

                // root node. there is no parent
                return null;
            }
            finally
            {
                // put the hashset back to the pool
                operationAlreadyProcessed.Free();
            }
        }
    }
}
