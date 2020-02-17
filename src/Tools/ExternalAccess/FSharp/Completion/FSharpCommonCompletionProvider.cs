﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.ExternalAccess.FSharp.Internal.Completion;

namespace Microsoft.CodeAnalysis.ExternalAccess.FSharp.Completion
{
    internal static class FSharpCommonCompletionProvider
    {
        public static CompletionProvider Create(IFSharpCommonCompletionProvider fsharpCommonCompletionProvider)
        {
            return new FSharpInternalCommonCompletionProvider(fsharpCommonCompletionProvider);
        }
    }
}
