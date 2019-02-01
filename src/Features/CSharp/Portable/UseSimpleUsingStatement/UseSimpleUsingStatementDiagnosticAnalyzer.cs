﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.UseSimpleUsingStatement
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class UseSimpleUsingStatementDiagnosticAnalyzer : AbstractBuiltInCodeStyleDiagnosticAnalyzer
    {
        public UseSimpleUsingStatementDiagnosticAnalyzer()
            : base(IDEDiagnosticIds.UseSimpleUsingStatementDiagnosticId,
                   new LocalizableResourceString(nameof(FeaturesResources.Use_simple_using_statement), FeaturesResources.ResourceManager, typeof(FeaturesResources)),
                   new LocalizableResourceString(nameof(FeaturesResources.using_statement_can_be_simplified), FeaturesResources.ResourceManager, typeof(FeaturesResources)))
        {
        }

        public override bool OpenFileOnly(Workspace workspace) => false;
        public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
            => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

        protected override void InitializeWorker(AnalysisContext context)
            => context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.UsingStatement);

        private void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
        {
            var usingStatement = (UsingStatementSyntax)context.Node;

            if (!(usingStatement.Parent is BlockSyntax parentBlock))
            {
                // Don't offer on a using statement that is parented by another using statement.
                // We'll just offer on the topmost using statement.
                return;
            }

            // Check that all the immediately nested usings are convertible as well.  
            // We don't want take a sequence of nested-using and only convert some of them.
            for (var current = usingStatement; current != null; current = current.Statement as UsingStatementSyntax)
            {
                if (current.Declaration == null)
                {
                    return;
                }
            }

            // Verify that changing this using-statement into a using-declaration will not
            // change semantics.
            if (!PreservesSemantics(parentBlock, usingStatement))
            {
                return;
            }

            var syntaxTree = context.Node.SyntaxTree;
            var options = (CSharpParseOptions)syntaxTree.Options;
            if (options.LanguageVersion < LanguageVersion.CSharp8)
            {
                return;
            }

            var cancellationToken = context.CancellationToken;
            var optionSet = context.Options.GetDocumentOptionSetAsync(syntaxTree, cancellationToken).GetAwaiter().GetResult();
            if (optionSet == null)
            {
                return;
            }

            var option = optionSet.GetOption(CSharpCodeStyleOptions.PreferSimpleUsingStatement);
            if (!option.Value)
            {
                return;
            }

            // Good to go!
            context.ReportDiagnostic(DiagnosticHelper.Create(
                Descriptor,
                usingStatement.UsingKeyword.GetLocation(),
                option.Notification.Severity,
                additionalLocations: ImmutableArray.Create(usingStatement.GetLocation()),
                properties: null));
        }

        private static bool PreservesSemantics(
            BlockSyntax parentBlock, UsingStatementSyntax usingStatement)
        {
            // Has to be one of the following forms:
            // 1. Using statement is the last statement in the parent.
            // 2. Using statement is not the last statement in parent, but is followed by 
            //    something that is unaffected by simplifying the using statement.  i.e.
            //    `return`/`break`/`continue`.  *Note*.  `return expr` would *not* be ok.
            //    In that case, `expr` would now be evaluated *before* the using disposed
            //    the resource, instead of afterwards.  Effectly, the statement following
            //    cannot actually execute any code that might depend on the .Dispose method
            //    being called or not.
            var statements = parentBlock.Statements;

            var index = statements.IndexOf(usingStatement);
            if (index == statements.Count - 1)
            {
                // very last statement in the block.  Can be converted.
                return true;
            }

            // Not the last statement, get the next statement and examine that.
            var nextStatement = statements[index + 1];
            if (nextStatement is BreakStatementSyntax ||
                nextStatement is ContinueStatementSyntax)
            {
                // using statemnet followed by break/continue.  Can conver this as executing 
                // the break/continue will cause the code to exit the using scope, causing 
                // Dispose to be called at the same place as before.
                return true;
            }

            if (nextStatement is ReturnStatementSyntax returnStatement &&
                returnStatement.Expression == null)
            {
                // using statement followed by `return`.  Can conver this as executing 
                // the `return` will cause the code to exit the using scope, causing 
                // Dispose to be called at the same place as before.
                //
                // Note: the expr has to be null.  If it was non-null, then the expr would
                // now execute before hte using called 'Dispose' instead of after, potentially
                // changing semantics.
                return true;
            }

            // Add any additional cases here in the future.
            return false;
        }
    }
}
