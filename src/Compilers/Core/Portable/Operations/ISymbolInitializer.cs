﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Semantics
{
    /// <summary>
    /// Represents an initializer for a field, property, parameter or a local variable declaration.
    /// </summary>
    /// <remarks>
    /// This interface is reserved for implementation by its associated APIs. We reserve the right to
    /// change it in the future.
    /// </remarks>
    public interface ISymbolInitializer : IOperation
    {
        /// <summary>
        /// Underlying initializer value.
        /// </summary>
        IOperation Value { get; }
    }
}

