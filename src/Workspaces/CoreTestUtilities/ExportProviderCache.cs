﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Composition;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
    public static class ExportProviderCache
    {
        private static readonly PartDiscovery s_partDiscovery = CreatePartDiscovery(Resolver.DefaultInstance);

        public static ComposableCatalog CreateAssemblyCatalog(Assembly assembly)
        {
            return CreateAssemblyCatalog(SpecializedCollections.SingletonEnumerable(assembly));
        }

        public static ComposableCatalog CreateAssemblyCatalog(IEnumerable<Assembly> assemblies, Resolver resolver = null)
        {
            var discovery = resolver == null ? s_partDiscovery : CreatePartDiscovery(resolver);

            // If we run CreatePartsAsync on the test thread we may deadlock since it'll schedule stuff back
            // on the thread.
            var parts = Task.Run(async () => await discovery.CreatePartsAsync(assemblies).ConfigureAwait(false)).Result;

            return ComposableCatalog.Create(resolver ?? Resolver.DefaultInstance).AddParts(parts);
        }

        public static ComposableCatalog CreateTypeCatalog(IEnumerable<Type> types, Resolver resolver = null)
        {
            var discovery = resolver == null ? s_partDiscovery : CreatePartDiscovery(resolver);

            // If we run CreatePartsAsync on the test thread we may deadlock since it'll schedule stuff back
            // on the thread.
            var parts = Task.Run(async () => await discovery.CreatePartsAsync(types).ConfigureAwait(false)).Result;

            return ComposableCatalog.Create(resolver ?? Resolver.DefaultInstance).AddParts(parts);
        }

        public static PartDiscovery CreatePartDiscovery(Resolver resolver)
        {
            return PartDiscovery.Combine(new AttributedPartDiscoveryV1(resolver), new AttributedPartDiscovery(resolver, isNonPublicSupported: true));
        }

        public static ComposableCatalog WithParts(this ComposableCatalog @this, ComposableCatalog catalog)
        {
            return @this.AddParts(catalog.DiscoveredParts);
        }

        public static ComposableCatalog WithParts(this ComposableCatalog catalog, IEnumerable<Type> types)
        {
            return catalog.WithParts(CreateTypeCatalog(types));
        }

        public static ComposableCatalog WithParts(this ComposableCatalog catalog, params Type[] types)
        {
            return WithParts(catalog, (IEnumerable<Type>)types);
        }

        public static ComposableCatalog WithPart(this ComposableCatalog catalog, Type t)
        {
            return catalog.WithParts(CreateTypeCatalog(SpecializedCollections.SingletonEnumerable(t)));
        }
    }
}
