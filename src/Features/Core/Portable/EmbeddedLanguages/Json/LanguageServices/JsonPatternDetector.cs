﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis.EmbeddedLanguages.LanguageServices;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Features.EmbeddedLanguages.Json.LanguageServices
{
    /// <summary>
    /// Helper class to detect json in string tokens in a document efficiently.
    /// </summary>
    internal class JsonPatternDetector
    {
        private const string s_jsonDotNetParameterName = "json";
        private const string s_jsonDotNetMethodNameOfInterest = "Parse";
        private static readonly HashSet<string> s_jsonDotNetTypeNamesOfInterest = new()
        {
            "Newtonsoft.Json.Linq.JToken",
            "Newtonsoft.Json.Linq.JObject",
            "Newtonsoft.Json.Linq.JArray"
        };

        private static readonly ConditionalWeakTable<SemanticModel, JsonPatternDetector> s_modelToDetector = new();

        private readonly SemanticModel _semanticModel;
        private readonly EmbeddedLanguageInfo _info;
        private readonly ISet<INamedTypeSymbol> _typesOfInterest;

        /// <summary>
        /// Helps match patterns of the form: language=json
        /// 
        /// All matching is case insensitive, with spaces allowed between the punctuation.
        /// </summary>
        private static readonly LanguageCommentDetector<JsonOptions> s_languageCommentDetector = new("json");

        public JsonPatternDetector(
            SemanticModel semanticModel,
            EmbeddedLanguageInfo info,
            ISet<INamedTypeSymbol> typesOfInterest)
        {
            _semanticModel = semanticModel;
            _info = info;
            _typesOfInterest = typesOfInterest;
        }

        public static JsonPatternDetector GetOrCreate(
            SemanticModel semanticModel, EmbeddedLanguageInfo info)
        {
            // Do a quick non-allocating check first.
            if (s_modelToDetector.TryGetValue(semanticModel, out var detector))
                return detector;

            return s_modelToDetector.GetValue(
                semanticModel, _ => Create(semanticModel, info));
        }

        private static JsonPatternDetector Create(
            SemanticModel semanticModel, EmbeddedLanguageInfo info)
        {
            var types = s_jsonDotNetTypeNamesOfInterest.Select(t => semanticModel.Compilation.GetTypeByMetadataName(t)).WhereNotNull().ToSet();
            return new JsonPatternDetector(semanticModel, info, types);
        }

        public static bool IsDefinitelyNotJson(SyntaxToken token, ISyntaxFacts syntaxFacts)
        {
            if (!syntaxFacts.IsStringLiteral(token))
                return true;

            if (token.ValueText == "")
                return true;

            return false;
        }

        private static bool HasJsonLanguageComment(
            SyntaxToken token, ISyntaxFacts syntaxFacts, out JsonOptions options)
        {
            if (HasJsonLanguageComment(token.GetPreviousToken().TrailingTrivia, syntaxFacts, out options))
                return true;

            for (var node = token.Parent; node != null; node = node.Parent)
            {
                if (HasJsonLanguageComment(node.GetLeadingTrivia(), syntaxFacts, out options))
                    return true;
            }

            options = default;
            return false;
        }

        private static bool HasJsonLanguageComment(
            SyntaxTriviaList list, ISyntaxFacts syntaxFacts, out JsonOptions options)
        {
            foreach (var trivia in list)
            {
                if (HasJsonLanguageComment(trivia, syntaxFacts, out options))
                    return true;
            }

            options = default;
            return false;
        }

        private static bool HasJsonLanguageComment(
            SyntaxTrivia trivia, ISyntaxFacts syntaxFacts, out JsonOptions options)
        {
            if (syntaxFacts.IsRegularComment(trivia))
            {
                var text = trivia.ToString();
                return s_languageCommentDetector.TryMatch(text, out options);
            }

            options = default;
            return false;
        }

        private static bool IsMethodArgument(SyntaxToken token, ISyntaxFacts syntaxFacts)
            => syntaxFacts.IsLiteralExpression(token.Parent) &&
               syntaxFacts.IsArgument(token.Parent.Parent) &&
               syntaxFacts.IsInvocationExpression(token.Parent.Parent.Parent?.Parent);

        public bool IsDefinitelyJson(SyntaxToken token, CancellationToken cancellationToken)
        {
            var syntaxFacts = _info.SyntaxFacts;
            if (IsDefinitelyNotJson(token, syntaxFacts))
                return false;

            if (HasJsonLanguageComment(token, syntaxFacts, out _))
                return true;

            if (!IsMethodArgument(token, syntaxFacts))
                return false;

            var stringLiteral = token;
            var literalNode = stringLiteral.GetRequiredParent();
            var argumentNode = literalNode.GetRequiredParent();
            Debug.Assert(syntaxFacts.IsArgument(argumentNode));

            var argumentList = argumentNode.GetRequiredParent();
            var invocationOrCreation = argumentList.Parent;
            if (syntaxFacts.IsInvocationExpression(invocationOrCreation))
            {
                var invokedExpression = syntaxFacts.GetExpressionOfInvocationExpression(invocationOrCreation);
                var name = GetNameOfInvokedExpression(invokedExpression);
                if (syntaxFacts.StringComparer.Equals(name, s_jsonDotNetMethodNameOfInterest))
                {
                    // Is a string argument to a method that looks like it could be a json-parsing
                    // method. Need to do deeper analysis
                    var method = _semanticModel.GetSymbolInfo(invocationOrCreation, cancellationToken).GetAnySymbol();
                    if (method != null &&
                        method.DeclaredAccessibility == Accessibility.Public &&
                        method.IsStatic &&
                        _typesOfInterest.Contains(method.ContainingType))
                    {
                        return IsArgumentToParameterWithName(
                            argumentNode, s_jsonDotNetParameterName, cancellationToken);
                    }
                }
            }

            return false;
        }

        public JsonTree? TryParseJson(SyntaxToken token)
        {
            var syntaxFacts = _info.SyntaxFacts;
            if (IsDefinitelyNotJson(token, syntaxFacts))
                return null;

            HasJsonLanguageComment(token, syntaxFacts, out var options);

            var chars = _info.VirtualCharService.TryConvertToVirtualChars(token);
            if (chars.IsDefaultOrEmpty)
                return null;

            return JsonParser.TryParse(chars, options);
        }

        private bool IsArgumentToParameterWithName(
            SyntaxNode argumentNode, string name, CancellationToken cancellationToken)
        {
            var parameter = _info.SemanticFacts.FindParameterForArgument(_semanticModel, argumentNode, cancellationToken);
            return parameter?.Name == name;
        }

        private string? GetNameOfInvokedExpression(SyntaxNode invokedExpression)
        {
            var syntaxFacts = _info.SyntaxFacts;
            if (syntaxFacts.IsSimpleMemberAccessExpression(invokedExpression))
            {
                return syntaxFacts.GetIdentifierOfSimpleName(syntaxFacts.GetNameOfMemberAccessExpression(invokedExpression)).ValueText;
            }
            else if (syntaxFacts.IsIdentifierName(invokedExpression))
            {
                return syntaxFacts.GetIdentifierOfSimpleName(invokedExpression).ValueText;
            }

            return null;
        }

        public bool IsProbablyJson(SyntaxToken token)
        {
            var tree = TryParseJson(token);
            if (tree == null || !tree.Diagnostics.IsEmpty)
                return false;

            return ContainsProbableJsonObject(tree.Root);
        }

        private static bool ContainsProbableJsonObject(JsonNode node)
        {
            if (node.Kind == JsonKind.Object)
            {
                var objNode = (JsonObjectNode)node;
                if (objNode.Sequence.Length >= 1)
                    return true;
            }

            foreach (var child in node)
            {
                if (child.IsNode)
                {
                    if (ContainsProbableJsonObject(child.Node))
                        return true;
                }
            }

            return false;
        }

        internal static class TestAccessor
        {
            public static bool TryMatch(string text, out JsonOptions options)
                => s_languageCommentDetector.TryMatch(text, out options);
        }
    }
}
