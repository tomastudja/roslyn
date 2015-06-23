﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;
using System;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Specifies output assembly kinds generated by compiler.
    /// </summary>
    public enum OutputKind
    {
        /// <summary>
        /// An .exe with an entry point and a console.
        /// </summary>
        ConsoleApplication = 0,

        /// <summary>
        /// An .exe with an entry point but no console.
        /// </summary>
        WindowsApplication = 1,

        /// <summary>
        /// A .dll file.
        /// </summary>
        DynamicallyLinkedLibrary = 2,

        /// <summary>
        /// A .netmodule file.
        /// </summary>
        NetModule = 3,

        /// <summary>
        /// A .winmdobj file.
        /// </summary>
        WindowsRuntimeMetadata = 4,

        /// <summary>
        /// An .exe that can run in an app container.
        /// </summary>
        /// <remarks>
        /// Equivalent to a WindowsApplication, but with an extra bit set in the Portable Executable file
        /// so that the application can only be run in an app container.
        /// Also known as a "Windows Store app".
        /// </remarks>
        WindowsRuntimeApplication = 5,
    }

    internal static partial class EnumBounds
    {
        internal static bool IsValid(this OutputKind value)
        {
            return value >= OutputKind.ConsoleApplication && value <= OutputKind.WindowsRuntimeApplication;
        }

        internal static string GetDefaultExtension(this OutputKind kind)
        {
            switch (kind)
            {
                case OutputKind.ConsoleApplication:
                case OutputKind.WindowsApplication:
                case OutputKind.WindowsRuntimeApplication:
                    return ".exe";

                case OutputKind.DynamicallyLinkedLibrary:
                    return ".dll";

                case OutputKind.NetModule:
                    return ".netmodule";

                case OutputKind.WindowsRuntimeMetadata:
                    return ".winmdobj";

                default:
                    return ".dll";
            }
        }

        internal static bool IsApplication(this OutputKind kind)
        {
            switch (kind)
            {
                case OutputKind.ConsoleApplication:
                case OutputKind.WindowsApplication:
                case OutputKind.WindowsRuntimeApplication:
                    return true;

                case OutputKind.DynamicallyLinkedLibrary:
                case OutputKind.NetModule:
                case OutputKind.WindowsRuntimeMetadata:
                    return false;

                default:
                    return false;
            }
        }

        internal static bool IsNetModule(this OutputKind kind)
        {
            return kind == OutputKind.NetModule;
        }

        internal static bool IsWindowsRuntime(this OutputKind kind)
        {
            return kind == OutputKind.WindowsRuntimeMetadata;
        }
    }
}
