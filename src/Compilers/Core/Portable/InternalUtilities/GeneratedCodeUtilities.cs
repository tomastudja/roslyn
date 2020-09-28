﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Utilities
{
    internal static class GeneratedCodeUtilities
    {
        private static readonly string[] s_autoGeneratedStrings = new[] { "<autogenerated", "<auto-generated" };

        internal static bool IsGeneratedSymbolWithGeneratedCodeAttribute(
            ISymbol symbol, INamedTypeSymbol generatedCodeAttribute)
        {
            RoslynDebug.Assert(symbol != null);
            RoslynDebug.Assert(generatedCodeAttribute != null);

            // Don't check this for namespaces.  Namespaces cannot have attributes on them. And, 
            // currently, calling DeclaringSyntaxReferences on an INamespaceSymbol is more expensive
            // than is desirable.
            if (symbol.Kind != SymbolKind.Namespace)
            {
                // GeneratedCodeAttribute can only be applied once on a symbol.
                // For partial symbols with more than one definition, we must treat them as non-generated code symbols.
                if (symbol.DeclaringSyntaxReferences.Length > 1)
                {
                    return false;
                }

                foreach (var attribute in symbol.GetAttributes())
                {
                    if (generatedCodeAttribute.Equals(attribute.AttributeClass))
                    {
                        return true;
                    }
                }
            }

            return symbol.ContainingSymbol != null && IsGeneratedSymbolWithGeneratedCodeAttribute(symbol.ContainingSymbol, generatedCodeAttribute);
        }

        internal static bool IsGeneratedCode(
            SyntaxTree tree, Func<SyntaxTrivia, bool> isComment, CancellationToken cancellationToken)
        {
            return IsGeneratedCodeFile(tree.FilePath) ||
                   BeginsWithAutoGeneratedComment(tree, isComment, cancellationToken);
        }

        internal static bool IsGeneratedCode(string? filePath, SyntaxNode root, Func<SyntaxTrivia, bool> isComment)
        {
            return IsGeneratedCodeFile(filePath) ||
                   BeginsWithAutoGeneratedComment(root, isComment);
        }

        private static bool IsGeneratedCodeFile([NotNullWhen(returnValue: true)] string? filePath)
        {
            if (!RoslynString.IsNullOrEmpty(filePath))
            {
                var fileName = PathUtilities.GetFileName(filePath);
                if (fileName.StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var extension = PathUtilities.GetExtension(fileName);
                if (!string.IsNullOrEmpty(extension))
                {
                    var fileNameWithoutExtension = PathUtilities.GetFileName(filePath, includeExtension: false);
                    if (fileNameWithoutExtension.EndsWith(".designer", StringComparison.OrdinalIgnoreCase) ||
                        fileNameWithoutExtension.EndsWith(".generated", StringComparison.OrdinalIgnoreCase) ||
                        fileNameWithoutExtension.EndsWith(".g", StringComparison.OrdinalIgnoreCase) ||
                        fileNameWithoutExtension.EndsWith(".g.i", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool BeginsWithAutoGeneratedComment(SyntaxNode root, Func<SyntaxTrivia, bool> isComment)
        {
            if (root.HasLeadingTrivia)
            {
                var leadingTrivia = root.GetLeadingTrivia();

                foreach (var trivia in leadingTrivia)
                {
                    if (!isComment(trivia))
                    {
                        continue;
                    }

                    var text = trivia.ToString();

                    // Check to see if the text of the comment contains an auto generated comment.
                    foreach (var autoGenerated in s_autoGeneratedStrings)
                    {
                        if (text.Contains(autoGenerated))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool BeginsWithAutoGeneratedComment(
            SyntaxTree tree, Func<SyntaxTrivia, bool> isComment, CancellationToken cancellationToken)
        {
            var root = tree.GetRoot(cancellationToken);
            if (root.HasLeadingTrivia)
            {
                var leadingTrivia = root.GetLeadingTrivia();

                foreach (var trivia in leadingTrivia)
                {
                    if (!isComment(trivia))
                    {
                        continue;
                    }

                    var text = trivia.ToString();

                    // Check to see if the text of the comment contains an auto generated comment.
                    foreach (var autoGenerated in s_autoGeneratedStrings)
                    {
                        if (text.Contains(autoGenerated))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        internal static GeneratedKind GetIsGeneratedCodeFromOptions(ImmutableDictionary<string, string> options)
        {
            // Check for explicit user configuration for generated code.
            //     generated_code = true | false
            if (options.TryGetValue("generated_code", out string? optionValue) &&
                bool.TryParse(optionValue, out var boolValue))
            {
                return boolValue ? GeneratedKind.MarkedGenerated : GeneratedKind.NotGenerated;
            }

            // Either no explicit user configuration or we don't recognize the option value.
            return GeneratedKind.Unknown;
        }

        internal static bool? GetIsGeneratedCodeFromOptions(AnalyzerConfigOptions options)
        {
            // Check for explicit user configuration for generated code.
            //     generated_code = true | false
            if (options.TryGetValue("generated_code", out string? optionValue) &&
                bool.TryParse(optionValue, out var boolValue))
            {
                return boolValue;
            }

            // Either no explicit user configuration or we don't recognize the option value.
            return null;
        }
    }
}
