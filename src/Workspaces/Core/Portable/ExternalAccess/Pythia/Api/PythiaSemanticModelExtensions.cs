﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.ExternalAccess.Pythia.Api
{
    internal static class PythiaSemanticModelExtensions
    {
        public static ISymbol GetEnclosingNamedTypeOrAssembly(this SemanticModel semanticModel, int position, CancellationToken cancellationToken)
            => SemanticModelExtensions.GetEnclosingNamedTypeOrAssembly(semanticModel, position, cancellationToken);

    }
}
