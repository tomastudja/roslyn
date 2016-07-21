﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Editor.Navigation
{
    internal interface INavigableItem
    {
        Glyph Glyph { get; }

        /// <summary>
        /// The string to display for this item. If null, the line of text from <see cref="Document"/> is used.
        /// </summary>
        string DisplayString { get; }

        /// <summary>
        /// Return true to display the file path of <see cref="Document"/> and the span of <see cref="SourceSpan"/> when displaying this item.
        /// </summary>
        bool DisplayFileLocation { get; }

        /// <summary>
        /// his is intended for symbols that are ordinary symbols in the language sense, and may be
        /// used by code, but that are simply declared implicitly rather than with explicit language
        /// syntax.  For example, a default synthesized constructor in C# when the class contains no
        /// explicit constructors.
        /// </summary>
        bool IsImplicitlyDeclared { get; }

        Document Document { get; }
        TextSpan SourceSpan { get; }

        ImmutableArray<INavigableItem> ChildItems { get; }
    }
}