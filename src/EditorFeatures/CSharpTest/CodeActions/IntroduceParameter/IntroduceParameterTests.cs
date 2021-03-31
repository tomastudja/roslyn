﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.IntroduceVariable;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeActions.IntroduceParameter
{
    public class IntroduceParameterTests : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, TestParameters parameters)
            => new CSharpIntroduceParameterCodeRefactoringProvider();

        protected override ImmutableArray<CodeAction> MassageActions(ImmutableArray<CodeAction> actions)
            => FlattenActions(actions);

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionWithNoMethodCallsCase()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z) 
    {
        int m = [|x * y * z;|]
    }
}";

            var expected =
@"using System;
class TestClass
{
    void M(int x, int y, int z, int m) 
    {
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithLocal()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z) 
    {
        int l = 5;
        int m = [|l * y * z;|]
    }
}";

            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithSingleMethodCall()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = [|x * z * z|];
    }

    void M1(int x, int y, int z) 
    {
        M(z, x, z);
    }
}";

            var expected =
@"using System;
class TestClass
{
    void M(int x, int y, int z, int m)
    {
    }

    void M1(int x, int y, int z) 
    {
        M(z, x, z, z * z * z);
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestLocalDeclarationMultipleDeclarators()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = [|x * z * z|], y = 0;
    }

    void M1(int x, int y, int z) 
    {
        M(z, x, z);
    }
}";

            var expected =
@"using System;
class TestClass
{
    void M(int x, int y, int z, int v)
    {
        int m = {|Rename:v|}, y = 0;
    }

    void M1(int x, int y, int z) 
    {
        M(z, x, z, z * z * z);
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithSingleMethodCallInLocalFunction()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = M2(x, z);
                        
        int M2(int x, int y)
        {
            int val = [|x * y|];
            return val;
        }
    }
}";

            var expected =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = M2(x, z, x * z);
                        
        int M2(int x, int y, int val)
        {
            return val;
        }
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithSingleMethodCallInStaticLocalFunction()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = M2(x, z);
                        
        static int M2(int x, int y)
        {
            int val = [|x * y|];
            return val;
        }
    }
}";

            var expected =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = M2(x, z, x * z);
                        
        static int M2(int x, int y, int val)
        {
            return val;
        }
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestHighlightIncompleteExpressionCaseWithSingleMethodCall()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = 5 * [|x * y * z|];
    }

    void M1(int x, int y, int z) 
    {
        M(z, y, x);
    }
}";

            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithMultipleMethodCall()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = [|x * y * z|];
    }

    void M1(int x, int y, int z) 
    {
        M(a + b, 5, x);
        M(z, y, x);
    }
}";

            var expected =
@"using System;
class TestClass
{
    void M(int x, int y, int z, int m)
    {
    }

    void M1(int x, int y, int z) 
    {
        M(a + b, 5, x, (a + b) * 5 * x);
        M(z, y, x, z * y * x);
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionAllOccurrences()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = x * y * z;
        int f = [|x * y * z|];
    }

    void M1(int x, int y, int z) 
    {
        M(a + b, 5, x);
    }
}";

            var expected =
@"using System;
class TestClass
{
    void M(int x, int y, int z, int f)
    {
        int m = f;
    }

    void M1(int x, int y, int z) 
    {
        M(a + b, 5, x, (a + b) * 5 * x);
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 1);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionWithNoMethodCallTrampoline()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z) 
    {
        int m = [|x * y * z;|]
    }
}";

            var expected =
@"using System;
class TestClass
{
    private int M_m(int x, int y, int z)
    {
        return x * y * z;
    }

    void M(int x, int y, int z, int m) 
    {
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 2);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionWithSingleMethodCallTrampoline()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = [|y * x|];
    }

    void M1(int x, int y, int z) 
    {
        M(z, y, x);
    }
}";

            var expected =
@"using System;
class TestClass
{
    private int M_m(int x, int y)
    {
        return y * x;
    }

    void M(int x, int y, int z, int m)
    {
    }

    void M1(int x, int y, int z) 
    {
        M(z, y, x, M_m(z, y));
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 1, options: new OptionsCollection(GetLanguage()), parseOptions: CSharpParseOptions.Default);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionWithSingleMethodCallTrampolineAllOccurrences()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = [|x * y * z|];
        int l = x * y * z;
    }

    void M1(int x, int y, int z) 
    {
        M(z, y, x);
    }
}";

            var expected =
