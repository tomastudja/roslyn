﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Completion.Providers;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Completion.Providers
{
    [ExportCompletionProvider(nameof(ConversionCompletionProvider), LanguageNames.CSharp), Shared]
    [ExtensionOrder(After = nameof(SymbolCompletionProvider))]
    internal class ConversionCompletionProvider : OperatorIndexerCompletionProviderBase
    {
        private const string MinimalTypeNamePropertyName = "MinimalTypeName";

        private static readonly ImmutableArray<SpecialType> s_builtInEnumConversionTargets = ImmutableArray.Create(
            // Presorted alphabetical to reduce sorting cost of the completion items. 
            SpecialType.System_Byte,    // byte
            SpecialType.System_Char,    // char
            SpecialType.System_Decimal, // decimal
            SpecialType.System_Double,  // double
            SpecialType.System_Single,  // float
            SpecialType.System_Int32,   // int
            SpecialType.System_Int64,   // long
            SpecialType.System_SByte,   // sbyte
            SpecialType.System_Int16,   // short
            SpecialType.System_UInt32,  // uint
            SpecialType.System_UInt64,  // ulong
            SpecialType.System_UInt16  // ushort
            );

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public ConversionCompletionProvider()
        {
        }

        protected override int SortingGroupIndex => 2;

        protected override ImmutableArray<CompletionItem> GetCompletionItemsForTypeSymbol(
            SemanticModel semanticModel,
            ITypeSymbol container,
            int position,
            bool isAccessedByConditionalAccess,
            CancellationToken cancellationToken)
        {
            // Lifted nullable value types should be suggested if container is nullable or accessed via conditional access.
            // Container is nullable: int? id; id.$$ -> (byte?)
            var containerIsNullable = container.IsNullable();

            // Container is access via conditional access: System.Diagnostics.Process p; p?.Id.$$ -> (byte?)
            containerIsNullable = containerIsNullable || (isAccessedByConditionalAccess && container.IsValueType);

            container = container.RemoveNullableIfPresent();

            using var _ = ArrayBuilder<CompletionItem>.GetInstance(out var builder);

            AddUserDefinedConversionsOfType(builder, semanticModel, container, containerIsNullable, position);
            if (container is INamedTypeSymbol namedType)
            {
                AddBuiltInNumericConversions(builder, semanticModel, namedType, containerIsNullable, position);
                AddBuiltInEnumConversions(builder, semanticModel, namedType, containerIsNullable, position);
            }

            return builder.ToImmutable();
        }

        private void AddUserDefinedConversionsOfType(
            ArrayBuilder<CompletionItem> builder,
            SemanticModel semanticModel,
            ITypeSymbol container,
            bool containerIsNullable,
            int position)
        {
            // Base types are valid sources for user-defined conversions.
            // Note: We only look in the source (aka container), because target could be any type (in scope) of the compilation.
            // No need to check for accessibility as operators must always be public.
            // The target type is lifted, if containerIsNullable and the target of the conversion is a struct

            foreach (var type in container.GetBaseTypesAndThis())
            {
                foreach (var member in type.GetMembers(WellKnownMemberNames.ExplicitConversionName))
                {
                    if (member is not IMethodSymbol method)
                        continue;

                    if (!method.IsConversion())
                        continue;

                    if (method.Parameters.Length != 1)
                        continue;

                    if (!type.Equals(method.Parameters[0].Type))
                        continue;

                    builder.Add(CreateSymbolCompletionItem(
                        targetTypeName: method.ReturnType.ToMinimalDisplayString(semanticModel, position),
                        targetTypeIsNullable: containerIsNullable && method.ReturnType.IsStructType(),
                        position,
                        method));
                }
            }
        }

        private void AddBuiltInNumericConversions(ArrayBuilder<CompletionItem> builder, SemanticModel semanticModel, INamedTypeSymbol container, bool containerIsNullable, int position)
        {
            if (container.SpecialType == SpecialType.System_Decimal)
            {
                // Decimal is defined in the spec with integrated conversions, but is the only type that reports it's
                // conversions as normal method symbols
                return;
            }

            var numericConversions = container.GetBuiltInNumericConversions();
            if (!numericConversions.HasValue)
                return;

            AddCompletionItemsForSpecialTypes(
                builder, semanticModel, container, containerIsNullable, position, numericConversions.Value);
        }

        private void AddBuiltInEnumConversions(
            ArrayBuilder<CompletionItem> builder,
            SemanticModel semanticModel,
            INamedTypeSymbol container,
            bool containerIsNullable,
            int position)
        {
            // https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#explicit-enumeration-conversions
            // Three kinds of conversions are defined in the spec.
            // Suggestion are made for one kind:
            // * From any enum_type to sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, or decimal.
            // No suggestion for the other two kinds of conversions:
            // * From sbyte, byte, short, ushort, int, uint, long, ulong, char, float, double, or decimal to any enum_type.
            // * From any enum_type to any other enum_type.

            // Suggest after enum members: SomeEnum.EnumMember.$$ 
            // or on enum values: someEnumVariable.$$
            if (container.IsEnumMember() || container.IsEnumType())
                AddCompletionItemsForSpecialTypes(builder, semanticModel, container, containerIsNullable, position, s_builtInEnumConversionTargets);
        }

        private void AddCompletionItemsForSpecialTypes(
            ArrayBuilder<CompletionItem> builder,
            SemanticModel semanticModel,
            INamedTypeSymbol fromType,
            bool containerIsNullable,
            int position,
            ImmutableArray<SpecialType> specialTypes)
        {
            foreach (var specialType in specialTypes)
            {
                var targetTypeSymbol = semanticModel.Compilation.GetSpecialType(specialType);
                var targetTypeName = targetTypeSymbol.ToMinimalDisplayString(semanticModel, position);
                builder.Add(CreateSymbolCompletionItem(
                    targetTypeName, targetTypeIsNullable: containerIsNullable, position, fromType, targetTypeSymbol));
            }
        }

        private CompletionItem CreateSymbolCompletionItem(string targetTypeName, bool targetTypeIsNullable, int position, params ISymbol[] symbols)
        {
            var optionalNullableQuestionmark = targetTypeIsNullable ? "?" : "";
            return SymbolCompletionItem.CreateWithSymbolId(
                           displayTextPrefix: "(",
                           displayText: targetTypeName,
                           displayTextSuffix: $"{optionalNullableQuestionmark})",
                           filterText: targetTypeName,
                           sortText: SortText(targetTypeName),
                           glyph: Glyph.Operator,
                           symbols: ImmutableList.Create(symbols),
                           rules: CompletionItemRules.Default,
                           contextPosition: position,
                           properties: CreatePropertiesBag((MinimalTypeNamePropertyName, $"{targetTypeName}{optionalNullableQuestionmark}")));
        }

        protected override async Task<CompletionDescription> GetDescriptionWorkerAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            var symbols = await SymbolCompletionItem.GetSymbolsAsync(item, document, cancellationToken).ConfigureAwait(false);
            if (symbols.Length == 2 && symbols[0] is INamedTypeSymbol from && symbols[1] is ITypeSymbol to)
            {
                return await GetBuiltInConversionDescriptionAsync(document, item, from, to, cancellationToken).ConfigureAwait(false);
            }

            return await base.GetDescriptionWorkerAsync(document, item, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<CompletionDescription> GetBuiltInConversionDescriptionAsync(Document document,
            CompletionItem item, INamedTypeSymbol fromType, ITypeSymbol toType, CancellationToken cancellationToken)
        {
            var symbol = new BuiltinOperatorMethodSymbol(toType, fromType);
            return await SymbolCompletionItem.GetDescriptionForSymbolsAsync(item, document, new ISymbol[] { symbol }.ToImmutableArray(), cancellationToken).ConfigureAwait(false);
        }

        internal override async Task<CompletionChange> GetChangeAsync(
            Document document, CompletionItem item, TextSpan completionListSpan, char? commitKey,
            bool disallowAddingImports, CancellationToken cancellationToken)
        {
            var position = SymbolCompletionItem.GetContextPosition(item);
            Contract.ThrowIfFalse(item.Properties.TryGetValue(MinimalTypeNamePropertyName, out var typeName));

            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var (tokenAtPosition, potentialDotTokenLeftOfCursor) = FindTokensAtPosition(position, root);
            // syntax tree manipulations are to complicated if a mixture of conditionals is involved. Some text manipulation is easier here.
            //                      ↓               | cursor position
            //                   ↓                  | potentialDotTokenLeftOfCursor
            // white?.Black.White.Black?.White      | current user input
            // white?.Black.White.Black?.White      | rootExpression (text manipulation starts with this)
            //       .Black.White                   | parentExpression (needed to calculate the position to insert the closing brace)
            //                    Black             | identifier at cursor position (gets removed, because the user typed the name of a type)
            // |----------------------|             | part to replace (TextChange.Span), if identifier is not present: ends at rootExpression.End (after White.)
            //                   ↑                  | insert closing brace between White and dot (parentExpression.Span.End)
            // ((Black)white?.Black.White).?.White  | The result. Because we removed the identifier, the remainder after the identifier may be syntactically wrong 
            //                             ↑        | cursor after the manipulation is placed after the dot
            var rootExpression = GetRootExpressionOfToken(potentialDotTokenLeftOfCursor);
            var parentExpression = GetParentExpressionOfToken(potentialDotTokenLeftOfCursor);
            var tokenToRemove = FindTokenToRemoveAtCursorPosition(tokenAtPosition);
            if (rootExpression is null || parentExpression is null)
            {
                // ProvideCompletionsAsync only adds CompletionItems, if GetParentExpressionOfToken returns an expression.
                // If GetParentExpressionOfToken returns an Expression, then GetRootExpressionOfToken should return an Expression too.
                throw ExceptionUtilities.Unreachable;
            }

            var spanToReplace = TextSpan.FromBounds(rootExpression.Span.Start, tokenToRemove.HasValue ? tokenToRemove.Value.Span.End : rootExpression.Span.End);
            var cursorPositionOffset = spanToReplace.End - position;
            var fromRootToParent = rootExpression.ToString();
            if (tokenToRemove is SyntaxToken token)
            {
                // Cut off the identifier
                var length = token.Span.Start - rootExpression.SpanStart;
                fromRootToParent = fromRootToParent.Substring(0, length);
                // place cursor right behind ).
                cursorPositionOffset = 0;
            }

            var fromRootToParentWithInsertedClosingBracket = fromRootToParent.Insert(parentExpression.Span.End - rootExpression.SpanStart, ")");
            var conversion = $"(({typeName}){fromRootToParentWithInsertedClosingBracket}";
            var newPosition = spanToReplace.Start + conversion.Length - cursorPositionOffset;

            return CompletionChange.Create(new TextChange(spanToReplace, conversion), newPosition);
        }
    }
}
