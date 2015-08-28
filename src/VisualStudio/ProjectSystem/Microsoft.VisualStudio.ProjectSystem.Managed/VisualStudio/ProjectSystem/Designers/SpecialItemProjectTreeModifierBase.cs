﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.ProjectSystem.Designers.Imaging;
using Microsoft.VisualStudio.ProjectSystem.Utilities;
using Microsoft.VisualStudio.ProjectSystem.Utilities.Designers;

namespace Microsoft.VisualStudio.ProjectSystem.Designers
{
    /// <summary>
    ///     Provides the base class for <see cref="IProjectTreeModifier"/> objects that handle special folders, such as the Properties "AppDesigner" folder.
    /// </summary>
    internal abstract class SpecialItemProjectTreeModifierBase : ProjectTreeModifierBase
    {
        private readonly IProjectImageProvider _imageProvider;

        protected SpecialItemProjectTreeModifierBase(IProjectImageProvider imageProvider)
        {
            Requires.NotNull(imageProvider, nameof(imageProvider));

            _imageProvider = imageProvider;
        }

        public abstract string ImageKey
        {
            get;
        }

        public abstract ImmutableHashSet<string> DefaultCapabilities
        {
            get;
        }

        public abstract bool HideChildren
        {
            get;
        }

        public override sealed IProjectTree ApplyModifications(IProjectTree tree, IProjectTree previousTree, IProjectTreeProvider projectTreeProvider)
        {
            if (!tree.IsProjectRoot())
                return tree;

            IProjectTree item = FindCandidateSpecialItem(tree);
            if (item == null)
                return tree;

            Assumes.True(item.Parent.IsProjectRoot(), "Expected returned item to be rooted by Project");

            ProjectImageMoniker icon = GetSpecialItemIcon();

            item = item.SetProperties(
                        icon: icon,
                        resetIcon: icon == null,
                        expandedIcon: icon,
                        resetExpandedIcon: icon == null,
                        capabilities: DefaultCapabilities.Union(item.Capabilities));

            if (HideChildren)
            {
                item = HideAllChildren(item);
            }

            return item.Parent;
        }

        protected abstract IProjectTree FindCandidateSpecialItem(IProjectTree projectRoot);

        private IProjectTree HideAllChildren(IProjectTree tree)
        {
            for (int i = 0; i < tree.Children.Count; i++)
            {
                var child = tree.Children[i].AddCapability(ProjectTreeCapabilities.VisibleOnlyInShowAllFiles);
                child = this.HideAllChildren(child);
                tree = child.Parent;
            }

            return tree;
        }

        private ProjectImageMoniker GetSpecialItemIcon()
        {
            ProjectImageMoniker moniker;
            if (_imageProvider.TryGetProjectImage(ImageKey, out moniker))
                return moniker;

            return null;
        }
    }
}
