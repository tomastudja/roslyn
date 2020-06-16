﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CodeAnalysis.Shared.Extensions
{
    internal static class IComparerExtensions
    {
        public static IComparer<T> Inverse<T>(this IComparer<T> comparer)
        {
            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            return new InverseComparer<T>(comparer);
        }

        private class InverseComparer<T> : IComparer<T>
        {
            private readonly IComparer<T> _comparer;

            internal InverseComparer(IComparer<T> comparer)
                => _comparer = comparer;

            public int Compare([AllowNull] T x, [AllowNull] T y)
                => _comparer.Compare(y, x);
        }
    }
}
