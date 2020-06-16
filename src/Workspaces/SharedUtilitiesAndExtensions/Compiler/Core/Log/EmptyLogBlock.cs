﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CodeAnalysis.Internal.Log
{
    /// <summary>
    /// no op log block
    /// </summary>
    internal sealed class EmptyLogBlock : IDisposable
    {
        public static readonly EmptyLogBlock Instance = new EmptyLogBlock();

        public void Dispose()
        {
        }
    }
}
