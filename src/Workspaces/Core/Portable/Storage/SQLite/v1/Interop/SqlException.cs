﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.SQLite.Interop;

namespace Microsoft.CodeAnalysis.SQLite.v1.Interop
{
    internal class SqlException : Exception
    {
        public readonly Result Result;

        public SqlException(Result result, string message)
            : base(message)
        {
            this.Result = result;
        }
    }
}
