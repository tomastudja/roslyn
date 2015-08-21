﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.ProjectSystem.Utilities;

namespace Microsoft.VisualStudio.ProjectSystem.VisualBasic.Templating
{
    /// <summary>
    /// Implementation of the item type provider for Visual Basic project system.
    /// </summary>
    //[Export(typeof(IItemTypeGuidProvider))]
    [AppliesTo(ProjectCapabilities.VB)]
    internal class VisualBasicItemTypeGuidProvider : IItemTypeGuidProvider
    {
        private static readonly Guid VBProjectType = new Guid("{F184B08F-C81C-45F6-A57F-5ABD9991F28F}");

        [ImportingConstructor]
        internal VisualBasicItemTypeGuidProvider()
        {
        }

        [Import]
        private UnconfiguredProject UnconfiguredProject
        {
            get;
            set;
        }

        /// <summary>
        /// Returns the item type Guid.
        /// </summary>
        public Guid ProjectTypeGuid
        {
            get { return VBProjectType; }
        }
    }
}
