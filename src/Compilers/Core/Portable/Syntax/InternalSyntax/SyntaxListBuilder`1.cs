﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.Syntax.InternalSyntax
{
    internal struct SyntaxListBuilder<TNode> where TNode : GreenNode
    {
        private readonly SyntaxListBuilder _builder;

        public SyntaxListBuilder(int size)
            : this(new SyntaxListBuilder(size))
        {
        }

        public static SyntaxListBuilder<TNode> Create()
        {
            return new SyntaxListBuilder<TNode>(8);
        }

        internal SyntaxListBuilder(SyntaxListBuilder builder)
        {
            _builder = builder;
        }

        public bool IsNull
        {
            get
            {
                return _builder == null;
            }
        }

        public int Count
        {
            get
            {
                return _builder.Count;
            }
        }

        public TNode? this[int index]
        {
            get
            {
                return (TNode?)_builder[index];
            }

            set
            {
                _builder[index] = value;
            }
        }

        public void Clear()
        {
            _builder.Clear();
        }

        /// <summary>
        /// Adds <paramref name="node"/> to the end of this builder.  No change happens if <see langword="null"/> is
        /// passed in.
        /// </summary>
        public SyntaxListBuilder<TNode> Add(TNode? node)
        {
            _builder.Add(node);
            return this;
        }

        public void AddRange(TNode[] items, int offset, int length)
        {
            _builder.AddRange(items, offset, length);
        }

        public void AddRange(SyntaxList<TNode> nodes)
        {
            _builder.AddRange(nodes);
        }

        public void AddRange(SyntaxList<TNode> nodes, int offset, int length)
        {
            _builder.AddRange(nodes, offset, length);
        }

        public bool Any(int kind)
        {
            return _builder.Any(kind);
        }

        public SyntaxList<TNode> ToList()
        {
            return _builder.ToList();
        }

        public GreenNode? ToListNode()
        {
            return _builder.ToListNode();
        }

        public static implicit operator SyntaxListBuilder(SyntaxListBuilder<TNode> builder)
        {
            return builder._builder;
        }

        public static implicit operator SyntaxList<TNode>(SyntaxListBuilder<TNode> builder)
        {
            if (builder._builder != null)
            {
                return builder.ToList();
            }

            return default(SyntaxList<TNode>);
        }

        public SyntaxList<TDerived> ToList<TDerived>() where TDerived : GreenNode
        {
            return new SyntaxList<TDerived>(ToListNode());
        }
    }
}
