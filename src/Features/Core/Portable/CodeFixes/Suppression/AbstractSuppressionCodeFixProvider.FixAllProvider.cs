﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeFixes.Suppression
{
    internal abstract partial class AbstractSuppressionCodeFixProvider : ISuppressionFixProvider
    {
        private class SuppressionFixAllProvider : FixAllProvider
        {
            public static readonly SuppressionFixAllProvider Instance = new SuppressionFixAllProvider();

            private SuppressionFixAllProvider()
            {
            }

            public async override Task<CodeAction> GetFixAsync(FixAllContext fixAllContext)
            {
                var batchFixer = (BatchFixAllProvider)WellKnownFixAllProviders.BatchFixer;
                var suppressionFixer = (AbstractSuppressionCodeFixProvider)((WrapperCodeFixProvider)fixAllContext.CodeFixProvider).SuppressionFixProvider;
                var isGlobalSuppression = NestedSuppressionCodeAction.IsEquivalenceKeyForGlobalSuppression(fixAllContext.CodeActionEquivalenceKey);
                if (!isGlobalSuppression)
                {
                    var isPragmaWarningSuppression = NestedSuppressionCodeAction.IsEquivalenceKeyForPragmaWarning(fixAllContext.CodeActionEquivalenceKey);
                    Contract.ThrowIfFalse(isPragmaWarningSuppression || NestedSuppressionCodeAction.IsEquivalenceKeyForRemoveSuppression(fixAllContext.CodeActionEquivalenceKey));

                    batchFixer = isPragmaWarningSuppression ?
                        new PragmaWarningBatchFixAllProvider(suppressionFixer) :
                        RemoveSuppressionCodeAction.GetBatchFixer(suppressionFixer);
                }

                var title = fixAllContext.CodeActionEquivalenceKey;
                if (fixAllContext.Document != null)
                {
#if false
                    var documentsAndDiagnosticsToFixMap = fixMultipleContext != null ?
                        fixMultipleContext.DocumentDiagnosticsToFix :
                        await batchFixer.GetDocumentDiagnosticsToFixAsync(fixAllContext).ConfigureAwait(false);
#endif

                    var documentsAndDiagnosticsToFixMap = 
                        await fixAllContext.GetDocumentDiagnosticsToFixAsync(batchFixer).ConfigureAwait(false);

                    return !isGlobalSuppression ?
                        await batchFixer.GetFixAsync(documentsAndDiagnosticsToFixMap, fixAllContext).ConfigureAwait(false) :
                        GlobalSuppressMessageFixAllCodeAction.Create(title, suppressionFixer, fixAllContext.Document, documentsAndDiagnosticsToFixMap);
                }
                else
                {
#if false
                    var projectsAndDiagnosticsToFixMap = fixMultipleContext != null ?
                        fixMultipleContext.ProjectDiagnosticsToFix :
                        await batchFixer.GetProjectDiagnosticsToFixAsync(fixAllContext).ConfigureAwait(false);
#endif
                    var projectsAndDiagnosticsToFixMap =
                        await fixAllContext.GetProjectDiagnosticsToFixAsync(batchFixer).ConfigureAwait(false);

                    return !isGlobalSuppression ?
                        await batchFixer.GetFixAsync(projectsAndDiagnosticsToFixMap, fixAllContext).ConfigureAwait(false) :
                        GlobalSuppressMessageFixAllCodeAction.Create(title, suppressionFixer, fixAllContext.Project, projectsAndDiagnosticsToFixMap);
                }
            }
        }
    }
}
