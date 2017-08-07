﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

// <auto-generated/>

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.CSharp.ProjectSystemShim.Interop
{
    [ComImport]
    [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid(Guids.CSharpProjectRootIdString)]
    internal interface ICSharpProjectRoot
    {
        // Sets the site for this project object.
        void SetProjectSite(ICSharpProjectSite site);

        // Get the site object for this project (any interface; IID_ICSharpProjectSite is
        // guaranteed)
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object GetProjectSite([In] ref Guid riid);

        // Returns the path containing the project file.
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetProjectLocation();

        // Returns the full path name of the project file, or some other entity which is guaranteed
        // to be unique across all open projects.
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetFullProjectName();

        // Determines whether the given file is contained in this project
        [PreserveSig]
        int BelongsToProject([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

        // Returns the name of the active configuration
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetActiveConfigurationName();

        // Constructs a suitable name for a per-project per-configuration cache file
        [return: MarshalAs(UnmanagedType.BStr)]
        string BuildPerConfigCacheFileName();

        // Configure the given compiler and input set objects based on all current
        // settings of the project, including configuration (debug/retail/goo, etc.)
        void ConfigureCompiler(
            [MarshalAs(UnmanagedType.Interface)] ICSCompiler compiler,
            [MarshalAs(UnmanagedType.Interface)] ICSInputSet inputSet,
            bool addSources);

        // Determines whether the project can create a file code model object for the given file
        bool CanCreateFileCodeModel([MarshalAs(UnmanagedType.LPWStr)] string pszFile);

        // Create a file code model object for the given file
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object CreateFileCodeModel([MarshalAs(UnmanagedType.LPWStr)] string pszFile, [In] ref Guid riid);

        // Get the VS hierarchy/item ID for the given file
        [PreserveSig]
        int GetHierarchyAndItemID(
            [MarshalAs(UnmanagedType.LPWStr)] string pszFile,
            [MarshalAs(UnmanagedType.Interface)] out IVsHierarchy ppHier,
            out uint pItemID);

        // Get the VS hierarchy/item ID for the given file, optionally for files that are not in
        // project
        void GetHierarchyAndItemIDOptionallyInProject(
            [MarshalAs(UnmanagedType.LPWStr)] string pszFile,
            [MarshalAs(UnmanagedType.Interface)] out IVsHierarchy ppHier,
            out uint pItemID,
            bool mustBeInProject);
    }
}
