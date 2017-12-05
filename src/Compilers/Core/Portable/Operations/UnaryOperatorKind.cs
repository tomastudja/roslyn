﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Operations
{
    /// <summary>
    /// Kind of unary operator
    /// </summary>
    public enum UnaryOperatorKind
    {
        /// <summary>
        /// Represents unknown or error operator kind.
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Represents the C# '~' operator.
        /// </summary>
        BitwiseNegation = 0x1,

        /// <summary>
        /// Represents the C# '!' operator and VB 'Not' operator.
        /// </summary>
        Not = 0x2,

        /// <summary>
        /// Represents the unary '+' operator.
        /// </summary>
        Plus = 0x3,

        /// <summary>
        /// Represents the unary '-' operator.
        /// </summary>
        Minus = 0x4,

        /// <summary>
        /// Represents the C# 'true' operator and VB 'IsTrue' operator.
        /// </summary>
        True = 0x5,

        /// <summary>
        /// Represents the C# 'false' operator and VB 'IsFalse' operator.
        /// </summary>
        False = 0x6
    }
}

