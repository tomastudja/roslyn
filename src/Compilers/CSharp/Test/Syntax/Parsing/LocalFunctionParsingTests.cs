﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using System.Linq;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    [CompilerTrait(CompilerFeature.LocalFunctions)]
    public class LocalFunctionParsingTests : ParsingTests
    {
        internal static readonly CSharpParseOptions LocalFuncOptions = TestOptions.Regular.WithLocalFunctionsFeature();

        [Fact]
        public void NeverEndingTest()
        {
            var file = ParseFile(@"public class C {
    public void M() {
        async public virtual M() {}
        unsafe public M() {}
        async override M() {}
        unsafe private async override M() {}
        async virtual override sealed M() {}
    }
}");
            file.SyntaxTree.GetDiagnostics().Verify(
                // (3,9): error CS0106: The modifier 'async' is not valid for this item
                //         async public virtual M() {}
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "async").WithArguments("async").WithLocation(3, 9),
                // (3,15): error CS0106: The modifier 'public' is not valid for this item
                //         async public virtual M() {}
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "public").WithArguments("public").WithLocation(3, 15),
                // (3,22): error CS1031: Type expected
                //         async public virtual M() {}
                Diagnostic(ErrorCode.ERR_TypeExpected, "virtual").WithLocation(3, 22),
                // (3,22): error CS1001: Identifier expected
                //         async public virtual M() {}
                Diagnostic(ErrorCode.ERR_IdentifierExpected, "virtual").WithLocation(3, 22),
                // (3,22): error CS1002: ; expected
                //         async public virtual M() {}
                Diagnostic(ErrorCode.ERR_SemicolonExpected, "virtual").WithLocation(3, 22),
                // (3,22): error CS1513: } expected
                //         async public virtual M() {}
                Diagnostic(ErrorCode.ERR_RbraceExpected, "virtual").WithLocation(3, 22),
                // (3,30): error CS1520: Method must have a return type
                //         async public virtual M() {}
                Diagnostic(ErrorCode.ERR_MemberNeedsType, "M").WithLocation(3, 30),
                // (4,23): error CS1520: Method must have a return type
                //         unsafe public M() {}
                Diagnostic(ErrorCode.ERR_MemberNeedsType, "M").WithLocation(4, 23),
                // (5,24): error CS1520: Method must have a return type
                //         async override M() {}
                Diagnostic(ErrorCode.ERR_MemberNeedsType, "M").WithLocation(5, 24),
                // (6,39): error CS1520: Method must have a return type
                //         unsafe private async override M() {}
                Diagnostic(ErrorCode.ERR_MemberNeedsType, "M").WithLocation(6, 39),
                // (7,39): error CS1520: Method must have a return type
                //         async virtual override sealed M() {}
                Diagnostic(ErrorCode.ERR_MemberNeedsType, "M").WithLocation(7, 39),
                // (9,1): error CS1022: Type or namespace definition, or end-of-file expected
                // }
                Diagnostic(ErrorCode.ERR_EOFExpected, "}").WithLocation(9, 1));
        }

        [Fact]
        public void DiagnosticsWithoutExperimental()
        {
            // Experimental nodes should only appear when experimental are
            // turned on in parse options
            var file = ParseFile(@"
class c
{
    void m()
    {
        int local() => 0;
    }
    void m2()
    {
        int local() { return 0; }
    }
}", parseOptions: TestOptions.Regular.WithLanguageVersion(LanguageVersion.CSharp6));
            Assert.NotNull(file);
            Assert.False(file.DescendantNodes().Any(n => n.Kind() == SyntaxKind.LocalFunctionStatement && !n.ContainsDiagnostics));
            Assert.True(file.HasErrors);
            file.SyntaxTree.GetDiagnostics().Verify(
                // (6,13): error CS8059: Feature 'local functions' is not available in C# 6.  Please use language version 7 or greater.
                //         int local() => 0;
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "local").WithArguments("local functions", "7").WithLocation(6, 13),
                // (10,13): error CS8059: Feature 'local functions' is not available in C# 6.  Please use language version 7 or greater.
                //         int local() { return 0; }
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion6, "local").WithArguments("local functions", "7").WithLocation(10, 13)
                );

            Assert.Equal(0, file.SyntaxTree.Options.Features.Count);
            var c = Assert.IsType<ClassDeclarationSyntax>(file.Members.Single());
            Assert.Equal(2, c.Members.Count);
            var m = Assert.IsType<MethodDeclarationSyntax>(c.Members[0]);
            var s1 = Assert.IsType<LocalFunctionStatementSyntax>(m.Body.Statements[0]);
            Assert.True(s1.ContainsDiagnostics);

            var m2 = Assert.IsType<MethodDeclarationSyntax>(c.Members[1]);
            s1 = Assert.IsType<LocalFunctionStatementSyntax>(m.Body.Statements[0]);
            Assert.True(s1.ContainsDiagnostics);
        }

        [Fact]
        public void NodesWithExperimental()
        {
            // Experimental nodes should only appear when experimental are
            // turned on in parse options
            var file = ParseFile(@"
class c
{
    void m()
    {
        int local() => 0;
    }
    void m2()
    {
        int local()
        {
            return 0;
        }
    }
}");

            Assert.NotNull(file);
            Assert.False(file.HasErrors);
            Assert.Equal(0, file.SyntaxTree.Options.Features.Count);
            var c = Assert.IsType<ClassDeclarationSyntax>(file.Members.Single());
            Assert.Equal(2, c.Members.Count);
            var m = Assert.IsType<MethodDeclarationSyntax>(c.Members[0]);
            var s1 = Assert.IsType<LocalFunctionStatementSyntax>(m.Body.Statements[0]);
            Assert.Equal(SyntaxKind.PredefinedType, s1.ReturnType.Kind());
            Assert.Equal("int", s1.ReturnType.ToString());
            Assert.Equal("local", s1.Identifier.ToString());
            Assert.NotNull(s1.ParameterList);
            Assert.Empty(s1.ParameterList.Parameters);
            Assert.NotNull(s1.ExpressionBody);
            Assert.Equal(SyntaxKind.NumericLiteralExpression, s1.ExpressionBody.Expression.Kind());

            var m2 = Assert.IsType<MethodDeclarationSyntax>(c.Members[1]);
            s1 = Assert.IsType<LocalFunctionStatementSyntax>(m2.Body.Statements[0]);
            Assert.Equal(SyntaxKind.PredefinedType, s1.ReturnType.Kind());
            Assert.Equal("int", s1.ReturnType.ToString());
            Assert.Equal("local", s1.Identifier.ToString());
            Assert.NotNull(s1.ParameterList);
            Assert.Empty(s1.ParameterList.Parameters);
            Assert.Null(s1.ExpressionBody);
            Assert.NotNull(s1.Body);
            var s2 = Assert.IsType<ReturnStatementSyntax>(s1.Body.Statements.Single());
            Assert.Equal(SyntaxKind.NumericLiteralExpression, s2.Expression.Kind());
        }

        [Fact(Skip = "https://github.com/dotnet/roslyn/issues/10388")]
        public void LocalFunctionsWithAwait()
        {
            var file = ParseFile(@"class c
{
    void m1() { await await() => new await(); }
    void m2() { await () => new await(); }
    async void m3() { await () => new await(); }
    void m4() { async await() => new await(); }
}");

            Assert.NotNull(file);
            var c = (ClassDeclarationSyntax)file.Members.Single();
            Assert.Equal(4, c.Members.Count);

            {
                Assert.Equal(SyntaxKind.MethodDeclaration, c.Members[0].Kind());
                var m1 = (MethodDeclarationSyntax)c.Members[0];
                Assert.Equal(0, m1.Modifiers.Count);
                Assert.Equal(1, m1.Body.Statements.Count);
                Assert.Equal(SyntaxKind.LocalFunctionStatement, m1.Body.Statements[0].Kind());
                var s1 = (LocalFunctionStatementSyntax)m1.Body.Statements[0];
                Assert.False(s1.HasErrors);
                Assert.Equal(0, s1.Modifiers.Count);
                Assert.Equal("await", s1.ReturnType.ToString());
                Assert.Equal("await", s1.Identifier.ToString());
                Assert.Null(s1.TypeParameterList);
                Assert.Equal(0, s1.ParameterList.ParameterCount);
                Assert.Null(s1.Body);
                Assert.NotNull(s1.ExpressionBody);
            }

            {
                Assert.Equal(SyntaxKind.MethodDeclaration, c.Members[1].Kind());
                var m2 = (MethodDeclarationSyntax)c.Members[1];
                Assert.Equal(0, m2.Modifiers.Count);
                Assert.Equal(2, m2.Body.Statements.Count);
                Assert.Equal(SyntaxKind.ExpressionStatement, m2.Body.Statements[0].Kind());
                var s1 = (ExpressionStatementSyntax)m2.Body.Statements[0];
                Assert.Equal(SyntaxKind.InvocationExpression, s1.Expression.Kind());
                var e1 = (InvocationExpressionSyntax)s1.Expression;
                Assert.Equal("await", e1.Expression.ToString());
                Assert.Equal(0, e1.ArgumentList.Arguments.Count);
                Assert.True(s1.SemicolonToken.IsMissing);
                Assert.Equal("=> ", s1.GetTrailingTrivia().ToFullString());
            }

            {
                Assert.Equal(SyntaxKind.MethodDeclaration, c.Members[2].Kind());
                var m3 = (MethodDeclarationSyntax)c.Members[2];
                Assert.Equal(1, m3.Modifiers.Count);
                Assert.Equal("async", m3.Modifiers.Single().ToString());
                Assert.Equal(2, m3.Body.Statements.Count);
                Assert.Equal(SyntaxKind.ExpressionStatement, m3.Body.Statements[0].Kind());
                var s1 = (ExpressionStatementSyntax)m3.Body.Statements[0];
                Assert.Equal(SyntaxKind.AwaitExpression, s1.Expression.Kind());
                var e1 = (AwaitExpressionSyntax)s1.Expression;
                Assert.Equal(SyntaxKind.SimpleLambdaExpression, e1.Expression.Kind());
                Assert.True(s1.SemicolonToken.IsMissing);
                Assert.Equal("=> ", s1.GetTrailingTrivia().ToFullString());
            }
        }
    }
}
