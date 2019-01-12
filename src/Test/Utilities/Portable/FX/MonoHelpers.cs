﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
    public static class MonoHelpers
    {
        public static bool IsRunningOnMono() => Roslyn.Test.Utilities.ExecutionConditionUtil.IsMonoDesktop;
    }
}
