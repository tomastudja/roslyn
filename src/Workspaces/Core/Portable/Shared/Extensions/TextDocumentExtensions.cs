﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Shared.Extensions
{
    internal static partial class TextDocumentExtensions
    {
        /// <summary>
        /// Creates a new instance of this text document updated to have the text specified.
        /// </summary>
        public static TextDocument WithText(this TextDocument textDocument, SourceText text)
        {
            switch (textDocument)
            {
                case Document document:
                    return document.WithText(text);

                case AnalyzerConfigDocument analyzerConfigDocument:
                    return analyzerConfigDocument.WithAnalyzerConfigDocumentText(text);

                case AdditionalDocument additionalDocument:
                    return additionalDocument.WithAdditionalDocumentText(text);

                default:
                    throw ExceptionUtilities.Unreachable;
            }
        }

        /// <summary>
        /// Creates a new instance of this additional document updated to have the text specified.
        /// </summary>
        public static TextDocument WithAdditionalDocumentText(this TextDocument textDocument, SourceText text)
        {
            Contract.ThrowIfFalse(textDocument is AdditionalDocument);
            return textDocument.Project.Solution.WithAdditionalDocumentText(textDocument.Id, text, PreservationMode.PreserveIdentity).GetTextDocument(textDocument.Id);
        }

        /// <summary>
        /// Creates a new instance of this analyzer config document updated to have the text specified.
        /// </summary>
        public static TextDocument WithAnalyzerConfigDocumentText(this TextDocument textDocument, SourceText text)
        {
            Contract.ThrowIfFalse(textDocument is AnalyzerConfigDocument);
            return textDocument.Project.Solution.WithAnalyzerConfigDocumentText(textDocument.Id, text, PreservationMode.PreserveIdentity).GetTextDocument(textDocument.Id);
        }
    }
}
