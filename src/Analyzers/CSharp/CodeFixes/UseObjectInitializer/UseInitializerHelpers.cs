﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.UseObjectInitializer
{
    internal static class UseInitializerHelpers
    {
        public static ObjectCreationExpressionSyntax GetNewObjectCreation(
            ObjectCreationExpressionSyntax objectCreation,
            SeparatedSyntaxList<ExpressionSyntax> expressions)
        {
            var openBrace = SyntaxFactory.Token(SyntaxKind.OpenBraceToken);
            var initializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression, expressions).WithOpenBraceToken(openBrace);

            if (objectCreation.ArgumentList != null &&
                objectCreation.ArgumentList.Arguments.Count == 0)
            {
                objectCreation = objectCreation.WithType(objectCreation.Type.WithTrailingTrivia(objectCreation.ArgumentList.GetTrailingTrivia()))
                                               .WithArgumentList(null);
            }

            return objectCreation.WithInitializer(initializer);
        }

        public static void AddExistingItems(ObjectCreationExpressionSyntax objectCreation, ArrayBuilder<SyntaxNodeOrToken> nodesAndTokens)
        {
            if (objectCreation.Initializer != null)
                nodesAndTokens.AddRange(objectCreation.Initializer.Expressions.GetWithSeparators());

            if (nodesAndTokens.Count % 2 == 1)
            {
                var last = nodesAndTokens.Last();
                nodesAndTokens.RemoveLast();
                nodesAndTokens.Add(last.WithTrailingTrivia());
                nodesAndTokens.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(last.GetTrailingTrivia()));
            }
        }
    }
}
