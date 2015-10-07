﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.VisualStudio.ProjectSystem
{
    public class CSharpProjectGuidProviderTests
    {
        [Fact]
        public void Constructor_NullAsUnconfiguredProject_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>("unconfiguredProject", () => {

                new CSharpProjectGuidProvider((UnconfiguredProject)null);
            });
        }

        [Fact]
        public void AddItemTemplatesGuid_ReturnsNonEmptyGuid()
        {
            var provider = CreateInstance();

            // Handshake between the project system and templates around the actual guid value
            Assert.NotEqual(Guid.Empty, provider.AddItemTemplatesGuid);
        }

        [Fact]
        public void ProjectTypeGuid_ReturnsNonEmptyGuid()
        {
            var provider = CreateInstance();

            // Handshake between the project system and factory around the actual guid value
            Assert.NotEqual(Guid.Empty, provider.ProjectTypeGuid);
        }

        private static CSharpProjectGuidProvider CreateInstance()
        {
            var unconfiguedProject = IUnconfiguredProjectFactory.Create();

            return new CSharpProjectGuidProvider(unconfiguedProject);
        }
    }
}
