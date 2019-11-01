﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Symbols.PublicModel
{
    internal sealed class ErrorTypeSymbol : NamedTypeSymbol, IErrorTypeSymbol
    {
        private readonly Symbols.ErrorTypeSymbol _underlying;

        public ErrorTypeSymbol(Symbols.ErrorTypeSymbol underlying, CodeAnalysis.NullableAnnotation nullableAnnotation)
            : base(nullableAnnotation)
        {
            Debug.Assert(underlying is object);
            _underlying = underlying;
        }

        protected override ITypeSymbol WithNullableAnnotation(CodeAnalysis.NullableAnnotation nullableAnnotation)
        {
            Debug.Assert(nullableAnnotation != _underlying.DefaultNullableAnnotation);
            Debug.Assert(nullableAnnotation != this.NullableAnnotation);
            return new ErrorTypeSymbol(_underlying, nullableAnnotation);
        }

        internal override CSharp.Symbol UnderlyingSymbol => _underlying;
        internal override Symbols.NamespaceOrTypeSymbol UnderlyingNamespaceOrTypeSymbol => _underlying;
        internal override Symbols.TypeSymbol UnderlyingTypeSymbol => _underlying;
        internal override Symbols.NamedTypeSymbol UnderlyingNamedTypeSymbol => _underlying;

        ImmutableArray<ISymbol> IErrorTypeSymbol.CandidateSymbols => _underlying.CandidateSymbols.SelectAsArray(s => s.GetPublicSymbol());

        CandidateReason IErrorTypeSymbol.CandidateReason => _underlying.CandidateReason;
    }
}
