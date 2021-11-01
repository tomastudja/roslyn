﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.Classification.Classifiers
{
    internal abstract class AbstractSyntaxClassifier : ISyntaxClassifier
    {
        protected AbstractSyntaxClassifier()
        {
        }

        protected static string? GetClassificationForType(ITypeSymbol type)
            => type.GetClassification();

        public virtual void AddClassifications(SyntaxNode syntax, SemanticModel semanticModel, ClassificationOptions options, ArrayBuilder<ClassifiedSpan> result, CancellationToken cancellationToken)
        {
        }

        public virtual void AddClassifications(SyntaxToken syntax, SemanticModel semanticModel, ClassificationOptions options, ArrayBuilder<ClassifiedSpan> result, CancellationToken cancellationToken)
        {
        }

        public virtual ImmutableArray<Type> SyntaxNodeTypes
            => ImmutableArray<Type>.Empty;

        public virtual ImmutableArray<int> SyntaxTokenKinds
            => ImmutableArray<int>.Empty;
    }
}
