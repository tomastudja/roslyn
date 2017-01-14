﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Roslyn.Utilities
{
    /// <summary>
    /// A <see cref="ObjectBinder"/> that records runtime types and object readers during object writing so they
    /// can be used to read back objects later.
    /// </summary>
    /// <remarks>
    /// This binder records runtime types an object readers as a way to avoid needing to describe all serialization types up front
    /// or using reflection to determine them on demand.
    /// </remarks>
    internal sealed class RecordingObjectBinder : ObjectBinder
    {
        private readonly Dictionary<TypeKey, Type> _typeMap_mustLock = new Dictionary<TypeKey, Type>();

        private readonly ConcurrentDictionary<Type, Func<ObjectReader, object>> _readerMap =
            new ConcurrentDictionary<Type, Func<ObjectReader, object>>(concurrencyLevel: 2, capacity: 64);

        public override bool TryGetType(TypeKey key, out Type type)
        {
            lock (_typeMap_mustLock)
            {
                return _typeMap_mustLock.TryGetValue(key, out type);
            }
        }

        public override bool TryGetTypeKey(Type type, out TypeKey key)
        {
            if (base.TryGetTypeKey(type, out key))
            {
                RecordType(type, key);
                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool TryGetReader(Type type, out Func<ObjectReader, object> reader)
        {
            return _readerMap.TryGetValue(type, out reader);
        }

        public override bool TryGetWriter(Object instance, out Action<ObjectWriter, Object> writer)
        {
            RecordReader(instance);
            return base.TryGetWriter(instance, out writer);
        }

        private void RecordType(Type type, TypeKey key)
        {
            if (type != null)
            {
                lock (_typeMap_mustLock)
                {
                    _typeMap_mustLock[key] = type;
                }
            }
        }

        private void RecordReader(object instance)
        {
            if (instance != null)
            {
                var type = instance.GetType();

                TypeKey key;
                if (TryGetTypeKey(type, out key)) // records type as side-effect
                {
                    var readable = instance as IObjectReadable;
                    if (readable != null)
                    {
                        if (_readerMap.ContainsKey(type))
                        {
#if DEBUG
                            lock (_typeMap_mustLock)
                            {
                                Debug.Assert(_typeMap_mustLock.ContainsKey(key));
                            }
#endif
                        }
                        else
                        {
                            _readerMap.TryAdd(type, readable.GetReader());
                        }
                    }
                }
            }
        }
    }
}
