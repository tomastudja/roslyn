﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Extensions
{
    public class TelemetryExtensionTests
    {
        [Fact]
        public void TestConstantTelemetryId()
        {
            var expected = Guid.Parse("00000000-0000-0000-c4c5-914100000000");
            var actual = typeof(TelemetryExtensionTests).GetTelemetryId();
            var actualBytes = actual.ToByteArray();

            // If the assertion fails then telemetry ids could be changing
            // making them hard to track. It's important to not regress
            // the ability to track telemetry across versions of Roslyn.
            Assert.Equal(new Guid(actualBytes), expected);
        }
    }
}
