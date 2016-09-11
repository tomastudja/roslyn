﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Roslyn.VisualStudio.Test.Utilities;

namespace Roslyn.VisualStudio.IntegrationTests
{
    public abstract class AbstractIntegrationTest : IDisposable
    {
        protected readonly VisualStudioInstanceContext _visualStudio;

        protected AbstractIntegrationTest(VisualStudioInstanceFactory instanceFactory)
        {
            _visualStudio = instanceFactory.GetNewOrUsedInstance(SharedIntegrationHostFixture.RequiredPackageIds);
        }

        public void Dispose()
        {
            _visualStudio.Dispose();
        }
    }
}
