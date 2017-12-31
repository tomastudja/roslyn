﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.RegularExpressions
{
    using static RegexHelpers;

    internal struct RegexLexer
    {
        public readonly ImmutableArray<VirtualChar> Text;
        public int Position;

        public RegexLexer(ImmutableArray<VirtualChar> text) : this()
        {
            Text = text;
        }

        public VirtualChar CurrentChar => Position < Text.Length ? Text[Position] : new VirtualChar((char)0, default);
        private VirtualChar PreviousChar => Text[Position - 1];

        public ImmutableArray<VirtualChar> GetSubPattern(int start, int end)
        {
            var result = ArrayBuilder<VirtualChar>.GetInstance(end - start);
            for (var i = start; i < end; i++)
            {
                result.Add(Text[i]);
            }

            return result.ToImmutableAndFree();
        }

        public RegexToken ScanNextToken(bool allowTrivia, RegexOptions options)
        {
            var trivia = ScanLeadingTrivia(allowTrivia, options);
            if (Position == Text.Length)
            {
                return new RegexToken(trivia, RegexKind.EndOfFile, ImmutableArray<VirtualChar>.Empty);
            }

            var ch = this.CurrentChar;
            Position++;

            var chars = ImmutableArray.Create(ch);
            return TryGetKind(ch, out var kind)
                ? new RegexToken(trivia, kind, chars)
                : new RegexToken(trivia, RegexKind.TextToken, chars);
        }

        private bool IsSpecial(char ch)
            =>IsPrimarySpecialChar(ch) ||
              IsQuantifierChar(ch) ||
              IsAlternationChar(ch);

        public static bool IsAlternationChar(char ch)
            => ch == '|';

        private static bool IsQuantifierChar(char ch)
        {
            switch (ch)
            {
            case '*':
            case '+':
            case '?':
            case '{':
                return true;
            default:
                return false;
            }
        }

        private static bool IsPrimarySpecialChar(char ch)
        {
            switch (ch)
            {
            case '\\':
            case '[':
            case '.':
            case '^':
            case '$':
            case '(':
            case ')':
                //case '#':
                return true;
            default:
                return false;
            }
        }

        private static RegexKind GetKind(char ch)
        {
            switch (ch)
            {
            case '|': return RegexKind.BarToken;
            case '*': return RegexKind.AsteriskToken;
            case '+': return RegexKind.PlusToken;
            case '?': return RegexKind.QuestionToken;
            case '{': return RegexKind.OpenBraceToken;
            case '\\': return RegexKind.BackslashToken;
            case '[': return RegexKind.OpenBracketToken;
            case '.': return RegexKind.DotToken;
            case '^': return RegexKind.CaretToken;
            case '$': return RegexKind.DollarToken;
            case '(': return RegexKind.OpenParenToken;
            case ')': return RegexKind.CloseParenToken;
            default: return RegexKind.None;
            }
        }

        private static bool TryGetKind(char ch, out RegexKind kind)
        {
            kind = GetKind(ch);
            return kind != RegexKind.None;
        }


        private ImmutableArray<RegexTrivia> ScanLeadingTrivia(bool allowTrivia, RegexOptions options)
        {
            if (!allowTrivia)
            {
                return ImmutableArray<RegexTrivia>.Empty;
            }

            var result = ArrayBuilder<RegexTrivia>.GetInstance();

            var start = Position;

            while (Position < Text.Length)
            {
                var comment = ScanComment(options);
                if (comment != null)
                {
                    result.Add(comment.Value);
                    continue;
                }

                var whitespace = ScanWhitespace(options);
                if (whitespace != null)
                {
                    result.Add(whitespace.Value);
                    continue;
                }

                break;
            }

            return result.ToImmutableAndFree();
        }

        public RegexTrivia? ScanComment(RegexOptions options)
        {
            if (Position < Text.Length)
            {
                if (HasOption(options, RegexOptions.IgnorePatternWhitespace))
                {
                    if (Text[Position] == '#')
                    {
                        var start = Position;
                        while (Position < Text.Length &&
                               Text[Position] != '\n')
                        {
                            Position++;
                        }

                        return new RegexTrivia(RegexKind.CommentTrivia, GetSubPattern(start, Position));
                    }
                }

                if (TextAt(Position, "(?#"))
                {
                    var start = Position;
                    while (Position < Text.Length &&
                           Text[Position] != ')')
                    {
                        Position++;
                    }

                    if (Position == Text.Length)
                    {
                        var diagnostics = ImmutableArray.Create(new RegexDiagnostic(
                            WorkspacesResources.Unterminated_regex_comment,
                            GetTextSpan(start, Position)));
                        return new RegexTrivia(RegexKind.CommentTrivia, GetSubPattern(start, Position), diagnostics);
                    }

                    Position++;
                    return new RegexTrivia(RegexKind.CommentTrivia, GetSubPattern(start, Position));
                }
            }

            return null;
        }

        public TextSpan GetTextSpan(int startInclusive, int endExclusive)
            => TextSpan.FromBounds(Text[startInclusive].Span.Start, Text[endExclusive - 1].Span.End);

        public bool IsAt(string val)
            => TextAt(this.Position, val);

        public bool TextAt(int position, string val)
        {
            for (var i = 0; i < val.Length; i++)
            {
                if (position + i >= Text.Length ||
                    Text[position + i] != val[i])
                {
                    return false;
                }
            }

            return true;
        }

        private RegexTrivia? ScanWhitespace(RegexOptions options)
        {
            if (HasOption(options, RegexOptions.IgnorePatternWhitespace))
            {
                var start = Position;
                while (Position < Text.Length && IsBlank(Text[Position]))
                {
                    Position++;
                }

                if (Position > start)
                {
                    return new RegexTrivia(RegexKind.WhitespaceTrivia, GetSubPattern(start, Position));
                }
            }

            return null;
        }

        private bool IsBlank(char ch)
        {
            switch (ch)
            {
            case '\u0009':
            case '\u000A':
            case '\u000C':
            case '\u000D':
            case ' ':
                return true;
            default:
                return false;
            }
        }

        public RegexToken? TryScanEscapeCategory()
        {
            var start = Position;
            while (Position < Text.Length &&
                   this.CurrentChar is var ch &&
                   IsEscapeCategoryChar(ch))
            {
                Position++;
            }

            if (Position == start)
            {
                return null;
            }

            var token = new RegexToken(ImmutableArray<RegexTrivia>.Empty, RegexKind.EscapeCategoryToken, GetSubPattern(start, Position));
            var category = new string(token.VirtualChars.Select(vc => vc.Char).ToArray());

            if (!s_escapeCategories.Contains(category))
            {
                token = token.AddDiagnosticIfNone(new RegexDiagnostic(
                    string.Format(WorkspacesResources.Unknown_property_0, category),
                    GetSpan(token)));
            }

            return token;
        }

        private static readonly HashSet<string> s_escapeCategories = new HashSet<string>
        {
            // Others
            "Cc", "Cf", "Cn", "Co", "Cs", "C",         
            // Letters
            "Ll", "Lm", "Lo", "Lt", "Lu", "L",         
            // Marks
            "Mc", "Me", "Mn", "M", 
            // Numbers
            "Nd", "Nl", "No", "N",                       
            // Punctuation
            "Pc", "Pd", "Pe", "Po", "Ps", "Pf", "Pi", "P",     
            // Symbols
            "Sc", "Sk", "Sm", "So", "S",               
            // Separators
            "Zl", "Zp", "Zs", "Z",                     

            "IsAlphabeticPresentationForms",
            "IsArabic",
            "IsArabicPresentationForms-A",
            "IsArabicPresentationForms-B",
            "IsArmenian",
            "IsArrows",
            "IsBasicLatin",
            "IsBengali",
            "IsBlockElements",
            "IsBopomofo",
            "IsBopomofoExtended",
            "IsBoxDrawing",
            "IsBraillePatterns",
            "IsBuhid",
            "IsCJKCompatibility",
            "IsCJKCompatibilityForms",
            "IsCJKCompatibilityIdeographs",
            "IsCJKRadicalsSupplement",
            "IsCJKSymbolsandPunctuation",
            "IsCJKUnifiedIdeographs",
            "IsCJKUnifiedIdeographsExtensionA",
            "IsCherokee",
            "IsCombiningDiacriticalMarks",
            "IsCombiningDiacriticalMarksforSymbols",
            "IsCombiningHalfMarks",
            "IsCombiningMarksforSymbols",
            "IsControlPictures",
            "IsCurrencySymbols",
            "IsCyrillic",
            "IsCyrillicSupplement",
            "IsDevanagari",
            "IsDingbats",
            "IsEnclosedAlphanumerics",
            "IsEnclosedCJKLettersandMonths",
            "IsEthiopic",
            "IsGeneralPunctuation",
            "IsGeometricShapes",
            "IsGeorgian",
            "IsGreek",
            "IsGreekExtended",
            "IsGreekandCoptic",
            "IsGujarati",
            "IsGurmukhi",
            "IsHalfwidthandFullwidthForms",
            "IsHangulCompatibilityJamo",
            "IsHangulJamo",
            "IsHangulSyllables",
            "IsHanunoo",
            "IsHebrew",
            "IsHighPrivateUseSurrogates",
            "IsHighSurrogates",
            "IsHiragana",
            "IsIPAExtensions",
            "IsIdeographicDescriptionCharacters",
            "IsKanbun",
            "IsKangxiRadicals",
            "IsKannada",
            "IsKatakana",
            "IsKatakanaPhoneticExtensions",
            "IsKhmer",
            "IsKhmerSymbols",
            "IsLao",
            "IsLatin-1Supplement",
            "IsLatinExtended-A",
            "IsLatinExtended-B",
            "IsLatinExtendedAdditional",
            "IsLetterlikeSymbols",
            "IsLimbu",
            "IsLowSurrogates",
            "IsMalayalam",
            "IsMathematicalOperators",
            "IsMiscellaneousMathematicalSymbols-A",
            "IsMiscellaneousMathematicalSymbols-B",
            "IsMiscellaneousSymbols",
            "IsMiscellaneousSymbolsandArrows",
            "IsMiscellaneousTechnical",
            "IsMongolian",
            "IsMyanmar",
            "IsNumberForms",
            "IsOgham",
            "IsOpticalCharacterRecognition",
            "IsOriya",
            "IsPhoneticExtensions",
            "IsPrivateUse",
            "IsPrivateUseArea",
            "IsRunic",
            "IsSinhala",
            "IsSmallFormVariants",
            "IsSpacingModifierLetters",
            "IsSpecials",
            "IsSuperscriptsandSubscripts",
            "IsSupplementalArrows-A",
            "IsSupplementalArrows-B",
            "IsSupplementalMathematicalOperators",
            "IsSyriac",
            "IsTagalog",
            "IsTagbanwa",
            "IsTaiLe",
            "IsTamil",
            "IsTelugu",
            "IsThaana",
            "IsThai",
            "IsTibetan",
            "IsUnifiedCanadianAboriginalSyllabics",
            "IsVariationSelectors",
            "IsYiRadicals",
            "IsYiSyllables",
            "IsYijingHexagramSymbols",
            "_xmlC",
            "_xmlD",
            "_xmlI",
            "_xmlW",
        };

        private static bool IsEscapeCategoryChar(VirtualChar ch)
            => ch == '-' ||
               (ch >= 'a' && ch <= 'z') ||
               (ch >= 'A' && ch <= 'Z');

        public RegexToken? TryScanNumber()
        {
            if (Position == Text.Length)
            {
                return null;
            }

            if (this.CurrentChar < '0' || this.CurrentChar > '9')
            {
                return null;
            }

            const int MaxValueDiv10 = int.MaxValue / 10;
            const int MaxValueMod10 = int.MaxValue % 10;

            var value = 0;
            var start = Position;
            var error = false;
            while (Position < Text.Length && this.CurrentChar is var ch && ch >= '0' && ch <= '9')
            {
                Position++;

                unchecked
                {
                    var charVal = ch - '0';
                    if (value > MaxValueDiv10 || (value == MaxValueDiv10 && charVal > MaxValueMod10))
                    {
                        error = true;
                    }

                    value *= 10;
                    value += charVal;
                }
            }

            var token = new RegexToken(ImmutableArray<RegexTrivia>.Empty, RegexKind.NumberToken, GetSubPattern(start, Position));
            token = token.With(value: value);

            if (error)
            {
                token = token.AddDiagnosticIfNone(new RegexDiagnostic(
                    WorkspacesResources.Capture_group_numbers_must_be_less_than_or_equal_to_Int32_MaxValue,
                    GetSpan(token)));
            }

            return token;
        }

        public RegexToken? TryScanCaptureName()
        {
            if (Position == Text.Length)
            {
                return null;
            }

            var start = Position;
            while (Position < Text.Length && RegexCharClass.IsWordChar(this.CurrentChar))
            {
                Position++;
            }

            if (Position == start)
            {
                return null;
            }

            var token = new RegexToken(ImmutableArray<RegexTrivia>.Empty, RegexKind.CaptureNameToken, GetSubPattern(start, Position));
            token = token.With(value: new string(token.VirtualChars.Select(vc => vc.Char).ToArray()));
            return token;
        }

        public RegexToken? TryScanNumberOrCaptureName()
            => TryScanNumber() ?? TryScanCaptureName();

        public RegexToken? TryScanOptions()
        {
            var start = Position;
            while (Position < Text.Length && IsOptionChar(this.CurrentChar))
            {
                Position++;
            }

            return start == Position
                ? default(RegexToken?)
                : new RegexToken(ImmutableArray<RegexTrivia>.Empty, RegexKind.OptionsToken, GetSubPattern(start, Position));
        }

        private bool IsOptionChar(char ch)
        {
            switch(ch)
            {
                case '+': case '-':
                case 'i': case 'I':
                case 'm': case 'M':
                case 'n': case 'N':
                case 's': case 'S':
                case 'x': case 'X':
                    return true;
                default:
                    return false;
            }
        }

        public RegexToken ScanHexCharacters(int count)
        {
            var start = Position;
            var beforeSlash = start - 2;

            // Make sure we're right after the \x or \u.
            Debug.Assert(Text[beforeSlash].Char == '\\');
            Debug.Assert(Text[beforeSlash + 1].Char == 'x' || Text[beforeSlash + 1].Char == 'u');

            for (int i = 0; i < count; i++)
            {
                if (Position < Text.Length && IsHexChar(this.CurrentChar))
                {
                    Position++;
                }
            }

            var result = new RegexToken(
                ImmutableArray<RegexTrivia>.Empty, RegexKind.TextToken, GetSubPattern(start, Position));

            var length = Position - start;
            if (length != count)
            {
                result = result.AddDiagnosticIfNone(new RegexDiagnostic(
                    WorkspacesResources.Insufficient_hexadecimal_digits,
                    TextSpan.FromBounds(Text[beforeSlash].Span.Start, Text[Position - 1].Span.End)));
            }

            return result;
        }

        private static bool IsHexChar(char ch)
            => (ch >= '0' && ch <= '9') ||
               (ch >= 'a' && ch <= 'f') ||
               (ch >= 'A' && ch <= 'F');

        private static bool IsOctalChar(char ch)
            => ch >= '0' && ch <= '7';

        public RegexToken ScanOctalCharacters(RegexOptions options)
        {
            var start = Position;
            var beforeSlash = start - 1;

            // Make sure we're right after the \ or \.
            Debug.Assert(Text[beforeSlash].Char == '\\');

            const int maxChars = 3;
            int currentVal = 0;

            for (int i = 0; i < maxChars; i++)
            {
                if (Position < Text.Length && IsOctalChar(this.CurrentChar))
                {
                    var octalVal = this.CurrentChar - '0';
                    Debug.Assert(octalVal >= 0 && octalVal <= 7);
                    currentVal *= 8;
                    currentVal += octalVal;

                    Position++;

                    // Ecmascript doesn't allow octal values above 32 (0x20 in hex)
                    if (HasOption(options, RegexOptions.ECMAScript) && currentVal >= 0x20)
                    {
                        break;
                    }
                }
            }

            var result = new RegexToken(
                ImmutableArray<RegexTrivia>.Empty, RegexKind.TextToken, GetSubPattern(start, Position));

            var length = Position - start;
            Debug.Assert(length > 0);

            return result;
        }
    }
}
