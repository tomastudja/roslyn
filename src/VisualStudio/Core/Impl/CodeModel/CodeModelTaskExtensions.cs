﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.CodeModel
{
    internal static class CodeModelTaskExtensions
    {
        /// <summary>
        /// Does a Roslyn.Utilities.TaskExtensions.WaitAndGetResult{T} for CodeModel.
        /// </summary>
        /// <remarks>
        /// This function is the exact same as Roslyn.Utilities.TaskExtensions.WaitAndGetResult{T}, except it opts out
        /// of enforcement that it can be called on non-UI threads. CodeModel, since it must implement a highly blocking API,
        /// has no choice but to use WaitAndGetResult in a bunch of places. But that's not a good reason to require the tests
        /// to have thread affinity, since the tests have no specific threading requirements. Thus, it's acceptable for CodeModel
        /// to call the _CanCallOnBackground variant. We hope to audit _CanCallOnBackground periodically, and so rather than
        /// having to understand that each of those uses are CodeModel and thus get a special pass.</remarks>
        public static T WaitAndGetResult_CodeModel<T>(this Task<T> task, CancellationToken cancellationToken)
            => task.WaitAndGetResult_CanCallOnBackground(cancellationToken);
    }
}
