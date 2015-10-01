﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem
{
    [UnitTestTrait]
    public class UnconfiguredProjectCommonServicesTests
    {
        [Fact]
        public void Constructor_NullAsFeatures_ThrowsArgumentNull()
        {
            var threadingPolicy = new Lazy<IThreadHandling>(() => IThreadHandlingFactory.Create());
            var unconfiguredProject = IUnconfiguredProjectFactory.Create();
            var projectProperties = ProjectPropertiesFactory.Create(unconfiguredProject);
            var activeConfiguredProject = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties.ConfiguredProject);
            var activeConfiguredProjectProperties = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties);

            Assert.Throws<ArgumentNullException>("features", () => {
                new UnconfiguredProjectCommonServices((Lazy<IProjectFeatures>)null, threadingPolicy, activeConfiguredProject, activeConfiguredProjectProperties);
            });
        }

        [Fact]
        public void Constructor_NullAsThreadingPolicy_ThrowsArgumentNull()
        {
            var features = new Lazy<IProjectFeatures>(() => IProjectFeaturesFactory.Create());
            var unconfiguredProject = IUnconfiguredProjectFactory.Create();
            var projectProperties = ProjectPropertiesFactory.Create(unconfiguredProject);
            var activeConfiguredProject = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties.ConfiguredProject);
            var activeConfiguredProjectProperties = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties);

            Assert.Throws<ArgumentNullException>("threadingPolicy", () => {
                new UnconfiguredProjectCommonServices(features, (Lazy<IThreadHandling>)null, activeConfiguredProject, activeConfiguredProjectProperties);
            });
        }

        [Fact]
        public void Constructor_ValueAsFeatures_SetsFeaturesProperty()
        {
            var features = new Lazy<IProjectFeatures>(() => IProjectFeaturesFactory.Create());
            var threadingPolicy = new Lazy<IThreadHandling>(() => IThreadHandlingFactory.Create());
            var unconfiguredProject = IUnconfiguredProjectFactory.Create();
            var projectProperties = ProjectPropertiesFactory.Create(unconfiguredProject);
            var activeConfiguredProject = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties.ConfiguredProject);
            var activeConfiguredProjectProperties = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties);

            var services = new UnconfiguredProjectCommonServices(features, threadingPolicy, activeConfiguredProject, activeConfiguredProjectProperties);

            Assert.Same(features.Value, services.Features);
        }

        [Fact]
        public void Constructor_ValueAsThreadingPolicy_SetsThreadingPolicyProperty()
        {
            var features = new Lazy<IProjectFeatures>(() => IProjectFeaturesFactory.Create());
            var threadingPolicy = new Lazy<IThreadHandling>(() => IThreadHandlingFactory.Create());
            var unconfiguredProject = IUnconfiguredProjectFactory.Create();
            var projectProperties = ProjectPropertiesFactory.Create(unconfiguredProject);
            var activeConfiguredProject = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties.ConfiguredProject);
            var activeConfiguredProjectProperties = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties);

            var services = new UnconfiguredProjectCommonServices(features, threadingPolicy, activeConfiguredProject, activeConfiguredProjectProperties);

            Assert.Same(threadingPolicy.Value, services.ThreadingPolicy);
        }

        [Fact]
        public void Constructor_ValueAsActiveConfiguredProject_SetsActiveConfiguredProjectProperty()
        {
            var features = new Lazy<IProjectFeatures>(() => IProjectFeaturesFactory.Create());
            var threadingPolicy = new Lazy<IThreadHandling>(() => IThreadHandlingFactory.Create());
            var unconfiguredProject = IUnconfiguredProjectFactory.Create();
            var projectProperties = ProjectPropertiesFactory.Create(unconfiguredProject);
            var activeConfiguredProject = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties.ConfiguredProject);
            var activeConfiguredProjectProperties = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties);

            var services = new UnconfiguredProjectCommonServices(features, threadingPolicy, activeConfiguredProject, activeConfiguredProjectProperties);

            Assert.Same(projectProperties.ConfiguredProject, services.ActiveConfiguredProject);
        }

        [Fact]
        public void Constructor_ValueAsActiveConfiguredProjectProperties_SetsActiveConfiguredProjectPropertiesProperty()
        {
            var features = new Lazy<IProjectFeatures>(() => IProjectFeaturesFactory.Create());
            var threadingPolicy = new Lazy<IThreadHandling>(() => IThreadHandlingFactory.Create());
            var unconfiguredProject = IUnconfiguredProjectFactory.Create();
            var projectProperties = ProjectPropertiesFactory.Create(unconfiguredProject);
            var activeConfiguredProject = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties.ConfiguredProject);
            var activeConfiguredProjectProperties = IActiveConfiguredProjectFactory.ImplementValue(() => projectProperties);

            var services = new UnconfiguredProjectCommonServices(features, threadingPolicy, activeConfiguredProject, activeConfiguredProjectProperties);

            Assert.Same(projectProperties, services.ActiveConfiguredProjectProperties);
        }
    }
}
