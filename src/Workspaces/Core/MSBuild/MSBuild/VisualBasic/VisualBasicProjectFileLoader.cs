﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.MSBuild;

namespace Microsoft.CodeAnalysis.VisualBasic
{
    internal partial class VisualBasicProjectFileLoader : ProjectFileLoader
    {
        public override string Language
        {
            get { return LanguageNames.VisualBasic; }
        }

        internal VisualBasicProjectFileLoader()
        {
        }

        protected override ProjectFile CreateProjectFile(LoadedProjectInfo info)
        {
            return new VisualBasicProjectFile(this, info.Project, info.Log);
        }
    }
}
