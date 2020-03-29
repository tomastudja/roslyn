﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Utilities
{
    // Helpers that are missing from Dev11 implementation:
    internal static class WeakReferenceExtensions
    {
        [return: MaybeNull]
        public static T GetTarget<T>(this WeakReference<T> reference) where T : class?
        {
            reference.TryGetTarget(out var target);
            return target;
        }

        public static bool IsNull<T>(this WeakReference<T> reference) where T : class?
            => !reference.TryGetTarget(out var target);
    }
}
