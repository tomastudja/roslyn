﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.FlowAnalysis
{
    /// <summary>
    /// PROTOTYPE(dataflow): Add documentation
    /// PROTOTYPE(dataflow): Rename this source file to "BasicBlock.cs"
    /// </summary>
    public sealed class BasicBlock
    {
#if DEBUG
        private bool _sealed;
#endif

        private ControlFlowBranch _lazySuccessor;
        private ControlFlowBranch _lazyConditionalSuccessor;
        private ImmutableArray<ControlFlowBranch> _lazyPredecessors;

        internal BasicBlock(
            BasicBlockKind kind,
            ImmutableArray<IOperation> operations,
            IOperation condition,
            int ordinal,
            bool isReachable,
            ControlFlowRegion region)
        {
            Kind = kind;
            Operations = operations;
            Condition = condition;
            Ordinal = ordinal;
            IsReachable = isReachable;
            Region = region;
        }

        public BasicBlockKind Kind { get; }

        public ImmutableArray<IOperation> Operations { get; }

        public IOperation Condition { get; }

        public ControlFlowBranch FallThroughSuccessor
        {
            get
            {
#if DEBUG
                Debug.Assert(_sealed);
#endif

                return _lazySuccessor;
            }
        }

        public ControlFlowBranch ConditionalSuccessor
        {
            get
            {
#if DEBUG
                Debug.Assert(_sealed);
#endif

                return _lazyConditionalSuccessor;
            }
        }

        public ImmutableArray<ControlFlowBranch> Predecessors
        {
            get
            {
#if DEBUG
                Debug.Assert(_sealed);
#endif
                return _lazyPredecessors;
            }
        }

        public int Ordinal { get; }

        public bool IsReachable { get; }

        /// <summary>
        /// Enclosing region
        /// </summary>
        public ControlFlowRegion Region { get; }

        internal void SetSuccessorsAndPredecessors(ControlFlowBranch successor, ControlFlowBranch conditionalSuccessor, ImmutableArray<ControlFlowBranch> predecessors)
        {
#if DEBUG
            Debug.Assert(!_sealed);
            Debug.Assert(_lazySuccessor == null);
            Debug.Assert(_lazyConditionalSuccessor == null);
            Debug.Assert(_lazyPredecessors.IsDefault);
#endif

            _lazySuccessor = successor;
            _lazyConditionalSuccessor = conditionalSuccessor;
            _lazyPredecessors = predecessors;

#if DEBUG
            _sealed = true;
#endif
        }
    }
}
