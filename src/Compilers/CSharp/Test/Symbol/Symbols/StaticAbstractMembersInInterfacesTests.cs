﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.CSharp.Symbols.Retargeting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Symbols
{
    public class StaticAbstractMembersInInterfacesTests : CSharpTestBase
    {
        [Fact]
        public void MethodModifiers_01()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01()
    ; 

    virtual static void M02()
    ; 

    sealed static void M03() 
    ; 

    override static void M04() 
    ; 

    abstract virtual static void M05()
    ; 

    abstract sealed static void M06()
    ; 

    abstract override static void M07()
    ; 

    virtual sealed static void M08() 
    ; 

    virtual override static void M09() 
    ; 

    sealed override static void M10() 
    ; 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M01()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(4, 26),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (7,25): error CS0501: 'I1.M02()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M02").WithArguments("I1.M02()").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static void M03() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "9.0", "preview").WithLocation(10, 24),
                // (10,24): error CS0501: 'I1.M03()' must declare a body because it is not marked abstract, extern, or partial
                //     sealed static void M03() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M03").WithArguments("I1.M03()").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (13,26): error CS0501: 'I1.M04()' must declare a body because it is not marked abstract, extern, or partial
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M04").WithArguments("I1.M04()").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "9.0", "preview").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "9.0", "preview").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "9.0", "preview").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "9.0", "preview").WithLocation(25, 32),
                // (25,32): error CS0501: 'I1.M08()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M08").WithArguments("I1.M08()").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (28,34): error CS0501: 'I1.M09()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M09").WithArguments("I1.M09()").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "9.0", "preview").WithLocation(31, 33),
                // (31,33): error CS0501: 'I1.M10()' must declare a body because it is not marked abstract, extern, or partial
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M10").WithArguments("I1.M10()").WithLocation(31, 33)
                );

            ValidateMethodModifiers_01(compilation1);
        }

        private static void ValidateMethodModifiers_01(CSharpCompilation compilation1)
        {
            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("M01");

            Assert.True(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.True(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            var m02 = i1.GetMember<MethodSymbol>("M02");

            Assert.False(m02.IsAbstract);
            Assert.False(m02.IsVirtual);
            Assert.False(m02.IsMetadataVirtual());
            Assert.False(m02.IsSealed);
            Assert.True(m02.IsStatic);
            Assert.False(m02.IsExtern);
            Assert.False(m02.IsAsync);
            Assert.False(m02.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m02));

            var m03 = i1.GetMember<MethodSymbol>("M03");

            Assert.False(m03.IsAbstract);
            Assert.False(m03.IsVirtual);
            Assert.False(m03.IsMetadataVirtual());
            Assert.False(m03.IsSealed);
            Assert.True(m03.IsStatic);
            Assert.False(m03.IsExtern);
            Assert.False(m03.IsAsync);
            Assert.False(m03.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m03));

            var m04 = i1.GetMember<MethodSymbol>("M04");

            Assert.False(m04.IsAbstract);
            Assert.False(m04.IsVirtual);
            Assert.False(m04.IsMetadataVirtual());
            Assert.False(m04.IsSealed);
            Assert.True(m04.IsStatic);
            Assert.False(m04.IsExtern);
            Assert.False(m04.IsAsync);
            Assert.False(m04.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m04));

            var m05 = i1.GetMember<MethodSymbol>("M05");

            Assert.True(m05.IsAbstract);
            Assert.False(m05.IsVirtual);
            Assert.True(m05.IsMetadataVirtual());
            Assert.False(m05.IsSealed);
            Assert.True(m05.IsStatic);
            Assert.False(m05.IsExtern);
            Assert.False(m05.IsAsync);
            Assert.False(m05.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m05));

            var m06 = i1.GetMember<MethodSymbol>("M06");

            Assert.True(m06.IsAbstract);
            Assert.False(m06.IsVirtual);
            Assert.True(m06.IsMetadataVirtual());
            Assert.False(m06.IsSealed);
            Assert.True(m06.IsStatic);
            Assert.False(m06.IsExtern);
            Assert.False(m06.IsAsync);
            Assert.False(m06.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m06));

            var m07 = i1.GetMember<MethodSymbol>("M07");

            Assert.True(m07.IsAbstract);
            Assert.False(m07.IsVirtual);
            Assert.True(m07.IsMetadataVirtual());
            Assert.False(m07.IsSealed);
            Assert.True(m07.IsStatic);
            Assert.False(m07.IsExtern);
            Assert.False(m07.IsAsync);
            Assert.False(m07.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m07));

            var m08 = i1.GetMember<MethodSymbol>("M08");

            Assert.False(m08.IsAbstract);
            Assert.False(m08.IsVirtual);
            Assert.False(m08.IsMetadataVirtual());
            Assert.False(m08.IsSealed);
            Assert.True(m08.IsStatic);
            Assert.False(m08.IsExtern);
            Assert.False(m08.IsAsync);
            Assert.False(m08.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m08));

            var m09 = i1.GetMember<MethodSymbol>("M09");

            Assert.False(m09.IsAbstract);
            Assert.False(m09.IsVirtual);
            Assert.False(m09.IsMetadataVirtual());
            Assert.False(m09.IsSealed);
            Assert.True(m09.IsStatic);
            Assert.False(m09.IsExtern);
            Assert.False(m09.IsAsync);
            Assert.False(m09.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m09));

            var m10 = i1.GetMember<MethodSymbol>("M10");

            Assert.False(m10.IsAbstract);
            Assert.False(m10.IsVirtual);
            Assert.False(m10.IsMetadataVirtual());
            Assert.False(m10.IsSealed);
            Assert.True(m10.IsStatic);
            Assert.False(m10.IsExtern);
            Assert.False(m10.IsAsync);
            Assert.False(m10.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m10));
        }

        [Fact]
        public void MethodModifiers_02()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01()
    {}

    virtual static void M02()
    {}

    sealed static void M03() 
    {}

    override static void M04() 
    {}

    abstract virtual static void M05()
    {}

    abstract sealed static void M06()
    {}

    abstract override static void M07()
    {}

    virtual sealed static void M08() 
    {}

    virtual override static void M09() 
    {}

    sealed override static void M10() 
    {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M01()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(4, 26),
                // (4,26): error CS0500: 'I1.M01()' cannot declare a body because it is marked abstract
                //     abstract static void M01()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M01").WithArguments("I1.M01()").WithLocation(4, 26),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static void M03() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "9.0", "preview").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "9.0", "preview").WithLocation(16, 34),
                // (16,34): error CS0500: 'I1.M05()' cannot declare a body because it is marked abstract
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M05").WithArguments("I1.M05()").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "9.0", "preview").WithLocation(19, 33),
                // (19,33): error CS0500: 'I1.M06()' cannot declare a body because it is marked abstract
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M06").WithArguments("I1.M06()").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "9.0", "preview").WithLocation(22, 35),
                // (22,35): error CS0500: 'I1.M07()' cannot declare a body because it is marked abstract
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M07").WithArguments("I1.M07()").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "9.0", "preview").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "9.0", "preview").WithLocation(31, 33)
                );

            ValidateMethodModifiers_01(compilation1);
        }

        [Fact]
        public void MethodModifiers_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01()
    ; 

    virtual static void M02()
    ; 

    sealed static void M03() 
    ; 

    override static void M04() 
    ; 

    abstract virtual static void M05()
    ; 

    abstract sealed static void M06()
    ; 

    abstract override static void M07()
    ; 

    virtual sealed static void M08() 
    ; 

    virtual override static void M09() 
    ; 

    sealed override static void M10() 
    ; 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (7,25): error CS0501: 'I1.M02()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M02").WithArguments("I1.M02()").WithLocation(7, 25),
                // (10,24): error CS0501: 'I1.M03()' must declare a body because it is not marked abstract, extern, or partial
                //     sealed static void M03() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M03").WithArguments("I1.M03()").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (13,26): error CS0501: 'I1.M04()' must declare a body because it is not marked abstract, extern, or partial
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M04").WithArguments("I1.M04()").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS0501: 'I1.M08()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M08").WithArguments("I1.M08()").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (28,34): error CS0501: 'I1.M09()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M09").WithArguments("I1.M09()").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS0501: 'I1.M10()' must declare a body because it is not marked abstract, extern, or partial
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M10").WithArguments("I1.M10()").WithLocation(31, 33)
                );

            ValidateMethodModifiers_01(compilation1);
        }

        [Fact]
        public void MethodModifiers_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01()
    {}

    virtual static void M02()
    {}

    sealed static void M03() 
    {}

    override static void M04() 
    {}

    abstract virtual static void M05()
    {}

    abstract sealed static void M06()
    {}

    abstract override static void M07()
    {}

    virtual sealed static void M08() 
    {}

    virtual override static void M09() 
    {}

    sealed override static void M10() 
    {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS0500: 'I1.M01()' cannot declare a body because it is marked abstract
                //     abstract static void M01()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M01").WithArguments("I1.M01()").WithLocation(4, 26),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS0500: 'I1.M05()' cannot declare a body because it is marked abstract
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M05").WithArguments("I1.M05()").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS0500: 'I1.M06()' cannot declare a body because it is marked abstract
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M06").WithArguments("I1.M06()").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS0500: 'I1.M07()' cannot declare a body because it is marked abstract
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M07").WithArguments("I1.M07()").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33)
                );

            ValidateMethodModifiers_01(compilation1);
        }

        [Fact]
        public void MethodModifiers_05()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01()
    ; 

    virtual static void M02()
    ; 

    sealed static void M03() 
    ; 

    override static void M04() 
    ; 

    abstract virtual static void M05()
    ; 

    abstract sealed static void M06()
    ; 

    abstract override static void M07()
    ; 

    virtual sealed static void M08() 
    ; 

    virtual override static void M09() 
    ; 

    sealed override static void M10() 
    ; 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static void M01()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "7.3", "preview").WithLocation(4, 26),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (7,25): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M02").WithArguments("static", "7.3", "8.0").WithLocation(7, 25),
                // (7,25): error CS0501: 'I1.M02()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M02").WithArguments("I1.M02()").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static void M03() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "7.3", "preview").WithLocation(10, 24),
                // (10,24): error CS0501: 'I1.M03()' must declare a body because it is not marked abstract, extern, or partial
                //     sealed static void M03() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M03").WithArguments("I1.M03()").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (13,26): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M04").WithArguments("static", "7.3", "8.0").WithLocation(13, 26),
                // (13,26): error CS0501: 'I1.M04()' must declare a body because it is not marked abstract, extern, or partial
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M04").WithArguments("I1.M04()").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "7.3", "preview").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "7.3", "preview").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "7.3", "preview").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "7.3", "preview").WithLocation(25, 32),
                // (25,32): error CS0501: 'I1.M08()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M08").WithArguments("I1.M08()").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (28,34): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M09").WithArguments("static", "7.3", "8.0").WithLocation(28, 34),
                // (28,34): error CS0501: 'I1.M09()' must declare a body because it is not marked abstract, extern, or partial
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M09").WithArguments("I1.M09()").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "7.3", "preview").WithLocation(31, 33),
                // (31,33): error CS0501: 'I1.M10()' must declare a body because it is not marked abstract, extern, or partial
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "M10").WithArguments("I1.M10()").WithLocation(31, 33)
                );

            ValidateMethodModifiers_01(compilation1);
        }

        [Fact]
        public void MethodModifiers_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01()
    {}

    virtual static void M02()
    {}

    sealed static void M03() 
    {}

    override static void M04() 
    {}

    abstract virtual static void M05()
    {}

    abstract sealed static void M06()
    {}

    abstract override static void M07()
    {}

    virtual sealed static void M08() 
    {}

    virtual override static void M09() 
    {}

    sealed override static void M10() 
    {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static void M01()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "7.3", "preview").WithLocation(4, 26),
                // (4,26): error CS0500: 'I1.M01()' cannot declare a body because it is marked abstract
                //     abstract static void M01()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M01").WithArguments("I1.M01()").WithLocation(4, 26),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (7,25): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual static void M02()
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M02").WithArguments("default interface implementation", "8.0").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static void M03() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "7.3", "preview").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (13,26): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     override static void M04() 
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M04").WithArguments("default interface implementation", "8.0").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "7.3", "preview").WithLocation(16, 34),
                // (16,34): error CS0500: 'I1.M05()' cannot declare a body because it is marked abstract
                //     abstract virtual static void M05()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M05").WithArguments("I1.M05()").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "7.3", "preview").WithLocation(19, 33),
                // (19,33): error CS0500: 'I1.M06()' cannot declare a body because it is marked abstract
                //     abstract sealed static void M06()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M06").WithArguments("I1.M06()").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "7.3", "preview").WithLocation(22, 35),
                // (22,35): error CS0500: 'I1.M07()' cannot declare a body because it is marked abstract
                //     abstract override static void M07()
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M07").WithArguments("I1.M07()").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static void M08() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "7.3", "preview").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (28,34): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual override static void M09() 
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M09").WithArguments("default interface implementation", "8.0").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static void M10() 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "7.3", "preview").WithLocation(31, 33)
                );

            ValidateMethodModifiers_01(compilation1);
        }

        [Fact]
        public void SealedStaticConstructor_01()
        {
            var source1 =
@"
interface I1
{
    sealed static I1() {}
}

partial interface I2
{
    partial sealed static I2();
}

partial interface I2
{
    partial static I2() {}
}

partial interface I3
{
    partial static I3();
}

partial interface I3
{
    partial sealed static I3() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,19): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static I1() {}
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "I1").WithArguments("sealed").WithLocation(4, 19),
                // (9,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial sealed static I2();
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(9, 5),
                // (9,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial sealed static I2();
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(9, 5),
                // (9,27): error CS0106: The modifier 'sealed' is not valid for this item
                //     partial sealed static I2();
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "I2").WithArguments("sealed").WithLocation(9, 27),
                // (14,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial static I2() {}
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(14, 5),
                // (14,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial static I2() {}
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(14, 5),
                // (14,20): error CS0111: Type 'I2' already defines a member called 'I2' with the same parameter types
                //     partial static I2() {}
                Diagnostic(ErrorCode.ERR_MemberAlreadyExists, "I2").WithArguments("I2", "I2").WithLocation(14, 20),
                // (19,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial static I3();
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(19, 5),
                // (19,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial static I3();
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(19, 5),
                // (24,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial sealed static I3() {}
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(24, 5),
                // (24,5): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     partial sealed static I3() {}
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(24, 5),
                // (24,27): error CS0106: The modifier 'sealed' is not valid for this item
                //     partial sealed static I3() {}
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "I3").WithArguments("sealed").WithLocation(24, 27),
                // (24,27): error CS0111: Type 'I3' already defines a member called 'I3' with the same parameter types
                //     partial sealed static I3() {}
                Diagnostic(ErrorCode.ERR_MemberAlreadyExists, "I3").WithArguments("I3", "I3").WithLocation(24, 27)
                );

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>(".cctor");

            Assert.False(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.False(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));
        }

        [Fact]
        public void SealedStaticConstructor_02()
        {
            var source1 =
@"
partial interface I2
{
    sealed static partial I2();
}

partial interface I2
{
    static partial I2() {}
}

partial interface I3
{
    static partial I3();
}

partial interface I3
{
    sealed static partial I3() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,19): error CS0246: The type or namespace name 'partial' could not be found (are you missing a using directive or an assembly reference?)
                //     sealed static partial I2();
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "partial").WithArguments("partial").WithLocation(4, 19),
                // (4,27): error CS0501: 'I2.I2()' must declare a body because it is not marked abstract, extern, or partial
                //     sealed static partial I2();
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "I2").WithArguments("I2.I2()").WithLocation(4, 27),
                // (4,27): error CS0542: 'I2': member names cannot be the same as their enclosing type
                //     sealed static partial I2();
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "I2").WithArguments("I2").WithLocation(4, 27),
                // (9,12): error CS0246: The type or namespace name 'partial' could not be found (are you missing a using directive or an assembly reference?)
                //     static partial I2() {}
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "partial").WithArguments("partial").WithLocation(9, 12),
                // (9,20): error CS0542: 'I2': member names cannot be the same as their enclosing type
                //     static partial I2() {}
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "I2").WithArguments("I2").WithLocation(9, 20),
                // (9,20): error CS0111: Type 'I2' already defines a member called 'I2' with the same parameter types
                //     static partial I2() {}
                Diagnostic(ErrorCode.ERR_MemberAlreadyExists, "I2").WithArguments("I2", "I2").WithLocation(9, 20),
                // (9,20): error CS0161: 'I2.I2()': not all code paths return a value
                //     static partial I2() {}
                Diagnostic(ErrorCode.ERR_ReturnExpected, "I2").WithArguments("I2.I2()").WithLocation(9, 20),
                // (14,12): error CS0246: The type or namespace name 'partial' could not be found (are you missing a using directive or an assembly reference?)
                //     static partial I3();
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "partial").WithArguments("partial").WithLocation(14, 12),
                // (14,20): error CS0501: 'I3.I3()' must declare a body because it is not marked abstract, extern, or partial
                //     static partial I3();
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "I3").WithArguments("I3.I3()").WithLocation(14, 20),
                // (14,20): error CS0542: 'I3': member names cannot be the same as their enclosing type
                //     static partial I3();
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "I3").WithArguments("I3").WithLocation(14, 20),
                // (19,19): error CS0246: The type or namespace name 'partial' could not be found (are you missing a using directive or an assembly reference?)
                //     sealed static partial I3() {}
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "partial").WithArguments("partial").WithLocation(19, 19),
                // (19,27): error CS0542: 'I3': member names cannot be the same as their enclosing type
                //     sealed static partial I3() {}
                Diagnostic(ErrorCode.ERR_MemberNameSameAsType, "I3").WithArguments("I3").WithLocation(19, 27),
                // (19,27): error CS0111: Type 'I3' already defines a member called 'I3' with the same parameter types
                //     sealed static partial I3() {}
                Diagnostic(ErrorCode.ERR_MemberAlreadyExists, "I3").WithArguments("I3", "I3").WithLocation(19, 27),
                // (19,27): error CS0161: 'I3.I3()': not all code paths return a value
                //     sealed static partial I3() {}
                Diagnostic(ErrorCode.ERR_ReturnExpected, "I3").WithArguments("I3.I3()").WithLocation(19, 27)
                );
        }

        [Fact]
        public void AbstractStaticConstructor_01()
        {
            var source1 =
@"
interface I1
{
    abstract static I1();
}

interface I2
{
    abstract static I2() {}
}

interface I3
{
    static I3();
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,21): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static I1();
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "I1").WithArguments("abstract").WithLocation(4, 21),
                // (9,21): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static I2() {}
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "I2").WithArguments("abstract").WithLocation(9, 21),
                // (14,12): error CS0501: 'I3.I3()' must declare a body because it is not marked abstract, extern, or partial
                //     static I3();
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "I3").WithArguments("I3.I3()").WithLocation(14, 12)
                );

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>(".cctor");

            Assert.False(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.False(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));
        }

        [Fact]
        public void PartialSealedStatic_01()
        {
            var source1 =
@"
partial interface I1
{
    sealed static partial void M01();
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("M01");

            Assert.False(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.False(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialDefinition());
            Assert.Null(m01.PartialImplementationPart);
        }

        [Fact]
        public void PartialSealedStatic_02()
        {
            var source1 =
@"
partial interface I1
{
    sealed static partial void M01();
}
partial interface I1
{
    sealed static partial void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            ValidatePartialSealedStatic_02(compilation1);
        }

        private static void ValidatePartialSealedStatic_02(CSharpCompilation compilation1)
        {
            compilation1.VerifyDiagnostics();

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("M01");

            Assert.False(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.False(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialDefinition());
            Assert.Same(m01, m01.PartialImplementationPart.PartialDefinitionPart);

            m01 = m01.PartialImplementationPart;

            Assert.False(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.False(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialImplementation());
        }

        [Fact]
        public void PartialSealedStatic_03()
        {
            var source1 =
@"
partial interface I1
{
    static partial void M01();
}
partial interface I1
{
    sealed static partial void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            ValidatePartialSealedStatic_02(compilation1);
        }

        [Fact]
        public void PartialSealedStatic_04()
        {
            var source1 =
@"
partial interface I1
{
    sealed static partial void M01();
}
partial interface I1
{
    static partial void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            ValidatePartialSealedStatic_02(compilation1);
        }

        [Fact]
        public void PartialAbstractStatic_01()
        {
            var source1 =
@"
partial interface I1
{
    abstract static partial void M01();
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,34): error CS0750: A partial method cannot have the 'abstract' modifier
                //     abstract static partial void M01();
                Diagnostic(ErrorCode.ERR_PartialMethodInvalidModifier, "M01").WithLocation(4, 34)
                );

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("M01");

            Assert.True(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.True(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialDefinition());
            Assert.Null(m01.PartialImplementationPart);
        }

        [Fact]
        public void PartialAbstractStatic_02()
        {
            var source1 =
@"
partial interface I1
{
    abstract static partial void M01();
}
partial interface I1
{
    abstract static partial void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,34): error CS0750: A partial method cannot have the 'abstract' modifier
                //     abstract static partial void M01();
                Diagnostic(ErrorCode.ERR_PartialMethodInvalidModifier, "M01").WithLocation(4, 34),
                // (8,34): error CS0500: 'I1.M01()' cannot declare a body because it is marked abstract
                //     abstract static partial void M01() {}
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M01").WithArguments("I1.M01()").WithLocation(8, 34),
                // (8,34): error CS0750: A partial method cannot have the 'abstract' modifier
                //     abstract static partial void M01() {}
                Diagnostic(ErrorCode.ERR_PartialMethodInvalidModifier, "M01").WithLocation(8, 34)
                );

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("M01");

            Assert.True(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.True(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialDefinition());
            Assert.Same(m01, m01.PartialImplementationPart.PartialDefinitionPart);

            m01 = m01.PartialImplementationPart;

            Assert.True(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.True(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialImplementation());
        }

        [Fact]
        public void PartialAbstractStatic_03()
        {
            var source1 =
@"
partial interface I1
{
    abstract static partial void M01();
}
partial interface I1
{
    static partial void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,34): error CS0750: A partial method cannot have the 'abstract' modifier
                //     abstract static partial void M01();
                Diagnostic(ErrorCode.ERR_PartialMethodInvalidModifier, "M01").WithLocation(4, 34)
                );

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("M01");

            Assert.True(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.True(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialDefinition());
            Assert.Same(m01, m01.PartialImplementationPart.PartialDefinitionPart);

            m01 = m01.PartialImplementationPart;

            Assert.False(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.False(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialImplementation());
        }

        [Fact]
        public void PartialAbstractStatic_04()
        {
            var source1 =
@"
partial interface I1
{
    static partial void M01();
}
partial interface I1
{
    abstract static partial void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,34): error CS0500: 'I1.M01()' cannot declare a body because it is marked abstract
                //     abstract static partial void M01() {}
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "M01").WithArguments("I1.M01()").WithLocation(8, 34),
                // (8,34): error CS0750: A partial method cannot have the 'abstract' modifier
                //     abstract static partial void M01() {}
                Diagnostic(ErrorCode.ERR_PartialMethodInvalidModifier, "M01").WithLocation(8, 34)
                );

            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("M01");

            Assert.False(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.False(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialDefinition());
            Assert.Same(m01, m01.PartialImplementationPart.PartialDefinitionPart);

            m01 = m01.PartialImplementationPart;

            Assert.True(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.True(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            Assert.True(m01.IsPartialImplementation());
        }

        [Fact]
        public void PrivateAbstractStatic_01()
        {
            var source1 =
@"
interface I1
{
    private abstract static void M01();
    private abstract static bool P01 { get; }
    private abstract static event System.Action E01;
    private abstract static I1 operator+ (I1 x);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,34): error CS0621: 'I1.M01()': virtual or abstract members cannot be private
                //     private abstract static void M01();
                Diagnostic(ErrorCode.ERR_VirtualPrivate, "M01").WithArguments("I1.M01()").WithLocation(4, 34),
                // (5,34): error CS0621: 'I1.P01': virtual or abstract members cannot be private
                //     private abstract static bool P01 { get; }
                Diagnostic(ErrorCode.ERR_VirtualPrivate, "P01").WithArguments("I1.P01").WithLocation(5, 34),
                // (6,49): error CS0621: 'I1.E01': virtual or abstract members cannot be private
                //     private abstract static event System.Action E01;
                Diagnostic(ErrorCode.ERR_VirtualPrivate, "E01").WithArguments("I1.E01").WithLocation(6, 49),
                // (7,40): error CS0558: User-defined operator 'I1.operator +(I1)' must be declared static and public
                //     private abstract static I1 operator+ (I1 x);
                Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, "+").WithArguments("I1.operator +(I1)").WithLocation(7, 40)
                );
        }

        [Fact]
        public void PropertyModifiers_01()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool M01 { get
    ; } 

    virtual static bool M02 { get
    ; } 

    sealed static bool M03 { get
    ; } 

    override static bool M04 { get
    ; } 

    abstract virtual static bool M05 { get
    ; } 

    abstract sealed static bool M06 { get
    ; } 

    abstract override static bool M07 { get
    ; } 

    virtual sealed static bool M08 { get
    ; } 

    virtual override static bool M09 { get
    ; } 

    sealed override static bool M10 { get
    ; } 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static bool M01 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(4, 26),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static bool M03 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "9.0", "preview").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "9.0", "preview").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "9.0", "preview").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "9.0", "preview").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "9.0", "preview").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "9.0", "preview").WithLocation(31, 33)
                );

            ValidatePropertyModifiers_01(compilation1);
        }

        private static void ValidatePropertyModifiers_01(CSharpCompilation compilation1)
        {
            var i1 = compilation1.GetTypeByMetadataName("I1");

            {
                var m01 = i1.GetMember<PropertySymbol>("M01");

                Assert.True(m01.IsAbstract);
                Assert.False(m01.IsVirtual);
                Assert.False(m01.IsSealed);
                Assert.True(m01.IsStatic);
                Assert.False(m01.IsExtern);
                Assert.False(m01.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));

                var m02 = i1.GetMember<PropertySymbol>("M02");

                Assert.False(m02.IsAbstract);
                Assert.False(m02.IsVirtual);
                Assert.False(m02.IsSealed);
                Assert.True(m02.IsStatic);
                Assert.False(m02.IsExtern);
                Assert.False(m02.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m02));

                var m03 = i1.GetMember<PropertySymbol>("M03");

                Assert.False(m03.IsAbstract);
                Assert.False(m03.IsVirtual);
                Assert.False(m03.IsSealed);
                Assert.True(m03.IsStatic);
                Assert.False(m03.IsExtern);
                Assert.False(m03.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m03));

                var m04 = i1.GetMember<PropertySymbol>("M04");

                Assert.False(m04.IsAbstract);
                Assert.False(m04.IsVirtual);
                Assert.False(m04.IsSealed);
                Assert.True(m04.IsStatic);
                Assert.False(m04.IsExtern);
                Assert.False(m04.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m04));

                var m05 = i1.GetMember<PropertySymbol>("M05");

                Assert.True(m05.IsAbstract);
                Assert.False(m05.IsVirtual);
                Assert.False(m05.IsSealed);
                Assert.True(m05.IsStatic);
                Assert.False(m05.IsExtern);
                Assert.False(m05.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m05));

                var m06 = i1.GetMember<PropertySymbol>("M06");

                Assert.True(m06.IsAbstract);
                Assert.False(m06.IsVirtual);
                Assert.False(m06.IsSealed);
                Assert.True(m06.IsStatic);
                Assert.False(m06.IsExtern);
                Assert.False(m06.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m06));

                var m07 = i1.GetMember<PropertySymbol>("M07");

                Assert.True(m07.IsAbstract);
                Assert.False(m07.IsVirtual);
                Assert.False(m07.IsSealed);
                Assert.True(m07.IsStatic);
                Assert.False(m07.IsExtern);
                Assert.False(m07.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m07));

                var m08 = i1.GetMember<PropertySymbol>("M08");

                Assert.False(m08.IsAbstract);
                Assert.False(m08.IsVirtual);
                Assert.False(m08.IsSealed);
                Assert.True(m08.IsStatic);
                Assert.False(m08.IsExtern);
                Assert.False(m08.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m08));

                var m09 = i1.GetMember<PropertySymbol>("M09");

                Assert.False(m09.IsAbstract);
                Assert.False(m09.IsVirtual);
                Assert.False(m09.IsSealed);
                Assert.True(m09.IsStatic);
                Assert.False(m09.IsExtern);
                Assert.False(m09.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m09));

                var m10 = i1.GetMember<PropertySymbol>("M10");

                Assert.False(m10.IsAbstract);
                Assert.False(m10.IsVirtual);
                Assert.False(m10.IsSealed);
                Assert.True(m10.IsStatic);
                Assert.False(m10.IsExtern);
                Assert.False(m10.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m10));
            }
            {
                var m01 = i1.GetMember<PropertySymbol>("M01").GetMethod;

                Assert.True(m01.IsAbstract);
                Assert.False(m01.IsVirtual);
                Assert.True(m01.IsMetadataVirtual());
                Assert.False(m01.IsSealed);
                Assert.True(m01.IsStatic);
                Assert.False(m01.IsExtern);
                Assert.False(m01.IsAsync);
                Assert.False(m01.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));

                var m02 = i1.GetMember<PropertySymbol>("M02").GetMethod;

                Assert.False(m02.IsAbstract);
                Assert.False(m02.IsVirtual);
                Assert.False(m02.IsMetadataVirtual());
                Assert.False(m02.IsSealed);
                Assert.True(m02.IsStatic);
                Assert.False(m02.IsExtern);
                Assert.False(m02.IsAsync);
                Assert.False(m02.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m02));

                var m03 = i1.GetMember<PropertySymbol>("M03").GetMethod;

                Assert.False(m03.IsAbstract);
                Assert.False(m03.IsVirtual);
                Assert.False(m03.IsMetadataVirtual());
                Assert.False(m03.IsSealed);
                Assert.True(m03.IsStatic);
                Assert.False(m03.IsExtern);
                Assert.False(m03.IsAsync);
                Assert.False(m03.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m03));

                var m04 = i1.GetMember<PropertySymbol>("M04").GetMethod;

                Assert.False(m04.IsAbstract);
                Assert.False(m04.IsVirtual);
                Assert.False(m04.IsMetadataVirtual());
                Assert.False(m04.IsSealed);
                Assert.True(m04.IsStatic);
                Assert.False(m04.IsExtern);
                Assert.False(m04.IsAsync);
                Assert.False(m04.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m04));

                var m05 = i1.GetMember<PropertySymbol>("M05").GetMethod;

                Assert.True(m05.IsAbstract);
                Assert.False(m05.IsVirtual);
                Assert.True(m05.IsMetadataVirtual());
                Assert.False(m05.IsSealed);
                Assert.True(m05.IsStatic);
                Assert.False(m05.IsExtern);
                Assert.False(m05.IsAsync);
                Assert.False(m05.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m05));

                var m06 = i1.GetMember<PropertySymbol>("M06").GetMethod;

                Assert.True(m06.IsAbstract);
                Assert.False(m06.IsVirtual);
                Assert.True(m06.IsMetadataVirtual());
                Assert.False(m06.IsSealed);
                Assert.True(m06.IsStatic);
                Assert.False(m06.IsExtern);
                Assert.False(m06.IsAsync);
                Assert.False(m06.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m06));

                var m07 = i1.GetMember<PropertySymbol>("M07").GetMethod;

                Assert.True(m07.IsAbstract);
                Assert.False(m07.IsVirtual);
                Assert.True(m07.IsMetadataVirtual());
                Assert.False(m07.IsSealed);
                Assert.True(m07.IsStatic);
                Assert.False(m07.IsExtern);
                Assert.False(m07.IsAsync);
                Assert.False(m07.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m07));

                var m08 = i1.GetMember<PropertySymbol>("M08").GetMethod;

                Assert.False(m08.IsAbstract);
                Assert.False(m08.IsVirtual);
                Assert.False(m08.IsMetadataVirtual());
                Assert.False(m08.IsSealed);
                Assert.True(m08.IsStatic);
                Assert.False(m08.IsExtern);
                Assert.False(m08.IsAsync);
                Assert.False(m08.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m08));

                var m09 = i1.GetMember<PropertySymbol>("M09").GetMethod;

                Assert.False(m09.IsAbstract);
                Assert.False(m09.IsVirtual);
                Assert.False(m09.IsMetadataVirtual());
                Assert.False(m09.IsSealed);
                Assert.True(m09.IsStatic);
                Assert.False(m09.IsExtern);
                Assert.False(m09.IsAsync);
                Assert.False(m09.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m09));

                var m10 = i1.GetMember<PropertySymbol>("M10").GetMethod;

                Assert.False(m10.IsAbstract);
                Assert.False(m10.IsVirtual);
                Assert.False(m10.IsMetadataVirtual());
                Assert.False(m10.IsSealed);
                Assert.True(m10.IsStatic);
                Assert.False(m10.IsExtern);
                Assert.False(m10.IsAsync);
                Assert.False(m10.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m10));
            }
        }

        [Fact]
        public void PropertyModifiers_02()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool M01 { get
    => throw null; } 

    virtual static bool M02 { get
    => throw null; } 

    sealed static bool M03 { get
    => throw null; } 

    override static bool M04 { get
    => throw null; } 

    abstract virtual static bool M05 { get
    { throw null; } } 

    abstract sealed static bool M06 { get
    => throw null; } 

    abstract override static bool M07 { get
    => throw null; } 

    virtual sealed static bool M08 { get
    => throw null; } 

    virtual override static bool M09 { get
    => throw null; } 

    sealed override static bool M10 { get
    => throw null; } 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static bool M01 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(4, 26),
                // (4,32): error CS0500: 'I1.M01.get' cannot declare a body because it is marked abstract
                //     abstract static bool M01 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M01.get").WithLocation(4, 32),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static bool M03 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "9.0", "preview").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "9.0", "preview").WithLocation(16, 34),
                // (16,40): error CS0500: 'I1.M05.get' cannot declare a body because it is marked abstract
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M05.get").WithLocation(16, 40),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "9.0", "preview").WithLocation(19, 33),
                // (19,39): error CS0500: 'I1.M06.get' cannot declare a body because it is marked abstract
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M06.get").WithLocation(19, 39),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "9.0", "preview").WithLocation(22, 35),
                // (22,41): error CS0500: 'I1.M07.get' cannot declare a body because it is marked abstract
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M07.get").WithLocation(22, 41),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "9.0", "preview").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "9.0", "preview").WithLocation(31, 33)
                );

            ValidatePropertyModifiers_01(compilation1);
        }

        [Fact]
        public void PropertyModifiers_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool M01 { get
    ; } 

    virtual static bool M02 { get
    ; } 

    sealed static bool M03 { get
    ; } 

    override static bool M04 { get
    ; } 

    abstract virtual static bool M05 { get
    ; } 

    abstract sealed static bool M06 { get
    ; } 

    abstract override static bool M07 { get
    ; } 

    virtual sealed static bool M08 { get
    ; } 

    virtual override static bool M09 { get
    ; } 

    sealed override static bool M10 { get
    ; } 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33)
                );

            ValidatePropertyModifiers_01(compilation1);
        }

        [Fact]
        public void PropertyModifiers_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool M01 { get
    => throw null; } 

    virtual static bool M02 { get
    => throw null; } 

    sealed static bool M03 { get
    => throw null; } 

    override static bool M04 { get
    => throw null; } 

    abstract virtual static bool M05 { get
    { throw null; } } 

    abstract sealed static bool M06 { get
    => throw null; } 

    abstract override static bool M07 { get
    => throw null; } 

    virtual sealed static bool M08 { get
    => throw null; } 

    virtual override static bool M09 { get
    => throw null; } 

    sealed override static bool M10 { get
    => throw null; } 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,32): error CS0500: 'I1.M01.get' cannot declare a body because it is marked abstract
                //     abstract static bool M01 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M01.get").WithLocation(4, 32),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,40): error CS0500: 'I1.M05.get' cannot declare a body because it is marked abstract
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M05.get").WithLocation(16, 40),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,39): error CS0500: 'I1.M06.get' cannot declare a body because it is marked abstract
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M06.get").WithLocation(19, 39),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,41): error CS0500: 'I1.M07.get' cannot declare a body because it is marked abstract
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M07.get").WithLocation(22, 41),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33)
                );

            ValidatePropertyModifiers_01(compilation1);
        }

        [Fact]
        public void PropertyModifiers_05()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool M01 { get
    ; } 

    virtual static bool M02 { get
    ; } 

    sealed static bool M03 { get
    ; } 

    override static bool M04 { get
    ; } 

    abstract virtual static bool M05 { get
    ; } 

    abstract sealed static bool M06 { get
    ; } 

    abstract override static bool M07 { get
    ; } 

    virtual sealed static bool M08 { get
    ; } 

    virtual override static bool M09 { get
    ; } 

    sealed override static bool M10 { get
    ; } 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static bool M01 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "7.3", "preview").WithLocation(4, 26),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (7,25): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M02").WithArguments("static", "7.3", "8.0").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static bool M03 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "7.3", "preview").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (13,26): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M04").WithArguments("static", "7.3", "8.0").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "7.3", "preview").WithLocation(16, 34),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "7.3", "preview").WithLocation(19, 33),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "7.3", "preview").WithLocation(22, 35),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "7.3", "preview").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (28,34): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M09").WithArguments("static", "7.3", "8.0").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "7.3", "preview").WithLocation(31, 33)
                );

            ValidatePropertyModifiers_01(compilation1);
        }

        [Fact]
        public void PropertyModifiers_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool M01 { get
    => throw null; } 

    virtual static bool M02 { get
    => throw null; } 

    sealed static bool M03 { get
    => throw null; } 

    override static bool M04 { get
    => throw null; } 

    abstract virtual static bool M05 { get
    { throw null; } } 

    abstract sealed static bool M06 { get
    => throw null; } 

    abstract override static bool M07 { get
    => throw null; } 

    virtual sealed static bool M08 { get
    => throw null; } 

    virtual override static bool M09 { get
    => throw null; } 

    sealed override static bool M10 { get
    => throw null; } 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static bool M01 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "7.3", "preview").WithLocation(4, 26),
                // (4,32): error CS0500: 'I1.M01.get' cannot declare a body because it is marked abstract
                //     abstract static bool M01 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M01.get").WithLocation(4, 32),
                // (7,25): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 25),
                // (7,25): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual static bool M02 { get
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M02").WithArguments("default interface implementation", "8.0").WithLocation(7, 25),
                // (10,24): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static bool M03 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "7.3", "preview").WithLocation(10, 24),
                // (13,26): error CS0106: The modifier 'override' is not valid for this item
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 26),
                // (13,26): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     override static bool M04 { get
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M04").WithArguments("default interface implementation", "8.0").WithLocation(13, 26),
                // (16,34): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 34),
                // (16,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "7.3", "preview").WithLocation(16, 34),
                // (16,40): error CS0500: 'I1.M05.get' cannot declare a body because it is marked abstract
                //     abstract virtual static bool M05 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M05.get").WithLocation(16, 40),
                // (19,33): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 33),
                // (19,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "7.3", "preview").WithLocation(19, 33),
                // (19,39): error CS0500: 'I1.M06.get' cannot declare a body because it is marked abstract
                //     abstract sealed static bool M06 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M06.get").WithLocation(19, 39),
                // (22,35): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 35),
                // (22,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "7.3", "preview").WithLocation(22, 35),
                // (22,41): error CS0500: 'I1.M07.get' cannot declare a body because it is marked abstract
                //     abstract override static bool M07 { get
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "get").WithArguments("I1.M07.get").WithLocation(22, 41),
                // (25,32): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 32),
                // (25,32): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static bool M08 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "7.3", "preview").WithLocation(25, 32),
                // (28,34): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 34),
                // (28,34): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 34),
                // (28,34): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual override static bool M09 { get
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M09").WithArguments("default interface implementation", "8.0").WithLocation(28, 34),
                // (31,33): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 33),
                // (31,33): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static bool M10 { get
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "7.3", "preview").WithLocation(31, 33)
                );

            ValidatePropertyModifiers_01(compilation1);
        }

        [Fact]
        public void EventModifiers_01()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
public interface I1
{
    abstract static event D M01
    ;

    virtual static event D M02
    ;

    sealed static event D M03
    ;

    override static event D M04
    ;

    abstract virtual static event D M05
    ;

    abstract sealed static event D M06
    ;

    abstract override static event D M07
    ;

    virtual sealed static event D M08
    ;

    virtual override static event D M09
    ;

    sealed override static event D M10
    ;
}

public delegate void D();
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,29): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static event D M01
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(4, 29),
                // (7,28): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static event D M02
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 28),
                // (10,27): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static event D M03
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "9.0", "preview").WithLocation(10, 27),
                // (13,29): error CS0106: The modifier 'override' is not valid for this item
                //     override static event D M04
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 29),
                // (16,37): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static event D M05
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 37),
                // (16,37): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static event D M05
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "9.0", "preview").WithLocation(16, 37),
                // (19,36): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static event D M06
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 36),
                // (19,36): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static event D M06
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "9.0", "preview").WithLocation(19, 36),
                // (22,38): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static event D M07
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 38),
                // (22,38): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static event D M07
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "9.0", "preview").WithLocation(22, 38),
                // (25,35): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static event D M08
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 35),
                // (25,35): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static event D M08
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "9.0", "preview").WithLocation(25, 35),
                // (28,37): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static event D M09
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 37),
                // (28,37): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static event D M09
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 37),
                // (31,36): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static event D M10
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 36),
                // (31,36): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static event D M10
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "9.0", "preview").WithLocation(31, 36)
                );

            ValidateEventModifiers_01(compilation1);
        }

        private static void ValidateEventModifiers_01(CSharpCompilation compilation1)
        {
            var i1 = compilation1.GetTypeByMetadataName("I1");

            {
                var m01 = i1.GetMember<EventSymbol>("M01");

                Assert.True(m01.IsAbstract);
                Assert.False(m01.IsVirtual);
                Assert.False(m01.IsSealed);
                Assert.True(m01.IsStatic);
                Assert.False(m01.IsExtern);
                Assert.False(m01.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));

                var m02 = i1.GetMember<EventSymbol>("M02");

                Assert.False(m02.IsAbstract);
                Assert.False(m02.IsVirtual);
                Assert.False(m02.IsSealed);
                Assert.True(m02.IsStatic);
                Assert.False(m02.IsExtern);
                Assert.False(m02.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m02));

                var m03 = i1.GetMember<EventSymbol>("M03");

                Assert.False(m03.IsAbstract);
                Assert.False(m03.IsVirtual);
                Assert.False(m03.IsSealed);
                Assert.True(m03.IsStatic);
                Assert.False(m03.IsExtern);
                Assert.False(m03.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m03));

                var m04 = i1.GetMember<EventSymbol>("M04");

                Assert.False(m04.IsAbstract);
                Assert.False(m04.IsVirtual);
                Assert.False(m04.IsSealed);
                Assert.True(m04.IsStatic);
                Assert.False(m04.IsExtern);
                Assert.False(m04.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m04));

                var m05 = i1.GetMember<EventSymbol>("M05");

                Assert.True(m05.IsAbstract);
                Assert.False(m05.IsVirtual);
                Assert.False(m05.IsSealed);
                Assert.True(m05.IsStatic);
                Assert.False(m05.IsExtern);
                Assert.False(m05.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m05));

                var m06 = i1.GetMember<EventSymbol>("M06");

                Assert.True(m06.IsAbstract);
                Assert.False(m06.IsVirtual);
                Assert.False(m06.IsSealed);
                Assert.True(m06.IsStatic);
                Assert.False(m06.IsExtern);
                Assert.False(m06.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m06));

                var m07 = i1.GetMember<EventSymbol>("M07");

                Assert.True(m07.IsAbstract);
                Assert.False(m07.IsVirtual);
                Assert.False(m07.IsSealed);
                Assert.True(m07.IsStatic);
                Assert.False(m07.IsExtern);
                Assert.False(m07.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m07));

                var m08 = i1.GetMember<EventSymbol>("M08");

                Assert.False(m08.IsAbstract);
                Assert.False(m08.IsVirtual);
                Assert.False(m08.IsSealed);
                Assert.True(m08.IsStatic);
                Assert.False(m08.IsExtern);
                Assert.False(m08.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m08));

                var m09 = i1.GetMember<EventSymbol>("M09");

                Assert.False(m09.IsAbstract);
                Assert.False(m09.IsVirtual);
                Assert.False(m09.IsSealed);
                Assert.True(m09.IsStatic);
                Assert.False(m09.IsExtern);
                Assert.False(m09.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m09));

                var m10 = i1.GetMember<EventSymbol>("M10");

                Assert.False(m10.IsAbstract);
                Assert.False(m10.IsVirtual);
                Assert.False(m10.IsSealed);
                Assert.True(m10.IsStatic);
                Assert.False(m10.IsExtern);
                Assert.False(m10.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m10));
            }

            foreach (var addAccessor in new[] { true, false })
            {
                var m01 = getAccessor(i1.GetMember<EventSymbol>("M01"), addAccessor);

                Assert.True(m01.IsAbstract);
                Assert.False(m01.IsVirtual);
                Assert.True(m01.IsMetadataVirtual());
                Assert.False(m01.IsSealed);
                Assert.True(m01.IsStatic);
                Assert.False(m01.IsExtern);
                Assert.False(m01.IsAsync);
                Assert.False(m01.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));

                var m02 = getAccessor(i1.GetMember<EventSymbol>("M02"), addAccessor);

                Assert.False(m02.IsAbstract);
                Assert.False(m02.IsVirtual);
                Assert.False(m02.IsMetadataVirtual());
                Assert.False(m02.IsSealed);
                Assert.True(m02.IsStatic);
                Assert.False(m02.IsExtern);
                Assert.False(m02.IsAsync);
                Assert.False(m02.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m02));

                var m03 = getAccessor(i1.GetMember<EventSymbol>("M03"), addAccessor);

                Assert.False(m03.IsAbstract);
                Assert.False(m03.IsVirtual);
                Assert.False(m03.IsMetadataVirtual());
                Assert.False(m03.IsSealed);
                Assert.True(m03.IsStatic);
                Assert.False(m03.IsExtern);
                Assert.False(m03.IsAsync);
                Assert.False(m03.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m03));

                var m04 = getAccessor(i1.GetMember<EventSymbol>("M04"), addAccessor);

                Assert.False(m04.IsAbstract);
                Assert.False(m04.IsVirtual);
                Assert.False(m04.IsMetadataVirtual());
                Assert.False(m04.IsSealed);
                Assert.True(m04.IsStatic);
                Assert.False(m04.IsExtern);
                Assert.False(m04.IsAsync);
                Assert.False(m04.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m04));

                var m05 = getAccessor(i1.GetMember<EventSymbol>("M05"), addAccessor);

                Assert.True(m05.IsAbstract);
                Assert.False(m05.IsVirtual);
                Assert.True(m05.IsMetadataVirtual());
                Assert.False(m05.IsSealed);
                Assert.True(m05.IsStatic);
                Assert.False(m05.IsExtern);
                Assert.False(m05.IsAsync);
                Assert.False(m05.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m05));

                var m06 = getAccessor(i1.GetMember<EventSymbol>("M06"), addAccessor);

                Assert.True(m06.IsAbstract);
                Assert.False(m06.IsVirtual);
                Assert.True(m06.IsMetadataVirtual());
                Assert.False(m06.IsSealed);
                Assert.True(m06.IsStatic);
                Assert.False(m06.IsExtern);
                Assert.False(m06.IsAsync);
                Assert.False(m06.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m06));

                var m07 = getAccessor(i1.GetMember<EventSymbol>("M07"), addAccessor);

                Assert.True(m07.IsAbstract);
                Assert.False(m07.IsVirtual);
                Assert.True(m07.IsMetadataVirtual());
                Assert.False(m07.IsSealed);
                Assert.True(m07.IsStatic);
                Assert.False(m07.IsExtern);
                Assert.False(m07.IsAsync);
                Assert.False(m07.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m07));

                var m08 = getAccessor(i1.GetMember<EventSymbol>("M08"), addAccessor);

                Assert.False(m08.IsAbstract);
                Assert.False(m08.IsVirtual);
                Assert.False(m08.IsMetadataVirtual());
                Assert.False(m08.IsSealed);
                Assert.True(m08.IsStatic);
                Assert.False(m08.IsExtern);
                Assert.False(m08.IsAsync);
                Assert.False(m08.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m08));

                var m09 = getAccessor(i1.GetMember<EventSymbol>("M09"), addAccessor);

                Assert.False(m09.IsAbstract);
                Assert.False(m09.IsVirtual);
                Assert.False(m09.IsMetadataVirtual());
                Assert.False(m09.IsSealed);
                Assert.True(m09.IsStatic);
                Assert.False(m09.IsExtern);
                Assert.False(m09.IsAsync);
                Assert.False(m09.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m09));

                var m10 = getAccessor(i1.GetMember<EventSymbol>("M10"), addAccessor);

                Assert.False(m10.IsAbstract);
                Assert.False(m10.IsVirtual);
                Assert.False(m10.IsMetadataVirtual());
                Assert.False(m10.IsSealed);
                Assert.True(m10.IsStatic);
                Assert.False(m10.IsExtern);
                Assert.False(m10.IsAsync);
                Assert.False(m10.IsOverride);
                Assert.Null(i1.FindImplementationForInterfaceMember(m10));
            }

            static MethodSymbol getAccessor(EventSymbol e, bool addAccessor)
            {
                return addAccessor ? e.AddMethod : e.RemoveMethod;
            }
        }

        [Fact]
        public void EventModifiers_02()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
public interface I1
{
    abstract static event D M01 { add {} remove {} }
    

    virtual static event D M02 { add {} remove {} }
    

    sealed static event D M03 { add {} remove {} }
    

    override static event D M04 { add {} remove {} }
    

    abstract virtual static event D M05 { add {} remove {} }
    

    abstract sealed static event D M06 { add {} remove {} }
    

    abstract override static event D M07 { add {} remove {} }
    

    virtual sealed static event D M08 { add {} remove {} }
    

    virtual override static event D M09 { add {} remove {} }
    

    sealed override static event D M10 { add {} remove {} }
}

public delegate void D();
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,29): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static event D M01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(4, 29),
                // (4,33): error CS8712: 'I1.M01': abstract event cannot use event accessor syntax
                //     abstract static event D M01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M01").WithLocation(4, 33),
                // (7,28): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static event D M02 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 28),
                // (10,27): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static event D M03 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "9.0", "preview").WithLocation(10, 27),
                // (13,29): error CS0106: The modifier 'override' is not valid for this item
                //     override static event D M04 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 29),
                // (16,37): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 37),
                // (16,37): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "9.0", "preview").WithLocation(16, 37),
                // (16,41): error CS8712: 'I1.M05': abstract event cannot use event accessor syntax
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M05").WithLocation(16, 41),
                // (19,36): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 36),
                // (19,36): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "9.0", "preview").WithLocation(19, 36),
                // (19,40): error CS8712: 'I1.M06': abstract event cannot use event accessor syntax
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M06").WithLocation(19, 40),
                // (22,38): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 38),
                // (22,38): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "9.0", "preview").WithLocation(22, 38),
                // (22,42): error CS8712: 'I1.M07': abstract event cannot use event accessor syntax
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M07").WithLocation(22, 42),
                // (25,35): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static event D M08 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 35),
                // (25,35): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static event D M08 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "9.0", "preview").WithLocation(25, 35),
                // (28,37): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static event D M09 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 37),
                // (28,37): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static event D M09 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 37),
                // (31,36): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static event D M10 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 36),
                // (31,36): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static event D M10 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "9.0", "preview").WithLocation(31, 36)
                );

            ValidateEventModifiers_01(compilation1);
        }

        [Fact]
        public void EventModifiers_03()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
public interface I1
{
    abstract static event D M01
    ;

    virtual static event D M02
    ;

    sealed static event D M03
    ;

    override static event D M04
    ;

    abstract virtual static event D M05
    ;

    abstract sealed static event D M06
    ;

    abstract override static event D M07
    ;

    virtual sealed static event D M08
    ;

    virtual override static event D M09
    ;

    sealed override static event D M10
    ;
}

public delegate void D();
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (7,28): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static event D M02
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 28),
                // (13,29): error CS0106: The modifier 'override' is not valid for this item
                //     override static event D M04
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 29),
                // (16,37): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static event D M05
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 37),
                // (19,36): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static event D M06
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 36),
                // (22,38): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static event D M07
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 38),
                // (25,35): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static event D M08
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 35),
                // (28,37): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static event D M09
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 37),
                // (28,37): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static event D M09
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 37),
                // (31,36): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static event D M10
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 36)
                );

            ValidateEventModifiers_01(compilation1);
        }

        [Fact]
        public void EventModifiers_04()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
public interface I1
{
    abstract static event D M01 { add {} remove {} }
    

    virtual static event D M02 { add {} remove {} }
    

    sealed static event D M03 { add {} remove {} }
    

    override static event D M04 { add {} remove {} }
    

    abstract virtual static event D M05 { add {} remove {} }
    

    abstract sealed static event D M06 { add {} remove {} }
    

    abstract override static event D M07 { add {} remove {} }
    

    virtual sealed static event D M08 { add {} remove {} }
    

    virtual override static event D M09 { add {} remove {} }
    

    sealed override static event D M10 { add {} remove {} }
}

public delegate void D();
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,33): error CS8712: 'I1.M01': abstract event cannot use event accessor syntax
                //     abstract static event D M01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M01").WithLocation(4, 33),
                // (7,28): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static event D M02 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 28),
                // (13,29): error CS0106: The modifier 'override' is not valid for this item
                //     override static event D M04 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 29),
                // (16,37): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 37),
                // (16,41): error CS8712: 'I1.M05': abstract event cannot use event accessor syntax
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M05").WithLocation(16, 41),
                // (19,36): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 36),
                // (19,40): error CS8712: 'I1.M06': abstract event cannot use event accessor syntax
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M06").WithLocation(19, 40),
                // (22,38): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 38),
                // (22,42): error CS8712: 'I1.M07': abstract event cannot use event accessor syntax
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M07").WithLocation(22, 42),
                // (25,35): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static event D M08 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 35),
                // (28,37): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static event D M09 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 37),
                // (28,37): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static event D M09 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 37),
                // (31,36): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static event D M10 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 36)
                );

            ValidateEventModifiers_01(compilation1);
        }

        [Fact]
        public void EventModifiers_05()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
public interface I1
{
    abstract static event D M01
    ;

    virtual static event D M02
    ;

    sealed static event D M03
    ;

    override static event D M04
    ;

    abstract virtual static event D M05
    ;

    abstract sealed static event D M06
    ;

    abstract override static event D M07
    ;

    virtual sealed static event D M08
    ;

    virtual override static event D M09
    ;

    sealed override static event D M10
    ;
}

public delegate void D();
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,29): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static event D M01
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "7.3", "preview").WithLocation(4, 29),
                // (7,28): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static event D M02
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 28),
                // (7,28): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     virtual static event D M02
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M02").WithArguments("static", "7.3", "8.0").WithLocation(7, 28),
                // (10,27): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static event D M03
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "7.3", "preview").WithLocation(10, 27),
                // (13,29): error CS0106: The modifier 'override' is not valid for this item
                //     override static event D M04
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 29),
                // (13,29): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     override static event D M04
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M04").WithArguments("static", "7.3", "8.0").WithLocation(13, 29),
                // (16,37): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static event D M05
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 37),
                // (16,37): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static event D M05
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "7.3", "preview").WithLocation(16, 37),
                // (19,36): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static event D M06
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 36),
                // (19,36): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static event D M06
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "7.3", "preview").WithLocation(19, 36),
                // (22,38): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static event D M07
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 38),
                // (22,38): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static event D M07
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "7.3", "preview").WithLocation(22, 38),
                // (25,35): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static event D M08
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 35),
                // (25,35): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static event D M08
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "7.3", "preview").WithLocation(25, 35),
                // (28,37): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static event D M09
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 37),
                // (28,37): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static event D M09
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 37),
                // (28,37): error CS8703: The modifier 'static' is not valid for this item in C# 7.3. Please use language version '8.0' or greater.
                //     virtual override static event D M09
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M09").WithArguments("static", "7.3", "8.0").WithLocation(28, 37),
                // (31,36): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static event D M10
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 36),
                // (31,36): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static event D M10
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "7.3", "preview").WithLocation(31, 36)
                );

            ValidateEventModifiers_01(compilation1);
        }

        [Fact]
        public void EventModifiers_06()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
public interface I1
{
    abstract static event D M01 { add {} remove {} }
    

    virtual static event D M02 { add {} remove {} }
    

    sealed static event D M03 { add {} remove {} }
    

    override static event D M04 { add {} remove {} }
    

    abstract virtual static event D M05 { add {} remove {} }
    

    abstract sealed static event D M06 { add {} remove {} }
    

    abstract override static event D M07 { add {} remove {} }
    

    virtual sealed static event D M08 { add {} remove {} }
    

    virtual override static event D M09 { add {} remove {} }
    

    sealed override static event D M10 { add {} remove {} }
}

public delegate void D();
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,29): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static event D M01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "7.3", "preview").WithLocation(4, 29),
                // (4,33): error CS8712: 'I1.M01': abstract event cannot use event accessor syntax
                //     abstract static event D M01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M01").WithLocation(4, 33),
                // (7,28): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual static event D M02 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M02").WithArguments("virtual").WithLocation(7, 28),
                // (7,28): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual static event D M02 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M02").WithArguments("default interface implementation", "8.0").WithLocation(7, 28),
                // (10,27): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static event D M03 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M03").WithArguments("sealed", "7.3", "preview").WithLocation(10, 27),
                // (13,29): error CS0106: The modifier 'override' is not valid for this item
                //     override static event D M04 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M04").WithArguments("override").WithLocation(13, 29),
                // (13,29): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     override static event D M04 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M04").WithArguments("default interface implementation", "8.0").WithLocation(13, 29),
                // (16,37): error CS0112: A static member cannot be marked as 'virtual'
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M05").WithArguments("virtual").WithLocation(16, 37),
                // (16,37): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M05").WithArguments("abstract", "7.3", "preview").WithLocation(16, 37),
                // (16,41): error CS8712: 'I1.M05': abstract event cannot use event accessor syntax
                //     abstract virtual static event D M05 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M05").WithLocation(16, 41),
                // (19,36): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M06").WithArguments("sealed").WithLocation(19, 36),
                // (19,36): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M06").WithArguments("abstract", "7.3", "preview").WithLocation(19, 36),
                // (19,40): error CS8712: 'I1.M06': abstract event cannot use event accessor syntax
                //     abstract sealed static event D M06 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M06").WithLocation(19, 40),
                // (22,38): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M07").WithArguments("override").WithLocation(22, 38),
                // (22,38): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M07").WithArguments("abstract", "7.3", "preview").WithLocation(22, 38),
                // (22,42): error CS8712: 'I1.M07': abstract event cannot use event accessor syntax
                //     abstract override static event D M07 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.M07").WithLocation(22, 42),
                // (25,35): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual sealed static event D M08 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M08").WithArguments("virtual").WithLocation(25, 35),
                // (25,35): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static event D M08 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M08").WithArguments("sealed", "7.3", "preview").WithLocation(25, 35),
                // (28,37): error CS0112: A static member cannot be marked as 'virtual'
                //     virtual override static event D M09 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M09").WithArguments("virtual").WithLocation(28, 37),
                // (28,37): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static event D M09 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M09").WithArguments("override").WithLocation(28, 37),
                // (28,37): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual override static event D M09 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "M09").WithArguments("default interface implementation", "8.0").WithLocation(28, 37),
                // (31,36): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static event D M10 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M10").WithArguments("override").WithLocation(31, 36),
                // (31,36): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static event D M10 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M10").WithArguments("sealed", "7.3", "preview").WithLocation(31, 36)
                );

            ValidateEventModifiers_01(compilation1);
        }

        [Fact]
        public void OperatorModifiers_01()
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator+ (I1 x)
    ; 

    virtual static I1 operator- (I1 x)
    ; 

    sealed static I1 operator++ (I1 x)
    ; 

    override static I1 operator-- (I1 x)
    ; 

    abstract virtual static I1 operator! (I1 x)
    ; 

    abstract sealed static I1 operator~ (I1 x)
    ; 

    abstract override static I1 operator+ (I1 x, I1 y)
    ; 

    virtual sealed static I1 operator- (I1 x, I1 y)
    ; 

    virtual override static I1 operator* (I1 x, I1 y) 
    ; 

    sealed override static I1 operator/ (I1 x, I1 y)
    ; 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static I1 operator+ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "9.0", "preview").WithLocation(4, 32),
                // (7,31): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(7, 31),
                // (7,31): error CS0501: 'I1.operator -(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "-").WithArguments("I1.operator -(I1)").WithLocation(7, 31),
                // (10,30): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static I1 operator++ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "++").WithArguments("sealed", "9.0", "preview").WithLocation(10, 30),
                // (10,30): error CS0501: 'I1.operator ++(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     sealed static I1 operator++ (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "++").WithArguments("I1.operator ++(I1)").WithLocation(10, 30),
                // (13,32): error CS0106: The modifier 'override' is not valid for this item
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "--").WithArguments("override").WithLocation(13, 32),
                // (13,32): error CS0501: 'I1.operator --(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "--").WithArguments("I1.operator --(I1)").WithLocation(13, 32),
                // (16,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!").WithArguments("virtual").WithLocation(16, 40),
                // (16,40): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!").WithArguments("abstract", "9.0", "preview").WithLocation(16, 40),
                // (19,39): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "~").WithArguments("sealed").WithLocation(19, 39),
                // (19,39): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "~").WithArguments("abstract", "9.0", "preview").WithLocation(19, 39),
                // (22,41): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("override").WithLocation(22, 41),
                // (22,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "9.0", "preview").WithLocation(22, 41),
                // (25,38): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(25, 38),
                // (25,38): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "-").WithArguments("sealed", "9.0", "preview").WithLocation(25, 38),
                // (25,38): error CS0501: 'I1.operator -(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "-").WithArguments("I1.operator -(I1, I1)").WithLocation(25, 38),
                // (28,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("virtual").WithLocation(28, 40),
                // (28,40): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("override").WithLocation(28, 40),
                // (28,40): error CS0501: 'I1.operator *(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "*").WithArguments("I1.operator *(I1, I1)").WithLocation(28, 40),
                // (31,39): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "/").WithArguments("override").WithLocation(31, 39),
                // (31,39): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "/").WithArguments("sealed", "9.0", "preview").WithLocation(31, 39),
                // (31,39): error CS0501: 'I1.operator /(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "/").WithArguments("I1.operator /(I1, I1)").WithLocation(31, 39)
                );

            ValidateOperatorModifiers_01(compilation1);
        }

        private static void ValidateOperatorModifiers_01(CSharpCompilation compilation1)
        {
            var i1 = compilation1.GetTypeByMetadataName("I1");
            var m01 = i1.GetMember<MethodSymbol>("op_UnaryPlus");

            Assert.True(m01.IsAbstract);
            Assert.False(m01.IsVirtual);
            Assert.True(m01.IsMetadataVirtual());
            Assert.False(m01.IsSealed);
            Assert.True(m01.IsStatic);
            Assert.False(m01.IsExtern);
            Assert.False(m01.IsAsync);
            Assert.False(m01.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            var m02 = i1.GetMember<MethodSymbol>("op_UnaryNegation");

            Assert.False(m02.IsAbstract);
            Assert.False(m02.IsVirtual);
            Assert.False(m02.IsMetadataVirtual());
            Assert.False(m02.IsSealed);
            Assert.True(m02.IsStatic);
            Assert.False(m02.IsExtern);
            Assert.False(m02.IsAsync);
            Assert.False(m02.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m02));

            var m03 = i1.GetMember<MethodSymbol>("op_Increment");

            Assert.False(m03.IsAbstract);
            Assert.False(m03.IsVirtual);
            Assert.False(m03.IsMetadataVirtual());
            Assert.False(m03.IsSealed);
            Assert.True(m03.IsStatic);
            Assert.False(m03.IsExtern);
            Assert.False(m03.IsAsync);
            Assert.False(m03.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m03));

            var m04 = i1.GetMember<MethodSymbol>("op_Decrement");

            Assert.False(m04.IsAbstract);
            Assert.False(m04.IsVirtual);
            Assert.False(m04.IsMetadataVirtual());
            Assert.False(m04.IsSealed);
            Assert.True(m04.IsStatic);
            Assert.False(m04.IsExtern);
            Assert.False(m04.IsAsync);
            Assert.False(m04.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m04));

            var m05 = i1.GetMember<MethodSymbol>("op_LogicalNot");

            Assert.True(m05.IsAbstract);
            Assert.False(m05.IsVirtual);
            Assert.True(m05.IsMetadataVirtual());
            Assert.False(m05.IsSealed);
            Assert.True(m05.IsStatic);
            Assert.False(m05.IsExtern);
            Assert.False(m05.IsAsync);
            Assert.False(m05.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m05));

            var m06 = i1.GetMember<MethodSymbol>("op_OnesComplement");

            Assert.True(m06.IsAbstract);
            Assert.False(m06.IsVirtual);
            Assert.True(m06.IsMetadataVirtual());
            Assert.False(m06.IsSealed);
            Assert.True(m06.IsStatic);
            Assert.False(m06.IsExtern);
            Assert.False(m06.IsAsync);
            Assert.False(m06.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m06));

            var m07 = i1.GetMember<MethodSymbol>("op_Addition");

            Assert.True(m07.IsAbstract);
            Assert.False(m07.IsVirtual);
            Assert.True(m07.IsMetadataVirtual());
            Assert.False(m07.IsSealed);
            Assert.True(m07.IsStatic);
            Assert.False(m07.IsExtern);
            Assert.False(m07.IsAsync);
            Assert.False(m07.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m07));

            var m08 = i1.GetMember<MethodSymbol>("op_Subtraction");

            Assert.False(m08.IsAbstract);
            Assert.False(m08.IsVirtual);
            Assert.False(m08.IsMetadataVirtual());
            Assert.False(m08.IsSealed);
            Assert.True(m08.IsStatic);
            Assert.False(m08.IsExtern);
            Assert.False(m08.IsAsync);
            Assert.False(m08.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m08));

            var m09 = i1.GetMember<MethodSymbol>("op_Multiply");

            Assert.False(m09.IsAbstract);
            Assert.False(m09.IsVirtual);
            Assert.False(m09.IsMetadataVirtual());
            Assert.False(m09.IsSealed);
            Assert.True(m09.IsStatic);
            Assert.False(m09.IsExtern);
            Assert.False(m09.IsAsync);
            Assert.False(m09.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m09));

            var m10 = i1.GetMember<MethodSymbol>("op_Division");

            Assert.False(m10.IsAbstract);
            Assert.False(m10.IsVirtual);
            Assert.False(m10.IsMetadataVirtual());
            Assert.False(m10.IsSealed);
            Assert.True(m10.IsStatic);
            Assert.False(m10.IsExtern);
            Assert.False(m10.IsAsync);
            Assert.False(m10.IsOverride);
            Assert.Null(i1.FindImplementationForInterfaceMember(m10));
        }

        [Fact]
        public void OperatorModifiers_02()
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator+ (I1 x)
    {throw null;} 

    virtual static I1 operator- (I1 x)
    {throw null;} 

    sealed static I1 operator++ (I1 x)
    {throw null;} 

    override static I1 operator-- (I1 x)
    {throw null;} 

    abstract virtual static I1 operator! (I1 x)
    {throw null;} 

    abstract sealed static I1 operator~ (I1 x)
    {throw null;} 

    abstract override static I1 operator+ (I1 x, I1 y)
    {throw null;} 

    virtual sealed static I1 operator- (I1 x, I1 y)
    {throw null;} 

    virtual override static I1 operator* (I1 x, I1 y) 
    {throw null;} 

    sealed override static I1 operator/ (I1 x, I1 y)
    {throw null;} 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static I1 operator+ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "9.0", "preview").WithLocation(4, 32),
                // (4,32): error CS0500: 'I1.operator +(I1)' cannot declare a body because it is marked abstract
                //     abstract static I1 operator+ (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "+").WithArguments("I1.operator +(I1)").WithLocation(4, 32),
                // (7,31): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(7, 31),
                // (10,30): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed static I1 operator++ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "++").WithArguments("sealed", "9.0", "preview").WithLocation(10, 30),
                // (13,32): error CS0106: The modifier 'override' is not valid for this item
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "--").WithArguments("override").WithLocation(13, 32),
                // (16,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!").WithArguments("virtual").WithLocation(16, 40),
                // (16,40): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!").WithArguments("abstract", "9.0", "preview").WithLocation(16, 40),
                // (16,40): error CS0500: 'I1.operator !(I1)' cannot declare a body because it is marked abstract
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!").WithArguments("I1.operator !(I1)").WithLocation(16, 40),
                // (19,39): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "~").WithArguments("sealed").WithLocation(19, 39),
                // (19,39): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "~").WithArguments("abstract", "9.0", "preview").WithLocation(19, 39),
                // (19,39): error CS0500: 'I1.operator ~(I1)' cannot declare a body because it is marked abstract
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "~").WithArguments("I1.operator ~(I1)").WithLocation(19, 39),
                // (22,41): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("override").WithLocation(22, 41),
                // (22,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "9.0", "preview").WithLocation(22, 41),
                // (22,41): error CS0500: 'I1.operator +(I1, I1)' cannot declare a body because it is marked abstract
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "+").WithArguments("I1.operator +(I1, I1)").WithLocation(22, 41),
                // (25,38): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(25, 38),
                // (25,38): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "-").WithArguments("sealed", "9.0", "preview").WithLocation(25, 38),
                // (28,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("virtual").WithLocation(28, 40),
                // (28,40): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("override").WithLocation(28, 40),
                // (31,39): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "/").WithArguments("override").WithLocation(31, 39),
                // (31,39): error CS8703: The modifier 'sealed' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "/").WithArguments("sealed", "9.0", "preview").WithLocation(31, 39)
                );

            ValidateOperatorModifiers_01(compilation1);
        }

        [Fact]
        public void OperatorModifiers_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator+ (I1 x)
    ; 

    virtual static I1 operator- (I1 x)
    ; 

    sealed static I1 operator++ (I1 x)
    ; 

    override static I1 operator-- (I1 x)
    ; 

    abstract virtual static I1 operator! (I1 x)
    ; 

    abstract sealed static I1 operator~ (I1 x)
    ; 

    abstract override static I1 operator+ (I1 x, I1 y)
    ; 

    virtual sealed static I1 operator- (I1 x, I1 y)
    ; 

    virtual override static I1 operator* (I1 x, I1 y) 
    ; 

    sealed override static I1 operator/ (I1 x, I1 y)
    ; 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (7,31): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(7, 31),
                // (7,31): error CS0501: 'I1.operator -(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "-").WithArguments("I1.operator -(I1)").WithLocation(7, 31),
                // (10,30): error CS0501: 'I1.operator ++(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     sealed static I1 operator++ (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "++").WithArguments("I1.operator ++(I1)").WithLocation(10, 30),
                // (13,32): error CS0106: The modifier 'override' is not valid for this item
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "--").WithArguments("override").WithLocation(13, 32),
                // (13,32): error CS0501: 'I1.operator --(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "--").WithArguments("I1.operator --(I1)").WithLocation(13, 32),
                // (16,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!").WithArguments("virtual").WithLocation(16, 40),
                // (19,39): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "~").WithArguments("sealed").WithLocation(19, 39),
                // (22,41): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("override").WithLocation(22, 41),
                // (25,38): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(25, 38),
                // (25,38): error CS0501: 'I1.operator -(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "-").WithArguments("I1.operator -(I1, I1)").WithLocation(25, 38),
                // (28,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("virtual").WithLocation(28, 40),
                // (28,40): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("override").WithLocation(28, 40),
                // (28,40): error CS0501: 'I1.operator *(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "*").WithArguments("I1.operator *(I1, I1)").WithLocation(28, 40),
                // (31,39): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "/").WithArguments("override").WithLocation(31, 39),
                // (31,39): error CS0501: 'I1.operator /(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "/").WithArguments("I1.operator /(I1, I1)").WithLocation(31, 39)
                );

            ValidateOperatorModifiers_01(compilation1);
        }

        [Fact]
        public void OperatorModifiers_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator+ (I1 x)
    {throw null;} 

    virtual static I1 operator- (I1 x)
    {throw null;} 

    sealed static I1 operator++ (I1 x)
    {throw null;} 

    override static I1 operator-- (I1 x)
    {throw null;} 

    abstract virtual static I1 operator! (I1 x)
    {throw null;} 

    abstract sealed static I1 operator~ (I1 x)
    {throw null;} 

    abstract override static I1 operator+ (I1 x, I1 y)
    {throw null;} 

    virtual sealed static I1 operator- (I1 x, I1 y)
    {throw null;} 

    virtual override static I1 operator* (I1 x, I1 y) 
    {throw null;} 

    sealed override static I1 operator/ (I1 x, I1 y)
    {throw null;} 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,32): error CS0500: 'I1.operator +(I1)' cannot declare a body because it is marked abstract
                //     abstract static I1 operator+ (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "+").WithArguments("I1.operator +(I1)").WithLocation(4, 32),
                // (7,31): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(7, 31),
                // (13,32): error CS0106: The modifier 'override' is not valid for this item
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "--").WithArguments("override").WithLocation(13, 32),
                // (16,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!").WithArguments("virtual").WithLocation(16, 40),
                // (16,40): error CS0500: 'I1.operator !(I1)' cannot declare a body because it is marked abstract
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!").WithArguments("I1.operator !(I1)").WithLocation(16, 40),
                // (19,39): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "~").WithArguments("sealed").WithLocation(19, 39),
                // (19,39): error CS0500: 'I1.operator ~(I1)' cannot declare a body because it is marked abstract
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "~").WithArguments("I1.operator ~(I1)").WithLocation(19, 39),
                // (22,41): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("override").WithLocation(22, 41),
                // (22,41): error CS0500: 'I1.operator +(I1, I1)' cannot declare a body because it is marked abstract
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "+").WithArguments("I1.operator +(I1, I1)").WithLocation(22, 41),
                // (25,38): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(25, 38),
                // (28,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("virtual").WithLocation(28, 40),
                // (28,40): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("override").WithLocation(28, 40),
                // (31,39): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "/").WithArguments("override").WithLocation(31, 39)
                );

            ValidateOperatorModifiers_01(compilation1);
        }

        [Fact]
        public void OperatorModifiers_05()
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator+ (I1 x)
    ; 

    virtual static I1 operator- (I1 x)
    ; 

    sealed static I1 operator++ (I1 x)
    ; 

    override static I1 operator-- (I1 x)
    ; 

    abstract virtual static I1 operator! (I1 x)
    ; 

    abstract sealed static I1 operator~ (I1 x)
    ; 

    abstract override static I1 operator+ (I1 x, I1 y)
    ; 

    virtual sealed static I1 operator- (I1 x, I1 y)
    ; 

    virtual override static I1 operator* (I1 x, I1 y) 
    ; 

    sealed override static I1 operator/ (I1 x, I1 y)
    ; 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static I1 operator+ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "7.3", "preview").WithLocation(4, 32),
                // (7,31): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(7, 31),
                // (7,31): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "-").WithArguments("default interface implementation", "8.0").WithLocation(7, 31),
                // (7,31): error CS0501: 'I1.operator -(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "-").WithArguments("I1.operator -(I1)").WithLocation(7, 31),
                // (10,30): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static I1 operator++ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "++").WithArguments("sealed", "7.3", "preview").WithLocation(10, 30),
                // (10,30): error CS0501: 'I1.operator ++(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     sealed static I1 operator++ (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "++").WithArguments("I1.operator ++(I1)").WithLocation(10, 30),
                // (13,32): error CS0106: The modifier 'override' is not valid for this item
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "--").WithArguments("override").WithLocation(13, 32),
                // (13,32): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "--").WithArguments("default interface implementation", "8.0").WithLocation(13, 32),
                // (13,32): error CS0501: 'I1.operator --(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "--").WithArguments("I1.operator --(I1)").WithLocation(13, 32),
                // (16,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!").WithArguments("virtual").WithLocation(16, 40),
                // (16,40): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!").WithArguments("abstract", "7.3", "preview").WithLocation(16, 40),
                // (19,39): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "~").WithArguments("sealed").WithLocation(19, 39),
                // (19,39): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "~").WithArguments("abstract", "7.3", "preview").WithLocation(19, 39),
                // (22,41): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("override").WithLocation(22, 41),
                // (22,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "7.3", "preview").WithLocation(22, 41),
                // (25,38): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(25, 38),
                // (25,38): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "-").WithArguments("sealed", "7.3", "preview").WithLocation(25, 38),
                // (25,38): error CS0501: 'I1.operator -(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "-").WithArguments("I1.operator -(I1, I1)").WithLocation(25, 38),
                // (28,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("virtual").WithLocation(28, 40),
                // (28,40): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("override").WithLocation(28, 40),
                // (28,40): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "*").WithArguments("default interface implementation", "8.0").WithLocation(28, 40),
                // (28,40): error CS0501: 'I1.operator *(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "*").WithArguments("I1.operator *(I1, I1)").WithLocation(28, 40),
                // (31,39): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "/").WithArguments("override").WithLocation(31, 39),
                // (31,39): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "/").WithArguments("sealed", "7.3", "preview").WithLocation(31, 39),
                // (31,39): error CS0501: 'I1.operator /(I1, I1)' must declare a body because it is not marked abstract, extern, or partial
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "/").WithArguments("I1.operator /(I1, I1)").WithLocation(31, 39)
                );

            ValidateOperatorModifiers_01(compilation1);
        }

        [Fact]
        public void OperatorModifiers_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator+ (I1 x)
    {throw null;} 

    virtual static I1 operator- (I1 x)
    {throw null;} 

    sealed static I1 operator++ (I1 x)
    {throw null;} 

    override static I1 operator-- (I1 x)
    {throw null;} 

    abstract virtual static I1 operator! (I1 x)
    {throw null;} 

    abstract sealed static I1 operator~ (I1 x)
    {throw null;} 

    abstract override static I1 operator+ (I1 x, I1 y)
    {throw null;} 

    virtual sealed static I1 operator- (I1 x, I1 y)
    {throw null;} 

    virtual override static I1 operator* (I1 x, I1 y) 
    {throw null;} 

    sealed override static I1 operator/ (I1 x, I1 y)
    {throw null;} 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static I1 operator+ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "7.3", "preview").WithLocation(4, 32),
                // (4,32): error CS0500: 'I1.operator +(I1)' cannot declare a body because it is marked abstract
                //     abstract static I1 operator+ (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "+").WithArguments("I1.operator +(I1)").WithLocation(4, 32),
                // (7,31): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(7, 31),
                // (7,31): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual static I1 operator- (I1 x)
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "-").WithArguments("default interface implementation", "8.0").WithLocation(7, 31),
                // (10,30): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed static I1 operator++ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "++").WithArguments("sealed", "7.3", "preview").WithLocation(10, 30),
                // (13,32): error CS0106: The modifier 'override' is not valid for this item
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "--").WithArguments("override").WithLocation(13, 32),
                // (13,32): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     override static I1 operator-- (I1 x)
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "--").WithArguments("default interface implementation", "8.0").WithLocation(13, 32),
                // (16,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!").WithArguments("virtual").WithLocation(16, 40),
                // (16,40): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!").WithArguments("abstract", "7.3", "preview").WithLocation(16, 40),
                // (16,40): error CS0500: 'I1.operator !(I1)' cannot declare a body because it is marked abstract
                //     abstract virtual static I1 operator! (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!").WithArguments("I1.operator !(I1)").WithLocation(16, 40),
                // (19,39): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "~").WithArguments("sealed").WithLocation(19, 39),
                // (19,39): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "~").WithArguments("abstract", "7.3", "preview").WithLocation(19, 39),
                // (19,39): error CS0500: 'I1.operator ~(I1)' cannot declare a body because it is marked abstract
                //     abstract sealed static I1 operator~ (I1 x)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "~").WithArguments("I1.operator ~(I1)").WithLocation(19, 39),
                // (22,41): error CS0106: The modifier 'override' is not valid for this item
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("override").WithLocation(22, 41),
                // (22,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "+").WithArguments("abstract", "7.3", "preview").WithLocation(22, 41),
                // (22,41): error CS0500: 'I1.operator +(I1, I1)' cannot declare a body because it is marked abstract
                //     abstract override static I1 operator+ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "+").WithArguments("I1.operator +(I1, I1)").WithLocation(22, 41),
                // (25,38): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "-").WithArguments("virtual").WithLocation(25, 38),
                // (25,38): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     virtual sealed static I1 operator- (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "-").WithArguments("sealed", "7.3", "preview").WithLocation(25, 38),
                // (28,40): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("virtual").WithLocation(28, 40),
                // (28,40): error CS0106: The modifier 'override' is not valid for this item
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "*").WithArguments("override").WithLocation(28, 40),
                // (28,40): error CS8370: Feature 'default interface implementation' is not available in C# 7.3. Please use language version 8.0 or greater.
                //     virtual override static I1 operator* (I1 x, I1 y) 
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7_3, "*").WithArguments("default interface implementation", "8.0").WithLocation(28, 40),
                // (31,39): error CS0106: The modifier 'override' is not valid for this item
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "/").WithArguments("override").WithLocation(31, 39),
                // (31,39): error CS8703: The modifier 'sealed' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     sealed override static I1 operator/ (I1 x, I1 y)
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "/").WithArguments("sealed", "7.3", "preview").WithLocation(31, 39)
                );

            ValidateOperatorModifiers_01(compilation1);
        }

        [Fact]
        public void OperatorModifiers_07()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool operator== (I1 x, I1 y); 

    abstract static bool operator!= (I1 x, I1 y) {return false;} 
}

public interface I2
{
    sealed static bool operator== (I2 x, I2 y);

    sealed static bool operator!= (I2 x, I2 y) {return false;} 
}

public interface I3
{
    abstract sealed static bool operator== (I3 x, I3 y);

    abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular7_3,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static bool operator== (I1 x, I1 y); 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "==").WithArguments("abstract", "7.3", "preview").WithLocation(4, 34),
                // (6,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract static bool operator!= (I1 x, I1 y) {return false;} 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!=").WithArguments("abstract", "7.3", "preview").WithLocation(6, 34),
                // (6,34): error CS0500: 'I1.operator !=(I1, I1)' cannot declare a body because it is marked abstract
                //     abstract static bool operator!= (I1 x, I1 y) {return false;} 
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!=").WithArguments("I1.operator !=(I1, I1)").WithLocation(6, 34),
                // (11,32): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static bool operator== (I2 x, I2 y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "==").WithArguments("sealed").WithLocation(11, 32),
                // (11,32): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static bool operator== (I2 x, I2 y);
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "==").WithLocation(11, 32),
                // (13,32): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static bool operator!= (I2 x, I2 y) {return false;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!=").WithArguments("sealed").WithLocation(13, 32),
                // (13,32): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static bool operator!= (I2 x, I2 y) {return false;} 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "!=").WithLocation(13, 32),
                // (18,41): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool operator== (I3 x, I3 y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "==").WithArguments("sealed").WithLocation(18, 41),
                // (18,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static bool operator== (I3 x, I3 y);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "==").WithArguments("abstract", "7.3", "preview").WithLocation(18, 41),
                // (20,41): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!=").WithArguments("sealed").WithLocation(20, 41),
                // (20,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 7.3. Please use language version 'preview' or greater.
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!=").WithArguments("abstract", "7.3", "preview").WithLocation(20, 41),
                // (20,41): error CS0500: 'I3.operator !=(I3, I3)' cannot declare a body because it is marked abstract
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!=").WithArguments("I3.operator !=(I3, I3)").WithLocation(20, 41)
                );

            validate();

            compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static bool operator== (I1 x, I1 y); 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "==").WithArguments("abstract", "9.0", "preview").WithLocation(4, 34),
                // (6,34): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static bool operator!= (I1 x, I1 y) {return false;} 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!=").WithArguments("abstract", "9.0", "preview").WithLocation(6, 34),
                // (6,34): error CS0500: 'I1.operator !=(I1, I1)' cannot declare a body because it is marked abstract
                //     abstract static bool operator!= (I1 x, I1 y) {return false;} 
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!=").WithArguments("I1.operator !=(I1, I1)").WithLocation(6, 34),
                // (11,32): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static bool operator== (I2 x, I2 y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "==").WithArguments("sealed").WithLocation(11, 32),
                // (11,32): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static bool operator== (I2 x, I2 y);
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "==").WithLocation(11, 32),
                // (13,32): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static bool operator!= (I2 x, I2 y) {return false;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!=").WithArguments("sealed").WithLocation(13, 32),
                // (13,32): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static bool operator!= (I2 x, I2 y) {return false;} 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "!=").WithLocation(13, 32),
                // (18,41): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool operator== (I3 x, I3 y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "==").WithArguments("sealed").WithLocation(18, 41),
                // (18,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static bool operator== (I3 x, I3 y);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "==").WithArguments("abstract", "9.0", "preview").WithLocation(18, 41),
                // (20,41): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!=").WithArguments("sealed").WithLocation(20, 41),
                // (20,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "!=").WithArguments("abstract", "9.0", "preview").WithLocation(20, 41),
                // (20,41): error CS0500: 'I3.operator !=(I3, I3)' cannot declare a body because it is marked abstract
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!=").WithArguments("I3.operator !=(I3, I3)").WithLocation(20, 41)
                );

            validate();

            compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (6,34): error CS0500: 'I1.operator !=(I1, I1)' cannot declare a body because it is marked abstract
                //     abstract static bool operator!= (I1 x, I1 y) {return false;} 
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!=").WithArguments("I1.operator !=(I1, I1)").WithLocation(6, 34),
                // (11,32): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static bool operator== (I2 x, I2 y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "==").WithArguments("sealed").WithLocation(11, 32),
                // (11,32): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static bool operator== (I2 x, I2 y);
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "==").WithLocation(11, 32),
                // (13,32): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static bool operator!= (I2 x, I2 y) {return false;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!=").WithArguments("sealed").WithLocation(13, 32),
                // (13,32): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static bool operator!= (I2 x, I2 y) {return false;} 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "!=").WithLocation(13, 32),
                // (18,41): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool operator== (I3 x, I3 y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "==").WithArguments("sealed").WithLocation(18, 41),
                // (20,41): error CS0106: The modifier 'sealed' is not valid for this item
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "!=").WithArguments("sealed").WithLocation(20, 41),
                // (20,41): error CS0500: 'I3.operator !=(I3, I3)' cannot declare a body because it is marked abstract
                //     abstract sealed static bool operator!= (I3 x, I3 y) {return false;} 
                Diagnostic(ErrorCode.ERR_AbstractHasBody, "!=").WithArguments("I3.operator !=(I3, I3)").WithLocation(20, 41)
                );

            validate();

            void validate()
            {
                foreach (MethodSymbol m01 in compilation1.GetTypeByMetadataName("I1").GetMembers())
                {
                    Assert.True(m01.IsAbstract);
                    Assert.False(m01.IsVirtual);
                    Assert.True(m01.IsMetadataVirtual());
                    Assert.False(m01.IsSealed);
                    Assert.True(m01.IsStatic);
                    Assert.False(m01.IsExtern);
                    Assert.False(m01.IsAsync);
                    Assert.False(m01.IsOverride);
                    Assert.Null(m01.ContainingType.FindImplementationForInterfaceMember(m01));
                }

                foreach (MethodSymbol m01 in compilation1.GetTypeByMetadataName("I2").GetMembers())
                {
                    Assert.False(m01.IsAbstract);
                    Assert.False(m01.IsVirtual);
                    Assert.False(m01.IsMetadataVirtual());
                    Assert.False(m01.IsSealed);
                    Assert.True(m01.IsStatic);
                    Assert.False(m01.IsExtern);
                    Assert.False(m01.IsAsync);
                    Assert.False(m01.IsOverride);
                    Assert.Null(m01.ContainingType.FindImplementationForInterfaceMember(m01));
                }

                foreach (MethodSymbol m01 in compilation1.GetTypeByMetadataName("I3").GetMembers())
                {
                    Assert.True(m01.IsAbstract);
                    Assert.False(m01.IsVirtual);
                    Assert.True(m01.IsMetadataVirtual());
                    Assert.False(m01.IsSealed);
                    Assert.True(m01.IsStatic);
                    Assert.False(m01.IsExtern);
                    Assert.False(m01.IsAsync);
                    Assert.False(m01.IsOverride);
                    Assert.Null(m01.ContainingType.FindImplementationForInterfaceMember(m01));
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void OperatorModifiers_08(bool use7_3)
        {
            var source1 =
@"
public interface I1
{
    abstract static implicit operator int(I1 x); 

    abstract static explicit operator bool(I1 x) {return false;} 
}

public interface I2
{
    sealed static implicit operator int(I2 x) {return 0;} 

    sealed static explicit operator bool(I2 x) {return false;} 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: use7_3 ? TestOptions.Regular7_3 : TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,39): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     abstract static implicit operator int(I1 x); 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "int").WithLocation(4, 39),
                // (6,39): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     abstract static explicit operator bool(I1 x) {return false;} 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "bool").WithLocation(6, 39),
                // (11,37): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static implicit operator int(I2 x) {return 0;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "int").WithArguments("sealed").WithLocation(11, 37),
                // (11,37): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static implicit operator int(I2 x) {return 0;} 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "int").WithLocation(11, 37),
                // (13,37): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static explicit operator bool(I2 x) {return false;} 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "bool").WithArguments("sealed").WithLocation(13, 37),
                // (13,37): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                //     sealed static explicit operator bool(I2 x) {return false;} 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, "bool").WithLocation(13, 37)
                );
        }

        [Fact]
        public void FieldModifiers_01()
        {
            var source1 =
@"
public interface I1
{
    abstract static int F1; 
    sealed static int F2; 
    abstract int F3; 
    sealed int F4; 
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,25): error CS0681: The modifier 'abstract' is not valid on fields. Try using a property instead.
                //     abstract static int F1; 
                Diagnostic(ErrorCode.ERR_AbstractField, "F1").WithLocation(4, 25),
                // (5,23): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static int F2; 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "F2").WithArguments("sealed").WithLocation(5, 23),
                // (6,18): error CS0681: The modifier 'abstract' is not valid on fields. Try using a property instead.
                //     abstract int F3; 
                Diagnostic(ErrorCode.ERR_AbstractField, "F3").WithLocation(6, 18),
                // (6,18): error CS0525: Interfaces cannot contain instance fields
                //     abstract int F3; 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainFields, "F3").WithLocation(6, 18),
                // (7,16): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed int F4; 
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "F4").WithArguments("sealed").WithLocation(7, 16),
                // (7,16): error CS0525: Interfaces cannot contain instance fields
                //     sealed int F4; 
                Diagnostic(ErrorCode.ERR_InterfacesCantContainFields, "F4").WithLocation(7, 16)
                );
        }

        [Fact]
        public void ExternAbstractStatic_01()
        {
            var source1 =
@"
interface I1
{
    extern abstract static void M01();
    extern abstract static bool P01 { get; }
    extern abstract static event System.Action E01;
    extern abstract static I1 operator+ (I1 x);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,33): error CS0180: 'I1.M01()' cannot be both extern and abstract
                //     extern abstract static void M01();
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "M01").WithArguments("I1.M01()").WithLocation(4, 33),
                // (5,33): error CS0180: 'I1.P01' cannot be both extern and abstract
                //     extern abstract static bool P01 { get; }
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "P01").WithArguments("I1.P01").WithLocation(5, 33),
                // (6,48): error CS0180: 'I1.E01' cannot be both extern and abstract
                //     extern abstract static event System.Action E01;
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "E01").WithArguments("I1.E01").WithLocation(6, 48),
                // (7,39): error CS0180: 'I1.operator +(I1)' cannot be both extern and abstract
                //     extern abstract static I1 operator+ (I1 x);
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "+").WithArguments("I1.operator +(I1)").WithLocation(7, 39)
                );
        }

        [Fact]
        public void ExternAbstractStatic_02()
        {
            var source1 =
@"
interface I1
{
    extern abstract static void M01() {}
    extern abstract static bool P01 { get => false; }
    extern abstract static event System.Action E01 { add {} remove {} }
    extern abstract static I1 operator+ (I1 x) => null;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,33): error CS0180: 'I1.M01()' cannot be both extern and abstract
                //     extern abstract static void M01() {}
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "M01").WithArguments("I1.M01()").WithLocation(4, 33),
                // (5,33): error CS0180: 'I1.P01' cannot be both extern and abstract
                //     extern abstract static bool P01 { get => false; }
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "P01").WithArguments("I1.P01").WithLocation(5, 33),
                // (6,48): error CS0180: 'I1.E01' cannot be both extern and abstract
                //     extern abstract static event System.Action E01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "E01").WithArguments("I1.E01").WithLocation(6, 48),
                // (6,52): error CS8712: 'I1.E01': abstract event cannot use event accessor syntax
                //     extern abstract static event System.Action E01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_AbstractEventHasAccessors, "{").WithArguments("I1.E01").WithLocation(6, 52),
                // (7,39): error CS0180: 'I1.operator +(I1)' cannot be both extern and abstract
                //     extern abstract static I1 operator+ (I1 x) => null;
                Diagnostic(ErrorCode.ERR_AbstractAndExtern, "+").WithArguments("I1.operator +(I1)").WithLocation(7, 39)
                );
        }

        [Fact]
        public void ExternSealedStatic_01()
        {
            var source1 =
@"
#pragma warning disable CS0626 // Method, operator, or accessor is marked external and has no attributes on it. Consider adding a DllImport attribute to specify the external implementation.

interface I1
{
    extern sealed static void M01();
    extern sealed static bool P01 { get; }
    extern sealed static event System.Action E01;
    extern sealed static I1 operator+ (I1 x);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();
        }

        [Fact]
        public void AbstractStaticInClass_01()
        {
            var source1 =
@"
abstract class C1
{
    public abstract static void M01();
    public abstract static bool P01 { get; }
    public abstract static event System.Action E01;
    public abstract static C1 operator+ (C1 x);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,33): error CS0112: A static member cannot be marked as 'abstract'
                //     public abstract static void M01();
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M01").WithArguments("abstract").WithLocation(4, 33),
                // (5,33): error CS0112: A static member cannot be marked as 'abstract'
                //     public abstract static bool P01 { get; }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "P01").WithArguments("abstract").WithLocation(5, 33),
                // (6,48): error CS0112: A static member cannot be marked as 'abstract'
                //     public abstract static event System.Action E01;
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "E01").WithArguments("abstract").WithLocation(6, 48),
                // (7,39): error CS0106: The modifier 'abstract' is not valid for this item
                //     public abstract static C1 operator+ (C1 x);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("abstract").WithLocation(7, 39),
                // (7,39): error CS0501: 'C1.operator +(C1)' must declare a body because it is not marked abstract, extern, or partial
                //     public abstract static C1 operator+ (C1 x);
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "+").WithArguments("C1.operator +(C1)").WithLocation(7, 39)
                );
        }

        [Fact]
        public void SealedStaticInClass_01()
        {
            var source1 =
@"
class C1
{
    sealed static void M01() {}
    sealed static bool P01 { get => false; }
    sealed static event System.Action E01 { add {} remove {} }
    public sealed static C1 operator+ (C1 x) => null;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,24): error CS0238: 'C1.M01()' cannot be sealed because it is not an override
                //     sealed static void M01() {}
                Diagnostic(ErrorCode.ERR_SealedNonOverride, "M01").WithArguments("C1.M01()").WithLocation(4, 24),
                // (5,24): error CS0238: 'C1.P01' cannot be sealed because it is not an override
                //     sealed static bool P01 { get => false; }
                Diagnostic(ErrorCode.ERR_SealedNonOverride, "P01").WithArguments("C1.P01").WithLocation(5, 24),
                // (6,39): error CS0238: 'C1.E01' cannot be sealed because it is not an override
                //     sealed static event System.Action E01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_SealedNonOverride, "E01").WithArguments("C1.E01").WithLocation(6, 39),
                // (7,37): error CS0106: The modifier 'sealed' is not valid for this item
                //     public sealed static C1 operator+ (C1 x) => null;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("sealed").WithLocation(7, 37)
                );
        }

        [Fact]
        public void AbstractStaticInStruct_01()
        {
            var source1 =
@"
struct C1
{
    public abstract static void M01();
    public abstract static bool P01 { get; }
    public abstract static event System.Action E01;
    public abstract static C1 operator+ (C1 x);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,33): error CS0112: A static member cannot be marked as 'abstract'
                //     public abstract static void M01();
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "M01").WithArguments("abstract").WithLocation(4, 33),
                // (5,33): error CS0112: A static member cannot be marked as 'abstract'
                //     public abstract static bool P01 { get; }
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "P01").WithArguments("abstract").WithLocation(5, 33),
                // (6,48): error CS0112: A static member cannot be marked as 'abstract'
                //     public abstract static event System.Action E01;
                Diagnostic(ErrorCode.ERR_StaticNotVirtual, "E01").WithArguments("abstract").WithLocation(6, 48),
                // (7,39): error CS0106: The modifier 'abstract' is not valid for this item
                //     public abstract static C1 operator+ (C1 x);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("abstract").WithLocation(7, 39),
                // (7,39): error CS0501: 'C1.operator +(C1)' must declare a body because it is not marked abstract, extern, or partial
                //     public abstract static C1 operator+ (C1 x);
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, "+").WithArguments("C1.operator +(C1)").WithLocation(7, 39)
                );
        }

        [Fact]
        public void SealedStaticInStruct_01()
        {
            var source1 =
@"
struct C1
{
    sealed static void M01() {}
    sealed static bool P01 { get => false; }
    sealed static event System.Action E01 { add {} remove {} }
    public sealed static C1 operator+ (C1 x) => default;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (4,24): error CS0238: 'C1.M01()' cannot be sealed because it is not an override
                //     sealed static void M01() {}
                Diagnostic(ErrorCode.ERR_SealedNonOverride, "M01").WithArguments("C1.M01()").WithLocation(4, 24),
                // (5,24): error CS0238: 'C1.P01' cannot be sealed because it is not an override
                //     sealed static bool P01 { get => false; }
                Diagnostic(ErrorCode.ERR_SealedNonOverride, "P01").WithArguments("C1.P01").WithLocation(5, 24),
                // (6,39): error CS0238: 'C1.E01' cannot be sealed because it is not an override
                //     sealed static event System.Action E01 { add {} remove {} }
                Diagnostic(ErrorCode.ERR_SealedNonOverride, "E01").WithArguments("C1.E01").WithLocation(6, 39),
                // (7,37): error CS0106: The modifier 'sealed' is not valid for this item
                //     public sealed static C1 operator+ (C1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "+").WithArguments("sealed").WithLocation(7, 37)
                );
        }

        [Fact]
        public void DefineAbstractStaticMethod_01()
        {
            var source1 =
@"
interface I1
{
    abstract static void M01();
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();

                Assert.False(m01.IsMetadataNewSlot());
                Assert.True(m01.IsAbstract);
                Assert.True(m01.IsMetadataVirtual());
                Assert.False(m01.IsMetadataFinal);
                Assert.False(m01.IsVirtual);
                Assert.False(m01.IsSealed);
                Assert.True(m01.IsStatic);
                Assert.False(m01.IsOverride);
            }
        }

        [Fact]
        public void DefineAbstractStaticMethod_02()
        {
            var source1 =
@"
interface I1
{
    abstract static void M01();
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation1.VerifyDiagnostics(
                // (4,26): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(4, 26)
                );
        }

        [Theory]
        [InlineData("I1", "+", "(I1 x)")]
        [InlineData("I1", "-", "(I1 x)")]
        [InlineData("I1", "!", "(I1 x)")]
        [InlineData("I1", "~", "(I1 x)")]
        [InlineData("I1", "++", "(I1 x)")]
        [InlineData("I1", "--", "(I1 x)")]
        [InlineData("I1", "+", "(I1 x, I1 y)")]
        [InlineData("I1", "-", "(I1 x, I1 y)")]
        [InlineData("I1", "*", "(I1 x, I1 y)")]
        [InlineData("I1", "/", "(I1 x, I1 y)")]
        [InlineData("I1", "%", "(I1 x, I1 y)")]
        [InlineData("I1", "&", "(I1 x, I1 y)")]
        [InlineData("I1", "|", "(I1 x, I1 y)")]
        [InlineData("I1", "^", "(I1 x, I1 y)")]
        [InlineData("I1", "<<", "(I1 x, int y)")]
        [InlineData("I1", ">>", "(I1 x, int y)")]
        public void DefineAbstractStaticOperator_01(string type, string op, string paramList)
        {
            var source1 =
@"
interface I1
{
    abstract static " + type + " operator " + op + " " + paramList + @";
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();

                Assert.False(m01.IsMetadataNewSlot());
                Assert.True(m01.IsAbstract);
                Assert.True(m01.IsMetadataVirtual());
                Assert.False(m01.IsMetadataFinal);
                Assert.False(m01.IsVirtual);
                Assert.False(m01.IsSealed);
                Assert.True(m01.IsStatic);
                Assert.False(m01.IsOverride);
            }
        }

        [Fact]
        public void DefineAbstractStaticOperator_02()
        {
            var source1 =
@"
interface I1
{
    abstract static bool operator true (I1 x);
    abstract static bool operator false (I1 x);
    abstract static I1 operator > (I1 x, I1 y);
    abstract static I1 operator < (I1 x, I1 y);
    abstract static I1 operator >= (I1 x, I1 y);
    abstract static I1 operator <= (I1 x, I1 y);
    abstract static I1 operator == (I1 x, I1 y);
    abstract static I1 operator != (I1 x, I1 y);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                int count = 0;
                foreach (var m01 in module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>())
                {
                    Assert.False(m01.IsMetadataNewSlot());
                    Assert.True(m01.IsAbstract);
                    Assert.True(m01.IsMetadataVirtual());
                    Assert.False(m01.IsMetadataFinal);
                    Assert.False(m01.IsVirtual);
                    Assert.False(m01.IsSealed);
                    Assert.True(m01.IsStatic);
                    Assert.False(m01.IsOverride);

                    count++;
                }

                Assert.Equal(8, count);
            }
        }

        [Theory]
        [InlineData("I1", "+", "(I1 x)")]
        [InlineData("I1", "-", "(I1 x)")]
        [InlineData("I1", "!", "(I1 x)")]
        [InlineData("I1", "~", "(I1 x)")]
        [InlineData("I1", "++", "(I1 x)")]
        [InlineData("I1", "--", "(I1 x)")]
        [InlineData("I1", "+", "(I1 x, I1 y)")]
        [InlineData("I1", "-", "(I1 x, I1 y)")]
        [InlineData("I1", "*", "(I1 x, I1 y)")]
        [InlineData("I1", "/", "(I1 x, I1 y)")]
        [InlineData("I1", "%", "(I1 x, I1 y)")]
        [InlineData("I1", "&", "(I1 x, I1 y)")]
        [InlineData("I1", "|", "(I1 x, I1 y)")]
        [InlineData("I1", "^", "(I1 x, I1 y)")]
        [InlineData("I1", "<<", "(I1 x, int y)")]
        [InlineData("I1", ">>", "(I1 x, int y)")]
        public void DefineAbstractStaticOperator_03(string type, string op, string paramList)
        {
            var source1 =
@"
interface I1
{
    abstract static " + type + " operator " + op + " " + paramList + @";
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation1.VerifyDiagnostics(
                // (4,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator + (I1 x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(4, 31 + type.Length)
                );
        }

        [Fact]
        public void DefineAbstractStaticOperator_04()
        {
            var source1 =
@"
interface I1
{
    abstract static bool operator true (I1 x);
    abstract static bool operator false (I1 x);
    abstract static I1 operator > (I1 x, I1 y);
    abstract static I1 operator < (I1 x, I1 y);
    abstract static I1 operator >= (I1 x, I1 y);
    abstract static I1 operator <= (I1 x, I1 y);
    abstract static I1 operator == (I1 x, I1 y);
    abstract static I1 operator != (I1 x, I1 y);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation1.VerifyDiagnostics(
                // (4,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static bool operator true (I1 x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "true").WithLocation(4, 35),
                // (5,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static bool operator false (I1 x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "false").WithLocation(5, 35),
                // (6,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator > (I1 x, I1 y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, ">").WithLocation(6, 33),
                // (7,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator < (I1 x, I1 y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "<").WithLocation(7, 33),
                // (8,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator >= (I1 x, I1 y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, ">=").WithLocation(8, 33),
                // (9,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator <= (I1 x, I1 y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "<=").WithLocation(9, 33),
                // (10,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator == (I1 x, I1 y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "==").WithLocation(10, 33),
                // (11,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator != (I1 x, I1 y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "!=").WithLocation(11, 33)
                );
        }

        [Fact]
        public void DefineAbstractStaticProperty_01()
        {
            var source1 =
@"
interface I1
{
    abstract static int P01 { get; set; }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var p01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<PropertySymbol>().Single();

                Assert.True(p01.IsAbstract);
                Assert.False(p01.IsVirtual);
                Assert.False(p01.IsSealed);
                Assert.True(p01.IsStatic);
                Assert.False(p01.IsOverride);

                int count = 0;
                foreach (var m01 in module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>())
                {
                    Assert.False(m01.IsMetadataNewSlot());
                    Assert.True(m01.IsAbstract);
                    Assert.True(m01.IsMetadataVirtual());
                    Assert.False(m01.IsMetadataFinal);
                    Assert.False(m01.IsVirtual);
                    Assert.False(m01.IsSealed);
                    Assert.True(m01.IsStatic);
                    Assert.False(m01.IsOverride);

                    count++;
                }

                Assert.Equal(2, count);
            }
        }

        [Fact]
        public void DefineAbstractStaticProperty_02()
        {
            var source1 =
@"
interface I1
{
    abstract static int P01 { get; set; }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation1.VerifyDiagnostics(
                // (4,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "get").WithLocation(4, 31),
                // (4,36): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "set").WithLocation(4, 36)
                );
        }

        [Fact]
        public void DefineAbstractStaticEvent_01()
        {
            var source1 =
@"
interface I1
{
    abstract static event System.Action E01;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var e01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<EventSymbol>().Single();

                Assert.True(e01.IsAbstract);
                Assert.False(e01.IsVirtual);
                Assert.False(e01.IsSealed);
                Assert.True(e01.IsStatic);
                Assert.False(e01.IsOverride);

                int count = 0;
                foreach (var m01 in module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>())
                {
                    Assert.False(m01.IsMetadataNewSlot());
                    Assert.True(m01.IsAbstract);
                    Assert.True(m01.IsMetadataVirtual());
                    Assert.False(m01.IsMetadataFinal);
                    Assert.False(m01.IsVirtual);
                    Assert.False(m01.IsSealed);
                    Assert.True(m01.IsStatic);
                    Assert.False(m01.IsOverride);

                    count++;
                }

                Assert.Equal(2, count);
            }
        }

        [Fact]
        public void DefineAbstractStaticEvent_02()
        {
            var source1 =
@"
interface I1
{
    abstract static event System.Action E01;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation1.VerifyDiagnostics(
                // (4,41): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static event System.Action E01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "E01").WithLocation(4, 41)
                );
        }

        [Fact]
        public void ConstraintChecks_01()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

public interface I2 : I1
{
}

public interface I3 : I2
{
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var source2 =
@"
class C1<T1> where T1 : I1
{
    void Test(C1<I2> x)
    {
    }
}

class C2
{
    void M<T2>() where T2 : I1 {}

    void Test(C2 x)
    {
        x.M<I2>();
    }
}

class C3<T3> where T3 : I2
{
    void Test(C3<I2> x, C3<I3> y)
    {
    }
}

class C4
{
    void M<T4>() where T4 : I2 {}

    void Test(C4 x)
    {
        x.M<I2>();
        x.M<I3>();
    }
}

class C5<T5> where T5 : I3
{
    void Test(C5<I3> y)
    {
    }
}

class C6
{
    void M<T6>() where T6 : I3 {}

    void Test(C6 x)
    {
        x.M<I3>();
    }
}

class C7<T7> where T7 : I1
{
    void Test(C7<I1> y)
    {
    }
}

class C8
{
    void M<T8>() where T8 : I1 {}

    void Test(C8 x)
    {
        x.M<I1>();
    }
}
";
            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            var expected = new[] {
                // (4,22): error CS9101: The interface 'I2' cannot be used as type parameter 'T1' in the generic type or method 'C1<T1>'. The constraint interface 'I1' or its base interface has static abstract members.
                //     void Test(C1<I2> x)
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "x").WithArguments("C1<T1>", "I1", "T1", "I2").WithLocation(4, 22),
                // (15,11): error CS9101: The interface 'I2' cannot be used as type parameter 'T2' in the generic type or method 'C2.M<T2>()'. The constraint interface 'I1' or its base interface has static abstract members.
                //         x.M<I2>();
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "M<I2>").WithArguments("C2.M<T2>()", "I1", "T2", "I2").WithLocation(15, 11),
                // (21,22): error CS9101: The interface 'I2' cannot be used as type parameter 'T3' in the generic type or method 'C3<T3>'. The constraint interface 'I2' or its base interface has static abstract members.
                //     void Test(C3<I2> x, C3<I3> y)
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "x").WithArguments("C3<T3>", "I2", "T3", "I2").WithLocation(21, 22),
                // (21,32): error CS9101: The interface 'I3' cannot be used as type parameter 'T3' in the generic type or method 'C3<T3>'. The constraint interface 'I2' or its base interface has static abstract members.
                //     void Test(C3<I2> x, C3<I3> y)
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "y").WithArguments("C3<T3>", "I2", "T3", "I3").WithLocation(21, 32),
                // (32,11): error CS9101: The interface 'I2' cannot be used as type parameter 'T4' in the generic type or method 'C4.M<T4>()'. The constraint interface 'I2' or its base interface has static abstract members.
                //         x.M<I2>();
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "M<I2>").WithArguments("C4.M<T4>()", "I2", "T4", "I2").WithLocation(32, 11),
                // (33,11): error CS9101: The interface 'I3' cannot be used as type parameter 'T4' in the generic type or method 'C4.M<T4>()'. The constraint interface 'I2' or its base interface has static abstract members.
                //         x.M<I3>();
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "M<I3>").WithArguments("C4.M<T4>()", "I2", "T4", "I3").WithLocation(33, 11),
                // (39,22): error CS9101: The interface 'I3' cannot be used as type parameter 'T5' in the generic type or method 'C5<T5>'. The constraint interface 'I3' or its base interface has static abstract members.
                //     void Test(C5<I3> y)
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "y").WithArguments("C5<T5>", "I3", "T5", "I3").WithLocation(39, 22),
                // (50,11): error CS9101: The interface 'I3' cannot be used as type parameter 'T6' in the generic type or method 'C6.M<T6>()'. The constraint interface 'I3' or its base interface has static abstract members.
                //         x.M<I3>();
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "M<I3>").WithArguments("C6.M<T6>()", "I3", "T6", "I3").WithLocation(50, 11),
                // (56,22): error CS9101: The interface 'I1' cannot be used as type parameter 'T7' in the generic type or method 'C7<T7>'. The constraint interface 'I1' or its base interface has static abstract members.
                //     void Test(C7<I1> y)
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "y").WithArguments("C7<T7>", "I1", "T7", "I1").WithLocation(56, 22),
                // (67,11): error CS9101: The interface 'I1' cannot be used as type parameter 'T8' in the generic type or method 'C8.M<T8>()'. The constraint interface 'I1' or its base interface has static abstract members.
                //         x.M<I1>();
                Diagnostic(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, "M<I1>").WithArguments("C8.M<T8>()", "I1", "T8", "I1").WithLocation(67, 11)
            };

            compilation2.VerifyDiagnostics(expected);

            compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.EmitToImageReference() });

            compilation2.VerifyDiagnostics(expected);
        }

        [Fact]
        public void ConstraintChecks_02()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

public class C : I1
{
    public static void M01() {}
}

public struct S : I1
{
    public static void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var source2 =
@"
class C1<T1> where T1 : I1
{
    void Test(C1<C> x, C1<S> y, C1<T1> z)
    {
    }
}

class C2
{
    public void M<T2>(C2 x) where T2 : I1
    {
        x.M<T2>(x);
    }

    void Test(C2 x)
    {
        x.M<C>(x);
        x.M<S>(x);
    }
}

class C3<T3> where T3 : I1
{
    void Test(C1<T3> z)
    {
    }
}

class C4
{
    void M<T4>(C2 x) where T4 : I1
    {
        x.M<T4>(x);
    }
}

class C5<T5>
{
    internal virtual void M<U5>() where U5 : T5 { }
}

class C6 : C5<I1>
{
    internal override void M<U6>() { base.M<U6>(); }
}
";
            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyEmitDiagnostics();

            compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.EmitToImageReference() });

            compilation2.VerifyEmitDiagnostics();
        }

        [Fact]
        public void VarianceSafety_01()
        {
            var source1 =
@"
interface I2<out T1, in T2>
{
    abstract static T1 P1 { get; }
    abstract static T2 P2 { get; }
    abstract static T1 P3 { set; }
    abstract static T2 P4 { set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.VerifyDiagnostics(
                // (5,21): error CS1961: Invalid variance: The type parameter 'T2' must be covariantly valid on 'I2<T1, T2>.P2'. 'T2' is contravariant.
                //     abstract static T2 P2 { get; }
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "T2").WithArguments("I2<T1, T2>.P2", "T2", "contravariant", "covariantly").WithLocation(5, 21),
                // (6,21): error CS1961: Invalid variance: The type parameter 'T1' must be contravariantly valid on 'I2<T1, T2>.P3'. 'T1' is covariant.
                //     abstract static T1 P3 { set; }
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "T1").WithArguments("I2<T1, T2>.P3", "T1", "covariant", "contravariantly").WithLocation(6, 21)
                );
        }

        [Fact]
        public void VarianceSafety_02()
        {
            var source1 =
@"
interface I2<out T1, in T2>
{
    abstract static T1 M1();
    abstract static T2 M2();
    abstract static void M3(T1 x);
    abstract static void M4(T2 x);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.VerifyDiagnostics(
                // (5,21): error CS1961: Invalid variance: The type parameter 'T2' must be covariantly valid on 'I2<T1, T2>.M2()'. 'T2' is contravariant.
                //     abstract static T2 M2();
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "T2").WithArguments("I2<T1, T2>.M2()", "T2", "contravariant", "covariantly").WithLocation(5, 21),
                // (6,29): error CS1961: Invalid variance: The type parameter 'T1' must be contravariantly valid on 'I2<T1, T2>.M3(T1)'. 'T1' is covariant.
                //     abstract static void M3(T1 x);
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "T1").WithArguments("I2<T1, T2>.M3(T1)", "T1", "covariant", "contravariantly").WithLocation(6, 29)
                );
        }

        [Fact]
        public void VarianceSafety_03()
        {
            var source1 =
@"
interface I2<out T1, in T2>
{
    abstract static event System.Action<System.Func<T1>> E1;
    abstract static event System.Action<System.Func<T2>> E2;
    abstract static event System.Action<System.Action<T1>> E3;
    abstract static event System.Action<System.Action<T2>> E4;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.VerifyDiagnostics(
                // (5,58): error CS1961: Invalid variance: The type parameter 'T2' must be covariantly valid on 'I2<T1, T2>.E2'. 'T2' is contravariant.
                //     abstract static event System.Action<System.Func<T2>> E2;
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "E2").WithArguments("I2<T1, T2>.E2", "T2", "contravariant", "covariantly").WithLocation(5, 58),
                // (6,60): error CS1961: Invalid variance: The type parameter 'T1' must be contravariantly valid on 'I2<T1, T2>.E3'. 'T1' is covariant.
                //     abstract static event System.Action<System.Action<T1>> E3;
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "E3").WithArguments("I2<T1, T2>.E3", "T1", "covariant", "contravariantly").WithLocation(6, 60)
                );
        }

        [Fact]
        public void VarianceSafety_04()
        {
            var source1 =
@"
interface I2<out T2>
{
    abstract static int operator +(I2<T2> x);
}

interface I3<out T3>
{
    abstract static int operator +(I3<T3> x);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.VerifyDiagnostics(
                // (4,36): error CS1961: Invalid variance: The type parameter 'T2' must be contravariantly valid on 'I2<T2>.operator +(I2<T2>)'. 'T2' is covariant.
                //     abstract static int operator +(I2<T2> x);
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "I2<T2>").WithArguments("I2<T2>.operator +(I2<T2>)", "T2", "covariant", "contravariantly").WithLocation(4, 36),
                // (9,36): error CS1961: Invalid variance: The type parameter 'T3' must be contravariantly valid on 'I3<T3>.operator +(I3<T3>)'. 'T3' is covariant.
                //     abstract static int operator +(I3<T3> x);
                Diagnostic(ErrorCode.ERR_UnexpectedVariance, "I3<T3>").WithArguments("I3<T3>.operator +(I3<T3>)", "T3", "covariant", "contravariantly").WithLocation(9, 36)
                );
        }

        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("!")]
        [InlineData("~")]
        [InlineData("true")]
        [InlineData("false")]
        public void OperatorSignature_01(string op)
        {
            var source1 =
@"
interface I1<T1> where T1 : I1<T1>
{
    static bool operator " + op + @"(T1 x) => throw null;
}

interface I2<T2> where T2 : struct, I2<T2>
{
    static bool operator " + op + @"(T2? x) => throw null;
}

interface I3<T3> where T3 : I3<T3>
{
    static abstract bool operator " + op + @"(T3 x);
}

interface I4<T4> where T4 : struct, I4<T4>
{
    static abstract bool operator " + op + @"(T4? x);
}

class C5<T5> where T5 : C5<T5>.I6
{
    public interface I6
    {
        static abstract bool operator " + op + @"(T5 x);
    }
}

interface I7<T71, T72> where T72 : I7<T71, T72> where T71 : T72
{
    static abstract bool operator " + op + @"(T71 x);
}

interface I8<T8> where T8 : I9<T8>
{
    static abstract bool operator " + op + @"(T8 x);
}

interface I9<T9> : I8<T9> where T9 : I9<T9> {}

interface I10<T10> where T10 : C11<T10>
{
    static abstract bool operator " + op + @"(T10 x);
}

class C11<T11> : I10<T11> where T11 : C11<T11> {}

interface I12
{
    static abstract bool operator " + op + @"(int x);
}

interface I13
{
    static abstract bool operator " + op + @"(I13 x);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (4,26): error CS0562: The parameter of a unary operator must be the containing type
                //     static bool operator +(T1 x) => throw null;
                Diagnostic(ErrorCode.ERR_BadUnaryOperatorSignature, op).WithLocation(4, 26),
                // (9,26): error CS0562: The parameter of a unary operator must be the containing type
                //     static bool operator +(T2? x) => throw null;
                Diagnostic(ErrorCode.ERR_BadUnaryOperatorSignature, op).WithLocation(9, 26),
                // (26,39): error CS9102: The parameter of a unary operator must be the containing type, or its type parameter constrained to it.
                //         static abstract bool operator +(T5 x);
                Diagnostic(ErrorCode.ERR_BadAbstractUnaryOperatorSignature, op).WithLocation(26, 39),
                // (32,35): error CS9102: The parameter of a unary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(T71 x);
                Diagnostic(ErrorCode.ERR_BadAbstractUnaryOperatorSignature, op).WithLocation(32, 35),
                // (37,35): error CS9102: The parameter of a unary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(T8 x);
                Diagnostic(ErrorCode.ERR_BadAbstractUnaryOperatorSignature, op).WithLocation(37, 35),
                // (44,35): error CS9102: The parameter of a unary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(T10 x);
                Diagnostic(ErrorCode.ERR_BadAbstractUnaryOperatorSignature, op).WithLocation(44, 35),
                // (47,18): error CS0535: 'C11<T11>' does not implement interface member 'I10<T11>.operator false(T11)'
                // class C11<T11> : I10<T11> where T11 : C11<T11> {}
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I10<T11>").WithArguments("C11<T11>", "I10<T11>.operator " + op + "(T11)").WithLocation(47, 18),
                // (51,35): error CS9102: The parameter of a unary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator false(int x);
                Diagnostic(ErrorCode.ERR_BadAbstractUnaryOperatorSignature, op).WithLocation(51, 35)
                );
        }

        [Theory]
        [InlineData("++")]
        [InlineData("--")]
        public void OperatorSignature_02(string op)
        {
            var source1 =
@"
interface I1<T1> where T1 : I1<T1>
{
    static T1 operator " + op + @"(T1 x) => throw null;
}

interface I2<T2> where T2 : struct, I2<T2>
{
    static T2? operator " + op + @"(T2? x) => throw null;
}

interface I3<T3> where T3 : I3<T3>
{
    static abstract T3 operator " + op + @"(T3 x);
}

interface I4<T4> where T4 : struct, I4<T4>
{
    static abstract T4? operator " + op + @"(T4? x);
}

class C5<T5> where T5 : C5<T5>.I6
{
    public interface I6
    {
        static abstract T5 operator " + op + @"(T5 x);
    }
}

interface I7<T71, T72> where T72 : I7<T71, T72> where T71 : T72
{
    static abstract T71 operator " + op + @"(T71 x);
}

interface I8<T8> where T8 : I9<T8>
{
    static abstract T8 operator " + op + @"(T8 x);
}

interface I9<T9> : I8<T9> where T9 : I9<T9> {}

interface I10<T10> where T10 : C11<T10>
{
    static abstract T10 operator " + op + @"(T10 x);
}

class C11<T11> : I10<T11> where T11 : C11<T11> {}

interface I12
{
    static abstract int operator " + op + @"(int x);
}

interface I13
{
    static abstract I13 operator " + op + @"(I13 x);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.VerifyDiagnostics(
                // (4,24): error CS0559: The parameter type for ++ or -- operator must be the containing type
                //     static T1 operator ++(T1 x) => throw null;
                Diagnostic(ErrorCode.ERR_BadIncDecSignature, op).WithLocation(4, 24),
                // (9,25): error CS0559: The parameter type for ++ or -- operator must be the containing type
                //     static T2? operator ++(T2? x) => throw null;
                Diagnostic(ErrorCode.ERR_BadIncDecSignature, op).WithLocation(9, 25),
                // (26,37): error CS9103: The parameter type for ++ or -- operator must be the containing type, or its type parameter constrained to it.
                //         static abstract T5 operator ++(T5 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecSignature, op).WithLocation(26, 37),
                // (32,34): error CS9103: The parameter type for ++ or -- operator must be the containing type, or its type parameter constrained to it.
                //     static abstract T71 operator ++(T71 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecSignature, op).WithLocation(32, 34),
                // (37,33): error CS9103: The parameter type for ++ or -- operator must be the containing type, or its type parameter constrained to it.
                //     static abstract T8 operator ++(T8 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecSignature, op).WithLocation(37, 33),
                // (44,34): error CS9103: The parameter type for ++ or -- operator must be the containing type, or its type parameter constrained to it.
                //     static abstract T10 operator ++(T10 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecSignature, op).WithLocation(44, 34),
                // (47,18): error CS0535: 'C11<T11>' does not implement interface member 'I10<T11>.operator --(T11)'
                // class C11<T11> : I10<T11> where T11 : C11<T11> {}
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I10<T11>").WithArguments("C11<T11>", "I10<T11>.operator " + op + "(T11)").WithLocation(47, 18),
                // (51,34): error CS9103: The parameter type for ++ or -- operator must be the containing type, or its type parameter constrained to it.
                //     static abstract int operator ++(int x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecSignature, op).WithLocation(51, 34)
                );
        }

        [Theory]
        [InlineData("++")]
        [InlineData("--")]
        public void OperatorSignature_03(string op)
        {
            var source1 =
@"
interface I1<T1> where T1 : I1<T1>
{
    static T1 operator " + op + @"(I1<T1> x) => throw null;
}

interface I2<T2> where T2 : struct, I2<T2>
{
    static T2? operator " + op + @"(I2<T2> x) => throw null;
}

interface I3<T3> where T3 : I3<T3>
{
    static abstract T3 operator " + op + @"(I3<T3> x);
}

interface I4<T4> where T4 : struct, I4<T4>
{
    static abstract T4? operator " + op + @"(I4<T4> x);
}

class C5<T5> where T5 : C5<T5>.I6
{
    public interface I6
    {
        static abstract T5 operator " + op + @"(I6 x);
    }
}

interface I7<T71, T72> where T72 : I7<T71, T72> where T71 : T72
{
    static abstract T71 operator " + op + @"(I7<T71, T72> x);
}

interface I8<T8> where T8 : I9<T8>
{
    static abstract T8 operator " + op + @"(I8<T8> x);
}

interface I9<T9> : I8<T9> where T9 : I9<T9> {}

interface I10<T10> where T10 : C11<T10>
{
    static abstract T10 operator " + op + @"(I10<T10> x);
}

class C11<T11> : I10<T11> where T11 : C11<T11> {}

interface I12
{
    static abstract int operator " + op + @"(I12 x);
}

interface I13<T13> where T13 : struct, I13<T13>
{
    static abstract T13? operator " + op + @"(T13 x);
}

interface I14<T14> where T14 : struct, I14<T14>
{
    static abstract T14 operator " + op + @"(T14? x);
}

interface I15<T151, T152> where T151 : I15<T151, T152> where T152 : I15<T151, T152>
{
    static abstract T151 operator " + op + @"(T152 x);
}

";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.VerifyDiagnostics(
                // (4,24): error CS0448: The return type for ++ or -- operator must match the parameter type or be derived from the parameter type
                //     static T1 operator ++(I1<T1> x) => throw null;
                Diagnostic(ErrorCode.ERR_BadIncDecRetType, op).WithLocation(4, 24),
                // (9,25): error CS0448: The return type for ++ or -- operator must match the parameter type or be derived from the parameter type
                //     static T2? operator ++(I2<T2> x) => throw null;
                Diagnostic(ErrorCode.ERR_BadIncDecRetType, op).WithLocation(9, 25),
                // (19,34): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract T4? operator ++(I4<T4> x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(19, 34),
                // (26,37): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //         static abstract T5 operator ++(I6 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(26, 37),
                // (32,34): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract T71 operator ++(I7<T71, T72> x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(32, 34),
                // (37,33): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract T8 operator ++(I8<T8> x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(37, 33),
                // (44,34): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract T10 operator ++(I10<T10> x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(44, 34),
                // (47,18): error CS0535: 'C11<T11>' does not implement interface member 'I10<T11>.operator ++(I10<T11>)'
                // class C11<T11> : I10<T11> where T11 : C11<T11> {}
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I10<T11>").WithArguments("C11<T11>", "I10<T11>.operator " + op + "(I10<T11>)").WithLocation(47, 18),
                // (51,34): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract int operator ++(I12 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(51, 34),
                // (56,35): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract T13? operator ++(T13 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(56, 35),
                // (61,34): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract T14 operator ++(T14? x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(61, 34),
                // (66,35): error CS9104: The return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type parameter constrained to it unless the parameter type is a different type parameter.
                //     static abstract T151 operator ++(T152 x);
                Diagnostic(ErrorCode.ERR_BadAbstractIncDecRetType, op).WithLocation(66, 35)
                );
        }

        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("%")]
        [InlineData("&")]
        [InlineData("|")]
        [InlineData("^")]
        [InlineData("<")]
        [InlineData(">")]
        [InlineData("<=")]
        [InlineData(">=")]
        public void OperatorSignature_04(string op)
        {
            var source1 =
@"
interface I1<T1> where T1 : I1<T1>
{
    static bool operator " + op + @"(T1 x, bool y) => throw null;
}

interface I2<T2> where T2 : struct, I2<T2>
{
    static bool operator " + op + @"(T2? x, bool y) => throw null;
}

interface I3<T3> where T3 : I3<T3>
{
    static abstract bool operator " + op + @"(T3 x, bool y);
}

interface I4<T4> where T4 : struct, I4<T4>
{
    static abstract bool operator " + op + @"(T4? x, bool y);
}

class C5<T5> where T5 : C5<T5>.I6
{
    public interface I6
    {
        static abstract bool operator " + op + @"(T5 x, bool y);
    }
}

interface I7<T71, T72> where T72 : I7<T71, T72> where T71 : T72
{
    static abstract bool operator " + op + @"(T71 x, bool y);
}

interface I8<T8> where T8 : I9<T8>
{
    static abstract bool operator " + op + @"(T8 x, bool y);
}

interface I9<T9> : I8<T9> where T9 : I9<T9> {}

interface I10<T10> where T10 : C11<T10>
{
    static abstract bool operator " + op + @"(T10 x, bool y);
}

class C11<T11> : I10<T11> where T11 : C11<T11> {}

interface I12
{
    static abstract bool operator " + op + @"(int x, bool y);
}

interface I13
{
    static abstract bool operator " + op + @"(I13 x, bool y);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (4,26): error CS0563: One of the parameters of a binary operator must be the containing type
                //     static bool operator +(T1 x, bool y) => throw null;
                Diagnostic(ErrorCode.ERR_BadBinaryOperatorSignature, op).WithLocation(4, 26),
                // (9,26): error CS0563: One of the parameters of a binary operator must be the containing type
                //     static bool operator +(T2? x, bool y) => throw null;
                Diagnostic(ErrorCode.ERR_BadBinaryOperatorSignature, op).WithLocation(9, 26),
                // (26,39): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //         static abstract bool operator +(T5 x, bool y);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(26, 39),
                // (32,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(T71 x, bool y);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(32, 35),
                // (37,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(T8 x, bool y);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(37, 35),
                // (44,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(T10 x, bool y);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(44, 35),
                // (47,18): error CS0535: 'C11<T11>' does not implement interface member 'I10<T11>.operator /(T11, bool)'
                // class C11<T11> : I10<T11> where T11 : C11<T11> {}
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I10<T11>").WithArguments("C11<T11>", "I10<T11>.operator " + op + "(T11, bool)").WithLocation(47, 18),
                // (51,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(int x, bool y);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(51, 35)
                );
        }

        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("%")]
        [InlineData("&")]
        [InlineData("|")]
        [InlineData("^")]
        [InlineData("<")]
        [InlineData(">")]
        [InlineData("<=")]
        [InlineData(">=")]
        public void OperatorSignature_05(string op)
        {
            var source1 =
@"
interface I1<T1> where T1 : I1<T1>
{
    static bool operator " + op + @"(bool y, T1 x) => throw null;
}

interface I2<T2> where T2 : struct, I2<T2>
{
    static bool operator " + op + @"(bool y, T2? x) => throw null;
}

interface I3<T3> where T3 : I3<T3>
{
    static abstract bool operator " + op + @"(bool y, T3 x);
}

interface I4<T4> where T4 : struct, I4<T4>
{
    static abstract bool operator " + op + @"(bool y, T4? x);
}

class C5<T5> where T5 : C5<T5>.I6
{
    public interface I6
    {
        static abstract bool operator " + op + @"(bool y, T5 x);
    }
}

interface I7<T71, T72> where T72 : I7<T71, T72> where T71 : T72
{
    static abstract bool operator " + op + @"(bool y, T71 x);
}

interface I8<T8> where T8 : I9<T8>
{
    static abstract bool operator " + op + @"(bool y, T8 x);
}

interface I9<T9> : I8<T9> where T9 : I9<T9> {}

interface I10<T10> where T10 : C11<T10>
{
    static abstract bool operator " + op + @"(bool y, T10 x);
}

class C11<T11> : I10<T11> where T11 : C11<T11> {}

interface I12
{
    static abstract bool operator " + op + @"(bool y, int x);
}

interface I13
{
    static abstract bool operator " + op + @"(bool y, I13 x);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (4,26): error CS0563: One of the parameters of a binary operator must be the containing type
                //     static bool operator +(bool y, T1 x) => throw null;
                Diagnostic(ErrorCode.ERR_BadBinaryOperatorSignature, op).WithLocation(4, 26),
                // (9,26): error CS0563: One of the parameters of a binary operator must be the containing type
                //     static bool operator +(bool y, T2? x) => throw null;
                Diagnostic(ErrorCode.ERR_BadBinaryOperatorSignature, op).WithLocation(9, 26),
                // (26,39): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //         static abstract bool operator +(bool y, T5 x);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(26, 39),
                // (32,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(bool y, T71 x);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(32, 35),
                // (37,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(bool y, T8 x);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(37, 35),
                // (44,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(bool y, T10 x);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(44, 35),
                // (47,18): error CS0535: 'C11<T11>' does not implement interface member 'I10<T11>.operator <=(bool, T11)'
                // class C11<T11> : I10<T11> where T11 : C11<T11> {}
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I10<T11>").WithArguments("C11<T11>", "I10<T11>.operator " + op + "(bool, T11)").WithLocation(47, 18),
                // (51,35): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     static abstract bool operator +(bool y, int x);
                Diagnostic(ErrorCode.ERR_BadAbstractBinaryOperatorSignature, op).WithLocation(51, 35)
                );
        }

        [Theory]
        [InlineData("<<")]
        [InlineData(">>")]
        public void OperatorSignature_06(string op)
        {
            var source1 =
@"
interface I1<T1> where T1 : I1<T1>
{
    static bool operator " + op + @"(T1 x, int y) => throw null;
}

interface I2<T2> where T2 : struct, I2<T2>
{
    static bool operator " + op + @"(T2? x, int y) => throw null;
}

interface I3<T3> where T3 : I3<T3>
{
    static abstract bool operator " + op + @"(T3 x, int y);
}

interface I4<T4> where T4 : struct, I4<T4>
{
    static abstract bool operator " + op + @"(T4? x, int y);
}

class C5<T5> where T5 : C5<T5>.I6
{
    public interface I6
    {
        static abstract bool operator " + op + @"(T5 x, int y);
    }
}

interface I7<T71, T72> where T72 : I7<T71, T72> where T71 : T72
{
    static abstract bool operator " + op + @"(T71 x, int y);
}

interface I8<T8> where T8 : I9<T8>
{
    static abstract bool operator " + op + @"(T8 x, int y);
}

interface I9<T9> : I8<T9> where T9 : I9<T9> {}

interface I10<T10> where T10 : C11<T10>
{
    static abstract bool operator " + op + @"(T10 x, int y);
}

class C11<T11> : I10<T11> where T11 : C11<T11> {}

interface I12
{
    static abstract bool operator " + op + @"(int x, int y);
}

interface I13
{
    static abstract bool operator " + op + @"(I13 x, int y);
}

interface I14
{
    static abstract bool operator " + op + @"(I14 x, bool y);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);
            compilation1.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (4,26): error CS0564: The first operand of an overloaded shift operator must have the same type as the containing type, and the type of the second operand must be int
                //     static bool operator <<(T1 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_BadShiftOperatorSignature, op).WithLocation(4, 26),
                // (9,26): error CS0564: The first operand of an overloaded shift operator must have the same type as the containing type, and the type of the second operand must be int
                //     static bool operator <<(T2? x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_BadShiftOperatorSignature, op).WithLocation(9, 26),
                // (26,39): error CS9106: The first operand of an overloaded shift operator must have the same type as the containing type or its type parameter constrained to it, and the type of the second operand must be int
                //         static abstract bool operator <<(T5 x, int y);
                Diagnostic(ErrorCode.ERR_BadAbstractShiftOperatorSignature, op).WithLocation(26, 39),
                // (32,35): error CS9106: The first operand of an overloaded shift operator must have the same type as the containing type or its type parameter constrained to it, and the type of the second operand must be int
                //     static abstract bool operator <<(T71 x, int y);
                Diagnostic(ErrorCode.ERR_BadAbstractShiftOperatorSignature, op).WithLocation(32, 35),
                // (37,35): error CS9106: The first operand of an overloaded shift operator must have the same type as the containing type or its type parameter constrained to it, and the type of the second operand must be int
                //     static abstract bool operator <<(T8 x, int y);
                Diagnostic(ErrorCode.ERR_BadAbstractShiftOperatorSignature, op).WithLocation(37, 35),
                // (44,35): error CS9106: The first operand of an overloaded shift operator must have the same type as the containing type or its type parameter constrained to it, and the type of the second operand must be int
                //     static abstract bool operator <<(T10 x, int y);
                Diagnostic(ErrorCode.ERR_BadAbstractShiftOperatorSignature, op).WithLocation(44, 35),
                // (47,18): error CS0535: 'C11<T11>' does not implement interface member 'I10<T11>.operator >>(T11, int)'
                // class C11<T11> : I10<T11> where T11 : C11<T11> {}
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I10<T11>").WithArguments("C11<T11>", "I10<T11>.operator " + op + "(T11, int)").WithLocation(47, 18),
                // (51,35): error CS9106: The first operand of an overloaded shift operator must have the same type as the containing type or its type parameter constrained to it, and the type of the second operand must be int
                //     static abstract bool operator <<(int x, int y);
                Diagnostic(ErrorCode.ERR_BadAbstractShiftOperatorSignature, op).WithLocation(51, 35),
                // (61,35): error CS9106: The first operand of an overloaded shift operator must have the same type as the containing type or its type parameter constrained to it, and the type of the second operand must be int
                //     static abstract bool operator <<(I14 x, bool y);
                Diagnostic(ErrorCode.ERR_BadAbstractShiftOperatorSignature, op).WithLocation(61, 35)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_01()
        {
            var source1 =
@"
interface I1
{
    abstract static void M01();

    static void M02()
    {
        M01();
        M04();
    }

    void M03()
    {
        this.M01();
        this.M04();
    }

    static void M04() {}

    protected abstract static void M05();
}

class Test
{
    static void MT1(I1 x)
    {
        I1.M01();
        x.M01();
        I1.M04();
        x.M04();
    }

    static void MT2<T>() where T : I1
    {
        T.M03();
        T.M04();
        T.M00();
        T.M05();

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.M01());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         M01();
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "M01").WithLocation(8, 9),
                // (14,9): error CS0176: Member 'I1.M01()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.M01();
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.M01").WithArguments("I1.M01()").WithLocation(14, 9),
                // (15,9): error CS0176: Member 'I1.M04()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.M04();
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.M04").WithArguments("I1.M04()").WithLocation(15, 9),
                // (27,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         I1.M01();
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.M01").WithLocation(27, 9),
                // (28,9): error CS0176: Member 'I1.M01()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.M01();
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.M01").WithArguments("I1.M01()").WithLocation(28, 9),
                // (30,9): error CS0176: Member 'I1.M04()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.M04();
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.M04").WithArguments("I1.M04()").WithLocation(30, 9),
                // (35,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.M03();
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 9),
                // (36,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.M04();
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 9),
                // (37,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.M00();
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 9),
                // (38,11): error CS0122: 'I1.M05()' is inaccessible due to its protection level
                //         T.M05();
                Diagnostic(ErrorCode.ERR_BadAccess, "M05").WithArguments("I1.M05()").WithLocation(38, 11),
                // (40,71): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.M01());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "T.M01()").WithLocation(40, 71)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_02()
        {
            var source1 =
@"
interface I1
{
    abstract static void M01();

    static void M02()
    {
        _ = nameof(M01);
        _ = nameof(M04);
    }

    void M03()
    {
        _ = nameof(this.M01);
        _ = nameof(this.M04);
    }

    static void M04() {}

    protected abstract static void M05();
}

class Test
{
    static void MT1(I1 x)
    {
        _ = nameof(I1.M01);
        _ = nameof(x.M01);
        _ = nameof(I1.M04);
        _ = nameof(x.M04);
    }

    static void MT2<T>() where T : I1
    {
        _ = nameof(T.M03);
        _ = nameof(T.M04);
        _ = nameof(T.M00);
        _ = nameof(T.M05);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (35,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.M03);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 20),
                // (36,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.M04);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 20),
                // (37,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.M00);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 20),
                // (38,22): error CS0122: 'I1.M05()' is inaccessible due to its protection level
                //         _ = nameof(T.M05);
                Diagnostic(ErrorCode.ERR_BadAccess, "M05").WithArguments("I1.M05()").WithLocation(38, 22)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
    abstract static void M04(int x);
}

class Test
{
    static void M02<T, U>() where T : U where U : I1
    {
        T.M01();
    }

    static string M03<T, U>() where T : U where U : I1
    {
        return nameof(T.M01);
    }

    static async void M05<T, U>() where T : U where U : I1
    {
        T.M04(await System.Threading.Tasks.Task.FromResult(1));
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       14 (0xe)
  .maxstack  0
  IL_0000:  nop
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""void I1.M01()""
  IL_000c:  nop
  IL_000d:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>()",
@"
{
  // Code size       11 (0xb)
  .maxstack  1
  .locals init (string V_0)
  IL_0000:  nop
  IL_0001:  ldstr      ""M01""
  IL_0006:  stloc.0
  IL_0007:  br.s       IL_0009
  IL_0009:  ldloc.0
  IL_000a:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       12 (0xc)
  .maxstack  0
  IL_0000:  constrained. ""T""
  IL_0006:  call       ""void I1.M01()""
  IL_000b:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>()",
@"
{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  ldstr      ""M01""
  IL_0005:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();

            Assert.Equal("T.M01()", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IInvocationOperation (virtual void I1.M01()) (OperationKind.Invocation, Type: System.Void) (Syntax: 'T.M01()')
  Instance Receiver: 
    null
  Arguments(0)
");

            var m02 = compilation1.GetMember<MethodSymbol>("Test.M02");

            Assert.Equal("void I1.M01()", ((CSharpSemanticModel)model).LookupSymbols(node.SpanStart, m02.TypeParameters[0], "M01").Single().ToTestDisplayString());
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.M01();
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         T.M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.M01").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,26): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(12, 26)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_05()
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 Select(System.Func<int, int> p);
}

class Test
{
    static void M02<T>() where T : I1
    {
        _ = from t in T select t + 1;
        _ = from t in I1 select t + 1;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (11,23): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = from t in T select t + 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(11, 23),
                // (12,26): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = from t in I1 select t + 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "select t + 1").WithLocation(12, 26)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.M01();
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.M01();
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.M01();
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9),
                // (12,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 26)
                );
        }

        [Theory]
        [InlineData("+", "")]
        [InlineData("-", "")]
        [InlineData("!", "")]
        [InlineData("~", "")]
        [InlineData("++", "")]
        [InlineData("--", "")]
        [InlineData("", "++")]
        [InlineData("", "--")]
        public void ConsumeAbstractUnaryOperator_01(string prefixOp, string postfixOp)
        {
            var source1 =
@"
interface I1<T> where T : I1<T>
{
    abstract static T operator" + prefixOp + postfixOp + @" (T x);
    abstract static I1<T> operator" + prefixOp + postfixOp + @" (I1<T> x);
    static void M02(I1<T> x)
    {
        _ = " + prefixOp + "x" + postfixOp + @";
    }

    void M03(I1<T> y)
    {
        _ = " + prefixOp + "y" + postfixOp + @";
    }
}

class Test<T> where T : I1<T>
{
    static void MT1(I1<T> a)
    {
        _ = " + prefixOp + "a" + postfixOp + @";
    }

    static void MT2()
    {
        _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (" + prefixOp + "b" + postfixOp + @").ToString());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = -x;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, prefixOp + "x" + postfixOp).WithLocation(8, 13),
                // (13,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = -y;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, prefixOp + "y" + postfixOp).WithLocation(13, 13),
                // (21,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = -a;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, prefixOp + "a" + postfixOp).WithLocation(21, 13),
                (prefixOp + postfixOp).Length == 1 ?
                    // (26,78): error CS9108: An expression tree may not contain an access of static abstract interface member
                    //         _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (-b).ToString());
                    Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, prefixOp + "b" + postfixOp).WithLocation(26, 78)
                    :
                    // (26,78): error CS0832: An expression tree may not contain an assignment operator
                    //         _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b--).ToString());
                    Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAssignment, prefixOp + "b" + postfixOp).WithLocation(26, 78)
                );
        }

        [Theory]
        [InlineData("+", "", "op_UnaryPlus", "Plus")]
        [InlineData("-", "", "op_UnaryNegation", "Minus")]
        [InlineData("!", "", "op_LogicalNot", "Not")]
        [InlineData("~", "", "op_OnesComplement", "BitwiseNegation")]
        [InlineData("++", "", "op_Increment", "Increment")]
        [InlineData("--", "", "op_Decrement", "Decrement")]
        [InlineData("", "++", "op_Increment", "Increment")]
        [InlineData("", "--", "op_Decrement", "Decrement")]
        public void ConsumeAbstractUnaryOperator_03(string prefixOp, string postfixOp, string metadataName, string opKind)
        {
            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator" + prefixOp + postfixOp + @" (T x);
}

class Test
{
    static T M02<T, U>(T x) where T : U where U : I1<T>
    {
        return " + prefixOp + "x" + postfixOp + @";
    }

    static T? M03<T, U>(T? y) where T : struct, U where U : I1<T>
    {
        return " + prefixOp + "y" + postfixOp + @";
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            switch ((prefixOp, postfixOp))
            {
                case ("++", ""):
                case ("--", ""):
                    verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       21 (0x15)
  .maxstack  2
  .locals init (T V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_000d:  dup
  IL_000e:  starg.s    V_0
  IL_0010:  stloc.0
  IL_0011:  br.s       IL_0013
  IL_0013:  ldloc.0
  IL_0014:  ret
}
");
                    verifier.VerifyIL("Test.M03<T, U>(T?)",
@"
{
  // Code size       54 (0x36)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1,
                T? V_2)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  call       ""readonly bool T?.HasValue.get""
  IL_000a:  brtrue.s   IL_0017
  IL_000c:  ldloca.s   V_1
  IL_000e:  initobj    ""T?""
  IL_0014:  ldloc.1
  IL_0015:  br.s       IL_002e
  IL_0017:  ldloca.s   V_0
  IL_0019:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001e:  constrained. ""T""
  IL_0024:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_0029:  newobj     ""T?..ctor(T)""
  IL_002e:  dup
  IL_002f:  starg.s    V_0
  IL_0031:  stloc.2
  IL_0032:  br.s       IL_0034
  IL_0034:  ldloc.2
  IL_0035:  ret
}
");
                    break;

                case ("", "++"):
                case ("", "--"):
                    verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       21 (0x15)
  .maxstack  2
  .locals init (T V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  dup
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_000e:  starg.s    V_0
  IL_0010:  stloc.0
  IL_0011:  br.s       IL_0013
  IL_0013:  ldloc.0
  IL_0014:  ret
}
");
                    verifier.VerifyIL("Test.M03<T, U>(T?)",
@"
{
  // Code size       54 (0x36)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1,
                T? V_2)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  dup
  IL_0003:  stloc.0
  IL_0004:  ldloca.s   V_0
  IL_0006:  call       ""readonly bool T?.HasValue.get""
  IL_000b:  brtrue.s   IL_0018
  IL_000d:  ldloca.s   V_1
  IL_000f:  initobj    ""T?""
  IL_0015:  ldloc.1
  IL_0016:  br.s       IL_002f
  IL_0018:  ldloca.s   V_0
  IL_001a:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001f:  constrained. ""T""
  IL_0025:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_002a:  newobj     ""T?..ctor(T)""
  IL_002f:  starg.s    V_0
  IL_0031:  stloc.2
  IL_0032:  br.s       IL_0034
  IL_0034:  ldloc.2
  IL_0035:  ret
}
");
                    break;

                default:
                    verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       18 (0x12)
  .maxstack  1
  .locals init (T V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_000d:  stloc.0
  IL_000e:  br.s       IL_0010
  IL_0010:  ldloc.0
  IL_0011:  ret
}
");
                    verifier.VerifyIL("Test.M03<T, U>(T?)",
@"
{
  // Code size       51 (0x33)
  .maxstack  1
  .locals init (T? V_0,
                T? V_1,
                T? V_2)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  call       ""readonly bool T?.HasValue.get""
  IL_000a:  brtrue.s   IL_0017
  IL_000c:  ldloca.s   V_1
  IL_000e:  initobj    ""T?""
  IL_0014:  ldloc.1
  IL_0015:  br.s       IL_002e
  IL_0017:  ldloca.s   V_0
  IL_0019:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001e:  constrained. ""T""
  IL_0024:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_0029:  newobj     ""T?..ctor(T)""
  IL_002e:  stloc.2
  IL_002f:  br.s       IL_0031
  IL_0031:  ldloc.2
  IL_0032:  ret
}
");
                    break;
            }

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            switch ((prefixOp, postfixOp))
            {
                case ("++", ""):
                case ("--", ""):
                    verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_000c:  dup
  IL_000d:  starg.s    V_0
  IL_000f:  ret
}
");
                    verifier.VerifyIL("Test.M03<T, U>(T?)",
@"
{
  // Code size       49 (0x31)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  call       ""readonly bool T?.HasValue.get""
  IL_0009:  brtrue.s   IL_0016
  IL_000b:  ldloca.s   V_1
  IL_000d:  initobj    ""T?""
  IL_0013:  ldloc.1
  IL_0014:  br.s       IL_002d
  IL_0016:  ldloca.s   V_0
  IL_0018:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001d:  constrained. ""T""
  IL_0023:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_0028:  newobj     ""T?..ctor(T)""
  IL_002d:  dup
  IL_002e:  starg.s    V_0
  IL_0030:  ret
}
");
                    break;

                case ("", "++"):
                case ("", "--"):
                    verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  dup
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_000d:  starg.s    V_0
  IL_000f:  ret
}
");
                    verifier.VerifyIL("Test.M03<T, U>(T?)",
@"
{
  // Code size       49 (0x31)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  ldarg.0
  IL_0001:  dup
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  call       ""readonly bool T?.HasValue.get""
  IL_000a:  brtrue.s   IL_0017
  IL_000c:  ldloca.s   V_1
  IL_000e:  initobj    ""T?""
  IL_0014:  ldloc.1
  IL_0015:  br.s       IL_002e
  IL_0017:  ldloca.s   V_0
  IL_0019:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001e:  constrained. ""T""
  IL_0024:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_0029:  newobj     ""T?..ctor(T)""
  IL_002e:  starg.s    V_0
  IL_0030:  ret
}
");
                    break;

                default:
                    verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       13 (0xd)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_000c:  ret
}
");
                    verifier.VerifyIL("Test.M03<T, U>(T?)",
@"
{
  // Code size       45 (0x2d)
  .maxstack  1
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  call       ""readonly bool T?.HasValue.get""
  IL_0009:  brtrue.s   IL_0015
  IL_000b:  ldloca.s   V_1
  IL_000d:  initobj    ""T?""
  IL_0013:  ldloc.1
  IL_0014:  ret
  IL_0015:  ldloca.s   V_0
  IL_0017:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001c:  constrained. ""T""
  IL_0022:  call       ""T I1<T>." + metadataName + @"(T)""
  IL_0027:  newobj     ""T?..ctor(T)""
  IL_002c:  ret
}
");
                    break;
            }

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = postfixOp != "" ? (ExpressionSyntax)tree.GetRoot().DescendantNodes().OfType<PostfixUnaryExpressionSyntax>().First() : tree.GetRoot().DescendantNodes().OfType<PrefixUnaryExpressionSyntax>().First();

            Assert.Equal(prefixOp + "x" + postfixOp, node.ToString());

            switch ((prefixOp, postfixOp))
            {
                case ("++", ""):
                case ("--", ""):
                case ("", "++"):
                case ("", "--"):
                    VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IIncrementOrDecrementOperation (" + (prefixOp != "" ? "Prefix" : "Postfix") + @") (OperatorMethod: T I1<T>." + metadataName + @"(T x)) (OperationKind." + opKind + @", Type: T) (Syntax: '" + prefixOp + "x" + postfixOp + @"')
  Target: 
    IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
");
                    break;

                default:
                    VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IUnaryOperation (UnaryOperatorKind." + opKind + @") (OperatorMethod: T I1<T>." + metadataName + @"(T x)) (OperationKind.Unary, Type: T) (Syntax: '" + prefixOp + "x" + postfixOp + @"')
  Operand: 
    IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
");
                    break;
            }
        }

        [Theory]
        [InlineData("+", "")]
        [InlineData("-", "")]
        [InlineData("!", "")]
        [InlineData("~", "")]
        [InlineData("++", "")]
        [InlineData("--", "")]
        [InlineData("", "++")]
        [InlineData("", "--")]
        public void ConsumeAbstractUnaryOperator_04(string prefixOp, string postfixOp)
        {
            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator" + prefixOp + postfixOp + @" (T x);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x) where T : I1<T>
    {
        _ = " + prefixOp + "x" + postfixOp + @";
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         _ = -x;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, prefixOp + "x" + postfixOp).WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,32): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static T operator- (T x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, prefixOp + postfixOp).WithLocation(12, 31)
                );
        }

        [Theory]
        [InlineData("+", "")]
        [InlineData("-", "")]
        [InlineData("!", "")]
        [InlineData("~", "")]
        [InlineData("++", "")]
        [InlineData("--", "")]
        [InlineData("", "++")]
        [InlineData("", "--")]
        public void ConsumeAbstractUnaryOperator_06(string prefixOp, string postfixOp)
        {
            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator" + prefixOp + postfixOp + @" (T x);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x) where T : I1<T>
    {
        _ = " + prefixOp + "x" + postfixOp + @";
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = -x;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, prefixOp + "x" + postfixOp).WithArguments("static abstract members in interfaces").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (12,31): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static T operator- (T x);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, prefixOp + postfixOp).WithArguments("abstract", "9.0", "preview").WithLocation(12, 31)
                );
        }

        [Fact]
        public void ConsumeAbstractTrueOperator_01()
        {
            var source1 =
@"
interface I1
{
    abstract static bool operator true (I1 x);
    abstract static bool operator false (I1 x);

    static void M02(I1 x)
    {
        _ = x ? true : false;
    }

    void M03(I1 y)
    {
        _ = y ? true : false;
    }
}

class Test
{
    static void MT1(I1 a)
    {
        _ = a ? true : false;
    }

    static void MT2<T>() where T : I1
    {
        _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b ? true : false).ToString());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (9,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = x ? true : false;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "x").WithLocation(9, 13),
                // (14,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = y ? true : false;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "y").WithLocation(14, 13),
                // (22,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = a ? true : false;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "a").WithLocation(22, 13),
                // (27,78): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b ? true : false).ToString());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "b").WithLocation(27, 78)
                );
        }

        [Fact]
        public void ConsumeAbstractTrueOperator_03()
        {
            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static bool operator true (T x);
    abstract static bool operator false (T x);
}

class Test
{
    static void M02<T, U>(T x) where T : U where U : I1<T>
    {
        _ = x ? true : false;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       18 (0x12)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""bool I1<T>.op_True(T)""
  IL_000d:  brtrue.s   IL_0011
  IL_000f:  br.s       IL_0011
  IL_0011:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       14 (0xe)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""bool I1<T>.op_True(T)""
  IL_000c:  pop
  IL_000d:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<ConditionalExpressionSyntax>().First();

            Assert.Equal("x ? true : false", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IConditionalOperation (OperationKind.Conditional, Type: System.Boolean) (Syntax: 'x ? true : false')
  Condition: 
    IUnaryOperation (UnaryOperatorKind.True) (OperatorMethod: System.Boolean I1<T>.op_True(T x)) (OperationKind.Unary, Type: System.Boolean, IsImplicit) (Syntax: 'x')
      Operand: 
        IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
  WhenTrue: 
    ILiteralOperation (OperationKind.Literal, Type: System.Boolean, Constant: True) (Syntax: 'true')
  WhenFalse: 
    ILiteralOperation (OperationKind.Literal, Type: System.Boolean, Constant: False) (Syntax: 'false')
");
        }

        [Fact]
        public void ConsumeAbstractTrueOperator_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool operator true (I1 x);
    abstract static bool operator false (I1 x);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x) where T : I1
    {
        _ = x ? true : false;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         _ = x ? true : false;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "x").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static bool operator true (I1 x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "true").WithLocation(12, 35),
                // (13,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static bool operator false (I1 x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "false").WithLocation(13, 35)
                );
        }

        [Fact]
        public void ConsumeAbstractTrueOperator_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static bool operator true (I1 x);
    abstract static bool operator false (I1 x);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x) where T : I1
    {
        _ = x ? true : false;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = x ? true : false;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "x").WithArguments("static abstract members in interfaces").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (12,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static bool operator true (I1 x);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "true").WithArguments("abstract", "9.0", "preview").WithLocation(12, 35),
                // (13,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static bool operator false (I1 x);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "false").WithArguments("abstract", "9.0", "preview").WithLocation(13, 35)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ConsumeAbstractBinaryOperator_01([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            var source1 =
@"
partial interface I1
{
    abstract static I1 operator" + op + @" (I1 x, int y);

    static void M02(I1 x)
    {
        _ = x " + op + @" 1;
    }

    void M03(I1 y)
    {
        _ = y " + op + @" 2;
    }
}

class Test
{
    static void MT1(I1 a)
    {
        _ = a " + op + @" 3;
    }

    static void MT2<T>() where T : I1
    {
        _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b " + op + @" 4).ToString());
    }
}
";

            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1
{
    abstract static I1 operator" + matchingOp + @" (I1 x, int y);
}
";
            }

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = x - 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "x " + op + " 1").WithLocation(8, 13),
                // (13,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = y - 2;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "y " + op + " 2").WithLocation(13, 13),
                // (21,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = a - 3;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "a " + op + " 3").WithLocation(21, 13),
                // (26,78): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b - 4).ToString());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "b " + op + " 4").WithLocation(26, 78)
                );
        }

        [Theory]
        [InlineData("&", true, false, false, false)]
        [InlineData("|", true, false, false, false)]
        [InlineData("&", false, false, true, false)]
        [InlineData("|", false, true, false, false)]
        [InlineData("&", true, false, true, false)]
        [InlineData("|", true, true, false, false)]
        [InlineData("&", false, true, false, true)]
        [InlineData("|", false, false, true, true)]
        public void ConsumeAbstractLogicalBinaryOperator_01(string op, bool binaryIsAbstract, bool trueIsAbstract, bool falseIsAbstract, bool success)
        {
            var source1 =
@"
interface I1
{
    " + (binaryIsAbstract ? "abstract " : "") + @"static I1 operator" + op + @" (I1 x, I1 y)" + (binaryIsAbstract ? ";" : " => throw null;") + @"
    " + (trueIsAbstract ? "abstract " : "") + @"static bool operator true (I1 x)" + (trueIsAbstract ? ";" : " => throw null;") + @"
    " + (falseIsAbstract ? "abstract " : "") + @"static bool operator false (I1 x)" + (falseIsAbstract ? ";" : " => throw null;") + @"

    static void M02(I1 x)
    {
        _ = x " + op + op + @" x;
    }

    void M03(I1 y)
    {
        _ = y " + op + op + @" y;
    }
}

class Test
{
    static void MT1(I1 a)
    {
        _ = a " + op + op + @" a;
    }

    static void MT2<T>() where T : I1
    {
        _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b " + op + op + @" b).ToString());
    }

    static void MT3(I1 b, dynamic c)
    {
        _ = b " + op + op + @" c;
    }
";
            if (!success)
            {
                source1 +=
@"
    static void MT4<T>() where T : I1
    {
        _ = (System.Linq.Expressions.Expression<System.Action<T, dynamic>>)((T d, dynamic e) => (d " + op + op + @" e).ToString());
    }
";
            }

            source1 +=
@"
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreAppAndCSharp);

            if (success)
            {
                Assert.False(binaryIsAbstract);
                Assert.False(op == "&" ? falseIsAbstract : trueIsAbstract);
                var binaryMetadataName = op == "&" ? "op_BitwiseAnd" : "op_BitwiseOr";
                var unaryMetadataName = op == "&" ? "op_False" : "op_True";

                var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

                verifier.VerifyIL("Test.MT1(I1)",
@"
{
  // Code size       22 (0x16)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloc.0
  IL_0004:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_0009:  brtrue.s   IL_0015
  IL_000b:  ldloc.0
  IL_000c:  ldarg.0
  IL_000d:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0012:  pop
  IL_0013:  br.s       IL_0015
  IL_0015:  ret
}
");

                if (op == "&")
                {
                    verifier.VerifyIL("Test.MT3(I1, dynamic)",
@"
{
  // Code size       97 (0x61)
  .maxstack  8
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  call       ""bool I1.op_False(I1)""
  IL_0007:  brtrue.s   IL_0060
  IL_0009:  ldsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_000e:  brfalse.s  IL_0012
  IL_0010:  br.s       IL_0047
  IL_0012:  ldc.i4.8
  IL_0013:  ldc.i4.2
  IL_0014:  ldtoken    ""Test""
  IL_0019:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_001e:  ldc.i4.2
  IL_001f:  newarr     ""Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo""
  IL_0024:  dup
  IL_0025:  ldc.i4.0
  IL_0026:  ldc.i4.1
  IL_0027:  ldnull
  IL_0028:  call       ""Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags, string)""
  IL_002d:  stelem.ref
  IL_002e:  dup
  IL_002f:  ldc.i4.1
  IL_0030:  ldc.i4.0
  IL_0031:  ldnull
  IL_0032:  call       ""Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags, string)""
  IL_0037:  stelem.ref
  IL_0038:  call       ""System.Runtime.CompilerServices.CallSiteBinder Microsoft.CSharp.RuntimeBinder.Binder.BinaryOperation(Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags, System.Linq.Expressions.ExpressionType, System.Type, System.Collections.Generic.IEnumerable<Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo>)""
  IL_003d:  call       ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>>.Create(System.Runtime.CompilerServices.CallSiteBinder)""
  IL_0042:  stsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_0047:  ldsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_004c:  ldfld      ""System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>>.Target""
  IL_0051:  ldsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_0056:  ldarg.0
  IL_0057:  ldarg.1
  IL_0058:  callvirt   ""dynamic System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>.Invoke(System.Runtime.CompilerServices.CallSite, I1, dynamic)""
  IL_005d:  pop
  IL_005e:  br.s       IL_0060
  IL_0060:  ret
}
");
                }
                else
                {
                    verifier.VerifyIL("Test.MT3(I1, dynamic)",
@"
{
  // Code size       98 (0x62)
  .maxstack  8
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  call       ""bool I1.op_True(I1)""
  IL_0007:  brtrue.s   IL_0061
  IL_0009:  ldsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_000e:  brfalse.s  IL_0012
  IL_0010:  br.s       IL_0048
  IL_0012:  ldc.i4.8
  IL_0013:  ldc.i4.s   36
  IL_0015:  ldtoken    ""Test""
  IL_001a:  call       ""System.Type System.Type.GetTypeFromHandle(System.RuntimeTypeHandle)""
  IL_001f:  ldc.i4.2
  IL_0020:  newarr     ""Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo""
  IL_0025:  dup
  IL_0026:  ldc.i4.0
  IL_0027:  ldc.i4.1
  IL_0028:  ldnull
  IL_0029:  call       ""Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags, string)""
  IL_002e:  stelem.ref
  IL_002f:  dup
  IL_0030:  ldc.i4.1
  IL_0031:  ldc.i4.0
  IL_0032:  ldnull
  IL_0033:  call       ""Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfoFlags, string)""
  IL_0038:  stelem.ref
  IL_0039:  call       ""System.Runtime.CompilerServices.CallSiteBinder Microsoft.CSharp.RuntimeBinder.Binder.BinaryOperation(Microsoft.CSharp.RuntimeBinder.CSharpBinderFlags, System.Linq.Expressions.ExpressionType, System.Type, System.Collections.Generic.IEnumerable<Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo>)""
  IL_003e:  call       ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>>.Create(System.Runtime.CompilerServices.CallSiteBinder)""
  IL_0043:  stsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_0048:  ldsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_004d:  ldfld      ""System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic> System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>>.Target""
  IL_0052:  ldsfld     ""System.Runtime.CompilerServices.CallSite<System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>> Test.<>o__2.<>p__0""
  IL_0057:  ldarg.0
  IL_0058:  ldarg.1
  IL_0059:  callvirt   ""dynamic System.Func<System.Runtime.CompilerServices.CallSite, I1, dynamic, dynamic>.Invoke(System.Runtime.CompilerServices.CallSite, I1, dynamic)""
  IL_005e:  pop
  IL_005f:  br.s       IL_0061
  IL_0061:  ret
}
");
                }
            }
            else
            {
                var builder = ArrayBuilder<DiagnosticDescription>.GetInstance();

                builder.AddRange(
                    // (10,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                    //         _ = x && x;
                    Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "x " + op + op + " x").WithLocation(10, 13),
                    // (15,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                    //         _ = y && y;
                    Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "y " + op + op + " y").WithLocation(15, 13),
                    // (23,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                    //         _ = a && a;
                    Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "a " + op + op + " a").WithLocation(23, 13),
                    // (28,78): error CS9108: An expression tree may not contain an access of static abstract interface member
                    //         _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b && b).ToString());
                    Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "b " + op + op + " b").WithLocation(28, 78)
                    );

                if (op == "&" ? falseIsAbstract : trueIsAbstract)
                {
                    builder.Add(
                        // (33,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                        //         _ = b || c;
                        Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "b " + op + op + " c").WithLocation(33, 13)
                        );
                }

                builder.Add(
                    // (38,98): error CS7083: Expression must be implicitly convertible to Boolean or its type 'T' must define operator 'true'.
                    //         _ = (System.Linq.Expressions.Expression<System.Action<T, dynamic>>)((T d, dynamic e) => (d || e).ToString());
                    Diagnostic(ErrorCode.ERR_InvalidDynamicCondition, "d").WithArguments("T", op == "&" ? "false" : "true").WithLocation(38, 98)
                    );

                compilation1.VerifyDiagnostics(builder.ToArrayAndFree());
            }
        }

        [Theory]
        [CombinatorialData]
        public void ConsumeAbstractCompoundBinaryOperator_01([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>")] string op)
        {
            var source1 =
@"
interface I1
{
    abstract static I1 operator" + op + @" (I1 x, int y);

    static void M02(I1 x)
    {
        x " + op + @"= 1;
    }

    void M03(I1 y)
    {
        y " + op + @"= 2;
    }
}

interface I2<T> where T : I2<T>
{
    abstract static T operator" + op + @" (T x, int y);
}

class Test
{
    static void MT1(I1 a)
    {
        a " + op + @"= 3;
    }

    static void MT2<T>() where T : I2<T>
    {
        _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b " + op + @"= 4).ToString());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         x /= 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "x " + op + "= 1").WithLocation(8, 9),
                // (13,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         y /= 2;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "y " + op + "= 2").WithLocation(13, 9),
                // (26,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         a /= 3;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "a " + op + "= 3").WithLocation(26, 9),
                // (31,78): error CS0832: An expression tree may not contain an assignment operator
                //         _ = (System.Linq.Expressions.Expression<System.Action<T>>)((T b) => (b /= 4).ToString());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAssignment, "b " + op + "= 4").WithLocation(31, 78)
                );
        }

        private static string BinaryOperatorKind(string op)
        {
            switch (op)
            {
                case "+":
                    return "Add";

                case "-":
                    return "Subtract";

                case "*":
                    return "Multiply";

                case "/":
                    return "Divide";

                case "%":
                    return "Remainder";

                case "<<":
                    return "LeftShift";

                case ">>":
                    return "RightShift";

                case "&":
                    return "And";

                case "|":
                    return "Or";

                case "^":
                    return "ExclusiveOr";

                case "<":
                    return "LessThan";

                case "<=":
                    return "LessThanOrEqual";

                case "==":
                    return "Equals";

                case "!=":
                    return "NotEquals";

                case ">=":
                    return "GreaterThanOrEqual";

                case ">":
                    return "GreaterThan";

            }

            throw TestExceptionUtilities.UnexpectedValue(op);
        }

        [Theory]
        [CombinatorialData]
        public void ConsumeAbstractBinaryOperator_03([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>")] string op)
        {
            string metadataName = BinaryOperatorName(op);

            bool isShiftOperator = op is "<<" or ">>";

            var source1 =
@"
public partial interface I1<T0> where T0 : I1<T0>
{
    abstract static T0 operator" + op + @" (T0 x, int a);
}

partial class Test
{
    static void M03<T, U>(T x) where T : U where U : I1<T>
    {
        _ = x " + op + @" 1;
    }

    static void M05<T, U>(T? y) where T : struct, U where U : I1<T>
    {
        _ = y " + op + @" 1;
    }
}
";

            if (!isShiftOperator)
            {
                source1 += @"
public partial interface I1<T0>
{
    abstract static T0 operator" + op + @" (int a, T0 x);
    abstract static T0 operator" + op + @" (I1<T0> x, T0 a);
    abstract static T0 operator" + op + @" (T0 x, I1<T0> a);
}

partial class Test
{
    static void M02<T, U>(T x) where T : U where U : I1<T>
    {
        _ = 1 " + op + @" x;
    }

    static void M04<T, U>(T? y) where T : struct, U where U : I1<T>
    {
        _ = 1 " + op + @" y;
    }

    static void M06<T, U>(I1<T> x, T y) where T : U where U : I1<T>
    {
        _ = x " + op + @" y;
    }

    static void M07<T, U>(T x, I1<T> y) where T : U where U : I1<T>
    {
        _ = x " + op + @" y;
    }
}
";
            }

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            if (!isShiftOperator)
            {
                verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldc.i4.1
  IL_0002:  ldarg.0
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""T I1<T>." + metadataName + @"(int, T)""
  IL_000e:  pop
  IL_000f:  ret
}
");
                verifier.VerifyIL("Test.M04<T, U>(T?)",
@"
{
  // Code size       35 (0x23)
  .maxstack  2
  .locals init (T? V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  call       ""readonly bool T?.HasValue.get""
  IL_000a:  brtrue.s   IL_000e
  IL_000c:  br.s       IL_0022
  IL_000e:  ldc.i4.1
  IL_000f:  ldloca.s   V_0
  IL_0011:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0016:  constrained. ""T""
  IL_001c:  call       ""T I1<T>." + metadataName + @"(int, T)""
  IL_0021:  pop
  IL_0022:  ret
}
");
                verifier.VerifyIL("Test.M06<T, U>(I1<T>, T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldarg.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""T I1<T>." + metadataName + @"(I1<T>, T)""
  IL_000e:  pop
  IL_000f:  ret
}
");

                verifier.VerifyIL("Test.M07<T, U>(T, I1<T>)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldarg.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""T I1<T>." + metadataName + @"(T, I1<T>)""
  IL_000e:  pop
  IL_000f:  ret
}
");
            }

            verifier.VerifyIL("Test.M03<T, U>(T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldc.i4.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_000e:  pop
  IL_000f:  ret
}
");

            verifier.VerifyIL("Test.M05<T, U>(T?)",
@"
{
  // Code size       35 (0x23)
  .maxstack  2
  .locals init (T? V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  call       ""readonly bool T?.HasValue.get""
  IL_000a:  brtrue.s   IL_000e
  IL_000c:  br.s       IL_0022
  IL_000e:  ldloca.s   V_0
  IL_0010:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0015:  ldc.i4.1
  IL_0016:  constrained. ""T""
  IL_001c:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_0021:  pop
  IL_0022:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            if (!isShiftOperator)
            {
                verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldc.i4.1
  IL_0001:  ldarg.0
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(int, T)""
  IL_000d:  pop
  IL_000e:  ret
}
");
                verifier.VerifyIL("Test.M04<T, U>(T?)",
@"
{
  // Code size       32 (0x20)
  .maxstack  2
  .locals init (T? V_0)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  call       ""readonly bool T?.HasValue.get""
  IL_0009:  brfalse.s  IL_001f
  IL_000b:  ldc.i4.1
  IL_000c:  ldloca.s   V_0
  IL_000e:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0013:  constrained. ""T""
  IL_0019:  call       ""T I1<T>." + metadataName + @"(int, T)""
  IL_001e:  pop
  IL_001f:  ret
}
");
                verifier.VerifyIL("Test.M06<T, U>(I1<T>, T)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(I1<T>, T)""
  IL_000d:  pop
  IL_000e:  ret
}
");

                verifier.VerifyIL("Test.M07<T, U>(T, I1<T>)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(T, I1<T>)""
  IL_000d:  pop
  IL_000e:  ret
}
");
            }

            verifier.VerifyIL("Test.M03<T, U>(T)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_000d:  pop
  IL_000e:  ret
}
");

            verifier.VerifyIL("Test.M05<T, U>(T?)",
@"
{
  // Code size       32 (0x20)
  .maxstack  2
  .locals init (T? V_0)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  call       ""readonly bool T?.HasValue.get""
  IL_0009:  brfalse.s  IL_001f
  IL_000b:  ldloca.s   V_0
  IL_000d:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0012:  ldc.i4.1
  IL_0013:  constrained. ""T""
  IL_0019:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_001e:  pop
  IL_001f:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Where(n => n.ToString() == "x " + op + " 1").Single();

            Assert.Equal("x " + op + " 1", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IBinaryOperation (BinaryOperatorKind." + BinaryOperatorKind(op) + @") (OperatorMethod: T I1<T>." + metadataName + @"(T x, System.Int32 a)) (OperationKind.Binary, Type: T) (Syntax: 'x " + op + @" 1')
  Left: 
    IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
  Right: 
    ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 1) (Syntax: '1')
");
        }

        [Theory]
        [CombinatorialData]
        public void ConsumeAbstractComparisonBinaryOperator_03([CombinatorialValues("<", ">", "<=", ">=", "==", "!=")] string op)
        {
            string metadataName = BinaryOperatorName(op);

            var source1 =
@"
public partial interface I1<T0> where T0 : I1<T0>
{
    abstract static bool operator" + op + @" (T0 x, int a);
    abstract static bool operator" + op + @" (int a, T0 x);
    abstract static bool operator" + op + @" (I1<T0> x, T0 a);
    abstract static bool operator" + op + @" (T0 x, I1<T0> a);
}

partial class Test
{
    static void M02<T, U>(T x) where T : U where U : I1<T>
    {
        _ = 1 " + op + @" x;
    }

    static void M03<T, U>(T x) where T : U where U : I1<T>
    {
        _ = x " + op + @" 1;
    }

    static void M06<T, U>(I1<T> x, T y) where T : U where U : I1<T>
    {
        _ = x " + op + @" y;
    }

    static void M07<T, U>(T x, I1<T> y) where T : U where U : I1<T>
    {
        _ = x " + op + @" y;
    }
}
";
            string matchingOp = MatchingBinaryOperator(op);

            source1 +=
@"
public partial interface I1<T0>
{
    abstract static bool operator" + matchingOp + @" (T0 x, int a);
    abstract static bool operator" + matchingOp + @" (int a, T0 x);
    abstract static bool operator" + matchingOp + @" (I1<T0> x, T0 a);
    abstract static bool operator" + matchingOp + @" (T0 x, I1<T0> a);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldc.i4.1
  IL_0002:  ldarg.0
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""bool I1<T>." + metadataName + @"(int, T)""
  IL_000e:  pop
  IL_000f:  ret
}
");
            verifier.VerifyIL("Test.M06<T, U>(I1<T>, T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldarg.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""bool I1<T>." + metadataName + @"(I1<T>, T)""
  IL_000e:  pop
  IL_000f:  ret
}
");

            verifier.VerifyIL("Test.M07<T, U>(T, I1<T>)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldarg.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""bool I1<T>." + metadataName + @"(T, I1<T>)""
  IL_000e:  pop
  IL_000f:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>(T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldc.i4.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""bool I1<T>." + metadataName + @"(T, int)""
  IL_000e:  pop
  IL_000f:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>(T)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldc.i4.1
  IL_0001:  ldarg.0
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""bool I1<T>." + metadataName + @"(int, T)""
  IL_000d:  pop
  IL_000e:  ret
}
");
            verifier.VerifyIL("Test.M06<T, U>(I1<T>, T)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""bool I1<T>." + metadataName + @"(I1<T>, T)""
  IL_000d:  pop
  IL_000e:  ret
}
");

            verifier.VerifyIL("Test.M07<T, U>(T, I1<T>)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""bool I1<T>." + metadataName + @"(T, I1<T>)""
  IL_000d:  pop
  IL_000e:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>(T)",
@"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""bool I1<T>." + metadataName + @"(T, int)""
  IL_000d:  pop
  IL_000e:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Where(n => n.ToString() == "x " + op + " 1").Single();

            Assert.Equal("x " + op + " 1", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IBinaryOperation (BinaryOperatorKind." + BinaryOperatorKind(op) + @") (OperatorMethod: System.Boolean I1<T>." + metadataName + @"(T x, System.Int32 a)) (OperationKind.Binary, Type: System.Boolean) (Syntax: 'x " + op + @" 1')
  Left: 
    IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
  Right: 
    ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 1) (Syntax: '1')
");
        }

        [Theory]
        [CombinatorialData]
        public void ConsumeAbstractLiftedComparisonBinaryOperator_03([CombinatorialValues("<", ">", "<=", ">=", "==", "!=")] string op)
        {
            string metadataName = BinaryOperatorName(op);

            var source1 =
@"
public partial interface I1<T0> where T0 : I1<T0>
{
    abstract static bool operator" + op + @" (T0 x, T0 a);
}

partial class Test
{
    static void M04<T, U>(T? x, T? y) where T : struct, U where U : I1<T>
    {
        _ = x " + op + @" y;
    }
}
";
            string matchingOp = MatchingBinaryOperator(op);

            source1 +=
@"
public partial interface I1<T0>
{
    abstract static bool operator" + matchingOp + @" (T0 x, T0 a);
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            if (op is "==" or "!=")
            {
                verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       61 (0x3d)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldarg.1
  IL_0004:  stloc.1
  IL_0005:  ldloca.s   V_0
  IL_0007:  call       ""readonly bool T?.HasValue.get""
  IL_000c:  ldloca.s   V_1
  IL_000e:  call       ""readonly bool T?.HasValue.get""
  IL_0013:  beq.s      IL_0017
  IL_0015:  br.s       IL_003c
  IL_0017:  ldloca.s   V_0
  IL_0019:  call       ""readonly bool T?.HasValue.get""
  IL_001e:  brtrue.s   IL_0022
  IL_0020:  br.s       IL_003c
  IL_0022:  ldloca.s   V_0
  IL_0024:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0029:  ldloca.s   V_1
  IL_002b:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0030:  constrained. ""T""
  IL_0036:  call       ""bool I1<T>." + metadataName + @"(T, T)""
  IL_003b:  pop
  IL_003c:  ret
}
");
            }
            else
            {
                verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       51 (0x33)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldarg.1
  IL_0004:  stloc.1
  IL_0005:  ldloca.s   V_0
  IL_0007:  call       ""readonly bool T?.HasValue.get""
  IL_000c:  ldloca.s   V_1
  IL_000e:  call       ""readonly bool T?.HasValue.get""
  IL_0013:  and
  IL_0014:  brtrue.s   IL_0018
  IL_0016:  br.s       IL_0032
  IL_0018:  ldloca.s   V_0
  IL_001a:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001f:  ldloca.s   V_1
  IL_0021:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0026:  constrained. ""T""
  IL_002c:  call       ""bool I1<T>." + metadataName + @"(T, T)""
  IL_0031:  pop
  IL_0032:  ret
}
");
            }

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            if (op is "==" or "!=")
            {
                verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       56 (0x38)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldarg.1
  IL_0003:  stloc.1
  IL_0004:  ldloca.s   V_0
  IL_0006:  call       ""readonly bool T?.HasValue.get""
  IL_000b:  ldloca.s   V_1
  IL_000d:  call       ""readonly bool T?.HasValue.get""
  IL_0012:  bne.un.s   IL_0037
  IL_0014:  ldloca.s   V_0
  IL_0016:  call       ""readonly bool T?.HasValue.get""
  IL_001b:  brfalse.s  IL_0037
  IL_001d:  ldloca.s   V_0
  IL_001f:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0024:  ldloca.s   V_1
  IL_0026:  call       ""readonly T T?.GetValueOrDefault()""
  IL_002b:  constrained. ""T""
  IL_0031:  call       ""bool I1<T>." + metadataName + @"(T, T)""
  IL_0036:  pop
  IL_0037:  ret
}
");

            }
            else
            {
                verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       48 (0x30)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldarg.1
  IL_0003:  stloc.1
  IL_0004:  ldloca.s   V_0
  IL_0006:  call       ""readonly bool T?.HasValue.get""
  IL_000b:  ldloca.s   V_1
  IL_000d:  call       ""readonly bool T?.HasValue.get""
  IL_0012:  and
  IL_0013:  brfalse.s  IL_002f
  IL_0015:  ldloca.s   V_0
  IL_0017:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001c:  ldloca.s   V_1
  IL_001e:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0023:  constrained. ""T""
  IL_0029:  call       ""bool I1<T>." + metadataName + @"(T, T)""
  IL_002e:  pop
  IL_002f:  ret
}
");
            }

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Where(n => n.ToString() == "x " + op + " y").Single();

            Assert.Equal("x " + op + " y", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IBinaryOperation (BinaryOperatorKind." + BinaryOperatorKind(op) + @", IsLifted) (OperatorMethod: System.Boolean I1<T>." + metadataName + @"(T x, T a)) (OperationKind.Binary, Type: System.Boolean) (Syntax: 'x " + op + @" y')
  Left: 
    IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T?) (Syntax: 'x')
  Right: 
    IParameterReferenceOperation: y (OperationKind.ParameterReference, Type: T?) (Syntax: 'y')
");
        }

        [Theory]
        [InlineData("&", true, true)]
        [InlineData("|", true, true)]
        [InlineData("&", true, false)]
        [InlineData("|", true, false)]
        [InlineData("&", false, true)]
        [InlineData("|", false, true)]
        public void ConsumeAbstractLogicalBinaryOperator_03(string op, bool binaryIsAbstract, bool unaryIsAbstract)
        {
            var binaryMetadataName = op == "&" ? "op_BitwiseAnd" : "op_BitwiseOr";
            var unaryMetadataName = op == "&" ? "op_False" : "op_True";
            var opKind = op == "&" ? "ConditionalAnd" : "ConditionalOr";

            if (binaryIsAbstract && unaryIsAbstract)
            {
                consumeAbstract(op);
            }
            else
            {
                consumeMixed(op, binaryIsAbstract, unaryIsAbstract);
            }

            void consumeAbstract(string op)
            {
                var source1 =
@"
public interface I1<T0> where T0 : I1<T0>
{
    abstract static T0 operator" + op + @" (T0 a, T0 x);
    abstract static bool operator true (T0 x);
    abstract static bool operator false (T0 x);
}

public interface I2<T0> where T0 : struct, I2<T0>
{
    abstract static T0 operator" + op + @" (T0 a, T0 x);
    abstract static bool operator true (T0? x);
    abstract static bool operator false (T0? x);
}

class Test
{
    static void M03<T, U>(T x, T y) where T : U where U : I1<T>
    {
        _ = x " + op + op + @" y;
    }

    static void M04<T, U>(T? x, T? y) where T : struct, U where U : I2<T>
    {
        _ = x " + op + op + @" y;
    }
}
";
                var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreAppAndCSharp);

                var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();
                verifier.VerifyIL("Test.M03<T, U>(T, T)",
@"
{
  // Code size       34 (0x22)
  .maxstack  2
  .locals init (T V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloc.0
  IL_0004:  constrained. ""T""
  IL_000a:  call       ""bool I1<T>." + unaryMetadataName + @"(T)""
  IL_000f:  brtrue.s   IL_0021
  IL_0011:  ldloc.0
  IL_0012:  ldarg.1
  IL_0013:  constrained. ""T""
  IL_0019:  call       ""T I1<T>." + binaryMetadataName + @"(T, T)""
  IL_001e:  pop
  IL_001f:  br.s       IL_0021
  IL_0021:  ret
}
");
                verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       69 (0x45)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1,
                T? V_2)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloc.0
  IL_0004:  constrained. ""T""
  IL_000a:  call       ""bool I2<T>." + unaryMetadataName + @"(T?)""
  IL_000f:  brtrue.s   IL_0044
  IL_0011:  ldloc.0
  IL_0012:  stloc.1
  IL_0013:  ldarg.1
  IL_0014:  stloc.2
  IL_0015:  ldloca.s   V_1
  IL_0017:  call       ""readonly bool T?.HasValue.get""
  IL_001c:  ldloca.s   V_2
  IL_001e:  call       ""readonly bool T?.HasValue.get""
  IL_0023:  and
  IL_0024:  brtrue.s   IL_0028
  IL_0026:  br.s       IL_0042
  IL_0028:  ldloca.s   V_1
  IL_002a:  call       ""readonly T T?.GetValueOrDefault()""
  IL_002f:  ldloca.s   V_2
  IL_0031:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0036:  constrained. ""T""
  IL_003c:  call       ""T I2<T>." + binaryMetadataName + @"(T, T)""
  IL_0041:  pop
  IL_0042:  br.s       IL_0044
  IL_0044:  ret
}
");

                compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreAppAndCSharp);

                verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();
                verifier.VerifyIL("Test.M03<T, U>(T, T)",
@"
{
  // Code size       31 (0x1f)
  .maxstack  2
  .locals init (T V_0)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloc.0
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""bool I1<T>." + unaryMetadataName + @"(T)""
  IL_000e:  brtrue.s   IL_001e
  IL_0010:  ldloc.0
  IL_0011:  ldarg.1
  IL_0012:  constrained. ""T""
  IL_0018:  call       ""T I1<T>." + binaryMetadataName + @"(T, T)""
  IL_001d:  pop
  IL_001e:  ret
}
");
                verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       64 (0x40)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1,
                T? V_2)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloc.0
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""bool I2<T>." + unaryMetadataName + @"(T?)""
  IL_000e:  brtrue.s   IL_003f
  IL_0010:  ldloc.0
  IL_0011:  stloc.1
  IL_0012:  ldarg.1
  IL_0013:  stloc.2
  IL_0014:  ldloca.s   V_1
  IL_0016:  call       ""readonly bool T?.HasValue.get""
  IL_001b:  ldloca.s   V_2
  IL_001d:  call       ""readonly bool T?.HasValue.get""
  IL_0022:  and
  IL_0023:  brfalse.s  IL_003f
  IL_0025:  ldloca.s   V_1
  IL_0027:  call       ""readonly T T?.GetValueOrDefault()""
  IL_002c:  ldloca.s   V_2
  IL_002e:  call       ""readonly T T?.GetValueOrDefault()""
  IL_0033:  constrained. ""T""
  IL_0039:  call       ""T I2<T>." + binaryMetadataName + @"(T, T)""
  IL_003e:  pop
  IL_003f:  ret
}
");

                var tree = compilation1.SyntaxTrees.Single();
                var model = compilation1.GetSemanticModel(tree);
                var node1 = tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Where(n => n.ToString() == "x " + op + op + " y").First();

                Assert.Equal("x " + op + op + " y", node1.ToString());

                VerifyOperationTreeForNode(compilation1, model, node1,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IBinaryOperation (BinaryOperatorKind." + opKind + @") (OperatorMethod: T I1<T>." + binaryMetadataName + @"(T a, T x)) (OperationKind.Binary, Type: T) (Syntax: 'x " + op + op + @" y')
  Left: 
    IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
  Right: 
    IParameterReferenceOperation: y (OperationKind.ParameterReference, Type: T) (Syntax: 'y')
");
            }

            void consumeMixed(string op, bool binaryIsAbstract, bool unaryIsAbstract)
            {
                var source1 =
@"
public interface I1
{
    " + (binaryIsAbstract ? "abstract " : "") + @"static I1 operator" + op + @" (I1 a, I1 x)" + (binaryIsAbstract ? ";" : " => throw null;") + @"
    " + (unaryIsAbstract ? "abstract " : "") + @"static bool operator true (I1 x)" + (unaryIsAbstract ? ";" : " => throw null;") + @"
    " + (unaryIsAbstract ? "abstract " : "") + @"static bool operator false (I1 x)" + (unaryIsAbstract ? ";" : " => throw null;") + @"
}

class Test
{
    static void M03<T, U>(T x, T y) where T : U where U : I1
    {
        _ = x " + op + op + @" y;
    }

    static void M04<T, U>(T? x, T? y) where T : struct, U where U : I1
    {
        _ = x " + op + op + @" y;
    }
}
";
                var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreAppAndCSharp);

                var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

                switch (binaryIsAbstract, unaryIsAbstract)
                {
                    case (true, false):
                        verifier.VerifyIL("Test.M03<T, U>(T, T)",
@"
{
  // Code size       38 (0x26)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  box        ""T""
  IL_0007:  stloc.0
  IL_0008:  ldloc.0
  IL_0009:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_000e:  brtrue.s   IL_0025
  IL_0010:  ldloc.0
  IL_0011:  ldarg.1
  IL_0012:  box        ""T""
  IL_0017:  constrained. ""T""
  IL_001d:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0022:  pop
  IL_0023:  br.s       IL_0025
  IL_0025:  ret
}
");
                        verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       38 (0x26)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  box        ""T?""
  IL_0007:  stloc.0
  IL_0008:  ldloc.0
  IL_0009:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_000e:  brtrue.s   IL_0025
  IL_0010:  ldloc.0
  IL_0011:  ldarg.1
  IL_0012:  box        ""T?""
  IL_0017:  constrained. ""T""
  IL_001d:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0022:  pop
  IL_0023:  br.s       IL_0025
  IL_0025:  ret
}
");
                        break;

                    case (false, true):
                        verifier.VerifyIL("Test.M03<T, U>(T, T)",
@"
{
  // Code size       38 (0x26)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  box        ""T""
  IL_0007:  stloc.0
  IL_0008:  ldloc.0
  IL_0009:  constrained. ""T""
  IL_000f:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_0014:  brtrue.s   IL_0025
  IL_0016:  ldloc.0
  IL_0017:  ldarg.1
  IL_0018:  box        ""T""
  IL_001d:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0022:  pop
  IL_0023:  br.s       IL_0025
  IL_0025:  ret
}
");
                        verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       38 (0x26)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  box        ""T?""
  IL_0007:  stloc.0
  IL_0008:  ldloc.0
  IL_0009:  constrained. ""T""
  IL_000f:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_0014:  brtrue.s   IL_0025
  IL_0016:  ldloc.0
  IL_0017:  ldarg.1
  IL_0018:  box        ""T?""
  IL_001d:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0022:  pop
  IL_0023:  br.s       IL_0025
  IL_0025:  ret
}
");
                        break;

                    default:
                        Assert.True(false);
                        break;
                }

                compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreAppAndCSharp);

                verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

                switch (binaryIsAbstract, unaryIsAbstract)
                {
                    case (true, false):
                        verifier.VerifyIL("Test.M03<T, U>(T, T)",
@"
{
  // Code size       35 (0x23)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  ldarg.0
  IL_0001:  box        ""T""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_000d:  brtrue.s   IL_0022
  IL_000f:  ldloc.0
  IL_0010:  ldarg.1
  IL_0011:  box        ""T""
  IL_0016:  constrained. ""T""
  IL_001c:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0021:  pop
  IL_0022:  ret
}
");
                        verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       35 (0x23)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  ldarg.0
  IL_0001:  box        ""T?""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_000d:  brtrue.s   IL_0022
  IL_000f:  ldloc.0
  IL_0010:  ldarg.1
  IL_0011:  box        ""T?""
  IL_0016:  constrained. ""T""
  IL_001c:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0021:  pop
  IL_0022:  ret
}
");
                        break;

                    case (false, true):
                        verifier.VerifyIL("Test.M03<T, U>(T, T)",
@"
{
  // Code size       35 (0x23)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  ldarg.0
  IL_0001:  box        ""T""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  constrained. ""T""
  IL_000e:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_0013:  brtrue.s   IL_0022
  IL_0015:  ldloc.0
  IL_0016:  ldarg.1
  IL_0017:  box        ""T""
  IL_001c:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0021:  pop
  IL_0022:  ret
}
");
                        verifier.VerifyIL("Test.M04<T, U>(T?, T?)",
@"
{
  // Code size       35 (0x23)
  .maxstack  2
  .locals init (I1 V_0)
  IL_0000:  ldarg.0
  IL_0001:  box        ""T?""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  constrained. ""T""
  IL_000e:  call       ""bool I1." + unaryMetadataName + @"(I1)""
  IL_0013:  brtrue.s   IL_0022
  IL_0015:  ldloc.0
  IL_0016:  ldarg.1
  IL_0017:  box        ""T?""
  IL_001c:  call       ""I1 I1." + binaryMetadataName + @"(I1, I1)""
  IL_0021:  pop
  IL_0022:  ret
}
");
                        break;

                    default:
                        Assert.True(false);
                        break;
                }

                var tree = compilation1.SyntaxTrees.Single();
                var model = compilation1.GetSemanticModel(tree);
                var node1 = tree.GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Where(n => n.ToString() == "x " + op + op + " y").First();

                Assert.Equal("x " + op + op + " y", node1.ToString());

                VerifyOperationTreeForNode(compilation1, model, node1,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IBinaryOperation (BinaryOperatorKind." + opKind + @") (OperatorMethod: I1 I1." + binaryMetadataName + @"(I1 a, I1 x)) (OperationKind.Binary, Type: I1) (Syntax: 'x " + op + op + @" y')
  Left: 
    IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: I1, IsImplicit) (Syntax: 'x')
      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Operand: 
        IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
  Right: 
    IConversionOperation (TryCast: False, Unchecked) (OperationKind.Conversion, Type: I1, IsImplicit) (Syntax: 'y')
      Conversion: CommonConversion (Exists: True, IsIdentity: False, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
      Operand: 
        IParameterReferenceOperation: y (OperationKind.ParameterReference, Type: T) (Syntax: 'y')
");
            }
        }

        [Theory]
        [InlineData("+", "op_Addition", "Add")]
        [InlineData("-", "op_Subtraction", "Subtract")]
        [InlineData("*", "op_Multiply", "Multiply")]
        [InlineData("/", "op_Division", "Divide")]
        [InlineData("%", "op_Modulus", "Remainder")]
        [InlineData("&", "op_BitwiseAnd", "And")]
        [InlineData("|", "op_BitwiseOr", "Or")]
        [InlineData("^", "op_ExclusiveOr", "ExclusiveOr")]
        [InlineData("<<", "op_LeftShift", "LeftShift")]
        [InlineData(">>", "op_RightShift", "RightShift")]
        public void ConsumeAbstractCompoundBinaryOperator_03(string op, string metadataName, string operatorKind)
        {
            bool isShiftOperator = op.Length == 2;

            var source1 =
@"
public interface I1<T0> where T0 : I1<T0>
{
";
            if (!isShiftOperator)
            {
                source1 += @"
    abstract static int operator" + op + @" (int a, T0 x);
    abstract static I1<T0> operator" + op + @" (I1<T0> x, T0 a);
    abstract static T0 operator" + op + @" (T0 x, I1<T0> a);
";
            }

            source1 += @"
    abstract static T0 operator" + op + @" (T0 x, int a);
}

class Test
{
";
            if (!isShiftOperator)
            {
                source1 += @"
    static void M02<T, U>(int a, T x) where T : U where U : I1<T>
    {
        a " + op + @"= x;
    }

    static void M04<T, U>(int? a, T? y) where T : struct, U where U : I1<T>
    {
        a " + op + @"= y;
    }

    static void M06<T, U>(I1<T> x, T y) where T : U where U : I1<T>
    {
        x " + op + @"= y;
    }

    static void M07<T, U>(T x, I1<T> y) where T : U where U : I1<T>
    {
        x " + op + @"= y;
    }
";
            }

            source1 += @"
    static void M03<T, U>(T x) where T : U where U : I1<T>
    {
        x " + op + @"= 1;
    }

    static void M05<T, U>(T? y) where T : struct, U where U : I1<T>
    {
        y " + op + @"= 1;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            if (!isShiftOperator)
            {
                verifier.VerifyIL("Test.M02<T, U>(int, T)",
@"
{
  // Code size       17 (0x11)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldarg.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""int I1<T>." + metadataName + @"(int, T)""
  IL_000e:  starg.s    V_0
  IL_0010:  ret
}
");
                verifier.VerifyIL("Test.M04<T, U>(int?, T?)",
@"
{
  // Code size       66 (0x42)
  .maxstack  2
  .locals init (int? V_0,
                T? V_1,
                int? V_2)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldarg.1
  IL_0004:  stloc.1
  IL_0005:  ldloca.s   V_0
  IL_0007:  call       ""readonly bool int?.HasValue.get""
  IL_000c:  ldloca.s   V_1
  IL_000e:  call       ""readonly bool T?.HasValue.get""
  IL_0013:  and
  IL_0014:  brtrue.s   IL_0021
  IL_0016:  ldloca.s   V_2
  IL_0018:  initobj    ""int?""
  IL_001e:  ldloc.2
  IL_001f:  br.s       IL_003f
  IL_0021:  ldloca.s   V_0
  IL_0023:  call       ""readonly int int?.GetValueOrDefault()""
  IL_0028:  ldloca.s   V_1
  IL_002a:  call       ""readonly T T?.GetValueOrDefault()""
  IL_002f:  constrained. ""T""
  IL_0035:  call       ""int I1<T>." + metadataName + @"(int, T)""
  IL_003a:  newobj     ""int?..ctor(int)""
  IL_003f:  starg.s    V_0
  IL_0041:  ret
}
");
                verifier.VerifyIL("Test.M06<T, U>(I1<T>, T)",
@"
{
  // Code size       17 (0x11)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldarg.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""I1<T> I1<T>." + metadataName + @"(I1<T>, T)""
  IL_000e:  starg.s    V_0
  IL_0010:  ret
}
");

                verifier.VerifyIL("Test.M07<T, U>(T, I1<T>)",
@"
{
  // Code size       17 (0x11)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldarg.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""T I1<T>." + metadataName + @"(T, I1<T>)""
  IL_000e:  starg.s    V_0
  IL_0010:  ret
}
");
            }

            verifier.VerifyIL("Test.M03<T, U>(T)",
@"
{
  // Code size       17 (0x11)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  ldc.i4.1
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_000e:  starg.s    V_0
  IL_0010:  ret
}
");

            verifier.VerifyIL("Test.M05<T, U>(T?)",
@"
{
  // Code size       50 (0x32)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  nop
  IL_0001:  ldarg.0
  IL_0002:  stloc.0
  IL_0003:  ldloca.s   V_0
  IL_0005:  call       ""readonly bool T?.HasValue.get""
  IL_000a:  brtrue.s   IL_0017
  IL_000c:  ldloca.s   V_1
  IL_000e:  initobj    ""T?""
  IL_0014:  ldloc.1
  IL_0015:  br.s       IL_002f
  IL_0017:  ldloca.s   V_0
  IL_0019:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001e:  ldc.i4.1
  IL_001f:  constrained. ""T""
  IL_0025:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_002a:  newobj     ""T?..ctor(T)""
  IL_002f:  starg.s    V_0
  IL_0031:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            if (!isShiftOperator)
            {
                verifier.VerifyIL("Test.M02<T, U>(int, T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""int I1<T>." + metadataName + @"(int, T)""
  IL_000d:  starg.s    V_0
  IL_000f:  ret
}
");
                verifier.VerifyIL("Test.M04<T, U>(int?, T?)",
@"
{
  // Code size       65 (0x41)
  .maxstack  2
  .locals init (int? V_0,
                T? V_1,
                int? V_2)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldarg.1
  IL_0003:  stloc.1
  IL_0004:  ldloca.s   V_0
  IL_0006:  call       ""readonly bool int?.HasValue.get""
  IL_000b:  ldloca.s   V_1
  IL_000d:  call       ""readonly bool T?.HasValue.get""
  IL_0012:  and
  IL_0013:  brtrue.s   IL_0020
  IL_0015:  ldloca.s   V_2
  IL_0017:  initobj    ""int?""
  IL_001d:  ldloc.2
  IL_001e:  br.s       IL_003e
  IL_0020:  ldloca.s   V_0
  IL_0022:  call       ""readonly int int?.GetValueOrDefault()""
  IL_0027:  ldloca.s   V_1
  IL_0029:  call       ""readonly T T?.GetValueOrDefault()""
  IL_002e:  constrained. ""T""
  IL_0034:  call       ""int I1<T>." + metadataName + @"(int, T)""
  IL_0039:  newobj     ""int?..ctor(int)""
  IL_003e:  starg.s    V_0
  IL_0040:  ret
}
");
                verifier.VerifyIL("Test.M06<T, U>(I1<T>, T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""I1<T> I1<T>." + metadataName + @"(I1<T>, T)""
  IL_000d:  starg.s    V_0
  IL_000f:  ret
}
");

                verifier.VerifyIL("Test.M07<T, U>(T, I1<T>)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(T, I1<T>)""
  IL_000d:  starg.s    V_0
  IL_000f:  ret
}
");
            }

            verifier.VerifyIL("Test.M03<T, U>(T)",
@"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_000d:  starg.s    V_0
  IL_000f:  ret
}
");

            verifier.VerifyIL("Test.M05<T, U>(T?)",
@"
{
  // Code size       49 (0x31)
  .maxstack  2
  .locals init (T? V_0,
                T? V_1)
  IL_0000:  ldarg.0
  IL_0001:  stloc.0
  IL_0002:  ldloca.s   V_0
  IL_0004:  call       ""readonly bool T?.HasValue.get""
  IL_0009:  brtrue.s   IL_0016
  IL_000b:  ldloca.s   V_1
  IL_000d:  initobj    ""T?""
  IL_0013:  ldloc.1
  IL_0014:  br.s       IL_002e
  IL_0016:  ldloca.s   V_0
  IL_0018:  call       ""readonly T T?.GetValueOrDefault()""
  IL_001d:  ldc.i4.1
  IL_001e:  constrained. ""T""
  IL_0024:  call       ""T I1<T>." + metadataName + @"(T, int)""
  IL_0029:  newobj     ""T?..ctor(T)""
  IL_002e:  starg.s    V_0
  IL_0030:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>().Where(n => n.ToString() == "x " + op + "= 1").Single();

            Assert.Equal("x " + op + "= 1", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" constraint is important for this operator, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
ICompoundAssignmentOperation (BinaryOperatorKind." + operatorKind + @") (OperatorMethod: T I1<T>." + metadataName + @"(T x, System.Int32 a)) (OperationKind.CompoundAssignment, Type: T) (Syntax: 'x " + op + @"= 1')
  InConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
  OutConversion: CommonConversion (Exists: True, IsIdentity: True, IsNumeric: False, IsReference: False, IsUserDefined: False) (MethodSymbol: null)
  Left: 
    IParameterReferenceOperation: x (OperationKind.ParameterReference, Type: T) (Syntax: 'x')
  Right: 
    ILiteralOperation (OperationKind.Literal, Type: System.Int32, Constant: 1) (Syntax: '1')
");
        }

        [Theory]
        [CombinatorialData]
        public void ConsumeAbstractBinaryOperator_04([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator" + op + @" (I1 x, int y);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x, int y) where T : I1
    {
        _ = x " + op + @" y;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         _ = x - y;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "x " + op + " y").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (12,32): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator- (I1 x, int y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(12, 32)
                );
        }

        [Theory]
        [InlineData("&", true, false, false, false)]
        [InlineData("|", true, false, false, false)]
        [InlineData("&", false, false, true, false)]
        [InlineData("|", false, true, false, false)]
        [InlineData("&", true, false, true, false)]
        [InlineData("|", true, true, false, false)]
        [InlineData("&", false, true, false, true)]
        [InlineData("|", false, false, true, true)]
        public void ConsumeAbstractLogicalBinaryOperator_04(string op, bool binaryIsAbstract, bool trueIsAbstract, bool falseIsAbstract, bool success)
        {
            var source1 =
@"
public interface I1
{
    " + (binaryIsAbstract ? "abstract " : "") + @"static I1 operator" + op + @" (I1 x, I1 y)" + (binaryIsAbstract ? ";" : " => throw null;") + @"
    " + (trueIsAbstract ? "abstract " : "") + @"static bool operator true (I1 x)" + (trueIsAbstract ? ";" : " => throw null;") + @"
    " + (falseIsAbstract ? "abstract " : "") + @"static bool operator false (I1 x)" + (falseIsAbstract ? ";" : " => throw null;") + @"
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x, T y) where T : I1
    {
        _ = x " + op + op + @" y;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreAppAndCSharp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            if (success)
            {
                compilation2.VerifyDiagnostics();
            }
            else
            {
                compilation2.VerifyDiagnostics(
                    // (6,13): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                    //         _ = x && y;
                    Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "x " + op + op + " y").WithLocation(6, 13)
                    );
            }

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            var builder = ArrayBuilder<DiagnosticDescription>.GetInstance();

            if (binaryIsAbstract)
            {
                builder.Add(
                    // (12,32): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                    //     abstract static I1 operator& (I1 x, I1 y);
                    Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(12, 32)
                    );
            }

            if (trueIsAbstract)
            {
                builder.Add(
                    // (13,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                    //     abstract static bool operator true (I1 x);
                    Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "true").WithLocation(13, 35)
                    );
            }

            if (falseIsAbstract)
            {
                builder.Add(
                    // (14,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                    //     abstract static bool operator false (I1 x);
                    Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "false").WithLocation(14, 35)
                    );
            }

            compilation3.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_RuntimeDoesNotSupportDefaultInterfaceImplementation).Verify(builder.ToArrayAndFree());
        }

        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("%")]
        [InlineData("&")]
        [InlineData("|")]
        [InlineData("^")]
        [InlineData("<<")]
        [InlineData(">>")]
        public void ConsumeAbstractCompoundBinaryOperator_04(string op)
        {
            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator" + op + @" (T x, int y);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x, int y) where T : I1<T>
    {
        x " + op + @"= y;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         x *= y;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "x " + op + "= y").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static T operator* (T x, int y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(12, 31)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ConsumeAbstractBinaryOperator_06([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator" + op + @" (I1 x, int y);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x, int y) where T : I1
    {
        _ = x " + op + @" y;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = x - y;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "x " + op + " y").WithArguments("static abstract members in interfaces").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (12,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static I1 operator- (I1 x, int y);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, op).WithArguments("abstract", "9.0", "preview").WithLocation(12, 32)
                );
        }

        [Theory]
        [InlineData("&", true, false, false, false)]
        [InlineData("|", true, false, false, false)]
        [InlineData("&", false, false, true, false)]
        [InlineData("|", false, true, false, false)]
        [InlineData("&", true, false, true, false)]
        [InlineData("|", true, true, false, false)]
        [InlineData("&", false, true, false, true)]
        [InlineData("|", false, false, true, true)]
        public void ConsumeAbstractLogicalBinaryOperator_06(string op, bool binaryIsAbstract, bool trueIsAbstract, bool falseIsAbstract, bool success)
        {
            var source1 =
@"
public interface I1
{
    " + (binaryIsAbstract ? "abstract " : "") + @"static I1 operator" + op + @" (I1 x, I1 y)" + (binaryIsAbstract ? ";" : " => throw null;") + @"
    " + (trueIsAbstract ? "abstract " : "") + @"static bool operator true (I1 x)" + (trueIsAbstract ? ";" : " => throw null;") + @"
    " + (falseIsAbstract ? "abstract " : "") + @"static bool operator false (I1 x)" + (falseIsAbstract ? ";" : " => throw null;") + @"
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x, T y) where T : I1
    {
        _ = x " + op + op + @" y;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreAppAndCSharp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            if (success)
            {
                compilation2.VerifyDiagnostics();
            }
            else
            {
                compilation2.VerifyDiagnostics(
                    // (6,13): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                    //         _ = x && y;
                    Diagnostic(ErrorCode.ERR_FeatureInPreview, "x " + op + op + " y").WithArguments("static abstract members in interfaces").WithLocation(6, 13)
                    );
            }

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var builder = ArrayBuilder<DiagnosticDescription>.GetInstance();

            if (binaryIsAbstract)
            {
                builder.Add(
                    // (12,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                    //     abstract static I1 operator& (I1 x, I1 y);
                    Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, op).WithArguments("abstract", "9.0", "preview").WithLocation(12, 32)
                    );
            }

            if (trueIsAbstract)
            {
                builder.Add(
                    // (13,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                    //     abstract static bool operator true (I1 x);
                    Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "true").WithArguments("abstract", "9.0", "preview").WithLocation(13, 35)
                    );
            }

            if (falseIsAbstract)
            {
                builder.Add(
                    // (14,35): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                    //     abstract static bool operator false (I1 x);
                    Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "false").WithArguments("abstract", "9.0", "preview").WithLocation(14, 35)
                    );
            }

            compilation3.VerifyDiagnostics(builder.ToArrayAndFree());
        }

        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("%")]
        [InlineData("&")]
        [InlineData("|")]
        [InlineData("^")]
        [InlineData("<<")]
        [InlineData(">>")]
        public void ConsumeAbstractCompoundBinaryOperator_06(string op)
        {
            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator" + op + @" (T x, int y);
}
";
            var source2 =
@"
class Test
{
    static void M02<T>(T x, int y) where T : I1<T>
    {
        x " + op + @"= y;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         x <<= y;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "x " + op + "= y").WithArguments("static abstract members in interfaces").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (12,31): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static T operator<< (T x, int y);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, op).WithArguments("abstract", "9.0", "preview").WithLocation(12, 31)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyGet_01()
        {
            var source1 =
@"
interface I1
{
    abstract static int P01 { get; set;}

    static void M02()
    {
        _ = P01;
        _ = P04;
    }

    void M03()
    {
        _ = this.P01;
        _ = this.P04;
    }

    static int P04 { get; set; }

    protected abstract static int P05 { get; set; }
}

class Test
{
    static void MT1(I1 x)
    {
        _ = I1.P01;
        _ = x.P01;
        _ = I1.P04;
        _ = x.P04;
    }

    static void MT2<T>() where T : I1
    {
        _ = T.P03;
        _ = T.P04;
        _ = T.P00;
        _ = T.P05;

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01.ToString());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = P01;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "P01").WithLocation(8, 13),
                // (14,13): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = this.P01;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P01").WithArguments("I1.P01").WithLocation(14, 13),
                // (15,13): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = this.P04;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P04").WithArguments("I1.P04").WithLocation(15, 13),
                // (27,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = I1.P01;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.P01").WithLocation(27, 13),
                // (28,13): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = x.P01;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P01").WithArguments("I1.P01").WithLocation(28, 13),
                // (30,13): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = x.P04;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P04").WithArguments("I1.P04").WithLocation(30, 13),
                // (35,13): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = T.P03;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 13),
                // (36,13): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = T.P04;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 13),
                // (37,13): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = T.P00;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 13),
                // (38,15): error CS0122: 'I1.P05' is inaccessible due to its protection level
                //         _ = T.P05;
                Diagnostic(ErrorCode.ERR_BadAccess, "P05").WithArguments("I1.P05").WithLocation(38, 15),
                // (40,71): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01.ToString());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "T.P01").WithLocation(40, 71)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertySet_01()
        {
            var source1 =
@"
interface I1
{
    abstract static int P01 { get; set;}

    static void M02()
    {
        P01 = 1;
        P04 = 1;
    }

    void M03()
    {
        this.P01 = 1;
        this.P04 = 1;
    }

    static int P04 { get; set; }

    protected abstract static int P05 { get; set; }
}

class Test
{
    static void MT1(I1 x)
    {
        I1.P01 = 1;
        x.P01 = 1;
        I1.P04 = 1;
        x.P04 = 1;
    }

    static void MT2<T>() where T : I1
    {
        T.P03 = 1;
        T.P04 = 1;
        T.P00 = 1;
        T.P05 = 1;

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 = 1);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         P01 = 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "P01").WithLocation(8, 9),
                // (14,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P01 = 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P01").WithArguments("I1.P01").WithLocation(14, 9),
                // (15,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P04 = 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P04").WithArguments("I1.P04").WithLocation(15, 9),
                // (27,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         I1.P01 = 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.P01").WithLocation(27, 9),
                // (28,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P01 = 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P01").WithArguments("I1.P01").WithLocation(28, 9),
                // (30,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P04 = 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P04").WithArguments("I1.P04").WithLocation(30, 9),
                // (35,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P03 = 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 9),
                // (36,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P04 = 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 9),
                // (37,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P00 = 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 9),
                // (38,11): error CS0122: 'I1.P05' is inaccessible due to its protection level
                //         T.P05 = 1;
                Diagnostic(ErrorCode.ERR_BadAccess, "P05").WithArguments("I1.P05").WithLocation(38, 11),
                // (40,71): error CS0832: An expression tree may not contain an assignment operator
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 = 1);
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAssignment, "T.P01 = 1").WithLocation(40, 71),
                // (40,71): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 = 1);
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "T.P01").WithLocation(40, 71)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyCompound_01()
        {
            var source1 =
@"
interface I1
{
    abstract static int P01 { get; set;}

    static void M02()
    {
        P01 += 1;
        P04 += 1;
    }

    void M03()
    {
        this.P01 += 1;
        this.P04 += 1;
    }

    static int P04 { get; set; }

    protected abstract static int P05 { get; set; }
}

class Test
{
    static void MT1(I1 x)
    {
        I1.P01 += 1;
        x.P01 += 1;
        I1.P04 += 1;
        x.P04 += 1;
    }

    static void MT2<T>() where T : I1
    {
        T.P03 += 1;
        T.P04 += 1;
        T.P00 += 1;
        T.P05 += 1;

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 += 1);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         P01 += 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "P01").WithLocation(8, 9),
                // (8,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         P01 += 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "P01").WithLocation(8, 9),
                // (14,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P01 += 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P01").WithArguments("I1.P01").WithLocation(14, 9),
                // (15,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P04 += 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P04").WithArguments("I1.P04").WithLocation(15, 9),
                // (27,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         I1.P01 += 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.P01").WithLocation(27, 9),
                // (27,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         I1.P01 += 1;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.P01").WithLocation(27, 9),
                // (28,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P01 += 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P01").WithArguments("I1.P01").WithLocation(28, 9),
                // (30,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P04 += 1;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P04").WithArguments("I1.P04").WithLocation(30, 9),
                // (35,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P03 += 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 9),
                // (36,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P04 += 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 9),
                // (37,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P00 += 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 9),
                // (38,11): error CS0122: 'I1.P05' is inaccessible due to its protection level
                //         T.P05 += 1;
                Diagnostic(ErrorCode.ERR_BadAccess, "P05").WithArguments("I1.P05").WithLocation(38, 11),
                // (40,71): error CS0832: An expression tree may not contain an assignment operator
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 += 1);
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAssignment, "T.P01 += 1").WithLocation(40, 71),
                // (40,71): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 += 1);
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "T.P01").WithLocation(40, 71)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticProperty_02()
        {
            var source1 =
@"
interface I1
{
    abstract static int P01 { get; set; }

    static void M02()
    {
        _ = nameof(P01);
        _ = nameof(P04);
    }

    void M03()
    {
        _ = nameof(this.P01);
        _ = nameof(this.P04);
    }

    static int P04 { get; set; }

    protected abstract static int P05 { get; set; }
}

class Test
{
    static void MT1(I1 x)
    {
        _ = nameof(I1.P01);
        _ = nameof(x.P01);
        _ = nameof(I1.P04);
        _ = nameof(x.P04);
    }

    static void MT2<T>() where T : I1
    {
        _ = nameof(T.P03);
        _ = nameof(T.P04);
        _ = nameof(T.P00);
        _ = nameof(T.P05);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (14,20): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(this.P01);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P01").WithArguments("I1.P01").WithLocation(14, 20),
                // (15,20): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(this.P04);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P04").WithArguments("I1.P04").WithLocation(15, 20),
                // (28,20): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(x.P01);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P01").WithArguments("I1.P01").WithLocation(28, 20),
                // (30,20): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(x.P04);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P04").WithArguments("I1.P04").WithLocation(30, 20),
                // (35,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.P03);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 20),
                // (36,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.P04);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 20),
                // (37,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.P00);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 20),
                // (38,22): error CS0122: 'I1.P05' is inaccessible due to its protection level
                //         _ = nameof(T.P05);
                Diagnostic(ErrorCode.ERR_BadAccess, "P05").WithArguments("I1.P05").WithLocation(38, 22)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyGet_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}

class Test
{
    static void M02<T, U>() where T : U where U : I1
    {
        _ = T.P01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       14 (0xe)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""int I1.P01.get""
  IL_000c:  pop
  IL_000d:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       13 (0xd)
  .maxstack  1
  IL_0000:  constrained. ""T""
  IL_0006:  call       ""int I1.P01.get""
  IL_000b:  pop
  IL_000c:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>().First().Right;

            Assert.Equal("T.P01", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IPropertyReferenceOperation: System.Int32 I1.P01 { get; set; } (Static) (OperationKind.PropertyReference, Type: System.Int32) (Syntax: 'T.P01')
  Instance Receiver: 
    null
");

            var m02 = compilation1.GetMember<MethodSymbol>("Test.M02");

            Assert.Equal("System.Int32 I1.P01 { get; set; }", ((CSharpSemanticModel)model).LookupSymbols(node.SpanStart, m02.TypeParameters[0], "P01").Single().ToTestDisplayString());
        }

        [Fact]
        public void ConsumeAbstractStaticPropertySet_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}

class Test
{
    static void M02<T, U>() where T : U where U : I1
    {
        T.P01 = 1;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       15 (0xf)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  ldc.i4.1
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""void I1.P01.set""
  IL_000d:  nop
  IL_000e:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       13 (0xd)
  .maxstack  1
  IL_0000:  ldc.i4.1
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""void I1.P01.set""
  IL_000c:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>().First().Left;

            Assert.Equal("T.P01", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IPropertyReferenceOperation: System.Int32 I1.P01 { get; set; } (Static) (OperationKind.PropertyReference, Type: System.Int32) (Syntax: 'T.P01')
  Instance Receiver: 
    null
");

            var m02 = compilation1.GetMember<MethodSymbol>("Test.M02");

            Assert.Equal("System.Int32 I1.P01 { get; set; }", ((CSharpSemanticModel)model).LookupSymbols(node.SpanStart, m02.TypeParameters[0], "P01").Single().ToTestDisplayString());
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyCompound_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}

class Test
{
    static void M02<T, U>() where T : U where U : I1
    {
        T.P01 += 1;
    }

    static string M03<T, U>() where T : U where U : I1
    {
        return nameof(T.P01);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       27 (0x1b)
  .maxstack  2
  IL_0000:  nop
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""int I1.P01.get""
  IL_000c:  ldc.i4.1
  IL_000d:  add
  IL_000e:  constrained. ""T""
  IL_0014:  call       ""void I1.P01.set""
  IL_0019:  nop
  IL_001a:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>()",
@"
{
  // Code size       11 (0xb)
  .maxstack  1
  .locals init (string V_0)
  IL_0000:  nop
  IL_0001:  ldstr      ""P01""
  IL_0006:  stloc.0
  IL_0007:  br.s       IL_0009
  IL_0009:  ldloc.0
  IL_000a:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       25 (0x19)
  .maxstack  2
  IL_0000:  constrained. ""T""
  IL_0006:  call       ""int I1.P01.get""
  IL_000b:  ldc.i4.1
  IL_000c:  add
  IL_000d:  constrained. ""T""
  IL_0013:  call       ""void I1.P01.set""
  IL_0018:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>()",
@"
{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  ldstr      ""P01""
  IL_0005:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>().First().Left;

            Assert.Equal("T.P01", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IPropertyReferenceOperation: System.Int32 I1.P01 { get; set; } (Static) (OperationKind.PropertyReference, Type: System.Int32) (Syntax: 'T.P01')
  Instance Receiver: 
    null
");

            var m02 = compilation1.GetMember<MethodSymbol>("Test.M02");

            Assert.Equal("System.Int32 I1.P01 { get; set; }", ((CSharpSemanticModel)model).LookupSymbols(node.SpanStart, m02.TypeParameters[0], "P01").Single().ToTestDisplayString());
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyGet_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        _ = T.P01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         _ = T.P01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.P01").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "get").WithLocation(12, 31),
                // (12,36): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "set").WithLocation(12, 36)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertySet_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 = 1;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         T.P01 = 1;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.P01").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "get").WithLocation(12, 31),
                // (12,36): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "set").WithLocation(12, 36)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyCompound_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 += 1;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         T.P01 += 1;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.P01").WithLocation(6, 9),
                // (6,9): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         T.P01 += 1;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.P01").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "get").WithLocation(12, 31),
                // (12,36): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "set").WithLocation(12, 36)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyGet_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        _ = T.P01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = T.P01;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,13): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = T.P01;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 13),
                // (12,25): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "P01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 25)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertySet_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 = 1;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 = 1;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 = 1;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9),
                // (12,25): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "P01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 25)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticPropertyCompound_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static int P01 { get; set; }
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 += 1;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 += 1;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 += 1;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9),
                // (12,25): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static int P01 { get; set; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "P01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 25)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticEventAdd_01()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
interface I1
{
    abstract static event System.Action P01;

    static void M02()
    {
        P01 += null;
        P04 += null;
    }

    void M03()
    {
        this.P01 += null;
        this.P04 += null;
    }

    static event System.Action P04;

    protected abstract static event System.Action P05;
}

class Test
{
    static void MT1(I1 x)
    {
        I1.P01 += null;
        x.P01 += null;
        I1.P04 += null;
        x.P04 += null;
    }

    static void MT2<T>() where T : I1
    {
        T.P03 += null;
        T.P04 += null;
        T.P00 += null;
        T.P05 += null;

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 += null);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         P01 += null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "P01 += null").WithLocation(8, 9),
                // (14,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P01 += null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P01").WithArguments("I1.P01").WithLocation(14, 9),
                // (14,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         this.P01 += null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "this.P01 += null").WithLocation(14, 9),
                // (15,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P04 += null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P04").WithArguments("I1.P04").WithLocation(15, 9),
                // (27,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         I1.P01 += null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.P01 += null").WithLocation(27, 9),
                // (28,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P01 += null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P01").WithArguments("I1.P01").WithLocation(28, 9),
                // (28,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         x.P01 += null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "x.P01 += null").WithLocation(28, 9),
                // (30,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P04 += null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P04").WithArguments("I1.P04").WithLocation(30, 9),
                // (35,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P03 += null;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 9),
                // (36,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P04 += null;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 9),
                // (37,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P00 += null;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 9),
                // (38,11): error CS0122: 'I1.P05' is inaccessible due to its protection level
                //         T.P05 += null;
                Diagnostic(ErrorCode.ERR_BadAccess, "P05").WithArguments("I1.P05").WithLocation(38, 11),
                // (40,71): error CS0832: An expression tree may not contain an assignment operator
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 += null);
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAssignment, "T.P01 += null").WithLocation(40, 71)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticEventRemove_01()
        {
            var source1 =
@"#pragma warning disable CS0067 // The event is never used
interface I1
{
    abstract static event System.Action P01;

    static void M02()
    {
        P01 -= null;
        P04 -= null;
    }

    void M03()
    {
        this.P01 -= null;
        this.P04 -= null;
    }

    static event System.Action P04;

    protected abstract static event System.Action P05;
}

class Test
{
    static void MT1(I1 x)
    {
        I1.P01 -= null;
        x.P01 -= null;
        I1.P04 -= null;
        x.P04 -= null;
    }

    static void MT2<T>() where T : I1
    {
        T.P03 -= null;
        T.P04 -= null;
        T.P00 -= null;
        T.P05 -= null;

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 -= null);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         P01 -= null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "P01 -= null").WithLocation(8, 9),
                // (14,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P01 -= null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P01").WithArguments("I1.P01").WithLocation(14, 9),
                // (14,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         this.P01 -= null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "this.P01 -= null").WithLocation(14, 9),
                // (15,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         this.P04 -= null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P04").WithArguments("I1.P04").WithLocation(15, 9),
                // (27,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         I1.P01 -= null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.P01 -= null").WithLocation(27, 9),
                // (28,9): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P01 -= null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P01").WithArguments("I1.P01").WithLocation(28, 9),
                // (28,9): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         x.P01 -= null;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "x.P01 -= null").WithLocation(28, 9),
                // (30,9): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         x.P04 -= null;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P04").WithArguments("I1.P04").WithLocation(30, 9),
                // (35,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P03 -= null;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 9),
                // (36,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P04 -= null;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 9),
                // (37,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.P00 -= null;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 9),
                // (38,11): error CS0122: 'I1.P05' is inaccessible due to its protection level
                //         T.P05 -= null;
                Diagnostic(ErrorCode.ERR_BadAccess, "P05").WithArguments("I1.P05").WithLocation(38, 11),
                // (40,71): error CS0832: An expression tree may not contain an assignment operator
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => T.P01 -= null);
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAssignment, "T.P01 -= null").WithLocation(40, 71)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticEvent_02()
        {
            var source1 =
@"
interface I1
{
    abstract static event System.Action P01;

    static void M02()
    {
        _ = nameof(P01);
        _ = nameof(P04);
    }

    void M03()
    {
        _ = nameof(this.P01);
        _ = nameof(this.P04);
    }

    static event System.Action P04;

    protected abstract static event System.Action P05;
}

class Test
{
    static void MT1(I1 x)
    {
        _ = nameof(I1.P01);
        _ = nameof(x.P01);
        _ = nameof(I1.P04);
        _ = nameof(x.P04);
    }

    static void MT2<T>() where T : I1
    {
        _ = nameof(T.P03);
        _ = nameof(T.P04);
        _ = nameof(T.P00);
        _ = nameof(T.P05);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (14,20): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(this.P01);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P01").WithArguments("I1.P01").WithLocation(14, 20),
                // (15,20): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(this.P04);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.P04").WithArguments("I1.P04").WithLocation(15, 20),
                // (28,20): error CS0176: Member 'I1.P01' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(x.P01);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P01").WithArguments("I1.P01").WithLocation(28, 20),
                // (30,20): error CS0176: Member 'I1.P04' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = nameof(x.P04);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.P04").WithArguments("I1.P04").WithLocation(30, 20),
                // (35,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.P03);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 20),
                // (36,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.P04);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 20),
                // (37,20): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = nameof(T.P00);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 20),
                // (38,22): error CS0122: 'I1.P05' is inaccessible due to its protection level
                //         _ = nameof(T.P05);
                Diagnostic(ErrorCode.ERR_BadAccess, "P05").WithArguments("I1.P05").WithLocation(38, 22)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticEvent_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static event System.Action E01;
}

class Test
{
    static void M02<T, U>() where T : U where U : I1
    {
        T.E01 += null;
        T.E01 -= null;
    }

    static string M03<T, U>() where T : U where U : I1
    {
        return nameof(T.E01);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       28 (0x1c)
  .maxstack  1
  IL_0000:  nop
  IL_0001:  ldnull
  IL_0002:  constrained. ""T""
  IL_0008:  call       ""void I1.E01.add""
  IL_000d:  nop
  IL_000e:  ldnull
  IL_000f:  constrained. ""T""
  IL_0015:  call       ""void I1.E01.remove""
  IL_001a:  nop
  IL_001b:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>()",
@"
{
  // Code size       11 (0xb)
  .maxstack  1
  .locals init (string V_0)
  IL_0000:  nop
  IL_0001:  ldstr      ""E01""
  IL_0006:  stloc.0
  IL_0007:  br.s       IL_0009
  IL_0009:  ldloc.0
  IL_000a:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       25 (0x19)
  .maxstack  1
  IL_0000:  ldnull
  IL_0001:  constrained. ""T""
  IL_0007:  call       ""void I1.E01.add""
  IL_000c:  ldnull
  IL_000d:  constrained. ""T""
  IL_0013:  call       ""void I1.E01.remove""
  IL_0018:  ret
}
");

            verifier.VerifyIL("Test.M03<T, U>()",
@"
{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  ldstr      ""E01""
  IL_0005:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>().First().Left;

            Assert.Equal("T.E01", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IEventReferenceOperation: event System.Action I1.E01 (Static) (OperationKind.EventReference, Type: System.Action) (Syntax: 'T.E01')
  Instance Receiver: 
    null
");

            var m02 = compilation1.GetMember<MethodSymbol>("Test.M02");

            Assert.Equal("event System.Action I1.E01", ((CSharpSemanticModel)model).LookupSymbols(node.SpanStart, m02.TypeParameters[0], "E01").Single().ToTestDisplayString());
        }

        [Fact]
        public void ConsumeAbstractStaticEventAdd_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static event System.Action P01;
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 += null;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         T.P01 += null;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.P01 += null").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,41): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static event System.Action P01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "P01").WithLocation(12, 41)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticEventRemove_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static event System.Action P01;
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 -= null;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         T.P01 -= null;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.P01 -= null").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,41): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static event System.Action P01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "P01").WithLocation(12, 41)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticEventAdd_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static event System.Action P01;
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 += null;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 += null;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 += null;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9),
                // (12,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static event System.Action P01;
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "P01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 41)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticEventRemove_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static event System.Action P01;
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.P01 -= null;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 -= null;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,9): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         T.P01 -= null;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 9),
                // (12,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static event System.Action P01;
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "P01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 41)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticIndexedProperty_03()
        {
            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .custom instance void [mscorlib]System.Reflection.DefaultMemberAttribute::.ctor(string) = (
        01 00 04 49 74 65 6d 00 00
    )

    // Methods
    .method public hidebysig specialname newslot abstract virtual 
        static int32 get_Item (
            int32 x
        ) cil managed 
    {
    } // end of method I1::get_Item

    .method public hidebysig specialname newslot abstract virtual 
        static void set_Item (
            int32 x,
            int32 'value'
        ) cil managed 
    {
    } // end of method I1::set_Item

    // Properties
    .property int32 Item(
        int32 x
    )
    {
        .get int32 I1::get_Item(int32)
        .set void I1::set_Item(int32, int32)
    }

} // end of class I1
";

            var source1 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.Item[0] += 1;
    }

    static string M03<T>() where T : I1
    {
        return nameof(T.Item);
    }
}
";
            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (6,9): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         T.Item[0] += 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(6, 9),
                // (11,23): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         return nameof(T.Item);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(11, 23)
                );

            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T[0] += 1;
    }

    static void M03<T>() where T : I1
    {
        T.set_Item(0, T.get_Item(0) + 1);
    }
}
";
            var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation2.VerifyDiagnostics(
                // (6,9): error CS0119: 'T' is a type, which is not valid in the given context
                //         T[0] += 1;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type").WithLocation(6, 9),
                // (11,11): error CS0571: 'I1.this[int].set': cannot explicitly call operator or accessor
                //         T.set_Item(0, T.get_Item(0) + 1);
                Diagnostic(ErrorCode.ERR_CantCallSpecialMethod, "set_Item").WithArguments("I1.this[int].set").WithLocation(11, 11),
                // (11,25): error CS0571: 'I1.this[int].get': cannot explicitly call operator or accessor
                //         T.set_Item(0, T.get_Item(0) + 1);
                Diagnostic(ErrorCode.ERR_CantCallSpecialMethod, "get_Item").WithArguments("I1.this[int].get").WithLocation(11, 25)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticIndexedProperty_04()
        {
            var ilSource = @"
.class interface public auto ansi abstract I1
{
    // Methods
    .method public hidebysig specialname newslot abstract virtual 
        static int32 get_Item (
            int32 x
        ) cil managed 
    {
    } // end of method I1::get_Item

    .method public hidebysig specialname newslot abstract virtual 
        static void set_Item (
            int32 x,
            int32 'value'
        ) cil managed 
    {
    } // end of method I1::set_Item

    // Properties
    .property int32 Item(
        int32 x
    )
    {
        .get int32 I1::get_Item(int32)
        .set void I1::set_Item(int32, int32)
    }

} // end of class I1
";

            var source1 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.Item[0] += 1;
    }

    static string M03<T>() where T : I1
    {
        return nameof(T.Item);
    }
}
";
            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (6,11): error CS1545: Property, indexer, or event 'I1.Item[int]' is not supported by the language; try directly calling accessor methods 'I1.get_Item(int)' or 'I1.set_Item(int, int)'
                //         T.Item[0] += 1;
                Diagnostic(ErrorCode.ERR_BindToBogusProp2, "Item").WithArguments("I1.Item[int]", "I1.get_Item(int)", "I1.set_Item(int, int)").WithLocation(6, 11),
                // (11,25): error CS1545: Property, indexer, or event 'I1.Item[int]' is not supported by the language; try directly calling accessor methods 'I1.get_Item(int)' or 'I1.set_Item(int, int)'
                //         return nameof(T.Item);
                Diagnostic(ErrorCode.ERR_BindToBogusProp2, "Item").WithArguments("I1.Item[int]", "I1.get_Item(int)", "I1.set_Item(int, int)").WithLocation(11, 25)
                );

            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        T.set_Item(0, T.get_Item(0) + 1);
    }
}
";
            var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation2, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T>()",
@"
{
  // Code size       29 (0x1d)
  .maxstack  3
  IL_0000:  nop
  IL_0001:  ldc.i4.0
  IL_0002:  ldc.i4.0
  IL_0003:  constrained. ""T""
  IL_0009:  call       ""int I1.get_Item(int)""
  IL_000e:  ldc.i4.1
  IL_000f:  add
  IL_0010:  constrained. ""T""
  IL_0016:  call       ""void I1.set_Item(int, int)""
  IL_001b:  nop
  IL_001c:  ret
}
");
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionToDelegate_01()
        {
            var source1 =
@"
interface I1
{
    abstract static void M01();

    static void M02()
    {
        _ = (System.Action)M01;
        _ = (System.Action)M04;
    }

    void M03()
    {
        _ = (System.Action)this.M01;
        _ = (System.Action)this.M04;
    }

    static void M04() {}

    protected abstract static void M05();
}

class Test
{
    static void MT1(I1 x)
    {
        _ = (System.Action)I1.M01;
        _ = (System.Action)x.M01;
        _ = (System.Action)I1.M04;
        _ = (System.Action)x.M04;
    }

    static void MT2<T>() where T : I1
    {
        _ = (System.Action)T.M03;
        _ = (System.Action)T.M04;
        _ = (System.Action)T.M00;
        _ = (System.Action)T.M05;

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => ((System.Action)T.M01).ToString());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = (System.Action)M01;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "(System.Action)M01").WithLocation(8, 13),
                // (14,28): error CS0176: Member 'I1.M01()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = (System.Action)this.M01;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.M01").WithArguments("I1.M01()").WithLocation(14, 28),
                // (15,28): error CS0176: Member 'I1.M04()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = (System.Action)this.M04;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.M04").WithArguments("I1.M04()").WithLocation(15, 28),
                // (27,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = (System.Action)I1.M01;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "(System.Action)I1.M01").WithLocation(27, 13),
                // (28,28): error CS0176: Member 'I1.M01()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = (System.Action)x.M01;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.M01").WithArguments("I1.M01()").WithLocation(28, 28),
                // (30,28): error CS0176: Member 'I1.M04()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = (System.Action)x.M04;
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.M04").WithArguments("I1.M04()").WithLocation(30, 28),
                // (35,28): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = (System.Action)T.M03;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 28),
                // (36,28): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = (System.Action)T.M04;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 28),
                // (37,28): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = (System.Action)T.M00;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 28),
                // (38,30): error CS0122: 'I1.M05()' is inaccessible due to its protection level
                //         _ = (System.Action)T.M05;
                Diagnostic(ErrorCode.ERR_BadAccess, "M05").WithArguments("I1.M05()").WithLocation(38, 30),
                // (40,87): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => ((System.Action)T.M01).ToString());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "T.M01").WithLocation(40, 87)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionToDelegate_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

class Test
{
    static System.Action M02<T, U>() where T : U where U : I1
    {
        return T.M01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       24 (0x18)
  .maxstack  2
  .locals init (System.Action V_0)
  IL_0000:  nop
  IL_0001:  ldnull
  IL_0002:  constrained. ""T""
  IL_0008:  ldftn      ""void I1.M01()""
  IL_000e:  newobj     ""System.Action..ctor(object, System.IntPtr)""
  IL_0013:  stloc.0
  IL_0014:  br.s       IL_0016
  IL_0016:  ldloc.0
  IL_0017:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       19 (0x13)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  constrained. ""T""
  IL_0007:  ldftn      ""void I1.M01()""
  IL_000d:  newobj     ""System.Action..ctor(object, System.IntPtr)""
  IL_0012:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().First();

            Assert.Equal("T.M01", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IMethodReferenceOperation: void I1.M01() (IsVirtual) (Static) (OperationKind.MethodReference, Type: null) (Syntax: 'T.M01')
  Instance Receiver: 
    null
");
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionToDelegate_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        _ = (System.Action)T.M01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         _ = (System.Action)T.M01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "(System.Action)T.M01").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,26): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(12, 26)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionToDelegate_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        _ = (System.Action)T.M01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,28): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = (System.Action)T.M01;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 28)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,28): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = (System.Action)T.M01;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 28),
                // (12,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 26)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_DelegateCreation_01()
        {
            var source1 =
@"
interface I1
{
    abstract static void M01();

    static void M02()
    {
        _ = new System.Action(M01);
        _ = new System.Action(M04);
    }

    void M03()
    {
        _ = new System.Action(this.M01);
        _ = new System.Action(this.M04);
    }

    static void M04() {}

    protected abstract static void M05();
}

class Test
{
    static void MT1(I1 x)
    {
        _ = new System.Action(I1.M01);
        _ = new System.Action(x.M01);
        _ = new System.Action(I1.M04);
        _ = new System.Action(x.M04);
    }

    static void MT2<T>() where T : I1
    {
        _ = new System.Action(T.M03);
        _ = new System.Action(T.M04);
        _ = new System.Action(T.M00);
        _ = new System.Action(T.M05);

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => new System.Action(T.M01).ToString());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,31): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = new System.Action(M01);
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "M01").WithLocation(8, 31),
                // (14,31): error CS0176: Member 'I1.M01()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = new System.Action(this.M01);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.M01").WithArguments("I1.M01()").WithLocation(14, 31),
                // (15,31): error CS0176: Member 'I1.M04()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = new System.Action(this.M04);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "this.M04").WithArguments("I1.M04()").WithLocation(15, 31),
                // (27,31): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = new System.Action(I1.M01);
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "I1.M01").WithLocation(27, 31),
                // (28,31): error CS0176: Member 'I1.M01()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = new System.Action(x.M01);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.M01").WithArguments("I1.M01()").WithLocation(28, 31),
                // (30,31): error CS0176: Member 'I1.M04()' cannot be accessed with an instance reference; qualify it with a type name instead
                //         _ = new System.Action(x.M04);
                Diagnostic(ErrorCode.ERR_ObjectProhibited, "x.M04").WithArguments("I1.M04()").WithLocation(30, 31),
                // (35,31): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = new System.Action(T.M03);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 31),
                // (36,31): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = new System.Action(T.M04);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 31),
                // (37,31): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = new System.Action(T.M00);
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 31),
                // (38,33): error CS0122: 'I1.M05()' is inaccessible due to its protection level
                //         _ = new System.Action(T.M05);
                Diagnostic(ErrorCode.ERR_BadAccess, "M05").WithArguments("I1.M05()").WithLocation(38, 33),
                // (40,89): error CS9108: An expression tree may not contain an access of static abstract interface member
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => new System.Action(T.M01).ToString());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsAbstractStaticMemberAccess, "T.M01").WithLocation(40, 89)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_DelegateCreation_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

class Test
{
    static System.Action M02<T, U>() where T : U where U : I1
    {
        return new System.Action(T.M01);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       24 (0x18)
  .maxstack  2
  .locals init (System.Action V_0)
  IL_0000:  nop
  IL_0001:  ldnull
  IL_0002:  constrained. ""T""
  IL_0008:  ldftn      ""void I1.M01()""
  IL_000e:  newobj     ""System.Action..ctor(object, System.IntPtr)""
  IL_0013:  stloc.0
  IL_0014:  br.s       IL_0016
  IL_0016:  ldloc.0
  IL_0017:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll,
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       19 (0x13)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  constrained. ""T""
  IL_0007:  ldftn      ""void I1.M01()""
  IL_000d:  newobj     ""System.Action..ctor(object, System.IntPtr)""
  IL_0012:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().First();

            Assert.Equal("T.M01", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IMethodReferenceOperation: void I1.M01() (IsVirtual) (Static) (OperationKind.MethodReference, Type: null) (Syntax: 'T.M01')
  Instance Receiver: 
    null
");
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_DelegateCreation_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        _ = new System.Action(T.M01);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         _ = new System.Action(T.M01);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "T.M01").WithLocation(6, 31)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,26): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(12, 26)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_DelegateCreation_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
class Test
{
    static void M02<T>() where T : I1
    {
        _ = new System.Action(T.M01);
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,31): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = new System.Action(T.M01);
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 31)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,31): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = new System.Action(T.M01);
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 31),
                // (12,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 26)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionToFunctionPointer_01()
        {
            var source1 =
@"
unsafe interface I1
{
    abstract static void M01();

    static void M02()
    {
        _ = (delegate*<void>)&M01;
        _ = (delegate*<void>)&M04;
    }

    void M03()
    {
        _ = (delegate*<void>)&this.M01;
        _ = (delegate*<void>)&this.M04;
    }

    static void M04() {}

    protected abstract static void M05();
}

unsafe class Test
{
    static void MT1(I1 x)
    {
        _ = (delegate*<void>)&I1.M01;
        _ = (delegate*<void>)&x.M01;
        _ = (delegate*<void>)&I1.M04;
        _ = (delegate*<void>)&x.M04;
    }

    static void MT2<T>() where T : I1
    {
        _ = (delegate*<void>)&T.M03;
        _ = (delegate*<void>)&T.M04;
        _ = (delegate*<void>)&T.M00;
        _ = (delegate*<void>)&T.M05;

        _ = (System.Linq.Expressions.Expression<System.Action>)(() => ((System.IntPtr)((delegate*<void>)&T.M01)).ToString());
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = (delegate*<void>)&M01;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "(delegate*<void>)&M01").WithLocation(8, 13),
                // (14,13): error CS8757: No overload for 'M01' matches function pointer 'delegate*<void>'
                //         _ = (delegate*<void>)&this.M01;
                Diagnostic(ErrorCode.ERR_MethFuncPtrMismatch, "(delegate*<void>)&this.M01").WithArguments("M01", "delegate*<void>").WithLocation(14, 13),
                // (15,13): error CS8757: No overload for 'M04' matches function pointer 'delegate*<void>'
                //         _ = (delegate*<void>)&this.M04;
                Diagnostic(ErrorCode.ERR_MethFuncPtrMismatch, "(delegate*<void>)&this.M04").WithArguments("M04", "delegate*<void>").WithLocation(15, 13),
                // (27,13): error CS9107: A static abstract interface member can be accessed only on a type parameter.
                //         _ = (delegate*<void>)&I1.M01;
                Diagnostic(ErrorCode.ERR_BadAbstractStaticMemberAccess, "(delegate*<void>)&I1.M01").WithLocation(27, 13),
                // (28,13): error CS8757: No overload for 'M01' matches function pointer 'delegate*<void>'
                //         _ = (delegate*<void>)&x.M01;
                Diagnostic(ErrorCode.ERR_MethFuncPtrMismatch, "(delegate*<void>)&x.M01").WithArguments("M01", "delegate*<void>").WithLocation(28, 13),
                // (30,13): error CS8757: No overload for 'M04' matches function pointer 'delegate*<void>'
                //         _ = (delegate*<void>)&x.M04;
                Diagnostic(ErrorCode.ERR_MethFuncPtrMismatch, "(delegate*<void>)&x.M04").WithArguments("M04", "delegate*<void>").WithLocation(30, 13),
                // (35,31): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = (delegate*<void>)&T.M03;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(35, 31),
                // (36,31): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = (delegate*<void>)&T.M04;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(36, 31),
                // (37,31): error CS0119: 'T' is a type parameter, which is not valid in the given context
                //         _ = (delegate*<void>)&T.M00;
                Diagnostic(ErrorCode.ERR_BadSKunknown, "T").WithArguments("T", "type parameter").WithLocation(37, 31),
                // (38,33): error CS0122: 'I1.M05()' is inaccessible due to its protection level
                //         _ = (delegate*<void>)&T.M05;
                Diagnostic(ErrorCode.ERR_BadAccess, "M05").WithArguments("I1.M05()").WithLocation(38, 33),
                // (40,88): error CS1944: An expression tree may not contain an unsafe pointer operation
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => ((System.IntPtr)((delegate*<void>)&T.M01)).ToString());
                Diagnostic(ErrorCode.ERR_ExpressionTreeContainsPointerOp, "(delegate*<void>)&T.M01").WithLocation(40, 88),
                // (40,106): error CS8810: '&' on method groups cannot be used in expression trees
                //         _ = (System.Linq.Expressions.Expression<System.Action>)(() => ((System.IntPtr)((delegate*<void>)&T.M01)).ToString());
                Diagnostic(ErrorCode.ERR_AddressOfMethodGroupInExpressionTree, "T.M01").WithLocation(40, 106)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionToFunctionPointer_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

unsafe class Test
{
    static delegate*<void> M02<T, U>() where T : U where U : I1
    {
        return &T.M01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       18 (0x12)
  .maxstack  1
  .locals init (delegate*<void> V_0)
  IL_0000:  nop
  IL_0001:  constrained. ""T""
  IL_0007:  ldftn      ""void I1.M01()""
  IL_000d:  stloc.0
  IL_000e:  br.s       IL_0010
  IL_0010:  ldloc.0
  IL_0011:  ret
}
");

            compilation1 = CreateCompilation(source1, options: TestOptions.ReleaseDll.WithAllowUnsafe(true),
                                             parseOptions: TestOptions.RegularPreview,
                                             targetFramework: TargetFramework.NetCoreApp);

            verifier = CompileAndVerify(compilation1, verify: Verification.Skipped).VerifyDiagnostics();

            verifier.VerifyIL("Test.M02<T, U>()",
@"
{
  // Code size       13 (0xd)
  .maxstack  1
  IL_0000:  constrained. ""T""
  IL_0006:  ldftn      ""void I1.M01()""
  IL_000c:  ret
}
");

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>().First();

            Assert.Equal("T.M01", node.ToString());
            VerifyOperationTreeForNode(compilation1, model, node,
// PROTOTYPE(StaticAbstractMembersInInterfaces): It feels like the "T" qualifier is important for this invocation, but it is not 
//                                               reflected in the IOperation tree. Should we change the shape of the tree in order
//                                               to expose this information? 
@"
IMethodReferenceOperation: void I1.M01() (IsVirtual) (Static) (OperationKind.MethodReference, Type: null) (Syntax: 'T.M01')
  Instance Receiver: 
    null
");
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionFunctionPointer_04()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
unsafe class Test
{
    static void M02<T>() where T : I1
    {
        _ = (delegate*<void>)&T.M01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,13): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //         _ = (delegate*<void>)&T.M01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "(delegate*<void>)&T.M01").WithLocation(6, 13)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (12,26): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(12, 26)
                );
        }

        [Fact]
        public void ConsumeAbstractStaticMethod_ConversionToFunctionPointer_06()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
@"
unsafe class Test
{
    static void M02<T>() where T : I1
    {
        _ = (delegate*<void>)&T.M01;
    }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (6,31): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = (delegate*<void>)&T.M01;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 31)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (6,31): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //         _ = (delegate*<void>)&T.M01;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "T").WithArguments("static abstract members in interfaces").WithLocation(6, 31),
                // (12,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(12, 26)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_01(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

" + typeKeyword + @"
    C1 : I1
{}

" + typeKeyword + @"
    C2 : I1
{
    public void M01() {}
}

" + typeKeyword + @"
    C3 : I1
{
    static void M01() {}
}

" + typeKeyword + @"
    C4 : I1
{
    void I1.M01() {}
}

" + typeKeyword + @"
    C5 : I1
{
    public static int M01() => throw null;
}

" + typeKeyword + @"
    C6 : I1
{
    static int I1.M01() => throw null;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1.M01()'
                //     C1 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01()").WithLocation(8, 10),
                // (12,10): error CS9109: 'C2' does not implement static interface member 'I1.M01()'. 'C2.M01()' cannot implement the interface member because it is not static.
                //     C2 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotStatic, "I1").WithArguments("C2", "I1.M01()", "C2.M01()").WithLocation(12, 10),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1.M01()'. 'C3.M01()' cannot implement an interface member because it is not public.
                //     C3 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1").WithArguments("C3", "I1.M01()", "C3.M01()").WithLocation(18, 10),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1.M01()'
                //     C4 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C4", "I1.M01()").WithLocation(24, 10),
                // (26,13): error CS0539: 'C4.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //     void I1.M01() {}
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C4.M01()").WithLocation(26, 13),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1.M01()'. 'C5.M01()' cannot implement 'I1.M01()' because it does not have the matching return type of 'void'.
                //     C5 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1").WithArguments("C5", "I1.M01()", "C5.M01()", "void").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1.M01()'
                //     C6 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C6", "I1.M01()").WithLocation(36, 10),
                // (38,19): error CS0539: 'C6.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static int I1.M01() => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C6.M01()").WithLocation(38, 19)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_02(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract void M01();
}

" + typeKeyword + @"
    C1 : I1
{}

" + typeKeyword + @"
    C2 : I1
{
    public static void M01() {}
}

" + typeKeyword + @"
    C3 : I1
{
    void M01() {}
}

" + typeKeyword + @"
    C4 : I1
{
    static void I1.M01() {}
}

" + typeKeyword + @"
    C5 : I1
{
    public int M01() => throw null;
}

" + typeKeyword + @"
    C6 : I1
{
    int I1.M01() => throw null;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1.M01()'
                //     C1 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01()").WithLocation(8, 10),
                // (12,10): error CS0736: 'C2' does not implement instance interface member 'I1.M01()'. 'C2.M01()' cannot implement the interface member because it is static.
                //     C2 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberStatic, "I1").WithArguments("C2", "I1.M01()", "C2.M01()").WithLocation(12, 10),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1.M01()'. 'C3.M01()' cannot implement an interface member because it is not public.
                //     C3 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1").WithArguments("C3", "I1.M01()", "C3.M01()").WithLocation(18, 10),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1.M01()'
                //     C4 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C4", "I1.M01()").WithLocation(24, 10),
                // (26,20): error CS0539: 'C4.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C4.M01()").WithLocation(26, 20),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1.M01()'. 'C5.M01()' cannot implement 'I1.M01()' because it does not have the matching return type of 'void'.
                //     C5 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1").WithArguments("C5", "I1.M01()", "C5.M01()", "void").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1.M01()'
                //     C6 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C6", "I1.M01()").WithLocation(36, 10),
                // (38,12): error CS0539: 'C6.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //     int I1.M01() => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C6.M01()").WithLocation(38, 12)
                );
        }

        [Fact]
        public void ImplementAbstractStaticMethod_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

interface I2 : I1
{}

interface I3 : I1
{
    public virtual void M01() {}
}

interface I4 : I1
{
    static void M01() {}
}

interface I5 : I1
{
    void I1.M01() {}
}

interface I6 : I1
{
    static void I1.M01() {}
}

interface I7 : I1
{
    abstract static void M01();
}

interface I8 : I1
{
    abstract static void I1.M01();
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (12,25): warning CS0108: 'I3.M01()' hides inherited member 'I1.M01()'. Use the new keyword if hiding was intended.
                //     public virtual void M01() {}
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I3.M01()", "I1.M01()").WithLocation(12, 25),
                // (17,17): warning CS0108: 'I4.M01()' hides inherited member 'I1.M01()'. Use the new keyword if hiding was intended.
                //     static void M01() {}
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I4.M01()", "I1.M01()").WithLocation(17, 17),
                // (22,13): error CS0539: 'I5.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //     void I1.M01() {}
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I5.M01()").WithLocation(22, 13),
                // (27,20): error CS0106: The modifier 'static' is not valid for this item
                //     static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M01").WithArguments("static").WithLocation(27, 20),
                // (27,20): error CS0539: 'I6.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I6.M01()").WithLocation(27, 20),
                // (32,26): warning CS0108: 'I7.M01()' hides inherited member 'I1.M01()'. Use the new keyword if hiding was intended.
                //     abstract static void M01();
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I7.M01()", "I1.M01()").WithLocation(32, 26),
                // (37,29): error CS0106: The modifier 'static' is not valid for this item
                //     abstract static void I1.M01();
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M01").WithArguments("static").WithLocation(37, 29),
                // (37,29): error CS0539: 'I8.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //     abstract static void I1.M01();
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I8.M01()").WithLocation(37, 29)
                );

            var m01 = compilation1.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();

            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I2").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I3").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I4").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I5").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I6").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I7").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I8").FindImplementationForInterfaceMember(m01));
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_04(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static void M01();
    abstract static void M02();
}
";
            var source2 =
typeKeyword + @"
    Test: I1
{
    static void I1.M01() {}
    public static void M02() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,20): error CS8703: The modifier 'static' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("static", "9.0", "preview").WithLocation(4, 20)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (4,20): error CS8703: The modifier 'static' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("static", "9.0", "preview").WithLocation(4, 20),
                // (10,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(10, 26),
                // (11,26): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static void M02();
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M02").WithArguments("abstract", "9.0", "preview").WithLocation(11, 26)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_05(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    public static void M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (2,12): error CS9110: 'Test1.M01()' cannot implement interface member 'I1.M01()' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01()", "I1.M01()", "Test1").WithLocation(2, 12)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (2,12): error CS9110: 'Test1.M01()' cannot implement interface member 'I1.M01()' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01()", "I1.M01()", "Test1").WithLocation(2, 12),
                // (9,26): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(9, 26)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_06(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    static void I1.M01() {}
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,20): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(4, 20)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (4,20): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(4, 20),
                // (9,26): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static void M01();
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(9, 26)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_07(bool structure)
        {
            // Basic implicit implementation scenario, MethodImpl is emitted

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

" + typeKeyword + @"
    C : I1
{
    public static void M01() {}
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.Ordinary, cM01.MethodKind);

                Assert.Equal("void C.M01()", cM01.ToTestDisplayString());

                if (module is PEModuleSymbol)
                {
                    Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_08(bool structure)
        {
            // Basic explicit implementation scenario

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

" + typeKeyword + @"
    C : I1
{
    static void I1.M01() {}
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, cM01.MethodKind);

                Assert.Equal("void C.I1.M01()", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticMethod_09()
        {
            // Explicit implementation from base is treated as an implementation

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

public class C1
{
    public static void M01() {}
}

public class C2 : C1, I1
{
    static void I1.M01() {}
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                     parseOptions: parseOptions,
                                                     targetFramework: TargetFramework.NetCoreApp,
                                                     references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));
                var m01 = c3.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                var cM01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);

                Assert.Equal("void C2.I1.M01()", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticMethod_10()
        {
            // Implicit implementation is considered only for types implementing interface in source.
            // In metadata, only explicit implementations are considered

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig static abstract virtual 
        void M01 () cil managed 
    {
    } // end of method I1::M01
} // end of class I1

.class public auto ansi beforefieldinit C1
    extends System.Object
    implements I1
{
    .method private hidebysig  
        static void I1.M01 () cil managed 
    {
        .override method void I1::M01()
        .maxstack 8

        IL_0000: ret
    } // end of method C1::I1.M01

    .method public hidebysig static 
        void M01 () cil managed 
    {
        IL_0000: ret
    } // end of method C1::M01

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void System.Object::.ctor()
        IL_0006: ret
    } // end of method C1::.ctor
} // end of class C1

.class public auto ansi beforefieldinit C2
    extends C1
    implements I1
{
    .method public hidebysig static 
        void M01 () cil managed 
    {
        IL_0000: ret
    } // end of method C2::M01

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void C1::.ctor()
        IL_0006: ret
    } // end of method C2::.ctor
} // end of class C2
";
            var source1 =
@"
public class C3 : C2
{
}

public class C4 : C1, I1
{
}

public class C5 : C2, I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

            var c1M01 = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01);

            Assert.Equal("void C1.I1.M01()", c1M01.ToTestDisplayString());
            Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());

            var c2 = compilation1.GlobalNamespace.GetTypeMember("C2");
            Assert.Same(c1M01, c2.FindImplementationForInterfaceMember(m01));

            var c3 = compilation1.GlobalNamespace.GetTypeMember("C3");
            Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));

            var c4 = compilation1.GlobalNamespace.GetTypeMember("C4");
            Assert.Same(c1M01, c4.FindImplementationForInterfaceMember(m01));

            var c5 = compilation1.GlobalNamespace.GetTypeMember("C5");

            Assert.Equal("void C2.M01()", c5.FindImplementationForInterfaceMember(m01).ToTestDisplayString());

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();
        }

        [Fact]
        public void ImplementAbstractStaticMethod_11()
        {
            // Ignore invalid metadata (non-abstract static virtual method). 

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig virtual 
        static void M01 () cil managed 
    {
        IL_0000: ret
    } // end of method I1::M01
} // end of class I1
";

            var source1 =
@"
public class C1 : I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i1 = c1.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<MethodSymbol>().Single();

            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics();

            var source2 =
@"
public class C1 : I1
{
   static void I1.M01() {}
}
";

            var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation2.VerifyEmitDiagnostics(
                // (4,19): error CS0539: 'C1.M01()' in explicit interface declaration is not found among members of the interface that can be implemented
                //    static void I1.M01() {}
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C1.M01()").WithLocation(4, 19)
                );

            c1 = compilation2.GlobalNamespace.GetTypeMember("C1");
            m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
        }

        [Fact]
        public void ImplementAbstractStaticMethod_12()
        {
            // Ignore invalid metadata (default interface implementation for a static method)

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig abstract virtual 
        static void M01 () cil managed 
    {
    } // end of method I1::M01
} // end of class I1
.class interface public auto ansi abstract I2
    implements I1
{
    // Methods
    .method private hidebysig 
        static void I1.M01 () cil managed 
    {
        .override method void I1::M01()
        IL_0000: ret
    } // end of method I2::I1.M01
} // end of class I2
";

            var source1 =
@"
public class C1 : I2
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics(
                // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01()'
                // public class C1 : I2
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2").WithArguments("C1", "I1.M01()").WithLocation(2, 19)
                );

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i2 = c1.Interfaces().Single();
            var i1 = i2.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<MethodSymbol>().Single();

            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01));

            var i2M01 = i2.GetMembers().OfType<MethodSymbol>().Single();
            Assert.Same(m01, i2M01.ExplicitInterfaceImplementations.Single());
        }

        [Fact]
        public void ImplementAbstractStaticMethod_13()
        {
            // A forwarding method is added for an implicit implementation declared in base class. 

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

class C1
{
    public static void M01() {}
}

class C2 : C1, I1
{
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();
                var c2 = module.GlobalNamespace.GetTypeMember("C2");

                var c2M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01.MethodKind);
                    Assert.Equal("void C2.I1.M01()", c2M01.ToTestDisplayString());
                    Assert.Same(m01, c2M01.ExplicitInterfaceImplementations.Single());

                    var c1M01 = module.GlobalNamespace.GetMember<MethodSymbol>("C1.M01");

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.False(c1M01.IsMetadataVirtual());
                    Assert.False(c1M01.IsMetadataFinal);
                    Assert.False(c1M01.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.Ordinary, c1M01.MethodKind);
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.Equal(MethodKind.Ordinary, c2M01.MethodKind);
                    Assert.Equal("void C1.M01()", c2M01.ToTestDisplayString());
                    Assert.Empty(c2M01.ExplicitInterfaceImplementations);
                }
            }

            verifier.VerifyIL("C2.I1.M01()",
@"
{
  // Code size        6 (0x6)
  .maxstack  0
  IL_0000:  call       ""void C1.M01()""
  IL_0005:  ret
}
");
        }

        [Fact]
        public void ImplementAbstractStaticMethod_14()
        {
            // A forwarding method is added for an implicit implementation with modopt mismatch. 

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig abstract virtual 
        static void modopt(I1) M01 () cil managed 
    {
    } // end of method I1::M01
} // end of class I1
";

            var source1 =
@"
class C1 : I1
{
    public static void M01() {}
}

class C2 : I1
{
    static void I1.M01() {}
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c1 = module.GlobalNamespace.GetTypeMember("C1");
                var m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                var c1M01 = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01);

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);
                Assert.False(c1M01.IsMetadataVirtual());
                Assert.False(c1M01.IsMetadataFinal);
                Assert.False(c1M01.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01.MethodKind);
                    Assert.Equal("void modopt(I1) C1.I1.M01()", c1M01.ToTestDisplayString());
                    Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());

                    c1M01 = module.GlobalNamespace.GetMember<MethodSymbol>("C1.M01");
                    Assert.Equal("void C1.M01()", c1M01.ToTestDisplayString());

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.False(c1M01.IsMetadataVirtual());
                    Assert.False(c1M01.IsMetadataFinal);
                    Assert.False(c1M01.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.Ordinary, c1M01.MethodKind);

                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.Equal(MethodKind.Ordinary, c1M01.MethodKind);
                    Assert.Equal("void C1.M01()", c1M01.ToTestDisplayString());
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }

                var c2 = module.GlobalNamespace.GetTypeMember("C2");

                var c2M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01.MethodKind);

                Assert.Equal("void modopt(I1) C2.I1.M01()", c2M01.ToTestDisplayString());
                Assert.Same(m01, c2M01.ExplicitInterfaceImplementations.Single());

                Assert.Same(c2M01, c2.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Single());
            }

            verifier.VerifyIL("C1.I1.M01()",
@"
{
  // Code size        6 (0x6)
  .maxstack  0
  IL_0000:  call       ""void C1.M01()""
  IL_0005:  ret
}
");
        }

        [Fact]
        public void ImplementAbstractStaticMethod_15()
        {
            // A forwarding method isn't created if base class implements interface exactly the same way. 

            var source1 =
@"
public interface I1
{
    abstract static void M01();
    abstract static void M02();
}

public class C1
{
    public static void M01() {}
}

public class C2 : C1, I1
{
    static void I1.M02() {}
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.RegularPreview, TestOptions.Regular9 })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));

                var m01 = c3.Interfaces().Single().GetMembers("M01").OfType<MethodSymbol>().Single();

                var c1M01 = c3.BaseType().BaseType().GetMember<MethodSymbol>("M01");
                Assert.Equal("void C1.M01()", c1M01.ToTestDisplayString());

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);
                Assert.False(c1M01.IsMetadataVirtual());
                Assert.False(c1M01.IsMetadataFinal);
                Assert.False(c1M01.IsMetadataNewSlot());

                Assert.Empty(c1M01.ExplicitInterfaceImplementations);

                if (c1M01.ContainingModule is PEModuleSymbol)
                {
                    var c2M01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);
                    Assert.Equal("void C2.I1.M01()", c2M01.ToTestDisplayString());
                    Assert.Same(m01, c2M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));
                }

                var m02 = c3.Interfaces().Single().GetMembers("M02").OfType<MethodSymbol>().Single();

                var c2M02 = c3.BaseType().GetMember<MethodSymbol>("I1.M02");
                Assert.Equal("void C2.I1.M02()", c2M02.ToTestDisplayString());
                Assert.Same(c2M02, c3.FindImplementationForInterfaceMember(m02));
            }
        }

        [Fact]
        public void ImplementAbstractStaticMethod_16()
        {
            // A new implicit implementation is properly considered.

            var source1 =
@"
public interface I1
{
    abstract static void M01();
}

public class C1 : I1
{
    public static void M01() {}
}

public class C2 : C1
{
    new public static void M01() {}
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    var verifier = CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

                    verifier.VerifyIL("C3.I1.M01()",
@"
{
  // Code size        6 (0x6)
  .maxstack  0
  IL_0000:  call       ""void C2.M01()""
  IL_0005:  ret
}
");
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                var m01 = c3.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                var c2M01 = c3.BaseType().GetMember<MethodSymbol>("M01");
                Assert.Equal("void C2.M01()", c2M01.ToTestDisplayString());

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());

                Assert.Empty(c2M01.ExplicitInterfaceImplementations);

                if (module is PEModuleSymbol)
                {
                    var c3M01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);
                    Assert.Equal("void C3.I1.M01()", c3M01.ToTestDisplayString());
                    Assert.Same(m01, c3M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Same(c2M01, c3.FindImplementationForInterfaceMember(m01));
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_17(bool genericFirst)
        {
            // An "ambiguity" in implicit implementation declared in generic base class 

            var generic =
@"
    public static void M01(T x) {}
";
            var nonGeneric =
@"
    public static void M01(int x) {}
";
            var source1 =
@"
public interface I1
{
    abstract static void M01(int x);
}

public class C1<T> : I1
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains("M01")).Count());
            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("void C1<T>.M01(System.Int32 x)", c1M01.OriginalDefinition.ToTestDisplayString());

                var baseI1M01 = c2.BaseType().FindImplementationForInterfaceMember(m01);
                Assert.Equal("void C1<T>.M01(System.Int32 x)", baseI1M01.OriginalDefinition.ToTestDisplayString());

                Assert.Equal(c1M01, baseI1M01);

                if (c1M01.OriginalDefinition.ContainingModule is PEModuleSymbol)
                {
                    Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_18(bool genericFirst)
        {
            // An "ambiguity" in implicit implementation declared in generic base class plus interface is generic too.

            var generic =
@"
    public static void M01(T x) {}
";
            var nonGeneric =
@"
    public static void M01(int x) {}
";
            var source1 =
@"
public interface I1<T>
{
    abstract static void M01(T x);
}

public class C1<T> : I1<T>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";



            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains("M01")).Count());
            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1<int>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("void C1<T>.M01(T x)", c1M01.OriginalDefinition.ToTestDisplayString());

                var baseI1M01 = c2.BaseType().FindImplementationForInterfaceMember(m01);
                Assert.Equal("void C1<T>.M01(T x)", baseI1M01.OriginalDefinition.ToTestDisplayString());

                Assert.Equal(c1M01, baseI1M01);

                if (c1M01.OriginalDefinition.ContainingModule is PEModuleSymbol)
                {
                    Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_19(bool genericFirst)
        {
            // Same as ImplementAbstractStaticMethod_17 only implementation is explicit in source.

            var generic =
@"
    public static void M01(T x) {}
";
            var nonGeneric =
@"
    static void I1.M01(int x) {}
";
            var source1 =
@"
public interface I1
{
    abstract static void M01(int x);
}

public class C1<T> : I1
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains("M01")).Count());
            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("void C1<T>.I1.M01(System.Int32 x)", c1M01.OriginalDefinition.ToTestDisplayString());
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(c1M01, c2.BaseType().FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_20(bool genericFirst)
        {
            // Same as ImplementAbstractStaticMethod_18 only implementation is explicit in source.

            var generic =
@"
    static void I1<T>.M01(T x) {}
";
            var nonGeneric =
@"
    public static void M01(int x) {}
";
            var source1 =
@"
public interface I1<T>
{
    abstract static void M01(T x);
}

public class C1<T> : I1<T>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains("M01")).Count());

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1<int>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("void C1<T>.I1<T>.M01(T x)", c1M01.OriginalDefinition.ToTestDisplayString());
                Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(c1M01, c2.BaseType().FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_21(bool genericFirst)
        {
            // Same as ImplementAbstractStaticMethod_17 only implicit implementation is in an intermediate base.

            var generic =
@"
    public static void M01(T x) {}
";
            var nonGeneric =
@"
    public static void M01(int x) {}
";
            var source1 =
@"
public interface I1
{
    abstract static void M01(int x);
}

public class C1<T>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}

public class C11<T> : C1<T>, I1
{
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains("M01")).Count());
            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C11<int>, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                var expectedDisplay = m01.ContainingModule is PEModuleSymbol ? "void C11<T>.I1.M01(System.Int32 x)" : "void C1<T>.M01(System.Int32 x)";
                Assert.Equal(expectedDisplay, c1M01.OriginalDefinition.ToTestDisplayString());

                var baseI1M01 = c2.BaseType().FindImplementationForInterfaceMember(m01);
                Assert.Equal(expectedDisplay, baseI1M01.OriginalDefinition.ToTestDisplayString());

                Assert.Equal(c1M01, baseI1M01);

                if (c1M01.OriginalDefinition.ContainingModule is PEModuleSymbol)
                {
                    Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticMethod_22(bool genericFirst)
        {
            // Same as ImplementAbstractStaticMethod_18 only implicit implementation is in an intermediate base.

            var generic =
@"
    public static void M01(T x) {}
";
            var nonGeneric =
@"
    public static void M01(int x) {}
";
            var source1 =
@"
public interface I1<T>
{
    abstract static void M01(T x);
}

public class C1<T>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}

public class C11<T> : C1<T>, I1<T>
{
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains("M01")).Count());
            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C11<int>, I1<int>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                var expectedDisplay = m01.ContainingModule is PEModuleSymbol ? "void C11<T>.I1<T>.M01(T x)" : "void C1<T>.M01(T x)";
                Assert.Equal(expectedDisplay, c1M01.OriginalDefinition.ToTestDisplayString());

                var baseI1M01 = c2.BaseType().FindImplementationForInterfaceMember(m01);
                Assert.Equal(expectedDisplay, baseI1M01.OriginalDefinition.ToTestDisplayString());

                Assert.Equal(c1M01, baseI1M01);

                if (c1M01.OriginalDefinition.ContainingModule is PEModuleSymbol)
                {
                    Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
            }
        }

        private static string UnaryOperatorName(string op) => OperatorFacts.UnaryOperatorNameFromSyntaxKindIfAny(SyntaxFactory.ParseToken(op).Kind());
        private static string BinaryOperatorName(string op) => op switch { ">>" => WellKnownMemberNames.RightShiftOperatorName, _ => OperatorFacts.BinaryOperatorNameFromSyntaxKindIfAny(SyntaxFactory.ParseToken(op).Kind()) };

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_01([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            string opName = UnaryOperatorName(op);

            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator " + op + @"(T x);
}

" + typeKeyword + @"
    C1 : I1<C1>
{}

" + typeKeyword + @"
    C2 : I1<C2>
{
    public C2 operator " + op + @"(C2 x) => throw null;
}

" + typeKeyword + @"
    C3 : I1<C3>
{
    static C3 operator " + op + @"(C3 x) => throw null;
}

" + typeKeyword + @"
    C4 : I1<C4>
{
    C4 I1<C4>.operator " + op + @"(C4 x) => throw null;
}

" + typeKeyword + @"
    C5 : I1<C5>
{
    public static int operator " + op + @" (C5 x) => throw null;
}

" + typeKeyword + @"
    C6 : I1<C6>
{
    static int I1<C6>.operator " + op + @" (C6 x) => throw null;
}

" + typeKeyword + @"
    C7 : I1<C7>
{
    public static C7 " + opName + @"(C7 x) => throw null;
}

" + typeKeyword + @"
    C8 : I1<C8>
{
    static C8 I1<C8>." + opName + @"(C8 x) => throw null;
}

public interface I2<T> where T : I2<T>
{
    abstract static T " + opName + @"(T x);
}

" + typeKeyword + @"
    C9 : I2<C9>
{
    public static C9 operator " + op + @"(C9 x) => throw null;
}

" + typeKeyword + @"
    C10 : I2<C10>
{
    static C10 I2<C10>.operator " + op + @"(C10 x) => throw null;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_BadIncDecRetType or (int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.ERR_OpTFRetType)).Verify(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1<C1>.operator +(C1)'
                //     C1 : I1<C1>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C1>").WithArguments("C1", "I1<C1>.operator " + op + "(C1)").WithLocation(8, 10),
                // (12,10): error CS9109: 'C2' does not implement static interface member 'I1<C2>.operator +(C2)'. 'C2.operator +(C2)' cannot implement the interface member because it is not static.
                //     C2 : I1<C2>
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotStatic, "I1<C2>").WithArguments("C2", "I1<C2>.operator " + op + "(C2)", "C2.operator " + op + "(C2)").WithLocation(12, 10),
                // (14,24): error CS0558: User-defined operator 'C2.operator +(C2)' must be declared static and public
                //     public C2 operator +(C2 x) => throw null;
                Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("C2.operator " + op + "(C2)").WithLocation(14, 24),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1<C3>.operator +(C3)'. 'C3.operator +(C3)' cannot implement an interface member because it is not public.
                //     C3 : I1<C3>
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1<C3>").WithArguments("C3", "I1<C3>.operator " + op + "(C3)", "C3.operator " + op + "(C3)").WithLocation(18, 10),
                // (20,24): error CS0558: User-defined operator 'C3.operator +(C3)' must be declared static and public
                //     static C3 operator +(C3 x) => throw null;
                Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("C3.operator " + op + "(C3)").WithLocation(20, 24),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1<C4>.operator +(C4)'
                //     C4 : I1<C4>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C4>").WithArguments("C4", "I1<C4>.operator " + op + "(C4)").WithLocation(24, 10),
                // (26,24): error CS9111: Explicit implementation of a user-defined operator 'C4.operator +(C4)' must be declared static
                //     C4 I1<C4>.operator +(C4 x) => throw null;
                Diagnostic(ErrorCode.ERR_ExplicitImplementationOfOperatorsMustBeStatic, op).WithArguments("C4.operator " + op + "(C4)").WithLocation(26, 24),
                // (26,24): error CS0539: 'C4.operator +(C4)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     C4 I1<C4>.operator +(C4 x) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C4.operator " + op + "(C4)").WithLocation(26, 24),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1<C5>.operator +(C5)'. 'C5.operator +(C5)' cannot implement 'I1<C5>.operator +(C5)' because it does not have the matching return type of 'C5'.
                //     C5 : I1<C5>
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1<C5>").WithArguments("C5", "I1<C5>.operator " + op + "(C5)", "C5.operator " + op + "(C5)", "C5").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1<C6>.operator +(C6)'
                //     C6 : I1<C6>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C6>").WithArguments("C6", "I1<C6>.operator " + op + "(C6)").WithLocation(36, 10),
                // (38,32): error CS0539: 'C6.operator +(C6)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static int I1<C6>.operator + (C6 x) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C6.operator " + op + "(C6)").WithLocation(38, 32),
                // (42,10): error CS0535: 'C7' does not implement interface member 'I1<C7>.operator +(C7)'
                //     C7 : I1<C7>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C7>").WithArguments("C7", "I1<C7>.operator " + op + "(C7)").WithLocation(42, 10),
                // (48,10): error CS0535: 'C8' does not implement interface member 'I1<C8>.operator +(C8)'
                //     C8 : I1<C8>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C8>").WithArguments("C8", "I1<C8>.operator " + op + "(C8)").WithLocation(48, 10),
                // (50,22): error CS0539: 'C8.op_UnaryPlus(C8)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static C8 I1<C8>.op_UnaryPlus(C8 x) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, opName).WithArguments("C8." + opName + "(C8)").WithLocation(50, 22),
                // (59,10): error CS0535: 'C9' does not implement interface member 'I2<C9>.op_UnaryPlus(C9)'
                //     C9 : I2<C9>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2<C9>").WithArguments("C9", "I2<C9>." + opName + "(C9)").WithLocation(59, 10),
                // (65,11): error CS0535: 'C10' does not implement interface member 'I2<C10>.op_UnaryPlus(C10)'
                //     C10 : I2<C10>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2<C10>").WithArguments("C10", "I2<C10>." + opName + "(C10)").WithLocation(65, 11),
                // (67,33): error CS0539: 'C10.operator +(C10)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static C10 I2<C10>.operator +(C10 x) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C10.operator " + op + "(C10)").WithLocation(67, 33)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_01([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            string opName = BinaryOperatorName(op);

            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator " + op + @"(T x, int y);
}

" + typeKeyword + @"
    C1 : I1<C1>
{}

" + typeKeyword + @"
    C2 : I1<C2>
{
    public C2 operator " + op + @"(C2 x, int y) => throw null;
}

" + typeKeyword + @"
    C3 : I1<C3>
{
    static C3 operator " + op + @"(C3 x, int y) => throw null;
}

" + typeKeyword + @"
    C4 : I1<C4>
{
    C4 I1<C4>.operator " + op + @"(C4 x, int y) => throw null;
}

" + typeKeyword + @"
    C5 : I1<C5>
{
    public static int operator " + op + @" (C5 x, int y) => throw null;
}

" + typeKeyword + @"
    C6 : I1<C6>
{
    static int I1<C6>.operator " + op + @" (C6 x, int y) => throw null;
}

" + typeKeyword + @"
    C7 : I1<C7>
{
    public static C7 " + opName + @"(C7 x, int y) => throw null;
}

" + typeKeyword + @"
    C8 : I1<C8>
{
    static C8 I1<C8>." + opName + @"(C8 x, int y) => throw null;
}

public interface I2<T> where T : I2<T>
{
    abstract static T " + opName + @"(T x, int y);
}

" + typeKeyword + @"
    C9 : I2<C9>
{
    public static C9 operator " + op + @"(C9 x, int y) => throw null;
}

" + typeKeyword + @"
    C10 : I2<C10>
{
    static C10 I2<C10>.operator " + op + @"(C10 x, int y) => throw null;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.WRN_EqualityOpWithoutEquals or (int)ErrorCode.WRN_EqualityOpWithoutGetHashCode)).Verify(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1<C1>.operator >>(C1, int)'
                //     C1 : I1<C1>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C1>").WithArguments("C1", "I1<C1>.operator " + op + "(C1, int)").WithLocation(8, 10),
                // (12,10): error CS9109: 'C2' does not implement static interface member 'I1<C2>.operator >>(C2, int)'. 'C2.operator >>(C2, int)' cannot implement the interface member because it is not static.
                //     C2 : I1<C2>
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotStatic, "I1<C2>").WithArguments("C2", "I1<C2>.operator " + op + "(C2, int)", "C2.operator " + op + "(C2, int)").WithLocation(12, 10),
                // (14,24): error CS0558: User-defined operator 'C2.operator >>(C2, int)' must be declared static and public
                //     public C2 operator >>(C2 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("C2.operator " + op + "(C2, int)").WithLocation(14, 24),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1<C3>.operator >>(C3, int)'. 'C3.operator >>(C3, int)' cannot implement an interface member because it is not public.
                //     C3 : I1<C3>
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1<C3>").WithArguments("C3", "I1<C3>.operator " + op + "(C3, int)", "C3.operator " + op + "(C3, int)").WithLocation(18, 10),
                // (20,24): error CS0558: User-defined operator 'C3.operator >>(C3, int)' must be declared static and public
                //     static C3 operator >>(C3 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("C3.operator " + op + "(C3, int)").WithLocation(20, 24),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1<C4>.operator >>(C4, int)'
                //     C4 : I1<C4>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C4>").WithArguments("C4", "I1<C4>.operator " + op + "(C4, int)").WithLocation(24, 10),
                // (26,24): error CS9111: Explicit implementation of a user-defined operator 'C4.operator >>(C4, int)' must be declared static
                //     C4 I1<C4>.operator >>(C4 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_ExplicitImplementationOfOperatorsMustBeStatic, op).WithArguments("C4.operator " + op + "(C4, int)").WithLocation(26, 24),
                // (26,24): error CS0539: 'C4.operator >>(C4, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     C4 I1<C4>.operator >>(C4 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C4.operator " + op + "(C4, int)").WithLocation(26, 24),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1<C5>.operator >>(C5, int)'. 'C5.operator >>(C5, int)' cannot implement 'I1<C5>.operator >>(C5, int)' because it does not have the matching return type of 'C5'.
                //     C5 : I1<C5>
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1<C5>").WithArguments("C5", "I1<C5>.operator " + op + "(C5, int)", "C5.operator " + op + "(C5, int)", "C5").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1<C6>.operator >>(C6, int)'
                //     C6 : I1<C6>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C6>").WithArguments("C6", "I1<C6>.operator " + op + "(C6, int)").WithLocation(36, 10),
                // (38,32): error CS0539: 'C6.operator >>(C6, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static int I1<C6>.operator >> (C6 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C6.operator " + op + "(C6, int)").WithLocation(38, 32),
                // (42,10): error CS0535: 'C7' does not implement interface member 'I1<C7>.operator >>(C7, int)'
                //     C7 : I1<C7>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C7>").WithArguments("C7", "I1<C7>.operator " + op + "(C7, int)").WithLocation(42, 10),
                // (48,10): error CS0535: 'C8' does not implement interface member 'I1<C8>.operator >>(C8, int)'
                //     C8 : I1<C8>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1<C8>").WithArguments("C8", "I1<C8>.operator " + op + "(C8, int)").WithLocation(48, 10),
                // (50,22): error CS0539: 'C8.op_RightShift(C8, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static C8 I1<C8>.op_RightShift(C8 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, opName).WithArguments("C8." + opName + "(C8, int)").WithLocation(50, 22),
                // (59,10): error CS0535: 'C9' does not implement interface member 'I2<C9>.op_RightShift(C9, int)'
                //     C9 : I2<C9>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2<C9>").WithArguments("C9", "I2<C9>." + opName + "(C9, int)").WithLocation(59, 10),
                // (65,11): error CS0535: 'C10' does not implement interface member 'I2<C10>.op_RightShift(C10, int)'
                //     C10 : I2<C10>
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2<C10>").WithArguments("C10", "I2<C10>." + opName + "(C10, int)").WithLocation(65, 11),
                // (67,33): error CS0539: 'C10.operator >>(C10, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static C10 I2<C10>.operator >>(C10 x, int y) => throw null;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C10.operator " + op + "(C10, int)").WithLocation(67, 33)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_03([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op)
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x);
}

interface I2 : I1
{}

interface I3 : I1
{
    I1 operator " + op + @"(I1 x) => default;
}

interface I4 : I1
{
    static I1 operator " + op + @"(I1 x) => default;
}

interface I5 : I1
{
    I1 I1.operator " + op + @"(I1 x) => default;
}

interface I6 : I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}

interface I7 : I1
{
    abstract static I1 operator " + op + @"(I1 x);
}

public interface I11<T> where T : I11<T>
{
    abstract static T operator " + op + @"(T x);
}

interface I8<T> : I11<T> where T : I8<T>
{
    T operator " + op + @"(T x) => default;
}

interface I9<T> : I11<T> where T : I9<T>
{
    static T operator " + op + @"(T x) => default;
}

interface I10<T> : I11<T> where T : I10<T>
{
    abstract static T operator " + op + @"(T x);
}

interface I12<T> : I11<T> where T : I12<T>
{
    static T I11<T>.operator " + op + @"(T x) => default;
}

interface I13<T> : I11<T> where T : I13<T>
{
    abstract static T I11<T>.operator " + op + @"(T x);
}

interface I14 : I1
{
    abstract static I1 I1.operator " + op + @"(I1 x);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            ErrorCode badSignatureError = op.Length != 2 ? ErrorCode.ERR_BadUnaryOperatorSignature : ErrorCode.ERR_BadIncDecSignature;
            ErrorCode badAbstractSignatureError = op.Length != 2 ? ErrorCode.ERR_BadAbstractUnaryOperatorSignature : ErrorCode.ERR_BadAbstractIncDecSignature;

            compilation1.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.ERR_OpTFRetType)).Verify(
                // (12,17): error CS0558: User-defined operator 'I3.operator +(I1)' must be declared static and public
                //     I1 operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("I3.operator " + op + "(I1)").WithLocation(12, 17),
                // (12,17): error CS0562: The parameter of a unary operator must be the containing type
                //     I1 operator +(I1 x) => default;
                Diagnostic(badSignatureError, op).WithLocation(12, 17),
                // (17,24): error CS0562: The parameter of a unary operator must be the containing type
                //     static I1 operator +(I1 x) => default;
                Diagnostic(badSignatureError, op).WithLocation(17, 24),
                // (22,20): error CS9111: Explicit implementation of a user-defined operator 'I5.operator +(I1)' must be declared static
                //     I1 I1.operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_ExplicitImplementationOfOperatorsMustBeStatic, op).WithArguments("I5.operator " + op + "(I1)").WithLocation(22, 20),
                // (22,20): error CS0539: 'I5.operator +(I1)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     I1 I1.operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I5.operator " + op + "(I1)").WithLocation(22, 20),
                // (27,27): error CS0539: 'I6.operator +(I1)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static I1 I1.operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I6.operator " + op + "(I1)").WithLocation(27, 27),
                // (32,33): error CS9102: The parameter of a unary operator must be the containing type, or its type parameter constrained to it.
                //     abstract static I1 operator +(I1 x);
                Diagnostic(badAbstractSignatureError, op).WithLocation(32, 33),
                // (42,16): error CS0558: User-defined operator 'I8<T>.operator +(T)' must be declared static and public
                //     T operator +(T x) => default;
                Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("I8<T>.operator " + op + "(T)").WithLocation(42, 16),
                // (42,16): error CS0562: The parameter of a unary operator must be the containing type
                //     T operator +(T x) => default;
                Diagnostic(badSignatureError, op).WithLocation(42, 16),
                // (47,23): error CS0562: The parameter of a unary operator must be the containing type
                //     static T operator +(T x) => default;
                Diagnostic(badSignatureError, op).WithLocation(47, 23),
                // (57,30): error CS0539: 'I12<T>.operator +(T)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static T I11<T>.operator +(T x) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I12<T>.operator " + op + "(T)").WithLocation(57, 30),
                // (62,39): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static T I11<T>.operator +(T x);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("abstract").WithLocation(62, 39),
                // (62,39): error CS0501: 'I13<T>.operator +(T)' must declare a body because it is not marked abstract, extern, or partial
                //     abstract static T I11<T>.operator +(T x);
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, op).WithArguments("I13<T>.operator " + op + "(T)").WithLocation(62, 39),
                // (62,39): error CS0539: 'I13<T>.operator +(T)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     abstract static T I11<T>.operator +(T x);
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I13<T>.operator " + op + "(T)").WithLocation(62, 39),
                // (67,36): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static I1 I1.operator +(I1 x);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("abstract").WithLocation(67, 36),
                // (67,36): error CS0501: 'I14.operator +(I1)' must declare a body because it is not marked abstract, extern, or partial
                //     abstract static I1 I1.operator +(I1 x);
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, op).WithArguments("I14.operator " + op + "(I1)").WithLocation(67, 36),
                // (67,36): error CS0539: 'I14.operator +(I1)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     abstract static I1 I1.operator +(I1 x);
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I14.operator " + op + "(I1)").WithLocation(67, 36)
                );

            var m01 = compilation1.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();

            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I2").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I3").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I4").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I5").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I6").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I7").FindImplementationForInterfaceMember(m01));

            var i8 = compilation1.GlobalNamespace.GetTypeMember("I8");
            Assert.Null(i8.FindImplementationForInterfaceMember(i8.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i9 = compilation1.GlobalNamespace.GetTypeMember("I9");
            Assert.Null(i9.FindImplementationForInterfaceMember(i9.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i10 = compilation1.GlobalNamespace.GetTypeMember("I10");
            Assert.Null(i10.FindImplementationForInterfaceMember(i10.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i12 = compilation1.GlobalNamespace.GetTypeMember("I12");
            Assert.Null(i12.FindImplementationForInterfaceMember(i12.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i13 = compilation1.GlobalNamespace.GetTypeMember("I13");
            Assert.Null(i13.FindImplementationForInterfaceMember(i13.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I14").FindImplementationForInterfaceMember(m01));
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_03([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x, int y);
}

interface I2 : I1
{}

interface I3 : I1
{
    I1 operator " + op + @"(I1 x, int y) => default;
}

interface I4 : I1
{
    static I1 operator " + op + @"(I1 x, int y) => default;
}

interface I5 : I1
{
    I1 I1.operator " + op + @"(I1 x, int y) => default;
}

interface I6 : I1
{
    static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

interface I7 : I1
{
    abstract static I1 operator " + op + @"(I1 x, int y);
}

public interface I11<T> where T : I11<T>
{
    abstract static T operator " + op + @"(T x, int y);
}

interface I8<T> : I11<T> where T : I8<T>
{
    T operator " + op + @"(T x, int y) => default;
}

interface I9<T> : I11<T> where T : I9<T>
{
    static T operator " + op + @"(T x, int y) => default;
}

interface I10<T> : I11<T> where T : I10<T>
{
    abstract static T operator " + op + @"(T x, int y);
}

interface I12<T> : I11<T> where T : I12<T>
{
    static T I11<T>.operator " + op + @"(T x, int y) => default;
}

interface I13<T> : I11<T> where T : I13<T>
{
    abstract static T I11<T>.operator " + op + @"(T x, int y);
}

interface I14 : I1
{
    abstract static I1 I1.operator " + op + @"(I1 x, int y);
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            bool isShift = op == "<<" || op == ">>";
            ErrorCode badSignatureError = isShift ? ErrorCode.ERR_BadShiftOperatorSignature : ErrorCode.ERR_BadBinaryOperatorSignature;
            ErrorCode badAbstractSignatureError = isShift ? ErrorCode.ERR_BadAbstractShiftOperatorSignature : ErrorCode.ERR_BadAbstractBinaryOperatorSignature;

            var expected = new[] {
                // (12,17): error CS0563: One of the parameters of a binary operator must be the containing type
                //     I1 operator |(I1 x, int y) => default;
                Diagnostic(badSignatureError, op).WithLocation(12, 17),
                // (17,24): error CS0563: One of the parameters of a binary operator must be the containing type
                //     static I1 operator |(I1 x, int y) => default;
                Diagnostic(badSignatureError, op).WithLocation(17, 24),
                // (22,20): error CS9111: Explicit implementation of a user-defined operator 'I5.operator |(I1, int)' must be declared static
                //     I1 I1.operator |(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_ExplicitImplementationOfOperatorsMustBeStatic, op).WithArguments("I5.operator " + op + "(I1, int)").WithLocation(22, 20),
                // (22,20): error CS0539: 'I5.operator |(I1, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     I1 I1.operator |(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I5.operator " + op + "(I1, int)").WithLocation(22, 20),
                // (27,27): error CS0539: 'I6.operator |(I1, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static I1 I1.operator |(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I6.operator " + op + "(I1, int)").WithLocation(27, 27),
                // (32,33): error CS9105: One of the parameters of a binary operator must be the containing type, or its type parameter constrained to it.
                //     abstract static I1 operator |(I1 x, int y);
                Diagnostic(badAbstractSignatureError, op).WithLocation(32, 33),
                // (42,16): error CS0563: One of the parameters of a binary operator must be the containing type
                //     T operator |(T x, int y) => default;
                Diagnostic(badSignatureError, op).WithLocation(42, 16),
                // (47,23): error CS0563: One of the parameters of a binary operator must be the containing type
                //     static T operator |(T x, int y) => default;
                Diagnostic(badSignatureError, op).WithLocation(47, 23),
                // (57,30): error CS0539: 'I12<T>.operator |(T, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static T I11<T>.operator |(T x, int y) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I12<T>.operator " + op + "(T, int)").WithLocation(57, 30),
                // (62,39): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static T I11<T>.operator |(T x, int y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("abstract").WithLocation(62, 39),
                // (62,39): error CS0501: 'I13<T>.operator |(T, int)' must declare a body because it is not marked abstract, extern, or partial
                //     abstract static T I11<T>.operator |(T x, int y);
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, op).WithArguments("I13<T>.operator " + op + "(T, int)").WithLocation(62, 39),
                // (62,39): error CS0539: 'I13<T>.operator |(T, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     abstract static T I11<T>.operator |(T x, int y);
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I13<T>.operator " + op + "(T, int)").WithLocation(62, 39),
                // (67,36): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static I1 I1.operator |(I1 x, int y);
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("abstract").WithLocation(67, 36),
                // (67,36): error CS0501: 'I14.operator |(I1, int)' must declare a body because it is not marked abstract, extern, or partial
                //     abstract static I1 I1.operator |(I1 x, int y);
                Diagnostic(ErrorCode.ERR_ConcreteMissingBody, op).WithArguments("I14.operator " + op + "(I1, int)").WithLocation(67, 36),
                // (67,36): error CS0539: 'I14.operator |(I1, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     abstract static I1 I1.operator |(I1 x, int y);
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("I14.operator " + op + "(I1, int)").WithLocation(67, 36)
                };

            if (op is "==" or "!=")
            {
                expected = expected.Concat(
                    new[] {
                        // (12,17): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                        //     I1 operator ==(I1 x, int y) => default;
                        Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, op).WithLocation(12, 17),
                        // (17,24): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                        //     static I1 operator ==(I1 x, int y) => default;
                        Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, op).WithLocation(17, 24),
                        // (42,16): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                        //     T operator ==(T x, int y) => default;
                        Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, op).WithLocation(42, 16),
                        // (47,23): error CS0567: Conversion, equality, or inequality operators declared in interfaces must be abstract
                        //     static T operator ==(T x, int y) => default;
                        Diagnostic(ErrorCode.ERR_InterfacesCantContainConversionOrEqualityOperators, op).WithLocation(47, 23),
                        }
                    ).ToArray();
            }
            else
            {
                expected = expected.Concat(
                    new[] {
                        // (12,17): error CS0558: User-defined operator 'I3.operator |(I1, int)' must be declared static and public
                        //     I1 operator |(I1 x, int y) => default;
                        Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("I3.operator " + op + "(I1, int)").WithLocation(12, 17),
                        // (42,16): error CS0558: User-defined operator 'I8<T>.operator |(T, int)' must be declared static and public
                        //     T operator |(T x, int y) => default;
                        Diagnostic(ErrorCode.ERR_OperatorsMustBeStatic, op).WithArguments("I8<T>.operator " + op + "(T, int)").WithLocation(42, 16)
                        }
                    ).ToArray();
            }

            compilation1.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(expected);

            var m01 = compilation1.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();

            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I2").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I3").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I4").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I5").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I6").FindImplementationForInterfaceMember(m01));
            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I7").FindImplementationForInterfaceMember(m01));

            var i8 = compilation1.GlobalNamespace.GetTypeMember("I8");
            Assert.Null(i8.FindImplementationForInterfaceMember(i8.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i9 = compilation1.GlobalNamespace.GetTypeMember("I9");
            Assert.Null(i9.FindImplementationForInterfaceMember(i9.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i10 = compilation1.GlobalNamespace.GetTypeMember("I10");
            Assert.Null(i10.FindImplementationForInterfaceMember(i10.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i12 = compilation1.GlobalNamespace.GetTypeMember("I12");
            Assert.Null(i12.FindImplementationForInterfaceMember(i12.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            var i13 = compilation1.GlobalNamespace.GetTypeMember("I13");
            Assert.Null(i13.FindImplementationForInterfaceMember(i13.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single()));

            Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I14").FindImplementationForInterfaceMember(m01));
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_04([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x);
}

public interface I2<T> where T : I2<T>
{
    abstract static T operator " + op + @"(T x);
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}
" + typeKeyword + @"
    Test2: I2<Test2>
{
    public static Test2 operator " + op + @"(Test2 x) => default;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.ERR_OpTFRetType)).Verify(
                // (4,15): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static I1 I1.operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "I1.").WithArguments("static abstract members in interfaces").WithLocation(4, 15)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.ERR_OpTFRetType)).Verify(
                // (4,15): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static I1 I1.operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "I1.").WithArguments("static abstract members in interfaces").WithLocation(4, 15),
                // (14,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static I1 operator +(I1 x);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, op).WithArguments("abstract", "9.0", "preview").WithLocation(14, 33),
                // (19,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static T operator +(T x);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, op).WithArguments("abstract", "9.0", "preview").WithLocation(19, 32)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_04([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x, int y);
}

public interface I2<T> where T : I2<T>
{
    abstract static T operator " + op + @"(T x, int y);
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    static I1 I1.operator " + op + @"(I1 x, int y) => default;
}
" + typeKeyword + @"
    Test2: I2<Test2>
{
    public static Test2 operator " + op + @"(Test2 x, int y) => default;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.WRN_EqualityOpWithoutEquals or (int)ErrorCode.WRN_EqualityOpWithoutGetHashCode)).Verify(
                // (4,15): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static I1 I1.operator +(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "I1.").WithArguments("static abstract members in interfaces").WithLocation(4, 15)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.WRN_EqualityOpWithoutEquals or (int)ErrorCode.WRN_EqualityOpWithoutGetHashCode)).Verify(
                // (4,15): error CS8652: The feature 'static abstract members in interfaces' is currently in Preview and *unsupported*. To use Preview features, use the 'preview' language version.
                //     static I1 I1.operator +(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_FeatureInPreview, "I1.").WithArguments("static abstract members in interfaces").WithLocation(4, 15),
                // (14,33): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static I1 operator +(I1 x, int y);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, op).WithArguments("abstract", "9.0", "preview").WithLocation(14, 33),
                // (19,32): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static T operator +(T x, int y);
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, op).WithArguments("abstract", "9.0", "preview").WithLocation(19, 32)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_05([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1<T> where T : I1<T> 
{
    abstract static T operator " + op + @"(T x);
}
";
            var source2 =
typeKeyword + @"
    Test1: I1<Test1>
{
    public static Test1 operator " + op + @"(Test1 x) => default;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.ERR_OpTFRetType)).Verify(
                // (2,12): error CS9110: 'Test1.operator +(Test1)' cannot implement interface member 'I1<Test1>.operator +(Test1)' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1<Test1>
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1<Test1>").WithArguments("Test1.operator " + op + "(Test1)", "I1<Test1>.operator " + op + "(Test1)", "Test1").WithLocation(2, 12)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.ERR_OpTFRetType)).Verify(
                // (2,12): error CS9110: 'Test1.operator +(Test1)' cannot implement interface member 'I1<Test1>.operator +(Test1)' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1<Test1>
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1<Test1>").WithArguments("Test1.operator " + op + "(Test1)", "I1<Test1>.operator " + op + "(Test1)", "Test1").WithLocation(2, 12),
                // (9,32): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static T operator +(T x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(9, 32)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_05([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1<T> where T : I1<T> 
{
    abstract static T operator " + op + @"(T x, int y);
}
";
            var source2 =
typeKeyword + @"
    Test1: I1<Test1>
{
    public static Test1 operator " + op + @"(Test1 x, int y) => default;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.WRN_EqualityOpWithoutEquals or (int)ErrorCode.WRN_EqualityOpWithoutGetHashCode)).Verify(
                // (2,12): error CS9110: 'Test1.operator >>(Test1, int)' cannot implement interface member 'I1<Test1>.operator >>(Test1, int)' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1<Test1>
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1<Test1>").WithArguments("Test1.operator " + op + "(Test1, int)", "I1<Test1>.operator " + op + "(Test1, int)", "Test1").WithLocation(2, 12)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.WRN_EqualityOpWithoutEquals or (int)ErrorCode.WRN_EqualityOpWithoutGetHashCode)).Verify(
                // (2,12): error CS9110: 'Test1.operator >>(Test1, int)' cannot implement interface member 'I1<Test1>.operator >>(Test1, int)' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1<Test1>
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1<Test1>").WithArguments("Test1.operator " + op + "(Test1, int)", "I1<Test1>.operator " + op + "(Test1, int)", "Test1").WithLocation(2, 12),
                // (9,32): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static T operator >>(T x, int y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(9, 32)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_06([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x);
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,27): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static I1 I1.operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(4, 27)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OperatorNeedsMatch or (int)ErrorCode.ERR_OpTFRetType)).Verify(
                // (4,27): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static I1 I1.operator +(I1 x) => default;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(4, 27),
                // (9,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator +(I1 x);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(9, 33)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_06([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op, bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x, int y);
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    static I1 I1.operator " + op + @"(I1 x, int y) => default;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,27): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static I1 I1.operator +(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(4, 27)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (4,27): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static I1 I1.operator +(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(4, 27),
                // (9,33): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static I1 operator +(I1 x, int y);
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, op).WithLocation(9, 33)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_07([CombinatorialValues("+", "-", "!", "~", "++", "--")] string op, bool structure)
        {
            // Basic implicit implementation scenario, MethodImpl is emitted

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1<T> where T : I1<T>
{
    abstract static T operator " + op + @"(T x);
}

" + typeKeyword + @"
    C : I1<C>
{
    public static C operator " + op + @"(C x) => default;
}
";

            var opName = UnaryOperatorName(op);
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c = module.GlobalNamespace.GetTypeMember("C");
                var i1 = c.Interfaces().Single();
                var m01 = i1.GetMembers().OfType<MethodSymbol>().Single();

                Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.UserDefinedOperator, cM01.MethodKind);
                Assert.False(cM01.HasRuntimeSpecialName);
                Assert.True(cM01.HasSpecialName);

                Assert.Equal("C C." + opName + "(C x)", cM01.ToTestDisplayString());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(m01, cM01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryTrueFalseOperator_07([CombinatorialValues("true", "false")] string op, bool structure)
        {
            // Basic implicit implementation scenario, MethodImpl is emitted

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public partial interface I1<T> where T : I1<T>
{
    abstract static bool operator " + op + @"(T x);
}

partial " + typeKeyword + @"
    C : I1<C>
{
    public static bool operator " + op + @"(C x) => default;
}
";
            string matchingOp = op == "true" ? "false" : "true";

            source1 +=
@"
public partial interface I1<T> where T : I1<T>
{
    abstract static bool operator " + matchingOp + @"(T x);
}

partial " + typeKeyword + @"
    C
{
    public static bool operator " + matchingOp + @"(C x) => default;
}
";

            var opName = UnaryOperatorName(op);
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c = module.GlobalNamespace.GetTypeMember("C");
                var i1 = c.Interfaces().Single();
                var m01 = i1.GetMembers(opName).OfType<MethodSymbol>().Single();

                Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.UserDefinedOperator, cM01.MethodKind);
                Assert.False(cM01.HasRuntimeSpecialName);
                Assert.True(cM01.HasSpecialName);

                Assert.Equal("System.Boolean C." + opName + "(C x)", cM01.ToTestDisplayString());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(m01, cM01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_07([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op, bool structure)
        {
            // Basic implicit implementation scenario, MethodImpl is emitted

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public partial interface I1<T> where T : I1<T>
{
    abstract static T operator " + op + @"(T x, int y);
}

#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

partial " + typeKeyword + @"
    C : I1<C>
{
    public static C operator " + op + @"(C x, int y) => default;
}
";
            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1<T> where T : I1<T>
{
    abstract static T operator " + matchingOp + @"(T x, int y);
}

partial " + typeKeyword + @"
    C
{
    public static C operator " + matchingOp + @"(C x, int y) => default;
}
";
            }

            var opName = BinaryOperatorName(op);
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c = module.GlobalNamespace.GetTypeMember("C");
                var i1 = c.Interfaces().Single();
                var m01 = i1.GetMembers(opName).OfType<MethodSymbol>().Single();

                Assert.Equal(matchingOp is null ? 1 : 2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.UserDefinedOperator, cM01.MethodKind);
                Assert.False(cM01.HasRuntimeSpecialName);
                Assert.True(cM01.HasSpecialName);

                Assert.Equal("C C." + opName + "(C x, System.Int32 y)", cM01.ToTestDisplayString());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(m01, cM01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_08([CombinatorialValues("+", "-", "!", "~", "++", "--")] string op, bool structure)
        {
            // Basic explicit implementation scenario

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x);
}

" + typeKeyword + @"
    C : I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}
";

            var opName = UnaryOperatorName(op);
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().Single();

            Assert.Equal("default", node.ToString());
            Assert.Equal("I1", model.GetTypeInfo(node).ConvertedType.ToTestDisplayString());

            var declaredSymbol = model.GetDeclaredSymbol(node.FirstAncestorOrSelf<OperatorDeclarationSyntax>());
            Assert.Equal("I1 C.I1." + opName + "(I1 x)", declaredSymbol.ToTestDisplayString());
            Assert.DoesNotContain(opName, declaredSymbol.ContainingType.MemberNames);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<MethodSymbol>().Single();
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, cM01.MethodKind);
                Assert.False(cM01.HasRuntimeSpecialName);
                Assert.False(cM01.HasSpecialName);

                Assert.Equal("I1 C.I1." + opName + "(I1 x)", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryTrueFalseOperator_08([CombinatorialValues("true", "false")] string op, bool structure)
        {
            // Basic explicit implementation scenario

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public partial interface I1
{
    abstract static bool operator " + op + @"(I1 x);
}

partial " + typeKeyword + @"
    C : I1
{
    static bool I1.operator " + op + @"(I1 x) => default;
}
";
            string matchingOp = op == "true" ? "false" : "true";

            source1 +=
@"
public partial interface I1
{
    abstract static bool operator " + matchingOp + @"(I1 x);
}

partial " + typeKeyword + @"
    C
{
    static bool I1.operator " + matchingOp + @"(I1 x) => default;
}
";

            var opName = UnaryOperatorName(op);
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().First();

            Assert.Equal("default", node.ToString());
            Assert.Equal("System.Boolean", model.GetTypeInfo(node).ConvertedType.ToTestDisplayString());

            var declaredSymbol = model.GetDeclaredSymbol(node.FirstAncestorOrSelf<OperatorDeclarationSyntax>());
            Assert.Equal("System.Boolean C.I1." + opName + "(I1 x)", declaredSymbol.ToTestDisplayString());
            Assert.DoesNotContain(opName, declaredSymbol.ContainingType.MemberNames);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers(opName).OfType<MethodSymbol>().Single();
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, cM01.MethodKind);
                Assert.False(cM01.HasRuntimeSpecialName);
                Assert.False(cM01.HasSpecialName);

                Assert.Equal("System.Boolean C.I1." + opName + "(I1 x)", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_08([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op, bool structure)
        {
            // Basic explicit implementation scenario

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public partial interface I1
{
    abstract static I1 operator " + op + @"(I1 x, int y);
}

partial " + typeKeyword + @"
    C : I1
{
    static I1 I1.operator " + op + @"(I1 x, int y) => default;
}
";
            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1
{
    abstract static I1 operator " + matchingOp + @"(I1 x, int y);
}

partial " + typeKeyword + @"
    C
{
    static I1 I1.operator " + matchingOp + @"(I1 x, int y) => default;
}
";
            }

            var opName = BinaryOperatorName(op);
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);


            var tree = compilation1.SyntaxTrees.Single();
            var model = compilation1.GetSemanticModel(tree);
            var node = tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().First();

            Assert.Equal("default", node.ToString());
            Assert.Equal("I1", model.GetTypeInfo(node).ConvertedType.ToTestDisplayString());

            var declaredSymbol = model.GetDeclaredSymbol(node.FirstAncestorOrSelf<OperatorDeclarationSyntax>());
            Assert.Equal("I1 C.I1." + opName + "(I1 x, System.Int32 y)", declaredSymbol.ToTestDisplayString());
            Assert.DoesNotContain(opName, declaredSymbol.ContainingType.MemberNames);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers(opName).OfType<MethodSymbol>().Single();
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(matchingOp is null ? 1 : 2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (MethodSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);
                Assert.False(cM01.IsMetadataVirtual());
                Assert.False(cM01.IsMetadataFinal);
                Assert.False(cM01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, cM01.MethodKind);
                Assert.False(cM01.HasRuntimeSpecialName);
                Assert.False(cM01.HasSpecialName);

                Assert.Equal("I1 C.I1." + opName + "(I1 x, System.Int32 y)", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_09([CombinatorialValues("+", "-", "!", "~", "++", "--")] string op)
        {
            // Explicit implementation from base is treated as an implementation

            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x);
}

public class C2 : I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            var opName = UnaryOperatorName(op);

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                     parseOptions: parseOptions,
                                                     targetFramework: TargetFramework.NetCoreApp,
                                                     references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));
                var m01 = c3.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                var cM01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);

                Assert.Equal("I1 C2.I1." + opName + "(I1 x)", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryTrueFalseOperator_09([CombinatorialValues("true", "false")] string op)
        {
            // Explicit implementation from base is treated as an implementation

            var source1 =
@"
public partial interface I1
{
    abstract static bool operator " + op + @"(I1 x);
}

public partial class C2 : I1
{
    static bool I1.operator " + op + @"(I1 x) => default;
}
";
            string matchingOp = op == "true" ? "false" : "true";

            source1 +=
@"
public partial interface I1
{
    abstract static bool operator " + matchingOp + @"(I1 x);
}

public partial class C2
{
    static bool I1.operator " + matchingOp + @"(I1 x) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            var opName = UnaryOperatorName(op);

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                     parseOptions: parseOptions,
                                                     targetFramework: TargetFramework.NetCoreApp,
                                                     references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));
                var m01 = c3.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().Single();

                var cM01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);

                Assert.Equal("System.Boolean C2.I1." + opName + "(I1 x)", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_09([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // Explicit implementation from base is treated as an implementation

            var source1 =
@"
public partial interface I1
{
    abstract static I1 operator " + op + @"(I1 x, int y);
}

public partial class C2 : I1
{
    static I1 I1.operator " + op + @"(I1 x, int y) => default;
}
";
            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1
{
    abstract static I1 operator " + matchingOp + @"(I1 x, int y);
}

public partial class C2
{
    static I1 I1.operator " + matchingOp + @"(I1 x, int y) => default;
}
";
            }

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            var opName = BinaryOperatorName(op);

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                     parseOptions: parseOptions,
                                                     targetFramework: TargetFramework.NetCoreApp,
                                                     references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));
                var m01 = c3.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().Single();

                var cM01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);

                Assert.Equal("I1 C2.I1." + opName + "(I1 x, System.Int32 y)", cM01.ToTestDisplayString());
                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_10([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op)
        {
            // Implicit implementation is considered only for types implementing interface in source.
            // In metadata, only explicit implementations are considered

            var opName = UnaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        class I1 " + opName + @" (
            class I1 x
        ) cil managed 
    {
    }
}

.class public auto ansi beforefieldinit C1
    extends System.Object
    implements I1
{
    .method private hidebysig
        static class I1 I1." + opName + @" (class I1 x) cil managed 
    {
        .override method class I1 I1::" + opName + @"(class I1)

        IL_0000: ldnull
        IL_0001: ret
    }

    .method public hidebysig static
        specialname class I1 " + opName + @" (class I1 x) cil managed 
    {
        IL_0000: ldnull
        IL_0001: ret
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void System.Object::.ctor()
        IL_0006: ret
    }
}

.class public auto ansi beforefieldinit C2
    extends C1
    implements I1
{
    .method public hidebysig static
        specialname class I1 " + opName + @" (class I1 x) cil managed 
    {
        IL_0000: ldnull
        IL_0001: ret
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void C1::.ctor()
        IL_0006: ret
    } // end of method C2::.ctor
} // end of class C2
";
            var source1 =
@"
public class C3 : C2
{
}

public class C4 : C1, I1
{
}

public class C5 : C2, I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal(MethodKind.UserDefinedOperator, m01.MethodKind);
            Assert.Equal(MethodKind.UserDefinedOperator, c1.GetMember<MethodSymbol>(opName).MethodKind);

            var c1M01 = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01);

            Assert.Equal("I1 C1.I1." + opName + "(I1 x)", c1M01.ToTestDisplayString());
            Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01.MethodKind);
            Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());

            var c2 = compilation1.GlobalNamespace.GetTypeMember("C2");
            Assert.Same(c1M01, c2.FindImplementationForInterfaceMember(m01));

            var c3 = compilation1.GlobalNamespace.GetTypeMember("C3");
            Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));

            var c4 = compilation1.GlobalNamespace.GetTypeMember("C4");
            Assert.Same(c1M01, c4.FindImplementationForInterfaceMember(m01));

            var c5 = compilation1.GlobalNamespace.GetTypeMember("C5");

            var c2M01 = (MethodSymbol)c5.FindImplementationForInterfaceMember(m01);
            Assert.Equal("I1 C2." + opName + "(I1 x)", c2M01.ToTestDisplayString());
            Assert.Equal(MethodKind.UserDefinedOperator, c2M01.MethodKind);

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_10([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // Implicit implementation is considered only for types implementing interface in source.
            // In metadata, only explicit implementations are considered

            var opName = BinaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        class I1 " + opName + @" (
            class I1 x,
            int32 y
        ) cil managed 
    {
    }
}

.class public auto ansi beforefieldinit C1
    extends System.Object
    implements I1
{
    .method private hidebysig
        static class I1 I1." + opName + @" (class I1 x, int32 y) cil managed 
    {
        .override method class I1 I1::" + opName + @"(class I1, int32)

        IL_0000: ldnull
        IL_0001: ret
    }

    .method public hidebysig static
        specialname class I1 " + opName + @" (class I1 x, int32 y) cil managed 
    {
        IL_0000: ldnull
        IL_0001: ret
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void System.Object::.ctor()
        IL_0006: ret
    }
}

.class public auto ansi beforefieldinit C2
    extends C1
    implements I1
{
    .method public hidebysig static
        specialname class I1 " + opName + @" (class I1 x, int32 y) cil managed 
    {
        IL_0000: ldnull
        IL_0001: ret
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void C1::.ctor()
        IL_0006: ret
    } // end of method C2::.ctor
} // end of class C2
";
            var source1 =
@"
public class C3 : C2
{
}

public class C4 : C1, I1
{
}

public class C5 : C2, I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal(MethodKind.UserDefinedOperator, m01.MethodKind);
            Assert.Equal(MethodKind.UserDefinedOperator, c1.GetMember<MethodSymbol>(opName).MethodKind);

            var c1M01 = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01);

            Assert.Equal("I1 C1.I1." + opName + "(I1 x, System.Int32 y)", c1M01.ToTestDisplayString());
            Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01.MethodKind);
            Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());

            var c2 = compilation1.GlobalNamespace.GetTypeMember("C2");
            Assert.Same(c1M01, c2.FindImplementationForInterfaceMember(m01));

            var c3 = compilation1.GlobalNamespace.GetTypeMember("C3");
            Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));

            var c4 = compilation1.GlobalNamespace.GetTypeMember("C4");
            Assert.Same(c1M01, c4.FindImplementationForInterfaceMember(m01));

            var c5 = compilation1.GlobalNamespace.GetTypeMember("C5");

            var c2M01 = (MethodSymbol)c5.FindImplementationForInterfaceMember(m01);
            Assert.Equal("I1 C2." + opName + "(I1 x, System.Int32 y)", c2M01.ToTestDisplayString());
            Assert.Equal(MethodKind.UserDefinedOperator, c2M01.MethodKind);

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_11([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op)
        {
            // Ignore invalid metadata (non-abstract static virtual method). 

            var opName = UnaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname virtual
        static class I1 " + opName + @" (
            class I1 x
        ) cil managed 
    {
        IL_0000: ldnull
        IL_0001: ret
    }
}
";

            var source1 =
@"
public class C1 : I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i1 = c1.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal(MethodKind.UserDefinedOperator, m01.MethodKind);
            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics();

            var source2 =
@"
public class C1 : I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}
";

            var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation2.VerifyEmitDiagnostics(
                // (4,27): error CS0539: 'C1.operator ~(I1)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static I1 I1.operator ~(I1 x) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C1.operator " + op + "(I1)").WithLocation(4, 27)
                );

            c1 = compilation2.GlobalNamespace.GetTypeMember("C1");
            m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal("I1 I1." + opName + "(I1 x)", m01.ToTestDisplayString());
            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_11([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // Ignore invalid metadata (non-abstract static virtual method). 

            var opName = BinaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname virtual
        static class I1 " + opName + @" (
            class I1 x,
            int32 y
        ) cil managed 
    {
        IL_0000: ldnull
        IL_0001: ret
    }
}
";

            var source1 =
@"
public class C1 : I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i1 = c1.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal(MethodKind.UserDefinedOperator, m01.MethodKind);
            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i1.FindImplementationForInterfaceMember(m01));

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics();

            var source2 =
@"
public class C1 : I1
{
    static I1 I1.operator " + op + @"(I1 x, int y) => default;
}
";

            var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation2.VerifyEmitDiagnostics(
                // (4,27): error CS0539: 'C1.operator <(I1, int)' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static I1 I1.operator <(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, op).WithArguments("C1.operator " + op + "(I1, int)").WithLocation(4, 27)
                );

            c1 = compilation2.GlobalNamespace.GetTypeMember("C1");
            m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal("I1 I1." + opName + "(I1 x, System.Int32 y)", m01.ToTestDisplayString());
            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_12([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op)
        {
            // Ignore invalid metadata (default interface implementation for a static method)

            var opName = UnaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        class I1 " + opName + @" (
            class I1 x
        ) cil managed 
    {
    }
}
.class interface public auto ansi abstract I2
    implements I1
{
    .method private hidebysig
        static class I1 I1." + opName + @" (class I1 x) cil managed 
    {
        .override method class I1 I1::" + opName + @"(class I1)

        IL_0000: ldnull
        IL_0001: ret
    }
}
";

            var source1 =
@"
public class C1 : I2
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics(
                // (2,19): error CS0535: 'C1' does not implement interface member 'I1.operator ~(I1)'
                // public class C1 : I2
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2").WithArguments("C1", "I1.operator " + op + "(I1)").WithLocation(2, 19)
                );

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i2 = c1.Interfaces().Single();
            var i1 = i2.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal(MethodKind.UserDefinedOperator, m01.MethodKind);
            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01));

            var i2M01 = i2.GetMembers().OfType<MethodSymbol>().Single();
            Assert.Same(m01, i2M01.ExplicitInterfaceImplementations.Single());
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_12([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // Ignore invalid metadata (default interface implementation for a static method)

            var opName = BinaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        class I1 " + opName + @" (
            class I1 x,
            int32 y
        ) cil managed 
    {
    }
}
.class interface public auto ansi abstract I2
    implements I1
{
    .method private hidebysig
        static class I1 I1." + opName + @" (class I1 x, int32 y) cil managed 
    {
        .override method class I1 I1::" + opName + @"(class I1, int32)

        IL_0000: ldnull
        IL_0001: ret
    }
}
";

            var source1 =
@"
public class C1 : I2
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics(
                // (2,19): error CS0535: 'C1' does not implement interface member 'I1.operator /(I1, int)'
                // public class C1 : I2
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2").WithArguments("C1", "I1.operator " + op + "(I1, int)").WithLocation(2, 19)
                );

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i2 = c1.Interfaces().Single();
            var i1 = i2.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<MethodSymbol>().Single();

            Assert.Equal(MethodKind.UserDefinedOperator, m01.MethodKind);
            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01));

            var i2M01 = i2.GetMembers().OfType<MethodSymbol>().Single();
            Assert.Same(m01, i2M01.ExplicitInterfaceImplementations.Single());
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_13([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // A forwarding method is added for an implicit implementation declared in base class. 

            var source1 =
@"
public partial interface I1<T> where T : I1<T>
{
    abstract static T operator " + op + @"(T x, C1 y);
}

#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

public partial class C1
{
    public static C2 operator " + op + @"(C2 x, C1 y) => default;
}

public class C2 : C1, I1<C2>
{
}
";
            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1<T>
{
    abstract static T operator " + matchingOp + @"(T x, C1 y);
}

public partial class C1
{
    public static C2 operator " + matchingOp + @"(C2 x, C1 y) => default;
}
";
            }

            var opName = BinaryOperatorName(op);
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var i1 = c2.Interfaces().Single();
                var m01 = i1.GetMembers(opName).OfType<MethodSymbol>().Single();

                var c2M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01.MethodKind);
                    Assert.False(c2M01.HasRuntimeSpecialName);
                    Assert.False(c2M01.HasSpecialName);

                    Assert.Equal("C2 C2.I1<C2>." + opName + "(C2 x, C1 y)", c2M01.ToTestDisplayString());
                    Assert.Equal(m01, c2M01.ExplicitInterfaceImplementations.Single());

                    var c1M01 = module.GlobalNamespace.GetMember<MethodSymbol>("C1." + opName);

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.False(c1M01.IsMetadataVirtual());
                    Assert.False(c1M01.IsMetadataFinal);
                    Assert.False(c1M01.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.UserDefinedOperator, c1M01.MethodKind);
                    Assert.False(c1M01.HasRuntimeSpecialName);
                    Assert.True(c1M01.HasSpecialName);
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.Equal(MethodKind.UserDefinedOperator, c2M01.MethodKind);
                    Assert.False(c2M01.HasRuntimeSpecialName);
                    Assert.True(c2M01.HasSpecialName);

                    Assert.Equal("C2 C1." + opName + "(C2 x, C1 y)", c2M01.ToTestDisplayString());
                    Assert.Empty(c2M01.ExplicitInterfaceImplementations);
                }
            }

            verifier.VerifyIL("C2.I1<C2>." + opName + "(C2, C1)",
@"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  call       ""C2 C1." + opName + @"(C2, C1)""
  IL_0007:  ret
}
");
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_14([CombinatorialValues("+", "-", "!", "~", "++", "--")] string op)
        {
            // A forwarding method is added for an implicit implementation with modopt mismatch. 

            var opName = UnaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1`1<(class I1`1<!T>) T>
{
    // Methods
    .method public hidebysig specialname abstract virtual static 
        !T modopt(I1`1) " + opName + @" (
            !T x
        ) cil managed 
    {
    }
} 
";

            var source1 =
@"
class C1 : I1<C1>
{
    public static C1 operator " + op + @"(C1 x) => default;
}

class C2 : I1<C2>
{
    static C2 I1<C2>.operator " + op + @"(C2 x) => default;
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c1 = module.GlobalNamespace.GetTypeMember("C1");
                var m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                var c1M01 = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01);

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);
                Assert.False(c1M01.IsMetadataVirtual());
                Assert.False(c1M01.IsMetadataFinal);
                Assert.False(c1M01.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01.MethodKind);
                    Assert.Equal("C1 modopt(I1<>) C1.I1<C1>." + opName + "(C1 x)", c1M01.ToTestDisplayString());
                    Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());

                    c1M01 = module.GlobalNamespace.GetMember<MethodSymbol>("C1." + opName);
                    Assert.Equal("C1 C1." + opName + "(C1 x)", c1M01.ToTestDisplayString());

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.False(c1M01.IsMetadataVirtual());
                    Assert.False(c1M01.IsMetadataFinal);
                    Assert.False(c1M01.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.UserDefinedOperator, c1M01.MethodKind);

                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.Equal(MethodKind.UserDefinedOperator, c1M01.MethodKind);
                    Assert.Equal("C1 C1." + opName + "(C1 x)", c1M01.ToTestDisplayString());
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }

                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();
                var c2M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01.MethodKind);

                Assert.Equal("C2 modopt(I1<>) C2.I1<C2>." + opName + "(C2 x)", c2M01.ToTestDisplayString());
                Assert.Equal(m01, c2M01.ExplicitInterfaceImplementations.Single());

                Assert.Same(c2M01, c2.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Single());
            }

            verifier.VerifyIL("C1.I1<C1>." + opName + "(C1)",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""C1 C1." + opName + @"(C1)""
  IL_0006:  ret
}
");
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryTrueFalseOperator_14([CombinatorialValues("true", "false")] string op)
        {
            // A forwarding method is added for an implicit implementation with modopt mismatch. 

            var opName = UnaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1`1<(class I1`1<!T>) T>
{
    // Methods
    .method public hidebysig specialname abstract virtual static 
        bool modopt(I1`1) " + opName + @" (
            !T x
        ) cil managed 
    {
    }
} 
";

            var source1 =
@"
class C1 : I1<C1>
{
    public static bool operator " + op + @"(C1 x) => default;
    public static bool operator " + (op == "true" ? "false" : "true") + @"(C1 x) => default;
}

class C2 : I1<C2>
{
    static bool I1<C2>.operator " + op + @"(C2 x) => default;
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c1 = module.GlobalNamespace.GetTypeMember("C1");
                var m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                var c1M01 = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01);

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);
                Assert.False(c1M01.IsMetadataVirtual());
                Assert.False(c1M01.IsMetadataFinal);
                Assert.False(c1M01.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01.MethodKind);
                    Assert.Equal("System.Boolean modopt(I1<>) C1.I1<C1>." + opName + "(C1 x)", c1M01.ToTestDisplayString());
                    Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());

                    c1M01 = module.GlobalNamespace.GetMember<MethodSymbol>("C1." + opName);
                    Assert.Equal("System.Boolean C1." + opName + "(C1 x)", c1M01.ToTestDisplayString());

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.False(c1M01.IsMetadataVirtual());
                    Assert.False(c1M01.IsMetadataFinal);
                    Assert.False(c1M01.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.UserDefinedOperator, c1M01.MethodKind);

                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.Equal(MethodKind.UserDefinedOperator, c1M01.MethodKind);
                    Assert.Equal("System.Boolean C1." + opName + "(C1 x)", c1M01.ToTestDisplayString());
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }

                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();
                var c2M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01.MethodKind);

                Assert.Equal("System.Boolean modopt(I1<>) C2.I1<C2>." + opName + "(C2 x)", c2M01.ToTestDisplayString());
                Assert.Equal(m01, c2M01.ExplicitInterfaceImplementations.Single());

                Assert.Same(c2M01, c2.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Single());
            }

            verifier.VerifyIL("C1.I1<C1>." + opName + "(C1)",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""bool C1." + opName + @"(C1)""
  IL_0006:  ret
}
");
        }

        private static string MatchingBinaryOperator(string op)
        {
            return op switch { "<" => ">", ">" => "<", "<=" => ">=", ">=" => "<=", "==" => "!=", "!=" => "==", _ => null };
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_14([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // A forwarding method is added for an implicit implementation with modopt mismatch. 

            var opName = BinaryOperatorName(op);

            var ilSource = @"
.class interface public auto ansi abstract I1`1<(class I1`1<!T>) T>
{
    // Methods
    .method public hidebysig specialname abstract virtual static 
        !T modopt(I1`1) " + opName + @" (
            !T x,
            int32 y
        ) cil managed 
    {
    }
} 
";
            string matchingOp = MatchingBinaryOperator(op);
            string additionalMethods = "";

            if (matchingOp is object)
            {
                additionalMethods =
@"
    public static C1 operator " + matchingOp + @"(C1 x, int y) => default;
";
            }

            var source1 =
@"
#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

class C1 : I1<C1>
{
    public static C1 operator " + op + @"(C1 x, int y) => default;
" + additionalMethods + @"
}

class C2 : I1<C2>
{
    static C2 I1<C2>.operator " + op + @"(C2 x, int y) => default;
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c1 = module.GlobalNamespace.GetTypeMember("C1");
                var m01 = c1.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();

                var c1M01 = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01);

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);
                Assert.False(c1M01.IsMetadataVirtual());
                Assert.False(c1M01.IsMetadataFinal);
                Assert.False(c1M01.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01.MethodKind);
                    Assert.Equal("C1 modopt(I1<>) C1.I1<C1>." + opName + "(C1 x, System.Int32 y)", c1M01.ToTestDisplayString());
                    Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());

                    c1M01 = module.GlobalNamespace.GetMember<MethodSymbol>("C1." + opName);
                    Assert.Equal("C1 C1." + opName + "(C1 x, System.Int32 y)", c1M01.ToTestDisplayString());

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.False(c1M01.IsMetadataVirtual());
                    Assert.False(c1M01.IsMetadataFinal);
                    Assert.False(c1M01.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.UserDefinedOperator, c1M01.MethodKind);

                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.Equal(MethodKind.UserDefinedOperator, c1M01.MethodKind);
                    Assert.Equal("C1 C1." + opName + "(C1 x, System.Int32 y)", c1M01.ToTestDisplayString());
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                m01 = c2.Interfaces().Single().GetMembers().OfType<MethodSymbol>().Single();
                var c2M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());
                Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01.MethodKind);

                Assert.Equal("C2 modopt(I1<>) C2.I1<C2>." + opName + "(C2 x, System.Int32 y)", c2M01.ToTestDisplayString());
                Assert.Equal(m01, c2M01.ExplicitInterfaceImplementations.Single());

                Assert.Same(c2M01, c2.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Single());
            }

            verifier.VerifyIL("C1.I1<C1>." + opName + "(C1, int)",
@"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  call       ""C1 C1." + opName + @"(C1, int)""
  IL_0007:  ret
}
");
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticUnaryOperator_15([CombinatorialValues("+", "-", "!", "~", "++", "--")] string op)
        {
            // A forwarding method isn't created if base class implements interface exactly the same way. 

            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x);
}

public partial class C2 : I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            var opName = UnaryOperatorName(op);

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.RegularPreview, TestOptions.Regular9 })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));

                var m02 = c3.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().Single();

                var c2M02 = c3.BaseType().GetMembers("I1." + opName).OfType<MethodSymbol>().Single();
                Assert.Equal("I1 C2.I1." + opName + "(I1 x)", c2M02.ToTestDisplayString());
                Assert.Same(c2M02, c3.FindImplementationForInterfaceMember(m02));
            }
        }

        [Fact]
        public void ImplementAbstractStaticUnaryTrueFalseOperator_15()
        {
            // A forwarding method isn't created if base class implements interface exactly the same way. 

            var source1 =
@"
public interface I1
{
    abstract static bool operator true(I1 x);
    abstract static bool operator false(I1 x);
}

public partial class C2 : I1
{
    static bool I1.operator true(I1 x) => default;
    static bool I1.operator false(I1 x) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";


            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.RegularPreview, TestOptions.Regular9 })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));

                var m01 = c3.Interfaces().Single().GetMembers("op_True").OfType<MethodSymbol>().Single();
                var m02 = c3.Interfaces().Single().GetMembers("op_False").OfType<MethodSymbol>().Single();

                var c2M01 = c3.BaseType().GetMembers("I1.op_True").OfType<MethodSymbol>().Single();
                Assert.Equal("System.Boolean C2.I1.op_True(I1 x)", c2M01.ToTestDisplayString());
                Assert.Same(c2M01, c3.FindImplementationForInterfaceMember(m01));

                var c2M02 = c3.BaseType().GetMembers("I1.op_False").OfType<MethodSymbol>().Single();
                Assert.Equal("System.Boolean C2.I1.op_False(I1 x)", c2M02.ToTestDisplayString());
                Assert.Same(c2M02, c3.FindImplementationForInterfaceMember(m02));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_15([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // A forwarding method isn't created if base class implements interface exactly the same way. 

            var source1 =
@"
public partial interface I1<T> where T : I1<T>
{
    abstract static T operator " + op + @"(T x, C1 y);
    abstract static T operator " + op + @"(T x, C2 y);
}

#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

public partial class C1
{
    public static C2 operator " + op + @"(C2 x, C1 y) => default;
}

public partial class C2 : C1, I1<C2>
{
    static C2 I1<C2>.operator " + op + @"(C2 x, C2 y) => default;
}
";

            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1<T>
{
    abstract static T operator " + matchingOp + @"(T x, C1 y);
    abstract static T operator " + matchingOp + @"(T x, C2 y);
}

public partial class C1
{
    public static C2 operator " + matchingOp + @"(C2 x, C1 y) => default;
}

public partial class C2
{
    static C2 I1<C2>.operator " + matchingOp + @"(C2 x, C2 y) => default;
}
";
            }

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1<C2>
{
}
";

            var opName = BinaryOperatorName(op);

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.RegularPreview, TestOptions.Regular9 })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));

                var m01 = c3.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().First();

                var c1M01 = c3.BaseType().BaseType().GetMember<MethodSymbol>(opName);
                Assert.Equal("C2 C1." + opName + "(C2 x, C1 y)", c1M01.ToTestDisplayString());

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);
                Assert.False(c1M01.IsMetadataVirtual());
                Assert.False(c1M01.IsMetadataFinal);
                Assert.False(c1M01.IsMetadataNewSlot());

                Assert.Empty(c1M01.ExplicitInterfaceImplementations);

                if (c1M01.ContainingModule is PEModuleSymbol)
                {
                    var c2M01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);
                    Assert.Equal("C2 C2.I1<C2>." + opName + "(C2 x, C1 y)", c2M01.ToTestDisplayString());
                    Assert.Equal(m01, c2M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));
                }

                var m02 = c3.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().ElementAt(1);

                var c2M02 = c3.BaseType().GetMembers("I1<C2>." + opName).OfType<MethodSymbol>().First();
                Assert.Equal("C2 C2.I1<C2>." + opName + "(C2 x, C2 y)", c2M02.ToTestDisplayString());
                Assert.Same(c2M02, c3.FindImplementationForInterfaceMember(m02));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_16([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            // A new implicit implementation is properly considered.

            var source1 =
@"
public partial interface I1<T> where T : I1<T>
{
    abstract static T operator " + op + @"(T x, C1 y);
}

#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

public partial class C1 : I1<C2>
{
    public static C2 operator " + op + @"(C2 x, C1 y) => default;
}

public partial class C2 : C1
{
    public static C2 operator " + op + @"(C2 x, C1 y) => default;
}
";
            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1<T>
{
    abstract static T operator " + matchingOp + @"(T x, C1 y);
}

public partial class C1 : I1<C2>
{
    public static C2 operator " + matchingOp + @"(C2 x, C1 y) => default;
}

public partial class C2 : C1
{
    public static C2 operator " + matchingOp + @"(C2 x, C1 y) => default;
}
";
            }

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1<C2>
{
}
";

            var opName = BinaryOperatorName(op);

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                     parseOptions: parseOptions,
                                                     targetFramework: TargetFramework.NetCoreApp,
                                                     references: new[] { reference });
                    var verifier = CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

                    verifier.VerifyIL("C3.I1<C2>." + opName + "(C2, C1)",
@"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  call       ""C2 C2." + opName + @"(C2, C1)""
  IL_0007:  ret
}
");
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                var m01 = c3.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().Single();

                var c2M01 = c3.BaseType().GetMember<MethodSymbol>(opName);
                Assert.Equal("C2 C2." + opName + "(C2 x, C1 y)", c2M01.ToTestDisplayString());

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.False(c2M01.IsMetadataVirtual());
                Assert.False(c2M01.IsMetadataFinal);
                Assert.False(c2M01.IsMetadataNewSlot());

                Assert.Empty(c2M01.ExplicitInterfaceImplementations);

                if (module is PEModuleSymbol)
                {
                    var c3M01 = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01);
                    Assert.Equal("C2 C3.I1<C2>." + opName + "(C2 x, C1 y)", c3M01.ToTestDisplayString());
                    Assert.Equal(m01, c3M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Same(c2M01, c3.FindImplementationForInterfaceMember(m01));
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_18([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<", ">", "<=", ">=", "==", "!=")] string op, bool genericFirst)
        {
            // An "ambiguity" in implicit implementation declared in generic base class plus interface is generic too.

            var generic =
@"
    public static C1<T, U> operator " + op + @"(C1<T, U> x, U y) => default;
";
            var nonGeneric =
@"
    public static C1<T, U> operator " + op + @"(C1<T, U> x, int y) => default;
";
            var source1 =
@"
public partial interface I1<T, U> where T : I1<T, U>
{
    abstract static T operator " + op + @"(T x, U y);
}

#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

public partial class C1<T, U> : I1<C1<T, U>, U>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1<T, U>
{
    abstract static T operator " + matchingOp + @"(T x, U y);
}

public partial class C1<T, U>
{
    public static C1<T, U> operator " + matchingOp + @"(C1<T, U> x, U y) => default;
    public static C1<T, U> operator " + matchingOp + @"(C1<T, U> x, int y) => default;
}
";
            }

            var opName = BinaryOperatorName(op);

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            compilation1.VerifyDiagnostics();
            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains(opName)).Count());

            var source2 =
@"
public class C2 : C1<int, int>, I1<C1<int, int>, int>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("C1<T, U> C1<T, U>." + opName + "(C1<T, U> x, U y)", c1M01.OriginalDefinition.ToTestDisplayString());

                var baseI1M01 = c2.BaseType().FindImplementationForInterfaceMember(m01);
                Assert.Equal("C1<T, U> C1<T, U>." + opName + "(C1<T, U> x, U y)", baseI1M01.OriginalDefinition.ToTestDisplayString());

                Assert.Equal(c1M01, baseI1M01);

                if (c1M01.OriginalDefinition.ContainingModule is PEModuleSymbol)
                {
                    Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_20([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<", ">", "<=", ">=", "==", "!=")] string op, bool genericFirst)
        {
            // Same as ImplementAbstractStaticBinaryOperator_18 only implementation is explicit in source.

            var generic =
@"
    static C1<T, U> I1<C1<T, U>, U>.operator " + op + @"(C1<T, U> x, U y) => default;
";
            var nonGeneric =
@"
    public static C1<T, U> operator " + op + @"(C1<T, U> x, int y) => default;
";
            var source1 =
@"
public partial interface I1<T, U> where T : I1<T, U>
{
    abstract static T operator " + op + @"(T x, U y);
}

#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

public partial class C1<T, U> : I1<C1<T, U>, U>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";
            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1<T, U> where T : I1<T, U>
{
    abstract static T operator " + matchingOp + @"(T x, U y);
}

public partial class C1<T, U> : I1<C1<T, U>, U>
{
    public static C1<T, U> operator " + matchingOp + @"(C1<T, U> x, int y) => default;
    static C1<T, U> I1<C1<T, U>, U>.operator " + matchingOp + @"(C1<T, U> x, U y) => default;
}
";
            }

            var opName = BinaryOperatorName(op);

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            compilation1.VerifyDiagnostics();
            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains(opName)).Count());

            var source2 =
@"
public class C2 : C1<int, int>, I1<C1<int, int>, int>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("C1<T, U> C1<T, U>.I1<C1<T, U>, U>." + opName + "(C1<T, U> x, U y)", c1M01.OriginalDefinition.ToTestDisplayString());

                Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(c1M01, c2.BaseType().FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticBinaryOperator_22([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<", ">", "<=", ">=", "==", "!=")] string op, bool genericFirst)
        {
            // Same as ImplementAbstractStaticMethod_18 only implicit implementation is in an intermediate base.

            var generic =
@"
    public static C11<T, U> operator " + op + @"(C11<T, U> x, C1<T, U> y) => default;
";
            var nonGeneric =
@"
    public static C11<T, U> operator " + op + @"(C11<T, int> x, C1<T, U> y) => default;
";
            var source1 =
@"
public partial interface I1<T, U> where T : I1<T, U>
{
    abstract static T operator " + op + @"(T x, U y);
}

#pragma warning disable CS0660, CS0661 // 'C1' defines operator == or operator != but does not override Object.Equals(object o)/Object.GetHashCode()

public partial class C1<T, U>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}

public class C11<T, U> : C1<T, U>, I1<C11<T, U>, C1<T, U>>
{
}
";
            string matchingOp = MatchingBinaryOperator(op);

            if (matchingOp is object)
            {
                source1 +=
@"
public partial interface I1<T, U>
{
    abstract static T operator " + matchingOp + @"(T x, U y);
}

public partial class C1<T, U>
{
    public static C11<T, U> operator " + matchingOp + @"(C11<T, U> x, C1<T, U> y) => default;
    public static C11<T, U> operator " + matchingOp + @"(C11<T, int> x, C1<T, U> y) => default;
}
";
            }

            var opName = BinaryOperatorName(op);

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            compilation1.VerifyDiagnostics();
            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().Where(m => m.Name.Contains(opName)).Count());

            var source2 =
@"
public class C2 : C11<int, int>, I1<C11<int, int>, C1<int, int>>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers(opName).OfType<MethodSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01);
                var expectedDisplay = m01.ContainingModule is PEModuleSymbol ? "C11<T, U> C11<T, U>.I1<C11<T, U>, C1<T, U>>." + opName + "(C11<T, U> x, C1<T, U> y)" : "C11<T, U> C1<T, U>." + opName + "(C11<T, U> x, C1<T, U> y)";
                Assert.Equal(expectedDisplay, c1M01.OriginalDefinition.ToTestDisplayString());

                var baseI1M01 = c2.BaseType().FindImplementationForInterfaceMember(m01);
                Assert.Equal(expectedDisplay, baseI1M01.OriginalDefinition.ToTestDisplayString());

                Assert.Equal(c1M01, baseI1M01);

                if (c1M01.OriginalDefinition.ContainingModule is PEModuleSymbol)
                {
                    Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ExplicitImplementationModifiersUnaryOperator_01([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op)
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x);
}

class
    C1 : I1
{
    static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C2 : I1
{
    private static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C3 : I1
{
    protected static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C4 : I1
{
    internal static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C5 : I1
{
    protected internal static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C6 : I1
{
    private protected static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C7 : I1
{
    public static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C8 : I1
{
    static partial I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C9 : I1
{
    async static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C10 : I1
{
    unsafe static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C11 : I1
{
    static readonly I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C12 : I1
{
    extern static I1 I1.operator " + op + @"(I1 x);
}

class
    C13 : I1
{
    abstract static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C14 : I1
{
    virtual static I1 I1.operator " + op + @"(I1 x) => default;
}

class
    C15 : I1
{
    sealed static I1 I1.operator " + op + @"(I1 x) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");

            Assert.Equal(Accessibility.Private, c1.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Single().DeclaredAccessibility);

            compilation1.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.WRN_ExternMethodNoImplementation or (int)ErrorCode.ERR_OpTFRetType or (int)ErrorCode.ERR_OperatorNeedsMatch)).Verify(
                // (16,35): error CS0106: The modifier 'private' is not valid for this item
                //     private static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("private").WithLocation(16, 35),
                // (22,37): error CS0106: The modifier 'protected' is not valid for this item
                //     protected static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("protected").WithLocation(22, 37),
                // (28,36): error CS0106: The modifier 'internal' is not valid for this item
                //     internal static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("internal").WithLocation(28, 36),
                // (34,46): error CS0106: The modifier 'protected internal' is not valid for this item
                //     protected internal static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("protected internal").WithLocation(34, 46),
                // (40,45): error CS0106: The modifier 'private protected' is not valid for this item
                //     private protected static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("private protected").WithLocation(40, 45),
                // (46,34): error CS0106: The modifier 'public' is not valid for this item
                //     public static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("public").WithLocation(46, 34),
                // (52,12): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     static partial I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(52, 12),
                // (58,33): error CS0106: The modifier 'async' is not valid for this item
                //     async static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("async").WithLocation(58, 33),
                // (70,36): error CS0106: The modifier 'readonly' is not valid for this item
                //     static readonly I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("readonly").WithLocation(70, 36),
                // (82,36): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("abstract").WithLocation(82, 36),
                // (88,35): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("virtual").WithLocation(88, 35),
                // (94,34): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static I1 I1.operator !(I1 x) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("sealed").WithLocation(94, 34)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ExplicitImplementationModifiersBinaryOperator_01([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            var source1 =
@"
public interface I1
{
    abstract static I1 operator " + op + @"(I1 x, int y);
}

struct
    C1 : I1
{
    static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C2 : I1
{
    private static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C3 : I1
{
    protected static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C4 : I1
{
    internal static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C5 : I1
{
    protected internal static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C6 : I1
{
    private protected static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C7 : I1
{
    public static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C8 : I1
{
    static partial I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C9 : I1
{
    async static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C10 : I1
{
    unsafe static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C11 : I1
{
    static readonly I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C12 : I1
{
    extern static I1 I1.operator " + op + @"(I1 x, int y);
}

struct
    C13 : I1
{
    abstract static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C14 : I1
{
    virtual static I1 I1.operator " + op + @"(I1 x, int y) => default;
}

struct
    C15 : I1
{
    sealed static I1 I1.operator " + op + @"(I1 x, int y) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");

            Assert.Equal(Accessibility.Private, c1.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Single().DeclaredAccessibility);

            compilation1.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.WRN_ExternMethodNoImplementation or (int)ErrorCode.ERR_OperatorNeedsMatch)).Verify(
                // (16,35): error CS0106: The modifier 'private' is not valid for this item
                //     private static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("private").WithLocation(16, 35),
                // (22,37): error CS0106: The modifier 'protected' is not valid for this item
                //     protected static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("protected").WithLocation(22, 37),
                // (28,36): error CS0106: The modifier 'internal' is not valid for this item
                //     internal static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("internal").WithLocation(28, 36),
                // (34,46): error CS0106: The modifier 'protected internal' is not valid for this item
                //     protected internal static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("protected internal").WithLocation(34, 46),
                // (40,45): error CS0106: The modifier 'private protected' is not valid for this item
                //     private protected static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("private protected").WithLocation(40, 45),
                // (46,34): error CS0106: The modifier 'public' is not valid for this item
                //     public static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("public").WithLocation(46, 34),
                // (52,12): error CS0267: The 'partial' modifier can only appear immediately before 'class', 'record', 'struct', 'interface', or a method return type.
                //     static partial I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_PartialMisplaced, "partial").WithLocation(52, 12),
                // (58,33): error CS0106: The modifier 'async' is not valid for this item
                //     async static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("async").WithLocation(58, 33),
                // (70,36): error CS0106: The modifier 'readonly' is not valid for this item
                //     static readonly I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("readonly").WithLocation(70, 36),
                // (82,36): error CS0106: The modifier 'abstract' is not valid for this item
                //     abstract static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("abstract").WithLocation(82, 36),
                // (88,35): error CS0106: The modifier 'virtual' is not valid for this item
                //     virtual static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("virtual").WithLocation(88, 35),
                // (94,34): error CS0106: The modifier 'sealed' is not valid for this item
                //     sealed static I1 I1.operator ^(I1 x, int y) => default;
                Diagnostic(ErrorCode.ERR_BadMemberFlag, op).WithArguments("sealed").WithLocation(94, 34)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ExplicitInterfaceSpecifierErrorsUnaryOperator_01([CombinatorialValues("+", "-", "!", "~", "++", "--", "true", "false")] string op)
        {
            var source1 =
@"
public interface I1<T> where T : struct
{
    abstract static I1<T> operator " + op + @"(I1<T> x);
}

class C1
{
    static I1<int> I1<int>.operator " + op + @"(I1<int> x) => default;
}

class C2 : I1<C2>
{
    static I1<C2> I1<C2>.operator " + op + @"(I1<C2> x) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.GetDiagnostics().Where(d => d.Code is not ((int)ErrorCode.ERR_OpTFRetType or (int)ErrorCode.ERR_OperatorNeedsMatch)).Verify(
                // (9,20): error CS0540: 'C1.I1<int>.operator -(I1<int>)': containing type does not implement interface 'I1<int>'
                //     static I1<int> I1<int>.operator -(I1<int> x) => default;
                Diagnostic(ErrorCode.ERR_ClassDoesntImplementInterface, "I1<int>").WithArguments("C1.I1<int>.operator " + op + "(I1<int>)", "I1<int>").WithLocation(9, 20),
                // (12,7): error CS0453: The type 'C2' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                // class C2 : I1<C2>
                Diagnostic(ErrorCode.ERR_ValConstraintNotSatisfied, "C2").WithArguments("I1<T>", "T", "C2").WithLocation(12, 7),
                // (14,19): error CS0453: The type 'C2' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                //     static I1<C2> I1<C2>.operator -(I1<C2> x) => default;
                Diagnostic(ErrorCode.ERR_ValConstraintNotSatisfied, "I1<C2>").WithArguments("I1<T>", "T", "C2").WithLocation(14, 19),
                // (14,35): error CS0453: The type 'C2' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                //     static I1<C2> I1<C2>.operator -(I1<C2> x) => default;
                Diagnostic(ErrorCode.ERR_ValConstraintNotSatisfied, op).WithArguments("I1<T>", "T", "C2").WithLocation(14, 35),
                // (14,44): error CS0453: The type 'C2' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                //     static I1<C2> I1<C2>.operator -(I1<C2> x) => default;
                Diagnostic(ErrorCode.ERR_ValConstraintNotSatisfied, "x").WithArguments("I1<T>", "T", "C2").WithLocation(14, 44 + op.Length - 1)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ExplicitInterfaceSpecifierErrorsBinaryOperator_01([CombinatorialValues("+", "-", "*", "/", "%", "&", "|", "^", "<<", ">>", "<", ">", "<=", ">=", "==", "!=")] string op)
        {
            var source1 =
@"
public interface I1<T> where T : class
{
    abstract static I1<T> operator " + op + @"(I1<T> x, int y);
}

struct C1
{
    static I1<string> I1<string>.operator " + op + @"(I1<string> x, int y) => default;
}

struct C2 : I1<C2>
{
    static I1<C2> I1<C2>.operator " + op + @"(I1<C2> x, int y) => default;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll.WithAllowUnsafe(true),
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.GetDiagnostics().Where(d => d.Code is not (int)ErrorCode.ERR_OperatorNeedsMatch).Verify(
                // (9,23): error CS0540: 'C1.I1<string>.operator %(I1<string>, int)': containing type does not implement interface 'I1<string>'
                //     static I1<string> I1<string>.operator %(I1<string> x, int y) => default;
                Diagnostic(ErrorCode.ERR_ClassDoesntImplementInterface, "I1<string>").WithArguments("C1.I1<string>.operator " + op + "(I1<string>, int)", "I1<string>").WithLocation(9, 23),
                // (12,8): error CS0452: The type 'C2' must be a reference type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                // struct C2 : I1<C2>
                Diagnostic(ErrorCode.ERR_RefConstraintNotSatisfied, "C2").WithArguments("I1<T>", "T", "C2").WithLocation(12, 8),
                // (14,19): error CS0452: The type 'C2' must be a reference type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                //     static I1<C2> I1<C2>.operator %(I1<C2> x, int y) => default;
                Diagnostic(ErrorCode.ERR_RefConstraintNotSatisfied, "I1<C2>").WithArguments("I1<T>", "T", "C2").WithLocation(14, 19),
                // (14,35): error CS0452: The type 'C2' must be a reference type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                //     static I1<C2> I1<C2>.operator %(I1<C2> x, int y) => default;
                Diagnostic(ErrorCode.ERR_RefConstraintNotSatisfied, op).WithArguments("I1<T>", "T", "C2").WithLocation(14, 35),
                // (14,44): error CS0452: The type 'C2' must be a reference type in order to use it as parameter 'T' in the generic type or method 'I1<T>'
                //     static I1<C2> I1<C2>.operator %(I1<C2> x, int y) => default;
                Diagnostic(ErrorCode.ERR_RefConstraintNotSatisfied, "x").WithArguments("I1<T>", "T", "C2").WithLocation(14, 44 + op.Length - 1)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_01(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

" + typeKeyword + @"
    C1 : I1
{}

" + typeKeyword + @"
    C2 : I1
{
    public int M01 { get; set; }
}

" + typeKeyword + @"
    C3 : I1
{
    static int M01 { get; set; }
}

" + typeKeyword + @"
    C4 : I1
{
    int I1.M01 { get; set; }
}

" + typeKeyword + @"
    C5 : I1
{
    public static long M01 { get; set; }
}

" + typeKeyword + @"
    C6 : I1
{
    static long I1.M01 { get; set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1.M01'
                //     C1 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(8, 10),
                // (12,10): error CS9109: 'C2' does not implement static interface member 'I1.M01'. 'C2.M01' cannot implement the interface member because it is not static.
                //     C2 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotStatic, "I1").WithArguments("C2", "I1.M01", "C2.M01").WithLocation(12, 10),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1.M01'. 'C3.M01' cannot implement an interface member because it is not public.
                //     C3 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1").WithArguments("C3", "I1.M01", "C3.M01").WithLocation(18, 10),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1.M01'
                //     C4 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C4", "I1.M01").WithLocation(24, 10),
                // (26,12): error CS0539: 'C4.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C4.M01").WithLocation(26, 12),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1.M01'. 'C5.M01' cannot implement 'I1.M01' because it does not have the matching return type of 'int'.
                //     C5 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1").WithArguments("C5", "I1.M01", "C5.M01", "int").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1.M01'
                //     C6 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C6", "I1.M01").WithLocation(36, 10),
                // (38,20): error CS0539: 'C6.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static long I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C6.M01").WithLocation(38, 20)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_02(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract int M01 { get; set; }
}

" + typeKeyword + @"
    C1 : I1
{}

" + typeKeyword + @"
    C2 : I1
{
    public static int M01 { get; set; }
}

" + typeKeyword + @"
    C3 : I1
{
    int M01 { get; set; }
}

" + typeKeyword + @"
    C4 : I1
{
    static int I1.M01 { get; set; }
}

" + typeKeyword + @"
    C5 : I1
{
    public long M01 { get; set; }
}

" + typeKeyword + @"
    C6 : I1
{
    long I1.M01 { get; set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1.M01'
                //     C1 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(8, 10),
                // (12,10): error CS0736: 'C2' does not implement instance interface member 'I1.M01'. 'C2.M01' cannot implement the interface member because it is static.
                //     C2 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberStatic, "I1").WithArguments("C2", "I1.M01", "C2.M01").WithLocation(12, 10),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1.M01'. 'C3.M01' cannot implement an interface member because it is not public.
                //     C3 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1").WithArguments("C3", "I1.M01", "C3.M01").WithLocation(18, 10),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1.M01'
                //     C4 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C4", "I1.M01").WithLocation(24, 10),
                // (26,19): error CS0539: 'C4.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C4.M01").WithLocation(26, 19),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1.M01'. 'C5.M01' cannot implement 'I1.M01' because it does not have the matching return type of 'int'.
                //     C5 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1").WithArguments("C5", "I1.M01", "C5.M01", "int").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1.M01'
                //     C6 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C6", "I1.M01").WithLocation(36, 10),
                // (38,13): error CS0539: 'C6.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     long I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C6.M01").WithLocation(38, 13)
                );
        }

        [Fact]
        public void ImplementAbstractStaticProperty_03()
        {
            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

interface I2 : I1
{}

interface I3 : I1
{
    public virtual int M01 { get => 0; set{} }
}

interface I4 : I1
{
    static int M01 { get; set; }
}

interface I5 : I1
{
    int I1.M01 { get => 0; set{} }
}

interface I6 : I1
{
    static int I1.M01 { get => 0; set{} }
}

interface I7 : I1
{
    abstract static int M01 { get; set; }
}

interface I8 : I1
{
    abstract static int I1.M01 { get; set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (12,24): warning CS0108: 'I3.M01' hides inherited member 'I1.M01'. Use the new keyword if hiding was intended.
                //     public virtual int M01 { get => 0; set{} }
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I3.M01", "I1.M01").WithLocation(12, 24),
                // (17,16): warning CS0108: 'I4.M01' hides inherited member 'I1.M01'. Use the new keyword if hiding was intended.
                //     static int M01 { get; set; }
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I4.M01", "I1.M01").WithLocation(17, 16),
                // (22,12): error CS0539: 'I5.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     int I1.M01 { get => 0; set{} }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I5.M01").WithLocation(22, 12),
                // (27,19): error CS0106: The modifier 'static' is not valid for this item
                //     static int I1.M01 { get => 0; set{} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M01").WithArguments("static").WithLocation(27, 19),
                // (27,19): error CS0539: 'I6.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static int I1.M01 { get => 0; set{} }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I6.M01").WithLocation(27, 19),
                // (32,25): warning CS0108: 'I7.M01' hides inherited member 'I1.M01'. Use the new keyword if hiding was intended.
                //     abstract static int M01 { get; set; }
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I7.M01", "I1.M01").WithLocation(32, 25),
                // (37,28): error CS0106: The modifier 'static' is not valid for this item
                //     abstract static int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M01").WithArguments("static").WithLocation(37, 28),
                // (37,28): error CS0539: 'I8.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     abstract static int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I8.M01").WithLocation(37, 28)
                );

            foreach (var m01 in compilation1.GlobalNamespace.GetTypeMember("I1").GetMembers())
            {
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I2").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I3").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I4").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I5").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I6").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I7").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I8").FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_04(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
    abstract static int M02 { get; set; }
}
";
            var source2 =
typeKeyword + @"
    Test: I1
{
    static int I1.M01 { get; set; }
    public static int M02 { get; set; }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,19): error CS8703: The modifier 'static' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     static int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("static", "9.0", "preview").WithLocation(4, 19)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (4,19): error CS8703: The modifier 'static' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     static int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("static", "9.0", "preview").WithLocation(4, 19),
                // (10,25): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static int M01 { get; set; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(10, 25),
                // (11,25): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static int M02 { get; set; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M02").WithArguments("abstract", "9.0", "preview").WithLocation(11, 25)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_05(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    public static int M01 { get; set; }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (2,12): error CS9110: 'Test1.M01.set' cannot implement interface member 'I1.M01.set' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.set", "I1.M01.set", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01.get' cannot implement interface member 'I1.M01.get' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.get", "I1.M01.get", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01' cannot implement interface member 'I1.M01' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01", "I1.M01", "Test1").WithLocation(2, 12)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (2,12): error CS9110: 'Test1.M01.set' cannot implement interface member 'I1.M01.set' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.set", "I1.M01.set", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01.get' cannot implement interface member 'I1.M01.get' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.get", "I1.M01.get", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01' cannot implement interface member 'I1.M01' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01", "I1.M01", "Test1").WithLocation(2, 12),
                // (9,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int M01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "get").WithLocation(9, 31),
                // (9,36): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int M01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "set").WithLocation(9, 36)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_06(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    static int I1.M01 { get; set; }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,19): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(4, 19)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (4,19): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static int I1.M01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(4, 19),
                // (9,31): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int M01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "get").WithLocation(9, 31),
                // (9,36): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static int M01 { get; set; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "set").WithLocation(9, 36)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_07(bool structure)
        {
            // Basic implicit implementation scenario, MethodImpl is emitted

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

" + typeKeyword + @"
    C : I1
{
    public static int M01 { get => 0; set {} }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<PropertySymbol>().Single();
                var m01Get = m01.GetMethod;
                var m01Set = m01.SetMethod;
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);

                Assert.Equal("System.Int32 C.M01 { get; set; }", cM01.ToTestDisplayString());

                var cM01Get = cM01.GetMethod;
                Assert.Same(cM01Get, c.FindImplementationForInterfaceMember(m01Get));

                Assert.True(cM01Get.IsStatic);
                Assert.False(cM01Get.IsAbstract);
                Assert.False(cM01Get.IsVirtual);
                Assert.False(cM01Get.IsMetadataVirtual());
                Assert.False(cM01Get.IsMetadataFinal);
                Assert.False(cM01Get.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertyGet, cM01Get.MethodKind);
                Assert.False(cM01Get.HasRuntimeSpecialName);
                Assert.True(cM01Get.HasSpecialName);

                Assert.Equal("System.Int32 C.M01.get", cM01Get.ToTestDisplayString());

                var cM01Set = cM01.SetMethod;
                Assert.Same(cM01Set, c.FindImplementationForInterfaceMember(m01Set));

                Assert.True(cM01Set.IsStatic);
                Assert.False(cM01Set.IsAbstract);
                Assert.False(cM01Set.IsVirtual);
                Assert.False(cM01Set.IsMetadataVirtual());
                Assert.False(cM01Set.IsMetadataFinal);
                Assert.False(cM01Set.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertySet, cM01Set.MethodKind);
                Assert.False(cM01Set.HasRuntimeSpecialName);
                Assert.True(cM01Set.HasSpecialName);

                Assert.Equal("void C.M01.set", cM01Set.ToTestDisplayString());

                if (module is PEModuleSymbol)
                {
                    Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                    Assert.Same(m01Get, cM01Get.ExplicitInterfaceImplementations.Single());
                    Assert.Same(m01Set, cM01Set.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                    Assert.Empty(cM01Get.ExplicitInterfaceImplementations);
                    Assert.Empty(cM01Set.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticReadonlyProperty_07(bool structure)
        {
            // Basic implicit implementation scenario, MethodImpl is emitted

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; }
}

" + typeKeyword + @"
    C : I1
{
    public static int M01 { get; set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<PropertySymbol>().Single();
                var m01Get = m01.GetMethod;
                Assert.Null(m01.SetMethod);

                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);

                Assert.Equal("System.Int32 C.M01 { get; set; }", cM01.ToTestDisplayString());

                var cM01Get = cM01.GetMethod;
                Assert.Same(cM01Get, c.FindImplementationForInterfaceMember(m01Get));

                Assert.True(cM01Get.IsStatic);
                Assert.False(cM01Get.IsAbstract);
                Assert.False(cM01Get.IsVirtual);
                Assert.False(cM01Get.IsMetadataVirtual());
                Assert.False(cM01Get.IsMetadataFinal);
                Assert.False(cM01Get.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertyGet, cM01Get.MethodKind);

                Assert.Equal("System.Int32 C.M01.get", cM01Get.ToTestDisplayString());

                var cM01Set = cM01.SetMethod;

                Assert.True(cM01Set.IsStatic);
                Assert.False(cM01Set.IsAbstract);
                Assert.False(cM01Set.IsVirtual);
                Assert.False(cM01Set.IsMetadataVirtual());
                Assert.False(cM01Set.IsMetadataFinal);
                Assert.False(cM01Set.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertySet, cM01Set.MethodKind);

                Assert.Equal("void C.M01.set", cM01Set.ToTestDisplayString());

                if (module is PEModuleSymbol)
                {
                    Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                    Assert.Same(m01Get, cM01Get.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                    Assert.Empty(cM01Get.ExplicitInterfaceImplementations);
                }

                Assert.Empty(cM01Set.ExplicitInterfaceImplementations);
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_08(bool structure)
        {
            // Basic explicit implementation scenario

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

" + typeKeyword + @"
    C : I1
{
    static int I1.M01 { get => 0; set {} }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<PropertySymbol>().Single();
                var m01Get = m01.GetMethod;
                var m01Set = m01.SetMethod;
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);

                Assert.Equal("System.Int32 C.I1.M01 { get; set; }", cM01.ToTestDisplayString());

                var cM01Get = cM01.GetMethod;
                Assert.Same(cM01Get, c.FindImplementationForInterfaceMember(m01Get));

                Assert.True(cM01Get.IsStatic);
                Assert.False(cM01Get.IsAbstract);
                Assert.False(cM01Get.IsVirtual);
                Assert.False(cM01Get.IsMetadataVirtual());
                Assert.False(cM01Get.IsMetadataFinal);
                Assert.False(cM01Get.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertyGet, cM01Get.MethodKind);
                Assert.False(cM01Get.HasRuntimeSpecialName);
                Assert.True(cM01Get.HasSpecialName);

                Assert.Equal("System.Int32 C.I1.M01.get", cM01Get.ToTestDisplayString());

                var cM01Set = cM01.SetMethod;
                Assert.Same(cM01Set, c.FindImplementationForInterfaceMember(m01Set));

                Assert.True(cM01Set.IsStatic);
                Assert.False(cM01Set.IsAbstract);
                Assert.False(cM01Set.IsVirtual);
                Assert.False(cM01Set.IsMetadataVirtual());
                Assert.False(cM01Set.IsMetadataFinal);
                Assert.False(cM01Set.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertySet, cM01Set.MethodKind);
                Assert.False(cM01Set.HasRuntimeSpecialName);
                Assert.True(cM01Set.HasSpecialName);

                Assert.Equal("void C.I1.M01.set", cM01Set.ToTestDisplayString());

                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01Get, cM01Get.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01Set, cM01Set.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticProperty_09()
        {
            // Explicit implementation from base is treated as an implementation

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

public class C1
{
    public static void M01() {}
}

public class C2 : C1, I1
{
    static int I1.M01 { get; set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                     parseOptions: parseOptions,
                                                     targetFramework: TargetFramework.NetCoreApp,
                                                     references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<PropertySymbol>());
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));
                var m01 = c3.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

                var cM01 = (PropertySymbol)c3.FindImplementationForInterfaceMember(m01);

                Assert.Equal("System.Int32 C2.I1.M01 { get; set; }", cM01.ToTestDisplayString());

                Assert.Same(cM01.GetMethod, c3.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Same(cM01.SetMethod, c3.FindImplementationForInterfaceMember(m01.SetMethod));

                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.GetMethod, cM01.GetMethod.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.SetMethod, cM01.SetMethod.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticProperty_10()
        {
            // Implicit implementation is considered only for types implementing interface in source.
            // In metadata, only explicit implementations are considered

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        int32 get_M01 () cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void set_M01 (
            int32 'value'
        ) cil managed 
    {
    }

    .property int32 M01()
    {
        .get int32 I1::get_M01()
        .set void I1::set_M01(int32)
    }
}

.class public auto ansi beforefieldinit C1
    extends System.Object
    implements I1
{
    .method private hidebysig specialname static
        int32 I1.get_M01 () cil managed 
    {
        .override method int32 I1::get_M01()
        IL_0000: ldc.i4.0
        IL_0001: ret
    }

    .method private hidebysig specialname static
        void I1.set_M01 (
            int32 'value'
        ) cil managed 
    {
        .override method void I1::set_M01(int32)
        IL_0000: ret
    }

    .property instance int32 I1.M01()
    {
        .get int32 C1::I1.get_M01()
        .set void C1::I1.set_M01(int32)
    }

    .method public hidebysig specialname static 
        int32 get_M01 () cil managed 
    {
        IL_0000: ldc.i4.0
        IL_0001: ret
    }

    .method public hidebysig specialname static 
        void set_M01 (
            int32 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .property int32 M01()
    {
        .get int32 C1::get_M01()
        .set void C1::set_M01(int32)
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void System.Object::.ctor()
        IL_0006: ret
    }
}

.class public auto ansi beforefieldinit C2
    extends C1
    implements I1
{
    .method public hidebysig specialname static 
        int32 get_M01 () cil managed 
    {
        IL_0000: ldc.i4.0
        IL_0001: ret
    }

    .method public hidebysig specialname static 
        void set_M01 (
            int32 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .property int32 M01()
    {
        .get int32 C2::get_M01()
        .set void C2::set_M01(int32)
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void C1::.ctor()
        IL_0006: ret
    }
}
";
            var source1 =
@"
public class C3 : C2
{
}

public class C4 : C1, I1
{
}

public class C5 : C2, I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var m01 = c1.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

            var c1M01 = (PropertySymbol)c1.FindImplementationForInterfaceMember(m01);

            Assert.Equal("System.Int32 C1.I1.M01 { get; set; }", c1M01.ToTestDisplayString());

            Assert.Same(c1M01.GetMethod, c1.FindImplementationForInterfaceMember(m01.GetMethod));
            Assert.Same(c1M01.SetMethod, c1.FindImplementationForInterfaceMember(m01.SetMethod));
            Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.GetMethod, c1M01.GetMethod.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.SetMethod, c1M01.SetMethod.ExplicitInterfaceImplementations.Single());

            var c2 = compilation1.GlobalNamespace.GetTypeMember("C2");
            Assert.Same(c1M01, c2.FindImplementationForInterfaceMember(m01));
            Assert.Same(c1M01.GetMethod, c2.FindImplementationForInterfaceMember(m01.GetMethod));
            Assert.Same(c1M01.SetMethod, c2.FindImplementationForInterfaceMember(m01.SetMethod));

            var c3 = compilation1.GlobalNamespace.GetTypeMember("C3");
            Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));
            Assert.Same(c1M01.GetMethod, c3.FindImplementationForInterfaceMember(m01.GetMethod));
            Assert.Same(c1M01.SetMethod, c3.FindImplementationForInterfaceMember(m01.SetMethod));

            var c4 = compilation1.GlobalNamespace.GetTypeMember("C4");
            Assert.Same(c1M01, c4.FindImplementationForInterfaceMember(m01));
            Assert.Same(c1M01.GetMethod, c4.FindImplementationForInterfaceMember(m01.GetMethod));
            Assert.Same(c1M01.SetMethod, c4.FindImplementationForInterfaceMember(m01.SetMethod));

            var c5 = compilation1.GlobalNamespace.GetTypeMember("C5");

            var c2M01 = (PropertySymbol)c5.FindImplementationForInterfaceMember(m01);
            Assert.Equal("System.Int32 C2.M01 { get; set; }", c2M01.ToTestDisplayString());
            Assert.Same(c2M01.GetMethod, c5.FindImplementationForInterfaceMember(m01.GetMethod));
            Assert.Same(c2M01.SetMethod, c5.FindImplementationForInterfaceMember(m01.SetMethod));

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();
        }

        [Fact]
        public void ImplementAbstractStaticProperty_11()
        {
            // Ignore invalid metadata (non-abstract static virtual method). 
            scenario1();
            scenario2();
            scenario3();

            void scenario1()
            {
                var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method private hidebysig specialname static virtual
        int32 get_M01 () cil managed 
    {
        IL_0000: ldc.i4.0
        IL_0001: ret
    }

    .method private hidebysig specialname static virtual
        void set_M01 (
            int32 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .property int32 M01()
    {
        .get int32 I1::get_M01()
        .set void I1::set_M01(int32)
    }
}
";

                var source1 =
@"
public class C1 : I1
{
}
";

                var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyEmitDiagnostics();

                var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
                var i1 = c1.Interfaces().Single();
                var m01 = i1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.SetMethod));

                compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.Regular9,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyEmitDiagnostics();

                var source2 =
@"
public class C1 : I1
{
   static int I1.M01 { get; set; }
}
";

                var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation2.VerifyEmitDiagnostics(
                    // (4,18): error CS0539: 'C1.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                    //    static int I1.M01 { get; set; }
                    Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C1.M01").WithLocation(4, 18)
                    );

                c1 = compilation2.GlobalNamespace.GetTypeMember("C1");
                m01 = c1.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.SetMethod));

                var source3 =
@"
public class C1 : I1
{
   public static int M01 { get; set; }
}
";

                var compilation3 = CreateCompilationWithIL(source3, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation3, sourceSymbolValidator: validate3, symbolValidator: validate3, verify: Verification.Skipped).VerifyDiagnostics();

                void validate3(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");
                    Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                    Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var m01 = c.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();
                    Assert.Null(c.FindImplementationForInterfaceMember(m01));
                    Assert.Null(c.FindImplementationForInterfaceMember(m01.GetMethod));
                    Assert.Null(c.FindImplementationForInterfaceMember(m01.SetMethod));
                }
            }

            void scenario2()
            {
                var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        int32 get_M01 () cil managed 
    {
    }

    .method private hidebysig specialname static virtual
        void set_M01 (
            int32 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .property int32 M01()
    {
        .get int32 I1::get_M01()
        .set void I1::set_M01(int32)
    }
}
";

                var source1 =
@"
public class C1 : I1
{
}
";

                var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(2, 19)
                    );

                var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
                var i1 = c1.Interfaces().Single();
                var m01 = i1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.SetMethod));

                var source2 =
@"
public class C1 : I1
{
   static int I1.M01 { get; }
}
";

                var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation2, sourceSymbolValidator: validate2, symbolValidator: validate2, verify: Verification.Skipped).VerifyDiagnostics();

                void validate2(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");
                    var m01 = c.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();
                    var m01Get = m01.GetMethod;
                    var m01Set = m01.SetMethod;

                    Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                    Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("System.Int32 C1.I1.M01 { get; }", cM01.ToTestDisplayString());

                    var cM01Get = cM01.GetMethod;
                    Assert.Same(cM01Get, c.FindImplementationForInterfaceMember(m01Get));

                    Assert.True(cM01Get.IsStatic);
                    Assert.False(cM01Get.IsAbstract);
                    Assert.False(cM01Get.IsVirtual);
                    Assert.False(cM01Get.IsMetadataVirtual());
                    Assert.False(cM01Get.IsMetadataFinal);
                    Assert.False(cM01Get.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertyGet, cM01Get.MethodKind);

                    Assert.Equal("System.Int32 C1.I1.M01.get", cM01Get.ToTestDisplayString());

                    Assert.Null(cM01.SetMethod);
                    Assert.Null(c.FindImplementationForInterfaceMember(m01Set));

                    Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                    Assert.Same(m01Get, cM01Get.ExplicitInterfaceImplementations.Single());
                }

                var source3 =
@"
public class C1 : I1
{
   public static int M01 { get; set; }
}
";

                var compilation3 = CreateCompilationWithIL(source3, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation3, sourceSymbolValidator: validate3, symbolValidator: validate3, verify: Verification.Skipped).VerifyDiagnostics();

                void validate3(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");

                    var m01 = c.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();
                    var m01Get = m01.GetMethod;

                    Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                    Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("System.Int32 C1.M01 { get; set; }", cM01.ToTestDisplayString());

                    var cM01Get = cM01.GetMethod;
                    Assert.Same(cM01Get, c.FindImplementationForInterfaceMember(m01Get));

                    Assert.True(cM01Get.IsStatic);
                    Assert.False(cM01Get.IsAbstract);
                    Assert.False(cM01Get.IsVirtual);
                    Assert.False(cM01Get.IsMetadataVirtual());
                    Assert.False(cM01Get.IsMetadataFinal);
                    Assert.False(cM01Get.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertyGet, cM01Get.MethodKind);

                    Assert.Equal("System.Int32 C1.M01.get", cM01Get.ToTestDisplayString());

                    var cM01Set = cM01.SetMethod;

                    Assert.True(cM01Set.IsStatic);
                    Assert.False(cM01Set.IsAbstract);
                    Assert.False(cM01Set.IsVirtual);
                    Assert.False(cM01Set.IsMetadataVirtual());
                    Assert.False(cM01Set.IsMetadataFinal);
                    Assert.False(cM01Set.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertySet, cM01Set.MethodKind);

                    Assert.Equal("void C1.M01.set", cM01Set.ToTestDisplayString());

                    Assert.Null(c.FindImplementationForInterfaceMember(m01.SetMethod));

                    if (module is PEModuleSymbol)
                    {
                        Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                        Assert.Same(m01Get, cM01Get.ExplicitInterfaceImplementations.Single());
                    }
                    else
                    {
                        Assert.Empty(cM01.ExplicitInterfaceImplementations);
                        Assert.Empty(cM01Get.ExplicitInterfaceImplementations);
                    }

                    Assert.Empty(cM01Set.ExplicitInterfaceImplementations);
                }

                var source4 =
@"
public class C1 : I1
{
   static int I1.M01 { get; set; }
}
";

                var compilation4 = CreateCompilationWithIL(source4, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation4.VerifyDiagnostics(
                    // (4,29): error CS0550: 'C1.I1.M01.set' adds an accessor not found in interface member 'I1.M01'
                    //    static int I1.M01 { get; set; }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "set").WithArguments("C1.I1.M01.set", "I1.M01").WithLocation(4, 29)
                    );

                c1 = compilation4.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<PropertySymbol>().Single();
                var c1M01 = c1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.GetMethod, c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.GetMethod, c1M01.GetMethod.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.SetMethod, c1M01.SetMethod.ExplicitInterfaceImplementations.Single());

                var source5 =
@"
public class C1 : I1
{
   public static int M01 { get; }
}
";

                var compilation5 = CreateCompilationWithIL(source5, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation5, sourceSymbolValidator: validate5, symbolValidator: validate5, verify: Verification.Skipped).VerifyDiagnostics();

                void validate5(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");

                    var m01 = c.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();
                    var m01Get = m01.GetMethod;

                    Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                    Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("System.Int32 C1.M01 { get; }", cM01.ToTestDisplayString());

                    var cM01Get = cM01.GetMethod;
                    Assert.Same(cM01Get, c.FindImplementationForInterfaceMember(m01Get));

                    Assert.True(cM01Get.IsStatic);
                    Assert.False(cM01Get.IsAbstract);
                    Assert.False(cM01Get.IsVirtual);
                    Assert.False(cM01Get.IsMetadataVirtual());
                    Assert.False(cM01Get.IsMetadataFinal);
                    Assert.False(cM01Get.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertyGet, cM01Get.MethodKind);

                    Assert.Equal("System.Int32 C1.M01.get", cM01Get.ToTestDisplayString());

                    Assert.Null(c.FindImplementationForInterfaceMember(m01.SetMethod));

                    if (module is PEModuleSymbol)
                    {
                        Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                        Assert.Same(m01Get, cM01Get.ExplicitInterfaceImplementations.Single());
                    }
                    else
                    {
                        Assert.Empty(cM01.ExplicitInterfaceImplementations);
                        Assert.Empty(cM01Get.ExplicitInterfaceImplementations);
                    }
                }

                var source6 =
@"
public class C1 : I1
{
   public static int M01 { set{} }
}
";

                var compilation6 = CreateCompilationWithIL(source6, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation6.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.get'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.get").WithLocation(2, 19)
                    );

                c1 = compilation6.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<PropertySymbol>().Single();
                c1M01 = c1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));

                var source7 =
@"
public class C1 : I1
{
   static int I1.M01 { set{} }
}
";

                var compilation7 = CreateCompilationWithIL(source7, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation7.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.get'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.get").WithLocation(2, 19),
                    // (4,18): error CS0551: Explicit interface implementation 'C1.I1.M01' is missing accessor 'I1.M01.get'
                    //    static int I1.M01 { set{} }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyMissingAccessor, "M01").WithArguments("C1.I1.M01", "I1.M01.get").WithLocation(4, 18),
                    // (4,24): error CS0550: 'C1.I1.M01.set' adds an accessor not found in interface member 'I1.M01'
                    //    static int I1.M01 { set{} }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "set").WithArguments("C1.I1.M01.set", "I1.M01").WithLocation(4, 24)
                    );

                c1 = compilation7.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<PropertySymbol>().Single();
                c1M01 = c1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.SetMethod, c1M01.SetMethod.ExplicitInterfaceImplementations.Single());
            }

            void scenario3()
            {
                var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method private hidebysig specialname static virtual
        int32 get_M01 () cil managed 
    {
        IL_0000: ldc.i4.0
        IL_0001: ret
    }

    .method public hidebysig specialname abstract virtual static 
        void set_M01 (
            int32 'value'
        ) cil managed 
    {
    }

    .property int32 M01()
    {
        .get int32 I1::get_M01()
        .set void I1::set_M01(int32)
    }
}
";

                var source1 =
@"
public class C1 : I1
{
}
";

                var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(2, 19)
                    );

                var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
                var i1 = c1.Interfaces().Single();
                var m01 = i1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.SetMethod));

                var source2 =
@"
public class C1 : I1
{
   static int I1.M01 { set{} }
}
";

                var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation2, sourceSymbolValidator: validate2, symbolValidator: validate2, verify: Verification.Skipped).VerifyDiagnostics();

                void validate2(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");
                    var m01 = c.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();
                    var m01Get = m01.GetMethod;
                    var m01Set = m01.SetMethod;

                    Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                    Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("System.Int32 C1.I1.M01 { set; }", cM01.ToTestDisplayString());

                    var cM01Set = cM01.SetMethod;
                    Assert.Same(cM01Set, c.FindImplementationForInterfaceMember(m01Set));

                    Assert.True(cM01Set.IsStatic);
                    Assert.False(cM01Set.IsAbstract);
                    Assert.False(cM01Set.IsVirtual);
                    Assert.False(cM01Set.IsMetadataVirtual());
                    Assert.False(cM01Set.IsMetadataFinal);
                    Assert.False(cM01Set.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertySet, cM01Set.MethodKind);

                    Assert.Equal("void C1.I1.M01.set", cM01Set.ToTestDisplayString());

                    Assert.Null(cM01.GetMethod);
                    Assert.Null(c.FindImplementationForInterfaceMember(m01Get));

                    Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                    Assert.Same(m01Set, cM01Set.ExplicitInterfaceImplementations.Single());
                }

                var source3 =
@"
public class C1 : I1
{
   public static int M01 { get; set; }
}
";

                var compilation3 = CreateCompilationWithIL(source3, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation3, sourceSymbolValidator: validate3, symbolValidator: validate3, verify: Verification.Skipped).VerifyDiagnostics();

                void validate3(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");

                    var m01 = c.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();
                    var m01Set = m01.SetMethod;

                    Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                    Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("System.Int32 C1.M01 { get; set; }", cM01.ToTestDisplayString());

                    var cM01Set = cM01.SetMethod;
                    Assert.Same(cM01Set, c.FindImplementationForInterfaceMember(m01Set));

                    Assert.True(cM01Set.IsStatic);
                    Assert.False(cM01Set.IsAbstract);
                    Assert.False(cM01Set.IsVirtual);
                    Assert.False(cM01Set.IsMetadataVirtual());
                    Assert.False(cM01Set.IsMetadataFinal);
                    Assert.False(cM01Set.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertySet, cM01Set.MethodKind);

                    Assert.Equal("void C1.M01.set", cM01Set.ToTestDisplayString());

                    var cM01Get = cM01.GetMethod;

                    Assert.True(cM01Get.IsStatic);
                    Assert.False(cM01Get.IsAbstract);
                    Assert.False(cM01Get.IsVirtual);
                    Assert.False(cM01Get.IsMetadataVirtual());
                    Assert.False(cM01Get.IsMetadataFinal);
                    Assert.False(cM01Get.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertyGet, cM01Get.MethodKind);

                    Assert.Equal("System.Int32 C1.M01.get", cM01Get.ToTestDisplayString());

                    Assert.Null(c.FindImplementationForInterfaceMember(m01.GetMethod));

                    if (module is PEModuleSymbol)
                    {
                        Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                        Assert.Same(m01Set, cM01Set.ExplicitInterfaceImplementations.Single());
                    }
                    else
                    {
                        Assert.Empty(cM01.ExplicitInterfaceImplementations);
                        Assert.Empty(cM01Set.ExplicitInterfaceImplementations);
                    }

                    Assert.Empty(cM01Get.ExplicitInterfaceImplementations);
                }

                var source4 =
@"
public class C1 : I1
{
   static int I1.M01 { get; set; }
}
";

                var compilation4 = CreateCompilationWithIL(source4, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation4.VerifyDiagnostics(
                    // (4,24): error CS0550: 'C1.I1.M01.get' adds an accessor not found in interface member 'I1.M01'
                    //    static int I1.M01 { get; set; }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "get").WithArguments("C1.I1.M01.get", "I1.M01").WithLocation(4, 24)
                   );

                c1 = compilation4.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<PropertySymbol>().Single();
                var c1M01 = c1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.SetMethod, c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.GetMethod, c1M01.GetMethod.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.SetMethod, c1M01.SetMethod.ExplicitInterfaceImplementations.Single());

                var source5 =
@"
public class C1 : I1
{
   public static int M01 { set{} }
}
";

                var compilation5 = CreateCompilationWithIL(source5, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation5, sourceSymbolValidator: validate5, symbolValidator: validate5, verify: Verification.Skipped).VerifyDiagnostics();

                void validate5(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");

                    var m01 = c.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();
                    var m01Set = m01.SetMethod;

                    Assert.Equal(1, c.GetMembers().OfType<PropertySymbol>().Count());
                    Assert.Equal(1, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (PropertySymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("System.Int32 C1.M01 { set; }", cM01.ToTestDisplayString());

                    var cM01Set = cM01.SetMethod;
                    Assert.Same(cM01Set, c.FindImplementationForInterfaceMember(m01Set));

                    Assert.True(cM01Set.IsStatic);
                    Assert.False(cM01Set.IsAbstract);
                    Assert.False(cM01Set.IsVirtual);
                    Assert.False(cM01Set.IsMetadataVirtual());
                    Assert.False(cM01Set.IsMetadataFinal);
                    Assert.False(cM01Set.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertySet, cM01Set.MethodKind);

                    Assert.Equal("void C1.M01.set", cM01Set.ToTestDisplayString());

                    Assert.Null(c.FindImplementationForInterfaceMember(m01.GetMethod));

                    if (module is PEModuleSymbol)
                    {
                        Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                        Assert.Same(m01Set, cM01Set.ExplicitInterfaceImplementations.Single());
                    }
                    else
                    {
                        Assert.Empty(cM01.ExplicitInterfaceImplementations);
                        Assert.Empty(cM01Set.ExplicitInterfaceImplementations);
                    }
                }

                var source6 =
@"
public class C1 : I1
{
   public static int M01 { get; }
}
";

                var compilation6 = CreateCompilationWithIL(source6, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation6.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.set'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.set").WithLocation(2, 19)
                    );

                c1 = compilation6.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<PropertySymbol>().Single();
                c1M01 = c1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));

                var source7 =
@"
public class C1 : I1
{
   static int I1.M01 { get; }
}
";

                var compilation7 = CreateCompilationWithIL(source7, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation7.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.set'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.set").WithLocation(2, 19),
                    // (4,18): error CS0551: Explicit interface implementation 'C1.I1.M01' is missing accessor 'I1.M01.set'
                    //    static int I1.M01 { get; }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyMissingAccessor, "M01").WithArguments("C1.I1.M01", "I1.M01.set").WithLocation(4, 18),
                    // (4,24): error CS0550: 'C1.I1.M01.get' adds an accessor not found in interface member 'I1.M01'
                    //    static int I1.M01 { get; }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "get").WithArguments("C1.I1.M01.get", "I1.M01").WithLocation(4, 24)
                    );

                c1 = compilation7.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<PropertySymbol>().Single();
                c1M01 = c1.GetMembers().OfType<PropertySymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.GetMethod, c1M01.GetMethod.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticProperty_12()
        {
            // Ignore invalid metadata (default interface implementation for a static method)

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        int32 get_M01 () cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void set_M01 (
            int32 'value'
        ) cil managed 
    {
    }

    .property int32 M01()
    {
        .get int32 I1::get_M01()
        .set void I1::set_M01(int32)
    }
}

.class interface public auto ansi abstract I2
    implements I1
{
    .method private hidebysig specialname static
        int32 I1.get_M01 () cil managed 
    {
        .override method int32 I1::get_M01()
        IL_0000: ldc.i4.0
        IL_0001: ret
    }

    .method private hidebysig specialname static
        void I1.set_M01 (
            int32 'value'
        ) cil managed 
    {
        .override method void I1::set_M01(int32)
        IL_0000: ret
    }

    .property instance int32 I1.M01()
    {
        .get int32 I2::I1.get_M01()
        .set void I2::I1.set_M01(int32)
    }
}
";

            var source1 =
@"
public class C1 : I2
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics(
                // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01'
                // public class C1 : I2
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2").WithArguments("C1", "I1.M01").WithLocation(2, 19)
                );

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i2 = c1.Interfaces().Single();
            var i1 = i2.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<PropertySymbol>().Single();

            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01));
            Assert.Null(c1.FindImplementationForInterfaceMember(m01.GetMethod));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01.GetMethod));
            Assert.Null(c1.FindImplementationForInterfaceMember(m01.SetMethod));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01.SetMethod));

            var i2M01 = i2.GetMembers().OfType<PropertySymbol>().Single();
            Assert.Same(m01, i2M01.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.GetMethod, i2M01.GetMethod.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.SetMethod, i2M01.SetMethod.ExplicitInterfaceImplementations.Single());
        }

        [Fact]
        public void ImplementAbstractStaticProperty_13()
        {
            // A forwarding method is added for an implicit implementation declared in base class. 

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

class C1
{
    public static int M01 { get; set; }
}

class C2 : C1, I1
{
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<PropertySymbol>().Single();
                var c2 = module.GlobalNamespace.GetTypeMember("C2");

                var c2M01 = (PropertySymbol)c2.FindImplementationForInterfaceMember(m01);
                var c2M01Get = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01.GetMethod);
                var c2M01Set = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01.SetMethod);

                Assert.True(c2M01Get.IsStatic);
                Assert.False(c2M01Get.IsAbstract);
                Assert.False(c2M01Get.IsVirtual);
                Assert.False(c2M01Get.IsMetadataVirtual());
                Assert.False(c2M01Get.IsMetadataFinal);
                Assert.False(c2M01Get.IsMetadataNewSlot());

                Assert.True(c2M01Set.IsStatic);
                Assert.False(c2M01Set.IsAbstract);
                Assert.False(c2M01Set.IsVirtual);
                Assert.False(c2M01Set.IsMetadataVirtual());
                Assert.False(c2M01Set.IsMetadataFinal);
                Assert.False(c2M01Set.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01Get.MethodKind);
                    Assert.False(c2M01Get.HasRuntimeSpecialName);
                    Assert.False(c2M01Get.HasSpecialName);
                    Assert.Equal("System.Int32 C2.I1.get_M01()", c2M01Get.ToTestDisplayString());
                    Assert.Same(m01.GetMethod, c2M01Get.ExplicitInterfaceImplementations.Single());

                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01Set.MethodKind);
                    Assert.False(c2M01Set.HasRuntimeSpecialName);
                    Assert.False(c2M01Set.HasSpecialName);
                    Assert.Equal("void C2.I1.set_M01(System.Int32 value)", c2M01Set.ToTestDisplayString());
                    Assert.Same(m01.SetMethod, c2M01Set.ExplicitInterfaceImplementations.Single());

                    // Forwarding methods for accessors aren't tied to a property
                    Assert.Null(c2M01);

                    var c1M01 = module.GlobalNamespace.GetMember<PropertySymbol>("C1.M01");
                    var c1M01Get = c1M01.GetMethod;
                    var c1M01Set = c1M01.SetMethod;

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);

                    Assert.True(c1M01Get.IsStatic);
                    Assert.False(c1M01Get.IsAbstract);
                    Assert.False(c1M01Get.IsVirtual);
                    Assert.False(c1M01Get.IsMetadataVirtual());
                    Assert.False(c1M01Get.IsMetadataFinal);
                    Assert.False(c1M01Get.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertyGet, c1M01Get.MethodKind);
                    Assert.False(c1M01Get.HasRuntimeSpecialName);
                    Assert.True(c1M01Get.HasSpecialName);
                    Assert.Empty(c1M01Get.ExplicitInterfaceImplementations);

                    Assert.True(c1M01Set.IsStatic);
                    Assert.False(c1M01Set.IsAbstract);
                    Assert.False(c1M01Set.IsVirtual);
                    Assert.False(c1M01Set.IsMetadataVirtual());
                    Assert.False(c1M01Set.IsMetadataFinal);
                    Assert.False(c1M01Set.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.PropertySet, c1M01Set.MethodKind);
                    Assert.False(c1M01Set.HasRuntimeSpecialName);
                    Assert.True(c1M01Set.HasSpecialName);
                    Assert.Empty(c1M01Set.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.True(c2M01.IsStatic);
                    Assert.False(c2M01.IsAbstract);
                    Assert.False(c2M01.IsVirtual);

                    Assert.Equal("System.Int32 C1.M01 { get; set; }", c2M01.ToTestDisplayString());
                    Assert.Empty(c2M01.ExplicitInterfaceImplementations);

                    Assert.Equal(MethodKind.PropertyGet, c2M01Get.MethodKind);
                    Assert.False(c2M01Get.HasRuntimeSpecialName);
                    Assert.True(c2M01Get.HasSpecialName);
                    Assert.Same(c2M01.GetMethod, c2M01Get);
                    Assert.Empty(c2M01Get.ExplicitInterfaceImplementations);

                    Assert.Equal(MethodKind.PropertySet, c2M01Set.MethodKind);
                    Assert.False(c2M01Set.HasRuntimeSpecialName);
                    Assert.True(c2M01Set.HasSpecialName);
                    Assert.Same(c2M01.SetMethod, c2M01Set);
                    Assert.Empty(c2M01Set.ExplicitInterfaceImplementations);
                }
            }

            verifier.VerifyIL("C2.I1.get_M01",
@"
{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  call       ""int C1.M01.get""
  IL_0005:  ret
}
");

            verifier.VerifyIL("C2.I1.set_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C1.M01.set""
  IL_0006:  ret
}
");
        }

        [Fact]
        public void ImplementAbstractStaticProperty_14()
        {
            // A forwarding method is added for an implicit implementation with modopt mismatch. 

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        int32 get_M01 () cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void modopt(I1) set_M01 (
            int32 modopt(I1) 'value'
        ) cil managed 
    {
    }

    .property int32 M01()
    {
        .get int32 I1::get_M01()
        .set void modopt(I1) I1::set_M01(int32 modopt(I1))
    }
}

.class interface public auto ansi abstract I2
{
    .method public hidebysig specialname abstract virtual static 
        int32 modopt(I2) get_M01 () cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void set_M01 (
            int32 modopt(I2) 'value'
        ) cil managed 
    {
    }

    .property int32 modopt(I2) M01()
    {
        .get int32 modopt(I2) I2::get_M01()
        .set void I2::set_M01(int32 modopt(I2))
    }
}
";

            var source1 =
@"
class C1 : I1
{
    public static int M01 { get; set; }
}

class C2 : I1
{
    static int I1.M01 { get; set; }
}

class C3 : I2
{
    static int I2.M01 { get; set; }
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c1 = module.GlobalNamespace.GetTypeMember("C1");
                var m01 = c1.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

                var c1M01 = (PropertySymbol)c1.FindImplementationForInterfaceMember(m01);
                var c1M01Get = c1M01.GetMethod;
                var c1M01Set = c1M01.SetMethod;

                Assert.Equal("System.Int32 C1.M01 { get; set; }", c1M01.ToTestDisplayString());
                Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);

                Assert.Equal(MethodKind.PropertyGet, c1M01Get.MethodKind);
                Assert.Equal("System.Int32 C1.M01.get", c1M01Get.ToTestDisplayString());
                Assert.True(c1M01Get.IsStatic);
                Assert.False(c1M01Get.IsAbstract);
                Assert.False(c1M01Get.IsVirtual);
                Assert.False(c1M01Get.IsMetadataVirtual());
                Assert.False(c1M01Get.IsMetadataFinal);
                Assert.False(c1M01Get.IsMetadataNewSlot());
                Assert.Same(c1M01Get, c1.FindImplementationForInterfaceMember(m01.GetMethod));

                Assert.Equal(MethodKind.PropertySet, c1M01Set.MethodKind);
                Assert.Equal("void C1.M01.set", c1M01Set.ToTestDisplayString());
                Assert.Empty(c1M01Set.ExplicitInterfaceImplementations);
                Assert.True(c1M01Set.IsStatic);
                Assert.False(c1M01Set.IsAbstract);
                Assert.False(c1M01Set.IsVirtual);
                Assert.False(c1M01Set.IsMetadataVirtual());
                Assert.False(c1M01Set.IsMetadataFinal);
                Assert.False(c1M01Set.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Same(m01.GetMethod, c1M01Get.ExplicitInterfaceImplementations.Single());

                    c1M01Set = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01.SetMethod);
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01Set.MethodKind);
                    Assert.Equal("void modopt(I1) C1.I1.set_M01(System.Int32 modopt(I1) value)", c1M01Set.ToTestDisplayString());
                    Assert.Same(m01.SetMethod, c1M01Set.ExplicitInterfaceImplementations.Single());

                    Assert.True(c1M01Set.IsStatic);
                    Assert.False(c1M01Set.IsAbstract);
                    Assert.False(c1M01Set.IsVirtual);
                    Assert.False(c1M01Set.IsMetadataVirtual());
                    Assert.False(c1M01Set.IsMetadataFinal);
                    Assert.False(c1M01Set.IsMetadataNewSlot());
                }
                else
                {
                    Assert.Empty(c1M01Get.ExplicitInterfaceImplementations);
                    Assert.Same(c1M01Set, c1.FindImplementationForInterfaceMember(m01.SetMethod));
                }

                var c2 = module.GlobalNamespace.GetTypeMember("C2");

                var c2M01 = (PropertySymbol)c2.FindImplementationForInterfaceMember(m01);
                var c2M01Get = c2M01.GetMethod;
                var c2M01Set = c2M01.SetMethod;

                Assert.Equal("System.Int32 C2.I1.M01 { get; set; }", c2M01.ToTestDisplayString());

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.Same(m01, c2M01.ExplicitInterfaceImplementations.Single());

                Assert.True(c2M01Get.IsStatic);
                Assert.False(c2M01Get.IsAbstract);
                Assert.False(c2M01Get.IsVirtual);
                Assert.False(c2M01Get.IsMetadataVirtual());
                Assert.False(c2M01Get.IsMetadataFinal);
                Assert.False(c2M01Get.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertyGet, c2M01Get.MethodKind);
                Assert.Equal("System.Int32 C2.I1.M01.get", c2M01Get.ToTestDisplayString());
                Assert.Same(m01.GetMethod, c2M01Get.ExplicitInterfaceImplementations.Single());
                Assert.Same(c2M01Get, c2.FindImplementationForInterfaceMember(m01.GetMethod));

                Assert.True(c2M01Set.IsStatic);
                Assert.False(c2M01Set.IsAbstract);
                Assert.False(c2M01Set.IsVirtual);
                Assert.False(c2M01Set.IsMetadataVirtual());
                Assert.False(c2M01Set.IsMetadataFinal);
                Assert.False(c2M01Set.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertySet, c2M01Set.MethodKind);
                Assert.Equal("void modopt(I1) C2.I1.M01.set", c2M01Set.ToTestDisplayString());
                Assert.Equal("System.Int32 modopt(I1) value", c2M01Set.Parameters.Single().ToTestDisplayString());
                Assert.Same(m01.SetMethod, c2M01Set.ExplicitInterfaceImplementations.Single());
                Assert.Same(c2M01Set, c2.FindImplementationForInterfaceMember(m01.SetMethod));

                Assert.Same(c2M01, c2.GetMembers().OfType<PropertySymbol>().Single());
                Assert.Equal(2, c2.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                m01 = c3.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

                var c3M01 = (PropertySymbol)c3.FindImplementationForInterfaceMember(m01);
                var c3M01Get = c3M01.GetMethod;
                var c3M01Set = c3M01.SetMethod;

                Assert.Equal("System.Int32 modopt(I2) C3.I2.M01 { get; set; }", c3M01.ToTestDisplayString());

                Assert.True(c3M01.IsStatic);
                Assert.False(c3M01.IsAbstract);
                Assert.False(c3M01.IsVirtual);
                Assert.Same(m01, c3M01.ExplicitInterfaceImplementations.Single());

                Assert.True(c3M01Get.IsStatic);
                Assert.False(c3M01Get.IsAbstract);
                Assert.False(c3M01Get.IsVirtual);
                Assert.False(c3M01Get.IsMetadataVirtual());
                Assert.False(c3M01Get.IsMetadataFinal);
                Assert.False(c3M01Get.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertyGet, c3M01Get.MethodKind);
                Assert.Equal("System.Int32 modopt(I2) C3.I2.M01.get", c3M01Get.ToTestDisplayString());
                Assert.Same(m01.GetMethod, c3M01Get.ExplicitInterfaceImplementations.Single());
                Assert.Same(c3M01Get, c3.FindImplementationForInterfaceMember(m01.GetMethod));


                Assert.True(c3M01Set.IsStatic);
                Assert.False(c3M01Set.IsAbstract);
                Assert.False(c3M01Set.IsVirtual);
                Assert.False(c3M01Set.IsMetadataVirtual());
                Assert.False(c3M01Set.IsMetadataFinal);
                Assert.False(c3M01Set.IsMetadataNewSlot());
                Assert.Equal(MethodKind.PropertySet, c3M01Set.MethodKind);
                Assert.Equal("void C3.I2.M01.set", c3M01Set.ToTestDisplayString());
                Assert.Equal("System.Int32 modopt(I2) value", c3M01Set.Parameters.Single().ToTestDisplayString());
                Assert.Same(m01.SetMethod, c3M01Set.ExplicitInterfaceImplementations.Single());
                Assert.Same(c3M01Set, c3.FindImplementationForInterfaceMember(m01.SetMethod));

                Assert.Same(c3M01, c3.GetMembers().OfType<PropertySymbol>().Single());
                Assert.Equal(2, c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());
            }

            verifier.VerifyIL("C1.I1.set_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C1.M01.set""
  IL_0006:  ret
}
");
        }

        [Fact]
        public void ImplementAbstractStatiProperty_15()
        {
            // A forwarding method isn't created if base class implements interface exactly the same way. 

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
    abstract static int M02 { get; set; }
}

public class C1
{
    public static int M01 { get; set; }
}

public class C2 : C1, I1
{
    static int I1.M02 { get; set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.RegularPreview, TestOptions.Regular9 })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<PropertySymbol>());
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));

                var m01 = c3.Interfaces().Single().GetMembers("M01").OfType<PropertySymbol>().Single();

                var c1M01 = c3.BaseType().BaseType().GetMember<PropertySymbol>("M01");
                Assert.Equal("System.Int32 C1.M01 { get; set; }", c1M01.ToTestDisplayString());

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);

                Assert.Empty(c1M01.ExplicitInterfaceImplementations);

                var c1M01Get = c1M01.GetMethod;
                Assert.True(c1M01Get.IsStatic);
                Assert.False(c1M01Get.IsAbstract);
                Assert.False(c1M01Get.IsVirtual);
                Assert.False(c1M01Get.IsMetadataVirtual());
                Assert.False(c1M01Get.IsMetadataFinal);
                Assert.False(c1M01Get.IsMetadataNewSlot());

                Assert.Empty(c1M01Get.ExplicitInterfaceImplementations);

                var c1M01Set = c1M01.SetMethod;
                Assert.True(c1M01Set.IsStatic);
                Assert.False(c1M01Set.IsAbstract);
                Assert.False(c1M01Set.IsVirtual);
                Assert.False(c1M01Set.IsMetadataVirtual());
                Assert.False(c1M01Set.IsMetadataFinal);
                Assert.False(c1M01Set.IsMetadataNewSlot());

                Assert.Empty(c1M01Set.ExplicitInterfaceImplementations);

                if (c1M01.ContainingModule is PEModuleSymbol)
                {
                    var c2M01Get = c3.FindImplementationForInterfaceMember(m01.GetMethod);
                    Assert.Equal("System.Int32 C2.I1.get_M01()", c2M01Get.ToTestDisplayString());

                    var c2M01Set = c3.FindImplementationForInterfaceMember(m01.SetMethod);
                    Assert.Equal("void C2.I1.set_M01(System.Int32 value)", c2M01Set.ToTestDisplayString());

                    // Forwarding methods for accessors aren't tied to a property
                    Assert.Null(c3.FindImplementationForInterfaceMember(m01));
                }
                else
                {
                    Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));
                    Assert.Same(c1M01.GetMethod, c3.FindImplementationForInterfaceMember(m01.GetMethod));
                    Assert.Same(c1M01.SetMethod, c3.FindImplementationForInterfaceMember(m01.SetMethod));
                }

                var m02 = c3.Interfaces().Single().GetMembers("M02").OfType<PropertySymbol>().Single();

                var c2M02 = c3.BaseType().GetMember<PropertySymbol>("I1.M02");
                Assert.Equal("System.Int32 C2.I1.M02 { get; set; }", c2M02.ToTestDisplayString());
                Assert.Same(c2M02, c3.FindImplementationForInterfaceMember(m02));
                Assert.Same(c2M02.GetMethod, c3.FindImplementationForInterfaceMember(m02.GetMethod));
                Assert.Same(c2M02.SetMethod, c3.FindImplementationForInterfaceMember(m02.SetMethod));
            }
        }

        [Fact]
        public void ImplementAbstractStaticProperty_16()
        {
            // A new implicit implementation is properly considered.

            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

public class C1 : I1
{
    public static int M01 { get; set; }
}

public class C2 : C1
{
    new public static int M01 { get; set; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    var verifier = CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

                    verifier.VerifyIL("C3.I1.get_M01",
@"
{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  call       ""int C2.M01.get""
  IL_0005:  ret
}
");

                    verifier.VerifyIL("C3.I1.set_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C2.M01.set""
  IL_0006:  ret
}
");
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                var m01 = c3.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

                var c2M01 = c3.BaseType().GetMember<PropertySymbol>("M01");
                var c2M01Get = c2M01.GetMethod;
                var c2M01Set = c2M01.SetMethod;
                Assert.Equal("System.Int32 C2.M01 { get; set; }", c2M01.ToTestDisplayString());

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.Empty(c2M01.ExplicitInterfaceImplementations);

                Assert.True(c2M01Get.IsStatic);
                Assert.False(c2M01Get.IsAbstract);
                Assert.False(c2M01Get.IsVirtual);
                Assert.False(c2M01Get.IsMetadataVirtual());
                Assert.False(c2M01Get.IsMetadataFinal);
                Assert.False(c2M01Get.IsMetadataNewSlot());
                Assert.Empty(c2M01Get.ExplicitInterfaceImplementations);

                Assert.True(c2M01Set.IsStatic);
                Assert.False(c2M01Set.IsAbstract);
                Assert.False(c2M01Set.IsVirtual);
                Assert.False(c2M01Set.IsMetadataVirtual());
                Assert.False(c2M01Set.IsMetadataFinal);
                Assert.False(c2M01Set.IsMetadataNewSlot());
                Assert.Empty(c2M01Set.ExplicitInterfaceImplementations);

                if (module is PEModuleSymbol)
                {
                    var c3M01 = (PropertySymbol)c3.FindImplementationForInterfaceMember(m01);
                    // Forwarding methods for accessors aren't tied to a property
                    Assert.Null(c3M01);

                    var c3M01Get = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01.GetMethod);
                    Assert.Equal("System.Int32 C3.I1.get_M01()", c3M01Get.ToTestDisplayString());
                    Assert.Same(m01.GetMethod, c3M01Get.ExplicitInterfaceImplementations.Single());

                    var c3M01Set = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01.SetMethod);
                    Assert.Equal("void C3.I1.set_M01(System.Int32 value)", c3M01Set.ToTestDisplayString());
                    Assert.Same(m01.SetMethod, c3M01Set.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Same(c2M01, c3.FindImplementationForInterfaceMember(m01));
                    Assert.Same(c2M01Get, c3.FindImplementationForInterfaceMember(m01.GetMethod));
                    Assert.Same(c2M01Set, c3.FindImplementationForInterfaceMember(m01.SetMethod));
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_19(bool genericFirst)
        {
            // An "ambiguity" in implicit/explicit implementation declared in generic base class.

            var generic =
@"
    public static T M01 { get; set; }
";
            var nonGeneric =
@"
    static int I1.M01 { get; set; }
";
            var source1 =
@"
public interface I1
{
    abstract static int M01 { get; set; }
}

public class C1<T> : I1
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().OfType<PropertySymbol>().Where(m => m.Name.Contains("M01")).Count());
            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (PropertySymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("System.Int32 C1<T>.I1.M01 { get; set; }", c1M01.OriginalDefinition.ToTestDisplayString());
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(c1M01, c2.BaseType().FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticProperty_20(bool genericFirst)
        {
            // Same as ImplementAbstractStaticProperty_19 only interface is generic too.

            var generic =
@"
    static T I1<T>.M01 { get; set; }
";
            var nonGeneric =
@"
    public static int M01 { get; set; }
";
            var source1 =
@"
public interface I1<T>
{
    abstract static T M01 { get; set; }
}

public class C1<T> : I1<T>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().OfType<PropertySymbol>().Where(m => m.Name.Contains("M01")).Count());

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1<int>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<PropertySymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (PropertySymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("T C1<T>.I1<T>.M01 { get; set; }", c1M01.OriginalDefinition.ToTestDisplayString());
                Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(c1M01, c2.BaseType().FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_01(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"#pragma warning disable CS0067 // WRN_UnreferencedEvent
public interface I1
{
    abstract static event System.Action M01;
}

" + typeKeyword + @"
    C1 : I1
{}

" + typeKeyword + @"
    C2 : I1
{
    public event System.Action M01;
}

" + typeKeyword + @"
    C3 : I1
{
    static event System.Action M01;
}

" + typeKeyword + @"
    C4 : I1
{
    event System.Action I1.M01 { add{} remove{}}
}

" + typeKeyword + @"
    C5 : I1
{
    public static event System.Action<int> M01;
}

" + typeKeyword + @"
    C6 : I1
{
    static event System.Action<int> I1.M01 { add{} remove{}}
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1.M01'
                //     C1 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(8, 10),
                // (12,10): error CS9109: 'C2' does not implement static interface member 'I1.M01'. 'C2.M01' cannot implement the interface member because it is not static.
                //     C2 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotStatic, "I1").WithArguments("C2", "I1.M01", "C2.M01").WithLocation(12, 10),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1.M01'. 'C3.M01' cannot implement an interface member because it is not public.
                //     C3 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1").WithArguments("C3", "I1.M01", "C3.M01").WithLocation(18, 10),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1.M01'
                //     C4 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C4", "I1.M01").WithLocation(24, 10),
                // (26,28): error CS0539: 'C4.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     event System.Action I1.M01 { add{} remove{}}
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C4.M01").WithLocation(26, 28),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1.M01'. 'C5.M01' cannot implement 'I1.M01' because it does not have the matching return type of 'Action'.
                //     C5 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1").WithArguments("C5", "I1.M01", "C5.M01", "System.Action").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1.M01'
                //     C6 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C6", "I1.M01").WithLocation(36, 10),
                // (38,40): error CS0539: 'C6.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static event System.Action<int> I1.M01 { add{} remove{}}
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C6.M01").WithLocation(38, 40)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_02(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"#pragma warning disable CS0067 // WRN_UnreferencedEvent
public interface I1
{
    abstract event System.Action M01;
}

" + typeKeyword + @"
    C1 : I1
{}

" + typeKeyword + @"
    C2 : I1
{
    public static event System.Action M01;
}

" + typeKeyword + @"
    C3 : I1
{
    event System.Action M01;
}

" + typeKeyword + @"
    C4 : I1
{
    static event System.Action I1.M01 { add{} remove{} }
}

" + typeKeyword + @"
    C5 : I1
{
    public event System.Action<int> M01;
}

" + typeKeyword + @"
    C6 : I1
{
    event System.Action<int> I1.M01 { add{} remove{} }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (8,10): error CS0535: 'C1' does not implement interface member 'I1.M01'
                //     C1 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(8, 10),
                // (12,10): error CS0736: 'C2' does not implement instance interface member 'I1.M01'. 'C2.M01' cannot implement the interface member because it is static.
                //     C2 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberStatic, "I1").WithArguments("C2", "I1.M01", "C2.M01").WithLocation(12, 10),
                // (18,10): error CS0737: 'C3' does not implement interface member 'I1.M01'. 'C3.M01' cannot implement an interface member because it is not public.
                //     C3 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberNotPublic, "I1").WithArguments("C3", "I1.M01", "C3.M01").WithLocation(18, 10),
                // (24,10): error CS0535: 'C4' does not implement interface member 'I1.M01'
                //     C4 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C4", "I1.M01").WithLocation(24, 10),
                // (26,35): error CS0539: 'C4.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static event System.Action I1.M01 { add{} remove{} }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C4.M01").WithLocation(26, 35),
                // (30,10): error CS0738: 'C5' does not implement interface member 'I1.M01'. 'C5.M01' cannot implement 'I1.M01' because it does not have the matching return type of 'Action'.
                //     C5 : I1
                Diagnostic(ErrorCode.ERR_CloseUnimplementedInterfaceMemberWrongReturnType, "I1").WithArguments("C5", "I1.M01", "C5.M01", "System.Action").WithLocation(30, 10),
                // (36,10): error CS0535: 'C6' does not implement interface member 'I1.M01'
                //     C6 : I1
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C6", "I1.M01").WithLocation(36, 10),
                // (38,33): error CS0539: 'C6.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     event System.Action<int> I1.M01 { add{} remove{} }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C6.M01").WithLocation(38, 33)
                );
        }

        [Fact]
        public void ImplementAbstractStaticEvent_03()
        {
            var source1 =
@"#pragma warning disable CS0067 // WRN_UnreferencedEvent
public interface I1
{
    abstract static event System.Action M01;
}

interface I2 : I1
{}

interface I3 : I1
{
    public virtual event System.Action M01 { add{} remove{} }
}

interface I4 : I1
{
    static event System.Action M01;
}

interface I5 : I1
{
    event System.Action I1.M01 { add{} remove{} }
}

interface I6 : I1
{
    static event System.Action I1.M01 { add{} remove{} }
}

interface I7 : I1
{
    abstract static event System.Action M01;
}

interface I8 : I1
{
    abstract static event System.Action I1.M01;
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics(
                // (12,40): warning CS0108: 'I3.M01' hides inherited member 'I1.M01'. Use the new keyword if hiding was intended.
                //     public virtual event System.Action M01 { add{} remove{} }
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I3.M01", "I1.M01").WithLocation(12, 40),
                // (17,32): warning CS0108: 'I4.M01' hides inherited member 'I1.M01'. Use the new keyword if hiding was intended.
                //     static event System.Action M01;
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I4.M01", "I1.M01").WithLocation(17, 32),
                // (22,28): error CS0539: 'I5.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     event System.Action I1.M01 { add{} remove{} }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I5.M01").WithLocation(22, 28),
                // (27,35): error CS0106: The modifier 'static' is not valid for this item
                //     static event System.Action I1.M01 { add{} remove{} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M01").WithArguments("static").WithLocation(27, 35),
                // (27,35): error CS0539: 'I6.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     static event System.Action I1.M01 { add{} remove{} }
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I6.M01").WithLocation(27, 35),
                // (32,41): warning CS0108: 'I7.M01' hides inherited member 'I1.M01'. Use the new keyword if hiding was intended.
                //     abstract static event System.Action M01;
                Diagnostic(ErrorCode.WRN_NewRequired, "M01").WithArguments("I7.M01", "I1.M01").WithLocation(32, 41),
                // (37,44): error CS0106: The modifier 'static' is not valid for this item
                //     abstract static event System.Action I1.M01 { add{} remove{} }
                Diagnostic(ErrorCode.ERR_BadMemberFlag, "M01").WithArguments("static").WithLocation(37, 44),
                // (37,44): error CS0539: 'I8.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                //     abstract static event System.Action I1.M01;
                Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("I8.M01").WithLocation(37, 44)
                );

            foreach (var m01 in compilation1.GlobalNamespace.GetTypeMember("I1").GetMembers())
            {
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I2").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I3").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I4").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I5").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I6").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I7").FindImplementationForInterfaceMember(m01));
                Assert.Null(compilation1.GlobalNamespace.GetTypeMember("I8").FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_04(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
    abstract static event System.Action M02;
}
";
            var source2 =
typeKeyword + @"
    Test: I1
{
    static event System.Action I1.M01 { add{} remove => throw null; }
    public static event System.Action M02;
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,35): error CS8703: The modifier 'static' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     static event System.Action I1.M01 { add{} remove => throw null; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("static", "9.0", "preview").WithLocation(4, 35),
                // (5,39): warning CS0067: The event 'Test.M02' is never used
                //     public static event System.Action M02;
                Diagnostic(ErrorCode.WRN_UnreferencedEvent, "M02").WithArguments("Test.M02").WithLocation(5, 39)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular9,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation3.VerifyDiagnostics(
                // (4,35): error CS8703: The modifier 'static' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     static event System.Action I1.M01 { add{} remove => throw null; }
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("static", "9.0", "preview").WithLocation(4, 35),
                // (5,39): warning CS0067: The event 'Test.M02' is never used
                //     public static event System.Action M02;
                Diagnostic(ErrorCode.WRN_UnreferencedEvent, "M02").WithArguments("Test.M02").WithLocation(5, 39),
                // (10,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static event System.Action M01;
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M01").WithArguments("abstract", "9.0", "preview").WithLocation(10, 41),
                // (11,41): error CS8703: The modifier 'abstract' is not valid for this item in C# 9.0. Please use language version 'preview' or greater.
                //     abstract static event System.Action M02;
                Diagnostic(ErrorCode.ERR_InvalidModifierForLanguageVersion, "M02").WithArguments("abstract", "9.0", "preview").WithLocation(11, 41)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_05(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    public static event System.Action M01 { add{} remove{} }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (2,12): error CS9110: 'Test1.M01.remove' cannot implement interface member 'I1.M01.remove' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.remove", "I1.M01.remove", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01.add' cannot implement interface member 'I1.M01.add' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.add", "I1.M01.add", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01' cannot implement interface member 'I1.M01' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01", "I1.M01", "Test1").WithLocation(2, 12)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (2,12): error CS9110: 'Test1.M01.remove' cannot implement interface member 'I1.M01.remove' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.remove", "I1.M01.remove", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01.add' cannot implement interface member 'I1.M01.add' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01.add", "I1.M01.add", "Test1").WithLocation(2, 12),
                // (2,12): error CS9110: 'Test1.M01' cannot implement interface member 'I1.M01' in type 'Test1' because the target runtime doesn't support static abstract members in interfaces.
                //     Test1: I1
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfacesForMember, "I1").WithArguments("Test1.M01", "I1.M01", "Test1").WithLocation(2, 12),
                // (9,41): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static event System.Action M01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(9, 41)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_06(bool structure)
        {
            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
}
";
            var source2 =
typeKeyword + @"
    Test1: I1
{
    static event System.Action I1.M01 { add => throw null; remove => throw null; }
}
";
            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended,
                                                 references: new[] { compilation1.ToMetadataReference() });

            compilation2.VerifyDiagnostics(
                // (4,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static event System.Action I1.M01 { add => throw null; remove => throw null; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(4, 35)
                );

            var compilation3 = CreateCompilation(source2 + source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.DesktopLatestExtended);

            compilation3.VerifyDiagnostics(
                // (4,35): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     static event System.Action I1.M01 { add => throw null; remove => throw null; }
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(4, 35),
                // (9,41): error CS9100: Target runtime doesn't support static abstract members in interfaces.
                //     abstract static event System.Action M01;
                Diagnostic(ErrorCode.ERR_RuntimeDoesNotSupportStaticAbstractMembersInInterfaces, "M01").WithLocation(9, 41)
                );
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_07(bool structure)
        {
            // Basic implicit implementation scenario, MethodImpl is emitted

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
}

" + typeKeyword + @"
    C : I1
{
    public static event System.Action M01 { add => throw null; remove {} }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<EventSymbol>().Single();
                var m01Add = m01.AddMethod;
                var m01Remove = m01.RemoveMethod;
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<EventSymbol>().Count());
                Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (EventSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);

                Assert.Equal("event System.Action C.M01", cM01.ToTestDisplayString());

                var cM01Add = cM01.AddMethod;
                Assert.Same(cM01Add, c.FindImplementationForInterfaceMember(m01Add));

                Assert.True(cM01Add.IsStatic);
                Assert.False(cM01Add.IsAbstract);
                Assert.False(cM01Add.IsVirtual);
                Assert.False(cM01Add.IsMetadataVirtual());
                Assert.False(cM01Add.IsMetadataFinal);
                Assert.False(cM01Add.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventAdd, cM01Add.MethodKind);
                Assert.False(cM01Add.HasRuntimeSpecialName);
                Assert.True(cM01Add.HasSpecialName);

                Assert.Equal("void C.M01.add", cM01Add.ToTestDisplayString());

                var cM01Remove = cM01.RemoveMethod;
                Assert.Same(cM01Remove, c.FindImplementationForInterfaceMember(m01Remove));

                Assert.True(cM01Remove.IsStatic);
                Assert.False(cM01Remove.IsAbstract);
                Assert.False(cM01Remove.IsVirtual);
                Assert.False(cM01Remove.IsMetadataVirtual());
                Assert.False(cM01Remove.IsMetadataFinal);
                Assert.False(cM01Remove.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventRemove, cM01Remove.MethodKind);
                Assert.False(cM01Remove.HasRuntimeSpecialName);
                Assert.True(cM01Remove.HasSpecialName);

                Assert.Equal("void C.M01.remove", cM01Remove.ToTestDisplayString());

                if (module is PEModuleSymbol)
                {
                    Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                    Assert.Same(m01Add, cM01Add.ExplicitInterfaceImplementations.Single());
                    Assert.Same(m01Remove, cM01Remove.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                    Assert.Empty(cM01Add.ExplicitInterfaceImplementations);
                    Assert.Empty(cM01Remove.ExplicitInterfaceImplementations);
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_08(bool structure)
        {
            // Basic explicit implementation scenario

            var typeKeyword = structure ? "struct" : "class";

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
}

" + typeKeyword + @"
    C : I1
{
    static event System.Action I1.M01 { add => throw null; remove {} }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped,
                             emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true).WithIncludePrivateMembers(false)).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<EventSymbol>().Single();
                var m01Add = m01.AddMethod;
                var m01Remove = m01.RemoveMethod;
                var c = module.GlobalNamespace.GetTypeMember("C");

                Assert.Equal(1, c.GetMembers().OfType<EventSymbol>().Count());
                Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var cM01 = (EventSymbol)c.FindImplementationForInterfaceMember(m01);

                Assert.True(cM01.IsStatic);
                Assert.False(cM01.IsAbstract);
                Assert.False(cM01.IsVirtual);

                Assert.Equal("event System.Action C.I1.M01", cM01.ToTestDisplayString());

                var cM01Add = cM01.AddMethod;
                Assert.Same(cM01Add, c.FindImplementationForInterfaceMember(m01Add));

                Assert.True(cM01Add.IsStatic);
                Assert.False(cM01Add.IsAbstract);
                Assert.False(cM01Add.IsVirtual);
                Assert.False(cM01Add.IsMetadataVirtual());
                Assert.False(cM01Add.IsMetadataFinal);
                Assert.False(cM01Add.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventAdd, cM01Add.MethodKind);
                Assert.False(cM01Add.HasRuntimeSpecialName);
                Assert.True(cM01Add.HasSpecialName);

                Assert.Equal("void C.I1.M01.add", cM01Add.ToTestDisplayString());

                var cM01Remove = cM01.RemoveMethod;
                Assert.Same(cM01Remove, c.FindImplementationForInterfaceMember(m01Remove));

                Assert.True(cM01Remove.IsStatic);
                Assert.False(cM01Remove.IsAbstract);
                Assert.False(cM01Remove.IsVirtual);
                Assert.False(cM01Remove.IsMetadataVirtual());
                Assert.False(cM01Remove.IsMetadataFinal);
                Assert.False(cM01Remove.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventRemove, cM01Remove.MethodKind);
                Assert.False(cM01Remove.HasRuntimeSpecialName);
                Assert.True(cM01Remove.HasSpecialName);

                Assert.Equal("void C.I1.M01.remove", cM01Remove.ToTestDisplayString());

                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01Add, cM01Add.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01Remove, cM01Remove.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticEvent_09()
        {
            // Explicit implementation from base is treated as an implementation

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
}

public class C1
{
    public static event System.Action M01 { add => throw null; remove {} }
}

public class C2 : C1, I1
{
    static event System.Action I1.M01 { add => throw null; remove {} }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                     parseOptions: parseOptions,
                                                     targetFramework: TargetFramework.NetCoreApp,
                                                     references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<EventSymbol>());
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));
                var m01 = c3.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

                var cM01 = (EventSymbol)c3.FindImplementationForInterfaceMember(m01);

                Assert.Equal("event System.Action C2.I1.M01", cM01.ToTestDisplayString());

                Assert.Same(cM01.AddMethod, c3.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Same(cM01.RemoveMethod, c3.FindImplementationForInterfaceMember(m01.RemoveMethod));

                Assert.Same(m01, cM01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.AddMethod, cM01.AddMethod.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.RemoveMethod, cM01.RemoveMethod.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticEvent_10()
        {
            // Implicit implementation is considered only for types implementing interface in source.
            // In metadata, only explicit implementations are considered

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        void add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
    }

    .event [mscorlib]System.Action M01
    {
        .addon void I1::add_M01(class [mscorlib]System.Action)
        .removeon void I1::remove_M01(class [mscorlib]System.Action)
    }
}

.class public auto ansi beforefieldinit C1
    extends System.Object
    implements I1
{
    .method private hidebysig specialname static
        void I1.add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        .override method void I1::add_M01(class [mscorlib]System.Action)
        IL_0000: ret
    }

    .method private hidebysig specialname static
        void I1.remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        .override method void I1::remove_M01(class [mscorlib]System.Action)
        IL_0000: ret
    }

    .event [mscorlib]System.Action I1.M01
    {
        .addon void C1::I1.add_M01(class [mscorlib]System.Action)
        .removeon void C1::I1.remove_M01(class [mscorlib]System.Action)
    }

    .method public hidebysig specialname static 
        void add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .method public hidebysig specialname static 
        void remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .event [mscorlib]System.Action M01
    {
        .addon void C1::add_M01(class [mscorlib]System.Action)
        .removeon void C1::remove_M01(class [mscorlib]System.Action)
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void System.Object::.ctor()
        IL_0006: ret
    }
}

.class public auto ansi beforefieldinit C2
    extends C1
    implements I1
{
    .method public hidebysig specialname static 
        void add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .method public hidebysig specialname static 
        void remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .event [mscorlib]System.Action M01
    {
        .addon void C2::add_M01(class [mscorlib]System.Action)
        .removeon void C2::remove_M01(class [mscorlib]System.Action)
    }

    .method public hidebysig specialname rtspecialname 
        instance void .ctor () cil managed 
    {
        IL_0000: ldarg.0
        IL_0001: call instance void C1::.ctor()
        IL_0006: ret
    }
}
";
            var source1 =
@"
public class C3 : C2
{
}

public class C4 : C1, I1
{
}

public class C5 : C2, I1
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var m01 = c1.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

            var c1M01 = (EventSymbol)c1.FindImplementationForInterfaceMember(m01);

            Assert.Equal("event System.Action C1.I1.M01", c1M01.ToTestDisplayString());

            Assert.Same(c1M01.AddMethod, c1.FindImplementationForInterfaceMember(m01.AddMethod));
            Assert.Same(c1M01.RemoveMethod, c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
            Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.AddMethod, c1M01.AddMethod.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.RemoveMethod, c1M01.RemoveMethod.ExplicitInterfaceImplementations.Single());

            var c2 = compilation1.GlobalNamespace.GetTypeMember("C2");
            Assert.Same(c1M01, c2.FindImplementationForInterfaceMember(m01));
            Assert.Same(c1M01.AddMethod, c2.FindImplementationForInterfaceMember(m01.AddMethod));
            Assert.Same(c1M01.RemoveMethod, c2.FindImplementationForInterfaceMember(m01.RemoveMethod));

            var c3 = compilation1.GlobalNamespace.GetTypeMember("C3");
            Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));
            Assert.Same(c1M01.AddMethod, c3.FindImplementationForInterfaceMember(m01.AddMethod));
            Assert.Same(c1M01.RemoveMethod, c3.FindImplementationForInterfaceMember(m01.RemoveMethod));

            var c4 = compilation1.GlobalNamespace.GetTypeMember("C4");
            Assert.Same(c1M01, c4.FindImplementationForInterfaceMember(m01));
            Assert.Same(c1M01.AddMethod, c4.FindImplementationForInterfaceMember(m01.AddMethod));
            Assert.Same(c1M01.RemoveMethod, c4.FindImplementationForInterfaceMember(m01.RemoveMethod));

            var c5 = compilation1.GlobalNamespace.GetTypeMember("C5");

            var c2M01 = (EventSymbol)c5.FindImplementationForInterfaceMember(m01);
            Assert.Equal("event System.Action C2.M01", c2M01.ToTestDisplayString());
            Assert.Same(c2M01.AddMethod, c5.FindImplementationForInterfaceMember(m01.AddMethod));
            Assert.Same(c2M01.RemoveMethod, c5.FindImplementationForInterfaceMember(m01.RemoveMethod));

            compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.Regular,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();
        }

        [Fact]
        public void ImplementAbstractStaticEvent_11()
        {
            // Ignore invalid metadata (non-abstract static virtual method). 
            scenario1();
            scenario2();
            scenario3();

            void scenario1()
            {
                var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname static virtual
        void add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .method public hidebysig specialname static virtual
        void remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .event [mscorlib]System.Action M01
    {
        .addon void I1::add_M01(class [mscorlib]System.Action)
        .removeon void I1::remove_M01(class [mscorlib]System.Action)
    }
}
";

                var source1 =
@"
public class C1 : I1
{
}
";

                var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyEmitDiagnostics();

                var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
                var i1 = c1.Interfaces().Single();
                var m01 = i1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.RemoveMethod));

                compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.Regular9,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyEmitDiagnostics();

                var source2 =
@"
public class C1 : I1
{
   static event System.Action I1.M01 { add{} remove{} }
}
";

                var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation2.VerifyEmitDiagnostics(
                    // (4,34): error CS0539: 'C1.M01' in explicit interface declaration is not found among members of the interface that can be implemented
                    //    static event System.Action I1.M01 { add{} remove{} }
                    Diagnostic(ErrorCode.ERR_InterfaceMemberNotFound, "M01").WithArguments("C1.M01").WithLocation(4, 34)
                    );

                c1 = compilation2.GlobalNamespace.GetTypeMember("C1");
                m01 = c1.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.RemoveMethod));

                var source3 =
@"
public class C1 : I1
{
   public static event System.Action M01 { add{} remove{} }
}
";

                var compilation3 = CreateCompilationWithIL(source3, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation3, sourceSymbolValidator: validate3, symbolValidator: validate3, verify: Verification.Skipped).VerifyDiagnostics();

                void validate3(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");
                    Assert.Equal(1, c.GetMembers().OfType<EventSymbol>().Count());
                    Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var m01 = c.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();
                    Assert.Null(c.FindImplementationForInterfaceMember(m01));
                    Assert.Null(c.FindImplementationForInterfaceMember(m01.AddMethod));
                    Assert.Null(c.FindImplementationForInterfaceMember(m01.RemoveMethod));
                }
            }

            void scenario2()
            {
                var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        void add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
    }

    .method public hidebysig specialname static virtual
        void remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .event [mscorlib]System.Action M01
    {
        .addon void I1::add_M01(class [mscorlib]System.Action)
        .removeon void I1::remove_M01(class [mscorlib]System.Action)
    }
}
";

                var source1 =
@"
public class C1 : I1
{
}
";

                var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(2, 19)
                    );

                var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
                var i1 = c1.Interfaces().Single();
                var m01 = i1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.RemoveMethod));

                var source2 =
@"
public class C1 : I1
{
   static event System.Action I1.M01 { add {} }
}
";

                var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation2.VerifyDiagnostics(
                    // (4,34): error CS0065: 'C1.I1.M01': event property must have both add and remove accessors
                    //    static event System.Action I1.M01 { add {} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.I1.M01").WithLocation(4, 34)
                    );

                c1 = compilation2.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                var c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.AddMethod, c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.AddMethod, c1M01.AddMethod.ExplicitInterfaceImplementations.Single());

                var source3 =
@"
public class C1 : I1
{
   public static event System.Action M01 { add{} remove{} }
}
";

                var compilation3 = CreateCompilationWithIL(source3, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation3, sourceSymbolValidator: validate3, symbolValidator: validate3, verify: Verification.Skipped).VerifyDiagnostics();

                void validate3(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");

                    var m01 = c.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();
                    var m01Add = m01.AddMethod;

                    Assert.Equal(1, c.GetMembers().OfType<EventSymbol>().Count());
                    Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (EventSymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("event System.Action C1.M01", cM01.ToTestDisplayString());

                    var cM01Add = cM01.AddMethod;
                    Assert.Same(cM01Add, c.FindImplementationForInterfaceMember(m01Add));

                    Assert.True(cM01Add.IsStatic);
                    Assert.False(cM01Add.IsAbstract);
                    Assert.False(cM01Add.IsVirtual);
                    Assert.False(cM01Add.IsMetadataVirtual());
                    Assert.False(cM01Add.IsMetadataFinal);
                    Assert.False(cM01Add.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.EventAdd, cM01Add.MethodKind);

                    Assert.Equal("void C1.M01.add", cM01Add.ToTestDisplayString());

                    var cM01Remove = cM01.RemoveMethod;

                    Assert.True(cM01Remove.IsStatic);
                    Assert.False(cM01Remove.IsAbstract);
                    Assert.False(cM01Remove.IsVirtual);
                    Assert.False(cM01Remove.IsMetadataVirtual());
                    Assert.False(cM01Remove.IsMetadataFinal);
                    Assert.False(cM01Remove.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.EventRemove, cM01Remove.MethodKind);

                    Assert.Equal("void C1.M01.remove", cM01Remove.ToTestDisplayString());

                    Assert.Null(c.FindImplementationForInterfaceMember(m01.RemoveMethod));

                    if (module is PEModuleSymbol)
                    {
                        Assert.Same(m01Add, cM01Add.ExplicitInterfaceImplementations.Single());
                    }
                    else
                    {
                        Assert.Empty(cM01Add.ExplicitInterfaceImplementations);
                    }

                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                    Assert.Empty(cM01Remove.ExplicitInterfaceImplementations);
                }

                var source4 =
@"
public class C1 : I1
{
   static event System.Action I1.M01 { add{} remove{} }
}
";

                var compilation4 = CreateCompilationWithIL(source4, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation4.VerifyDiagnostics(
                    // (4,46): error CS0550: 'C1.I1.M01.remove' adds an accessor not found in interface member 'I1.M01'
                    //    static event System.Action I1.M01 { add{} remove{} }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "remove").WithArguments("C1.I1.M01.remove", "I1.M01").WithLocation(4, 46)
                    );

                c1 = compilation4.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.AddMethod, c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.AddMethod, c1M01.AddMethod.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.RemoveMethod, c1M01.RemoveMethod.ExplicitInterfaceImplementations.Single());

                var source5 =
@"
public class C1 : I1
{
   public static event System.Action M01 { add{} }
}
";

                var compilation5 = CreateCompilationWithIL(source5, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation5.VerifyDiagnostics(
                    // (4,38): error CS0065: 'C1.M01': event property must have both add and remove accessors
                    //    public static event System.Action M01 { add{} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.M01").WithLocation(4, 38)
                    );

                c1 = compilation5.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.AddMethod, c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));

                var source6 =
@"
public class C1 : I1
{
   public static event System.Action M01 { remove{} }
}
";

                var compilation6 = CreateCompilationWithIL(source6, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation6.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.add'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.add").WithLocation(2, 19),
                    // (4,38): error CS0065: 'C1.M01': event property must have both add and remove accessors
                    //    public static event System.Action M01 { remove{} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.M01").WithLocation(4, 38)
                    );

                c1 = compilation6.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));

                var source7 =
@"
public class C1 : I1
{
   static event System.Action I1.M01 { remove{} }
}
";

                var compilation7 = CreateCompilationWithIL(source7, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation7.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.add'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.add").WithLocation(2, 19),
                    // (4,34): error CS0065: 'C1.I1.M01': event property must have both add and remove accessors
                    //    static event System.Action I1.M01 { remove{} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.I1.M01").WithLocation(4, 34),
                    // (4,40): error CS0550: 'C1.I1.M01.remove' adds an accessor not found in interface member 'I1.M01'
                    //    static event System.Action I1.M01 { remove{} }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "remove").WithArguments("C1.I1.M01.remove", "I1.M01").WithLocation(4, 40)
                    );

                c1 = compilation7.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.RemoveMethod, c1M01.RemoveMethod.ExplicitInterfaceImplementations.Single());
            }

            void scenario3()
            {
                var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname static virtual
        void add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        IL_0000: ret
    }

    .method public hidebysig specialname abstract virtual static 
        void remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
    }

    .event [mscorlib]System.Action M01
    {
        .addon void I1::add_M01(class [mscorlib]System.Action)
        .removeon void I1::remove_M01(class [mscorlib]System.Action)
    }
}
";

                var source1 =
@"
public class C1 : I1
{
}
";

                var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation1.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01").WithLocation(2, 19)
                    );

                var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
                var i1 = c1.Interfaces().Single();
                var m01 = i1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(i1.FindImplementationForInterfaceMember(m01.AddMethod));

                var source2 =
@"
public class C1 : I1
{
   static event System.Action I1.M01 { remove {} }
}
";

                var compilation2 = CreateCompilationWithIL(source2, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation2.VerifyDiagnostics(
                    // (4,34): error CS0065: 'C1.I1.M01': event property must have both add and remove accessors
                    //    static event System.Action I1.M01 { add {} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.I1.M01").WithLocation(4, 34)
                    );

                c1 = compilation2.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                var c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.RemoveMethod, c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.RemoveMethod, c1M01.RemoveMethod.ExplicitInterfaceImplementations.Single());

                var source3 =
@"
public class C1 : I1
{
   public static event System.Action M01 { add{} remove{} }
}
";

                var compilation3 = CreateCompilationWithIL(source3, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                CompileAndVerify(compilation3, sourceSymbolValidator: validate3, symbolValidator: validate3, verify: Verification.Skipped).VerifyDiagnostics();

                void validate3(ModuleSymbol module)
                {
                    var c = module.GlobalNamespace.GetTypeMember("C1");

                    var m01 = c.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();
                    var m01Remove = m01.RemoveMethod;

                    Assert.Equal(1, c.GetMembers().OfType<EventSymbol>().Count());
                    Assert.Equal(2, c.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                    var cM01 = (EventSymbol)c.FindImplementationForInterfaceMember(m01);

                    Assert.True(cM01.IsStatic);
                    Assert.False(cM01.IsAbstract);
                    Assert.False(cM01.IsVirtual);

                    Assert.Equal("event System.Action C1.M01", cM01.ToTestDisplayString());

                    var cM01Remove = cM01.RemoveMethod;
                    Assert.Same(cM01Remove, c.FindImplementationForInterfaceMember(m01Remove));

                    Assert.True(cM01Remove.IsStatic);
                    Assert.False(cM01Remove.IsAbstract);
                    Assert.False(cM01Remove.IsVirtual);
                    Assert.False(cM01Remove.IsMetadataVirtual());
                    Assert.False(cM01Remove.IsMetadataFinal);
                    Assert.False(cM01Remove.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.EventRemove, cM01Remove.MethodKind);

                    Assert.Equal("void C1.M01.remove", cM01Remove.ToTestDisplayString());

                    var cM01Add = cM01.AddMethod;

                    Assert.True(cM01Add.IsStatic);
                    Assert.False(cM01Add.IsAbstract);
                    Assert.False(cM01Add.IsVirtual);
                    Assert.False(cM01Add.IsMetadataVirtual());
                    Assert.False(cM01Add.IsMetadataFinal);
                    Assert.False(cM01Add.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.EventAdd, cM01Add.MethodKind);

                    Assert.Equal("void C1.M01.add", cM01Add.ToTestDisplayString());

                    Assert.Null(c.FindImplementationForInterfaceMember(m01.AddMethod));

                    if (module is PEModuleSymbol)
                    {
                        Assert.Same(m01Remove, cM01Remove.ExplicitInterfaceImplementations.Single());
                    }
                    else
                    {
                        Assert.Empty(cM01Remove.ExplicitInterfaceImplementations);
                    }

                    Assert.Empty(cM01.ExplicitInterfaceImplementations);
                    Assert.Empty(cM01Add.ExplicitInterfaceImplementations);
                }

                var source4 =
@"
public class C1 : I1
{
   static event System.Action I1.M01 { add{} remove{} }
}
";

                var compilation4 = CreateCompilationWithIL(source4, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation4.VerifyDiagnostics(
                    // (4,40): error CS0550: 'C1.I1.M01.add' adds an accessor not found in interface member 'I1.M01'
                    //    static event System.Action I1.M01 { add{} remove{} }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "add").WithArguments("C1.I1.M01.add", "I1.M01").WithLocation(4, 40)
                    );

                c1 = compilation4.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.RemoveMethod, c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.RemoveMethod, c1M01.RemoveMethod.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.AddMethod, c1M01.AddMethod.ExplicitInterfaceImplementations.Single());

                var source5 =
@"
public class C1 : I1
{
   public static event System.Action M01 { remove{} }
}
";

                var compilation5 = CreateCompilationWithIL(source5, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation5.VerifyDiagnostics(
                    // (4,38): error CS0065: 'C1.M01': event property must have both add and remove accessors
                    //    public static event System.Action M01 { remove{} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.M01").WithLocation(4, 38)
                    );

                c1 = compilation5.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Same(c1M01.RemoveMethod, c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));

                var source6 =
@"
public class C1 : I1
{
   public static event System.Action M01 { add{} }
}
";

                var compilation6 = CreateCompilationWithIL(source6, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation6.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.remove'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.remove").WithLocation(2, 19),
                    // (4,38): error CS0065: 'C1.M01': event property must have both add and remove accessors
                    //    public static event System.Action M01 { remove{} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.M01").WithLocation(4, 38)
                    );

                c1 = compilation6.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));

                var source7 =
@"
public class C1 : I1
{
   static event System.Action I1.M01 { add{} }
}
";

                var compilation7 = CreateCompilationWithIL(source7, ilSource, options: TestOptions.DebugDll,
                                                     parseOptions: TestOptions.RegularPreview,
                                                     targetFramework: TargetFramework.NetCoreApp);

                compilation7.VerifyDiagnostics(
                    // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01.remove'
                    // public class C1 : I1
                    Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I1").WithArguments("C1", "I1.M01.remove").WithLocation(2, 19),
                    // (4,34): error CS0065: 'C1.I1.M01': event property must have both add and remove accessors
                    //    static event System.Action I1.M01 { add{} }
                    Diagnostic(ErrorCode.ERR_EventNeedsBothAccessors, "M01").WithArguments("C1.I1.M01").WithLocation(4, 34),
                    // (4,40): error CS0550: 'C1.I1.M01.add' adds an accessor not found in interface member 'I1.M01'
                    //    static event System.Action I1.M01 { add{} }
                    Diagnostic(ErrorCode.ERR_ExplicitPropertyAddingAccessor, "add").WithArguments("C1.I1.M01.add", "I1.M01").WithLocation(4, 40)
                    );

                c1 = compilation7.GlobalNamespace.GetTypeMember("C1");
                i1 = c1.Interfaces().Single();
                m01 = i1.GetMembers().OfType<EventSymbol>().Single();
                c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();

                Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
                Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(m01.AddMethod, c1M01.AddMethod.ExplicitInterfaceImplementations.Single());
            }
        }

        [Fact]
        public void ImplementAbstractStaticEvent_12()
        {
            // Ignore invalid metadata (default interface implementation for a static method)

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        void add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
    }

    .event [mscorlib]System.Action M01
    {
        .addon void I1::add_M01(class [mscorlib]System.Action)
        .removeon void I1::remove_M01(class [mscorlib]System.Action)
    }
}

.class interface public auto ansi abstract I2
    implements I1
{
    .method private hidebysig specialname static
        void I1.add_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        .override method void I1::add_M01(class [mscorlib]System.Action)
        IL_0000: ret
    }

    .method private hidebysig specialname static
        void I1.remove_M01 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
        .override method void I1::remove_M01(class [mscorlib]System.Action)
        IL_0000: ret
    }

    .event [mscorlib]System.Action I1.M01
    {
        .addon void I2::I1.add_M01(class [mscorlib]System.Action)
        .removeon void I2::I1.remove_M01(class [mscorlib]System.Action)
    }
}
";

            var source1 =
@"
public class C1 : I2
{
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyEmitDiagnostics(
                // (2,19): error CS0535: 'C1' does not implement interface member 'I1.M01'
                // public class C1 : I2
                Diagnostic(ErrorCode.ERR_UnimplementedInterfaceMember, "I2").WithArguments("C1", "I1.M01").WithLocation(2, 19)
                );

            var c1 = compilation1.GlobalNamespace.GetTypeMember("C1");
            var i2 = c1.Interfaces().Single();
            var i1 = i2.Interfaces().Single();
            var m01 = i1.GetMembers().OfType<EventSymbol>().Single();

            Assert.Null(c1.FindImplementationForInterfaceMember(m01));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01));
            Assert.Null(c1.FindImplementationForInterfaceMember(m01.AddMethod));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01.AddMethod));
            Assert.Null(c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
            Assert.Null(i2.FindImplementationForInterfaceMember(m01.RemoveMethod));

            var i2M01 = i2.GetMembers().OfType<EventSymbol>().Single();
            Assert.Same(m01, i2M01.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.AddMethod, i2M01.AddMethod.ExplicitInterfaceImplementations.Single());
            Assert.Same(m01.RemoveMethod, i2M01.RemoveMethod.ExplicitInterfaceImplementations.Single());
        }

        [Fact]
        public void ImplementAbstractStaticEvent_13()
        {
            // A forwarding method is added for an implicit implementation declared in base class. 

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
}

class C1
{
    public static event System.Action M01 { add => throw null; remove{} }
}

class C2 : C1, I1
{
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var m01 = module.GlobalNamespace.GetTypeMember("I1").GetMembers().OfType<EventSymbol>().Single();
                var c2 = module.GlobalNamespace.GetTypeMember("C2");

                var c2M01 = (EventSymbol)c2.FindImplementationForInterfaceMember(m01);
                var c2M01Add = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01.AddMethod);
                var c2M01Remove = (MethodSymbol)c2.FindImplementationForInterfaceMember(m01.RemoveMethod);

                Assert.True(c2M01Add.IsStatic);
                Assert.False(c2M01Add.IsAbstract);
                Assert.False(c2M01Add.IsVirtual);
                Assert.False(c2M01Add.IsMetadataVirtual());
                Assert.False(c2M01Add.IsMetadataFinal);
                Assert.False(c2M01Add.IsMetadataNewSlot());

                Assert.True(c2M01Remove.IsStatic);
                Assert.False(c2M01Remove.IsAbstract);
                Assert.False(c2M01Remove.IsVirtual);
                Assert.False(c2M01Remove.IsMetadataVirtual());
                Assert.False(c2M01Remove.IsMetadataFinal);
                Assert.False(c2M01Remove.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01Add.MethodKind);
                    Assert.False(c2M01Add.HasRuntimeSpecialName);
                    Assert.False(c2M01Add.HasSpecialName);
                    Assert.Equal("void C2.I1.add_M01(System.Action value)", c2M01Add.ToTestDisplayString());
                    Assert.Same(m01.AddMethod, c2M01Add.ExplicitInterfaceImplementations.Single());

                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c2M01Remove.MethodKind);
                    Assert.False(c2M01Remove.HasRuntimeSpecialName);
                    Assert.False(c2M01Remove.HasSpecialName);
                    Assert.Equal("void C2.I1.remove_M01(System.Action value)", c2M01Remove.ToTestDisplayString());
                    Assert.Same(m01.RemoveMethod, c2M01Remove.ExplicitInterfaceImplementations.Single());

                    // Forwarding methods for accessors aren't tied to a property
                    Assert.Null(c2M01);

                    var c1M01 = module.GlobalNamespace.GetMember<EventSymbol>("C1.M01");
                    var c1M01Add = c1M01.AddMethod;
                    var c1M01Remove = c1M01.RemoveMethod;

                    Assert.True(c1M01.IsStatic);
                    Assert.False(c1M01.IsAbstract);
                    Assert.False(c1M01.IsVirtual);
                    Assert.Empty(c1M01.ExplicitInterfaceImplementations);

                    Assert.True(c1M01Add.IsStatic);
                    Assert.False(c1M01Add.IsAbstract);
                    Assert.False(c1M01Add.IsVirtual);
                    Assert.False(c1M01Add.IsMetadataVirtual());
                    Assert.False(c1M01Add.IsMetadataFinal);
                    Assert.False(c1M01Add.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.EventAdd, c1M01Add.MethodKind);
                    Assert.False(c1M01Add.HasRuntimeSpecialName);
                    Assert.True(c1M01Add.HasSpecialName);
                    Assert.Empty(c1M01Add.ExplicitInterfaceImplementations);

                    Assert.True(c1M01Remove.IsStatic);
                    Assert.False(c1M01Remove.IsAbstract);
                    Assert.False(c1M01Remove.IsVirtual);
                    Assert.False(c1M01Remove.IsMetadataVirtual());
                    Assert.False(c1M01Remove.IsMetadataFinal);
                    Assert.False(c1M01Remove.IsMetadataNewSlot());
                    Assert.Equal(MethodKind.EventRemove, c1M01Remove.MethodKind);
                    Assert.False(c1M01Remove.HasRuntimeSpecialName);
                    Assert.True(c1M01Remove.HasSpecialName);
                    Assert.Empty(c1M01Remove.ExplicitInterfaceImplementations);
                }
                else
                {
                    Assert.True(c2M01.IsStatic);
                    Assert.False(c2M01.IsAbstract);
                    Assert.False(c2M01.IsVirtual);

                    Assert.Equal("event System.Action C1.M01", c2M01.ToTestDisplayString());
                    Assert.Empty(c2M01.ExplicitInterfaceImplementations);

                    Assert.Equal(MethodKind.EventAdd, c2M01Add.MethodKind);
                    Assert.False(c2M01Add.HasRuntimeSpecialName);
                    Assert.True(c2M01Add.HasSpecialName);
                    Assert.Same(c2M01.AddMethod, c2M01Add);
                    Assert.Empty(c2M01Add.ExplicitInterfaceImplementations);

                    Assert.Equal(MethodKind.EventRemove, c2M01Remove.MethodKind);
                    Assert.False(c2M01Remove.HasRuntimeSpecialName);
                    Assert.True(c2M01Remove.HasSpecialName);
                    Assert.Same(c2M01.RemoveMethod, c2M01Remove);
                    Assert.Empty(c2M01Remove.ExplicitInterfaceImplementations);
                }
            }

            verifier.VerifyIL("C2.I1.add_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C1.M01.add""
  IL_0006:  ret
}
");

            verifier.VerifyIL("C2.I1.remove_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C1.M01.remove""
  IL_0006:  ret
}
");
        }

        [Fact]
        public void ImplementAbstractStaticEvent_14()
        {
            // A forwarding method is added for an implicit implementation with modopt mismatch. 

            var ilSource = @"
.class interface public auto ansi abstract I1
{
    .method public hidebysig specialname abstract virtual static 
        void add_M01 (
            class [mscorlib]System.Action`1<int32 modopt(I1)> 'value'
        ) cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void remove_M01 (
            class [mscorlib]System.Action`1<int32 modopt(I1)> 'value'
        ) cil managed 
    {
    }

    .event class [mscorlib]System.Action`1<int32 modopt(I1)> M01
    {
        .addon void I1::add_M01(class [mscorlib]System.Action`1<int32 modopt(I1)>)
        .removeon void I1::remove_M01(class [mscorlib]System.Action`1<int32 modopt(I1)>)
    }
}

.class interface public auto ansi abstract I2
{
    .method public hidebysig specialname abstract virtual static 
        void add_M02 (
            class [mscorlib]System.Action modopt(I1) 'value'
        ) cil managed 
    {
    }

    .method public hidebysig specialname abstract virtual static 
        void modopt(I2) remove_M02 (
            class [mscorlib]System.Action 'value'
        ) cil managed 
    {
    }

    .event class [mscorlib]System.Action M02
    {
        .addon void I2::add_M02(class [mscorlib]System.Action modopt(I1))
        .removeon void modopt(I2) I2::remove_M02(class [mscorlib]System.Action)
    }
}
";

            var source1 =
@"
class C1 : I1
{
    public static event System.Action<int> M01 { add => throw null; remove{} }
}

class C2 : I1
{
    static event System.Action<int> I1.M01 { add => throw null; remove{} }
}

#pragma warning disable CS0067 // The event 'C3.M02' is never used

class C3 : I2
{
    public static event System.Action M02;
}

class C4 : I2
{
    static event System.Action I2.M02 { add => throw null; remove{} }
}
";

            var compilation1 = CreateCompilationWithIL(source1, ilSource, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            var verifier = CompileAndVerify(compilation1, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

            void validate(ModuleSymbol module)
            {
                var c1 = module.GlobalNamespace.GetTypeMember("C1");
                var m01 = c1.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

                var c1M01 = c1.GetMembers().OfType<EventSymbol>().Single();
                var c1M01Add = c1M01.AddMethod;
                var c1M01Remove = c1M01.RemoveMethod;

                Assert.Equal("event System.Action<System.Int32> C1.M01", c1M01.ToTestDisplayString());
                Assert.Empty(c1M01.ExplicitInterfaceImplementations);
                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);

                Assert.Equal(MethodKind.EventAdd, c1M01Add.MethodKind);
                Assert.Equal("void C1.M01.add", c1M01Add.ToTestDisplayString());
                Assert.Equal("System.Action<System.Int32> value", c1M01Add.Parameters.Single().ToTestDisplayString());
                Assert.Empty(c1M01Add.ExplicitInterfaceImplementations);
                Assert.True(c1M01Add.IsStatic);
                Assert.False(c1M01Add.IsAbstract);
                Assert.False(c1M01Add.IsVirtual);
                Assert.False(c1M01Add.IsMetadataVirtual());
                Assert.False(c1M01Add.IsMetadataFinal);
                Assert.False(c1M01Add.IsMetadataNewSlot());

                Assert.Equal(MethodKind.EventRemove, c1M01Remove.MethodKind);
                Assert.Equal("void C1.M01.remove", c1M01Remove.ToTestDisplayString());
                Assert.Equal("System.Action<System.Int32> value", c1M01Remove.Parameters.Single().ToTestDisplayString());
                Assert.Empty(c1M01Remove.ExplicitInterfaceImplementations);
                Assert.True(c1M01Remove.IsStatic);
                Assert.False(c1M01Remove.IsAbstract);
                Assert.False(c1M01Remove.IsVirtual);
                Assert.False(c1M01Remove.IsMetadataVirtual());
                Assert.False(c1M01Remove.IsMetadataFinal);
                Assert.False(c1M01Remove.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    c1M01Add = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01.AddMethod);
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01Add.MethodKind);
                    Assert.Equal("void C1.I1.add_M01(System.Action<System.Int32 modopt(I1)> value)", c1M01Add.ToTestDisplayString());
                    Assert.Same(m01.AddMethod, c1M01Add.ExplicitInterfaceImplementations.Single());

                    Assert.True(c1M01Add.IsStatic);
                    Assert.False(c1M01Add.IsAbstract);
                    Assert.False(c1M01Add.IsVirtual);
                    Assert.False(c1M01Add.IsMetadataVirtual());
                    Assert.False(c1M01Add.IsMetadataFinal);
                    Assert.False(c1M01Add.IsMetadataNewSlot());

                    c1M01Remove = (MethodSymbol)c1.FindImplementationForInterfaceMember(m01.RemoveMethod);
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c1M01Remove.MethodKind);
                    Assert.Equal("void C1.I1.remove_M01(System.Action<System.Int32 modopt(I1)> value)", c1M01Remove.ToTestDisplayString());
                    Assert.Same(m01.RemoveMethod, c1M01Remove.ExplicitInterfaceImplementations.Single());

                    Assert.True(c1M01Remove.IsStatic);
                    Assert.False(c1M01Remove.IsAbstract);
                    Assert.False(c1M01Remove.IsVirtual);
                    Assert.False(c1M01Remove.IsMetadataVirtual());
                    Assert.False(c1M01Remove.IsMetadataFinal);
                    Assert.False(c1M01Remove.IsMetadataNewSlot());

                    // Forwarding methods aren't tied to an event  
                    Assert.Null(c1.FindImplementationForInterfaceMember(m01));
                }
                else
                {
                    Assert.Same(c1M01, c1.FindImplementationForInterfaceMember(m01));
                    Assert.Same(c1M01Add, c1.FindImplementationForInterfaceMember(m01.AddMethod));
                    Assert.Same(c1M01Remove, c1.FindImplementationForInterfaceMember(m01.RemoveMethod));
                }

                var c2 = module.GlobalNamespace.GetTypeMember("C2");

                var c2M01 = (EventSymbol)c2.FindImplementationForInterfaceMember(m01);
                var c2M01Add = c2M01.AddMethod;
                var c2M01Remove = c2M01.RemoveMethod;

                Assert.Equal("event System.Action<System.Int32 modopt(I1)> C2.I1.M01", c2M01.ToTestDisplayString());

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.Same(m01, c2M01.ExplicitInterfaceImplementations.Single());

                Assert.True(c2M01Add.IsStatic);
                Assert.False(c2M01Add.IsAbstract);
                Assert.False(c2M01Add.IsVirtual);
                Assert.False(c2M01Add.IsMetadataVirtual());
                Assert.False(c2M01Add.IsMetadataFinal);
                Assert.False(c2M01Add.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventAdd, c2M01Add.MethodKind);
                Assert.Equal("void C2.I1.M01.add", c2M01Add.ToTestDisplayString());
                Assert.Equal("System.Action<System.Int32 modopt(I1)> value", c2M01Add.Parameters.Single().ToTestDisplayString());
                Assert.Same(m01.AddMethod, c2M01Add.ExplicitInterfaceImplementations.Single());
                Assert.Same(c2M01Add, c2.FindImplementationForInterfaceMember(m01.AddMethod));

                Assert.True(c2M01Remove.IsStatic);
                Assert.False(c2M01Remove.IsAbstract);
                Assert.False(c2M01Remove.IsVirtual);
                Assert.False(c2M01Remove.IsMetadataVirtual());
                Assert.False(c2M01Remove.IsMetadataFinal);
                Assert.False(c2M01Remove.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventRemove, c2M01Remove.MethodKind);
                Assert.Equal("void C2.I1.M01.remove", c2M01Remove.ToTestDisplayString());
                Assert.Equal("System.Action<System.Int32 modopt(I1)> value", c2M01Remove.Parameters.Single().ToTestDisplayString());
                Assert.Same(m01.RemoveMethod, c2M01Remove.ExplicitInterfaceImplementations.Single());
                Assert.Same(c2M01Remove, c2.FindImplementationForInterfaceMember(m01.RemoveMethod));

                Assert.Same(c2M01, c2.GetMembers().OfType<EventSymbol>().Single());
                Assert.Equal(2, c2.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());

                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                var m02 = c3.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

                var c3M02 = c3.GetMembers().OfType<EventSymbol>().Single();
                var c3M02Add = c3M02.AddMethod;
                var c3M02Remove = c3M02.RemoveMethod;

                Assert.Equal("event System.Action C3.M02", c3M02.ToTestDisplayString());
                Assert.Empty(c3M02.ExplicitInterfaceImplementations);
                Assert.True(c3M02.IsStatic);
                Assert.False(c3M02.IsAbstract);
                Assert.False(c3M02.IsVirtual);

                Assert.Equal(MethodKind.EventAdd, c3M02Add.MethodKind);
                Assert.Equal("void C3.M02.add", c3M02Add.ToTestDisplayString());
                Assert.Equal("System.Action value", c3M02Add.Parameters.Single().ToTestDisplayString());
                Assert.Empty(c3M02Add.ExplicitInterfaceImplementations);
                Assert.True(c3M02Add.IsStatic);
                Assert.False(c3M02Add.IsAbstract);
                Assert.False(c3M02Add.IsVirtual);
                Assert.False(c3M02Add.IsMetadataVirtual());
                Assert.False(c3M02Add.IsMetadataFinal);
                Assert.False(c3M02Add.IsMetadataNewSlot());

                Assert.Equal(MethodKind.EventRemove, c3M02Remove.MethodKind);
                Assert.Equal("void C3.M02.remove", c3M02Remove.ToTestDisplayString());
                Assert.Equal("System.Void", c3M02Remove.ReturnTypeWithAnnotations.ToTestDisplayString());
                Assert.Empty(c3M02Remove.ExplicitInterfaceImplementations);
                Assert.True(c3M02Remove.IsStatic);
                Assert.False(c3M02Remove.IsAbstract);
                Assert.False(c3M02Remove.IsVirtual);
                Assert.False(c3M02Remove.IsMetadataVirtual());
                Assert.False(c3M02Remove.IsMetadataFinal);
                Assert.False(c3M02Remove.IsMetadataNewSlot());

                if (module is PEModuleSymbol)
                {
                    c3M02Add = (MethodSymbol)c3.FindImplementationForInterfaceMember(m02.AddMethod);
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c3M02Add.MethodKind);
                    Assert.Equal("void C3.I2.add_M02(System.Action modopt(I1) value)", c3M02Add.ToTestDisplayString());
                    Assert.Same(m02.AddMethod, c3M02Add.ExplicitInterfaceImplementations.Single());

                    Assert.True(c3M02Add.IsStatic);
                    Assert.False(c3M02Add.IsAbstract);
                    Assert.False(c3M02Add.IsVirtual);
                    Assert.False(c3M02Add.IsMetadataVirtual());
                    Assert.False(c3M02Add.IsMetadataFinal);
                    Assert.False(c3M02Add.IsMetadataNewSlot());

                    c3M02Remove = (MethodSymbol)c3.FindImplementationForInterfaceMember(m02.RemoveMethod);
                    Assert.Equal(MethodKind.ExplicitInterfaceImplementation, c3M02Remove.MethodKind);
                    Assert.Equal("void modopt(I2) C3.I2.remove_M02(System.Action value)", c3M02Remove.ToTestDisplayString());
                    Assert.Same(m02.RemoveMethod, c3M02Remove.ExplicitInterfaceImplementations.Single());

                    Assert.True(c3M02Remove.IsStatic);
                    Assert.False(c3M02Remove.IsAbstract);
                    Assert.False(c3M02Remove.IsVirtual);
                    Assert.False(c3M02Remove.IsMetadataVirtual());
                    Assert.False(c3M02Remove.IsMetadataFinal);
                    Assert.False(c3M02Remove.IsMetadataNewSlot());

                    // Forwarding methods aren't tied to an event  
                    Assert.Null(c3.FindImplementationForInterfaceMember(m02));
                }
                else
                {
                    Assert.Same(c3M02, c3.FindImplementationForInterfaceMember(m02));
                    Assert.Same(c3M02Add, c3.FindImplementationForInterfaceMember(m02.AddMethod));
                    Assert.Same(c3M02Remove, c3.FindImplementationForInterfaceMember(m02.RemoveMethod));
                }

                var c4 = module.GlobalNamespace.GetTypeMember("C4");

                var c4M02 = (EventSymbol)c4.FindImplementationForInterfaceMember(m02);
                var c4M02Add = c4M02.AddMethod;
                var c4M02Remove = c4M02.RemoveMethod;

                Assert.Equal("event System.Action C4.I2.M02", c4M02.ToTestDisplayString());

                // Signatures of accessors are lacking custom modifiers due to https://github.com/dotnet/roslyn/issues/53390.

                Assert.True(c4M02.IsStatic);
                Assert.False(c4M02.IsAbstract);
                Assert.False(c4M02.IsVirtual);
                Assert.Same(m02, c4M02.ExplicitInterfaceImplementations.Single());

                Assert.True(c4M02Add.IsStatic);
                Assert.False(c4M02Add.IsAbstract);
                Assert.False(c4M02Add.IsVirtual);
                Assert.False(c4M02Add.IsMetadataVirtual());
                Assert.False(c4M02Add.IsMetadataFinal);
                Assert.False(c4M02Add.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventAdd, c4M02Add.MethodKind);
                Assert.Equal("void C4.I2.M02.add", c4M02Add.ToTestDisplayString());
                Assert.Equal("System.Action value", c4M02Add.Parameters.Single().ToTestDisplayString());
                Assert.Equal("System.Void", c4M02Add.ReturnTypeWithAnnotations.ToTestDisplayString());
                Assert.Same(m02.AddMethod, c4M02Add.ExplicitInterfaceImplementations.Single());
                Assert.Same(c4M02Add, c4.FindImplementationForInterfaceMember(m02.AddMethod));

                Assert.True(c4M02Remove.IsStatic);
                Assert.False(c4M02Remove.IsAbstract);
                Assert.False(c4M02Remove.IsVirtual);
                Assert.False(c4M02Remove.IsMetadataVirtual());
                Assert.False(c4M02Remove.IsMetadataFinal);
                Assert.False(c4M02Remove.IsMetadataNewSlot());
                Assert.Equal(MethodKind.EventRemove, c4M02Remove.MethodKind);
                Assert.Equal("void C4.I2.M02.remove", c4M02Remove.ToTestDisplayString());
                Assert.Equal("System.Action value", c4M02Remove.Parameters.Single().ToTestDisplayString());
                Assert.Equal("System.Void", c4M02Remove.ReturnTypeWithAnnotations.ToTestDisplayString());
                Assert.Same(m02.RemoveMethod, c4M02Remove.ExplicitInterfaceImplementations.Single());
                Assert.Same(c4M02Remove, c4.FindImplementationForInterfaceMember(m02.RemoveMethod));

                Assert.Same(c4M02, c4.GetMembers().OfType<EventSymbol>().Single());
                Assert.Equal(2, c4.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()).Count());
            }

            verifier.VerifyIL("C1.I1.add_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C1.M01.add""
  IL_0006:  ret
}
");

            verifier.VerifyIL("C1.I1.remove_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C1.M01.remove""
  IL_0006:  ret
}
");

            verifier.VerifyIL("C3.I2.add_M02",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C3.M02.add""
  IL_0006:  ret
}
");

            verifier.VerifyIL("C3.I2.remove_M02",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C3.M02.remove""
  IL_0006:  ret
}
");
        }

        [Fact]
        public void ImplementAbstractStaticEvent_15()
        {
            // A forwarding method isn't created if base class implements interface exactly the same way. 

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
    abstract static event System.Action M02;
}

public class C1
{
    public static event System.Action M01 { add => throw null; remove{} }
}

public class C2 : C1, I1
{
    static event System.Action I1.M02 { add => throw null; remove{} }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.RegularPreview, TestOptions.Regular9 })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                Assert.Empty(c3.GetMembers().OfType<EventSymbol>());
                Assert.Empty(c3.GetMembers().OfType<MethodSymbol>().Where(m => !m.IsConstructor()));

                var m01 = c3.Interfaces().Single().GetMembers("M01").OfType<EventSymbol>().Single();

                var c1M01 = c3.BaseType().BaseType().GetMember<EventSymbol>("M01");
                Assert.Equal("event System.Action C1.M01", c1M01.ToTestDisplayString());

                Assert.True(c1M01.IsStatic);
                Assert.False(c1M01.IsAbstract);
                Assert.False(c1M01.IsVirtual);

                Assert.Empty(c1M01.ExplicitInterfaceImplementations);

                var c1M01Add = c1M01.AddMethod;
                Assert.True(c1M01Add.IsStatic);
                Assert.False(c1M01Add.IsAbstract);
                Assert.False(c1M01Add.IsVirtual);
                Assert.False(c1M01Add.IsMetadataVirtual());
                Assert.False(c1M01Add.IsMetadataFinal);
                Assert.False(c1M01Add.IsMetadataNewSlot());

                Assert.Empty(c1M01Add.ExplicitInterfaceImplementations);

                var c1M01Remove = c1M01.RemoveMethod;
                Assert.True(c1M01Remove.IsStatic);
                Assert.False(c1M01Remove.IsAbstract);
                Assert.False(c1M01Remove.IsVirtual);
                Assert.False(c1M01Remove.IsMetadataVirtual());
                Assert.False(c1M01Remove.IsMetadataFinal);
                Assert.False(c1M01Remove.IsMetadataNewSlot());

                Assert.Empty(c1M01Remove.ExplicitInterfaceImplementations);

                if (c1M01.ContainingModule is PEModuleSymbol)
                {
                    var c2M01Add = c3.FindImplementationForInterfaceMember(m01.AddMethod);
                    Assert.Equal("void C2.I1.add_M01(System.Action value)", c2M01Add.ToTestDisplayString());

                    var c2M01Remove = c3.FindImplementationForInterfaceMember(m01.RemoveMethod);
                    Assert.Equal("void C2.I1.remove_M01(System.Action value)", c2M01Remove.ToTestDisplayString());

                    // Forwarding methods for accessors aren't tied to an event
                    Assert.Null(c3.FindImplementationForInterfaceMember(m01));
                }
                else
                {
                    Assert.Same(c1M01, c3.FindImplementationForInterfaceMember(m01));
                    Assert.Same(c1M01.AddMethod, c3.FindImplementationForInterfaceMember(m01.AddMethod));
                    Assert.Same(c1M01.RemoveMethod, c3.FindImplementationForInterfaceMember(m01.RemoveMethod));
                }

                var m02 = c3.Interfaces().Single().GetMembers("M02").OfType<EventSymbol>().Single();

                var c2M02 = c3.BaseType().GetMember<EventSymbol>("I1.M02");
                Assert.Equal("event System.Action C2.I1.M02", c2M02.ToTestDisplayString());
                Assert.Same(c2M02, c3.FindImplementationForInterfaceMember(m02));
                Assert.Same(c2M02.AddMethod, c3.FindImplementationForInterfaceMember(m02.AddMethod));
                Assert.Same(c2M02.RemoveMethod, c3.FindImplementationForInterfaceMember(m02.RemoveMethod));
            }
        }

        [Fact]
        public void ImplementAbstractStaticEvent_16()
        {
            // A new implicit implementation is properly considered.

            var source1 =
@"
public interface I1
{
    abstract static event System.Action M01;
}

public class C1 : I1
{
    public static event System.Action M01 { add{} remove => throw null; }
}

public class C2 : C1
{
    new public static event System.Action M01 { add{} remove => throw null; }
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp);

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C3 : C2, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                         parseOptions: parseOptions,
                                                         targetFramework: TargetFramework.NetCoreApp,
                                                         references: new[] { reference });
                    var verifier = CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();

                    verifier.VerifyIL("C3.I1.add_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C2.M01.add""
  IL_0006:  ret
}
");

                    verifier.VerifyIL("C3.I1.remove_M01",
@"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void C2.M01.remove""
  IL_0006:  ret
}
");
                }
            }

            void validate(ModuleSymbol module)
            {
                var c3 = module.GlobalNamespace.GetTypeMember("C3");
                var m01 = c3.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

                var c2M01 = c3.BaseType().GetMember<EventSymbol>("M01");
                var c2M01Add = c2M01.AddMethod;
                var c2M01Remove = c2M01.RemoveMethod;
                Assert.Equal("event System.Action C2.M01", c2M01.ToTestDisplayString());

                Assert.True(c2M01.IsStatic);
                Assert.False(c2M01.IsAbstract);
                Assert.False(c2M01.IsVirtual);
                Assert.Empty(c2M01.ExplicitInterfaceImplementations);

                Assert.True(c2M01Add.IsStatic);
                Assert.False(c2M01Add.IsAbstract);
                Assert.False(c2M01Add.IsVirtual);
                Assert.False(c2M01Add.IsMetadataVirtual());
                Assert.False(c2M01Add.IsMetadataFinal);
                Assert.False(c2M01Add.IsMetadataNewSlot());
                Assert.Empty(c2M01Add.ExplicitInterfaceImplementations);

                Assert.True(c2M01Remove.IsStatic);
                Assert.False(c2M01Remove.IsAbstract);
                Assert.False(c2M01Remove.IsVirtual);
                Assert.False(c2M01Remove.IsMetadataVirtual());
                Assert.False(c2M01Remove.IsMetadataFinal);
                Assert.False(c2M01Remove.IsMetadataNewSlot());
                Assert.Empty(c2M01Remove.ExplicitInterfaceImplementations);

                if (module is PEModuleSymbol)
                {
                    var c3M01 = (EventSymbol)c3.FindImplementationForInterfaceMember(m01);
                    // Forwarding methods for accessors aren't tied to an event
                    Assert.Null(c3M01);

                    var c3M01Add = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01.AddMethod);
                    Assert.Equal("void C3.I1.add_M01(System.Action value)", c3M01Add.ToTestDisplayString());
                    Assert.Same(m01.AddMethod, c3M01Add.ExplicitInterfaceImplementations.Single());

                    var c3M01Remove = (MethodSymbol)c3.FindImplementationForInterfaceMember(m01.RemoveMethod);
                    Assert.Equal("void C3.I1.remove_M01(System.Action value)", c3M01Remove.ToTestDisplayString());
                    Assert.Same(m01.RemoveMethod, c3M01Remove.ExplicitInterfaceImplementations.Single());
                }
                else
                {
                    Assert.Same(c2M01, c3.FindImplementationForInterfaceMember(m01));
                    Assert.Same(c2M01Add, c3.FindImplementationForInterfaceMember(m01.AddMethod));
                    Assert.Same(c2M01Remove, c3.FindImplementationForInterfaceMember(m01.RemoveMethod));
                }
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_19(bool genericFirst)
        {
            // An "ambiguity" in implicit/explicit implementation declared in generic base class.

            var generic =
@"
    public static event System.Action<T> M01 { add{} remove{} }
";
            var nonGeneric =
@"
    static event System.Action<int> I1.M01 { add{} remove{} }
";
            var source1 =
@"
public interface I1
{
    abstract static event System.Action<int> M01;
}

public class C1<T> : I1
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().OfType<EventSymbol>().Where(m => m.Name.Contains("M01")).Count());
            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (EventSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("event System.Action<System.Int32> C1<T>.I1.M01", c1M01.OriginalDefinition.ToTestDisplayString());
                Assert.Same(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(c1M01, c2.BaseType().FindImplementationForInterfaceMember(m01));
            }
        }

        [Theory]
        [CombinatorialData]
        public void ImplementAbstractStaticEvent_20(bool genericFirst)
        {
            // Same as ImplementAbstractStaticEvent_19 only interface is generic too.

            var generic =
@"
    static event System.Action<T> I1<T>.M01 { add{} remove{} }
";
            var nonGeneric =
@"
    public static event System.Action<int> M01 { add{} remove{} }
";
            var source1 =
@"
public interface I1<T>
{
    abstract static event System.Action<T> M01;
}

public class C1<T> : I1<T>
{
" + (genericFirst ? generic + nonGeneric : nonGeneric + generic) + @"
}
";

            var compilation1 = CreateCompilation(source1, options: TestOptions.DebugDll,
                                                 parseOptions: TestOptions.RegularPreview,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { CreateCompilation("", targetFramework: TargetFramework.NetCoreApp).ToMetadataReference() });

            Assert.Equal(2, compilation1.GlobalNamespace.GetTypeMember("C1").GetMembers().OfType<EventSymbol>().Where(m => m.Name.Contains("M01")).Count());

            compilation1.VerifyDiagnostics();

            var source2 =
@"
public class C2 : C1<int>, I1<int>
{
}
";

            foreach (var reference in new[] { compilation1.ToMetadataReference(), compilation1.EmitToImageReference() })
            {
                foreach (var parseOptions in new[] { TestOptions.Regular9, TestOptions.RegularPreview })
                {
                    var compilation2 = CreateCompilation(source2, options: TestOptions.DebugDll,
                                                 parseOptions: parseOptions,
                                                 targetFramework: TargetFramework.NetCoreApp,
                                                 references: new[] { reference });

                    CompileAndVerify(compilation2, sourceSymbolValidator: validate, symbolValidator: validate, verify: Verification.Skipped).VerifyDiagnostics();
                }
            }

            void validate(ModuleSymbol module)
            {
                var c2 = module.GlobalNamespace.GetTypeMember("C2");
                var m01 = c2.Interfaces().Single().GetMembers().OfType<EventSymbol>().Single();

                Assert.True(m01.ContainingModule is RetargetingModuleSymbol or PEModuleSymbol);

                var c1M01 = (EventSymbol)c2.FindImplementationForInterfaceMember(m01);
                Assert.Equal("event System.Action<T> C1<T>.I1<T>.M01", c1M01.OriginalDefinition.ToTestDisplayString());
                Assert.Equal(m01, c1M01.ExplicitInterfaceImplementations.Single());
                Assert.Same(c1M01, c2.BaseType().FindImplementationForInterfaceMember(m01));
            }
        }
    }
}
