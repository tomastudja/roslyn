﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Formatting.Rules
{
    internal sealed class NoOpFormattingRule : AbstractFormattingRule
    {
        public static readonly NoOpFormattingRule Instance = new NoOpFormattingRule();

        private NoOpFormattingRule()
        {
        }
    }
}
