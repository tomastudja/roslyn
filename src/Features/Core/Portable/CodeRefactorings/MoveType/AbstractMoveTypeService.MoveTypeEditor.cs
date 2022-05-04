﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.RemoveUnnecessaryImports;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeRefactorings.MoveType
{
    internal abstract partial class AbstractMoveTypeService<TService, TTypeDeclarationSyntax, TNamespaceDeclarationSyntax, TMemberDeclarationSyntax, TCompilationUnitSyntax>
    {
        private class MoveTypeEditor : Editor
        {
            public MoveTypeEditor(
                TService service,
                State state,
                string fileName,
                CancellationToken cancellationToken) : base(service, state, fileName, cancellationToken)
            {
            }

            /// <summary>
            /// Given a document and a type contained in it, moves the type
            /// out to its own document. The new document's name typically
            /// is the type name, or is at least based on the type name.
            /// </summary>
            /// <remarks>
            /// The algorithm for this, is as follows:
            /// 1. Fork the original document that contains the type to be moved.
            /// 2. Keep the type, required namespace containers and using statements.
            ///    remove everything else from the forked document.
            /// 3. Add this forked document to the solution.
            /// 4. Finally, update the original document and remove the type from it.
            /// </remarks>
            public override async Task<Solution> GetModifiedSolutionAsync()
            {
                // Fork, update and add as new document.
                var projectToBeUpdated = SemanticDocument.Document.Project;
                var newDocumentId = DocumentId.CreateNewId(projectToBeUpdated.Id, FileName);

                // We do this process in the following steps:
                //
                // 1. Produce the new document, with the moved type, with all the same imports as the original file.
                // 2. Remove the original type from the first document, not touching the imports in it.  This is
                //    necessary to prevent duplicate symbol errors (and other compiler issues) as we process imports.
                // 3. Now that the type has been moved to the new file, remove the unnecessary imports from the new
                //    file.  This will also tell us which imports are necessary in the new file.
                // 4. Now go back to the original file and remove any unnecessary imports *if* they are in the new file.
                //    these imports only were needed for the moved type, and so they shouldn't stay in the original
                //    file.

                var documentWithMovedType = await AddNewDocumentWithSingleTypeDeclarationAsync(newDocumentId).ConfigureAwait(false);

                var solutionWithNewDocument = documentWithMovedType.Project.Solution;

                // Get the original source document again, from the latest forked solution.
                var sourceDocument = solutionWithNewDocument.GetRequiredDocument(SemanticDocument.Document.Id);

                // update source document to add partial modifiers to type chain
                // and/or remove type declaration from original source document.
                var solutionWithBothDocumentsUpdated = await RemoveTypeFromSourceDocumentAsync(sourceDocument).ConfigureAwait(false);

                return await RemoveUnnecessaryImportsAsync(solutionWithBothDocumentsUpdated, sourceDocument.Id, documentWithMovedType.Id).ConfigureAwait(false);
            }

            private async Task<Solution> RemoveUnnecessaryImportsAsync(
                Solution solution, DocumentId sourceDocumentId, DocumentId documentWithMovedTypeId)
            {
                var documentWithMovedType = solution.GetRequiredDocument(documentWithMovedTypeId);
                var documentWithMovedTypeFormattingOptions = await documentWithMovedType.GetSyntaxFormattingOptionsAsync(State.FallbackOptions, CancellationToken).ConfigureAwait(false);

                var syntaxFacts = documentWithMovedType.GetRequiredLanguageService<ISyntaxFactsService>();
                var removeUnnecessaryImports = documentWithMovedType.GetRequiredLanguageService<IRemoveUnnecessaryImportsService>();

                // Remove all unnecessary imports from the new document we've created.
                documentWithMovedType = await removeUnnecessaryImports.RemoveUnnecessaryImportsAsync(documentWithMovedType, documentWithMovedTypeFormattingOptions, CancellationToken).ConfigureAwait(false);

                solution = solution.WithDocumentSyntaxRoot(
                    documentWithMovedTypeId, await documentWithMovedType.GetRequiredSyntaxRootAsync(CancellationToken).ConfigureAwait(false));

                // See which imports we kept around.
                var rootWithMovedType = await documentWithMovedType.GetRequiredSyntaxRootAsync(CancellationToken).ConfigureAwait(false);
                var movedImports = rootWithMovedType.DescendantNodes()
                                                    .Where(syntaxFacts.IsUsingOrExternOrImport)
                                                    .ToImmutableArray();

                // Now remove any unnecessary imports from the original doc that moved to the new doc.
                var sourceDocument = solution.GetRequiredDocument(sourceDocumentId);
                var sourceDocumentFormattingOptions = await sourceDocument.GetSyntaxFormattingOptionsAsync(State.FallbackOptions, CancellationToken).ConfigureAwait(false);
                sourceDocument = await removeUnnecessaryImports.RemoveUnnecessaryImportsAsync(
                    sourceDocument,
                    n => movedImports.Contains(i => syntaxFacts.AreEquivalent(i, n)),
                    sourceDocumentFormattingOptions,
                    CancellationToken).ConfigureAwait(false);

                return solution.WithDocumentSyntaxRoot(
                    sourceDocumentId, await sourceDocument.GetRequiredSyntaxRootAsync(CancellationToken).ConfigureAwait(false));
            }

            /// <summary>
            /// Forks the source document, keeps required type, namespace containers
            /// and adds it the solution.
            /// </summary>
            /// <param name="newDocumentId">id for the new document to be added</param>
            private async Task<Document> AddNewDocumentWithSingleTypeDeclarationAsync(DocumentId newDocumentId)
            {
                var document = SemanticDocument.Document;
                Debug.Assert(document.Name != FileName,
                             $"New document name is same as old document name:{FileName}");

                var root = SemanticDocument.Root;
                var projectToBeUpdated = document.Project;
                var documentEditor = await DocumentEditor.CreateAsync(document, CancellationToken).ConfigureAwait(false);

                // Make the type chain above this new type partial.  Also, remove any 
                // attributes from the containing partial types.  We don't want to create
                // duplicate attributes on things.
                AddPartialModifiersToTypeChain(
                    documentEditor, removeAttributesAndComments: true, removeTypeInheritance: true);

                // remove things that are not being moved, from the forked document.
                var membersToRemove = GetMembersToRemove(root);
                foreach (var member in membersToRemove)
                    documentEditor.RemoveNode(member, SyntaxRemoveOptions.KeepNoTrivia);

                // Remove attributes from the root node as well, since those will apply as AttributeTarget.Assembly and
                // don't need to be specified multiple times
                documentEditor.RemoveAllAttributes(root);

                var modifiedRoot = documentEditor.GetChangedRoot();
                modifiedRoot = await AddFinalNewLineIfDesiredAsync(document, modifiedRoot).ConfigureAwait(false);

                // add an empty document to solution, so that we'll have options from the right context.
                var solutionWithNewDocument = projectToBeUpdated.Solution.AddDocument(
                    newDocumentId, FileName, text: string.Empty, folders: document.Folders);

                // update the text for the new document
                solutionWithNewDocument = solutionWithNewDocument.WithDocumentSyntaxRoot(newDocumentId, modifiedRoot, PreservationMode.PreserveIdentity);

                // get the updated document, give it the minimal set of imports that the type
                // inside it needs.
                var newDocument = solutionWithNewDocument.GetRequiredDocument(newDocumentId);
                return newDocument;
            }

            /// <summary>
            /// Add a trailing newline if we don't already have one if that's what the user's 
            /// preference is.
            /// </summary>
            private async Task<SyntaxNode> AddFinalNewLineIfDesiredAsync(Document document, SyntaxNode modifiedRoot)
            {
                var options = await document.GetOptionsAsync(CancellationToken).ConfigureAwait(false);
                var insertFinalNewLine = options.GetOption(FormattingOptions2.InsertFinalNewLine);
                if (insertFinalNewLine)
                {
                    var endOfFileToken = ((ICompilationUnitSyntax)modifiedRoot).EndOfFileToken;
                    var previousToken = endOfFileToken.GetPreviousToken(includeZeroWidth: true, includeSkipped: true);

                    var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();
                    if (endOfFileToken.LeadingTrivia.IsEmpty() &&
                        !previousToken.TrailingTrivia.Any(syntaxFacts.IsEndOfLineTrivia))
                    {
                        var generator = document.GetRequiredLanguageService<SyntaxGeneratorInternal>();
                        var endOfLine = generator.EndOfLine(options.GetOption(FormattingOptions.NewLine));
                        return modifiedRoot.ReplaceToken(
                            previousToken, previousToken.WithAppendedTrailingTrivia(endOfLine));
                    }
                }

                return modifiedRoot;
            }

            /// <summary>
            /// update the original document and remove the type that was moved.
            /// perform other fix ups as necessary.
            /// </summary>
            /// <returns>an updated solution with the original document fixed up as appropriate.</returns>
            private async Task<Solution> RemoveTypeFromSourceDocumentAsync(Document sourceDocument)
            {
                var documentEditor = await DocumentEditor.CreateAsync(sourceDocument, CancellationToken).ConfigureAwait(false);

                // Make the type chain above the type we're moving 'partial'.  
                // However, keep all the attributes on these types as theses are the 
                // original attributes and we don't want to mess with them. 
                AddPartialModifiersToTypeChain(documentEditor,
                    removeAttributesAndComments: false, removeTypeInheritance: false);
                documentEditor.RemoveNode(State.TypeNode, SyntaxRemoveOptions.KeepUnbalancedDirectives);

                var updatedDocument = documentEditor.GetChangedDocument();

                return updatedDocument.Project.Solution;
            }

            /// <summary>
            /// Traverses the syntax tree of the forked document and
            /// collects a list of nodes that are not being moved.
            /// This list of nodes are then removed from the forked copy.
            /// </summary>
            /// <param name="root">root, of the syntax tree of forked document</param>
            /// <returns>list of syntax nodes, to be removed from the forked copy.</returns>
            private ISet<SyntaxNode> GetMembersToRemove(SyntaxNode root)
            {
                var spine = new HashSet<SyntaxNode>();

                // collect the parent chain of declarations to keep.
                spine.AddRange(State.TypeNode.GetAncestors());

                // get potential namespace, types and members to remove.
                var removableCandidates = root
                    .DescendantNodes(n => spine.Contains(n))
                    .Where(n => FilterToTopLevelMembers(n, State.TypeNode)).ToSet();

                // diff candidates with items we want to keep.
                removableCandidates.ExceptWith(spine);

#if DEBUG
                // None of the nodes we're removing should also have any of their parent
                // nodes removed.  If that happened we could get a crash by first trying to remove
                // the parent, then trying to remove the child.
                foreach (var node in removableCandidates)
                {
                    foreach (var ancestor in node.GetAncestors())
                    {
                        Debug.Assert(!removableCandidates.Contains(ancestor));
                    }
                }
#endif

                return removableCandidates;
            }

            private static bool FilterToTopLevelMembers(SyntaxNode node, SyntaxNode typeNode)
            {
                // We never filter out the actual node we're trying to keep around.
                if (node == typeNode)
                {
                    return false;
                }

                return node is TTypeDeclarationSyntax or
                       TMemberDeclarationSyntax or
                       TNamespaceDeclarationSyntax;
            }

            /// <summary>
            /// if a nested type is being moved, this ensures its containing type is partial.
            /// </summary>
            private void AddPartialModifiersToTypeChain(
                DocumentEditor documentEditor,
                bool removeAttributesAndComments,
                bool removeTypeInheritance)
            {
                var semanticFacts = State.SemanticDocument.Document.GetRequiredLanguageService<ISemanticFactsService>();
                var typeChain = State.TypeNode.Ancestors().OfType<TTypeDeclarationSyntax>();

                foreach (var node in typeChain)
                {
                    var symbol = (ITypeSymbol?)State.SemanticDocument.SemanticModel.GetDeclaredSymbol(node, CancellationToken);
                    Contract.ThrowIfNull(symbol);
                    if (!semanticFacts.IsPartial(symbol, CancellationToken))
                    {
                        documentEditor.SetModifiers(node,
                            documentEditor.Generator.GetModifiers(node) | DeclarationModifiers.Partial);
                    }

                    if (removeAttributesAndComments)
                    {
                        documentEditor.RemoveAllAttributes(node);
                        documentEditor.RemoveAllComments(node);
                    }

                    if (removeTypeInheritance)
                    {
                        documentEditor.RemoveAllTypeInheritance(node);
                    }
                }

                documentEditor.ReplaceNode(State.TypeNode,
                    (currentNode, generator) =>
                    {
                        var currentTypeNode = (TTypeDeclarationSyntax)currentNode;

                        // Trim leading blank lines from the type so we don't have an 
                        // excessive number of them.
                        return RemoveLeadingBlankLines(currentTypeNode);
                    });
            }

            private TTypeDeclarationSyntax RemoveLeadingBlankLines(
                TTypeDeclarationSyntax currentTypeNode)
            {
                var syntaxFacts = State.SemanticDocument.Document.GetRequiredLanguageService<ISyntaxFactsService>();
                var bannerService = State.SemanticDocument.Document.GetRequiredLanguageService<IFileBannerFactsService>();

                var withoutBlankLines = bannerService.GetNodeWithoutLeadingBlankLines(currentTypeNode);

                // Add an elastic marker so the formatter can add any blank lines it thinks are
                // important to have (i.e. after a block of usings/imports).
                return withoutBlankLines.WithPrependedLeadingTrivia(syntaxFacts.ElasticMarker);
            }
        }
    }
}
