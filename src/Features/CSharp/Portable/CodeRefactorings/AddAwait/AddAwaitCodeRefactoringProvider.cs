﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CodeRefactorings.AddAwait;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait
{
    /// <summary>
    /// This refactoring complements the AddAwait fixer. It allows adding `await` and `await ... .ConfigureAwait(false)` even there is no compiler error to trigger the fixer.
    /// </summary>
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = PredefinedCodeRefactoringProviderNames.AddAwait), Shared]
    internal partial class CSharpAddAwaitCodeRefactoringProvider : AbstractAddAwaitCodeRefactoringProvider<InvocationExpressionSyntax>
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public CSharpAddAwaitCodeRefactoringProvider()
        {
        }

        protected override string GetTitle()
            => CSharpFeaturesResources.Add_await;

        protected override string GetTitleWithConfigureAwait()
            => CSharpFeaturesResources.Add_Await_and_ConfigureAwaitFalse;
    }
}
