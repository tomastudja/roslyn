﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Options.Providers;

namespace Microsoft.CodeAnalysis.ImplementType
{
    [ExportOptionProvider, Shared]
    internal class ImplementTypeOptionsProvider : IOptionProvider
    {
        private readonly IEnumerable<IOption> _options = ImmutableArray.Create<IOption>(
            ImplementTypeOptions.InsertionBehavior);

        public IEnumerable<IOption> GetOptions() => _options;
    }
}