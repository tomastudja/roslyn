﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.CSharp.EmbeddedLanguages.LanguageServices;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.CodeAnalysis.CSharp.Classification
{
    [ExportEmbeddedLanguageClassifier(PredefinedEmbeddedLanguageClassifierNames.Fallback, LanguageNames.CSharp), Shared]
    internal class CSharpFallbackEmbeddedLanguageClassifier : AbstractFallbackEmbeddedLanguageClassifier
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public CSharpFallbackEmbeddedLanguageClassifier()
            : base(CSharpEmbeddedLanguagesProvider.Info)
        {
        }
    }
}
