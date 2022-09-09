﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

// We only support grammar generation in the command line version for now which is the netcoreapp target
#if NETCOREAPP

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace CSharpSyntaxGenerator.Grammar
{
    internal static class GrammarGenerator
    {
        public static string Run(List<TreeType> types)
        {
            // Syntax.xml refers to a special pseudo-element 'Modifier'.  Synthesize that for the grammar.
            var modifiers = GetMembers<DeclarationModifiers>()
                .Select(m => m + "Keyword").Where(n => GetSyntaxKind(n) != SyntaxKind.None)
                .Select(n => new Kind { Name = n }).ToList();

            types.Add(new Node { Name = "Modifier", Children = { new Field { Type = "SyntaxToken", Kinds = modifiers } } });

            var rules = types.ToDictionary(n => n.Name, _ => new List<Production>());
            foreach (var type in types)
            {
                if (type.Base != null && rules.TryGetValue(type.Base, out var productions))
                    productions.Add(RuleReference(type.Name));

                if (type is Node && type.Children.Count > 0)
                {
                    // Convert rules like `a: (x | y) ...` into:
                    //
                    // a: x ...
                    //  | y ...;
                    //
                    // Note: if we have `a: (a1 | b1) ... (ax | bx) presume that that's a paired construct and generate:
                    //
                    // a: a1 ... ax
                    //  | b1 ... bx;

                    if (type.Children.First() is Field firstField && firstField.Kinds.Count > 0)
                    {
                        var originalFirstFieldKinds = firstField.Kinds.ToList();
                        if (type.Children.Count >= 2 && type.Children.Last() is Field lastField && lastField.Kinds.Count == firstField.Kinds.Count)
                        {
                            var originalLastFieldKinds = lastField.Kinds.ToList();
                            for (int i = 0; i < originalFirstFieldKinds.Count; i++)
                            {
                                firstField.Kinds = new List<Kind> { originalFirstFieldKinds[i] };
                                lastField.Kinds = new List<Kind> { originalLastFieldKinds[i] };
                                rules[type.Name].Add(HandleChildren(type.Children));
                            }
                        }
                        else
                        {
                            for (int i = 0; i < originalFirstFieldKinds.Count; i++)
                            {
                                firstField.Kinds = new List<Kind> { originalFirstFieldKinds[i] };
                                rules[type.Name].Add(HandleChildren(type.Children));
                            }
                        }
                    }
                    else
                    {
                        rules[type.Name].Add(HandleChildren(type.Children));
                    }
                }
            }

            // The grammar will bottom out with certain lexical productions. Create rules for these.
            var lexicalRules = rules.Values.SelectMany(ps => ps).SelectMany(p => p.ReferencedRules)
                .Where(r => !rules.TryGetValue(r, out var productions) || productions.Count == 0).ToArray();
            foreach (var name in lexicalRules)
                rules[name] = new List<Production> { new Production("/* see lexical specification */") };

            var seen = new HashSet<string>();

            // Define a few major sections to help keep the grammar file naturally grouped.
            var majorRules = ImmutableArray.Create(
                "CompilationUnitSyntax", "MemberDeclarationSyntax", "TypeSyntax", "StatementSyntax", "ExpressionSyntax", "XmlNodeSyntax", "StructuredTriviaSyntax");

            var result = "// <auto-generated />" + Environment.NewLine + "grammar csharp;" + Environment.NewLine;

            // Handle each major section first and then walk any rules not hit transitively from them.
            foreach (var rule in majorRules.Concat(rules.Keys.OrderBy(a => a)))
                processRule(rule, ref result);

            return result;

            void processRule(string name, ref string result)
            {
                if (name != "CSharpSyntaxNode" && seen.Add(name))
                {
                    // Order the productions to keep us independent from whatever changes happen in Syntax.xml.
                    var sorted = rules[name].OrderBy(v => v);
                    result += Environment.NewLine + RuleReference(name).Text + Environment.NewLine + "  : " +
                                string.Join(Environment.NewLine + "  | ", sorted) + Environment.NewLine + "  ;" + Environment.NewLine;

                    // Now proceed in depth-first fashion through the referenced rules to keep related rules
                    // close by. Don't recurse into major-sections to help keep them separated in grammar file.
                    foreach (var production in sorted)
                        foreach (var referencedRule in production.ReferencedRules)
                            if (!majorRules.Concat(lexicalRules).Contains(referencedRule))
                                processRule(referencedRule, ref result);
                }
            }
        }

        private static Production Join(string delim, IEnumerable<Production> productions)
            => new Production(string.Join(delim, productions.Where(p => p.Text.Length > 0)), productions.SelectMany(p => p.ReferencedRules));

        private static Production HandleChildren(IEnumerable<TreeTypeChild> children, string delim = " ")
            => Join(delim, children.Select(child =>
                child is Choice c ? HandleChildren(c.Children, delim: " | ").Parenthesize().Suffix("?", when: c.Optional) :
                child is Sequence s ? HandleChildren(s.Children).Parenthesize() :
                child is Field f ? HandleField(f).Suffix("?", when: f.IsOptional) : throw new InvalidOperationException()));

        private static Production HandleField(Field field)
            // 'bool' fields are for a few properties we generate on DirectiveTrivia. They're not
            // relevant to the grammar, so we just return an empty production to ignore them.
            => field.Type == "bool" ? new Production("") :
               field.Type == "CSharpSyntaxNode" ? RuleReference(field.Kinds.Single().Name + "Syntax") :
               field.Type.StartsWith("SeparatedSyntaxList") ? HandleSeparatedList(field, field.Type[("SeparatedSyntaxList".Length + 1)..^1]) :
               field.Type.StartsWith("SyntaxList") ? HandleList(field, field.Type[("SyntaxList".Length + 1)..^1]) :
               field.IsToken ? HandleTokenField(field) : RuleReference(field.Type);

        private static Production HandleSeparatedList(Field field, string elementType)
            => RuleReference(elementType).Suffix(" (',' " + RuleReference(elementType) + ")")
                .Suffix("*", when: field.MinCount < 2).Suffix("+", when: field.MinCount >= 2)
                .Suffix(" ','?", when: field.AllowTrailingSeparator)
                .Parenthesize(when: field.MinCount == 0).Suffix("?", when: field.MinCount == 0);

        private static Production HandleList(Field field, string elementType)
            => (elementType != "SyntaxToken" ? RuleReference(elementType) :
                field.Name == "Commas" ? new Production("','") :
                field.Name == "Modifiers" ? RuleReference("Modifier") :
                field.Name == "TextTokens" ? RuleReference(nameof(SyntaxKind.XmlTextLiteralToken)) : RuleReference(elementType))
                    .Suffix(field.MinCount == 0 ? "*" : "+");

        private static Production HandleTokenField(Field field)
            => field.Kinds.Count == 0
                ? HandleTokenName(field.Name)
                : Join(" | ", field.Kinds.Select(k => HandleTokenName(k.Name))).Parenthesize(when: field.Kinds.Count >= 2);

        private static Production HandleTokenName(string tokenName)
            => GetSyntaxKind(tokenName) is var kind && kind == SyntaxKind.None ? RuleReference("SyntaxToken") :
               SyntaxFacts.GetText(kind) is var text && text != "" ? new Production(text == "'" ? "'\\''" : $"'{text}'") :
               tokenName.StartsWith("EndOf") ? new Production("") :
               tokenName.StartsWith("Omitted") ? new Production("/* epsilon */") : RuleReference(tokenName);

        private static SyntaxKind GetSyntaxKind(string name)
            => GetMembers<SyntaxKind>().Where(k => k.ToString() == name).SingleOrDefault();

        private static IEnumerable<TEnum> GetMembers<TEnum>() where TEnum : struct, Enum
            => (IEnumerable<TEnum>)Enum.GetValues(typeof(TEnum));

        private static Production RuleReference(string name)
            => new Production(
                s_normalizationRegex.Replace(name.EndsWith("Syntax") ? name[..^"Syntax".Length] : name, "_").ToLower(),
                ImmutableArray.Create(name));

        // Converts a PascalCased name into snake_cased name.
        private static readonly Regex s_normalizationRegex = new Regex(
            "(?<=[A-Z])(?=[A-Z][a-z]) | (?<=[^A-Z])(?=[A-Z]) | (?<=[A-Za-z])(?=[^A-Za-z])",
            RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);
    }

    internal readonly struct Production : IComparable<Production>
    {
        public readonly string Text;
        public readonly ImmutableArray<string> ReferencedRules;

        public Production(string text, IEnumerable<string> referencedRules = null)
        {
            Text = text;
            ReferencedRules = referencedRules?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
        }

        public override string ToString() => Text;
        public int CompareTo(Production other) => StringComparer.Ordinal.Compare(this.Text, other.Text);
        public Production Prefix(string prefix) => new Production(prefix + this, ReferencedRules);
        public Production Suffix(string suffix, bool when = true) => when ? new Production(this + suffix, ReferencedRules) : this;
        public Production Parenthesize(bool when = true) => when ? Prefix("(").Suffix(")") : this;
    }
}

namespace Microsoft.CodeAnalysis
{
    internal static class GreenNode
    {
        internal const int ListKind = 1; // See SyntaxKind.
    }
}

#endif
