﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Indentation
{
    /// <summary>
    /// An indentation result represents where the indent should be placed.  It conveys this through
    /// a pair of values.  A position in the existing document where the indent should be relative,
    /// and the number of columns after that the indent should be placed at.  
    /// 
    /// This pairing provides flexibility to the implementor to compute the indentation results in
    /// a variety of ways.  For example, one implementation may wish to express indentation of a 
    /// newline as being four columns past the start of the first token on a previous line.  Another
    /// may wish to simply express the indentation as an absolute amount from the start of the 
    /// current line.  With this tuple, both forms can be expressed, and the implementor does not
    /// have to convert from one to the other.
    /// </summary>
    internal struct IndentationResult
    {
        /// <summary>
        /// The base position in the document that the indent should be relative to.  This position
        /// can occur on any line (including the current line, or a previous line).
        /// </summary>
        public int BasePosition { get; }

        /// <summary>
        /// The number of columns the indent should be at relative to the BasePosition's column.
        /// </summary>
        public int Offset { get; }

        public IndentationResult(int basePosition, int offset) : this()
        {
            this.BasePosition = basePosition;
            this.Offset = offset;
        }
    }

    internal interface IIndentationService : ILanguageService
    {
        /// <summary>
        /// Determines the desired indentation of a given line.  This is conceptually what indentation
        /// would be provided if 'enter' was pressed in the middle of a line.  It will determine what
        /// position the remainder of the line should start at.  This may differ from
        /// <see cref="GetBlankLineIndentation"/> if the language thinks the indentation should be
        /// different if the line is completely blank, or if it contains text after the caret.
        /// </summary>
        IndentationResult GetDesiredIndentation(Document document, int lineNumber, CancellationToken cancellationToken);

        /// <summary>
        /// Determines indentation for a blank line (i.e. after hitting enter at the end of a line,
        /// or after moving to a blank line).
        /// 
        /// Specifically, this function operates as if the line specified by <paramref name="lineNumber"/>
        /// is blank.  The actual contents of the line do not matter.  All indentation information is
        /// determined from the previous lines in the document.
        /// 
        /// This is often useful for features which want to insert new code at a certain
        /// location, indented to the appropriate amount.  This allows those features to
        /// figure out that position, without having to care about what might already be
        /// at that line (or further on in the document).
        /// 
        /// This function will always succeed.
        /// </summary>
        IndentationResult GetBlankLineIndentation(
            Document document, int lineNumber, FormattingOptions.IndentStyle indentStyle, CancellationToken cancellationToken);
    }
}
