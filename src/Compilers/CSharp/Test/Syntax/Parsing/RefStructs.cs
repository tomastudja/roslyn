// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Xunit;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Parsing
{
    [CompilerTrait(CompilerFeature.ReadonlyReferences)]
    public class RefStructs : ParsingTests
    {
        public RefStructs(ITestOutputHelper output) : base(output) { }

        protected override SyntaxTree ParseTree(string text, CSharpParseOptions options)
        {
            return SyntaxFactory.ParseSyntaxTree(text, options: options);
        }

        [Fact]
        public void RefStructSimple()
        {
            var text = @"
class Program
{
    ref struct S1{} 

    public ref struct S2{}
}
";

            var comp = CreateCompilationWithMscorlib45(text, parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.Latest), options: TestOptions.DebugDll);
            comp.VerifyDiagnostics();
        }

        [Fact]
        public void RefStructSimpleLangVer()
        {
            var text = @"
class Program
{
    ref struct S1{}

    public ref struct S2{}
}
";

            var comp = CreateCompilationWithMscorlib45(text, parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.CSharp7), options: TestOptions.DebugDll);
            comp.VerifyDiagnostics(
                // (4,5): error CS8107: Feature 'ref structs' is not available in C# 7. Please use language version 7.1 or greater.
                //     ref struct S1{}
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7, "ref").WithArguments("ref structs", "7.1").WithLocation(4, 5),
                // (6,12): error CS8107: Feature 'ref structs' is not available in C# 7. Please use language version 7.1 or greater.
                //     public ref struct S2{}
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7, "ref").WithArguments("ref structs", "7.1").WithLocation(6, 12)
            );
        }

        [Fact]
        public void RefStructErr()
        {
            var text = @"
class Program
{
    ref class S1{}

    public ref unsafe struct S2{}

    ref interface I1{};

    public ref delegate ref int D1();
}
";

            var comp = CreateCompilationWithMscorlib45(text, parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.Latest), options: TestOptions.DebugDll);
            comp.VerifyDiagnostics(
                // (4,9): error CS1031: Type expected
                //     ref class S1{}
                Diagnostic(ErrorCode.ERR_TypeExpected, "class"),
                // (6,16): error CS1031: Type expected
                //     public ref unsafe struct S2{}
                Diagnostic(ErrorCode.ERR_TypeExpected, "unsafe"),
                // (8,9): error CS1031: Type expected
                //     ref interface I1{};
                Diagnostic(ErrorCode.ERR_TypeExpected, "interface").WithLocation(8, 9),
                // (10,16): error CS1031: Type expected
                //     public ref delegate ref int D1();
                Diagnostic(ErrorCode.ERR_TypeExpected, "delegate").WithLocation(10, 16),
                // (6,30): error CS0227: Unsafe code may only appear if compiling with /unsafe
                //     public ref unsafe struct S2{}
                Diagnostic(ErrorCode.ERR_IllegalUnsafe, "S2")
            );
        }

        [Fact]
        public void RefStructPartial()
        {
            var text = @"
class Program
{
    partial ref struct S1{}

    partial ref struct S1{}
}
";

            var comp = CreateCompilationWithMscorlib45(text, parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.CSharp7_1), options: TestOptions.DebugDll);
            comp.VerifyDiagnostics();
        }
    }
}
