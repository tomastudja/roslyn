﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem;
using Microsoft.VisualStudio.LanguageServices.Implementation.Venus;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.Library.ObjectBrowser
{
    internal static class Extensions
    {
        private static readonly SymbolDisplayFormat s_typeDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance);

        private static readonly SymbolDisplayFormat s_memberDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeVariance,
            memberOptions: SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeParameters,
            parameterOptions: SymbolDisplayParameterOptions.IncludeType,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public static string GetMemberNavInfoNameOrEmpty(this ISymbol memberSymbol)
        {
            return memberSymbol != null
                ? memberSymbol.ToDisplayString(s_memberDisplayFormat)
                : string.Empty;
        }

        public static string GetNamespaceNavInfoNameOrEmpty(this INamespaceSymbol namespaceSymbol)
        {
            if (namespaceSymbol == null)
            {
                return string.Empty;
            }

            return !namespaceSymbol.IsGlobalNamespace
                ? namespaceSymbol.ToDisplayString()
                : string.Empty;
        }

        public static string GetTypeNavInfoNameOrEmpty(this ITypeSymbol typeSymbol)
        {
            return typeSymbol != null
                ? typeSymbol.ToDisplayString(s_typeDisplayFormat)
                : string.Empty;
        }

        public static string GetProjectDisplayName(this Project project)
        {
            var workspace = project.Solution.Workspace as VisualStudioWorkspaceImpl;
            if (workspace != null)
            {
                return workspace.GetProjectDisplayName(project);
            }

            return project.Name;
        }

        public static bool IsVenus(this Project project)
        {
            var workspace = project.Solution.Workspace as VisualStudioWorkspaceImpl;
            if (workspace == null)
            {
                return false;
            }

            foreach (var documentId in project.DocumentIds)
            {
                if (workspace.GetHostDocument(documentId) is ContainedDocument)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a display name for the given project, walking its parent IVsHierarchy chain and
        /// pre-pending the names of parenting hierarchies (except the solution).
        /// </summary>
        public static string GetProjectNavInfoName(this Project project)
        {
            var result = project.Name;

            var workspace = project.Solution.Workspace as VisualStudioWorkspace;
            if (workspace == null)
            {
                return result;
            }

            var hierarchy = workspace.GetHierarchy(project.Id);
            if (hierarchy == null)
            {
                return result;
            }

            if (!hierarchy.TryGetName(out result))
            {
                return result;
            }

            IVsHierarchy parentHierarchy;
            if (hierarchy.TryGetParentHierarchy(out parentHierarchy) && !(parentHierarchy is IVsSolution))
            {
                var builder = new StringBuilder(result);

                while (parentHierarchy != null && !(parentHierarchy is IVsSolution))
                {
                    string parentName;
                    if (parentHierarchy.TryGetName(out parentName))
                    {
                        builder.Insert(0, parentName + "\\");
                    }

                    if (!parentHierarchy.TryGetParentHierarchy(out parentHierarchy))
                    {
                        break;
                    }
                }

                result = builder.ToString();
            }

            return result;
        }
    }
}
