﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
    public interface ITestErrorHandler
    {
        /// <summary>
        /// Records unexpected exceptions thrown during test executino that can't be immediately
        /// reported.
        /// </summary>
        ImmutableList<Exception> Exceptions { get; }
    }
}
