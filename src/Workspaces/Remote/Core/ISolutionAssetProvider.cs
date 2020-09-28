﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Remote
{
    /// <summary>
    /// Brokered service.
    /// </summary>
    internal interface ISolutionAssetProvider
    {
        /// <summary>
        /// Streams serialized assets into the given stream.
        /// </summary>
        ValueTask GetAssetsAsync(Stream outputStream, int scopeId, Checksum[] checksums, CancellationToken cancellationToken);

        // TODO: remove (https://github.com/dotnet/roslyn/issues/43477)
        ValueTask<bool> IsExperimentEnabledAsync(string experimentName, CancellationToken cancellationToken);
    }
}
