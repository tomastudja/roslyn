﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Roslyn.Utilities
{
#if COMPILERCORE
    using Resources = CodeAnalysisResources;
#else
    using Resources = WorkspacesResources;
#endif

    using SOW = ObjectWriter;
    using EncodingKind = ObjectWriter.EncodingKind;
    using Variant = ObjectWriter.Variant;
    using VariantKind = ObjectWriter.VariantKind;

    /// <summary>
    /// An <see cref="ObjectReader"/> that deserializes objects from a byte stream.
    /// </summary>
    internal sealed partial class ObjectReader : IDisposable
    {
        /// <summary>
        /// We start the version at something reasonably random.  That way an older file, with 
        /// some random start-bytes, has little chance of matching our version.  When incrementing
        /// this version, just change VersionByte2.
        /// </summary>
        internal const byte VersionByte1 = 0b10101010;
        internal const byte VersionByte2 = 0b00000110;

        private readonly BinaryReader _reader;
        private readonly bool _recursive;
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// Map of reference id's to deserialized objects.
        /// </summary>
        private readonly ReaderReferenceMap<object> _objectReferenceMap;
        private readonly ReaderReferenceMap<string> _stringReferenceMap;

        /// <summary>
        /// Stack of values used to construct objects and arrays
        /// </summary>
        private readonly Stack<Variant> _valueStack;

        /// <summary>
        /// Stack of pending object and array constructions
        /// </summary>
        private readonly Stack<Construction> _constructionStack;

        /// <summary>
        /// List of member values that object deserializers read from.
        /// </summary>
        private readonly ImmutableArray<Variant>.Builder _memberList;
        private int _indexInMemberList;

        private static readonly ObjectPool<Stack<Construction>> s_constructionStackPool
            = new ObjectPool<Stack<Construction>>(() => new Stack<Construction>(20));

        /// <summary>
        /// Creates a new instance of a <see cref="ObjectReader"/>.
        /// </summary>
        /// <param name="stream">The stream to read objects from.</param>
        /// <param name="knownObjects">An optional list of objects assumed known by the corresponding <see cref="ObjectWriter"/>.</param>
        /// <param name="cancellationToken"></param>
        private ObjectReader(
            Stream stream,
            ObjectData knownObjects,
            CancellationToken cancellationToken)
        {
            // String serialization assumes both reader and writer to be of the same endianness.
            // It can be adjusted for BigEndian if needed.
            Debug.Assert(BitConverter.IsLittleEndian);

            _recursive = IsRecursive(stream);

            _reader = new BinaryReader(stream, Encoding.UTF8);
            _objectReferenceMap = new ReaderReferenceMap<object>(knownObjects);
            _stringReferenceMap = new ReaderReferenceMap<string>(knownObjects);
            _cancellationToken = cancellationToken;

            _memberList = SOW.s_variantListPool.Allocate();

            if (!_recursive)
            {
                _valueStack = SOW.s_variantStackPool.Allocate();
                _constructionStack = s_constructionStackPool.Allocate();
            }
        }

        /// <summary>
        /// Attempts to create a <see cref="ObjectReader"/> from the provided <paramref name="stream"/>.
        /// If the <paramref name="stream"/> does not start with a valid header, then <code>null</code> will
        /// be returned.
        /// </summary>
        public static ObjectReader TryGetReader(
            Stream stream,
            ObjectData knownObjects = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null)
            {
                return null;
            }

            if (stream.ReadByte() != VersionByte1 ||
                stream.ReadByte() != VersionByte2)
            {
                return null;
            }

            return new ObjectReader(stream, knownObjects, cancellationToken);
        }

        internal static bool IsRecursive(Stream stream)
        {
            var recursionKind = (EncodingKind)stream.ReadByte(); 
            switch (recursionKind)
            {
                case EncodingKind.Recursive:
                    return true;
                case EncodingKind.NonRecursive:
                    return false;
                default:
                    throw ExceptionUtilities.UnexpectedValue(recursionKind);
            }
        }

        public void Dispose()
        {
            _objectReferenceMap.Dispose();
            _stringReferenceMap.Dispose();

            ResetMemberList();
            SOW.s_variantListPool.Free(_memberList);

            if (!_recursive)
            {
                _valueStack.Clear();
                SOW.s_variantStackPool.Free(_valueStack);

                _constructionStack.Clear();
                s_constructionStackPool.Free(_constructionStack);
            }
        }

        private void ResetMemberList()
        {
            _memberList.Clear();
            _indexInMemberList = 0;
        }

        private Variant NextFromMemberList()
            => _memberList[_indexInMemberList++];

        private bool ShouldReadFromMemberList => _memberList.Count > 0;

        public bool ReadBoolean()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsBoolean()
                : _reader.ReadBoolean();

        public byte ReadByte()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsByte()
                : _reader.ReadByte();

        // read as ushort because BinaryWriter fails on chars that are unicode surrogates
        public char ReadChar()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsChar()
                : (char)_reader.ReadUInt16();

        public decimal ReadDecimal()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsDecimal()
                : _reader.ReadDecimal();

        public double ReadDouble()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsDouble()
                : _reader.ReadDouble();

        public float ReadSingle()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsSingle()
                : _reader.ReadSingle();

        public int ReadInt32()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsInt32()
                : _reader.ReadInt32();

        public long ReadInt64()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsInt64()
                : _reader.ReadInt64();

        public sbyte ReadSByte()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsSByte()
                : _reader.ReadSByte();

        public short ReadInt16()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsInt16()
                : _reader.ReadInt16();

        public uint ReadUInt32()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsUInt32()
                : _reader.ReadUInt32();

        public ulong ReadUInt64()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsUInt64()
                : _reader.ReadUInt64();

        public ushort ReadUInt16()
            => ShouldReadFromMemberList
                ? NextFromMemberList().AsUInt16()
                : _reader.ReadUInt16();

        public string ReadString()
            => ShouldReadFromMemberList
                ? ReadStringFromMemberList()
                : ReadStringValue();

        public object ReadValue()
        {
            if (ShouldReadFromMemberList)
            {
                return ReadValueFromMemberList();
            }

            var v = ReadVariant();

            // if we didn't get anything, it must have been an object or array header
            if (!_recursive && v.Kind == VariantKind.None)
            {
                v = ConstructFromValues();
            }

            return v.ToBoxedObject();
        }

        private string ReadStringFromMemberList()
        {
            var next = NextFromMemberList();
            return next.Kind == VariantKind.Null
                ? null
                : next.AsString();
        }

        private object ReadValueFromMemberList()
            => NextFromMemberList().ToBoxedObject();

        private Variant ConstructFromValues()
        {
            Debug.Assert(_constructionStack.Count > 0);

            // keep reading until we've got all the values needed to construct the object or array
            while (_constructionStack.Count > 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var construction = _constructionStack.Peek();
                if (construction.CanConstruct(_valueStack.Count))
                {
                    construction = _constructionStack.Pop();
                    var constructed = construction.Construct(this);
                    _valueStack.Push(constructed);
                }
                else
                {
                    var element = ReadVariant();
                    if (element.Kind != VariantKind.None)
                    {
                        _valueStack.Push(element);
                    }
                }
            }

            Debug.Assert(_valueStack.Count == 1);
            return _valueStack.Pop();
        }

        /// <summary>
        /// Represents either a pending object or array construction.
        /// </summary>
        private struct Construction
        {
            /// <summary>
            /// The type of the object or the element type of the array.
            /// </summary>
            private readonly Type _type;

            /// <summary>
            /// The number of values that must appear on the value stack (beyond the stack start) in order to 
            /// trigger construction of the object or array instance.
            /// </summary>
            private readonly int _valueCount;

            /// <summary>
            /// The size of the value stack before we started this construction.
            /// Only new values pushed onto the stack are used for this construction.
            /// </summary>
            private readonly int _stackStart;

            /// <summary>
            /// The reader that constructs the object. Null if the construction is for an array.
            /// </summary>
            private readonly Func<ObjectReader, object> _reader;

            /// <summary>
            /// The reference id of the object being constructed.
            /// </summary>
            private readonly int _id;

            private Construction(Type type, int valueCount, int stackStart, Func<ObjectReader, object> reader, int id)
            {
                _type = type;
                _valueCount = valueCount;
                _stackStart = stackStart;
                _reader = reader;
                _id = id;
            }

            public bool CanConstruct(int stackCount)
            {
                return stackCount == _stackStart + _valueCount;
            }

            public static Construction CreateObjectConstruction(Type type, int memberCount, int stackStart, Func<ObjectReader, object> reader, int id)
            {
                Debug.Assert(type != null);
                Debug.Assert(reader != null);
                return new Construction(type, memberCount, stackStart, reader, id);
            }

            public static Construction CreateArrayConstruction(Type elementType, int elementCount, int stackStart)
            {
                Debug.Assert(elementType != null);
                return new Construction(elementType, elementCount, stackStart, reader: null, id: 0);
            }

            public Variant Construct(ObjectReader reader)
            {
                if (_reader != null)
                {
                    return reader.ConstructObject(_type, _valueCount, _reader, _id);
                }
                else
                {
                    return reader.ConstructArray(_type, _valueCount);
                }
            }
        }

        private Variant ReadVariant()
        {
            var kind = (EncodingKind)_reader.ReadByte();
            switch (kind)
            {
                case EncodingKind.Null:
                    return Variant.Null;
                case EncodingKind.Boolean_True:
                    return Variant.FromBoolean(true);
                case EncodingKind.Boolean_False:
                    return Variant.FromBoolean(false);
                case EncodingKind.Int8:
                    return Variant.FromSByte(_reader.ReadSByte());
                case EncodingKind.UInt8:
                    return Variant.FromByte(_reader.ReadByte());
                case EncodingKind.Int16:
                    return Variant.FromInt16(_reader.ReadInt16());
                case EncodingKind.UInt16:
                    return Variant.FromUInt16(_reader.ReadUInt16());
                case EncodingKind.Int32:
                    return Variant.FromInt32(_reader.ReadInt32());
                case EncodingKind.Int32_1Byte:
                    return Variant.FromInt32((int)_reader.ReadByte());
                case EncodingKind.Int32_2Bytes:
                    return Variant.FromInt32((int)_reader.ReadUInt16());
                case EncodingKind.Int32_0:
                case EncodingKind.Int32_1:
                case EncodingKind.Int32_2:
                case EncodingKind.Int32_3:
                case EncodingKind.Int32_4:
                case EncodingKind.Int32_5:
                case EncodingKind.Int32_6:
                case EncodingKind.Int32_7:
                case EncodingKind.Int32_8:
                case EncodingKind.Int32_9:
                case EncodingKind.Int32_10:
                    return Variant.FromInt32((int)kind - (int)EncodingKind.Int32_0);
                case EncodingKind.UInt32:
                    return Variant.FromUInt32(_reader.ReadUInt32());
                case EncodingKind.UInt32_1Byte:
                    return Variant.FromUInt32((uint)_reader.ReadByte());
                case EncodingKind.UInt32_2Bytes:
                    return Variant.FromUInt32((uint)_reader.ReadUInt16());
                case EncodingKind.UInt32_0:
                case EncodingKind.UInt32_1:
                case EncodingKind.UInt32_2:
                case EncodingKind.UInt32_3:
                case EncodingKind.UInt32_4:
                case EncodingKind.UInt32_5:
                case EncodingKind.UInt32_6:
                case EncodingKind.UInt32_7:
                case EncodingKind.UInt32_8:
                case EncodingKind.UInt32_9:
                case EncodingKind.UInt32_10:
                    return Variant.FromUInt32((uint)((int)kind - (int)EncodingKind.UInt32_0));
                case EncodingKind.Int64:
                    return Variant.FromInt64(_reader.ReadInt64());
                case EncodingKind.UInt64:
                    return Variant.FromUInt64(_reader.ReadUInt64());
                case EncodingKind.Float4:
                    return Variant.FromSingle(_reader.ReadSingle());
                case EncodingKind.Float8:
                    return Variant.FromDouble(_reader.ReadDouble());
                case EncodingKind.Decimal:
                    return Variant.FromDecimal(_reader.ReadDecimal());
                case EncodingKind.Char:
                    // read as ushort because BinaryWriter fails on chars that are unicode surrogates
                    return Variant.FromChar((char)_reader.ReadUInt16());
                case EncodingKind.StringUtf8:
                case EncodingKind.StringUtf16:
                case EncodingKind.StringRef_4Bytes:
                case EncodingKind.StringRef_1Byte:
                case EncodingKind.StringRef_2Bytes:
                    return Variant.FromString(ReadStringValue(kind));
                case EncodingKind.ObjectRef_4Bytes:
                    return Variant.FromObject(_objectReferenceMap.GetValue(_reader.ReadInt32()));
                case EncodingKind.ObjectRef_1Byte:
                    return Variant.FromObject(_objectReferenceMap.GetValue(_reader.ReadByte()));
                case EncodingKind.ObjectRef_2Bytes:
                    return Variant.FromObject(_objectReferenceMap.GetValue(_reader.ReadUInt16()));
                case EncodingKind.Object:
                    return ReadObject();
                case EncodingKind.Type:
                    return Variant.FromType(ReadType());
                case EncodingKind.DateTime:
                    return Variant.FromDateTime(DateTime.FromBinary(_reader.ReadInt64()));
                case EncodingKind.Array:
                case EncodingKind.Array_0:
                case EncodingKind.Array_1:
                case EncodingKind.Array_2:
                case EncodingKind.Array_3:
                    return ReadArray(kind);
                default:
                    throw ExceptionUtilities.UnexpectedValue(kind);
            }
        }

        /// <summary>
        /// An reference-id to object map, that can share base data efficiently.
        /// </summary>
        private class ReaderReferenceMap<T> where T : class
        {
            private readonly ObjectData _baseData;
            private readonly int _baseDataCount;
            private readonly List<T> _values;

            internal static readonly ObjectPool<List<T>> s_objectListPool
                = new ObjectPool<List<T>>(() => new List<T>(20));

            public ReaderReferenceMap(ObjectData baseData)
            {
                _baseData = baseData;
                _baseDataCount = baseData != null ? _baseData.Objects.Length : 0;
                _values = s_objectListPool.Allocate();
            }

            public void Dispose()
            {
                _values.Clear();
                s_objectListPool.Free(_values);
            }

            public int GetNextReferenceId()
            {
                _values.Add(null);
                return _baseDataCount + _values.Count - 1;
            }

            public void SetValue(int referenceId, T value)
            {
                _values[referenceId - _baseDataCount] = value;
            }

            public T GetValue(int referenceId)
            {
                if (_baseData != null)
                {
                    if (referenceId < _baseDataCount)
                    {
                        return (T)_baseData.Objects[referenceId];
                    }
                    else
                    {
                        return _values[referenceId - _baseDataCount];
                    }
                }
                else
                {
                    return _values[referenceId];
                }
            }
        }

        internal uint ReadCompressedUInt()
        {
            var info = _reader.ReadByte();
            byte marker = (byte)(info & ObjectWriter.ByteMarkerMask);
            byte byte0 = (byte)(info & ~ObjectWriter.ByteMarkerMask);

            if (marker == ObjectWriter.Byte1Marker)
            {
                return byte0;
            }

            if (marker == ObjectWriter.Byte2Marker)
            {
                var byte1 = _reader.ReadByte();
                return (((uint)byte0) << 8) | byte1;
            }

            if (marker == ObjectWriter.Byte4Marker)
            {
                var byte1 = _reader.ReadByte();
                var byte2 = _reader.ReadByte();
                var byte3 = _reader.ReadByte();

                return (((uint)byte0) << 24) | (((uint)byte1) << 16) | (((uint)byte2) << 8) | byte3;
            }

            throw ExceptionUtilities.UnexpectedValue(marker);
        }

        private string ReadStringValue()
        {
            var kind = (EncodingKind)_reader.ReadByte();
            return kind == EncodingKind.Null ? null : ReadStringValue(kind);
        }

        private string ReadStringValue(EncodingKind kind)
        {
            switch (kind)
            {
                case EncodingKind.StringRef_1Byte:
                    return _stringReferenceMap.GetValue(_reader.ReadByte());

                case EncodingKind.StringRef_2Bytes:
                    return _stringReferenceMap.GetValue(_reader.ReadUInt16());

                case EncodingKind.StringRef_4Bytes:
                    return _stringReferenceMap.GetValue(_reader.ReadInt32());

                case EncodingKind.StringUtf16:
                case EncodingKind.StringUtf8:
                    return ReadStringLiteral(kind);

                default:
                    throw ExceptionUtilities.UnexpectedValue(kind);
            }
        }

        private unsafe string ReadStringLiteral(EncodingKind kind)
        {
            int id = _stringReferenceMap.GetNextReferenceId();
            string value;
            if (kind == EncodingKind.StringUtf8)
            {
                value = _reader.ReadString();
            }
            else
            {
                // This is rare, just allocate UTF16 bytes for simplicity.
                int characterCount = (int)ReadCompressedUInt();
                byte[] bytes = _reader.ReadBytes(characterCount * sizeof(char));
                fixed (byte* bytesPtr = bytes)
                {
                    value = new string((char*)bytesPtr, 0, characterCount);
                }
            }

            _stringReferenceMap.SetValue(id, value);
            return value;
        }

        private Variant ReadArray(EncodingKind kind)
        {
            int length;
            switch (kind)
            {
                case EncodingKind.Array_0:
                    length = 0;
                    break;
                case EncodingKind.Array_1:
                    length = 1;
                    break;
                case EncodingKind.Array_2:
                    length = 2;
                    break;
                case EncodingKind.Array_3:
                    length = 3;
                    break;
                default:
                    length = (int)this.ReadCompressedUInt();
                    break;
            }

            // SUBTLE: If it was a primitive array, only the EncodingKind byte of the element type was written, instead of encoding as a type.
            var elementKind = (EncodingKind)_reader.ReadByte();

            var elementType = ObjectWriter.s_reverseTypeMap[(int)elementKind];
            if (elementType != null)
            {
                return Variant.FromArray(this.ReadPrimitiveTypeArrayElements(elementType, elementKind, length));
            }
            else
            {
                // custom type case
                elementType = this.ReadType();

                if (_recursive)
                {
                    // recursive: create instance and read elements next in stream
                    Array array = Array.CreateInstance(elementType, length);

                    for (int i = 0; i < length; ++i)
                    {
                        var value = this.ReadValue();
                        array.SetValue(value, i);
                    }

                    return Variant.FromObject(array);
                }
                else
                {
                    // non-recursive: remember construction info to be used later when all elements are available
                    _constructionStack.Push(Construction.CreateArrayConstruction(elementType, length, _valueStack.Count));
                    return Variant.None;
                }
            }
        }

        private Variant ConstructArray(Type elementType, int length)
        {
            Array array = Array.CreateInstance(elementType, length);

            // values are on stack in reverse order
            for (int i = length - 1; i >= 0; i--)
            {
                var value = _valueStack.Pop().ToBoxedObject();
                array.SetValue(value, i);
            }

            return Variant.FromArray(array);
        }

        private Array ReadPrimitiveTypeArrayElements(Type type, EncodingKind kind, int length)
        {
            Debug.Assert(ObjectWriter.s_reverseTypeMap[(int)kind] == type);

            // optimizations for supported array type by binary reader
            if (type == typeof(byte)) { return _reader.ReadBytes(length); }
            if (type == typeof(char)) { return _reader.ReadChars(length); }

            // optimizations for string where object reader/writer has its own mechanism to
            // reduce duplicated strings
            if (type == typeof(string)) { return ReadStringArrayElements(CreateArray<string>(length)); }
            if (type == typeof(bool)) { return ReadBooleanArrayElements(CreateArray<bool>(length)); }

            // otherwise, read elements directly from underlying binary writer
            switch (kind)
            {
                case EncodingKind.Int8: return ReadInt8ArrayElements(CreateArray<sbyte>(length));
                case EncodingKind.Int16: return ReadInt16ArrayElements(CreateArray<short>(length));
                case EncodingKind.Int32: return ReadInt32ArrayElements(CreateArray<int>(length));
                case EncodingKind.Int64: return ReadInt64ArrayElements(CreateArray<long>(length));
                case EncodingKind.UInt16: return ReadUInt16ArrayElements(CreateArray<ushort>(length));
                case EncodingKind.UInt32: return ReadUInt32ArrayElements(CreateArray<uint>(length));
                case EncodingKind.UInt64: return ReadUInt64ArrayElements(CreateArray<ulong>(length));
                case EncodingKind.Float4: return ReadFloat4ArrayElements(CreateArray<float>(length));
                case EncodingKind.Float8: return ReadFloat8ArrayElements(CreateArray<double>(length));
                case EncodingKind.Decimal: return ReadDecimalArrayElements(CreateArray<decimal>(length));
                default:
                    throw ExceptionUtilities.UnexpectedValue(kind);
            }
        }

        private bool[] ReadBooleanArrayElements(bool[] array)
        {
            var wordLength = BitVector.WordsRequired(array.Length);

            var count = 0;
            for (var i = 0; i < wordLength; i++)
            {
                var word = _reader.ReadUInt32();

                for (var p = 0; p < BitVector.BitsPerWord; p++)
                {
                    if (count >= array.Length)
                    {
                        return array;
                    }

                    array[count++] = BitVector.IsTrue(word, p);
                }
            }

            return array;
        }

        private static T[] CreateArray<T>(int length)
        {
            if (length == 0)
            {
                // quick check
                return Array.Empty<T>();
            }
            else
            {
                return new T[length];
            }
        }

        private string[] ReadStringArrayElements(string[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = this.ReadStringValue();
            }

            return array;
        }

        private sbyte[] ReadInt8ArrayElements(sbyte[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadSByte();
            }

            return array;
        }

        private short[] ReadInt16ArrayElements(short[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadInt16();
            }

            return array;
        }

        private int[] ReadInt32ArrayElements(int[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadInt32();
            }

            return array;
        }

        private long[] ReadInt64ArrayElements(long[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadInt64();
            }

            return array;
        }

        private ushort[] ReadUInt16ArrayElements(ushort[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadUInt16();
            }

            return array;
        }

        private uint[] ReadUInt32ArrayElements(uint[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadUInt32();
            }

            return array;
        }

        private ulong[] ReadUInt64ArrayElements(ulong[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadUInt64();
            }

            return array;
        }

        private decimal[] ReadDecimalArrayElements(decimal[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadDecimal();
            }

            return array;
        }

        private float[] ReadFloat4ArrayElements(float[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadSingle();
            }

            return array;
        }

        private double[] ReadFloat8ArrayElements(double[] array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = _reader.ReadDouble();
            }

            return array;
        }

        private Type ReadType()
        {
            var kind = (EncodingKind)_reader.ReadByte();
            var typeId = this.ReadInt32();
            return ObjectBinder.GetTypeFromId(typeId);
        }

        private (Type, Func<ObjectReader, object>) ReadTypeAndReader()
        {
            var kind = (EncodingKind)_reader.ReadByte();
            var typeId = this.ReadInt32();
            return ObjectBinder.GetTypeAndReaderFromId(typeId);
        }

        private Variant ReadObject()
        {
            int id = _objectReferenceMap.GetNextReferenceId();

            var (type, typeReader) = this.ReadTypeAndReader();

            if (_recursive)
            {
                // recursive: read and construct instance immediately from member elements encoding next in the stream
                var instance = typeReader(this);
                _objectReferenceMap.SetValue(id, instance);
                return Variant.FromObject(instance);
            }
            else
            {
                uint memberCount = this.ReadCompressedUInt();

                if (memberCount == 0)
                {
                    return ConstructObject(type, (int)memberCount, typeReader, id);
                }
                else
                {
                    // non-recursive: remember construction information to invoke later when member elements available on the stack
                    _constructionStack.Push(Construction.CreateObjectConstruction(type, (int)memberCount, _valueStack.Count, typeReader, id));
                    return Variant.None;
                }
            }
        }

        private Variant ConstructObject(Type type, int memberCount, Func<ObjectReader, object> reader, int id)
        {
            Debug.Assert(_memberList.Count == 0);
            Debug.Assert(_indexInMemberList == 0);

            _memberList.Count = memberCount;

            // take members from the stack
            for (int i = 0; i < memberCount; i++)
            {
                _memberList[memberCount - i - 1] = _valueStack.Pop();
                // _memberList.Add(_valueStack.Pop());
            }

            // reverse list so that first member to be read is first
            // Reverse(_memberList);

            // invoke the deserialization constructor to create instance and read & assign members           
            var instance = reader(this);

            if (_indexInMemberList != memberCount)
            {
                throw DeserializationReadIncorrectNumberOfValuesException(type.Name);
            }

            ResetMemberList();

            _objectReferenceMap.SetValue(id, instance);

            return Variant.FromObject(instance);
        }

        private static void Reverse(List<Variant> memberList)
            => Reverse(memberList, 0, memberList.Count);

        private static void Reverse(List<Variant> memberList, int index, int length)
        {
            // Note: we do not call List<T>.Reverse as that causes boxing of elements when
            // T is a struct type:
            // https://github.com/dotnet/coreclr/issues/7986
            int i = index;
            int j = index + length - 1;
            while (i < j)
            {
                var temp = memberList[i];
                memberList[i] = memberList[j];
                memberList[j] = temp;
                i++;
                j--;
            }
        }

        private static Exception DeserializationReadIncorrectNumberOfValuesException(string typeName)
        {
            throw new InvalidOperationException(String.Format(Resources.Deserialization_reader_for_0_read_incorrect_number_of_values, typeName));
        }

        private static Exception NoSerializationTypeException(string typeName)
        {
            return new InvalidOperationException(string.Format(Resources.The_type_0_is_not_understood_by_the_serialization_binder, typeName));
        }

        private static Exception NoSerializationReaderException(string typeName)
        {
            return new InvalidOperationException(string.Format(Resources.Cannot_serialize_type_0, typeName));
        }
    }
}
