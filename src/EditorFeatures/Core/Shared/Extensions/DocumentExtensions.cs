﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Editor.Shared.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Shared.Extensions
{
    internal static class DocumentExtensions
    {
        public static bool IsInCloudEnvironmentClientContext(this Document document)
        {
            var workspaceContextService = document.Project.Solution.Workspace.Services.GetRequiredService<IWorkspaceContextService>();
            return workspaceContextService.IsCloudEnvironmentClient();
        }
    }
}
