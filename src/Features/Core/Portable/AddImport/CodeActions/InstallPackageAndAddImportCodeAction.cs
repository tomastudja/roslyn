﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.AddPackage;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.AddImport
{
    internal abstract partial class AbstractAddImportCodeFixProvider<TSimpleNameSyntax>
    {
            private class InstallPackageAndAddImportCodeAction : CodeAction
            {
                /// <summary>
                /// The document before we added the import. Used so we can roll back if installing
                /// the package failed.
                /// </summary>
                private readonly Document _originalDocument;

                /// <summary>
                /// The changes to make to <see cref="_originalDocument"/> to add the import.
                /// </summary>
                private readonly ImmutableArray<TextChange> _textChanges;

                /// <summary>
                /// The operation that will actually install the nuget package.
                /// </summary>
                private readonly InstallPackageDirectlyCodeActionOperation _installOperation;

                public override string Title { get; }
                public override string EquivalenceKey => Title;
                internal override CodeActionPriority Priority { get; }

                public InstallPackageAndAddImportCodeAction(
                    Document originalDocument,
                    ImmutableArray<TextChange> textChanges,
                    string title,
                    CodeActionPriority priority,
                    InstallPackageDirectlyCodeActionOperation installOperation)
            {
                    _originalDocument = originalDocument;
                    _textChanges = textChanges;
                    Title = title;
                    Priority = priority;
                    _installOperation = installOperation;
                }

                /// <summary>
                /// For preview purposes we return all the operations in a list.  This way the 
                /// preview system stiches things together in the UI to make a suitable display.
                /// i.e. if we have a SolutionChangedOperation and some other operation with a 
                /// Title, then the UI will show that nicely to the user.
                /// </summary>
                protected override async Task<IEnumerable<CodeActionOperation>> ComputePreviewOperationsAsync(CancellationToken cancellationToken)
                {
                    // Make a SolutionChangeAction.  This way we can let it generate the diff
                    // preview appropriately.
                    var solutionChangeAction = new SolutionChangeAction(
                        "", c => GetUpdatedSolutionAsync(c));

                    var result = ArrayBuilder<CodeActionOperation>.GetInstance();
                    result.AddRange(await solutionChangeAction.GetPreviewOperationsAsync(cancellationToken).ConfigureAwait(false));
                    result.Add(_installOperation);
                    return result.ToImmutableAndFree();
                }

                private async Task<Solution> GetUpdatedSolutionAsync(CancellationToken cancellationToken)
                {
                    var oldText = await _originalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var newText = oldText.WithChanges(_textChanges);

                    var newDocument = _originalDocument.WithText(newText);
                    var newRoot = await newDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

                    // Suppress diagnostics on the import we create.  Because we only get here when we are 
                    // adding a nuget package, it is certainly the case that in the preview this will not
                    // bind properly.  It will look silly to show such an error, so we just suppress things.
                    var updatedRoot = newRoot.WithAdditionalAnnotations(SuppressDiagnosticsAnnotation.Create());
                    var updatedDocument = newDocument.WithSyntaxRoot(updatedRoot);

                    return updatedDocument.Project.Solution;
                }

                /// <summary>
                /// However, for application purposes, we end up returning a single operation
                /// that will then apply all our sub actions in order, stopping the moment
                /// one of them fails.
                /// </summary>
                protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(
                    CancellationToken cancellationToken)
                {
                    var oldText = await _originalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    var newText = oldText.WithChanges(_textChanges);

                    return ImmutableArray.Create<CodeActionOperation>(
                        new InstallPackageAndAddImportOperation(
                            _originalDocument.Id, oldText, newText, _installOperation));
                }
            }

            private class InstallPackageAndAddImportOperation : CodeActionOperation
            {
                private readonly DocumentId _changedDocumentId;
                private readonly SourceText _oldText;
                private readonly SourceText _newText;
                private readonly InstallPackageDirectlyCodeActionOperation _installPackageOperation;

                public InstallPackageAndAddImportOperation(
                    DocumentId changedDocumentId,
                    SourceText oldText,
                    SourceText newText,
                    InstallPackageDirectlyCodeActionOperation item2)
                {
                    _changedDocumentId = changedDocumentId;
                    _oldText = oldText;
                    _newText = newText;
                    _installPackageOperation = item2;
                }

                internal override bool ApplyDuringTests => _installPackageOperation.ApplyDuringTests;
                public override string Title => _installPackageOperation.Title;

                internal override bool TryApply(Workspace workspace, IProgressTracker progressTracker, CancellationToken cancellationToken)
                {
                    var newSolution = workspace.CurrentSolution.WithDocumentText(
                        _changedDocumentId, _newText);

                    // First make the changes to add the import to the document.
                    if (workspace.TryApplyChanges(newSolution, progressTracker))
                    {
                        if (_installPackageOperation.TryApply(workspace, progressTracker, cancellationToken))
                        {
                            return true;
                        }

                        // Installing the nuget package failed.  Roll back the workspace.
                        var rolledBackSolution = workspace.CurrentSolution.WithDocumentText(
                            _changedDocumentId, _oldText);
                        workspace.TryApplyChanges(rolledBackSolution, progressTracker);
                    }

                    return false;
                }
            }
    }
}