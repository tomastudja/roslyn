﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class LocalRewriter
    {
        public override BoundNode VisitAwaitExpression(BoundAwaitExpression node)
        {
            _sawAwait = true;
            return base.VisitAwaitExpression(node);
        }
    }
}
