﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.FlowAnalysis
{
    /// <summary>
    /// Defines kinds of regions that can be present in a <see cref="ControlFlowGraph"/>
    /// </summary>
    public enum ControlFlowRegionKind
    {
        /// <summary>
        /// A root region encapsulating all <see cref="BasicBlock"/>s in a <see cref="ControlFlowGraph"/>
        /// </summary>
        Root,

        /// <summary>
        /// Region with the only purpose to represent the life-time of locals and nested methods (local functions, lambdas).
        /// Lifetime for a local symbol represents the region within which the local allocation is valid and can be referenced.
        /// Lifetime for a nested method represents the region within which the method can be referenced.
        /// </summary>
        LocalLifetime,

        /// <summary>
        /// Region representing a try region. For example, <see cref="ITryOperation.Body"/>
        /// </summary>
        Try,

        /// <summary>
        /// Region representing <see cref="ICatchClauseOperation.Filter"/>
        /// </summary>
        Filter,

        /// <summary>
        /// Region representing <see cref="ICatchClauseOperation.Handler"/>
        /// </summary>
        Catch,

        /// <summary>
        /// Region representing a union of a <see cref="Filter"/> and the corresponding catch <see cref="Catch"/> regions. 
        /// Doesn't contain any <see cref="BasicBlock"/>s directly.
        /// </summary>
        FilterAndHandler,

        /// <summary>
        /// Region representing a union of a <see cref="Try"/> and all corresponding catch <see cref="Catch"/>
        /// and <see cref="FilterAndHandler"/> regions. Doesn't contain any <see cref="BasicBlock"/>s directly.
        /// </summary>
        TryAndCatch,

        /// <summary>
        /// Region representing <see cref="ITryOperation.Finally"/>
        /// </summary>
        Finally,

        /// <summary>
        /// Region representing a union of a <see cref="Try"/> and corresponding finally <see cref="Finally"/>
        /// region. Doesn't contain any <see cref="BasicBlock"/>s directly.
        /// 
        /// An <see cref="ITryOperation"/> that has a set of <see cref="ITryOperation.Catches"/> and a <see cref="ITryOperation.Finally"/> 
        /// at the same time is mapped to a <see cref="TryAndFinally"/> region with <see cref="TryAndCatch"/> region inside its <see cref="Try"/> region.
        /// </summary>
        TryAndFinally,

        /// <summary>
        /// Region representing the initialization for a VB <code>Static</code> local variable. This region will only be executed
        /// the first time a function is called.
        /// </summary>
        StaticLocalInitializer,

        /// <summary>
        /// Region representing erroneous block of code that is unreachable from the entry block.
        /// </summary>
        ErroneousBody,
    }
}
