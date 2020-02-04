﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics.SimplifyTypeNames
{
    public partial class BatchFixerTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        internal override (DiagnosticAnalyzer, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
            => (new QualifyWithThisAnalyzer(), new QualifyWithThisFixer());

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        private class QualifyWithThisAnalyzer : DiagnosticAnalyzer
        {
            public static readonly DiagnosticDescriptor Descriptor = DescriptorFactory.CreateSimpleDescriptor("QualifyWithThis");

            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            {
                get
                {
                    return ImmutableArray.Create(Descriptor);
                }
            }

            public override void Initialize(AnalysisContext context)
            {
                context.RegisterSyntaxNodeAction<SyntaxKind>(AnalyzeNode, SyntaxKind.IdentifierName);
            }

            private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
            {
                if (context.Node is SimpleNameSyntax node)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(node).Symbol;
                    if (symbol != null && symbol.Kind == SymbolKind.Field)
                    {
                        var diagnostic = Diagnostic.Create(Descriptor, node.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private class QualifyWithThisFixer : CodeFixProvider
        {
            public override ImmutableArray<string> FixableDiagnosticIds
            {
                get
                {
                    return ImmutableArray.Create(QualifyWithThisAnalyzer.Descriptor.Id);
                }
            }

            public async override Task RegisterCodeFixesAsync(CodeFixContext context)
            {
                var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
                if (root.FindNode(context.Span, getInnermostNodeForTie: true) is SimpleNameSyntax node)
                {
                    var leadingTrivia = node.GetLeadingTrivia();
                    var newNode = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ThisExpression(),
                        node.WithoutLeadingTrivia())
                        .WithLeadingTrivia(leadingTrivia);

                    var newRoot = root.ReplaceNode(node, newNode);
                    var newDocument = context.Document.WithSyntaxRoot(newRoot);

                    // Disable RS0005 as this is test code and we don't need telemetry for created code action.
#pragma warning disable RS0005 // Do not use generic CodeAction.Create to create CodeAction
                    var fix = CodeAction.Create("QualifyWithThisFix", _ => Task.FromResult(newDocument));
#pragma warning restore RS0005 // Do not use generic CodeAction.Create to create CodeAction

                    context.RegisterCodeFix(fix, context.Diagnostics);
                }
            }

            public override FixAllProvider GetFixAllProvider()
            {
                return WellKnownFixAllProviders.BatchFixer;
            }
        }

        #region "Fix all occurrences tests"

        [Fact, WorkItem(320, "https://github.com/dotnet/roslyn/issues/320")]
        [Trait(Traits.Feature, Traits.Features.CodeActionsFixAllOccurrences)]
        public async Task TestFixAllInDocument_QualifyWithThis()
        {
            var input = @"
<Workspace>
    <Project Language=""C#"" AssemblyName=""Assembly1"" CommonReferences=""true"">
        <Document>
class C
{
    int Sign;
    void F()
    {
        string x = @""namespace Namespace
    {
        class Type
        {
            void Goo()
            {
                int x = 1 "" + {|FixAllInDocument:Sign|} + @"" "" + Sign + @""3;
            }
        }
    }
"";
    }
}
        </Document>
    </Project>
</Workspace>";

            var expected = @"
<Workspace>
    <Project Language=""C#"" AssemblyName=""Assembly1"" CommonReferences=""true"">
        <Document>
class C
{
    int Sign;
    void F()
    {
        string x = @""namespace Namespace
    {
        class Type
        {
            void Goo()
            {
                int x = 1 "" + this.Sign + @"" "" + this.Sign + @""3;
            }
        }
    }
"";
    }
}
        </Document>
    </Project>
</Workspace>";

            await TestInRegularAndScriptAsync(input, expected);
        }

        #endregion
    }
}
