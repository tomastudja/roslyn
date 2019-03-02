﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.EmbeddedLanguages.VirtualChars
{
    internal abstract partial class VirtualCharSequence
    {
        private class StringVirtualCharSequence : VirtualCharSequence
        {
            private readonly int _firstVirtualCharPosition;

            /// <summary>
            /// The underlying string that we're returning virtual chars from.
            /// Note the chars we return may be from a subsection of this string.
            /// </summary>
            private readonly string _underlyingData;

            /// <summary>
            /// The subsection of <see cref="_underlyingData"/> that we're producing virtual chars from.
            /// </summary>
            private readonly TextSpan _underlyingDataSpan;

            public StringVirtualCharSequence(int firstVirtualCharPosition, string data, TextSpan dataSpan)
            {
                _firstVirtualCharPosition = firstVirtualCharPosition;
                _underlyingData = data;
                _underlyingDataSpan = dataSpan;
            }

            public override int Length => _underlyingDataSpan.Length;

            public override VirtualChar this[int index]
                => new VirtualChar(
                    _underlyingData[_underlyingDataSpan.Start + index],
                    new TextSpan(_firstVirtualCharPosition + index, length: 1));

            protected override string CreateStringWorker()
                => _underlyingData.Substring(_underlyingDataSpan.Start, _underlyingDataSpan.Length);
        }
    }
}
