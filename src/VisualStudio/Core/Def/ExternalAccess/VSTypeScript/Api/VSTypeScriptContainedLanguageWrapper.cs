﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem;
using Microsoft.VisualStudio.LanguageServices.Implementation.Venus;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.LanguageServices.ExternalAccess.VSTypeScript.Api
{
    internal struct VSTypeScriptContainedLanguageWrapper
    {
        private readonly ContainedLanguage _underlyingObject;

        public VSTypeScriptContainedLanguageWrapper(
            IVsTextBufferCoordinator bufferCoordinator,
            IComponentModel componentModel,
            VSTypeScriptVisualStudioProjectWrapper project,
            IVsHierarchy hierarchy,
            uint itemid,
            Guid languageServiceGuid)
        {
            _underlyingObject = new ContainedLanguage(
                bufferCoordinator,
                componentModel,
                componentModel.GetService<VisualStudioWorkspace>(),
                project.Project.Id,
                project.Project,
                hierarchy,
                itemid,
                languageServiceGuid,
                vbHelperFormattingRule: null);
        }

        public VSTypeScriptContainedLanguageWrapper(
            IVsTextBufferCoordinator bufferCoordinator,
            IComponentModel componentModel,
            Workspace workspace,
            IVsHierarchy hierarchy,
            uint itemid,
            Guid languageServiceGuid)
        {
            var projectId = ProjectId.CreateNewId();

            _underlyingObject = new ContainedLanguage(
                bufferCoordinator,
                componentModel,
                workspace,
                projectId,
                null,
                hierarchy,
                itemid,
                languageServiceGuid,
                vbHelperFormattingRule: null);

            var filePath = _underlyingObject.GetFilePathFromBufferOrHierarchy(hierarchy, itemid);
            workspace.OnProjectAdded(ProjectInfo.Create(projectId, VersionStamp.Default, filePath, string.Empty, InternalLanguageNames.TypeScript));
        }

        public bool IsDefault => _underlyingObject == null;

        public void DisconnectHost() => _underlyingObject.SetHost(null);
    }
}
