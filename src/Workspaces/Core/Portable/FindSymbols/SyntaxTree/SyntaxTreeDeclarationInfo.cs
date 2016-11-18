﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    internal class SyntaxTreeDeclarationInfo : AbstractSyntaxTreeInfo
    {
        private const string PersistenceName = "<SyntaxTreeInfoDeclarationPersistence>";
        private const string SerializationFormat = "3";

        /// <summary>
        /// in memory cache will hold onto any info related to opened documents in primary branch or all documents in forked branch
        /// 
        /// this is not snapshot based so multiple versions of snapshots can re-use same data as long as it is relevant.
        /// </summary>
        private static readonly ConditionalWeakTable<BranchId, ConditionalWeakTable<DocumentId, SyntaxTreeDeclarationInfo>> s_cache =
            new ConditionalWeakTable<BranchId, ConditionalWeakTable<DocumentId, SyntaxTreeDeclarationInfo>>();

        public ImmutableArray<DeclaredSymbolInfo> DeclaredSymbolInfos { get; }

        public SyntaxTreeDeclarationInfo(VersionStamp version, ImmutableArray<DeclaredSymbolInfo> declaredSymbolInfos)
            : base(version)
        {
            DeclaredSymbolInfos = declaredSymbolInfos;
        }

        public override void WriteTo(ObjectWriter writer)
        {
            writer.WriteInt32(DeclaredSymbolInfos.Length);
            foreach (var declaredSymbolInfo in DeclaredSymbolInfos)
            {
                declaredSymbolInfo.WriteTo(writer);
            }
        }

        public override Task<bool> SaveAsync(Document document, CancellationToken cancellationToken)
        {
            return SaveAsync(document, s_cache, PersistenceName, SerializationFormat, cancellationToken);
        }

        public static Task<bool> PrecalculatedAsync(Document document, CancellationToken cancellationToken)
        {
            return PrecalculatedAsync(document, PersistenceName, SerializationFormat, cancellationToken);
        }

        public static Task<SyntaxTreeDeclarationInfo> LoadAsync(Document document, CancellationToken cancellationToken)
            => LoadAsync(document, ReadFrom, s_cache, PersistenceName, SerializationFormat, cancellationToken);

        private static SyntaxTreeDeclarationInfo ReadFrom(ObjectReader reader, VersionStamp version)
        {
            try
            {
                var declaredSymbolCount = reader.ReadInt32();
                var builder = ImmutableArray.CreateBuilder<DeclaredSymbolInfo>(declaredSymbolCount);
                for (int i = 0; i < declaredSymbolCount; i++)
                {
                    builder.Add(DeclaredSymbolInfo.ReadFrom(reader));
                }

                return new SyntaxTreeDeclarationInfo(
                    version, builder.MoveToImmutable());
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}