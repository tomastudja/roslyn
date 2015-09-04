﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.ProjectSystem
{
    /// <summary>
    ///     Provides an implementation of <see cref="IProjectFeatures"/> based on Visual Studio services.
    /// </summary>
    [Export(typeof(IProjectFeatures))]
    internal class VsProjectFeatures : IProjectFeatures
    {
        private readonly IUnconfiguredProjectVsServices _projectVsServices;
        private readonly IThreadHandling _threadPolicy;

        [ImportingConstructor]
        public VsProjectFeatures(IUnconfiguredProjectVsServices projectVsServices, IThreadHandling threadPolicy)
        {
            Requires.NotNull(projectVsServices, nameof(projectVsServices));
            Requires.NotNull(threadPolicy, nameof(threadPolicy));

            _projectVsServices = projectVsServices;
            _threadPolicy = threadPolicy;
        }

        public bool SupportsProjectDesigner
        {
            get { return _projectVsServices.Hierarchy.GetProperty(VsHierarchyPropID.SupportsProjectDesigner, defaultValue: false); }
        }
    }
}
