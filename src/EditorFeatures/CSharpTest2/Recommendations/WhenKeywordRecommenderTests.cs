﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Recommendations
{
    public class WhenKeywordRecommenderTests : KeywordRecommenderTests
    {
        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_AfterCatch()
        {
            await VerifyKeywordAsync(AddInsideMethod(
@"try {} catch $$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_AfterCatchDeclaration1()
        {
            await VerifyKeywordAsync(AddInsideMethod(
@"try {} catch (Exception) $$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_AfterCatchDeclaration2()
        {
            await VerifyKeywordAsync(AddInsideMethod(
@"try {} catch (Exception e) $$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_AfterCatchDeclarationEmpty()
        {
            await VerifyKeywordAsync(AddInsideMethod(
@"try {} catch () $$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_NotAfterTryBlock()
        {
            await VerifyAbsenceAsync(AddInsideMethod(
@"try {} $$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_NotAfterFilter1()
        {
            await VerifyAbsenceAsync(AddInsideMethod(
@"try {} catch (Exception e) when $$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_NotAfterFilter2()
        {
            await VerifyAbsenceAsync(AddInsideMethod(
@"try {} catch (Exception e) when ($$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForCatchClause_NotAfterFilter3()
        {
            await VerifyAbsenceAsync(AddInsideMethod(
@"try {} catch (Exception e) when (true) $$"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        [WorkItem(24113, "https://github.com/dotnet/roslyn/issues/24113")]
        public async Task TestForSwitchCase_AfterDeclarationPattern()
        {
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case int i $$ }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case int i $$: }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case int i $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        [WorkItem(25084, "https://github.com/dotnet/roslyn/issues/25084")]
        public async Task TestForSwitchCase_AfterLiteral()
        {
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case 1 $$ }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case 1 $$: }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case 1 $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_AfterBinaryExpression()
        {
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case 1 + 1 $$ }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case 1 + 1 $$: }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case 1 + 1 $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_AfterParenthesesWithMissingTokenInside()
        {
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case (1 + ) $$ }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case (1 + ) $$: }"));
            await VerifyKeywordAsync(AddInsideMethod(@"switch (1) { case (1 + ) $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotAfterMissingTokenInBinaryExpression()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case 1 + $$ }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case 1 + $$: }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case 1 + $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotAfterMissingCloseParen()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case (1 + 1 $$ }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case (1 + 1 $$: }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case (1 + 1 $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotInsideParentheses()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case (1 + 1 $$) }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case (1 + 1 $$): }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case (1 + 1 $$) break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotAfterNew()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case new $$ }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case new $$: }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case new $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotAfterCase()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case $$ }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case $$: }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotAfterDefault()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { default $$ }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { default $$: }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { default $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotAfterWhen()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case 1 when $$ }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case 1 when $$: }"));
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { case 1 when $$ break; }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_NotInEmptySwitchStatement()
        {
            await VerifyAbsenceAsync(AddInsideMethod(@"switch (1) { $$ }"));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_SemanticCheck_NotAfterPredefinedType()
        {
            await VerifyAbsenceAsync(@"
class SyntaxNode { }
class Color { }
class C
{
    const Color Color = null;
    void M() { switch (new object()) { case int $$ } }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_SemanticCheck_NotAfterGenericType()
        {
            await VerifyAbsenceAsync(@"
class SyntaxNode { }
class Color { }
class C
{
    const Color Color = null;
    void M() { switch (new object()) { case Dictionary<string, int> $$ } }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_SemanticCheck_NotAfterCustomType()
        {
            await VerifyAbsenceAsync(@"
class SyntaxNode { }
class Color { }
class C
{
    const Color Color = null;
    void M() { switch (new object()) { case SyntaxNode $$ } }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_SemanticCheck_AfterColorColor()
        {
            await VerifyKeywordAsync(@"
class SyntaxNode { }
class Color { }
class C
{
    const Color Color = null;
    void M() { switch (new object()) { case Color $$ } }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_SemanticCheck_AfterLocalConstant()
        {
            await VerifyKeywordAsync(@"
class SyntaxNode { }
class Color { }
class C
{
    const Color Color = null;
    void M() { const object local = null; switch (new object()) { case local $$ } }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.KeywordRecommending)]
        public async Task TestForSwitchCase_SemanticCheck_AfterUnknownName()
        {
            await VerifyKeywordAsync(@"
class SyntaxNode { }
class Color { }
class C
{
    const Color Color = null;
    void M() { switch (new object()) { case unknown $$ } }
}");
        }
    }
}
