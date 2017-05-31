// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.Packaging;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeFixes.AddImport
{
    internal abstract partial class AbstractAddImportCodeFixProvider<TSimpleNameSyntax>
    {
        private partial class PackageReference : Reference
        {
            private readonly IPackageInstallerService _installerService;
            private readonly string _source;
            private readonly string _packageName;
            private readonly string _versionOpt;

            public PackageReference(
                AbstractAddImportCodeFixProvider<TSimpleNameSyntax> provider,
                IPackageInstallerService installerService,
                SearchResult searchResult,
                string source,
                string packageName,
                string versionOpt)
                : base(provider, searchResult)
            {
                _installerService = installerService;
                _source = source;
                _packageName = packageName;
                _versionOpt = versionOpt;
            }

            public override async Task<CodeAction> CreateCodeActionAsync(
                Document document, SyntaxNode node, bool placeSystemNamespaceFirst, CancellationToken cancellationToken)
            {
                var originalDocument = document;

                (node, document) = await this.ReplaceNameNodeAsync(
                    node, document, cancellationToken).ConfigureAwait(false);

                var newDocument = await this.provider.AddImportAsync(
                    node, this.SearchResult.NameParts, document, placeSystemNamespaceFirst, cancellationToken).ConfigureAwait(false);

                var cleanedDocument = await CodeAction.CleanupDocumentAsync(
                    newDocument, cancellationToken).ConfigureAwait(false);

                return new ParentCodeAction(this, originalDocument, cleanedDocument);
            }

            public override bool Equals(object obj)
            {
                var reference = obj as PackageReference;
                return base.Equals(obj) &&
                    _packageName == reference._packageName &&
                    _versionOpt == reference._versionOpt;
            }

            public override int GetHashCode()
            {
                return Hash.Combine(_versionOpt,
                    Hash.Combine(_packageName, base.GetHashCode()));
            }
        }
    }
}
