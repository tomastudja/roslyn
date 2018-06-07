﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;
using Roslyn.Utilities;

using static Microsoft.CodeAnalysis.CodeGeneration.CodeGenerationHelpers;

namespace Microsoft.CodeAnalysis.CSharp.CodeGeneration
{
    internal static class ExpressionGenerator
    {
        internal static ExpressionSyntax GenerateExpression(
            TypedConstant typedConstant)
        {
            switch (typedConstant.Kind)
            {
                case TypedConstantKind.Primitive:
                case TypedConstantKind.Enum:
                    return GenerateExpression(typedConstant.Type, typedConstant.Value, canUseFieldReference: true);

                case TypedConstantKind.Type:
                    return typedConstant.Value is ITypeSymbol
                        ? SyntaxFactory.TypeOfExpression(((ITypeSymbol)typedConstant.Value).GenerateTypeSyntax())
                        : GenerateNullLiteral();

                case TypedConstantKind.Array:
                    return typedConstant.IsNull ?
                        GenerateNullLiteral() :
                        SyntaxFactory.ImplicitArrayCreationExpression(
                            SyntaxFactory.InitializerExpression(SyntaxKind.ArrayInitializerExpression,
                                SyntaxFactory.SeparatedList(typedConstant.Values.Select(GenerateExpression))));

                default:
                    return GenerateNullLiteral();
            }
        }

        private static ExpressionSyntax GenerateNullLiteral()
            => SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);

        internal static ExpressionSyntax GenerateExpression(
            ITypeSymbol type,
            object value,
            bool canUseFieldReference)
        {
            if (value != null)
            {
                if (type.TypeKind == TypeKind.Enum)
                {
                    var enumType = (INamedTypeSymbol)type;
                    return (ExpressionSyntax)CSharpFlagsEnumGenerator.Instance.CreateEnumConstantValue(enumType, value);
                }
                else if (type.IsNullable())
                {
                    // If the type of the argument is T?, then the type of the supplied default value can either be T 
                    // (e.g. int? x = 5) or it can be T? (e.g. SomeStruct? x = null). The below statement handles the case
                    // where the type of the supplied default value is T.
                    return GenerateExpression(((INamedTypeSymbol)type).TypeArguments[0], value, canUseFieldReference);
                }
            }

            return GenerateNonEnumValueExpression(type, value, canUseFieldReference);
        }

        internal static ExpressionSyntax GenerateNonEnumValueExpression(
            ITypeSymbol type, object value, bool canUseFieldReference)
        {
            switch (value)
            {
                case bool val: return GenerateBooleanLiteralExpression(val);
                case string val: return GenerateStringLiteralExpression(val);
                case char val: return GenerateCharLiteralExpression(val);
                case sbyte val: return GenerateLiteralExpression(type, val, LiteralSpecialValues.SByteSpecialValues, null, canUseFieldReference, (s, v) => SyntaxFactory.Literal(s, v), x => x < 0, x => (sbyte)-x);
                case short val: return GenerateLiteralExpression(type, val, LiteralSpecialValues.Int16SpecialValues, null, canUseFieldReference, (s, v) => SyntaxFactory.Literal(s, v), x => x < 0, x => (short)-x);
                case int val: return GenerateLiteralExpression(type, val, LiteralSpecialValues.Int32SpecialValues, null, canUseFieldReference, SyntaxFactory.Literal, x => x < 0, x => -x);
                case long val: return GenerateLiteralExpression(type, val, LiteralSpecialValues.Int64SpecialValues, null, canUseFieldReference, SyntaxFactory.Literal, x => x < 0, x => -x);
                case byte val: return GenerateNonNegativeLiteralExpression(type, val, LiteralSpecialValues.ByteSpecialValues, null, canUseFieldReference, (s, v) => SyntaxFactory.Literal(s, v));
                case ushort val: return GenerateNonNegativeLiteralExpression(type, val, LiteralSpecialValues.UInt16SpecialValues, null, canUseFieldReference, (s, v) => SyntaxFactory.Literal(s, (uint)v));
                case uint val: return GenerateNonNegativeLiteralExpression(type, val, LiteralSpecialValues.UInt32SpecialValues, null, canUseFieldReference, SyntaxFactory.Literal);
                case ulong val: return GenerateNonNegativeLiteralExpression(type, val, LiteralSpecialValues.UInt64SpecialValues, null, canUseFieldReference, SyntaxFactory.Literal);
                case float val: return GenerateSingleLiteralExpression(type, val, canUseFieldReference);
                case double val: return GenerateDoubleLiteralExpression(type, val, canUseFieldReference);
                case decimal val: return GenerateLiteralExpression(type, val, LiteralSpecialValues.DecimalSpecialValues, null, canUseFieldReference, SyntaxFactory.Literal, x => x < 0, x => -x);
            }

            return type == null || type.IsReferenceType || type.IsPointerType() || type.IsNullable()
                ? GenerateNullLiteral()
                : (ExpressionSyntax)CSharpSyntaxGenerator.Instance.DefaultExpression(type);
        }

