﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.CodeAnalysis.FindUsages;

namespace Microsoft.VisualStudio.LanguageServices.FindUsages
{
    [Export(typeof(IStreamingFindUsagesPresenter)), Shared]
    internal partial class StreamingFindUsagesPresenter :
        ForegroundThreadAffinitizedObject, IStreamingFindUsagesPresenter
    {
        public const string RoslynFindUsagesTableDataSourceIdentifier =
            nameof(RoslynFindUsagesTableDataSourceIdentifier);

        public const string RoslynFindUsagesTableDataSourceSourceTypeIdentifier =
            nameof(RoslynFindUsagesTableDataSourceSourceTypeIdentifier);

        private readonly IServiceProvider _serviceProvider;

        private readonly ITextBufferFactoryService _textBufferFactoryService;
        private readonly IProjectionBufferFactoryService _projectionBufferFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFactoryService;
        private readonly ITextEditorFactoryService _textEditorFactoryService;
        private readonly IContentTypeRegistryService _contentTypeRegistryService;

        private readonly ClassificationTypeMap _typeMap;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly IFindAllReferencesService _vsFindAllReferencesService;

        [ImportingConstructor]
        public StreamingFindUsagesPresenter(
            Shell.SVsServiceProvider serviceProvider,
            ITextBufferFactoryService textBufferFactoryService,
            IProjectionBufferFactoryService projectionBufferFactoryService,
            IEditorOptionsFactoryService editorOptionsFactoryService,
            ITextEditorFactoryService textEditorFactoryService,
            IContentTypeRegistryService contentTypeRegistryService,
            ClassificationTypeMap typeMap,
            IEditorFormatMapService formatMapService)
        {
            _serviceProvider = serviceProvider;
            _textBufferFactoryService = textBufferFactoryService;
            _projectionBufferFactoryService = projectionBufferFactoryService;
            _editorOptionsFactoryService = editorOptionsFactoryService;
            _contentTypeRegistryService = contentTypeRegistryService;

            _textEditorFactoryService = textEditorFactoryService;
            _typeMap = typeMap;
            _formatMapService = formatMapService;

            _vsFindAllReferencesService = (IFindAllReferencesService)_serviceProvider.GetService(typeof(SVsFindAllReferences));
        }

        public FindUsagesContext StartSearch(string title)
        {
            this.AssertIsForeground();

            // Get the appropriate window for FAR results to go into.
            var window = _vsFindAllReferencesService.StartSearch(title);

            // Make the data source that will feed data into this window.
            var dataSource = new TableDataSourceFindUsagesContext(this, window);

            // And return the data source so that the FindRefs engine can report results
            // which the data source can then create the appropriate presentation items for
            // for the window.
            return dataSource;
        }
    }
}