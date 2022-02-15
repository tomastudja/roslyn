﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;

namespace Microsoft.CodeAnalysis.LanguageServices
{
    internal partial interface ISemanticFacts
    {
        /// <summary>
        /// True if this language supports implementing an interface by signature only. If false,
        /// implementations must specific explicitly which symbol they're implementing.
        /// </summary>
        bool SupportsImplicitInterfaceImplementation { get; }

        bool SupportsParameterizedProperties { get; }

        /// <summary>
        /// True if anonymous functions in this language have signatures that include named
        /// parameters that can be referenced later on when the function is invoked.  Or, if the
        /// anonymous function is simply a signature that will be assigned to a delegate, and the
        /// delegate's parameter names are used when invoking.  
        /// 
        /// For example, in VB one can do this: 
        /// 
        /// dim v = Sub(x as Integer) Blah()
        /// v(x:=4)
        /// 
        /// However, in C# that would need to be:
        /// 
        /// Action&lt;int&gt; v = (int x) => Blah();
        /// v(obj:=4)
        /// 
        /// Note that in VB one can access 'x' outside of the declaration of the anonymous type.
        /// While in C# 'x' can only be accessed within the anonymous type.
        /// </summary>
        bool ExposesAnonymousFunctionParameterNames { get; }

        /// <summary>
        /// True if a write is performed to the given expression.  Note: reads may also be performed
        /// to the expression as well.  For example, "++a".  In this expression 'a' is both read from
        /// and written to.
        /// </summary>
        bool IsWrittenTo(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken);

        /// <summary>
        /// True if a write is performed to the given expression.  Note: unlike IsWrittenTo, this
        /// will not return true if reads are performed on the expression as well.  For example,
        /// "++a" will return 'false'.  However, 'a' in "out a" or "a = 1" will return true.
        /// </summary>
        bool IsOnlyWrittenTo(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken);
        bool IsInOutContext(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken);
        bool IsInRefContext(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken);
        bool IsInInContext(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken);

        bool CanReplaceWithRValue(SemanticModel semanticModel, SyntaxNode expression, CancellationToken cancellationToken);

        ISymbol GetDeclaredSymbol(SemanticModel semanticModel, SyntaxToken token, CancellationToken cancellationToken);

        bool LastEnumValueHasInitializer(INamedTypeSymbol namedTypeSymbol);

        /// <summary>
        /// return speculative semantic model for supported node. otherwise, it will return null
        /// </summary>
        bool TryGetSpeculativeSemanticModel(SemanticModel oldSemanticModel, SyntaxNode oldNode, SyntaxNode newNode, out SemanticModel speculativeModel);

        /// <summary>
        /// get all alias names defined in the semantic model
        /// </summary>
        ImmutableHashSet<string> GetAliasNameSet(SemanticModel model, CancellationToken cancellationToken);

        ForEachSymbols GetForEachSymbols(SemanticModel semanticModel, SyntaxNode forEachStatement);

        IMethodSymbol GetGetAwaiterMethod(SemanticModel semanticModel, SyntaxNode node);

        ImmutableArray<IMethodSymbol> GetDeconstructionAssignmentMethods(SemanticModel semanticModel, SyntaxNode node);

        ImmutableArray<IMethodSymbol> GetDeconstructionForEachMethods(SemanticModel semanticModel, SyntaxNode node);

        bool IsPartial(ITypeSymbol typeSymbol, CancellationToken cancellationToken);

        bool IsNullChecked(IParameterSymbol parameter, CancellationToken cancellationToken);

        IEnumerable<ISymbol> GetDeclaredSymbols(SemanticModel semanticModel, SyntaxNode memberDeclaration, CancellationToken cancellationToken);

        IParameterSymbol FindParameterForArgument(SemanticModel semanticModel, SyntaxNode argument, CancellationToken cancellationToken);
        IParameterSymbol FindParameterForAttributeArgument(SemanticModel semanticModel, SyntaxNode argument, CancellationToken cancellationToken);

#nullable enable
        ImmutableArray<ISymbol> GetBestOrAllSymbols(SemanticModel semanticModel, SyntaxNode? node, SyntaxToken token, CancellationToken cancellationToken);
#nullable disable

        bool IsInsideNameOfExpression(SemanticModel semanticModel, SyntaxNode node, CancellationToken cancellationToken);

        /// <summary>
        /// Finds all local function definitions within the syntax references for a given <paramref name="symbol"/>
        /// </summary>
        ImmutableArray<IMethodSymbol> GetLocalFunctionSymbols(Compilation compilation, ISymbol symbol, CancellationToken cancellationToken);

        bool IsInExpressionTree(SemanticModel semanticModel, SyntaxNode node, INamedTypeSymbol expressionTypeOpt, CancellationToken cancellationToken);
    }
}
