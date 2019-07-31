﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.ExternalAccess.LiveShare.Projects
{
    /// <summary>
    /// Discovers project information for remote directories
    /// </summary>
    [Export(typeof(RemoteProjectInfoProvider))]
    internal class RemoteProjectInfoProvider
    {
        private readonly IEnumerable<IRemoteProjectInfoProvider> _remoteProjectInfoProviders;

        [ImportingConstructor]
        public RemoteProjectInfoProvider([ImportMany] IEnumerable<IRemoteProjectInfoProvider> remoteProjectInfoProviders)
        {
            _remoteProjectInfoProviders = remoteProjectInfoProviders ?? throw new ArgumentNullException(nameof(remoteProjectInfoProviders));
        }

        public async Task<IReadOnlyCollection<ProjectInfo>> GetRemoteProjectInfosAsync(CancellationToken cancellationToken)
        {
            var projectInfos = new List<ProjectInfo>();
            foreach (var remoteProjectInfoProvider in _remoteProjectInfoProviders)
            {
                try
                {
                    foreach (var projectInfo in await remoteProjectInfoProvider.GetRemoteProjectInfosAsync(cancellationToken).ConfigureAwait(false))
                    {
                        projectInfos.Add(projectInfo);
                    }
                }
                catch (Exception)
                {
                    // Continue with the other providers even if one of them fails. 
                    continue;
                }
            }

            return projectInfos;
        }

        public static ProjectInfo CreateProjectInfo(string projectName, string language, ImmutableArray<string> files)
        {
            var projectId = ProjectId.CreateNewId();
            var docInfos = ImmutableArray.CreateBuilder<DocumentInfo>();

            foreach (string file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var docInfo = DocumentInfo.Create(DocumentId.CreateNewId(projectId),
                    fileName,
                    filePath: file,
                    loader: new FileTextLoaderNoException(file, null));
                docInfos.Add(docInfo);
            }

            return ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                projectName,
                projectName,
                language,
                documents: docInfos.ToImmutable());
        }
    }
}
