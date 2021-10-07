﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Options.Providers;

namespace Microsoft.CodeAnalysis
{
    [ExportOptionProvider, Shared]
    internal class WorkspaceConfigurationOptions : IOptionProvider
    {
        public static readonly Option<WorkspaceExperiment> WorkspaceExperiment = new(
            nameof(WorkspaceConfigurationOptions), nameof(WorkspaceExperiment), defaultValue: CodeAnalysis.WorkspaceExperiment.None,
            new FeatureFlagStorageLocation("Roslyn.WorkspaceExperiment"));

        ImmutableArray<IOption> IOptionProvider.Options { get; } = ImmutableArray.Create<IOption>(
            WorkspaceExperiment);

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public WorkspaceConfigurationOptions()
        {
        }
    }

    internal enum WorkspaceExperiment
    {
        None,
        DisableRecoverableTrees = 1,
        DisableRecoverableText = 2,
    }
}
