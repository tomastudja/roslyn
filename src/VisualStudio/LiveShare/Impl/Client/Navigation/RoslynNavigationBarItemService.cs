﻿//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Cascade.Telemetry;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor;
using Microsoft.CodeAnalysis.Editor.Extensibility.NavigationBar;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Navigation;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text.Editor;


namespace Microsoft.VisualStudio.LanguageServices.LiveShare.Client
{
    internal class RoslynNavigationBarItemService : AbstractNavigationBarItemService
    {
        private readonly RoslynLSPClientServiceFactory roslynLSPClientServiceFactory;

        internal RoslynNavigationBarItemService(RoslynLSPClientServiceFactory roslynLSPClientServiceFactory)
        {
            this.roslynLSPClientServiceFactory = roslynLSPClientServiceFactory ?? throw new ArgumentNullException(nameof(roslynLSPClientServiceFactory));
        }

        public override async Task<IList<NavigationBarItem>> GetItemsAsync(Document document, CancellationToken cancellationToken)
        {
            var lspClient = this.roslynLSPClientServiceFactory.ActiveLanguageServerClient;
            if (lspClient == null)
            {
                return ImmutableArray<NavigationBarItem>.Empty;
            }

            var documentSymbolParams = new DocumentSymbolParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(document.FilePath) }
            };

            SymbolInformation[] symbols = await lspClient.RequestAsync(Methods.TextDocumentDocumentSymbol, documentSymbolParams, cancellationToken).ConfigureAwait(false);
            if (symbols == null)
            {
                return ImmutableArray<NavigationBarItem>.Empty;
            }

            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

            var navBarItems = new List<NavigationBarItem>();
            IEnumerable<IGrouping<string, SymbolInformation>> containerGroups = symbols.GroupBy(s => s.ContainerName);

            // Add symbols that are containers with children.
            foreach (var containerGroup in containerGroups)
            {
                SymbolInformation containerSymbol = symbols.Where(s => s.Name == containerGroup.Key).FirstOrDefault();
                if (containerSymbol == null)
                {
                    continue;
                }

                var children = new List<NavigationBarItem>();
                foreach (SymbolInformation child in containerGroup)
                {
                    children.Add(CreateNavigationBarItem(child, text, ImmutableArray<NavigationBarItem>.Empty));
                }

                navBarItems.Add(CreateNavigationBarItem(containerSymbol, text, ImmutableArray.CreateRange(children.Where(nbi => nbi != null))));
            }

            // Now add top-level items that are not containers.
            var topLevelSymbols = symbols.Where(s => s.ContainerName == null);
            var containerNames = containerGroups.Select(g => g.Key).Where(c => c != null);
            var topLevelSymbolsWithoutChildren = topLevelSymbols.Where(s => !containerNames.Contains(s.Name));
            navBarItems.AddRange(topLevelSymbolsWithoutChildren.Select(symbol => CreateNavigationBarItem(symbol, text, ImmutableArray<NavigationBarItem>.Empty)));

            return ImmutableList.CreateRange(navBarItems.Where(nbi => nbi != null));
        }

        private static NavigationBarItem CreateNavigationBarItem(SymbolInformation symbolInformation, SourceText text, ImmutableArray<NavigationBarItem> children)
        {
            try
            {
                var name = symbolInformation.Name;
                var glyph = symbolInformation.Kind.ToGlyph();
                var textSpan = symbolInformation.Location.Range.ToTextSpan(text);
                return new RemoteNavigationBarItem(name, glyph, ImmutableArray.Create(textSpan), children);
            }
            catch (ArgumentOutOfRangeException)
            {
                LanguageServiceTelemetry.ReportInvalidNavBarItem();
                return null;
            }
        }

        public override void NavigateToItem(Document document, NavigationBarItem item, ITextView textView, CancellationToken cancellationToken)
        {
            if (item.Spans.Count > 0)
            {
                var workspace = document.Project.Solution.Workspace;
                var navigationService = workspace.Services.GetService<IDocumentNavigationService>();

                navigationService.TryNavigateToPosition(workspace, document.Id, item.Spans[0].Start);
            }
        }

        protected internal override VirtualTreePoint? GetSymbolItemNavigationPoint(Document document, NavigationBarSymbolItem item, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private class RemoteNavigationBarItem : NavigationBarItem
        {
            public RemoteNavigationBarItem(string text, Glyph glyph, IList<TextSpan> spans, IList<NavigationBarItem> childItems = null, int indent = 0, bool bolded = false, bool grayed = false) : base(text, glyph, spans, childItems, indent, bolded, grayed)
            {
            }
        }
    }
}
