// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.Suggestions
{
    /// <summary>
    /// Represents light bulb menu item for code fixes.
    /// </summary>
    internal class CodeFixSuggestedAction : SuggestedActionWithFlavors, ITelemetryDiagnosticID<string>
    {
        private readonly CodeFix _fix;
        private readonly SuggestedActionSet _fixAllSuggestedActionSet;

        public CodeFixSuggestedAction(
            Workspace workspace,
            ITextBuffer subjectBuffer,
            ICodeActionEditHandlerService editHandler,
            IWaitIndicator waitIndicator,
            CodeFix fix,
            CodeAction action,
            object provider,
            SuggestedActionSet fixAllSuggestedActionSet,
            IAsynchronousOperationListener operationListener)
            : base(workspace, subjectBuffer, editHandler, waitIndicator, action, provider, operationListener)
        {
            _fix = fix;
            _fixAllSuggestedActionSet = fixAllSuggestedActionSet;
        }

        /// <summary>
        /// If the provided fix all context is non-null and the context's code action Id matches the given code action's Id then,
        /// returns the set of fix all occurrences actions associated with the code action.
        /// </summary>
        internal static SuggestedActionSet GetFixAllSuggestedActionSet(
            CodeAction action,
            int actionCount,
            FixAllProvider fixAllProvider,
            FixAllCodeActionContext fixAllCodeActionContext,
            IEnumerable<FixAllScope> supportedScopes,
            Diagnostic firstDiagnostic,
            Workspace workspace,
            ITextBuffer subjectBuffer,
            ICodeActionEditHandlerService editHandler,
            IWaitIndicator waitIndicator,
            IAsynchronousOperationListener operationListener)
        {
            if (fixAllCodeActionContext == null)
            {
                return null;
            }

            if (actionCount > 1 && action.EquivalenceKey == null)
            {
                return null;
            }

            var fixAllSuggestedActions = ImmutableArray.CreateBuilder<FixAllSuggestedAction>();
            foreach (var scope in supportedScopes)
            {
                var fixAllContext = GetContextForScopeAndActionId(fixAllCodeActionContext, scope, action.EquivalenceKey);
                var fixAllAction = new FixAllCodeAction(fixAllContext, fixAllProvider, showPreviewChangesDialog: true);
                var fixAllSuggestedAction = new FixAllSuggestedAction(
                    workspace, subjectBuffer, editHandler, waitIndicator, fixAllAction,
                    fixAllProvider, firstDiagnostic, operationListener);
                fixAllSuggestedActions.Add(fixAllSuggestedAction);
            }

            return new SuggestedActionSet(fixAllSuggestedActions.ToImmutable(), title: EditorFeaturesResources.FixAllOccurrencesIn);
        }

        /// <summary>
        /// Transforms this context into the public <see cref="FixAllContext"/> to be used for <see cref="FixAllProvider.GetFixAsync(FixAllContext)"/> invocation.
        /// </summary>
        private static FixAllContext GetContextForScopeAndActionId(
            FixAllCodeActionContext context,
            FixAllScope scope, string codeActionEquivalenceKey)
        {
            if (context.Scope == scope && context.CodeActionEquivalenceKey == codeActionEquivalenceKey)
            {
                return context;
            }

            if (context.Document != null)
            {
                return new FixAllContext(context.Document, context.CodeFixProvider, scope, codeActionEquivalenceKey,
                    context.DiagnosticIds, context.GetDiagnosticProvider(), context.CancellationToken);
            }

            return new FixAllContext(context.Project, context.CodeFixProvider, scope, codeActionEquivalenceKey,
                    context.DiagnosticIds, context.GetDiagnosticProvider(), context.CancellationToken);
        }

        public string GetDiagnosticID()
        {
            var diagnostic = _fix.PrimaryDiagnostic;

            // we log diagnostic id as it is if it is from us
            if (diagnostic.Descriptor.CustomTags.Any(t => t == WellKnownDiagnosticTags.Telemetry))
            {
                return diagnostic.Id;
            }

            // if it is from third party, we use hashcode
            return diagnostic.GetHashCode().ToString(CultureInfo.InvariantCulture);
        }

        protected override DiagnosticData GetDiagnostic()
        {
            return _fix.GetPrimaryDiagnosticData();
        }

        protected override SuggestedActionSet GetFixAllSuggestedActionSet()
        {
            return _fixAllSuggestedActionSet;
        }
    }
}
