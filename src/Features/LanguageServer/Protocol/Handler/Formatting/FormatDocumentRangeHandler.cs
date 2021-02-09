﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler
{
    [ExportLspRequestHandlerProvider, Shared]
    [ProvidesMethod(Methods.TextDocumentRangeFormattingName)]
    internal class FormatDocumentRangeHandler : AbstractFormatDocumentHandlerBase<DocumentRangeFormattingParams, TextEdit[]>
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public FormatDocumentRangeHandler()
        {
        }

        public override string Method => Methods.TextDocumentRangeFormattingName;

        public override TextDocumentIdentifier? GetTextDocumentIdentifier(DocumentRangeFormattingParams request) => request.TextDocument;

        public override Task<TextEdit[]> HandleRequestAsync(DocumentRangeFormattingParams request, RequestContext context, CancellationToken cancellationToken)
            => GetTextEditsAsync(context, cancellationToken, range: request.Range);
    }
}
