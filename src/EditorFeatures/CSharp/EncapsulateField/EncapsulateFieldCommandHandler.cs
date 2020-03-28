﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.Implementation.EncapsulateField;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CSharp.EncapsulateField
{
    [Export(typeof(ICommandHandler))]
    [ContentType(ContentTypeNames.CSharpContentType)]
    [Name(PredefinedCommandHandlerNames.EncapsulateField)]
    [Order(After = PredefinedCommandHandlerNames.DocumentationComments)]
    internal class EncapsulateFieldCommandHandler : AbstractEncapsulateFieldCommandHandler
    {
        [ImportingConstructor]
        [SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification = "Used in test code: https://github.com/dotnet/roslyn/issues/42814")]
        public EncapsulateFieldCommandHandler(
            IThreadingContext threadingContext,
            ITextBufferUndoManagerProvider undoManager,
            IAsynchronousOperationListenerProvider listenerProvider)
            : base(threadingContext, undoManager, listenerProvider)
        {
        }
    }
}
