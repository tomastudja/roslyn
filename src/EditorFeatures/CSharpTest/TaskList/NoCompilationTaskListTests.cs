﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Remote.Testing;
using Microsoft.CodeAnalysis.TaskList;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities.TaskList;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.UnitTests;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.TaskList
{
    [UseExportProvider]
    public class NoCompilationTaskListTests : AbstractTaskListTests
    {
        protected override TestWorkspace CreateWorkspace(string codeWithMarker)
        {
            var workspace = TestWorkspace.CreateWorkspace(XElement.Parse(
$@"<Workspace>
    <Project Language=""NoCompilation"">
        <Document>{codeWithMarker}</Document>
    </Project>
</Workspace>"), composition: EditorTestCompositions.EditorFeatures.AddParts(
                typeof(NoCompilationContentTypeDefinitions),
                typeof(NoCompilationContentTypeLanguageService),
                typeof(NoCompilationTaskListService)));

            return workspace;
        }

        [Fact, WorkItem(1192024, "https://dev.azure.com/devdiv/DevDiv/_workitems/edit/1192024")]
        public async Task TodoCommentInNoCompilationProject()
        {
            var code = @"(* [|Message|] *)";

            await TestAsync(code);
        }
    }

    [PartNotDiscoverable]
    [ExportLanguageService(typeof(ITaskListService), language: NoCompilationConstants.LanguageName), Shared]
    internal class NoCompilationTaskListService : ITaskListService
    {
        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public NoCompilationTaskListService()
        {
        }

        public Task<ImmutableArray<TaskListItem>> GetTaskListItemsAsync(Document document, ImmutableArray<TaskListItemDescriptor> descriptors, CancellationToken cancellationToken)
            => Task.FromResult(ImmutableArray.Create(new TaskListItem(
                descriptors.First().Priority,
                "Message",
                document.Id,
                span: new FileLinePositionSpan("dummy", new LinePosition(0, 3), new LinePosition(0, 3)),
                mappedSpan: new FileLinePositionSpan("dummy", new LinePosition(0, 3), new LinePosition(0, 3)))));
    }
}
