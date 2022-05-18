﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    public class AnalyzerConfigDocumentEventArgs : EventArgs, ITextDocumentEventArgs<AnalyzerConfigDocument>
    {
        public AnalyzerConfigDocument Document { get; }

        public AnalyzerConfigDocumentEventArgs(AnalyzerConfigDocument document)
        {
            Contract.ThrowIfNull(document);
            this.Document = document;
        }
    }
}
