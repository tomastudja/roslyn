﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.CodeAnalysis.CSharp.Test.Utilities
{
    public static class TestOptions
    {
        // Disable documentation comments by default so that we don't need to
        // document every public member of every test input.
        public static readonly CSharpParseOptions Script = new CSharpParseOptions(kind: SourceCodeKind.Script, documentationMode: DocumentationMode.None);
        public static readonly CSharpParseOptions Regular = new CSharpParseOptions(kind: SourceCodeKind.Regular, documentationMode: DocumentationMode.None);
        public static readonly CSharpParseOptions Regular6 = Regular.WithLanguageVersion(LanguageVersion.CSharp6);
        public static readonly CSharpParseOptions RegularWithDocumentationComments = new CSharpParseOptions(kind: SourceCodeKind.Regular, documentationMode: DocumentationMode.Diagnose);

        // enable pattern-switch translation even for switches that use no new syntax.
        public static readonly CSharpParseOptions RegularWithPatterns = Regular.WithFeatures(new Dictionary<string, string>() { { "typeswitch", "true" } });

        public static readonly CSharpCompilationOptions ReleaseDll = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release).WithExtendedCustomDebugInformation(true);
        public static readonly CSharpCompilationOptions ReleaseExe = new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release).WithExtendedCustomDebugInformation(true);

        public static readonly CSharpCompilationOptions ReleaseDebugDll = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release).
            WithExtendedCustomDebugInformation(true).
            WithDebugPlusMode(true);

        public static readonly CSharpCompilationOptions ReleaseDebugExe = new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release).
            WithExtendedCustomDebugInformation(true).
            WithDebugPlusMode(true);

        public static readonly CSharpCompilationOptions DebugDll = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug).WithExtendedCustomDebugInformation(true);
        public static readonly CSharpCompilationOptions DebugExe = new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Debug).WithExtendedCustomDebugInformation(true);

        public static readonly CSharpCompilationOptions ReleaseWinMD = new CSharpCompilationOptions(OutputKind.WindowsRuntimeMetadata, optimizationLevel: OptimizationLevel.Release).WithExtendedCustomDebugInformation(true);
        public static readonly CSharpCompilationOptions DebugWinMD = new CSharpCompilationOptions(OutputKind.WindowsRuntimeMetadata, optimizationLevel: OptimizationLevel.Debug).WithExtendedCustomDebugInformation(true);

        public static readonly CSharpCompilationOptions ReleaseModule = new CSharpCompilationOptions(OutputKind.NetModule, optimizationLevel: OptimizationLevel.Release);
        public static readonly CSharpCompilationOptions DebugModule = new CSharpCompilationOptions(OutputKind.NetModule, optimizationLevel: OptimizationLevel.Debug);

        public static readonly CSharpCompilationOptions UnsafeReleaseDll = ReleaseDll.WithAllowUnsafe(true);
        public static readonly CSharpCompilationOptions UnsafeReleaseExe = ReleaseExe.WithAllowUnsafe(true);

        public static readonly CSharpCompilationOptions UnsafeDebugDll = DebugDll.WithAllowUnsafe(true);
        public static readonly CSharpCompilationOptions UnsafeDebugExe = DebugExe.WithAllowUnsafe(true);

        public static CSharpParseOptions WithStrictFeature(this CSharpParseOptions options)
        {
            return options.WithFeatures(options.Features.Concat(new[] { new KeyValuePair<string, string>("strict", "true") }));
        }

        internal static CSharpParseOptions WithExperimental(this CSharpParseOptions options, params MessageID[] features)
        {
            if (features.Length == 0)
            {
                throw new InvalidOperationException("Need at least one feature to enable");
            }

            var list = new List<KeyValuePair<string, string>>();
            foreach (var feature in features)
            {
                var name = feature.RequiredFeature();
                if (name == null)
                {
                    throw new InvalidOperationException($"{feature} is not a valid experimental feature");
                }

                list.Add(new KeyValuePair<string, string>(name, "true"));
            }

            return options.WithFeatures(options.Features.Concat(list));
        }
    }
}

