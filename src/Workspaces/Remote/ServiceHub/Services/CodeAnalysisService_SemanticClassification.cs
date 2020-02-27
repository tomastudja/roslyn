﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote
{
    internal partial class CodeAnalysisService : IRemoteSemanticClassificationService
    {
        public Task<IList<ClassifiedSpan>> AddSemanticClassificationsAsync(
            PinnedSolutionInfo solutionInfo, DocumentId documentId,
            TextSpan span, CancellationToken cancellationToken)
        {
            return RunServiceAsync(async () =>
            {
                using (UserOperationBooster.Boost())
                {
                    var solution = await GetSolutionAsync(solutionInfo, cancellationToken).ConfigureAwait(false);

                    var document = solution.GetDocument(documentId);
                    var result = new List<ClassifiedSpan>();
                    await AbstractClassificationService.AddSemanticClassificationsInCurrentProcessAsync(
                        document, span, result, cancellationToken).ConfigureAwait(false);

                    return (IList<ClassifiedSpan>)result;
                }
            }, cancellationToken);
        }
    }
}
