﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Editor.Navigation
{
    internal partial class NavigableItemFactory
    {
        private class SymbolLocationNavigableItem : INavigableItem
        {
            private readonly Solution _solution;
            private readonly ISymbol _symbol;
            private readonly Location _location;
            private readonly Lazy<string> _lazyDisplayString;

            public SymbolLocationNavigableItem(
                Solution solution,
                ISymbol symbol,
                Location location)
            {
                _solution = solution;
                _symbol = symbol;
                _location = location;

                _lazyDisplayString = new Lazy<string>(() =>
                {
                    return GetSymbolDisplayString(Document.Project, _symbol);
                });
            }

            public string DisplayString
            {
                get
                {
                    return _lazyDisplayString.Value;
                }
            }

            public Glyph Glyph
            {
                get
                {
                    return _symbol.GetGlyph();
                }
            }

            public Document Document
            {
                get
                {
                    return _location.IsInSource ? _solution.GetDocument(_location.SourceTree) : null;
                }
            }

            public TextSpan SourceSpan
            {
                get
                {
                    return _location.SourceSpan;
                }
            }

            public ImmutableArray<INavigableItem> ChildItems => ImmutableArray<INavigableItem>.Empty;
        }
    }
}
