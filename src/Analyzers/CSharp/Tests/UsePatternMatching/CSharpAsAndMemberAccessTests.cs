﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.UsePatternMatching;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.UsePatternMatching
{
    using VerifyCS = CSharpCodeFixVerifier<
        CSharpAsAndMemberAccessDiagnosticAnalyzer,
        CSharpAsAndMemberAccessCodeFixProvider>;

    [Trait(Traits.Feature, Traits.Features.CodeActionsUsePatternMatchingForAsAndMemberAccess)]
    public partial class CSharpAsAndMemberAccessTests
    {
        [Fact]
        public async Task TestCoreCase()
        {
            await new VerifyCS.Test
            {
                TestCode = """
                    class C
                    {
                        void M(object o)
                        {
                            if (([|o as string|])?.Length == 0)
                            {
                            }
                        }
                    }
                    """,
                FixedCode = """
                    class C
                    {
                        void M(object o)
                        {
                            if (o is string { Length: 0 })
                            {
                            }
                        }
                    }
                    """,
            }.RunAsync();
        }
    }
}
