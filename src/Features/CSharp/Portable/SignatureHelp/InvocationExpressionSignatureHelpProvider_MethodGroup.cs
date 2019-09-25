﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.SignatureHelp;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.SignatureHelp
{
    internal partial class InvocationExpressionSignatureHelpProvider
    {
        private (IList<SignatureHelpItem>, int?) GetMethodGroupItemsAndSelection(
            Document document,
            InvocationExpressionSyntax invocationExpression,
            SemanticModel semanticModel,
            ISymbol within,
            IEnumerable<IMethodSymbol> methodGroup,
            SymbolInfo currentSymbol,
            CancellationToken cancellationToken)
        {
            ITypeSymbol throughType = null;
            if (invocationExpression.Expression is MemberAccessExpressionSyntax)
            {
                var throughExpression = ((MemberAccessExpressionSyntax)invocationExpression.Expression).Expression;
                var throughSymbol = semanticModel.GetSymbolInfo(throughExpression, cancellationToken).GetAnySymbol();

                // if it is via a base expression "base.", we know the "throughType" is the base class but
                // we need to be able to tell between "base.M()" and "new Base().M()".
                // currently, Access check methods do not differentiate between them.
                // so handle "base." primary-expression here by nulling out "throughType"
                if (!(throughExpression is BaseExpressionSyntax))
                {
                    throughType = semanticModel.GetTypeInfo(throughExpression, cancellationToken).Type;
                }

                var includeInstance = !throughExpression.IsKind(SyntaxKind.IdentifierName) ||
                    semanticModel.LookupSymbols(throughExpression.SpanStart, name: throughSymbol.Name).Any(s => !(s is INamedTypeSymbol)) ||
                    (!(throughSymbol is INamespaceOrTypeSymbol) && semanticModel.LookupSymbols(throughExpression.SpanStart, container: throughSymbol.ContainingType).Any(s => !(s is INamedTypeSymbol)));

                var includeStatic = throughSymbol is INamedTypeSymbol ||
                    (throughExpression.IsKind(SyntaxKind.IdentifierName) &&
                    semanticModel.LookupNamespacesAndTypes(throughExpression.SpanStart, name: throughSymbol.Name).Any(t => Equals(t.GetSymbolType(), throughType)));

                Contract.ThrowIfFalse(includeInstance || includeStatic);
                methodGroup = methodGroup.Where(m => (m.IsStatic && includeStatic) || (!m.IsStatic && includeInstance));
            }
            else if (invocationExpression.Expression is SimpleNameSyntax &&
                invocationExpression.IsInStaticContext())
            {
                methodGroup = methodGroup.Where(m => m.IsStatic);
            }

            var accessibleMethods = methodGroup.Where(m => m.IsAccessibleWithin(within, throughType: throughType)).ToImmutableArrayOrEmpty();
            if (accessibleMethods.Length == 0)
            {
                return default;
            }

            var methodSet = accessibleMethods.ToSet();
            accessibleMethods = accessibleMethods.Where(m => !IsHiddenByOtherMethod(m, methodSet)).ToImmutableArrayOrEmpty();

            return (accessibleMethods.Select(m =>
                ConvertMethodGroupMethod(document, m, invocationExpression.SpanStart, semanticModel, cancellationToken)).ToList(),
                TryGetSelectedIndex(accessibleMethods, currentSymbol));
        }

        private bool IsHiddenByOtherMethod(IMethodSymbol method, ISet<IMethodSymbol> methodSet)
        {
            foreach (var m in methodSet)
            {
                if (!Equals(m, method))
                {
                    if (IsHiddenBy(method, m))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsHiddenBy(IMethodSymbol method1, IMethodSymbol method2)
        {
            // If they have the same parameter types and the same parameter names, then the 
            // constructed method is hidden by the unconstructed one.
            return method2.IsMoreSpecificThan(method1) == true;
        }
    }
}
