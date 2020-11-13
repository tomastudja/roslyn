﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// A wrapper for either a syntax node (<see cref="SyntaxNode"/>) or a syntax token (<see
    /// cref="SyntaxToken"/>).
    /// </summary>
    /// <remarks>
    /// Note that we do not store the token directly, we just store enough information to reconstruct it.
    /// This allows us to reuse nodeOrToken as a token's parent.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
    public readonly struct SyntaxNodeOrToken : IEquatable<SyntaxNodeOrToken>
    {
        // In a case if we are wrapping a SyntaxNode this is the SyntaxNode itself.
        // In a case where we are wrapping a token, this is the token's parent.
        private readonly SyntaxNode? _nodeOrParent;

        // Green node for the token. 
        private readonly GreenNode? _token;

        // Used in both node and token cases.
        // When we have a node, _position == _nodeOrParent.Position.
        private readonly int _position;

        // Index of the token among parent's children. 
        // This field only makes sense if this is a token.
        // For regular nodes it is set to -1 to distinguish from default(SyntaxToken)
        private readonly int _tokenIndex;

        internal SyntaxNodeOrToken(SyntaxNode node)
            : this()
        {
            Debug.Assert(!node.Green.IsList, "node cannot be a list");
            _position = node.Position;
            _nodeOrParent = node;
            _tokenIndex = -1;
        }

        internal SyntaxNodeOrToken(SyntaxNode? parent, GreenNode? token, int position, int index)
        {
            Debug.Assert(parent == null || !parent.Green.IsList, "parent cannot be a list");
            Debug.Assert(token != null || (parent == null && position == 0 && index == 0), "parts must form a token");
            Debug.Assert(token == null || token.IsToken, "token must be a token");
            Debug.Assert(index >= 0, "index must not be negative");
            Debug.Assert(parent == null || token != null, "null token cannot have parent");

            _position = position;
            _tokenIndex = index;
            _nodeOrParent = parent;
            _token = token;
        }

        internal string GetDebuggerDisplay()
        {
            return GetType().Name + " " + KindText + " " + ToString();
        }

        private string KindText
        {
            get
            {
                if (_token != null)
                {
                    return _token.KindText;
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.Green.KindText;
                }

                return "None";
            }
        }

        /// <summary>
        /// An integer representing the language specific kind of the underlying node or token.
        /// </summary>
        public int RawKind => _token?.RawKind ?? _nodeOrParent?.RawKind ?? 0;

        /// <summary>
        /// The language name that this node or token is syntax of.
        /// </summary>
        public string Language
        {
            get
            {
                if (_token != null)
                {
                    return _token.Language;
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.Language;
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Determines whether the underlying node or token represents a language construct that was actually parsed
        /// from source code. Missing nodes and tokens are typically generated by the parser in error scenarios to
        /// represent constructs that should have been present in the source code for the source code to compile
        /// successfully but were actually missing.
        /// </summary>
        public bool IsMissing => _token?.IsMissing ?? _nodeOrParent?.IsMissing ?? false;

        /// <summary>
        /// The node that contains the underlying node or token in its Children collection.
        /// </summary>
        public SyntaxNode? Parent => _token != null ? _nodeOrParent : _nodeOrParent?.Parent;

        internal GreenNode? UnderlyingNode => _token ?? _nodeOrParent?.Green;

        internal int Position => _position;

        internal GreenNode RequiredUnderlyingNode
        {
            get
            {
                Debug.Assert(UnderlyingNode is not null);
                return UnderlyingNode;
            }
        }

        /// <summary>
        /// Determines whether this <see cref="SyntaxNodeOrToken"/> is wrapping a token.
        /// </summary>
        public bool IsToken => !IsNode;

        /// <summary>
        /// Determines whether this <see cref="SyntaxNodeOrToken"/> is wrapping a node.
        /// </summary>
        public bool IsNode => _tokenIndex < 0;

        /// <summary>
        /// Returns the underlying token if this <see cref="SyntaxNodeOrToken"/> is wrapping a
        /// token.
        /// </summary>
        /// <returns>
        /// The underlying token if this <see cref="SyntaxNodeOrToken"/> is wrapping a token.
        /// </returns>
        public SyntaxToken AsToken()
        {
            if (_token != null)
            {
                return new SyntaxToken(_nodeOrParent, _token, this.Position, _tokenIndex);
            }

            return default(SyntaxToken);
        }

        internal bool AsToken(out SyntaxToken token)
        {
            if (IsToken)
            {
                token = AsToken()!;
                return true;
            }

            token = default;
            return false;
        }

        /// <summary>
        /// Returns the underlying node if this <see cref="SyntaxNodeOrToken"/> is wrapping a
        /// node.
        /// </summary>
        /// <returns>
        /// The underlying node if this <see cref="SyntaxNodeOrToken"/> is wrapping a node.
        /// </returns>
        public SyntaxNode? AsNode()
        {
            if (_token != null)
            {
                return null;
            }

            return _nodeOrParent;
        }

        internal bool AsNode([NotNullWhen(true)] out SyntaxNode? node)
        {
            if (IsNode)
            {
                node = _nodeOrParent;
                return node is object;
            }

            node = null;
            return false;
        }

        /// <summary>
        /// The list of child nodes and tokens of the underlying node or token.
        /// </summary>
        public ChildSyntaxList ChildNodesAndTokens()
        {
            if (AsNode(out var node))
            {
                return node.ChildNodesAndTokens();
            }

            return default;
        }

        /// <summary>
        /// The absolute span of the underlying node or token in characters, not including its leading and trailing
        /// trivia.
        /// </summary>
        public TextSpan Span
        {
            get
            {
                if (_token != null)
                {
                    return this.AsToken().Span;
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.Span;
                }

                return default(TextSpan);
            }
        }

        /// <summary>
        /// Same as accessing <see cref="TextSpan.Start"/> on <see cref="Span"/>.
        /// </summary>
        /// <remarks>
        /// Slight performance improvement.
        /// </remarks>
        public int SpanStart
        {
            get
            {
                if (_token != null)
                {
                    // PERF: Inlined "this.AsToken().SpanStart"
                    return _position + _token.GetLeadingTriviaWidth();
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.SpanStart;
                }

                return 0; //default(TextSpan).Start
            }
        }

        /// <summary>
        /// The absolute span of the underlying node or token in characters, including its leading and trailing trivia.
        /// </summary>
        public TextSpan FullSpan
        {
            get
            {
                if (_token != null)
                {
                    return new TextSpan(Position, _token.FullWidth);
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.FullSpan;
                }

                return default(TextSpan);
            }
        }

        /// <summary>
        /// Returns the string representation of this node or token, not including its leading and trailing
        /// trivia.
        /// </summary>
        /// <returns>
        /// The string representation of this node or token, not including its leading and trailing trivia.
        /// </returns>
        /// <remarks>The length of the returned string is always the same as Span.Length</remarks>
        public override string ToString()
        {
            if (_token != null)
            {
                return _token.ToString();
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the full string representation of this node or token including its leading and trailing trivia.
        /// </summary>
        /// <returns>The full string representation of this node or token including its leading and trailing
        /// trivia.</returns>
        /// <remarks>The length of the returned string is always the same as FullSpan.Length</remarks>
        public string ToFullString()
        {
            if (_token != null)
            {
                return _token.ToFullString();
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.ToFullString();
            }

            return string.Empty;
        }

        /// <summary>
        /// Writes the full text of this node or token to the specified TextWriter.
        /// </summary>
        public void WriteTo(System.IO.TextWriter writer)
        {
            if (_token != null)
            {
                _token.WriteTo(writer);
            }
            else
            {
                _nodeOrParent?.WriteTo(writer);
            }
        }

        /// <summary>
        /// Determines whether the underlying node or token has any leading trivia.
        /// </summary>
        public bool HasLeadingTrivia => this.GetLeadingTrivia().Count > 0;

        /// <summary>
        /// The list of trivia that appear before the underlying node or token in the source code and are attached to a
        /// token that is a descendant of the underlying node or token.
        /// </summary>
        public SyntaxTriviaList GetLeadingTrivia()
        {
            if (_token != null)
            {
                return this.AsToken().LeadingTrivia;
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.GetLeadingTrivia();
            }

            return default(SyntaxTriviaList);
        }

        /// <summary>
        /// Determines whether the underlying node or token has any trailing trivia.
        /// </summary>
        public bool HasTrailingTrivia => this.GetTrailingTrivia().Count > 0;

        /// <summary>
        /// The list of trivia that appear after the underlying node or token in the source code and are attached to a
        /// token that is a descendant of the underlying node or token.
        /// </summary>
        public SyntaxTriviaList GetTrailingTrivia()
        {
            if (_token != null)
            {
                return this.AsToken().TrailingTrivia;
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.GetTrailingTrivia();
            }

            return default(SyntaxTriviaList);
        }

        public SyntaxNodeOrToken WithLeadingTrivia(IEnumerable<SyntaxTrivia> trivia)
        {
            if (_token != null)
            {
                return AsToken().WithLeadingTrivia(trivia);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.WithLeadingTrivia(trivia);
            }

            return this;
        }

        public SyntaxNodeOrToken WithLeadingTrivia(params SyntaxTrivia[] trivia)
        {
            return WithLeadingTrivia((IEnumerable<SyntaxTrivia>)trivia);
        }

        public SyntaxNodeOrToken WithTrailingTrivia(IEnumerable<SyntaxTrivia> trivia)
        {
            if (_token != null)
            {
                return AsToken().WithTrailingTrivia(trivia);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.WithTrailingTrivia(trivia);
            }

            return this;
        }

        public SyntaxNodeOrToken WithTrailingTrivia(params SyntaxTrivia[] trivia)
        {
            return WithTrailingTrivia((IEnumerable<SyntaxTrivia>)trivia);
        }

        /// <summary>
        /// Determines whether the underlying node or token or any of its descendant nodes, tokens or trivia have any
        /// diagnostics on them. 
        /// </summary>
        public bool ContainsDiagnostics
        {
            get
            {
                if (_token != null)
                {
                    return _token.ContainsDiagnostics;
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.ContainsDiagnostics;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets a list of all the diagnostics in either the sub tree that has this node as its root or
        /// associated with this token and its related trivia. 
        /// This method does not filter diagnostics based on #pragmas and compiler options
        /// like nowarn, warnaserror etc.
        /// </summary>
        public IEnumerable<Diagnostic> GetDiagnostics()
        {
            if (_token != null)
            {
                return this.AsToken().GetDiagnostics();
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.GetDiagnostics();
            }

            return SpecializedCollections.EmptyEnumerable<Diagnostic>();
        }


        /// <summary>
        /// Determines whether the underlying node or token has any descendant preprocessor directives.
        /// </summary>
        public bool ContainsDirectives
        {
            get
            {
                if (_token != null)
                {
                    return _token.ContainsDirectives;
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.ContainsDirectives;
                }

                return false;
            }
        }

        #region Annotations 
        /// <summary>
        /// Determines whether this node or token (or any sub node, token or trivia) as annotations.
        /// </summary>
        public bool ContainsAnnotations
        {
            get
            {
                if (_token != null)
                {
                    return _token.ContainsAnnotations;
                }

                if (_nodeOrParent != null)
                {
                    return _nodeOrParent.ContainsAnnotations;
                }

                return false;
            }
        }

        /// <summary>
        /// Determines whether this node or token has annotations of the specified kind.
        /// </summary>
        public bool HasAnnotations(string annotationKind)
        {
            if (_token != null)
            {
                return _token.HasAnnotations(annotationKind);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.HasAnnotations(annotationKind);
            }

            return false;
        }

        /// <summary>
        /// Determines whether this node or token has annotations of the specified kind.
        /// </summary>
        public bool HasAnnotations(IEnumerable<string> annotationKinds)
        {
            if (_token != null)
            {
                return _token.HasAnnotations(annotationKinds);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.HasAnnotations(annotationKinds);
            }

            return false;
        }

        /// <summary>
        /// Determines if this node or token has the specific annotation.
        /// </summary>
        public bool HasAnnotation([NotNullWhen(true)] SyntaxAnnotation? annotation)
        {
            if (_token != null)
            {
                return _token.HasAnnotation(annotation);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.HasAnnotation(annotation);
            }

            return false;
        }

        /// <summary>
        /// Gets all annotations of the specified annotation kind.
        /// </summary>
        public IEnumerable<SyntaxAnnotation> GetAnnotations(string annotationKind)
        {
            if (_token != null)
            {
                return _token.GetAnnotations(annotationKind);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.GetAnnotations(annotationKind);
            }

            return SpecializedCollections.EmptyEnumerable<SyntaxAnnotation>();
        }

        /// <summary>
        /// Gets all annotations of the specified annotation kind.
        /// </summary>
        public IEnumerable<SyntaxAnnotation> GetAnnotations(IEnumerable<string> annotationKinds)
        {
            if (_token != null)
            {
                return _token.GetAnnotations(annotationKinds);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.GetAnnotations(annotationKinds);
            }

            return SpecializedCollections.EmptyEnumerable<SyntaxAnnotation>();
        }

        /// <summary>
        /// Creates a new node or token identical to this one with the specified annotations.
        /// </summary>
        public SyntaxNodeOrToken WithAdditionalAnnotations(params SyntaxAnnotation[] annotations)
        {
            return WithAdditionalAnnotations((IEnumerable<SyntaxAnnotation>)annotations);
        }

        /// <summary>
        /// Creates a new node or token identical to this one with the specified annotations.
        /// </summary>
        public SyntaxNodeOrToken WithAdditionalAnnotations(IEnumerable<SyntaxAnnotation> annotations)
        {
            if (annotations == null)
            {
                throw new ArgumentNullException(nameof(annotations));
            }

            if (_token != null)
            {
                return this.AsToken().WithAdditionalAnnotations(annotations);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.WithAdditionalAnnotations(annotations);
            }

            return this;
        }

        /// <summary>
        /// Creates a new node or token identical to this one without the specified annotations.
        /// </summary>
        public SyntaxNodeOrToken WithoutAnnotations(params SyntaxAnnotation[] annotations)
        {
            return WithoutAnnotations((IEnumerable<SyntaxAnnotation>)annotations);
        }

        /// <summary>
        /// Creates a new node or token identical to this one without the specified annotations.
        /// </summary>
        public SyntaxNodeOrToken WithoutAnnotations(IEnumerable<SyntaxAnnotation> annotations)
        {
            if (annotations == null)
            {
                throw new ArgumentNullException(nameof(annotations));
            }

            if (_token != null)
            {
                return this.AsToken().WithoutAnnotations(annotations);
            }

            if (_nodeOrParent != null)
            {
                return _nodeOrParent.WithoutAnnotations(annotations);
            }

            return this;
        }

        /// <summary>
        /// Creates a new node or token identical to this one without annotations of the specified kind.
        /// </summary>
        public SyntaxNodeOrToken WithoutAnnotations(string annotationKind)
        {
            if (annotationKind == null)
            {
                throw new ArgumentNullException(nameof(annotationKind));
            }

            if (this.HasAnnotations(annotationKind))
            {
                return this.WithoutAnnotations(this.GetAnnotations(annotationKind));
            }

            return this;
        }

        #endregion

        /// <summary>
        /// Determines whether the supplied <see cref="SyntaxNodeOrToken"/> is equal to this
        /// <see cref="SyntaxNodeOrToken"/>.
        /// </summary>
        public bool Equals(SyntaxNodeOrToken other)
        {
            // index replaces position to ensure equality.  Assert if offset affects equality.
            Debug.Assert(
                (_nodeOrParent == other._nodeOrParent && _token == other._token && _position == other._position && _tokenIndex == other._tokenIndex) ==
                (_nodeOrParent == other._nodeOrParent && _token == other._token && _tokenIndex == other._tokenIndex));

            return _nodeOrParent == other._nodeOrParent &&
                   _token == other._token &&
                   _tokenIndex == other._tokenIndex;
        }

        /// <summary>
        /// Determines whether two <see cref="SyntaxNodeOrToken"/>s are equal.
        /// </summary>
        public static bool operator ==(SyntaxNodeOrToken left, SyntaxNodeOrToken right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two <see cref="SyntaxNodeOrToken"/>s are unequal.
        /// </summary>
        public static bool operator !=(SyntaxNodeOrToken left, SyntaxNodeOrToken right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether the supplied <see cref="SyntaxNodeOrToken"/> is equal to this
        /// <see cref="SyntaxNodeOrToken"/>.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is SyntaxNodeOrToken token && Equals(token);
        }

        /// <summary>
        /// Serves as hash function for <see cref="SyntaxNodeOrToken"/>.
        /// </summary>
        public override int GetHashCode()
        {
            return Hash.Combine(_nodeOrParent, Hash.Combine(_token, _tokenIndex));
        }

        /// <summary>
        /// Determines if the two nodes or tokens are equivalent.
        /// </summary>
        public bool IsEquivalentTo(SyntaxNodeOrToken other)
        {
            if (this.IsNode != other.IsNode)
            {
                return false;
            }

            var thisUnderlying = this.UnderlyingNode;
            var otherUnderlying = other.UnderlyingNode;

            return (thisUnderlying == otherUnderlying) || (thisUnderlying != null && thisUnderlying.IsEquivalentTo(otherUnderlying));
        }

        /// <summary>
        /// Returns a new <see cref="SyntaxNodeOrToken"/> that wraps the supplied token.
        /// </summary>
        /// <param name="token">The input token.</param>
        /// <returns>
        /// A <see cref="SyntaxNodeOrToken"/> that wraps the supplied token.
        /// </returns>
        public static implicit operator SyntaxNodeOrToken(SyntaxToken token)
        {
            return new SyntaxNodeOrToken(token.Parent, token.Node, token.Position, token.Index);
        }

        /// <summary>
        /// Returns the underlying token wrapped by the supplied <see cref="SyntaxNodeOrToken"/>.
        /// </summary>
        /// <param name="nodeOrToken">
        /// The input <see cref="SyntaxNodeOrToken"/>.
        /// </param>
        /// <returns>
        /// The underlying token wrapped by the supplied <see cref="SyntaxNodeOrToken"/>.
        /// </returns>
        public static explicit operator SyntaxToken(SyntaxNodeOrToken nodeOrToken)
        {
            return nodeOrToken.AsToken();
        }

        /// <summary>
        /// Returns a new <see cref="SyntaxNodeOrToken"/> that wraps the supplied node.
        /// </summary>
        /// <param name="node">The input node.</param>
        /// <returns>
        /// A <see cref="SyntaxNodeOrToken"/> that wraps the supplied node.
        /// </returns>
        public static implicit operator SyntaxNodeOrToken(SyntaxNode? node)
        {
            return node is object
                ? new SyntaxNodeOrToken(node)
                : default;
        }

        /// <summary>
        /// Returns the underlying node wrapped by the supplied <see cref="SyntaxNodeOrToken"/>.
        /// </summary>
        /// <param name="nodeOrToken">
        /// The input <see cref="SyntaxNodeOrToken"/>.
        /// </param>
        /// <returns>
        /// The underlying node wrapped by the supplied <see cref="SyntaxNodeOrToken"/>.
        /// </returns>
        public static explicit operator SyntaxNode?(SyntaxNodeOrToken nodeOrToken)
        {
            return nodeOrToken.AsNode();
        }

        /// <summary>
        /// SyntaxTree which contains current SyntaxNodeOrToken.
        /// </summary>
        public SyntaxTree? SyntaxTree => _nodeOrParent?.SyntaxTree;

        /// <summary>
        /// Get the location of this node or token.
        /// </summary>
        public Location? GetLocation()
        {
            if (AsToken(out var token))
            {
                return token.GetLocation();
            }

            return _nodeOrParent?.GetLocation();
        }

        #region Directive Lookup

        // Get all directives under the node and its children in source code order.
        internal IList<TDirective> GetDirectives<TDirective>(Func<TDirective, bool>? filter = null)
            where TDirective : SyntaxNode
        {
            List<TDirective>? directives = null;
            GetDirectives(this, filter, ref directives);
            return directives ?? SpecializedCollections.EmptyList<TDirective>();
        }

        private static void GetDirectives<TDirective>(in SyntaxNodeOrToken node, Func<TDirective, bool>? filter, ref List<TDirective>? directives)
            where TDirective : SyntaxNode
        {
            if (node._token != null && node.AsToken() is var token && token.ContainsDirectives)
            {
                GetDirectives(token.LeadingTrivia, filter, ref directives);
                GetDirectives(token.TrailingTrivia, filter, ref directives);
            }
            else if (node._nodeOrParent != null)
            {
                GetDirectives(node._nodeOrParent, filter, ref directives);
            }
        }

        private static void GetDirectives<TDirective>(SyntaxNode node, Func<TDirective, bool>? filter, ref List<TDirective>? directives)
            where TDirective : SyntaxNode
        {
            foreach (var trivia in node.DescendantTrivia(node => node.ContainsDirectives, descendIntoTrivia: true))
            {
                _ = GetDirectivesInTrivia(trivia, filter, ref directives);
            }
        }

        private static bool GetDirectivesInTrivia<TDirective>(in SyntaxTrivia trivia, Func<TDirective, bool>? filter, ref List<TDirective>? directives)
            where TDirective : SyntaxNode
        {
            if (trivia.IsDirective)
            {
                if (trivia.GetStructure() is TDirective directive &&
                    filter?.Invoke(directive) != false)
                {
                    if (directives == null)
                    {
                        directives = new List<TDirective>();
                    }

                    directives.Add(directive);
                }

                return true;
            }
            return false;
        }

        private static void GetDirectives<TDirective>(in SyntaxTriviaList trivia, Func<TDirective, bool>? filter, ref List<TDirective>? directives)
            where TDirective : SyntaxNode
        {
            foreach (var tr in trivia)
            {
                if (!GetDirectivesInTrivia(tr, filter, ref directives) && tr.GetStructure() is SyntaxNode node)
                {
                    GetDirectives(node, filter, ref directives);
                }
            }
        }

        #endregion

        internal int Width => _token?.Width ?? _nodeOrParent?.Width ?? 0;

        internal int FullWidth => _token?.FullWidth ?? _nodeOrParent?.FullWidth ?? 0;

        internal int EndPosition => _position + this.FullWidth;

        public static int GetFirstChildIndexSpanningPosition(SyntaxNode node, int position)
        {
            if (!node.FullSpan.IntersectsWith(position))
            {
                throw new ArgumentException("Must be within node's FullSpan", nameof(position));
            }

            return GetFirstChildIndexSpanningPosition(node.ChildNodesAndTokens(), position);
        }

        internal static int GetFirstChildIndexSpanningPosition(ChildSyntaxList list, int position)
        {
            int lo = 0;
            int hi = list.Count - 1;
            while (lo <= hi)
            {
                int r = lo + ((hi - lo) >> 1);

                var m = list[r];
                if (position < m.Position)
                {
                    hi = r - 1;
                }
                else
                {
                    if (position == m.Position)
                    {
                        // If we hit a zero width node, move left to the first such node (or the
                        // first one in the list)
                        for (; r > 0 && list[r - 1].FullWidth == 0; r--)
                        {
                            ;
                        }

                        return r;
                    }

                    if (position >= m.EndPosition)
                    {
                        lo = r + 1;
                        continue;
                    }

                    return r;
                }
            }

            throw ExceptionUtilities.Unreachable;
        }

        public SyntaxNodeOrToken GetNextSibling()
        {
            var parent = this.Parent;
            if (parent == null)
            {
                return default(SyntaxNodeOrToken);
            }

            var siblings = parent.ChildNodesAndTokens();

            return siblings.Count < 8
                ? GetNextSiblingFromStart(siblings)
                : GetNextSiblingWithSearch(siblings);
        }

        public SyntaxNodeOrToken GetPreviousSibling()
        {
            if (this.Parent != null)
            {
                // walk reverse in parent's child list until we find ourself 
                // and then return the next child
                var returnNext = false;
                foreach (var child in this.Parent.ChildNodesAndTokens().Reverse())
                {
                    if (returnNext)
                    {
                        return child;
                    }

                    if (child == this)
                    {
                        returnNext = true;
                    }
                }
            }

            return default(SyntaxNodeOrToken);
        }

        private SyntaxNodeOrToken GetNextSiblingFromStart(ChildSyntaxList siblings)
        {
            var returnNext = false;
            foreach (var sibling in siblings)
            {
                if (returnNext)
                {
                    return sibling;
                }

                if (sibling == this)
                {
                    returnNext = true;
                }
            }

            return default(SyntaxNodeOrToken);
        }

        private SyntaxNodeOrToken GetNextSiblingWithSearch(ChildSyntaxList siblings)
        {
            var firstIndex = GetFirstChildIndexSpanningPosition(siblings, _position);

            var count = siblings.Count;
            var returnNext = false;

            for (int i = firstIndex; i < count; i++)
            {
                if (returnNext)
                {
                    return siblings[i];
                }

                if (siblings[i] == this)
                {
                    returnNext = true;
                }
            }

            return default(SyntaxNodeOrToken);
        }
    }
}
