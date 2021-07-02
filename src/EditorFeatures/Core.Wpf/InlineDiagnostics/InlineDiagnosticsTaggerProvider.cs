﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.Implementation.Diagnostics;
using Microsoft.CodeAnalysis.Editor.Implementation.EditAndContinue;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.InlineDiagnostics
{
    [Export(typeof(ITaggerProvider))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [TagType(typeof(InlineDiagnosticsTag))]
    internal class InlineDiagnosticsTaggerProvider : AbstractDiagnosticsAdornmentTaggerProvider<InlineDiagnosticsTag>
    {
        private readonly IEditorFormatMap _editorFormatMap;
        protected sealed override IEnumerable<PerLanguageOption2<bool>> PerLanguageOptions => SpecializedCollections.SingletonEnumerable(InlineDiagnosticsOptions.EnableInlineDiagnostics);

        protected internal override bool IsEnabled => true;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InlineDiagnosticsTaggerProvider(
            IThreadingContext threadingContext,
            IEditorFormatMapService editorFormatMapService,
            IDiagnosticService diagnosticService,
            IAsynchronousOperationListenerProvider listenerProvider)
            : base(threadingContext, diagnosticService, listenerProvider)
        {
            _editorFormatMap = editorFormatMapService.GetEditorFormatMap("text");
        }

        protected internal override bool IncludeDiagnostic(DiagnosticData diagnostic)
        {
            return
                diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error &&
                !string.IsNullOrWhiteSpace(diagnostic.Message) &&
                !diagnostic.IsSuppressed;
        }

        /// <summary>
        /// Creates the InlineErrorTag with the error distinction
        /// </summary>
        protected override InlineDiagnosticsTag? CreateTag(Workspace workspace, DiagnosticData diagnostic)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(diagnostic.Message));
            var errorType = GetErrorTypeFromDiagnostic(diagnostic);
            if (errorType is null)
            {
                return null;
            }

            if (diagnostic.DocumentId is null)
            {
                return null;
            }

            var document = workspace.CurrentSolution.GetRequiredDocument(diagnostic.DocumentId);
            if (document is null)
            {
                return null;
            }

            var locationOption = workspace.Options.GetOption(InlineDiagnosticsOptions.Location, document.Project.Language);
            var navigateService = workspace.Services.GetRequiredService<INavigateToLinkService>();
            return new InlineDiagnosticsTag(errorType, diagnostic, _editorFormatMap, locationOption, navigateService);
        }

        private static string? GetErrorTypeFromDiagnostic(DiagnosticData diagnostic)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                return diagnostic.CustomTags.Contains(WellKnownDiagnosticTags.EditAndContinue)
                    ? EditAndContinueErrorTypeDefinition.Name
                    : PredefinedErrorTypeNames.SyntaxError;
            }
            else if (diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                return PredefinedErrorTypeNames.Warning;
            }
            else
            {
                return null;
            }
        }
    }
}
