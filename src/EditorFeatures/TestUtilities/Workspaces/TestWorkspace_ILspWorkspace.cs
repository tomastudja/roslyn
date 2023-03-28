﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces
{
    public partial class TestWorkspace : Workspace, ILspWorkspace
    {
        bool ILspWorkspace.SupportsMutation => _supportsLspMutation;

        void ILspWorkspace.UpdateTextIfPresent(DocumentId documentId, SourceText sourceText)
        {
            Contract.ThrowIfFalse(_supportsLspMutation);
            OnDocumentTextChanged(documentId, sourceText, PreservationMode.PreserveIdentity, requireDocumentPresent: false);
        }

        void ILspWorkspace.UpdateTextIfPresent(DocumentId documentId, TextLoader textLoader)
        {
            Contract.ThrowIfFalse(_supportsLspMutation);
            OnDocumentTextLoaderChanged(documentId, textLoader, requireDocumentPresent: false);
        }
    }
}
