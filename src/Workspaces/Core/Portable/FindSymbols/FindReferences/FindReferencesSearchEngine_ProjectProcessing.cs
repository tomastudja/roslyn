﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.FindSymbols.Finders;
using Microsoft.CodeAnalysis.Internal.Log;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    using DocumentMap = Dictionary<Document, HashSet<(ISymbol symbol, IReferenceFinder finder)>>;

    internal partial class FindReferencesSearchEngine
    {
        private async Task ProcessProjectAsync(
            Project project,
            DocumentMap documentMap,
            Func<ISymbol, ValueTask<bool>> isMatchAsync,
            CancellationToken cancellationToken)
        {
            using (Logger.LogBlock(FunctionId.FindReference_ProcessProjectAsync, project.Name, cancellationToken))
            {
                if (project.SupportsCompilation)
                {
                    // make sure we hold onto compilation while we search documents belong to this project
                    var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

                    var documentTasks = new List<Task>();
                    foreach (var (document, documentQueue) in documentMap)
                    {
                        if (document.Project == project)
                            documentTasks.Add(Task.Factory.StartNew(() => ProcessDocumentQueueAsync(document, documentQueue, isMatchAsync, cancellationToken), cancellationToken, TaskCreationOptions.None, _scheduler).Unwrap());
                    }

                    await Task.WhenAll(documentTasks).ConfigureAwait(false);

                    GC.KeepAlive(compilation);
                }
            }
        }
    }
}
