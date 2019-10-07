﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace Roslyn.Utilities
{
    internal partial class SpecializedCollections
    {
        private partial class Empty
        {
            internal class Enumerable<T> : IEnumerable<T>
            {
                // PERF: cache the instance of enumerator. 
                // accessing a generic static field is kinda slow from here,
                // but since empty enumerables are singletons, there is no harm in having 
                // one extra instance field
                private readonly IEnumerator<T> _enumerator = Enumerator<T>.Instance;

                public IEnumerator<T> GetEnumerator()
                {
                    return _enumerator;
                }

                System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
                {
                    return GetEnumerator();
                }
            }
        }
    }
}