        private static ExpressionSyntax GenerateBooleanLiteralExpression(bool val)
        {
            return SyntaxFactory.LiteralExpression(val
                ? SyntaxKind.TrueLiteralExpression
                : SyntaxKind.FalseLiteralExpression);
        }

        private static ExpressionSyntax GenerateStringLiteralExpression(string val)
        {
            var valueString = SymbolDisplay.FormatLiteral(val, quote: true);
            return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(valueString, val));
        }

        private static ExpressionSyntax GenerateCharLiteralExpression(char val)
        {
            var literal = SymbolDisplay.FormatLiteral(val, quote: true);
            return SyntaxFactory.LiteralExpression(
                SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(literal, val));
        }

        private static string DetermineSuffix(ITypeSymbol type, object value)
        {
            if (value is float f)
            {
                var stringValue = ((IFormattable)value).ToString("R", CultureInfo.InvariantCulture);

                var isNotSingle = !IsSpecialType(type, SpecialType.System_Single);
                var containsDoubleCharacter =
                    stringValue.Contains("E") || stringValue.Contains("e") || stringValue.Contains(".") ||
                    stringValue.Contains("+") || stringValue.Contains("-");

                if (isNotSingle || containsDoubleCharacter)
                {
                    return "F";
                }
            }

            if (value is double && !IsSpecialType(type, SpecialType.System_Double))
            {
                return "D";
            }

            if (value is uint && !IsSpecialType(type, SpecialType.System_UInt32))
            {
                return "U";
            }

            if (value is long && !IsSpecialType(type, SpecialType.System_Int64))
            {
                return "L";
            }

            if (value is ulong && !IsSpecialType(type, SpecialType.System_UInt64))
            {
                return "UL";
            }

            if (value is decimal d)
            {
                var scale = d.GetScale();

                var isNotDecimal = !IsSpecialType(type, SpecialType.System_Decimal);
                var isOutOfRange = d < long.MinValue || d > long.MaxValue;
                var scaleIsNotZero = scale != 0;

                if (isNotDecimal || isOutOfRange || scaleIsNotZero)
                {
                    return "M";
                }
            }

            return string.Empty;
        }

        private static ExpressionSyntax GenerateDoubleLiteralExpression(ITypeSymbol type, double value, bool canUseFieldReference)
        {
            if (!canUseFieldReference)
            {
                if (double.IsNaN(value))
                {
                    return SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
                        GenerateDoubleLiteralExpression(null, 0.0, false),
                        GenerateDoubleLiteralExpression(null, 0.0, false));
                }
                else if (double.IsPositiveInfinity(value))
                {
                    return SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
                        GenerateDoubleLiteralExpression(null, 1.0, false),
                        GenerateDoubleLiteralExpression(null, 0.0, false));
                }
                else if (double.IsNegativeInfinity(value))
                {
                    return SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
                        SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, GenerateDoubleLiteralExpression(null, 1.0, false)),
                        GenerateDoubleLiteralExpression(null, 0.0, false));
                }
            }

            return GenerateLiteralExpression(
                type, value, LiteralSpecialValues.DoubleSpecialValues, "R", canUseFieldReference, 
                SyntaxFactory.Literal, x => x < 0, x => -x);
        }

        private static ExpressionSyntax GenerateSingleLiteralExpression(ITypeSymbol type, float value, bool canUseFieldReference)
        {
            if (!canUseFieldReference)
            {
                if (float.IsNaN(value))
                {
                    return SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
                        GenerateSingleLiteralExpression(null, 0.0F, false),
                        GenerateSingleLiteralExpression(null, 0.0F, false));
                }
                else if (float.IsPositiveInfinity(value))
                {
                    return SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
                        GenerateSingleLiteralExpression(null, 1.0F, false),
                        GenerateSingleLiteralExpression(null, 0.0F, false));
                }
                else if (float.IsNegativeInfinity(value))
                {
                    return SyntaxFactory.BinaryExpression(SyntaxKind.DivideExpression,
                        SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, GenerateSingleLiteralExpression(null, 1.0F, false)),
                        GenerateSingleLiteralExpression(null, 0.0F, false));
                }
            }

            return GenerateLiteralExpression(
                type, value, LiteralSpecialValues.SingleSpecialValues, "R", canUseFieldReference, 
                SyntaxFactory.Literal, x => x < 0, x => -x);
        }

        private static ExpressionSyntax GenerateNonNegativeLiteralExpression<T>(
            ITypeSymbol type, T value, IEnumerable<KeyValuePair<T, string>> constants,
            string formatString, bool canUseFieldReference,
            Func<string, T, SyntaxToken> tokenFactory)
        {
            return GenerateLiteralExpression(
                type, value, constants, formatString, canUseFieldReference,
                tokenFactory, isNegative: x => false, negate: t => throw new InvalidOperationException());
        }

        private static ExpressionSyntax GenerateLiteralExpression<T>(
            ITypeSymbol type, T value, IEnumerable<KeyValuePair<T, string>> constants, 
            string formatString, bool canUseFieldReference,
            Func<string, T, SyntaxToken> tokenFactory,
            Func<T, bool> isNegative, Func<T, T> negate)
        {
            if (canUseFieldReference)
            {
                var result = GenerateFieldReference(type, value, constants);
                if (result != null)
                {
                    return result;
                }
            }

            var negative = isNegative(value);
            if (negative)
            {
                value = negate(value);
            }

            var suffix = DetermineSuffix(type, value);
            var stringValue = ((IFormattable)value).ToString(formatString, CultureInfo.InvariantCulture) + suffix;

            var literal = SyntaxFactory.LiteralExpression(
               SyntaxKind.NumericLiteralExpression, tokenFactory(stringValue, value));

            return negative
                ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, literal)
                : (ExpressionSyntax)literal;
        }

        private static ExpressionSyntax GenerateFieldReference<T>(ITypeSymbol type, T value, IEnumerable<KeyValuePair<T, string>> constants)
        {
            foreach (var constant in constants)
            {
                if (constant.Key.Equals(value))
                {
                    var memberAccess = GenerateMemberAccess("System", typeof(T).Name);
                    if (type != null && !(type is IErrorTypeSymbol))
                    {
                        memberAccess = memberAccess.WithAdditionalAnnotations(SpecialTypeAnnotation.Create(type.SpecialType));
                    }

                    var result = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, memberAccess, SyntaxFactory.IdentifierName(constant.Value));
                    return result.WithAdditionalAnnotations(Simplifier.Annotation);
                }
            }

            return null;
        }

        private static ExpressionSyntax GenerateMemberAccess(params string[] names)
        {
            ExpressionSyntax result = SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword));
            for (int i = 0; i < names.Length; i++)
            {
                var name = SyntaxFactory.IdentifierName(names[i]);
                if (i == 0)
                {
                    result = SyntaxFactory.AliasQualifiedName((IdentifierNameSyntax)result, name);
                }
                else
                {
                    result = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, result, name);
                }
            }

            result = result.WithAdditionalAnnotations(Simplifier.Annotation);
            return result;
        }
    }
}
