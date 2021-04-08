﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.MSBuild.Build;
using Microsoft.CodeAnalysis.MSBuild.Logging;
using MSB = Microsoft.Build;

namespace Microsoft.CodeAnalysis.VisualBasic
{
    internal partial class VisualBasicProjectFileLoader : ProjectFileLoader
    {
        public override string Language => LanguageNames.VisualBasic;

        internal VisualBasicProjectFileLoader()
        {
        }

        protected override ProjectFile CreateProjectFile(MSB.Evaluation.Project? project, ProjectBuildManager buildManager, DiagnosticLog log)
        {
            return new VisualBasicProjectFile(this, project, buildManager, log);
        }
    }
}
