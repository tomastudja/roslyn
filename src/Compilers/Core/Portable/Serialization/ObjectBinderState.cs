﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Roslyn.Utilities
{
    internal struct ObjectBinderState
    {
        public readonly int Version;
        private readonly Dictionary<Type, int> _typeToIndex;
        private ImmutableArray<Type> _types;
        private ImmutableArray<Func<ObjectReader, object>> _typeReaders;

        public ObjectBinderState(
            int version,
            Dictionary<Type, int> typeToIndex,
            ImmutableArray<Type> types,
            ImmutableArray<Func<ObjectReader, object>> typeReaders)
        {
            Version = version;
            _typeToIndex = typeToIndex;
            _types = types;
            _typeReaders = typeReaders;
        }

        //public void CopyFrom(ObjectBinderState other)
        //{
        //    if (_types.Count == 0)
        //    {
        //        Debug.Assert(_typeToIndex.Count == 0);
        //        Debug.Assert(_types.Count == 0);
        //        Debug.Assert(_typeReaders.Count == 0);

        //        foreach (var kvp in other._typeToIndex)
        //        {
        //            _typeToIndex.Add(kvp.Key, kvp.Value);
        //        }

        //        _types.AddRange(other._types);
        //        _typeReaders.AddRange(other._typeReaders);
        //    }
        //}

        public int GetTypeId(Type type)
            => _typeToIndex[type];

        public int GetOrAddTypeId(Type type)
        {
            if (_typeToIndex.TryGetValue(type, out var index))
            {
                return index;
            }

            RegisterTypeReader(type, typeReader: null);
            index = _typeToIndex[type];
            return index;
        }

        public Type GetTypeFromId(int typeId)
            => _types[typeId];

        public Func<ObjectReader, object> GetTypeReaderFromId(int typeId)
            => _typeReaders[typeId];

        public bool RegisterTypeReader(Type type, Func<ObjectReader, object> typeReader)
        {
            if (_typeToIndex.ContainsKey(type))
            {
                // We already knew about this type, nothing to register.
                return false;
            }

            var index = _typeReaders.Length;
            _types = _types.Add(type);
            _typeReaders = _typeReaders.Add(typeReader);
            _typeToIndex.Add(type, index);

            // We may be a local copy of the object-binder-state.  Inform the primary 
            // binder of this new registration.  Note: there is no re-entrancy issue here
            // as we've already updated our local state, so we'll bail out immediately
            // when we get called back through RegisterTypeReader.
            ObjectBinder.RegisterTypeReader(type, typeReader);
            return true;
        }
    }
}