﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MakeMethodAsynchronous;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.CodeAnalysis.CSharp.MakeMethodAsynchronous
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = PredefinedCodeFixProviderNames.AddAsync), Shared]
    internal class CSharpMakeMethodAsynchronousCodeFixProvider : AbstractMakeMethodAsynchronousCodeFixProvider
    {
        private const string CS4032 = nameof(CS4032); // The 'await' operator can only be used within an async method. Consider marking this method with the 'async' modifier and changing its return type to 'Task'.
        private const string CS4033 = nameof(CS4033); // The 'await' operator can only be used within an async method. Consider marking this method with the 'async' modifier and changing its return type to 'Task'.
        private const string CS4034 = nameof(CS4034); // The 'await' operator can only be used within an async lambda expression. Consider marking this method with the 'async' modifier.

        private static readonly SyntaxToken s_asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword);

        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public CSharpMakeMethodAsynchronousCodeFixProvider()
        {
        }

        public override ImmutableArray<string> FixableDiagnosticIds { get; } =
            ImmutableArray.Create(CS4032, CS4033, CS4034);

        protected override string GetMakeAsyncTaskFunctionResource()
            => CSharpFeaturesResources.Make_method_async;

        protected override string GetMakeAsyncVoidFunctionResource()
            => CSharpFeaturesResources.Make_method_async_remain_void;

        protected override bool IsAsyncSupportingFunctionSyntax(SyntaxNode node)
            => node.IsAsyncSupportingFunctionSyntax();

        protected override bool IsAsyncReturnType(ITypeSymbol type, KnownTypes knownTypes)
        {
            return IsIAsyncEnumerableOrEnumerator(type, knownTypes)
                || IsTaskLike(type, knownTypes);
        }

        protected override SyntaxNode AddAsyncTokenAndFixReturnType(
            bool keepVoid,
            IMethodSymbol methodSymbolOpt,
            SyntaxNode node,
            KnownTypes knownTypes,
            CancellationToken cancellationToken)
        {
            switch (node)
            {
                case MethodDeclarationSyntax method: return FixMethod(keepVoid, methodSymbolOpt, method, knownTypes, cancellationToken);
                case LocalFunctionStatementSyntax localFunction: return FixLocalFunction(keepVoid, methodSymbolOpt, localFunction, knownTypes, cancellationToken);
                case AnonymousFunctionExpressionSyntax anonymous: return FixAnonymousFunction(anonymous);
                default: return node;
            }
        }

        private static SyntaxNode FixMethod(
            bool keepVoid,
            IMethodSymbol methodSymbol,
            MethodDeclarationSyntax method,
            KnownTypes knownTypes,
            CancellationToken cancellationToken)
        {
            var newReturnType = FixMethodReturnType(keepVoid, methodSymbol, method.ReturnType, knownTypes, cancellationToken);
            var newModifiers = AddAsyncModifierWithCorrectedTrivia(method.Modifiers, ref newReturnType);
            return method.WithReturnType(newReturnType).WithModifiers(newModifiers);
        }

        private static SyntaxNode FixLocalFunction(
            bool keepVoid,
            IMethodSymbol methodSymbol,
            LocalFunctionStatementSyntax localFunction,
            KnownTypes knownTypes,
            CancellationToken cancellationToken)
        {
            var newReturnType = FixMethodReturnType(keepVoid, methodSymbol, localFunction.ReturnType, knownTypes, cancellationToken);
            var newModifiers = AddAsyncModifierWithCorrectedTrivia(localFunction.Modifiers, ref newReturnType);
            return localFunction.WithReturnType(newReturnType).WithModifiers(newModifiers);
        }

        private static TypeSyntax FixMethodReturnType(
            bool keepVoid,
            IMethodSymbol methodSymbol,
            TypeSyntax returnTypeSyntax,
            KnownTypes knownTypes,
            CancellationToken cancellationToken)
        {
            var newReturnType = returnTypeSyntax.WithAdditionalAnnotations(Formatter.Annotation);

            if (methodSymbol.ReturnsVoid)
            {
                if (!keepVoid)
                {
                    newReturnType = knownTypes.TaskType.GenerateTypeSyntax();
                }
            }
            else
            {
                var returnType = methodSymbol.ReturnType;
                if (IsIEnumerable(returnType, knownTypes) && IsIterator(methodSymbol, cancellationToken))
                {
                    newReturnType = knownTypes.IAsyncEnumerableOfTTypeOpt is null
                        ? MakeGenericType(nameof(IAsyncEnumerable<int>), methodSymbol.ReturnType)
                        : knownTypes.IAsyncEnumerableOfTTypeOpt.Construct(methodSymbol.ReturnType.GetTypeArguments()[0]).GenerateTypeSyntax();
                }
                else if (IsIEnumerator(returnType, knownTypes) && IsIterator(methodSymbol, cancellationToken))
                {
                    newReturnType = knownTypes.IAsyncEnumeratorOfTTypeOpt is null
                        ? MakeGenericType(nameof(IAsyncEnumerator<int>), methodSymbol.ReturnType)
                        : knownTypes.IAsyncEnumeratorOfTTypeOpt.Construct(methodSymbol.ReturnType.GetTypeArguments()[0]).GenerateTypeSyntax();
                }
                else if (IsIAsyncEnumerableOrEnumerator(returnType, knownTypes))
                {
                    // Leave the return type alone
                }
                else if (!IsTaskLike(returnType, knownTypes))
                {
                    // If it's not already Task-like, then wrap the existing return type
                    // in Task<>.
                    newReturnType = knownTypes.TaskOfTType.Construct(methodSymbol.ReturnType).GenerateTypeSyntax();
                }
            }

            return newReturnType.WithTriviaFrom(returnTypeSyntax).WithAdditionalAnnotations(Simplifier.AddImportsAnnotation);

            static TypeSyntax MakeGenericType(string type, ITypeSymbol typeArgumentFrom)
            {
                var result = SyntaxFactory.GenericName(SyntaxFactory.Identifier(type),
                        SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(typeArgumentFrom.GetTypeArguments()[0].GenerateTypeSyntax())));

                return result.WithAdditionalAnnotations(Simplifier.Annotation);
            }
        }

        private static bool IsIterator(IMethodSymbol method, CancellationToken cancellationToken)
            => method.Locations.Any(static (loc, cancellationToken) => loc.FindNode(cancellationToken).ContainsYield(), cancellationToken);

        private static bool IsIAsyncEnumerableOrEnumerator(ITypeSymbol returnType, KnownTypes knownTypes)
            => returnType.OriginalDefinition.Equals(knownTypes.IAsyncEnumerableOfTTypeOpt) ||
                returnType.OriginalDefinition.Equals(knownTypes.IAsyncEnumeratorOfTTypeOpt);

        private static bool IsIEnumerable(ITypeSymbol returnType, KnownTypes knownTypes)
            => returnType.OriginalDefinition.Equals(knownTypes.IEnumerableOfTType);

        private static bool IsIEnumerator(ITypeSymbol returnType, KnownTypes knownTypes)
            => returnType.OriginalDefinition.Equals(knownTypes.IEnumeratorOfTType);

        private static SyntaxTokenList AddAsyncModifierWithCorrectedTrivia(SyntaxTokenList modifiers, ref TypeSyntax newReturnType)
        {
            if (modifiers.Any())
                return modifiers.Add(s_asyncToken);

            // Move the leading trivia from the return type to the new modifiers list.
            var result = SyntaxFactory.TokenList(s_asyncToken.WithLeadingTrivia(newReturnType.GetLeadingTrivia()));
            newReturnType = newReturnType.WithoutLeadingTrivia();
            return result;
        }

        private static SyntaxNode FixAnonymousFunction(AnonymousFunctionExpressionSyntax anonymous)
        {
            return anonymous.WithoutLeadingTrivia()
                         .WithAsyncKeyword(s_asyncToken.WithPrependedLeadingTrivia(anonymous.GetLeadingTrivia()));
        }
    }
}
