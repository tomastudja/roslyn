﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.AddMissingImports;

namespace Microsoft.CodeAnalysis.ExternalAccess.UnitTestGenerator.Api;

internal sealed class WrappedMissingImportsAnalysisResult(ImmutableArray<WrappedAddImportFixData> addImportFixDatas)
{
    public ImmutableArray<WrappedAddImportFixData> AddImportFixDatas = addImportFixDatas;
}
