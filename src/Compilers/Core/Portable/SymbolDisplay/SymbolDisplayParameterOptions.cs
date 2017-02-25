﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Specifies how parameters are displayed in the description of a (member, property/indexer, or delegate) symbol.
    /// </summary>
    [Flags]
    public enum SymbolDisplayParameterOptions
    {
        /// <summary>
        /// Omits parameters from symbol descriptions.
        /// </summary>
        /// <remarks>
        /// If this option is combined with <see cref="SymbolDisplayMemberOptions.IncludeParameters"/>, then only
        /// the parentheses will be shown (e.g. M()).
        /// </remarks>
        None = 0,

        /// <summary>
        /// Includes the <c>this</c> keyword before the first parameter of an extension method in C#. 
        /// </summary>
        /// <remarks>
        /// This option has no effect in Visual Basic.
        /// </remarks>
        IncludeExtensionThis = 1 << 0,

        /// <summary>
        /// Includes the <c>params</c>, <c>ref</c>, <c>ref readonly</c>, <c>out</c>, <c>ByRef</c>, <c>ByVal</c> keywords before parameters.
        /// </summary>
        IncludeParamsRefOut = 1 << 1,

        /// <summary>
        /// Includes parameter types in symbol descriptions.
        /// </summary>
        IncludeType = 1 << 2,

        /// <summary>
        /// Includes parameter names in symbol descriptions.
        /// </summary>
        IncludeName = 1 << 3,

        /// <summary>
        /// Includes parameter default values in symbol descriptions.
        /// </summary>
        /// <remarks>Ignored if <see cref="IncludeName"/> is not set.</remarks>
        IncludeDefaultValue = 1 << 4,

        /// <summary>
        /// Includes square brackets around optional parameters.
        /// </summary>
        IncludeOptionalBrackets = 1 << 5,
    }
}
