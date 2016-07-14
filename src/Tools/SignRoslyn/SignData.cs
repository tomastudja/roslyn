﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SignRoslyn
{
    internal sealed class SignData
    {
        /// <summary>
        /// The path where the binaries are built to: e:\path\to\source\Binaries\Debug
        /// </summary>
        internal string RootBinaryPath { get; }

        /// <summary>
        /// The names of the binaries to be signed.  These are all relative paths off of the <see cref="RootBinaryPath"/>
        /// property.
        /// </summary>
        internal ImmutableArray<BinaryName> BinaryNames { get; }

        /// <summary>
        /// These are binaries which are included in our VSIX files but are already signed.  This list is used for 
        /// validation purpsoes.  These are all flat names and cannot be relative paths.
        /// </summary>
        internal ImmutableArray<string> ExternalBinaryNames { get;}

        /// <summary>
        /// Names of assemblies that need to be signed.  This is a subste of <see cref="BinaryNames"/>
        /// </summary>
        internal ImmutableArray<BinaryName> AssemblyNames { get; }

        /// <summary>
        /// Names of VSIX that need to be signed.  This is a subste of <see cref="BinaryNames"/>
        /// </summary>
        internal ImmutableArray<BinaryName> VsixNames { get; }

        /// <summary>
        /// A map of all of the binaries that need to be signed to the actual signing data.
        /// </summary>
        internal ImmutableDictionary<BinaryName, BinarySignData> BinarySignDataMap { get; }

        internal SignData(string rootBinaryPath, Dictionary<string, FileSignData> fileSignDataMap, IEnumerable<string> externalBinaryNames)
        {
            RootBinaryPath = rootBinaryPath;

            // Use order by to make the output of this tool as predictable as possible.
            var binaryNames = fileSignDataMap.Keys;
            BinaryNames = binaryNames.OrderBy(x => x).Select(x => new BinaryName(rootBinaryPath, x)).ToImmutableArray();
            ExternalBinaryNames = externalBinaryNames.OrderBy(x => x).ToImmutableArray();

            AssemblyNames = BinaryNames.Where(x => x.IsAssembly).ToImmutableArray();
            VsixNames = BinaryNames.Where(x => x.IsVsix).ToImmutableArray();

            var builder = ImmutableDictionary.CreateBuilder<BinaryName, BinarySignData>();
            foreach (var name in BinaryNames)
            {
                var data = fileSignDataMap[name.RelativePath];
                builder.Add(name, new BinarySignData(name, data));
            }
            BinarySignDataMap = builder.ToImmutable();
        }
    }
}

