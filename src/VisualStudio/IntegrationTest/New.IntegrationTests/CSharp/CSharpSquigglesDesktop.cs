﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.IntegrationTest.Utilities;
using Xunit;

namespace Roslyn.VisualStudio.NewIntegrationTests.CSharp
{
    [Trait(Traits.Feature, Traits.Features.ErrorSquiggles)]
    public class CSharpSquigglesDesktop : CSharpSquigglesCommon
    {
        protected override bool SupportsGlobalUsings => false;

        public CSharpSquigglesDesktop()
            : base(WellKnownProjectTemplates.ClassLibrary)
        {
        }

        [IdeFact]
        public override async Task VerifySyntaxErrorSquiggles()
        {
            await base.VerifySyntaxErrorSquiggles();
        }

        [IdeFact]
        public override async Task VerifySemanticErrorSquiggles()
        {
            await base.VerifySemanticErrorSquiggles();
        }
    }
}
