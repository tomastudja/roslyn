﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.LiveShare.Client.Debugging
{
    [ExportContentTypeLanguageService(ContentTypeNames.CSharpLspContentTypeName, StringConstants.CSharpLspLanguageName), Shared]
    internal class CSharpLspContentTypeLanguageService : IContentTypeLanguageService
    {
        private readonly IContentTypeRegistryService _contentTypeRegistry;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public CSharpLspContentTypeLanguageService(IContentTypeRegistryService contentTypeRegistry)
            => _contentTypeRegistry = contentTypeRegistry;

        public IContentType GetDefaultContentType()
            => _contentTypeRegistry.GetContentType(StringConstants.CSharpLspLanguageName);
    }
}
