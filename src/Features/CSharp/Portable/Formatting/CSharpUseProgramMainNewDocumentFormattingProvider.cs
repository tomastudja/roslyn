﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.ConvertProgram;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.CodeAnalysis.CSharp.Formatting
{
    [ExportNewDocumentFormattingProvider(LanguageNames.CSharp), Shared]
    internal class CSharpUseProgramMainNewDocumentFormattingProvider : INewDocumentFormattingProvider
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public CSharpUseProgramMainNewDocumentFormattingProvider()
        {
        }

        public async Task<Document> FormatNewDocumentAsync(Document document, Document? hintDocument, SyntaxFormattingOptions formattingOptions, CancellationToken cancellationToken)
        {
            var options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            // if the user prefers Program.Main style instead, then attempt to convert a template with
            // top-level-statements to that form.
            var option = options.GetOption(CSharpCodeStyleOptions.PreferTopLevelStatements);
            if (option.Value)
                return document;

            return await ConvertProgramTransform.ConvertToProgramMainAsync(document, cancellationToken).ConfigureAwait(false);
        }
    }
}
