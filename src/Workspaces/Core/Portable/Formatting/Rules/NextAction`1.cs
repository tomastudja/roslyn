﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Formatting.Rules
{
    /// <summary>
    /// Represents a next operation to run in a continuation style chaining.
    /// </summary>
    internal readonly struct NextAction<TArgument>
    {
        private readonly int _index;
        private readonly SyntaxNode _node;
        private readonly ActionCache<TArgument> _actionCache;

        public NextAction(int index, SyntaxNode node, in ActionCache<TArgument> actionCache)
        {
            _index = index;
            _node = node;
            _actionCache = actionCache;
        }

        public void Invoke(List<TArgument> arguments)
        {
            _actionCache.Continuation(_index, arguments, _node, _actionCache);
        }
    }
}