@"using System;
class TestClass
{
    private int M_m(int x, int y, int z)
    {
        return x * y * z;
    }

    void M(int x, int y, int z, int m)
    {
        int l = m;
    }

    void M1(int x, int y, int z) 
    {
        M(z, y, x, M_m(z, y, x));
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 3, options: new OptionsCollection(GetLanguage()), parseOptions: CSharpParseOptions.Default);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionWithNoMethodCallOverload()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z) 
    {
        int m = [|x * y * z;|]
    }
}";

            var expected =
@"using System;
class TestClass
{
    private void M(int x, int y, int z)
    {
        M(x, y, z, x * y * z);
    }

    void M(int x, int y, int z, int m) 
    {
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 4);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionWithSingleMethodCallOverload()
        {
            var code =
@"using System;
class TestClass
{
    void M(int x, int y, int z)
    {
        int m = [|x * y * z|];
    }

    void M1(int x, int y, int z) 
    {
        M(z, y, x);
    }
}";

            var expected =
@"using System;
class TestClass
{
    private void M(int x, int y, int z)
    {
        M(x, y, z, x * y * z);
    }

    void M(int x, int y, int z, int m)
    {
    }

    void M1(int x, int y, int z) 
    {
        M(z, y, x);
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 4);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestExpressionBodiedMemberOverload()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y, int z) => [|x * y * z|];

    void M1(int x, int y, int z)
    {
        int prod = M(z, y, x);
    }
}";

            var expected =
@"using System;
class TestClass
{
    private int M(int x, int y, int z)
    {
        return M(x, y, z, x * y * z);
    }

    int M(int x, int y, int z, int v) => {|Rename:v|};

    void M1(int x, int y, int z)
    {
        int prod = M(z, y, x);
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 4);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestExpressionBodiedMemberTrampoline()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y, int z) => [|x * y * z|];

    void M1(int x, int y, int z)
    {
        int prod = M(z, y, x);
    }
}";

            var expected =
@"using System;
class TestClass
{
    private int M_v(int x, int y, int z)
    {
        return x * y * z;
    }

    int M(int x, int y, int z, int v) => {|Rename:v|};

    void M1(int x, int y, int z)
    {
        int prod = M(z, y, x, M_v(z, y, x));
    }
}";

            await TestInRegularAndScriptAsync(code, expected, index: 2);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithRecursiveCall()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y, int z)
    {
        int m = [|x * y * z|];
        return M(x, x, z);
    }
}";

            var expected =
@"using System;
class TestClass
{
    int M(int x, int y, int z, int m)
    {
        return M(x, x, z, x * x * z);
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithNestedRecursiveCall()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y, int z)
    {
        int m = [|x * y * z|];
        return M(x, x, M(x, y, z));
    }
}";

            var expected =
@"using System;
class TestClass
{
    int M(int x, int y, int z, int m)
    {
        return M(x, x, M(x, y, z, x * y * z), x * x * M(x, y, z, x * y * z));
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithParamsArg()
        {
            var code =
@"using System;
class TestClass
{
    int M(params int[] args)
    {
        int m = [|args[0] + args[1]|];
        return m;
    }

    void M1()
    {
        M(5, 6, 7);
    }
}";
            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithOptionalParameters()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y = 5)
    {
        int m = [|x * y|];
        return m;
    }

    void M1()
    {
        M(5, 3);
    }
}";

            var expected =
@"using System;
class TestClass
{
    int M(int x, int m, int y = 5)
    {
        return m;
    }

    void M1()
    {
        M(5, 5 * 3, 3);
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithOptionalParametersUsed()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y = 5)
    {
        int m = [|x * y|];
        return m;
    }

    void M1()
    {
        M(7);
    }
}";

            var expected =
@"using System;
class TestClass
{
    int M(int x, int m, int y = 5)
    {
        return m;
    }

