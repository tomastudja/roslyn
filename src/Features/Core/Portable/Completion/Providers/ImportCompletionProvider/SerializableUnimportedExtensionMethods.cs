﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Microsoft.CodeAnalysis.Completion.Providers
{
    [DataContract]
    internal sealed class SerializableUnimportedExtensionMethods
    {
        [DataMember(Order = 0)]
        public readonly ImmutableArray<SerializableImportCompletionItem> CompletionItems;

        [DataMember(Order = 1)]
        public readonly bool IsPartialResult;

        [DataMember(Order = 2)]
        public TimeSpan GetSymbolsTime { get; set; }

        [DataMember(Order = 3)]
        public readonly TimeSpan CreateItemsTime;

        [DataMember(Order = 4)]
        public readonly TimeSpan? RemoteAssetSyncTime;

        public SerializableUnimportedExtensionMethods(
            ImmutableArray<SerializableImportCompletionItem> completionItems,
            bool isPartialResult,
            TimeSpan getSymbolsTime,
            TimeSpan createItemsTime,
            TimeSpan? remoteAssetSyncTime)
        {
            CompletionItems = completionItems;
            IsPartialResult = isPartialResult;
            GetSymbolsTime = getSymbolsTime;
            CreateItemsTime = createItemsTime;
            RemoteAssetSyncTime = remoteAssetSyncTime;
        }
    }
}
