﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.


namespace Microsoft.CodeAnalysis.FlowAnalysis
{
    /// <summary>
    /// Indicates the kind of flow capture in an <see cref="IFlowCaptureOperation"/>.
    /// </summary>
    internal enum FlowCaptureKind
    {
        /// <summary>
        /// Indicates an R-Value flow capture, i.e. capture of a symbol's value.
        /// </summary>
        RValueCapture,

        /// <summary>
        /// Indicates an L-Value flow capture, i.e. captures of a symbol's location/address.
        /// </summary>
        LValueCapture,

        /// <summary>
        /// Indicates both an R-Value and an L-Value flow capture, i.e. captures of a symbol's value and location/address.
        /// These are generated for left of a compound assignment operation, such that there is conditional code on the right side of the compound assignment.
        /// </summary>
        LValueAndRValueCapture
    }
}
