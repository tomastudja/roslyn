﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Options;
using Roslyn.Utilities;
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Completion.Providers
{
    abstract class AbstractCrefCompletionProvider : CommonCompletionProvider
    {
        protected const string HideAdvancedMembers = nameof(HideAdvancedMembers);

        public override async Task<CompletionDescription> GetDescriptionAsync(Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            var position = SymbolCompletionItem.GetContextPosition(item);

            // What EditorBrowsable settings were we previously passed in (if it mattered)?
            bool hideAdvancedMembers = false;
            string hideAdvancedMembersString;
            if (item.Properties.TryGetValue(HideAdvancedMembers, out hideAdvancedMembersString))
            {
                bool.TryParse(hideAdvancedMembersString, out hideAdvancedMembers);
            }

            var options = document.Project.Solution.Workspace.Options
                .WithChangedOption(new OptionKey(CompletionOptions.HideAdvancedMembers, document.Project.Language), hideAdvancedMembers);

            var info = await GetSymbolsAsync(document, position, options, cancellationToken).ConfigureAwait(false);
            var name = SymbolCompletionItem.GetSymbolName(item);
            var kind = SymbolCompletionItem.GetKind(item);
            var bestSymbols = info.Item3.WhereAsArray(s => s.Kind == kind && s.Name == name);
            return await SymbolCompletionItem.GetDescriptionAsync(item, bestSymbols, document, info.Item2, cancellationToken).ConfigureAwait(false);
        }

        protected abstract Task<Tuple<SyntaxToken, SemanticModel, ImmutableArray<ISymbol>>> GetSymbolsAsync(
            Document document, int position, OptionSet options, CancellationToken cancellationToken);
    }
}
