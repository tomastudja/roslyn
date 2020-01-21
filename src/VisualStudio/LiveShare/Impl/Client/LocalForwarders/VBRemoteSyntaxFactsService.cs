﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServices;

namespace Microsoft.VisualStudio.LanguageServices.LiveShare.Client.LocalForwarders
{
    [ExportLanguageServiceFactory(typeof(ISyntaxFactsService), StringConstants.VBLspLanguageName), Shared]
    internal class VBLspSyntaxFactsServiceFactory : ILanguageServiceFactory
    {
        [ImportingConstructor]
        public VBLspSyntaxFactsServiceFactory()
        {
        }

        public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
        {
            return languageServices.GetOriginalLanguageService<ISyntaxFactsService>();
        }
    }
}
