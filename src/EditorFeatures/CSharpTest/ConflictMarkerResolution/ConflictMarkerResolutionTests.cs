﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.ConflictMarkerResolution;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Diagnostics;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.ConflictMarkerResolution
{
    public class ConflictMarkerResolutionTests : AbstractCSharpDiagnosticProviderBasedUserDiagnosticTest
    {
        internal override (DiagnosticAnalyzer, CodeFixProvider) CreateDiagnosticProviderAndFixer(Workspace workspace)
            => (null, new CSharpResolveConflictMarkerCodeFixProvider());

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsResolveConflictMarker)]
        public async Task TestTakeTop1()
        {
            await TestAsync(
@"
using System;

namespace N
{
[|<<<<<<<|] This is mine!
    class Program
    {
        static void Main(string[] args)
        {
            Program p;
            Console.WriteLine(""My section"");
        }
    }
=======
    class Program2
    {
        static void Main2(string[] args)
        {
            Program2 p;
            Console.WriteLine(""Their section"");
        }
    }
>>>>>>> This is theirs!
}",
@"
using System;

namespace N
{
    class Program
    {
        static void Main(string[] args)
        {
            Program p;
            Console.WriteLine(""My section"");
        }
    }
}", index: 0, compareTokens: false);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsResolveConflictMarker)]
        public async Task TestTakeBottom1()
        {
            await TestAsync(
@"
using System;

namespace N
{
[|<<<<<<<|] This is mine!
    class Program
    {
        static void Main(string[] args)
        {
            Program p;
            Console.WriteLine(""My section"");
        }
    }
=======
    class Program2
    {
        static void Main2(string[] args)
        {
            Program2 p;
            Console.WriteLine(""Their section"");
        }
    }
>>>>>>> This is theirs!
}",
@"
using System;

namespace N
{
    class Program2
    {
        static void Main2(string[] args)
        {
            Program2 p;
            Console.WriteLine(""Their section"");
        }
    }
}", index: 1, compareTokens: false);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsResolveConflictMarker)]
        public async Task TestTakeBoth1()
        {
            await TestAsync(
@"
using System;

namespace N
{
[|<<<<<<<|] This is mine!
    class Program
    {
        static void Main(string[] args)
        {
            Program p;
            Console.WriteLine(""My section"");
        }
    }
=======
    class Program2
    {
        static void Main2(string[] args)
        {
            Program2 p;
            Console.WriteLine(""Their section"");
        }
    }
>>>>>>> This is theirs!
}",
@"
using System;

namespace N
{
    class Program
    {
        static void Main(string[] args)
        {
            Program p;
            Console.WriteLine(""My section"");
        }
    }
    class Program2
    {
        static void Main2(string[] args)
        {
            Program2 p;
            Console.WriteLine(""Their section"");
        }
    }
}", index: 2, compareTokens: false);
        }
    }
}
