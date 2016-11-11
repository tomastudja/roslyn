// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Semantics;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.PopulateSwitch
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed class PopulateSwitchDiagnosticAnalyzer : 
        AbstractCodeStyleDiagnosticAnalyzer, IBuiltInAnalyzer
    {
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(FeaturesResources.Add_missing_cases), FeaturesResources.ResourceManager, typeof(FeaturesResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(WorkspacesResources.Populate_switch), WorkspacesResources.ResourceManager, typeof(WorkspacesResources));

        public PopulateSwitchDiagnosticAnalyzer()
            : base(IDEDiagnosticIds.PopulateSwitchDiagnosticId,
                  s_localizableTitle, s_localizableMessage)
        {
        }

        #region Interface methods

        public bool OpenFileOnly(Workspace workspace) => false;

        private static MethodInfo s_registerMethod = typeof(AnalysisContext).GetTypeInfo().GetDeclaredMethod("RegisterOperationActionImmutableArrayInternal");

        protected override void InitializeWorker(AnalysisContext context)
            => s_registerMethod.Invoke(context, new object[]
               {
                   new Action<OperationAnalysisContext>(AnalyzeOperation),
                   ImmutableArray.Create(OperationKind.SwitchStatement)
               });

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            var switchOperation = (ISwitchStatement)context.Operation;
            var switchBlock = switchOperation.Syntax;
            var tree = switchBlock.SyntaxTree;

            bool missingCases;
            bool missingDefaultCase;
            if (SwitchIsIncomplete(switchOperation, out missingCases, out missingDefaultCase) &&
                !tree.OverlapsHiddenPosition(switchBlock.Span, context.CancellationToken))
            {
                Debug.Assert(missingCases || missingDefaultCase);
                var properties = ImmutableDictionary<string, string>.Empty
                    .Add(PopulateSwitchHelpers.MissingCases, missingCases.ToString())
                    .Add(PopulateSwitchHelpers.MissingDefaultCase, missingDefaultCase.ToString());

                var diagnostic = Diagnostic.Create(
                    HiddenDescriptor, switchBlock.GetLocation(), properties: properties);
                context.ReportDiagnostic(diagnostic);
            }
        }

        #endregion

        private bool SwitchIsIncomplete(
            ISwitchStatement switchStatement,
            out bool missingCases, out bool missingDefaultCase)
        {
            var missingEnumMembers = PopulateSwitchHelpers.GetMissingEnumMembers(switchStatement);

            missingCases = missingEnumMembers.Count > 0;
            missingDefaultCase = !PopulateSwitchHelpers.HasDefaultCase(switchStatement);

            // The switch is incomplete if we're missing any cases or we're missing a default case.
            return missingDefaultCase || missingCases;
        }

        public DiagnosticAnalyzerCategory GetAnalyzerCategory() => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;
    }
}