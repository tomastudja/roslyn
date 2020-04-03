﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.ChangeSignature
{
    internal interface IChangeSignatureOptionsService : IWorkspaceService
    {
        /// <summary>
        /// Changes signature of the symbol (currently a method symbol or an event symbol)
        /// </summary>
        /// <param name="document">the context document</param>
        /// <param name="insertPosition">the position in the document with the signature of the method</param>
        /// <param name="symbol">the symbol for changing the signature</param>
        /// <param name="parameters">existing parameters of the symbol</param>
        /// <returns></returns>
        ChangeSignatureOptionsResult? GetChangeSignatureOptions(
            Document document,
            int insertPosition,
            ISymbol symbol,
            ParameterConfiguration parameters);
    }
}
