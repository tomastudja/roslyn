﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Completion.Providers.ImportCompletion
{
    internal interface IImportCompletionCacheService<TProject, TPortableExecutable> : IWorkspaceService
    {
        // PE references are keyed on assembly path.
        IDictionary<string, TPortableExecutable> PEItemsCache { get; }

        IDictionary<ProjectId, TProject> ProjectItemsCache { get; }
    }
}
