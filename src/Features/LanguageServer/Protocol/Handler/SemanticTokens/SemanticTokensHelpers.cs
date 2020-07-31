﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.SemanticTokens
{
    internal class SemanticTokensHelpers
    {
        /// <summary>
        /// Maps an enum modifier type to the matching LSP modifier type.
        /// Used for ordering purposes in InitializeHandler.
        /// </summary>
        private static readonly Dictionary<TokenModifiers, string> s_tokenModifierToSemanticTokenModifierMap =
            new Dictionary<TokenModifiers, string>
            {
                [TokenModifiers.Static] = LSP.SemanticTokenModifiers.Static
            };

        // TO-DO: Change this mapping once support for custom token types is added:
        // https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1085998
        private static readonly Dictionary<string, string> s_classificationTypeToSemanticTokenTypeMap =
            new Dictionary<string, string>
            {
                [ClassificationTypeNames.ClassName] = LSP.SemanticTokenTypes.Class,
                [ClassificationTypeNames.Comment] = LSP.SemanticTokenTypes.Comment,
                [ClassificationTypeNames.ConstantName] = LSP.SemanticTokenTypes.Variable, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.ControlKeyword] = LSP.SemanticTokenTypes.Keyword, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.DelegateName] = LSP.SemanticTokenTypes.Member, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.EnumMemberName] = LSP.SemanticTokenTypes.EnumMember,
                [ClassificationTypeNames.EnumName] = LSP.SemanticTokenTypes.Enum,
                [ClassificationTypeNames.EventName] = LSP.SemanticTokenTypes.Event,
                [ClassificationTypeNames.ExcludedCode] = LSP.SemanticTokenTypes.String, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.ExtensionMethodName] = LSP.SemanticTokenTypes.Member, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.FieldName] = LSP.SemanticTokenTypes.Property, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.Identifier] = LSP.SemanticTokenTypes.Variable, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.InterfaceName] = LSP.SemanticTokenTypes.Interface,
                [ClassificationTypeNames.Keyword] = LSP.SemanticTokenTypes.Keyword,
                [ClassificationTypeNames.LabelName] = LSP.SemanticTokenTypes.Variable, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.LocalName] = LSP.SemanticTokenTypes.Member, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.MethodName] = LSP.SemanticTokenTypes.Member, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.ModuleName] = LSP.SemanticTokenTypes.Member, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.NamespaceName] = LSP.SemanticTokenTypes.Namespace,
                [ClassificationTypeNames.NumericLiteral] = LSP.SemanticTokenTypes.Number,
                [ClassificationTypeNames.Operator] = LSP.SemanticTokenTypes.Operator,
                [ClassificationTypeNames.OperatorOverloaded] = LSP.SemanticTokenTypes.Operator, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.ParameterName] = LSP.SemanticTokenTypes.Parameter,
                [ClassificationTypeNames.PreprocessorKeyword] = LSP.SemanticTokenTypes.Keyword, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.PreprocessorText] = LSP.SemanticTokenTypes.String, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.PropertyName] = LSP.SemanticTokenTypes.Property,
                [ClassificationTypeNames.Punctuation] = LSP.SemanticTokenTypes.Operator, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexAlternation] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexAnchor] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexCharacterClass] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexComment] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexGrouping] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexOtherEscape] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexQuantifier] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexSelfEscapedCharacter] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.RegexText] = LSP.SemanticTokenTypes.Regexp, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.StructName] = LSP.SemanticTokenTypes.Struct,
                [ClassificationTypeNames.Text] = LSP.SemanticTokenTypes.Variable, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.TypeParameterName] = LSP.SemanticTokenTypes.TypeParameter,
                [ClassificationTypeNames.VerbatimStringLiteral] = LSP.SemanticTokenTypes.String, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.WhiteSpace] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentAttributeName] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentAttributeQuotes] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentAttributeValue] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentCDataSection] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentComment] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentDelimiter] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentEntityReference] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentName] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentProcessingInstruction] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlDocCommentText] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralAttributeName] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralAttributeQuotes] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralAttributeValue] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralCDataSection] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralComment] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralDelimiter] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralEmbeddedExpression] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralEntityReference] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralName] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralProcessingInstruction] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
                [ClassificationTypeNames.XmlLiteralText] = LSP.SemanticTokenTypes.Comment, // TO-DO: Potentially change to custom type
            };

        private static readonly Dictionary<string, TokenModifiers> s_classificationTypeToSemanticTokenModifierMap =
            new Dictionary<string, TokenModifiers>
            {
                [ClassificationTypeNames.StaticSymbol] = TokenModifiers.Static
            };

        /// <summary>
        /// Returns the semantic tokens for a given document with an optional range.
        /// </summary>
        internal static async Task<LSP.SemanticTokens> ComputeSemanticTokensAsync(
            LSP.TextDocumentIdentifier textDocument,
            string resultId,
            string? clientName,
            ILspSolutionProvider solutionProvider,
            LSP.Range? range,
            CancellationToken cancellationToken)
        {
            var document = solutionProvider.GetDocument(textDocument, clientName);
            Contract.ThrowIfNull(document);

            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

            // By default we calculate the tokens for the full document span, although the user 
            // can pass in a range if they wish.
            var textSpan = root.FullSpan;
            if (range != null)
            {
                textSpan = ProtocolConversions.RangeToTextSpan(range, text);
            }

            var classifiedSpans = await Classifier.GetClassifiedSpansAsync(document, textSpan, cancellationToken).ConfigureAwait(false);
            Contract.ThrowIfNull(classifiedSpans);

            // A TextSpan can be associated with multiple ClassifiedSpans (i.e. if  a token has
            // modifiers). We perform this group by since LSP requires that each token is
            // reported together with all its modifiers.
            var groupedSpans = classifiedSpans.GroupBy(s => s.TextSpan);

            // TO-DO: We should implement support for streaming once this LSP bug is fixed:
            // https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1132601
            var tokens = ComputeTokens(text.Lines, groupedSpans);

            return new LSP.SemanticTokens { ResultId = resultId, Data = tokens };
        }

        private static int[] ComputeTokens(
            TextLineCollection lines,
            IEnumerable<IGrouping<TextSpan, ClassifiedSpan>> groupedSpans)
        {
            using var _ = ArrayBuilder<int>.GetInstance(out var data);

            var lastLineNumber = 0;
            var lastStartCharacter = 0;

            foreach (var span in groupedSpans)
            {
                ComputeNextToken(lines, ref lastLineNumber, ref lastStartCharacter, span,
                    out var deltaLine, out var startCharacterDelta, out var tokenLength,
                    out var tokenType, out var tokenModifiers);

                data.AddRange(deltaLine, startCharacterDelta, tokenLength, tokenType, tokenModifiers);
            }

            return data.ToArray();
        }

        private static void ComputeNextToken(
            TextLineCollection lines,
            ref int lastLineNumber,
            ref int lastStartCharacter,
            IGrouping<TextSpan, ClassifiedSpan> textSpanToClassifiedSpans,
            // Out params
            out int deltaLineOut,
            out int startCharacterDeltaOut,
            out int tokenLengthOut,
            out int tokenTypeOut,
            out int tokenModifiersOut)
        {
            // Each semantic token is represented in LSP by five numbers:
            //     1. Token line number delta, relative to the previous token
            //     2. Token start character delta, relative to the previous token
            //     3. Token length
            //     4. Token type (index) - looked up in SemanticTokensLegend.tokenTypes
            //     5. Token modifiers - each set bit will be looked up in SemanticTokensLegend.tokenModifiers

            var textSpan = textSpanToClassifiedSpans.Key;
            var linePosition = lines.GetLinePositionSpan(textSpan).Start;
            var lineNumber = linePosition.Line;
            var startCharacter = linePosition.Character;

            // 1. Token line number delta, relative to the previous token
            var deltaLine = lineNumber - lastLineNumber;
            Contract.ThrowIfTrue(deltaLine < 0);

            // 2. Token start character delta, relative to the previous token
            // (Relative to 0 or the previous token’s start if they're on the same line)
            var startCharacterDelta = startCharacter;
            if (lastLineNumber == lineNumber)
            {
                startCharacterDelta = startCharacter - lastStartCharacter;
            }

            // 3. Token length
            var tokenLength = textSpan.Length;

            // Getting the classified spans that are modifiers.
            // Since a TextSpan can have multiple ClassifiedSpans, all of the ClassifiedSpans except one (the token)
            // should be a modifier.
            var modifiers = textSpanToClassifiedSpans.Where(
                s => ClassificationTypeNames.AdditiveTypeNames.Contains(s.ClassificationType));

            // Filtering out the modifiers for now (will be added in step 5). We just want the primary token.
            var tokenTypeClassifiedSpan = textSpanToClassifiedSpans.Except(modifiers).Single();

            // 4. Token type - looked up in SemanticTokensLegend.tokenTypes (language server defined mapping
            // from integer to LSP token types).
            if (!s_classificationTypeToSemanticTokenTypeMap.TryGetValue(tokenTypeClassifiedSpan.ClassificationType, out var tokenTypeStr))
            {
                throw new NotSupportedException($"Classification type {tokenTypeClassifiedSpan.ClassificationType} is unsupported.");
            }

            var tokenTypeIndex = GetTokenTypeIndex(tokenTypeStr);

            // 5. Token modifiers - each set bit will be looked up in SemanticTokensLegend.tokenModifiers
            var modifierBits = TokenModifiers.None;
            foreach (var currentModifier in modifiers)
            {
                if (!s_classificationTypeToSemanticTokenModifierMap.TryGetValue(currentModifier.ClassificationType, out var modifier))
                {
                    throw new NotSupportedException($"Classification type {currentModifier.ClassificationType} is unsupported.");
                }

                modifierBits |= modifier;
            }

            lastLineNumber = lineNumber;
            lastStartCharacter = startCharacter;

            deltaLineOut = deltaLine;
            startCharacterDeltaOut = startCharacterDelta;
            tokenLengthOut = tokenLength;
            tokenTypeOut = tokenTypeIndex;
            tokenModifiersOut = (int)modifierBits;
        }

        // Note: Method is internal since it is used by tests
        internal static int GetTokenTypeIndex(string? tokenTypeStr)
        {
            var tokenTypeIndex = 0;
            foreach (var tokenType in LSP.SemanticTokenTypes.AllTypes)
            {
                if (tokenType == tokenTypeStr)
                {
                    break;
                }

                tokenTypeIndex += 1;
            }

            return tokenTypeIndex;
        }

        /// <summary>
        /// Returns the set of ordered semantic token modifiers. Used by LSP to decipher any modifier values
        /// we later pass back to them.
        /// </summary>
        internal static string[] GetOrderedSemanticTokenModifiers()
        {
            using var _ = ArrayBuilder<string>.GetInstance(out var orderedModifiers);
            foreach (TokenModifiers? modifier in Enum.GetValues(typeof(TokenModifiers)))
            {
                Contract.ThrowIfFalse(modifier.HasValue);

                // Skip the none modifier
                if (modifier.Value == TokenModifiers.None)
                {
                    continue;
                }

                if (!s_tokenModifierToSemanticTokenModifierMap.TryGetValue(modifier.Value, out var semanticTokenModifier))
                {
                    throw new ArgumentException($"Modifier is invalid.");
                }

                orderedModifiers.Add(semanticTokenModifier);
            }

            return orderedModifiers.ToArray();
        }
    }
}
