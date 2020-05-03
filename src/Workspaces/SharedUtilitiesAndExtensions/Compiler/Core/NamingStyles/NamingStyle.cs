﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Diagnostics.Analyzers.NamingStyles;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.NamingStyles
{
    internal partial struct NamingStyle : IEquatable<NamingStyle>
    {
        public Guid ID { get; }
        public string Name { get; }
        public string Prefix { get; }
        public string Suffix { get; }
        public string WordSeparator { get; }
        public Capitalization CapitalizationScheme { get; }

        public NamingStyle(
            Guid id, string name = null, string prefix = null, string suffix = null,
            string wordSeparator = null, Capitalization capitalizationScheme = Capitalization.PascalCase) : this()
        {
            ID = id;
            Name = name;
            Prefix = prefix ?? "";
            Suffix = suffix ?? "";
            WordSeparator = wordSeparator ?? "";
            CapitalizationScheme = capitalizationScheme;
        }

        public NamingStyle With(
          Optional<string> name = default,
          Optional<string> prefix = default,
          Optional<string> suffix = default,
          Optional<string> wordSeparator = default,
          Optional<Capitalization> capitalizationScheme = default)
        {
            var newName = name.HasValue ? name.Value : this.Name;
            var newPrefix = prefix.HasValue ? prefix.Value : this.Prefix;
            var newSuffix = suffix.HasValue ? suffix.Value : this.Suffix;
            var newWordSeparator = wordSeparator.HasValue ? wordSeparator.Value : this.WordSeparator;
            var newCapitalizationScheme = capitalizationScheme.HasValue ? capitalizationScheme.Value : this.CapitalizationScheme;

            if (newName == this.Name &&
                newPrefix == this.Prefix &&
                newSuffix == this.Suffix &&
                newWordSeparator == this.WordSeparator &&
                newCapitalizationScheme == this.CapitalizationScheme)
            {
                return this;
            }

            return new NamingStyle(this.ID,
                newName, newPrefix, newSuffix, newWordSeparator, newCapitalizationScheme);
        }

        public override bool Equals(object obj)
        {
            return obj is NamingStyle other
                && Equals(other);
        }

        public bool Equals(NamingStyle other)
        {
            return ID == other.ID
                && Name == other.Name
                && Prefix == other.Prefix
                && Suffix == other.Suffix
                && WordSeparator == other.WordSeparator
                && CapitalizationScheme == other.CapitalizationScheme;
        }

        public override int GetHashCode()
        {
            return Hash.Combine(ID.GetHashCode(),
                Hash.Combine(Name?.GetHashCode() ?? 0,
                    Hash.Combine(Prefix?.GetHashCode() ?? 0,
                        Hash.Combine(Suffix?.GetHashCode() ?? 0,
                            Hash.Combine(WordSeparator?.GetHashCode() ?? 0,
                                (int)CapitalizationScheme)))));
        }

        public string CreateName(ImmutableArray<string> words)
        {
            var wordsWithCasing = ApplyCapitalization(words);
            var combinedWordsWithCasing = string.Join(WordSeparator, wordsWithCasing);
            return Prefix + combinedWordsWithCasing + Suffix;
        }

        private IEnumerable<string> ApplyCapitalization(IEnumerable<string> words)
        {
            switch (CapitalizationScheme)
            {
                case Capitalization.PascalCase:
                    return words.Select(CapitalizeFirstLetter);
                case Capitalization.CamelCase:
                    return words.Take(1).Select(DecapitalizeFirstLetter).Concat(words.Skip(1).Select(CapitalizeFirstLetter));
                case Capitalization.FirstUpper:
                    return words.Take(1).Select(CapitalizeFirstLetter).Concat(words.Skip(1).Select(DecapitalizeFirstLetter));
                case Capitalization.AllUpper:
                    return words.Select(w => w.ToUpper());
                case Capitalization.AllLower:
                    return words.Select(w => w.ToLower());
                default:
                    throw new InvalidOperationException();
            }
        }

        private string CapitalizeFirstLetter(string word)
        {
            if (word.Length == 0)
            {
                return word;
            }

            if (char.IsUpper(word[0]))
            {
                return word;
            }

            var chars = word.ToCharArray();
            chars[0] = char.ToUpper(chars[0]);

            return new string(chars);
        }

        private string DecapitalizeFirstLetter(string word)
        {
            if (word.Length == 0)
            {
                return word;
            }

            if (char.IsLower(word[0]))
            {
                return word;
            }

            var chars = word.ToCharArray();
            chars[0] = char.ToLower(chars[0]);

            return new string(chars);
        }

        public bool IsNameCompliant(string name, out string failureReason)
        {
            if (!name.StartsWith(Prefix))
            {
                failureReason = string.Format(CompilerExtensionsResources.Missing_prefix_colon_0, Prefix);
                return false;
            }

            if (!name.EndsWith(Suffix))
            {
                failureReason = string.Format(CompilerExtensionsResources.Missing_suffix_colon_0, Suffix);
                return false;
            }

            if (name.Length <= Prefix.Length + Suffix.Length)
            {
                // name consists of Prefix and Suffix and no base name
                // Prefix and Suffix can overlap
                // Example: Prefix = "s_", Suffix = "_t", name "s_t"
                failureReason = null;
                return true;
            }

            // remove specified Prefix, then look for any other common prefixes
            name = StripCommonPrefixes(name.Substring(Prefix.Length), out var prefix);

            if (prefix != string.Empty)
            {
                // name started with specified prefix, but has at least one additional common prefix 
                // Example: specified prefix "test_", actual prefix "test_m_"
                failureReason = Prefix == string.Empty ?
                    string.Format(CompilerExtensionsResources.Prefix_0_is_not_expected, prefix) :
                    string.Format(CompilerExtensionsResources.Prefix_0_does_not_match_expected_prefix_1, prefix, Prefix);
                return false;
            }

            // specified and common prefixes have been removed. Now see that the base name has correct capitalization
            var spanToCheck = TextSpan.FromBounds(0, name.Length - Suffix.Length);
            Debug.Assert(spanToCheck.Length > 0);

            switch (CapitalizationScheme)
            {
                case Capitalization.PascalCase: return CheckPascalCase(name, spanToCheck, out failureReason);
                case Capitalization.CamelCase: return CheckCamelCase(name, spanToCheck, out failureReason);
                case Capitalization.FirstUpper: return CheckFirstUpper(name, spanToCheck, out failureReason);
                case Capitalization.AllUpper: return CheckAllUpper(name, spanToCheck, out failureReason);
                case Capitalization.AllLower: return CheckAllLower(name, spanToCheck, out failureReason);
                default: throw new InvalidOperationException();
            }
        }

        private WordSpanEnumerable GetWordSpans(string name, TextSpan nameSpan)
            => new WordSpanEnumerable(name, nameSpan, WordSeparator);

        private static string Substring(string name, TextSpan wordSpan)
            => name.Substring(wordSpan.Start, wordSpan.Length);

        private static readonly Func<string, TextSpan, bool> s_firstCharIsLowerCase = (val, span) => !DoesCharacterHaveCasing(val[span.Start]) || char.IsLower(val[span.Start]);
        private static readonly Func<string, TextSpan, bool> s_firstCharIsUpperCase = (val, span) => !DoesCharacterHaveCasing(val[span.Start]) || char.IsUpper(val[span.Start]);

        private static readonly Func<string, TextSpan, bool> s_wordIsAllUpperCase = (val, span) =>
        {
            for (int i = span.Start, n = span.End; i < n; i++)
            {
                if (DoesCharacterHaveCasing(val[i]) && !char.IsUpper(val[i]))
                {
                    return false;
                }
            }

            return true;
        };

        private static readonly Func<string, TextSpan, bool> s_wordIsAllLowerCase = (val, span) =>
        {
            for (int i = span.Start, n = span.End; i < n; i++)
            {
                if (DoesCharacterHaveCasing(val[i]) && !char.IsLower(val[i]))
                {
                    return false;
                }
            }

            return true;
        };

        private bool CheckAllWords(
            string name, TextSpan nameSpan, Func<string, TextSpan, bool> wordCheck,
            string resourceId, out string reason)
        {
            reason = null;
            using var _ = ArrayBuilder<string>.GetInstance(out var violations);

            foreach (var wordSpan in GetWordSpans(name, nameSpan))
            {
                if (!wordCheck(name, wordSpan))
                {
                    violations.Add(Substring(name, wordSpan));
                }
            }

            if (violations.Count > 0)
            {
                reason = string.Format(resourceId, string.Join(", ", violations));
            }

            return reason == null;
        }

        private bool CheckPascalCase(string name, TextSpan nameSpan, out string reason)
            => CheckAllWords(
                name, nameSpan, s_firstCharIsUpperCase,
                CompilerExtensionsResources.These_words_must_begin_with_upper_case_characters_colon_0, out reason);

        private bool CheckAllUpper(string name, TextSpan nameSpan, out string reason)
            => CheckAllWords(
                name, nameSpan, s_wordIsAllUpperCase,
                CompilerExtensionsResources.These_words_cannot_contain_lower_case_characters_colon_0, out reason);

        private bool CheckAllLower(string name, TextSpan nameSpan, out string reason)
            => CheckAllWords(
                name, nameSpan, s_wordIsAllLowerCase,
                CompilerExtensionsResources.These_words_cannot_contain_upper_case_characters_colon_0, out reason);

        private bool CheckFirstAndRestWords(
            string name, TextSpan nameSpan,
            Func<string, TextSpan, bool> firstWordCheck,
            Func<string, TextSpan, bool> restWordCheck,
            string firstResourceId,
            string restResourceId,
            out string reason)
        {
            reason = null;
            using var _ = ArrayBuilder<string>.GetInstance(out var violations);

            var first = true;

            foreach (var wordSpan in GetWordSpans(name, nameSpan))
            {
                if (first)
                {
                    if (!firstWordCheck(name, wordSpan))
                    {
                        reason = string.Format(firstResourceId, Substring(name, wordSpan));
                    }
                }
                else
                {
                    if (!restWordCheck(name, wordSpan))
                    {
                        violations.Add(Substring(name, wordSpan));
                    }
                }

                first = false;
            }

            if (violations.Count > 0)
            {
                var restString = string.Format(restResourceId, string.Join(", ", violations));
                reason = reason == null
                    ? restString
                    : reason + Environment.NewLine + restString;
            }

            return reason == null;
        }

        private bool CheckCamelCase(string name, TextSpan nameSpan, out string reason)
            => CheckFirstAndRestWords(
                name, nameSpan, s_firstCharIsLowerCase, s_firstCharIsUpperCase,
                CompilerExtensionsResources.The_first_word_0_must_begin_with_a_lower_case_character,
                CompilerExtensionsResources.These_non_leading_words_must_begin_with_an_upper_case_letter_colon_0,
                out reason);

        private bool CheckFirstUpper(string name, TextSpan nameSpan, out string reason)
            => CheckFirstAndRestWords(
                name, nameSpan, s_firstCharIsUpperCase, s_firstCharIsLowerCase,
                CompilerExtensionsResources.The_first_word_0_must_begin_with_an_upper_case_character,
                CompilerExtensionsResources.These_non_leading_words_must_begin_with_a_lowercase_letter_colon_0,
                out reason);

        private static bool DoesCharacterHaveCasing(char c) => char.ToLower(c) != char.ToUpper(c);

        private string CreateCompliantNameDirectly(string name)
        {
            // Example: for specified prefix = "Test_" and name = "Test_m_BaseName", we remove "Test_m_"
            // "Test_" will be added back later in this method
            name = StripCommonPrefixes(name.StartsWith(Prefix) ? name.Substring(Prefix.Length) : name, out _);

            var addPrefix = !name.StartsWith(Prefix);
            var addSuffix = !name.EndsWith(Suffix);

            name = addPrefix ? (Prefix + name) : name;
            name = addSuffix ? (name + Suffix) : name;

            return FinishFixingName(name);
        }

        public IEnumerable<string> MakeCompliant(string name)
        {
            var name1 = CreateCompliantNameReusingPartialPrefixesAndSuffixes(name);
            yield return name1;

            var name2 = CreateCompliantNameDirectly(name);
            if (name2 != name1)
            {
                yield return name2;
            }
        }

        private string CreateCompliantNameReusingPartialPrefixesAndSuffixes(string name)
        {
            name = StripCommonPrefixes(name, out _);
            name = EnsurePrefix(name);
            name = EnsureSuffix(name);

            return FinishFixingName(name);
        }

        public static string StripCommonPrefixes(string name, out string prefix)
        {
            var index = 0;
            while (index + 1 < name.Length)
            {
                switch (char.ToLowerInvariant(name[index]))
                {
                    case 'm':
                    case 's':
                    case 't':
                        if (index + 2 < name.Length && name[index + 1] == '_')
                        {
                            index += 2;
                            continue;
                        }

                        break;

                    case '_':
                        index++;
                        continue;

                    default:
                        break;
                }

                // If we reach this point, the current iteration did not strip any additional characters
                break;
            }

            prefix = name.Substring(0, index);
            return name.Substring(index);
        }

        private string FinishFixingName(string name)
        {
            // Edge case: prefix "as", suffix "sa", name "asa"
            if (Suffix.Length + Prefix.Length >= name.Length)
            {
                return name;
            }

            name = name.Substring(Prefix.Length, name.Length - Suffix.Length - Prefix.Length);
            IEnumerable<string> words = new[] { name };

            if (!string.IsNullOrEmpty(WordSeparator))
            {
                words = name.Split(new[] { WordSeparator }, StringSplitOptions.RemoveEmptyEntries);

                // Edge case: the only character(s) in the name is(are) the WordSeparator
                if (words.Count() == 0)
                {
                    return name;
                }

                if (words.Count() == 1) // Only Split if words have not been split before 
                {
                    var isWord = true;
                    var parts = StringBreaker.GetParts(name, isWord);
                    var newWords = new string[parts.Count];
                    for (var i = 0; i < parts.Count; i++)
                    {
                        newWords[i] = name.Substring(parts[i].Start, parts[i].End - parts[i].Start);
                    }
                    words = newWords;
                }
            }

            words = ApplyCapitalization(words);

            return Prefix + string.Join(WordSeparator, words) + Suffix;
        }

        private string EnsureSuffix(string name)
        {
            // If the name already ends with any prefix of the Suffix, only append the suffix of
            // the Suffix not contained in the longest such Suffix prefix. For example, if the 
            // required suffix is "_catdog" and the name is "test_cat", then only append "dog".
            for (var i = Suffix.Length; i > 0; i--)
            {
                if (name.EndsWith(Suffix.Substring(0, i)))
                {
                    return name + Suffix.Substring(i);
                }
            }

            return name + Suffix;
        }

        private string EnsurePrefix(string name)
        {
            // If the name already starts with any suffix of the Prefix, only prepend the prefix of
            // the Prefix not contained in the longest such Prefix suffix. For example, if the 
            // required prefix is "catdog_" and the name is "dog_test", then only prepend "cat".
            for (var i = 0; i < Prefix.Length; i++)
            {
                if (name.StartsWith(Prefix.Substring(i)))
                {
                    return Prefix.Substring(0, i) + name;
                }
            }

            return Prefix + name;
        }

        internal XElement CreateXElement()
            => new XElement(nameof(NamingStyle),
                new XAttribute(nameof(ID), ID),
                new XAttribute(nameof(Name), Name),
                new XAttribute(nameof(Prefix), Prefix ?? string.Empty),
                new XAttribute(nameof(Suffix), Suffix ?? string.Empty),
                new XAttribute(nameof(WordSeparator), WordSeparator ?? string.Empty),
                new XAttribute(nameof(CapitalizationScheme), CapitalizationScheme));

        internal static NamingStyle FromXElement(XElement namingStyleElement)
            => new NamingStyle(
                id: Guid.Parse(namingStyleElement.Attribute(nameof(ID)).Value),
                name: namingStyleElement.Attribute(nameof(Name)).Value,
                prefix: namingStyleElement.Attribute(nameof(Prefix)).Value,
                suffix: namingStyleElement.Attribute(nameof(Suffix)).Value,
                wordSeparator: namingStyleElement.Attribute(nameof(WordSeparator)).Value,
                capitalizationScheme: (Capitalization)Enum.Parse(typeof(Capitalization), namingStyleElement.Attribute(nameof(CapitalizationScheme)).Value));
    }
}
