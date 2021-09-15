﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.FindUsages;
using Microsoft.CodeAnalysis.Editor.GoToDefinition;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MetadataAsSource;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.SymbolMapping;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.CodeDefinitionWindow
{
    [Export(typeof(ITextViewConnectionListener))]
    [Export(typeof(DefinitionContextTracker))]
    [ContentType(ContentTypeNames.RoslynContentType)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal class DefinitionContextTracker : ITextViewConnectionListener
    {
        private readonly HashSet<ITextView> _subscribedViews = new HashSet<ITextView>();
        private readonly IMetadataAsSourceFileService _metadataAsSourceFileService;
        private readonly ICodeDefinitionWindowService _codeDefinitionWindowService;
        private readonly IThreadingContext _threadingContext;
        private readonly IAsynchronousOperationListener _asyncListener;

        private CancellationTokenSource? _currentUpdateCancellationToken;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public DefinitionContextTracker(
            IMetadataAsSourceFileService metadataAsSourceFileService,
            ICodeDefinitionWindowService codeDefinitionWindowService,
            IThreadingContext threadingContext,
            IAsynchronousOperationListenerProvider listenerProvider)
        {
            _metadataAsSourceFileService = metadataAsSourceFileService;
            _codeDefinitionWindowService = codeDefinitionWindowService;
            _threadingContext = threadingContext;
            _asyncListener = listenerProvider.GetListener(FeatureAttribute.CodeDefinitionWindow);
        }

        void ITextViewConnectionListener.SubjectBuffersConnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
        {
            Contract.ThrowIfFalse(_threadingContext.JoinableTaskContext.IsOnMainThread);

            // We won't listen to caret changes in the code definition window itself, since navigations there would cause it to
            // keep refreshing itself.
            if (!_subscribedViews.Contains(textView) && !textView.Roles.Contains(PredefinedTextViewRoles.CodeDefinitionView))
            {
                _subscribedViews.Add(textView);
                textView.Caret.PositionChanged += OnTextViewCaretPositionChanged;
                QueueUpdateForCaretPosition(textView.Caret.Position);
            }
        }

        void ITextViewConnectionListener.SubjectBuffersDisconnected(ITextView textView, ConnectionReason reason, IReadOnlyCollection<ITextBuffer> subjectBuffers)
        {
            Contract.ThrowIfFalse(_threadingContext.JoinableTaskContext.IsOnMainThread);

            if (reason == ConnectionReason.TextViewLifetime ||
                !textView.BufferGraph.GetTextBuffers(b => b.ContentType.IsOfType(ContentTypeNames.RoslynContentType)).Any())
            {
                if (_subscribedViews.Contains(textView))
                {
                    _subscribedViews.Remove(textView);
                    textView.Caret.PositionChanged -= OnTextViewCaretPositionChanged;
                }
            }
        }

        private void OnTextViewCaretPositionChanged(object? sender, CaretPositionChangedEventArgs e)
        {
            Contract.ThrowIfFalse(_threadingContext.JoinableTaskContext.IsOnMainThread);

            QueueUpdateForCaretPosition(e.NewPosition);
        }

        private void QueueUpdateForCaretPosition(CaretPosition caretPosition)
        {
            Contract.ThrowIfFalse(_threadingContext.JoinableTaskContext.IsOnMainThread);

            // Cancel any pending update for this view
            _currentUpdateCancellationToken?.Cancel();

            // See if we moved somewhere else in a projection that we care about
            var pointInRoslynSnapshot = caretPosition.Point.GetPoint(tb => tb.ContentType.IsOfType(ContentTypeNames.RoslynContentType), caretPosition.Affinity);
            if (pointInRoslynSnapshot == null)
            {
                return;
            }

            _currentUpdateCancellationToken = new CancellationTokenSource();

            var cancellationToken = _currentUpdateCancellationToken.Token;
            var asyncToken = _asyncListener.BeginAsyncOperation(nameof(DefinitionContextTracker) + "." + nameof(QueueUpdateForCaretPosition));
            UpdateForCaretPositionAsync(pointInRoslynSnapshot.Value, cancellationToken).CompletesAsyncOperation(asyncToken);
        }

        private async Task UpdateForCaretPositionAsync(SnapshotPoint pointInRoslynSnapshot, CancellationToken cancellationToken)
        {
            await _asyncListener.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);

            var document = pointInRoslynSnapshot.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document == null)
            {
                return;
            }

            var locations = await GetContextFromPointAsync(document, pointInRoslynSnapshot, cancellationToken).ConfigureAwait(true);
            await _codeDefinitionWindowService.SetContextAsync(locations, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Internal for testing purposes.
        /// </summary>
        internal async Task<ImmutableArray<CodeDefinitionWindowLocation>> GetContextFromPointAsync(
            Document document, int position, CancellationToken cancellationToken)
        {
            var workspace = document.Project.Solution.Workspace;
            var navigableItems = await GoToDefinitionHelpers.GetDefinitionsAsync(document, position, cancellationToken).ConfigureAwait(false);
            if (navigableItems?.Any() == true)
            {
                var navigationService = workspace.Services.GetRequiredService<IDocumentNavigationService>();

                var builder = new ArrayBuilder<CodeDefinitionWindowLocation>();
                foreach (var item in navigableItems)
                {
                    if (await navigationService.CanNavigateToSpanAsync(workspace, item.Document.Id, item.SourceSpan, cancellationToken).ConfigureAwait(false))
                    {
                        var text = await item.Document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                        var linePositionSpan = text.Lines.GetLinePositionSpan(item.SourceSpan);

                        if (item.Document.FilePath != null)
                        {
                            builder.Add(new CodeDefinitionWindowLocation(item.DisplayTaggedParts.JoinText(), item.Document.FilePath, linePositionSpan.Start));
                        }
                    }
                }

                return builder.ToImmutable();
            }

            // We didn't have regular source references, but possibly:
            // 1. Another language (like XAML) will take over via ISymbolNavigationService
            // 2. There are no locations from source, so we'll try to generate a metadata as source file and use that
            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(
                document,
                position,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (symbol == null)
            {
                return ImmutableArray<CodeDefinitionWindowLocation>.Empty;
            }

            var symbolNavigationService = workspace.Services.GetRequiredService<ISymbolNavigationService>();
            var definitionItem = symbol.ToNonClassifiedDefinitionItem(document.Project.Solution, includeHiddenLocations: false);
            var result = await symbolNavigationService.GetExternalNavigationSymbolLocationAsync(definitionItem, cancellationToken).ConfigureAwait(false);

            if (result != null)
            {
                return ImmutableArray.Create(new CodeDefinitionWindowLocation(symbol.ToDisplayString(), result.Value.filePath, result.Value.linePosition));
            }
            else if (_metadataAsSourceFileService.IsNavigableMetadataSymbol(symbol))
            {
                // Don't allow decompilation when generating, since we don't have a good way to prompt the user
                // without a modal dialog.
                var declarationFile = await _metadataAsSourceFileService.GetGeneratedFileAsync(document.Project, symbol, allowDecompilation: false, cancellationToken).ConfigureAwait(false);
                var identifierSpan = declarationFile.IdentifierLocation.GetLineSpan().Span;
                return ImmutableArray.Create(new CodeDefinitionWindowLocation(symbol.ToDisplayString(), declarationFile.FilePath, identifierSpan.Start));
            }

            return ImmutableArray<CodeDefinitionWindowLocation>.Empty;
        }
    }
}
