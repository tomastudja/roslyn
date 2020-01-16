﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Options.Providers;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.UnitTests
{
    [ExportWorkspaceServiceFactory(typeof(IOptionService), ServiceLayer.Host), Shared]
    internal class TestOptionsServiceFactory : IWorkspaceServiceFactory
    {
        private readonly ImmutableArray<Lazy<IOptionProvider, LanguageMetadata>> _providers;

        [ImportingConstructor]
        public TestOptionsServiceFactory(
            [ImportMany] IEnumerable<Lazy<IOptionProvider, LanguageMetadata>> optionProviders)
        {
            _providers = optionProviders.ToImmutableArray();
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            // give out new option service per workspace
            return new OptionServiceFactory.OptionService(
                new GlobalOptionService(_providers, SpecializedCollections.EmptyEnumerable<Lazy<IOptionPersister>>()),
                workspaceServices);
        }
    }
}
