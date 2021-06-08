﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    partial class BoundDagEvaluation
    {
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is BoundDagEvaluation other && this.Equals(other);
        public virtual bool Equals(BoundDagEvaluation other)
        {
            return this == other ||
                this.Kind == other.Kind &&
                this.Input.Equals(other.Input) &&
                Symbol.Equals(this.Symbol, other.Symbol, TypeCompareKind.AllIgnoreOptions);
        }

        private Symbol? Symbol
        {
            get
            {
                return this switch
                {
                    BoundDagFieldEvaluation e => e.Field.CorrespondingTupleField ?? e.Field,
                    BoundDagPropertyEvaluation e => e.Property,
                    BoundDagTypeEvaluation e => e.Type,
                    BoundDagDeconstructEvaluation e => e.DeconstructMethod,
                    BoundDagIndexEvaluation e => e.Property,
                    BoundDagSliceEvaluation e => (Symbol?)e.SliceMethod ?? e.IndexerAccess?.Indexer,
                    BoundDagIndexerEvaluation e => e.IndexerSymbol ?? e.IndexerAccess?.Indexer,
                    _ => throw ExceptionUtilities.UnexpectedValue(this.Kind)
                };
            }
        }

        public override int GetHashCode()
        {
            return Hash.Combine(Input.GetHashCode(), this.Symbol?.GetHashCode() ?? 0);
        }

#if DEBUG
        private int _id = -1;

        public int Id
        {
            get
            {
                return _id;
            }
            internal set
            {
                Debug.Assert(value > 0, "Id must be positive but was set to " + value);
                Debug.Assert(_id == -1, $"Id was set to {_id} and set again to {value}");
                _id = value;
            }
        }

        internal string GetOutputTempDebuggerDisplay()
        {
            var id = Id;
            return id switch
            {
                -1 => "<uninitialized>",

                // Note that we never expect to create an evaluation with id 0
                // To do so would imply that dag evaluation assigns to the original input
                0 => "<error>",

                _ => $"t{id}"
            };
        }
#endif
    }

    partial class BoundDagIndexEvaluation
    {
        public override int GetHashCode() => base.GetHashCode() ^ this.Index;
        public override bool Equals(BoundDagEvaluation obj)
        {
            return this == obj ||
                base.Equals(obj) &&
                // base.Equals checks the kind field, so the following cast is safe
                this.Index == ((BoundDagIndexEvaluation)obj).Index;
        }
    }

    partial class BoundDagIndexerEvaluation
    {
        public override int GetHashCode() => base.GetHashCode() ^ this.Index;
        public override bool Equals(BoundDagEvaluation obj)
        {
            return this == obj ||
                base.Equals(obj) &&
                // base.Equals checks the kind field, so the following cast is safe
                this.Index == ((BoundDagIndexerEvaluation)obj).Index;
        }
    }

    partial class BoundDagSliceEvaluation
    {
        public override int GetHashCode() => base.GetHashCode() ^ this.StartIndex ^ this.EndIndex;
        public override bool Equals(BoundDagEvaluation obj)
        {
            return this == obj ||
                base.Equals(obj) &&
                // base.Equals checks the kind field, so the following cast is safe
                (BoundDagSliceEvaluation)obj is var e &&
                this.StartIndex == e.StartIndex && this.EndIndex == e.EndIndex;
        }
    }
}
