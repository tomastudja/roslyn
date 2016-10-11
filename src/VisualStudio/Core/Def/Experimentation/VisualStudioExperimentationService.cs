﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Experiments;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.LanguageServices.Experimentation
{
    [ExportWorkspaceService(typeof(IExperimentationService), ServiceLayer.Host), Shared]
    internal class VisualStudioExperimentationService : IExperimentationService
    {
        private readonly IVsExperimentationService _experimentationServiceOpt;

        [ImportingConstructor]
        public VisualStudioExperimentationService(
            SVsServiceProvider serviceProvider)
        {
            try
            {
                _experimentationServiceOpt = (IVsExperimentationService)serviceProvider.GetService(typeof(SVsServiceProvider));
            }
            catch
            {
            }
        }

        public bool IsExperimentEnabled(string experimentName)
            => _experimentationServiceOpt?.IsCachedFlightEnabled(experimentName) ?? false;
    }
}