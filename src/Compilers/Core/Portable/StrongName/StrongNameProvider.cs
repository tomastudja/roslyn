﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using Microsoft.Cci;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Provides strong name and signs source assemblies.
    /// </summary>
    public abstract class StrongNameProvider
    {
        protected StrongNameProvider()
        {
        }

        public abstract override int GetHashCode();
        public override abstract bool Equals(object other);

        internal abstract StrongNameFileSystem FileSystem { get; }

        /// <summary>
        /// Signs the <paramref name="filePath"/> value using <paramref name="keys"/>.
        /// </summary>
        internal abstract void SignFile(StrongNameKeys keys, string filePath);

        /// <summary>
        /// Signs the contents of <paramref name="peBlob"/> using <paramref name="peBuilder"/> and <paramref name="privateKey"/>.
        /// </summary>
        internal abstract void SignBuilder(ExtendedPEBuilder peBuilder, BlobBuilder peBlob, RSAParameters privateKey);

        /// <summary>
        /// Create a <see cref="StrongNameKeys"/> for the provided information.
        /// </summary>
        internal abstract StrongNameKeys CreateKeys(string keyFilePath, string keyContainerName, bool hasCounterSignature, CommonMessageProvider messageProvider);
    }
}
