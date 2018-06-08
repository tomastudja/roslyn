﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.EmbeddedLanguages.Common;
using Microsoft.CodeAnalysis.EmbeddedLanguages.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.EmbeddedLanguages.Json.LanguageServices
{
    using static EmbeddedSyntaxHelpers;

    using JsonToken = EmbeddedSyntaxToken<JsonKind>;
    using JsonTrivia = EmbeddedSyntaxTrivia<JsonKind>;

    /// <summary>
    /// Classifier impl for embedded json strings.
    /// </summary>
    internal class JsonEmbeddedClassifier : IEmbeddedClassifier
    {
        private static ObjectPool<Visitor> _visitorPool = new ObjectPool<Visitor>(() => new Visitor());
        private readonly JsonEmbeddedLanguage _language;

        public JsonEmbeddedClassifier(JsonEmbeddedLanguage language)
        {
            _language = language;
        }

        public void AddClassifications(
            Workspace workspace, SyntaxToken token, SemanticModel semanticModel, ArrayBuilder<ClassifiedSpan> result,
            CancellationToken cancellationToken)
        {
            Debug.Assert(token.RawKind == _language.StringLiteralKind);

            if (!workspace.Options.GetOption(JsonOptions.ColorizeJsonPatterns, token.Language))
            {
                return;
            }

            // Do some quick syntactic checks before doing any complex work.
            if (JsonPatternDetector.IsDefinitelyNotJson(token, _language.SyntaxFacts))
            {
                return;
            }

            var detector = JsonPatternDetector.GetOrCreate(semanticModel, _language);
            if (!detector.IsDefinitelyJson(token, cancellationToken))
            {
                return;
            }

            var tree = detector?.TryParseJson(token);
            if (tree == null)
            {
                return;
            }

            var visitor = _visitorPool.Allocate();
            try
            {
                visitor.Result = result;
                AddClassifications(tree.Root, visitor, result);
            }
            finally
            {
                visitor.Result = null;
                _visitorPool.Free(visitor);
            }
        }

        private static void AddClassifications(JsonNode node, Visitor visitor, ArrayBuilder<ClassifiedSpan> result)
        {
            node.Accept(visitor);

            foreach (var child in node)
            {
                if (child.IsNode)
                {
                    AddClassifications(child.Node, visitor, result);
                }
                else
                {
                    AddTriviaClassifications(child.Token, result);
                }
            }
        }

        private static void AddTriviaClassifications(JsonToken token, ArrayBuilder<ClassifiedSpan> result)
        {
            foreach (var trivia in token.LeadingTrivia)
            {
                AddTriviaClassifications(trivia, result);
            }

            foreach (var trivia in token.TrailingTrivia)
            {
                AddTriviaClassifications(trivia, result);
            }
        }

        private static void AddTriviaClassifications(JsonTrivia trivia, ArrayBuilder<ClassifiedSpan> result)
        {
            if ((trivia.Kind == JsonKind.MultiLineCommentTrivia || trivia.Kind == JsonKind.SingleLineCommentTrivia) &&
                trivia.VirtualChars.Length > 0)
            {
                result.Add(new ClassifiedSpan(
                    ClassificationTypeNames.JsonComment, GetSpan(trivia.VirtualChars)));
            }
        }

        private class Visitor : IJsonNodeVisitor
        {
            public ArrayBuilder<ClassifiedSpan> Result;

            private void AddClassification(JsonToken token, string typeName)
            {
                if (!token.IsMissing)
                {
                    Result.Add(new ClassifiedSpan(typeName, token.GetSpan()));
                }
            }

            private void ClassifyWholeNode(JsonNode node, string typeName)
            {
                foreach (var child in node)
                {
                    if (child.IsNode)
                    {
                        ClassifyWholeNode(child.Node, typeName);
                    }
                    else
                    {
                        AddClassification(child.Token, typeName);
                    }
                }
            }

            public void Visit(JsonCompilationUnit node)
            {
                // nothing to do.
            }

            public void Visit(JsonSequenceNode node)
            {
                // nothing to do.
            }

            public void Visit(JsonArrayNode node)
            {
                AddClassification(node.OpenBracketToken, ClassificationTypeNames.JsonArray);
                AddClassification(node.CloseBracketToken, ClassificationTypeNames.JsonArray);
            }

            public void Visit(JsonObjectNode node)
            {
                AddClassification(node.OpenBraceToken, ClassificationTypeNames.JsonObject);
                AddClassification(node.CloseBraceToken, ClassificationTypeNames.JsonObject);
            }

            public void Visit(JsonPropertyNode node)
            {
                AddClassification(node.NameToken, ClassificationTypeNames.JsonPropertyName);
                AddClassification(node.ColonToken, ClassificationTypeNames.JsonPunctuation);
            }

            public void Visit(JsonConstructorNode node)
            {
                AddClassification(node.NewKeyword, ClassificationTypeNames.JsonKeyword);
                AddClassification(node.NameToken, ClassificationTypeNames.JsonConstructorName);
                AddClassification(node.OpenParenToken, ClassificationTypeNames.JsonPunctuation);
                AddClassification(node.CloseParenToken, ClassificationTypeNames.JsonPunctuation);
            }

            public void Visit(JsonLiteralNode node)
            {
                VisitLiteral(node.LiteralToken);
            }

            private void VisitLiteral(JsonToken literalToken)
            {
                switch (literalToken.Kind)
                {
                    case JsonKind.NumberToken:
                        AddClassification(literalToken, ClassificationTypeNames.JsonNumber);
                        return;

                    case JsonKind.StringToken:
                        AddClassification(literalToken, ClassificationTypeNames.JsonString);
                        return;

                    case JsonKind.TrueLiteralToken:
                    case JsonKind.FalseLiteralToken:
                    case JsonKind.NullLiteralToken:
                    case JsonKind.UndefinedLiteralToken:
                    case JsonKind.NaNLiteralToken:
                    case JsonKind.InfinityLiteralToken:
                        AddClassification(literalToken, ClassificationTypeNames.JsonKeyword);
                        return;

                    default:
                        AddClassification(literalToken, ClassificationTypeNames.JsonText);
                        return;
                }
            }

            public void Visit(JsonNegativeLiteralNode node)
            {
                AddClassification(node.MinusToken, ClassificationTypeNames.JsonOperator);
                VisitLiteral(node.LiteralToken);
            }

            public void Visit(JsonTextNode node)
            {
                VisitLiteral(node.TextToken);
            }

            public void Visit(JsonCommaValueNode node)
            {
                AddClassification(node.CommaToken, ClassificationTypeNames.JsonPunctuation);
            }
        }
    }
}
