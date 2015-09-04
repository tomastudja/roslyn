﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.VisualStudio
{
    internal static class StringComparers
    {
        public static IEqualityComparer<string> Paths
        {
            get { return StringComparer.OrdinalIgnoreCase; }
        }
    }
}
