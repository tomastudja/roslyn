﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServer.UnitTests.References;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.CodeAnalysis.LanguageServer.UnitTests.DocumentChanges
{
    public partial class DocumentChangesTests
    {
        [Theory, CombinatorialData]
        public async Task FindReferencesInChangingDocument(bool mutatingLspWorkspace)
        {
            var source =
@"class A
{
    public int {|type:|}someInt = 1;
    void M()
    {
    }
}
class B
{
    void M2()
    {
    }
}";

            var (testLspServer, locationTyped, _) = await GetTestLspServerAndLocationAsync(source, mutatingLspWorkspace);

            await using (testLspServer)
            {
                Assert.Empty(testLspServer.GetTrackedTexts());

                await DidOpen(testLspServer, locationTyped.Uri);

                var originalDocument = testLspServer.GetCurrentSolution().Projects.Single().Documents.Single();

                var findResults = await FindAllReferencesHandlerTests.RunFindAllReferencesAsync<VSInternalReferenceItem>(testLspServer, locationTyped);
                Assert.Single(findResults);

                Assert.Equal("A", findResults[0].ContainingType);

                // Declare a local inside A.M()
                await DidChange(testLspServer, locationTyped.Uri, (5, 0, "var i = someInt + 1;\r\n"));

                findResults = await FindAllReferencesHandlerTests.RunFindAllReferencesAsync<VSInternalReferenceItem>(testLspServer, locationTyped);
                Assert.Equal(2, findResults.Length);

                Assert.Equal("A", findResults[0].ContainingType);
                Assert.Equal("M", findResults[1].ContainingMember);

                // Declare a field in B
                await DidChange(testLspServer, locationTyped.Uri, (10, 0, "int someInt = A.someInt + 1;\r\n"));

                findResults = await FindAllReferencesHandlerTests.RunFindAllReferencesAsync<VSInternalReferenceItem>(testLspServer, locationTyped);
                Assert.Equal(3, findResults.Length);

                Assert.Equal("A", findResults[0].ContainingType);
                Assert.Equal("B", findResults[2].ContainingType);
                Assert.Equal("M", findResults[1].ContainingMember);

                // Declare a local inside B.M2()
                await DidChange(testLspServer, locationTyped.Uri, (13, 0, "var j = someInt + A.someInt;\r\n"));

                findResults = await FindAllReferencesHandlerTests.RunFindAllReferencesAsync<VSInternalReferenceItem>(testLspServer, locationTyped);
                Assert.Equal(4, findResults.Length);

                Assert.Equal("A", findResults[0].ContainingType);
                Assert.Equal("B", findResults[2].ContainingType);
                Assert.Equal("M", findResults[1].ContainingMember);
                Assert.Equal("M2", findResults[3].ContainingMember);

                {
                    // NOTE: This is not a real world scenario.
                    //
                    // By closing the document we revert back to the original state, but in the real world the original
                    // state will have been updated by back channels (text buffer sync, file changed on disk, etc.) This
                    // is validating that the above didn't succeed by any means except the FAR handler being passed the
                    // updated document, so if we regress and get lucky, we still know about it.
                    await DidClose(testLspServer, locationTyped.Uri);

                    // In the mutating scenario, simulate this by rolling back to the text we had when the workspace was
                    // created.
                    if (mutatingLspWorkspace)
                        ((ILspWorkspace)testLspServer.TestWorkspace).UpdateTextIfPresent(originalDocument.Id, await originalDocument.GetTextAsync());
                }

                findResults = await FindAllReferencesHandlerTests.RunFindAllReferencesAsync<VSInternalReferenceItem>(testLspServer, locationTyped);
                Assert.Single(findResults);

                Assert.Equal("A", findResults[0].ContainingType);
            }
        }
    }
}
