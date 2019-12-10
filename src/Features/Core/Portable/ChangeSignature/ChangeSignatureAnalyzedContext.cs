﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.ChangeSignature
{
    internal sealed class ChangeSignatureAnalyzedContext
    {
        public readonly bool CanChangeSignature;
        public readonly Document Document;
        public readonly Project Project;
        public readonly ISymbol Symbol;
        public readonly CannotChangeSignatureReason CannotChangeSignatureReason;
        public readonly ParameterConfiguration ParameterConfiguration;
        public readonly int InsertPosition;

        public Solution Solution => Project.Solution;

        public ChangeSignatureAnalyzedContext(
            Document document, ISymbol symbol, ParameterConfiguration parameterConfiguration, int insertPosition)
        {
            CanChangeSignature = true;
            Document = document;
            Project = document.Project;
            Symbol = symbol;
            ParameterConfiguration = parameterConfiguration;
            InsertPosition = insertPosition;
            CannotChangeSignatureReason = CannotChangeSignatureReason.None;
        }

        public ChangeSignatureAnalyzedContext(CannotChangeSignatureReason reason)
        {
            CanChangeSignature = false;
            CannotChangeSignatureReason = reason;
        }
    }
}
