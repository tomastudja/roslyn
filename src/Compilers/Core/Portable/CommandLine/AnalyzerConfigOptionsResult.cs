﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using AnalyzerOptions = System.Collections.Immutable.ImmutableDictionary<string, string>;
using TreeOptions = System.Collections.Immutable.ImmutableDictionary<string, Microsoft.CodeAnalysis.ReportDiagnostic>;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Holds results from <see cref="AnalyzerConfigSet.GetOptionsForSourcePath(string)"/>.
    /// </summary>
    public readonly struct AnalyzerConfigOptionsResult
    {
        /// <summary>
        /// Options that customize diagnostic severity as reported by the compiler.
        /// </summary>
        public TreeOptions TreeOptions { get; }

        /// <summary>
        /// Options that do not have any special compiler behavior and are passed to analyzers as-is.
        /// </summary>
        public AnalyzerOptions AnalyzerOptions { get; }

        /// <summary>
        /// Any produced diagnostics while applying analyzer configuration.
        /// </summary>
        public ImmutableArray<Diagnostic> Diagnostics { get; }

        internal AnalyzerConfigOptionsResult(
            TreeOptions treeOptions,
            AnalyzerOptions analyzerOptions,
            ImmutableArray<Diagnostic> diagnostics)
        {
            Debug.Assert(treeOptions != null);
            Debug.Assert(analyzerOptions != null);

            TreeOptions = treeOptions;
            AnalyzerOptions = analyzerOptions;
            Diagnostics = diagnostics;
        }
    }
}
