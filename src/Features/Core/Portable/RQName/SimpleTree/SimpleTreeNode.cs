﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.CodeAnalysis.Features.RQName.SimpleTree
{
    internal abstract class SimpleTreeNode
    {
        public readonly string Text;

        public SimpleTreeNode(string text)
            => Text = text;
    }
}
