// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.GenerateEqualsAndGetHashCodeFromMembers;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Roslyn.Test.Utilities;
using Xunit;
using Microsoft.CodeAnalysis.PickMembers;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.GenerateEqualsAndGetHashCodeFromMembers
{
    public class GenerateEqualsAndGetHashCodeFromMembersTests : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, object fixProviderData)
            => new GenerateEqualsAndGetHashCodeFromMembersCodeRefactoringProvider((IPickMembersService)fixProviderData);

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsSingleField()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program
{
    [|int a;|]
}",
@"using System.Collections.Generic;

class Program
{
    int a;

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null && a == program.a;
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestReferenceIEquatable()
        {
            await TestAsync(
@"
using System;
using System.Collections.Generic;

class S : IEquatable<S> { }

class Program
{
    [|S a;|]
}",
@"
using System;
using System.Collections.Generic;

class S : IEquatable<S> { }

class Program
{
    S a;

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null && EqualityComparer<S>.Default.Equals(a, program.a);
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestValueIEquatable()
        {
            await TestAsync(
@"
using System;
using System.Collections.Generic;

struct S : IEquatable<S> { }

class Program
{
    [|S a;|]
}",
@"
using System;
using System.Collections.Generic;

struct S : IEquatable<S> { }

class Program
{
    S a;

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null && a.Equals(program.a);
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsLongName()
        {
            await TestAsync(
@"using System.Collections.Generic;

class ReallyLongName
{
    [|int a;|]
}",
@"using System.Collections.Generic;

class ReallyLongName
{
    int a;

    public override bool Equals(object obj)
    {
        var name = obj as ReallyLongName;
        return name != null && a == name.a;
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsKeywordName()
        {
            await TestAsync(
@"using System.Collections.Generic;

class ReallyLongLong
{
    [|long a;|]
}",
@"using System.Collections.Generic;

class ReallyLongLong
{
    long a;

    public override bool Equals(object obj)
    {
        var @long = obj as ReallyLongLong;
        return @long != null && a == @long.a;
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsProperty()
        {
            await TestAsync(
@"using System.Collections.Generic;

class ReallyLongName
{
    [|int a;

    string B { get; }|]
}",
@"using System.Collections.Generic;

class ReallyLongName
{
    int a;

    string B { get; }

    public override bool Equals(object obj)
    {
        var name = obj as ReallyLongName;
        return name != null && a == name.a && B == name.B;
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsBaseTypeWithNoEquals()
        {
            await TestAsync(
@"class Base
{
}

class Program : Base
{
    [|int i;|]
}",
@"class Base
{
}

class Program : Base
{
    int i;

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null && i == program.i;
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsBaseWithOverriddenEquals()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Base
{
    public override bool Equals(object o)
    {
    }
}

class Program : Base
{
    [|int i;

    string S { get; }|]
}",
@"using System.Collections.Generic;

class Base
{
    public override bool Equals(object o)
    {
    }
}

class Program : Base
{
    int i;

    string S { get; }

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null &&
               base.Equals(obj) &&
               i == program.i &&
               S == program.S;
    }
}",
index: 0, compareTokens: false);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsOverriddenDeepBase()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Base
{
    public override bool Equals(object o)
    {
    }
}

class Middle : Base
{
}

class Program : Middle
{
    [|int i;

    string S { get; }|]
}",
@"using System.Collections.Generic;

class Base
{
    public override bool Equals(object o)
    {
    }
}

class Middle : Base
{
}

class Program : Middle
{
    int i;

    string S { get; }

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null && base.Equals(obj) &&
               i == program.i && S == program.S;
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsStruct()
        {
            await TestAsync(
@"using System.Collections.Generic;

struct ReallyLongName
{
    [|int i;

    string S { get; }|]
}",
@"using System.Collections.Generic;

struct ReallyLongName
{
    int i;

    string S { get; }

    public override bool Equals(object obj)
    {
        if (!(obj is ReallyLongName))
        {
            return false;
        }

        var name = (ReallyLongName)obj;
        return i == name.i && S == name.S;
    }
}",
index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestEqualsGenericType()
        {
            var code = @"
using System.Collections.Generic;
class Program<T>
{
    [|int i;|]
}
";

            var expected = @"
using System.Collections.Generic;
class Program<T>
{
    int i;

    public override bool Equals(object obj)
    {
        var program = obj as Program<T>;
        return program != null &&
               i == program.i;
    }
}
";

            await TestAsync(code, expected, compareTokens: false);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeSingleField1()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program
{
    [|int i;|]
}",
@"using System.Collections.Generic;

class Program
{
    int i;

    public override int GetHashCode()
    {
        return -1936752161 + i.GetHashCode();
    }
}",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeSingleField2()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program
{
    [|int j;|]
}",
@"using System.Collections.Generic;

class Program
{
    int j;

    public override int GetHashCode()
    {
        return -415617866 + j.GetHashCode();
    }
}",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeWithBaseHashCode1()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Base {
    public override int GetHashCode() => 0;
}

class Program : Base
{
    [|int j;|]
}",
@"using System.Collections.Generic;

class Base {
    public override int GetHashCode() => 0;
}

class Program : Base
{
    int j;

    public override int GetHashCode()
    {
        var hashCode = -842352698;
        hashCode = hashCode * -1521134295 + base.GetHashCode();
        hashCode = hashCode * -1521134295 + j.GetHashCode();
        return hashCode;
    }
}",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeWithBaseHashCode2()
        {
            await TestWithPickMembersDialogAsync(
@"using System.Collections.Generic;

class Base {
    public override int GetHashCode() => 0;
}

class Program : Base
{
    int j;
    [||]
}",
@"using System.Collections.Generic;

class Base {
    public override int GetHashCode() => 0;
}

class Program : Base
{
    int j;

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }
}",
chosenSymbols: new string[] { },
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeSingleField_CodeStyle1()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program
{
    [|int i;|]
}",
@"using System.Collections.Generic;

class Program
{
    int i;

    public override int GetHashCode() => -1936752161 + i.GetHashCode();
}",
index: 1,
options: Option(CSharpCodeStyleOptions.PreferExpressionBodiedMethods, CodeStyleOptions.TrueWithNoneEnforcement));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeTypeParameter()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program<T>
{
    [|T i;|]
}",
@"using System.Collections.Generic;

class Program<T>
{
    T i;

    public override int GetHashCode()
    {
        return -1936752161 + EqualityComparer<T>.Default.GetHashCode(i);
    }
}",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeGenericType()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program<T>
{
    [|Program<T> i;|]
}",
@"using System.Collections.Generic;

class Program<T>
{
    Program<T> i;

    public override int GetHashCode()
    {
        return -1936752161 + EqualityComparer<Program<T>>.Default.GetHashCode(i);
    }
}",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestGetHashCodeMultipleMembers()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program
{
    [|int i;

    string S { get; }|]
}",
@"using System.Collections.Generic;

class Program
{
    int i;

    string S { get; }

    public override int GetHashCode()
    {
        var hashCode = 1515862476;
        hashCode = hashCode * -1521134295 + i.GetHashCode();
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(S);
        return hashCode;
    }
}",
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestSmartTagText1()
        {
            await TestSmartTagTextAsync(
@"using System.Collections.Generic;

class Program
{
    [|bool b;
    HashSet<string> s;|]

    public Program(bool b)
    {
        this.b = b;
    }
}",
FeaturesResources.Generate_Equals_object);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestSmartTagText2()
        {
            await TestSmartTagTextAsync(
@"using System.Collections.Generic;

class Program
{
    [|bool b;
    HashSet<string> s;|]

    public Program(bool b)
    {
        this.b = b;
    }
}",
FeaturesResources.Generate_GetHashCode,
index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestSmartTagText3()
        {
            await TestSmartTagTextAsync(
@"using System.Collections.Generic;

class Program
{
    [|bool b;
    HashSet<string> s;|]

    public Program(bool b)
    {
        this.b = b;
    }
}",
FeaturesResources.Generate_Both,
index: 2);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task Tuple_Disabled()
        {
            await TestAsync(
@"using System.Collections.Generic;

class C
{
    [|(int, string) a;|]
}",
@"using System.Collections.Generic;

class C
{
    (int, string) a;

    public override bool Equals(object obj)
    {
        var c = obj as C;
        return c != null && EqualityComparer<(int, string)>.Default.Equals(a, c.a);
    }
}",
index: 0,
                parseOptions: TestOptions.Regular.WithLanguageVersion(CodeAnalysis.CSharp.LanguageVersion.CSharp6));
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task Tuples_Equals()
        {
            await TestAsync(
@"using System.Collections.Generic;

class C
{
    [|(int, string) a;|]
}",
@"using System.Collections.Generic;

class C
{
    (int, string) a;

    public override bool Equals(object obj)
    {
        var c = obj as C;
        return c != null && EqualityComparer<(int, string)>.Default.Equals(a, c.a);
    }
}",
index: 0,
parseOptions: TestOptions.Regular, withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TupleWithNames_Equals()
        {
            await TestAsync(
@"using System.Collections.Generic;

class C
{
    [|(int x, string y) a;|]
}",
@"using System.Collections.Generic;

class C
{
    (int x, string y) a;

    public override bool Equals(object obj)
    {
        var c = obj as C;
        return c != null && EqualityComparer<(int x, string y)>.Default.Equals(a, c.a);
    }
}",
index: 0,
parseOptions: TestOptions.Regular, withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task Tuple_HashCode()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program
{
    [|(int, string) i;|]
}",
@"using System.Collections.Generic;

class Program
{
    (int, string) i;

    public override int GetHashCode()
    {
        return -1936752161 + i.GetHashCode();
    }
}",
index: 1,
parseOptions: TestOptions.Regular, withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TupleWithNames_HashCode()
        {
            await TestAsync(
@"using System.Collections.Generic;

class Program
{
    [|(int x, string y) i;|]
}",
@"using System.Collections.Generic;

class Program
{
    (int x, string y) i;

    public override int GetHashCode()
    {
        return -1936752161 + i.GetHashCode();
    }
}",
index: 1,
parseOptions: TestOptions.Regular,
withScriptOption: true);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestWithDialog1()
        {
            await TestWithPickMembersDialogAsync(
@"using System.Collections.Generic;

class Program
{
    int a;
    string b;
    [||]
}",
@"using System.Collections.Generic;

class Program
{
    int a;
    string b;

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null &&
               a == program.a &&
               b == program.b;
    }
}",
chosenSymbols: new[] { "a", "b" },
compareTokens: false);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestWithDialog2()
        {
            await TestWithPickMembersDialogAsync(
@"using System.Collections.Generic;

class Program
{
    int a;
    string b;
    bool c;
    [||]
}",
@"using System.Collections.Generic;

class Program
{
    int a;
    string b;
    bool c;

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null &&
               c == program.c &&
               b == program.b;
    }
}",
chosenSymbols: new[] { "c", "b" },
compareTokens: false);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsGenerateEqualsAndGetHashCode)]
        public async Task TestWithDialog3()
        {
            await TestWithPickMembersDialogAsync(
@"using System.Collections.Generic;

class Program
{
    int a;
    string b;
    bool c;
    [||]
}",
@"using System.Collections.Generic;

class Program
{
    int a;
    string b;
    bool c;

    public override bool Equals(object obj)
    {
        var program = obj as Program;
        return program != null;
    }
}",
chosenSymbols: new string[] { },
compareTokens: false);
        }
    }
}