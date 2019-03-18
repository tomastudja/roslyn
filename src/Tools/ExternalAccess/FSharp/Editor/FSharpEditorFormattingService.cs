﻿using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.ExternalAccess.FSharp.Editor
{
    [Shared]
    [ExportLanguageService(typeof(IEditorFormattingService), LanguageNames.FSharp)]
    internal class FSharpEditorFormattingService : IEditorFormattingService
    {
        private readonly IFSharpEditorFormattingService _service;

        [ImportingConstructor]
        public FSharpEditorFormattingService(IFSharpEditorFormattingService service)
        {
            _service = service;
        }

        public bool SupportsFormatDocument => _service.SupportsFormatDocument;

        public bool SupportsFormatSelection => _service.SupportsFormatSelection;

        public bool SupportsFormatOnPaste => _service.SupportsFormatOnPaste;

        public bool SupportsFormatOnReturn => _service.SupportsFormatOnReturn;

        public Task<IList<TextChange>> GetFormattingChangesAsync(Document document, TextSpan? textSpan, CancellationToken cancellationToken)
        {
            return _service.GetFormattingChangesAsync(document, textSpan, cancellationToken);
        }

        public Task<IList<TextChange>> GetFormattingChangesAsync(Document document, char typedChar, int position, CancellationToken cancellationToken)
        {
            return _service.GetFormattingChangesAsync(document, typedChar, position, cancellationToken);
        }

        public Task<IList<TextChange>> GetFormattingChangesOnPasteAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            return _service.GetFormattingChangesOnPasteAsync(document, textSpan, cancellationToken);
        }

        public Task<IList<TextChange>> GetFormattingChangesOnReturnAsync(Document document, int position, CancellationToken cancellationToken)
        {
            return _service.GetFormattingChangesOnReturnAsync(document, position, cancellationToken);
        }

        public bool SupportsFormattingOnTypedCharacter(Document document, char ch)
        {
            return _service.SupportsFormattingOnTypedCharacter(document, ch);
        }
    }
}
