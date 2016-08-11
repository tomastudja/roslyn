﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Shared.Utilities;
using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.QuickInfo
{
    internal abstract partial class CommonSemanticQuickInfoElementProvider : CommonQuickInfoElementProvider
    {
        private class SymbolComparer : IEqualityComparer<ISymbol>
        {
            public static readonly SymbolComparer Instance = new SymbolComparer();

            private SymbolComparer()
            {
            }

            public bool Equals(ISymbol x, ISymbol y)
            {
                if (x is ILabelSymbol || x is ILocalSymbol || x is IRangeVariableSymbol)
                {
                    return object.ReferenceEquals(x, y);
                }

                return SymbolEquivalenceComparer.Instance.Equals(x, y);
            }

            public int GetHashCode(ISymbol obj)
            {
                if (obj is ILabelSymbol || obj is ILocalSymbol || obj is IRangeVariableSymbol)
                {
                    return obj.GetHashCode();
                }

                return SymbolEquivalenceComparer.Instance.GetHashCode(obj);
            }
        }
    }
}