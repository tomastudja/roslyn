﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;
using static Microsoft.CodeAnalysis.Editor.Implementation.InlineRename.AbstractEditorInlineRenameService;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename
{
    [Export(typeof(IInlineRenameService))]
    [Export(typeof(InlineRenameService))]
    internal class InlineRenameService : IInlineRenameService
    {
        private readonly IThreadingContext _threadingContext;
        private readonly IWaitIndicator _waitIndicator;
        private readonly ITextBufferAssociatedViewService _textBufferAssociatedViewService;
        private readonly IAsynchronousOperationListener _asyncListener;
        private readonly IEnumerable<IRefactorNotifyService> _refactorNotifyServices;
        private readonly ITextBufferFactoryService _textBufferFactoryService;
        private readonly IFeatureServiceFactory _featureServiceFactory;
        private InlineRenameSession _activeRenameSession;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public InlineRenameService(
            IThreadingContext threadingContext,
            IWaitIndicator waitIndicator,
            ITextBufferAssociatedViewService textBufferAssociatedViewService,
            ITextBufferFactoryService textBufferFactoryService,
            IFeatureServiceFactory featureServiceFactory,
            [ImportMany] IEnumerable<IRefactorNotifyService> refactorNotifyServices,
            IAsynchronousOperationListenerProvider listenerProvider)
        {
            _threadingContext = threadingContext;
            _waitIndicator = waitIndicator;
            _textBufferAssociatedViewService = textBufferAssociatedViewService;
            _textBufferFactoryService = textBufferFactoryService;
            _featureServiceFactory = featureServiceFactory;
            _refactorNotifyServices = refactorNotifyServices;
            _asyncListener = listenerProvider.GetListener(FeatureAttribute.Rename);
        }

        public InlineRenameSessionInfo StartInlineSession(
            Document document,
            TextSpan textSpan,
            CancellationToken cancellationToken)
        {
            if (_activeRenameSession != null)
            {
                throw new InvalidOperationException(EditorFeaturesResources.An_active_inline_rename_session_is_still_active_Complete_it_before_starting_a_new_one);
            }

            var editorRenameService = document.GetLanguageService<IEditorInlineRenameService>();
            var renameInfo = editorRenameService.GetRenameInfoAsync(document, textSpan.Start, cancellationToken).WaitAndGetResult(cancellationToken);
            if (IsReadOnlyOrCannotNavigateToSpan(renameInfo, document, cancellationToken))
            {
                return new InlineRenameSessionInfo(EditorFeaturesResources.You_cannot_rename_this_element);
            }

            if (!renameInfo.CanRename)
            {
                return new InlineRenameSessionInfo(renameInfo.LocalizedErrorMessage);
            }

            var snapshot = document.GetTextAsync(cancellationToken).WaitAndGetResult(cancellationToken).FindCorrespondingEditorTextSnapshot();
            ActiveSession = new InlineRenameSession(
                _threadingContext,
                this,
                document.Project.Solution.Workspace,
                renameInfo.TriggerSpan.ToSnapshotSpan(snapshot),
                renameInfo,
                _waitIndicator,
                _textBufferAssociatedViewService,
                _textBufferFactoryService,
                _featureServiceFactory,
                _refactorNotifyServices,
                _asyncListener);

            return new InlineRenameSessionInfo(ActiveSession);

            static bool IsReadOnlyOrCannotNavigateToSpan(IInlineRenameInfo renameInfo, Document document, CancellationToken cancellationToken)
            {
                // This is unpleasant, but we do everything synchronously.  That's because we end up
                // needing to make calls on the UI thread to determine if the locations of the symbol
                // are in readonly buffer sections or not.  If we go pure async we have the following
                // problem:
                //   1) if we call ConfigureAwait(false), then we might call into the text buffer on 
                //      the wrong thread.
                //   2) if we try to call those pieces of code on the UI thread, then we will deadlock
                //      as our caller often is doing a 'Wait' on us, and our UI calling code won't run.
                if (renameInfo is SymbolInlineRenameInfo symbolInlineRenameInfo)
                {
                    var workspace = document.Project.Solution.Workspace;
                    var navigationService = workspace.Services.GetService<IDocumentNavigationService>();
                    foreach (var documentSpan in symbolInlineRenameInfo.DocumentSpans)
                    {
                        var sourceText = documentSpan.Document.GetTextAsync(cancellationToken).WaitAndGetResult(cancellationToken);
                        var textSnapshot = sourceText.FindCorrespondingEditorTextSnapshot();

                        if (textSnapshot != null)
                        {
                            var buffer = textSnapshot.TextBuffer;
                            var originalSpan = documentSpan.SourceSpan.ToSnapshotSpan(textSnapshot).TranslateTo(buffer.CurrentSnapshot, SpanTrackingMode.EdgeInclusive);

                            if (buffer.IsReadOnly(originalSpan) || !navigationService.CanNavigateToSpan(workspace, document.Id, documentSpan.SourceSpan))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
        }

        IInlineRenameSession IInlineRenameService.ActiveSession => _activeRenameSession;

        internal InlineRenameSession ActiveSession
        {
            get
            {
                return _activeRenameSession;
            }

            set
            {
                var previousSession = _activeRenameSession;
                _activeRenameSession = value;
                ActiveSessionChanged?.Invoke(this, new ActiveSessionChangedEventArgs(previousSession));
            }
        }

        /// <summary>
        /// Raised when the ActiveSession property has changed.
        /// </summary>
        internal event EventHandler<ActiveSessionChangedEventArgs> ActiveSessionChanged;

        internal class ActiveSessionChangedEventArgs : EventArgs
        {
            public ActiveSessionChangedEventArgs(InlineRenameSession previousSession)
            {
                this.PreviousSession = previousSession;
            }

            public InlineRenameSession PreviousSession { get; }
        }
    }
}
