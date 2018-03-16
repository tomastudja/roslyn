﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.VirtualChars
{
    /// <summary>
    /// Helper service that takes the raw text of a string token and produces the individual
    /// characters that raw string token represents (i.e. with escapes collapsed).  The difference
    /// between this and the result from token.ValueText is that for each collapsed character returned
    /// the original span of text in the original token can be found.  i.e. if you had the following
    /// in C#:
    /// 
    /// "G\u006fo"
    /// 
    /// Then you'd get back:
    /// 
    /// 'G' -> [0, 1)
    /// 'o' -> [1, 7)
    /// 'o' -> [7, 1)
    /// 
    /// This allows for embedded language processing that can refer back to the users' original 
    /// code instead of the escaped value we're processing.
    /// 
    /// </summary>
    internal interface IVirtualCharService : ILanguageService
    {
        /// <summary>
        /// Takes in a string token and return the <see cref="VirtualChar"/>s corresponding to each
        /// char of the tokens <see cref="SyntaxToken.ValueText"/>.  In other words, for each char
        /// in ValueText there will be a VirtualChar in the resultant array.  Each VirtualChar will
        /// specify what char the language considers them to represent, as well as the span of text
        /// in the original <see cref="SourceText"/> that the language created that char from. 
        /// 
        /// For most chars this will be a single character span.  i.e. 'c' -> 'c'.  However, for
        /// escapes this may be a multi character span.  i.e. 'c' -> '\u0063'
        /// 
        /// If the token is not a string literal token, or the string literal has any diagnostics
        /// on it, then <see langword="default"/> will be returned.   Additionally, because a
        /// VirtualChar can only represent a single char, while some escape sequences represent
        /// multiple chars, <see langword="default"/> will also be returned in those cases. All these
        /// cases could be relaxed in the future.  But they greatly simplify the implementation.
        /// </summary>
        ImmutableArray<VirtualChar> TryConvertToVirtualChars(SyntaxToken token);
    }
}
