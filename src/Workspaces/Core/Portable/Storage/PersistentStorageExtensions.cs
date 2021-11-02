﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.SQLite.v2;
using Microsoft.CodeAnalysis.Storage.CloudCache;

namespace Microsoft.CodeAnalysis.Storage
{
    internal static class PersistentStorageExtensions
    {
        public static IChecksummedPersistentStorageService GetPersistentStorageService(this HostWorkspaceServices services, OptionSet options)
            => GetPersistentStorageService(services, GetPersistentStorageDatabase(options));

        public static StorageDatabase GetPersistentStorageDatabase(this OptionSet options)
            => options.GetOption(StorageOptions.CloudCacheFeatureFlag) ? StorageDatabase.CloudCache : options.GetOption(StorageOptions.Database);

        public static IChecksummedPersistentStorageService GetPersistentStorageService(this HostWorkspaceServices services, StorageDatabase database)
        {
            var configuration = services.GetRequiredService<IPersistentStorageConfiguration>();

            return database switch
            {
                StorageDatabase.SQLite
                    => services.GetService<SQLitePersistentStorageService>() ??
                       NoOpPersistentStorageService.GetOrThrow(configuration),
                StorageDatabase.CloudCache
                    => services.GetService<ICloudCacheStorageService>() ??
                       NoOpPersistentStorageService.GetOrThrow(configuration),
                _ => NoOpPersistentStorageService.GetOrThrow(configuration),
            };
        }
    }
}
