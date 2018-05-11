﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings.InvertIf
{
    public partial class InvertIfTests
    {
        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless01()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (!c)
                return;
            f();
        }
    }
}",
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            if (c)
                f();
            else
                return;
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless02()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (!c)
            {
                continue;
            }
            f();
        }
    }
}",
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (c)
            {
                f();
            }
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless03()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (c)
            {
                f();
            }
        }
    }
}",
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (!c)
            {
                continue;
            }

            f();
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless04()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (c)
                break;
            return;
        }
    }
}",
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (!c)
                return;
            break;
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless05()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (!c)
            {
                return;
            }
            break;
        }
    }
}",
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            [||]if (c)
            {
                break;
            }
            return;
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless06()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            {
                [||]if (c)
                {
                    f();
                }
            }
        }
    }
}",
@"class C
{
    void M()
    {
        foreach (var item in list)
        {
            {
                [||]if (!c)
                {
                    continue;
                }

                f();
            }
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless07()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        [||]if (c)
        {
            f();
        }
    }
}",
@"class C
{
    void M()
    {
        [||]if (!c)
        {
            return;
        }

        f();
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless08()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                [||]if (c)
                {
                    f();
                    f();
                }

                break;
        }
    }
}",
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                [||]if (!c)
                {
                    break;
                }

                f();
                f();

                break;
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless09()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                [||]if (c)
                {
                    if (c)
                    {
                        return 1;
                    }
                }

                return 2;
        }
    }
}",
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                [||]if (!c)
                {
                    return 2;
                }

                if (c)
                {
                    return 1;
                }

                return 2;
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless10()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                if (c)
                {
                    [||]if (c)
                    {
                        return 1;
                    }
                }
                return 2;
        }
    }
}",
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                if (c)
                {
                    [||]if (!c)
                    {
                    }
                    else
                    {
                        return 1;
                    }
                }
                return 2;
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless11()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                [||]if (c)
                {
                    f();
                }
                g();
                g();
                break;
        }
    }
}",
@"class C
{
    void M()
    {
        switch (o)
        {
            case 1:
                [||]if (!c)
                {
                }
                else
                {
                    f();
                }
                g();
                g();
                break;
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless12()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        while (c)
        {
            if (c)
            {
                [||]if (c)
                {
                    continue;
                }
                if (c())
                    return;
            }
        }
    }
}",
@"class C
{
    void M()
    {
        while (c)
        {
            if (c)
            {
                [||]if (!c)
                {
                    if (c())
                        return;
                }
            }
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless13()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        while (c)
        {
            {
                [||]if (c)
                {
                    continue;
                }
                if (c())
                    return;
            }
        }
    }
}",
@"class C
{
    void M()
    {
        while (c)
        {
            {
                [||]if (!c)
                {
                    if (c())
                        return;
                }
            }
        }
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless14()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        [||]if (c)
        {
            f();
        }
        g();
        g();
    }
}",
@"class C
{
    void M()
    {
        if (!c)
        {
        }
        else
        {
            f();
        }
        g();
        g();
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless15()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    bool M()
    {
        if (c)
        {
            [||]if (c)
            {
                f();
            }
            g();
        }
        return false;
    }
}",
@"class C
{
    bool M()
    {
        if (c)
        {
            if (!c)
            {
            }
            else
            {
                f();
            }
            g();
        }
        return false;
    }
}");
        }

        [Fact, Trait(Traits.Feature, Traits.Features.CodeActionsInvertIf)]
        public async Task TestElseless16()
        {
            await TestInRegularAndScriptAsync(
@"class C
{
    void M()
    {
        [||]if (c)
        {
            f();
        }

        g();
        g();
    }
}",
@"class C
{
    void M()
    {
        if (!c)
        {
        }
        else
        {
            f();
        }

        g();
        g();
    }
}");
        }
    }
}
