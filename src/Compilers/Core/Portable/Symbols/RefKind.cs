﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Denotes the kind of reference parameter.
    /// </summary>
    public enum RefKind : byte
    {
        /// <summary>
        /// Indicates a "value" parameter.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates a "ref" parameter.
        /// </summary>
        Ref = 1,

        /// <summary>
        /// Indicates an "out" parameter.
        /// </summary>
        Out = 2,

        /// <summary>
        /// Indicates an "in" parameter.
        /// </summary>
        In = 3,
    }

    internal static class RefKindExtensions
    {
        internal static string ToDisplayString(this RefKind kind)
        {
            switch (kind)
            {
                case RefKind.Out: return "out";
                case RefKind.Ref: return "ref";
                case RefKind.In: return "in";
                default: return null;
            }
        }

        internal static string ToPrefix(this RefKind kind)
        {
            switch (kind)
            {
                case RefKind.Out: return "out ";
                case RefKind.Ref: return "ref ";
                case RefKind.In: return "in ";
                default: return string.Empty;
            }
        }
    }
}
