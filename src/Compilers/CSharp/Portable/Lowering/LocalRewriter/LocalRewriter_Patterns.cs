﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;
using System;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed partial class LocalRewriter
    {
        public override BoundNode VisitIsPatternExpression(BoundIsPatternExpression node)
        {
            var expression = VisitExpression(node.Expression);
            var result = TranslatePattern(expression, node.Pattern);
            return result;
        }

        // Input must be used no more than once in the result. If it is needed repeatedly store its value in a temp and use the temp.
        BoundExpression TranslatePattern(BoundExpression input, BoundPattern pattern)
        {
            var syntax = _factory.Syntax = pattern.Syntax;
            switch (pattern.Kind)
            {
                case BoundKind.DeclarationPattern:
                    {
                        var declPattern = (BoundDeclarationPattern)pattern;
                        Debug.Assert(declPattern.LocalSymbol.Type == declPattern.DeclaredType.Type);
                        if (declPattern.IsVar)
                        {
                            Debug.Assert(input.Type == declPattern.LocalSymbol.Type);
                            var assignment = _factory.AssignmentExpression(_factory.Local(declPattern.LocalSymbol), input);
                            var result = _factory.Literal(true);
                            return _factory.Sequence(assignment, result);
                        }

                        return DeclPattern(syntax, input, declPattern.LocalSymbol);
                    }

                case BoundKind.PropertyPattern:
                    {
                        var pat = (BoundPropertyPattern)pattern;
                        var temp = _factory.SynthesizedLocal(pat.Type);
                        var matched = DeclPattern(syntax, input, temp);
                        input = _factory.Local(temp);
                        for (int i = 0; i < pat.Patterns.Length; i++)
                        {
                            var subProperty = pat.Properties[i];
                            var subPattern = pat.Patterns[i];
                            var subExpression =
                                subProperty.Kind == SymbolKind.Field
                                    ? (BoundExpression)_factory.Field(input, (FieldSymbol)subProperty)
                                    : _factory.Call(input, ((PropertySymbol)subProperty).GetMethod);
                            var partialMatch = this.TranslatePattern(subExpression, subPattern);
                            matched = _factory.LogicalAnd(matched, partialMatch);
                        }
                        return _factory.Sequence(temp, matched);
                    }

                case BoundKind.WildcardPattern:
                    return _factory.Literal(true);

                case BoundKind.ConstantPattern:
                    {
                        var constantPattern = (BoundConstantPattern)pattern;
                        return CompareWithConstant(input, constantPattern.BoundConstant);
                    }

                case BoundKind.RecursivePattern:
                default:
                    throw ExceptionUtilities.UnexpectedValue(pattern.Kind);
            }
        }

        private BoundExpression CompareWithConstant(BoundExpression input, BoundExpression boundConstant)
        {
            // We currently use "exact" type semantics.
            // TODO: We need to change this to be sensitive to conversions
            // among integral types, so that the same value of different integral types are considered matching.
            return _factory.StaticCall(
                _factory.SpecialType(SpecialType.System_Object),
                "Equals",
                _factory.Convert(_factory.SpecialType(SpecialType.System_Object), input),
                _factory.Convert(_factory.SpecialType(SpecialType.System_Object), boundConstant)
                );
        }

        BoundExpression DeclPattern(CSharpSyntaxNode syntax, BoundExpression input, LocalSymbol target)
        {
            var type = target.Type;
            // a pattern match of the form "expression is Type identifier" is equivalent to
            // an invocation of one of these helpers:
            if (type.IsReferenceType)
            {
                // bool Is<T>(object e, out T t) where T : class // reference type
                // {
                //     t = e as T;
                //     return t != null;
                // }
                var assignment = _factory.AssignmentExpression(_factory.Local(target), _factory.As(input, type));
                var result = _factory.ObjectNotEqual(_factory.Local(target), _factory.Null(type));
                return _factory.Sequence(assignment, result);
            }
            else if (type.IsNullableType())
            {
                // bool Is<T>(object e, out T? t) where T : struct
                // {
                //     t = e as T?;
                //     return e == null || t.HasValue;
                // }
                throw new NotImplementedException();
            }
            else if (type.IsValueType)
            {
                // TODO: only assign t when returning true (avoid returning a new default value)
                // bool Is<T>(object e, out T t) where T : struct // non-Nullable value type
                // {
                //     T? tmp = e as T?;
                //     t = tmp.GetValueOrDefault();
                //     return tmp.HasValue;
                // }
                var tmpType = _factory.SpecialType(SpecialType.System_Nullable_T).Construct(type);
                var tmp = _factory.SynthesizedLocal(tmpType, syntax);
                var asg1 = _factory.AssignmentExpression(_factory.Local(tmp), _factory.As(input, tmpType));
                var value = _factory.Call(_factory.Local(tmp), GetNullableMethod(syntax, tmpType, SpecialMember.System_Nullable_T_GetValueOrDefault));
                var asg2 = _factory.AssignmentExpression(_factory.Local(target), value);
                var result = MakeNullableHasValue(syntax, _factory.Local(tmp));
                return _factory.Sequence(tmp, asg1, asg2, result);
            }
            else
            {
                // bool Is<T>(this object i, out T o)
                // {
                //     // inefficient because it performs the type test twice.
                //     bool s = i is T;
                //     if (s) o = (T)i;
                //     return s;
                // }
                throw new NotImplementedException();
            }
        }
    }
}
