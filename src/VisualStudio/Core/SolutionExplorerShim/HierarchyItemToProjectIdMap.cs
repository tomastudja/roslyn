// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.SolutionExplorer
{
    [ExportWorkspaceService(typeof(IHierarchyItemToProjectIdMap), ServiceLayer.Host), Shared]
    internal class HierarchyItemToProjectIdMap : IHierarchyItemToProjectIdMap
    {
        private readonly VisualStudioWorkspaceImpl _workspace;

        [ImportingConstructor]
        public HierarchyItemToProjectIdMap(VisualStudioWorkspaceImpl workspace)
        {
            _workspace = workspace;
        }

        public bool TryGetProjectId(IVsHierarchyItem hierarchyItem, string targetFrameworkMoniker, out ProjectId projectId)
        {
            if (_workspace.DeferredState == null)
            {
                projectId = default(ProjectId);
                return false;
            }

            var nestedHierarchy = hierarchyItem.HierarchyIdentity.NestedHierarchy;
            var nestedHierarchyId = hierarchyItem.HierarchyIdentity.NestedItemID;

            if (!nestedHierarchy.TryGetCanonicalName(nestedHierarchyId, out string nestedCanonicalName))
            {
                projectId = default(ProjectId);
                return false;
            }

            var project = _workspace.DeferredState.ProjectTracker.ImmutableProjects
                    .Where(p =>
                    {
                        if (p.Hierarchy.TryGetCanonicalName((uint)VSConstants.VSITEMID.Root, out string projectCanonicalName)
                            && projectCanonicalName.Equals(nestedCanonicalName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            if (targetFrameworkMoniker == null)
                            {
                                return true;
                            }

                            return p.Hierarchy.TryGetTargetFrameworkMoniker((uint)VSConstants.VSITEMID.Root, out string projectTargetFrameworkMoniker)
                                && projectTargetFrameworkMoniker.Equals(targetFrameworkMoniker);
                        }

                        return false;
                    })
                    .SingleOrDefault();

            if (project == null)
            {
                projectId = default(ProjectId);
                return false;
            }
            else
            {
                projectId = project.Id;
                return true;
            }
        }
    }
}
