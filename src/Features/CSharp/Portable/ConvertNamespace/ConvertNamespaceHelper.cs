﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.ConvertNamespace
{
    internal static class ConvertNamespaceHelper
    {
        public static (string title, string equivalenceKey) GetInfo(NamespaceDeclarationPreference preference)
            => preference switch
            {
                NamespaceDeclarationPreference.BlockScoped => (CSharpFeaturesResources.Convert_to_block_scoped_namespace, nameof(CSharpFeaturesResources.Convert_to_block_scoped_namespace)),
                NamespaceDeclarationPreference.FileScoped => (CSharpFeaturesResources.Convert_to_file_scoped_namespace, nameof(CSharpFeaturesResources.Convert_to_file_scoped_namespace)),
                _ => throw ExceptionUtilities.UnexpectedValue(preference),
            };

        public static bool CanOfferUseBlockScoped(OptionSet optionSet, BaseNamespaceDeclarationSyntax declaration, bool forAnalyzer)
        {
            if (declaration is not FileScopedNamespaceDeclarationSyntax)
                return false;

            var option = optionSet.GetOption(CSharpCodeStyleOptions.NamespaceDeclarations);
            var userPrefersRegularNamespaces = option.Value == NamespaceDeclarationPreference.BlockScoped;
            var analyzerDisabled = option.Notification.Severity == ReportDiagnostic.Suppress;
            var forRefactoring = !forAnalyzer;

            // If the user likes regular namespaces, then we offer regular namespaces from the diagnostic analyzer.
            // If the user does not like regular namespaces then we offer regular namespaces bodies from the refactoring provider.
            // If the analyzer is disabled completely, the refactoring is enabled in both directions.
            var canOffer = userPrefersRegularNamespaces == forAnalyzer || (forRefactoring && analyzerDisabled);
            return canOffer;
        }

        internal static bool CanOfferUseFileScoped(OptionSet optionSet, CompilationUnitSyntax root, BaseNamespaceDeclarationSyntax declaration, bool forAnalyzer)
        {
            if (declaration is not NamespaceDeclarationSyntax namespaceDeclaration)
                return false;

            if (namespaceDeclaration.OpenBraceToken.IsMissing)
                return false;

            if (((CSharpParseOptions)root.SyntaxTree.Options).LanguageVersion < LanguageVersion.CSharp10)
                return false;

            var option = optionSet.GetOption(CSharpCodeStyleOptions.NamespaceDeclarations);
            var userPrefersFileScopedNamespaces = option.Value == NamespaceDeclarationPreference.FileScoped;
            var analyzerDisabled = option.Notification.Severity == ReportDiagnostic.Suppress;
            var forRefactoring = !forAnalyzer;

            // If the user likes file scoped namespaces, then we offer file scoped namespaces from the diagnostic analyzer.
            // If the user does not like file scoped namespaces then we offer file scoped namespaces from the refactoring provider.
            // If the analyzer is disabled completely, the refactoring is enabled in both directions.
            var canOffer = userPrefersFileScopedNamespaces == forAnalyzer || (forRefactoring && analyzerDisabled);
            if (!canOffer)
                return false;

            // even if we could offer this here, we have to make sure it would be legal.  A file scoped namespace is
            // only legal if it's the only namespace in the file and there are no top level statements.
            var tooManyNamespaces = root.DescendantNodesAndSelf(n => n is CompilationUnitSyntax || n is BaseNamespaceDeclarationSyntax)
                                        .OfType<BaseNamespaceDeclarationSyntax>()
                                        .Take(2)
                                        .Count() != 1;
            if (tooManyNamespaces)
                return false;

            if (root.Members.Any(m => m is GlobalStatementSyntax))
                return false;

            return true;
        }

        public static async Task<Document> ConvertAsync(
            Document document, BaseNamespaceDeclarationSyntax baseNamespace, CancellationToken cancellationToken)
        {
            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return document.WithSyntaxRoot(root.ReplaceNode(baseNamespace, Convert(baseNamespace)));
        }

        public static BaseNamespaceDeclarationSyntax Convert(BaseNamespaceDeclarationSyntax baseNamespace)
        {
            return baseNamespace switch
            {
                FileScopedNamespaceDeclarationSyntax fileScopedNamespace => ConvertFileScopedNamespace(fileScopedNamespace),
                NamespaceDeclarationSyntax namespaceDeclaration => ConvertNamespaceDeclaration(namespaceDeclaration),
                _ => throw ExceptionUtilities.UnexpectedValue(baseNamespace.Kind()),
            };
        }

        private static bool HasLeadingBlankLine(
            SyntaxToken token, out SyntaxToken withoutBlankLine)
        {
            var leadingTrivia = token.LeadingTrivia;

            if (leadingTrivia.Count >= 1 && leadingTrivia[0].Kind() == SyntaxKind.EndOfLineTrivia)
            {
                withoutBlankLine = token.WithLeadingTrivia(leadingTrivia.RemoveAt(0));
                return true;
            }

            if (leadingTrivia.Count >= 2 && leadingTrivia[0].IsKind(SyntaxKind.WhitespaceTrivia) && leadingTrivia[1].IsKind(SyntaxKind.EndOfLineTrivia))
            {
                withoutBlankLine = token.WithLeadingTrivia(leadingTrivia.Skip(2));
                return true;
            }

            withoutBlankLine = default;
            return false;
        }

        private static FileScopedNamespaceDeclarationSyntax ConvertNamespaceDeclaration(NamespaceDeclarationSyntax namespaceDeclaration)
        {
            var fileScopedNamespace = SyntaxFactory.FileScopedNamespaceDeclaration(
                namespaceDeclaration.AttributeLists,
                namespaceDeclaration.Modifiers,
                namespaceDeclaration.NamespaceKeyword,
                namespaceDeclaration.Name,
                SyntaxFactory.Token(SyntaxKind.SemicolonToken).WithTrailingTrivia(namespaceDeclaration.OpenBraceToken.TrailingTrivia),
                namespaceDeclaration.Externs,
                namespaceDeclaration.Usings,
                namespaceDeclaration.Members).WithAdditionalAnnotations(Formatter.Annotation);

            // Ensure there's a blank line between the namespace line and the first body member.
            var firstBodyToken = fileScopedNamespace.SemicolonToken.GetNextToken();
            if (firstBodyToken.Kind() != SyntaxKind.EndOfFileToken &&
                !HasLeadingBlankLine(firstBodyToken, out _))
            {
                fileScopedNamespace = fileScopedNamespace.ReplaceToken(
                    firstBodyToken,
                    firstBodyToken.WithPrependedLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));
            }

            return fileScopedNamespace;
        }

        private static NamespaceDeclarationSyntax ConvertFileScopedNamespace(FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
        {
            var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
                fileScopedNamespace.AttributeLists,
                fileScopedNamespace.Modifiers,
                fileScopedNamespace.NamespaceKeyword,
                fileScopedNamespace.Name,
                SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(fileScopedNamespace.SemicolonToken.TrailingTrivia),
                fileScopedNamespace.Externs,
                fileScopedNamespace.Usings,
                fileScopedNamespace.Members,
                SyntaxFactory.Token(SyntaxKind.CloseBraceToken),
                semicolonToken: default).WithAdditionalAnnotations(Formatter.Annotation);

            // Ensure there is no errant blank line between the open curly and the first body element.
            var firstBodyToken = namespaceDeclaration.OpenBraceToken.GetNextToken();
            if (firstBodyToken != namespaceDeclaration.CloseBraceToken &&
                firstBodyToken.Kind() != SyntaxKind.EndOfFileToken &&
                HasLeadingBlankLine(firstBodyToken, out var firstBodyTokenWithoutBlankLine))
            {
                namespaceDeclaration = namespaceDeclaration.ReplaceToken(firstBodyToken, firstBodyTokenWithoutBlankLine);
            }

            return namespaceDeclaration;
        }
    }
}
