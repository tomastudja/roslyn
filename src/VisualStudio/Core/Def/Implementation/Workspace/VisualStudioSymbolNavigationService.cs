﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.DecompiledSource;
using Microsoft.CodeAnalysis.Editor.Shared.Options;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.FindUsages;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MetadataAsSource;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.LanguageServices.Implementation.Library;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem;
using Microsoft.VisualStudio.LanguageServices.Utilities;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation
{
    [ExportWorkspaceService(typeof(ISymbolNavigationService), ServiceLayer.Host), Shared]
    internal partial class VisualStudioSymbolNavigationService : ForegroundThreadAffinitizedObject, ISymbolNavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IGlobalOptionService _globalOptions;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;
        private readonly IMetadataAsSourceFileService _metadataAsSourceFileService;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public VisualStudioSymbolNavigationService(
            SVsServiceProvider serviceProvider,
            IGlobalOptionService globalOptions,
            IThreadingContext threadingContext,
            IVsEditorAdaptersFactoryService editorAdaptersFactory,
            IMetadataAsSourceFileService metadataAsSourceFileService)
            : base(threadingContext)
        {
            _serviceProvider = serviceProvider;
            _globalOptions = globalOptions;
            _editorAdaptersFactory = editorAdaptersFactory;
            _metadataAsSourceFileService = metadataAsSourceFileService;
        }

        public async Task<bool> TryNavigateToSymbolAsync(
            ISymbol symbol, Project project, NavigationOptions options, CancellationToken cancellationToken)
        {
            if (project == null || symbol == null)
            {
                return false;
            }

            symbol = symbol.OriginalDefinition;

            // Prefer visible source locations if possible.
            var sourceLocations = symbol.Locations.Where(loc => loc.IsInSource);
            var visibleSourceLocations = sourceLocations.Where(loc => loc.IsVisibleSourceLocation());
            var sourceLocation = visibleSourceLocations.FirstOrDefault() ?? sourceLocations.FirstOrDefault();

            if (sourceLocation != null)
            {
                var targetDocument = project.Solution.GetDocument(sourceLocation.SourceTree);
                if (targetDocument != null)
                {
                    var editorWorkspace = targetDocument.Project.Solution.Workspace;
                    var navigationService = editorWorkspace.Services.GetRequiredService<IDocumentNavigationService>();
                    return await navigationService.TryNavigateToSpanAsync(
                        editorWorkspace, targetDocument.Id, sourceLocation.SourceSpan, options, cancellationToken).ConfigureAwait(false);
                }
            }

            // We don't have a source document, so show the Metadata as Source view in a preview tab.

            if (!_metadataAsSourceFileService.IsNavigableMetadataSymbol(symbol))
            {
                return false;
            }

            // Should we prefer navigating to the Object Browser over metadata-as-source?
            if (_globalOptions.GetOption(VisualStudioNavigationOptions.NavigateToObjectBrowser, project.Language))
            {
                var libraryService = project.LanguageServices.GetService<ILibraryService>();
                if (libraryService == null)
                {
                    return false;
                }

                var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                var navInfo = libraryService.NavInfoFactory.CreateForSymbol(symbol, project, compilation);
                if (navInfo == null)
                {
                    navInfo = libraryService.NavInfoFactory.CreateForProject(project);
                }

                if (navInfo != null)
                {
                    var navigationTool = IServiceProviderExtensions.GetService<SVsObjBrowser, IVsNavigationTool>(_serviceProvider);
                    return navigationTool.NavigateToNavInfo(navInfo) == VSConstants.S_OK;
                }

                // Note: we'll fallback to Metadata-As-Source if we fail to get IVsNavInfo, but that should never happen.
            }

            // Generate new source or retrieve existing source for the symbol in question
            return await TryNavigateToMetadataAsync(project, symbol, options, cancellationToken).ConfigureAwait(false);
        }

        private async Task<bool> TryNavigateToMetadataAsync(Project project, ISymbol symbol, NavigationOptions options, CancellationToken cancellationToken)
        {
            var allowDecompilation = _globalOptions.GetOption(FeatureOnOffOptions.NavigateToDecompiledSources);

            var result = await _metadataAsSourceFileService.GetGeneratedFileAsync(project, symbol, signaturesOnly: false, allowDecompilation, cancellationToken).ConfigureAwait(false);

            await this.ThreadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var vsRunningDocumentTable4 = IServiceProviderExtensions.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable4>(_serviceProvider);
            var fileAlreadyOpen = vsRunningDocumentTable4.IsMonikerValid(result.FilePath);

            var openDocumentService = IServiceProviderExtensions.GetService<SVsUIShellOpenDocument, IVsUIShellOpenDocument>(_serviceProvider);
            openDocumentService.OpenDocumentViaProject(result.FilePath, VSConstants.LOGVIEWID.TextView_guid, out _, out _, out _, out var windowFrame);

            var documentCookie = vsRunningDocumentTable4.GetDocumentCookie(result.FilePath);

            var vsTextBuffer = (IVsTextBuffer)vsRunningDocumentTable4.GetDocumentData(documentCookie);

            // Set the buffer to read only, just in case the file isn't
            ErrorHandler.ThrowOnFailure(vsTextBuffer.GetStateFlags(out var flags));
            flags |= (int)BUFFERSTATEFLAGS.BSF_USER_READONLY;
            ErrorHandler.ThrowOnFailure(vsTextBuffer.SetStateFlags(flags));

            var textBuffer = _editorAdaptersFactory.GetDataBuffer(vsTextBuffer);

            if (!fileAlreadyOpen)
            {
                ErrorHandler.ThrowOnFailure(windowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_IsProvisional, true));
                ErrorHandler.ThrowOnFailure(windowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_OverrideCaption, result.DocumentTitle));
                ErrorHandler.ThrowOnFailure(windowFrame.SetProperty((int)__VSFPROPID5.VSFPROPID_OverrideToolTip, result.DocumentTooltip));
            }

            windowFrame.Show();

            var openedDocument = textBuffer?.AsTextContainer().GetRelatedDocuments().FirstOrDefault();
            if (openedDocument != null)
            {
                var editorWorkspace = openedDocument.Project.Solution.Workspace;
                var navigationService = editorWorkspace.Services.GetRequiredService<IDocumentNavigationService>();

                return await navigationService.TryNavigateToSpanAsync(
                    editorWorkspace,
                    openedDocument.Id,
                    result.IdentifierLocation.SourceSpan,
                    options with { PreferProvisionalTab = true },
                    cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        public async Task<bool> TrySymbolNavigationNotifyAsync(ISymbol symbol, Project project, CancellationToken cancellationToken)
        {
            await this.ThreadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            AssertIsForeground();

            var definitionItem = symbol.ToNonClassifiedDefinitionItem(project.Solution, includeHiddenLocations: true);
            definitionItem.Properties.TryGetValue(DefinitionItem.RQNameKey1, out var rqName);

            var result = await TryGetNavigationAPIRequiredArgumentsAsync(definitionItem, rqName, cancellationToken).ConfigureAwait(true);
            if (result is not var (hierarchy, itemID, navigationNotify))
                return false;

            var returnCode = navigationNotify.OnBeforeNavigateToSymbol(
                hierarchy,
                itemID,
                rqName,
                out var navigationHandled);

            return returnCode == VSConstants.S_OK && navigationHandled == 1;
        }

        public async Task<(string filePath, LinePosition linePosition)?> GetExternalNavigationSymbolLocationAsync(
            DefinitionItem definitionItem, CancellationToken cancellationToken)
        {
            definitionItem.Properties.TryGetValue(DefinitionItem.RQNameKey1, out var rqName1);
            definitionItem.Properties.TryGetValue(DefinitionItem.RQNameKey2, out var rqName2);

            return await GetExternalNavigationLocationForSpecificSymbolAsync(definitionItem, rqName1, cancellationToken).ConfigureAwait(false) ??
                   await GetExternalNavigationLocationForSpecificSymbolAsync(definitionItem, rqName2, cancellationToken).ConfigureAwait(false);
        }

        public async Task<(string filePath, LinePosition linePosition)?> GetExternalNavigationLocationForSpecificSymbolAsync(
            DefinitionItem definitionItem, string? rqName, CancellationToken cancellationToken)
        {
            if (rqName == null)
                return null;

            var values = await TryGetNavigationAPIRequiredArgumentsAsync(
                definitionItem, rqName, cancellationToken).ConfigureAwait(false);
            if (values is not var (hierarchy, itemID, navigationNotify))
                return null;

            var navigateToTextSpan = new Microsoft.VisualStudio.TextManager.Interop.TextSpan[1];

            await this.ThreadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            var queryNavigateStatusCode = navigationNotify.QueryNavigateToSymbol(
                hierarchy,
                itemID,
                rqName,
                out var navigateToHierarchy,
                out var navigateToItem,
                navigateToTextSpan,
                out var wouldNavigate);

            if (queryNavigateStatusCode != VSConstants.S_OK || wouldNavigate != 1)
                return null;

            navigateToHierarchy.GetCanonicalName(navigateToItem, out var filePath);
            var lineNumber = navigateToTextSpan[0].iStartLine;
            var charOffset = navigateToTextSpan[0].iStartIndex;

            return (filePath, new LinePosition(lineNumber, charOffset));
        }

        private async Task<(IVsHierarchy hierarchy, uint itemId, IVsSymbolicNavigationNotify navigationNotify)?> TryGetNavigationAPIRequiredArgumentsAsync(
            DefinitionItem definitionItem,
            string? rqName,
            CancellationToken cancellationToken)
        {
            if (rqName == null)
                return null;

            var sourceLocations = definitionItem.SourceSpans;
            if (!sourceLocations.Any())
                return null;

            using var _ = ArrayBuilder<Document>.GetInstance(out var documentsBuilder);
            foreach (var loc in sourceLocations)
                documentsBuilder.AddIfNotNull(loc.Document);

            var documents = documentsBuilder.ToImmutable();

            // We can only pass one itemid to IVsSymbolicNavigationNotify, so prefer itemids from
            // documents we consider to be "generated" to give external language services the best
            // chance of participating.

            var generatedDocuments = documents.WhereAsArray(d => d.IsGeneratedCode(cancellationToken));

            var documentToUse = generatedDocuments.FirstOrDefault() ?? documents.First();

            await this.ThreadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!TryGetVsHierarchyAndItemId(documentToUse, out var hierarchy, out var itemID))
                return null;

            var navigationNotify = hierarchy as IVsSymbolicNavigationNotify;
            if (navigationNotify == null)
                return null;

            return (hierarchy, itemID, navigationNotify);
        }

        private bool TryGetVsHierarchyAndItemId(Document document, [NotNullWhen(true)] out IVsHierarchy? hierarchy, out uint itemID)
        {
            AssertIsForeground();

            if (document.Project.Solution.Workspace is VisualStudioWorkspace visualStudioWorkspace
                && document.FilePath is object)
            {
                hierarchy = visualStudioWorkspace.GetHierarchy(document.Project.Id);
                if (hierarchy is object)
                {
                    itemID = hierarchy.TryGetItemId(document.FilePath);
                    if (itemID != VSConstants.VSITEMID_NIL)
                    {
                        return true;
                    }
                }
            }

            hierarchy = null;
            itemID = (uint)VSConstants.VSITEMID.Nil;
            return false;
        }
    }
}
