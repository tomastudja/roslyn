﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.UseSystemHashCode
{
    internal partial struct Analyzer
    {
        /// <summary>
        /// Breaks down complex <see cref="IOperation"/> trees, looking for particular
        /// <see cref="object.GetHashCode"/> patterns and extracting out the field and property
        /// symbols use to compute the hash.
        /// </summary>
        private struct OperationDeconstructor : IDisposable
        {
            private readonly Analyzer _analyzer;
            private readonly IMethodSymbol _method;
            private readonly ILocalSymbol? _hashCodeVariable;

            private readonly ArrayBuilder<ISymbol> _hashedSymbols;
            private bool _accessesBase;

            public OperationDeconstructor(
                Analyzer analyzer, IMethodSymbol method, ILocalSymbol? hashCodeVariable)
            {
                _analyzer = analyzer;
                _method = method;
                _hashCodeVariable = hashCodeVariable;
                _hashedSymbols = ArrayBuilder<ISymbol>.GetInstance();
                _accessesBase = false;
            }

            public void Dispose()
                => _hashedSymbols.Free();

            public (bool accessesBase, ImmutableArray<ISymbol> hashedSymbol) GetResult()
                => (_accessesBase, _hashedSymbols.ToImmutable());

            // Recursive function that decomposes <paramref name="value"/>, looking for particular
            // forms that VS or ReSharper generate to hash fields in the containing type.
            public bool TryAddHashedSymbol(IOperation value, bool seenHash)
            {
                value = Unwrap(value);
                if (value is IInvocationOperation invocation)
                {
                    var targetMethod = invocation.TargetMethod;
                    if (_analyzer.OverridesSystemObject(targetMethod))
                    {
                        // Either:
                        //
                        //      a.GetHashCode()
                        //
                        // or
                        //
                        //      (hashCode * -1521134295 + a.GetHashCode()).GetHashCode()
                        //
                        // recurse on the value we're calling GetHashCode on.
                        return TryAddHashedSymbol(invocation.Instance, seenHash: true);
                    }

                    if (targetMethod.Name == nameof(GetHashCode) &&
                        Equals(_analyzer._equalityComparerType, targetMethod.ContainingType.OriginalDefinition) &&
                        invocation.Arguments.Length == 1)
                    {
                        // EqualityComparer<T>.Default.GetHashCode(i)
                        //
                        // VS codegen only.
                        return TryAddHashedSymbol(invocation.Arguments[0].Value, seenHash: true);
                    }
                }

                // (hashCode op1 constant) op1 hashed_value
                //
                // This is generated by both VS and ReSharper.  Though each use different mathematical
                // ops to combine the values.
                if (_hashCodeVariable != null && value is IBinaryOperation topBinary)
                {
                    return topBinary.LeftOperand is IBinaryOperation leftBinary &&
                           IsLocalReference(leftBinary.LeftOperand, _hashCodeVariable) &&
                           IsLiteralNumber(leftBinary.RightOperand) &&
                           TryAddHashedSymbol(topBinary.RightOperand, seenHash: true);
                }

                // (StringProperty != null ? StringProperty.GetHashCode() : 0)
                //
                // ReSharper codegen only.
                if (value is IConditionalOperation conditional &&
                    conditional.Condition is IBinaryOperation binary)
                {
                    if (Unwrap(binary.RightOperand).IsNullLiteral() &&
                        TryGetFieldOrProperty(binary.LeftOperand, out _))
                    {
                        if (binary.OperatorKind == BinaryOperatorKind.Equals)
                        {
                            // (StringProperty == null ? 0 : StringProperty.GetHashCode())
                            return TryAddHashedSymbol(conditional.WhenFalse, seenHash: true);
                        }
                        else if (binary.OperatorKind == BinaryOperatorKind.NotEquals)
                        {
                            // (StringProperty != null ? StringProperty.GetHashCode() : 0)
                            return TryAddHashedSymbol(conditional.WhenTrue, seenHash: true);
                        }
                    }
                }

                // Look to see if we're referencing some field/prop/base.  However, we only accept
                // this reference if we've at least been through something that indicates that we've
                // hashed the value.
                if (seenHash)
                {
                    if (value is IInstanceReferenceOperation instanceReference &&
                        instanceReference.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                        Equals(_method.ContainingType.BaseType, instanceReference.Type))
                    {
                        if (_accessesBase)
                        {
                            // already had a reference to base.GetHashCode();
                            return false;
                        }

                        // reference to base.
                        //
                        // Happens with code like: `var hashCode = base.GetHashCode();`
                        _accessesBase = true;
                        return true;
                    }

                    // After decomposing all of the above patterns, we must end up with an operation that is
                    // a reference to an instance-field (or prop) in our type.  If so, and this is the only
                    // time we've seen that field/prop, then we're good.
                    //
                    // We only do this if we actually did something that counts as hashing along the way.  This
                    // way
                    if (TryGetFieldOrProperty(value, out var fieldOrProp) &&
                        Equals(fieldOrProp.ContainingType.OriginalDefinition, _method.ContainingType))
                    {
                        return TryAddSymbol(fieldOrProp);
                    }

                    if (value is ITupleOperation tupleOperation)
                    {
                        foreach (var element in tupleOperation.Elements)
                        {
                            if (!TryAddHashedSymbol(element, seenHash: true))
                            {
                                return false;
                            }
                        }

                        return true;
                    }
                }

                // Anything else is not recognized.
                return false;
            }

            private static bool TryGetFieldOrProperty(IOperation operation, [NotNullWhen(true)]out ISymbol? symbol)
            {
                operation = Unwrap(operation);

                if (operation is IFieldReferenceOperation fieldReference)
                {
                    symbol = fieldReference.Member;
                    return !symbol.IsStatic;
                }

                if (operation is IPropertyReferenceOperation propertyReference)
                {
                    symbol = propertyReference.Member;
                    return !symbol.IsStatic;
                }

                symbol = null;
                return false;
            }

            private bool TryAddSymbol(ISymbol member)
            {
                // Not a legal GetHashCode to convert if we refer to members multiple times.
                if (_hashedSymbols.Contains(member))
                {
                    return false;
                }

                _hashedSymbols.Add(member);
                return true;
            }
        }
    }
}
