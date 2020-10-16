﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.UnitTests.Initialize
{
    public class InitializeTests : AbstractLanguageServerProtocolTests
    {
        private static void AssertServerCapabilities(LSP.ServerCapabilities actual)
        {
            Assert.True((bool)actual.DefinitionProvider.Value);
            Assert.True((bool)actual.ImplementationProvider.Value);
            Assert.True((bool)actual.DocumentSymbolProvider.Value);
            Assert.True((bool)actual.WorkspaceSymbolProvider.Value);
            Assert.True((bool)actual.DocumentFormattingProvider.Value);
            Assert.True((bool)actual.DocumentRangeFormattingProvider.Value);
            Assert.True((bool)actual.DocumentHighlightProvider.Value);

            Assert.True(actual.CompletionProvider.ResolveProvider);
            Assert.Equal(new[] { ".", " ", "#", "<", ">", "\"", ":", "[", "(", "~", "=", "{", "/", "\\" }.OrderBy(string.Compare),
                actual.CompletionProvider.TriggerCharacters.OrderBy(string.Compare));

            Assert.Equal(new[] { "(", "," }, actual.SignatureHelpProvider.TriggerCharacters);

            Assert.Equal("}", actual.DocumentOnTypeFormattingProvider.FirstTriggerCharacter);
            Assert.Equal(new[] { ";", "\n" }, actual.DocumentOnTypeFormattingProvider.MoreTriggerCharacter);
        }
    }
}
