﻿// <auto-generated/>

using System.Runtime.CompilerServices;
using Roslyn.Test.Utilities;

internal sealed class InitializeTestModule
{
    [ModuleInitializer]
    internal static void Initializer()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(TestBase).Module.ModuleHandle);
    }
}
