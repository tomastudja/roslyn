﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DiaSymReader
{
    [ComImport]
    [Guid("ACCEE350-89AF-4ccb-8B40-1C2C4C6F9434")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(false)]
    public interface ISymUnmanagedBinder2 : ISymUnmanagedBinder
    {
        #region ISymUnmanagedBinder methods

        /// <summary>
        /// Given a metadata interface and a file name, returns the
        /// correct <see cref="ISymUnmanagedReader"/> that will read the debugging symbols
        /// associated with the module.
        /// </summary>
        /// <param name="metadataImporter">An instance of IMetadataImport providing metadata for the specified PE file.</param>
        /// <param name="fileName">PE file path.</param>
        /// <param name="searchPath">Alternate path to search for debug data.</param>
        /// <param name="reader">The new reader instance.</param>
        [PreserveSig]
        new int GetReaderForFile(
            [MarshalAs(UnmanagedType.Interface)]object metadataImporter,
            [MarshalAs(UnmanagedType.LPWStr)]string fileName,
            [MarshalAs(UnmanagedType.LPWStr)]string searchPath,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader);

        /// <summary>
        /// Given a metadata interface and a stream that contains
        /// the symbol store, returns the <see cref="ISymUnmanagedReader"/>
        /// that will read the debugging symbols from the given
        /// symbol store.
        /// </summary>
        /// <param name="metadataImporter">An instance of IMetadataImport providing metadata for the corresponding PE file.</param>
        /// <param name="stream">PDB stream.</param>
        /// <param name="reader">The new reader instance.</param>
        [PreserveSig]
        new int GetReaderFromStream(
            [MarshalAs(UnmanagedType.Interface)]object metadataImporter,
            [MarshalAs(UnmanagedType.Interface)]object stream,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader);

        #endregion

        /// <summary>
        /// Given a metadata interface and a file name, returns the 
        /// <see cref="ISymUnmanagedReader"/> interface that will read the debugging symbols associated
        /// with the module.
        /// </summary>
        /// <remarks>
        /// This version of the function can search for the PDB in areas other than
        /// right next to the module, controlled by the <paramref name="searchPolicy"/>.
        /// If a <paramref name="searchPath"/> is provided, those directories will always be searched.
        /// </remarks>
        /// <param name="metadataImporter">An instance of IMetadataImport providing metadata for the specified PE file.</param>
        /// <param name="fileName">PE file path.</param>
        /// <param name="searchPath">Alternate path to search for debug data.</param>
        /// <param name="searchPolicy">Search policy.</param>
        /// <param name="reader">The new reader instance.</param>
        [PreserveSig]
        int GetReaderForFile2(
            [MarshalAs(UnmanagedType.Interface)]object metadataImporter,
            [MarshalAs(UnmanagedType.LPWStr)]string fileName,
            [MarshalAs(UnmanagedType.LPWStr)]string searchPath,
            SymUnmanagedSearchPolicy searchPolicy,
            [MarshalAs(UnmanagedType.Interface)]out ISymUnmanagedReader reader);
    }
}