    void M1()
    {
        M(7, m: 7 * 5);
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithCancellationToken()
        {
            var code =
@"using System;
using System.Threading;
class TestClass
{
    int M(int x, CancellationToken cancellationToken)
    {
        int m = [|x * x|];
        return m;
    }

    void M1(CancellationToken cancellationToken)
    {
        M(7, cancellationToken);
    }
}";

            var expected =
@"using System;
using System.Threading;
class TestClass
{
    int M(int x, int m, CancellationToken cancellationToken)
    {
        return m;
    }

    void M1(CancellationToken cancellationToken)
    {
        M(7, 7 * 7, cancellationToken);
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithRecursiveCallTrampoline()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y, int z)
    {
        int m = [|x * y * z|];
        return M(x, x, z);
    }
}";

            var expected =
@"using System;
class TestClass
{
    private int M_m(int x, int y, int z)
    {
        return x * y * z;
    }

    int M(int x, int y, int z, int m)
    {
        return M(x, x, z, M_m(x, x, z));
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 2);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseWithNestedRecursiveCallTrampoline()
        {
            var code =
@"using System;
class TestClass
{
    int M(int x, int y, int z)
    {
        int m = [|x * y * z|];
        return M(x, x, M(x, y, x));
    }
}";

            var expected =
@"using System;
class TestClass
{
    private int M_m(int x, int y, int z)
    {
        return x * y * z;
    }

    int M(int x, int y, int z, int m)
    {
        return M(x, x, M(x, y, x, M_m(x, y, x)), M_m(x, x, M(x, y, x, M_m(x, y, x))));
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 2);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestSimpleExpressionCaseInConstructor()
        {
            var code =
@"using System;
class TestClass
{
    public TestClass(int x, int y)
    {
        Math.Max([|x + y|], x * y);
    }
}";

            var expected =
@"using System;
class TestClass
{
    public TestClass(int x, int y, int val1)
    {
        Math.Max({|Rename:val1|}, x * y);
    }
}";
            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestLambdaCaseNormal()
        {
            var code =
@"using System;
class TestClass
{
    Func<int, int, int> mult = (x, y) => [|x * y|];
}";

            var expected =
@"using System;
class TestClass
{
    Func<int, int, int> mult = (x, y, int v) => v;
}";
            await TestInRegularAndScriptAsync(code, expected, index: 0);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestLambdaCaseTrampoline()
        {
            var code =
@"using System;
class TestClass
{
    Func<int, int, int> mult = (x, y) => [|x * y|];
}";

            var expected =
@"using System;
class TestClass
{
    Func<int, int, int>
    Func<int, int, int> mult = (x, y, int v) => v;
}";
            await TestInRegularAndScriptAsync(code, expected, index: 2);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestLambdaCaseOverload()
        {
            var code =
@"using System;
class TestClass
{
    Func<int, int, int> mult = (x, y) => [|x * y|];
}";

            var expected =
@"using System;
class TestClass
{
    Func<int, int, int> mult = (x, y, int v) => v;
}";
            await TestInRegularAndScriptAsync(code, expected, index: 4);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestTopLevelStatements()
        {
            var code =
@"using System;
Math.Max(5 + 5, [|6 + 7|]);";

            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestFieldInitializer()
        {
            var code =
@"using System;
class TestClass
{
    int a = [|5 + 3|];
}";
            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestIndexer()
        {
            var code =
@"using System;
class SampleCollection<T>
{
    private T[] arr = new T[100];

    public T this[int i] => arr[[|i + 5|]];
}";
            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestPropertyGetter()
        {
            var code =
@"using System;

class TimePeriod
{
   private double _seconds;

   public double Hours
   {
       get { return [|_seconds / 3600|]; }
   }
}";
            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestPropertySetter()
        {
            var code =
@"using System;

class TimePeriod
{
   private double _seconds;

   public double Hours
   {
       set {
          _seconds = [|value * 3600|];
        }
    }
}";
            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestDestructor()
        {
            var code =
@"using System;
class TestClass
{
    public ~TestClass()
    {
        Math.Max([|5 + 5|], 5 * 5);
    }
}";
            await TestMissingInRegularAndScriptAsync(code);
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsIntroduceParameter)]
        public async Task TestExpressionInParameter()
        {
            var code =
@"using System;
class TestClass
{
    public void M(int x = [|5 * 5|])
    {
    }
}";
            await TestMissingInRegularAndScriptAsync(code);
        }
    }
}
