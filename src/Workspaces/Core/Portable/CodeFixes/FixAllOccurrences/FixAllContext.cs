﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CodeFixes
{
    /// <summary>
    /// Context for "Fix all occurrences" code fixes provided by an <see cref="FixAllProvider"/>.
    /// </summary>
    public partial class FixAllContext
    {
        private readonly DiagnosticProvider _diagnosticProvider;

        /// <summary>
        /// Solution to fix all occurrences.
        /// </summary>
        public Solution Solution { get { return this.Project.Solution; } }

        /// <summary>
        /// Project within which fix all occurrences was triggered.
        /// </summary>
        public Project Project { get; }

        /// <summary>
        /// Document within which fix all occurrences was triggered.
        /// Can be null if the context was created using <see cref="FixAllContext.FixAllContext(Project, CodeFixProvider, FixAllScope, string, IEnumerable{string}, DiagnosticProvider, CancellationToken)"/>.
        /// </summary>
        public Document Document { get; }

        /// <summary>
        /// Underlying <see cref="CodeFixes.CodeFixProvider"/> which triggered this fix all.
        /// </summary>
        public CodeFixProvider CodeFixProvider { get; }

        /// <summary>
        /// <see cref="FixAllScope"/> to fix all occurrences.
        /// </summary>
        public FixAllScope Scope { get; }

        /// <summary>
        /// Diagnostic Ids to fix.
        /// Note that <see cref="GetDocumentDiagnosticsAsync(Document)"/>, <see cref="GetProjectDiagnosticsAsync(Project)"/> and <see cref="GetAllDiagnosticsAsync(Project)"/> methods
        /// return only diagnostics whose IDs are contained in this set of Ids.
        /// </summary>
        public ImmutableHashSet<string> DiagnosticIds { get; }

        /// <summary>
        /// The <see cref="CodeAction.EquivalenceKey"/> value expected of a <see cref="CodeAction"/> participating in this fix all.
        /// </summary>
        public string CodeActionEquivalenceKey { get; }

        /// <summary>
        /// CancellationToken for fix all session.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Creates a new <see cref="FixAllContext"/>.
        /// Use this overload when applying fix all to a diagnostic with a source location.
        /// </summary>
        /// <param name="document">Document within which fix all occurrences was triggered.</param>
        /// <param name="codeFixProvider">Underlying <see cref="CodeFixes.CodeFixProvider"/> which triggered this fix all.</param>
        /// <param name="scope"><see cref="FixAllScope"/> to fix all occurrences.</param>
        /// <param name="codeActionEquivalenceKey">The <see cref="CodeAction.EquivalenceKey"/> value expected of a <see cref="CodeAction"/> participating in this fix all.</param>
        /// <param name="diagnosticIds">Diagnostic Ids to fix.</param>
        /// <param name="fixAllDiagnosticProvider">
        /// <see cref="DiagnosticProvider"/> to fetch document/project diagnostics to fix in a <see cref="FixAllContext"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for fix all computation.</param>
        public FixAllContext(
            Document document,
            CodeFixProvider codeFixProvider,
            FixAllScope scope,
            string codeActionEquivalenceKey,
            IEnumerable<string> diagnosticIds,
            DiagnosticProvider fixAllDiagnosticProvider,
            CancellationToken cancellationToken)
            : this(document, document.Project, codeFixProvider, scope, codeActionEquivalenceKey, diagnosticIds, fixAllDiagnosticProvider, cancellationToken)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
        }

        /// <summary>
        /// Creates a new <see cref="FixAllContext"/>.
        /// Use this overload when applying fix all to a diagnostic with no source location, i.e. <see cref="Location.None"/>.
        /// </summary>
        /// <param name="project">Project within which fix all occurrences was triggered.</param>
        /// <param name="codeFixProvider">Underlying <see cref="CodeFixes.CodeFixProvider"/> which triggered this fix all.</param>
        /// <param name="scope"><see cref="FixAllScope"/> to fix all occurrences.</param>
        /// <param name="codeActionEquivalenceKey">The <see cref="CodeAction.EquivalenceKey"/> value expected of a <see cref="CodeAction"/> participating in this fix all.</param>
        /// <param name="diagnosticIds">Diagnostic Ids to fix.</param>
        /// <param name="fixAllDiagnosticProvider">
        /// <see cref="DiagnosticProvider"/> to fetch document/project diagnostics to fix in a <see cref="FixAllContext"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for fix all computation.</param>
        public FixAllContext(
            Project project,
            CodeFixProvider codeFixProvider,
            FixAllScope scope,
            string codeActionEquivalenceKey,
            IEnumerable<string> diagnosticIds,
            DiagnosticProvider fixAllDiagnosticProvider,
            CancellationToken cancellationToken)
            : this(null, project, codeFixProvider, scope, codeActionEquivalenceKey, diagnosticIds, fixAllDiagnosticProvider, cancellationToken)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }
        }

        private FixAllContext(
            Document document,
            Project project,
            CodeFixProvider codeFixProvider,
            FixAllScope scope,
            string codeActionEquivalenceKey,
            IEnumerable<string> diagnosticIds,
            DiagnosticProvider fixAllDiagnosticProvider,
            CancellationToken cancellationToken)
        {
            Contract.ThrowIfNull(project);

            if (codeFixProvider == null)
            {
                throw new ArgumentNullException(nameof(codeFixProvider));
            }

            if (diagnosticIds == null)
            {
                throw new ArgumentNullException(nameof(diagnosticIds));
            }

            if (diagnosticIds.Any(d => d == null))
            {
                throw new ArgumentException(WorkspacesResources.DiagnosticCannotBeNull, nameof(diagnosticIds));
            }

            if (fixAllDiagnosticProvider == null)
            {
                throw new ArgumentNullException(nameof(fixAllDiagnosticProvider));
            }

            this.Document = document;
            this.Project = project;
            this.CodeFixProvider = codeFixProvider;
            this.Scope = scope;
            this.CodeActionEquivalenceKey = codeActionEquivalenceKey;
            this.DiagnosticIds = ImmutableHashSet.CreateRange(diagnosticIds);
            _diagnosticProvider = fixAllDiagnosticProvider;
            this.CancellationToken = cancellationToken;
        }

        internal DiagnosticProvider GetDiagnosticProvider() => _diagnosticProvider;

        /// <summary>
        /// Gets all the diagnostics in the given document filtered by <see cref="DiagnosticIds"/>.
        /// </summary>
        public async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (this.Project.Language != document.Project.Language)
            {
                return ImmutableArray<Diagnostic>.Empty;
            }

            var getDiagnosticsTask = _diagnosticProvider.GetDocumentDiagnosticsAsync(document, this.CancellationToken);
            return await GetFilteredDiagnosticsAsync(getDiagnosticsTask, this.DiagnosticIds).ConfigureAwait(false);
        }

        private static async Task<ImmutableArray<Diagnostic>> GetFilteredDiagnosticsAsync(Task<IEnumerable<Diagnostic>> getDiagnosticsTask, ImmutableHashSet<string> diagnosticIds)
        {
            if (getDiagnosticsTask != null)
            {
                var diagnostics = await getDiagnosticsTask.ConfigureAwait(false);
                if (diagnostics != null)
                {
                    return diagnostics.Where(d => d != null && diagnosticIds.Contains(d.Id)).ToImmutableArray();
                }
            }

            return ImmutableArray<Diagnostic>.Empty;
        }

        /// <summary>
        /// Gets all the project-level diagnostics, i.e. diagnostics with no source location, in the given project filtered by <see cref="DiagnosticIds"/>.
        /// </summary>
        public async Task<ImmutableArray<Diagnostic>> GetProjectDiagnosticsAsync(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return await GetProjectDiagnosticsAsync(project, includeAllDocumentDiagnostics: false).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all the diagnostics in the given project filtered by <see cref="DiagnosticIds"/>.
        /// This includes both document-level diagnostics for all documents in the given project and project-level diagnostics, i.e. diagnostics with no source location, in the given project. 
        /// </summary>
        public async Task<ImmutableArray<Diagnostic>> GetAllDiagnosticsAsync(Project project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            return await GetProjectDiagnosticsAsync(project, includeAllDocumentDiagnostics: true).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all the project diagnostics in the given project filtered by <see cref="DiagnosticIds"/>.
        /// If <paramref name="includeAllDocumentDiagnostics"/> is false, then returns only project-level diagnostics which have no source location.
        /// Otherwise, returns all diagnostics in the project, including the document diagnostics for all documents in the given project.
        /// </summary>
        private async Task<ImmutableArray<Diagnostic>> GetProjectDiagnosticsAsync(Project project, bool includeAllDocumentDiagnostics)
        {
            Contract.ThrowIfNull(project);

            if (this.Project.Language != project.Language)
            {
                return ImmutableArray<Diagnostic>.Empty;
            }

            var getDiagnosticsTask = includeAllDocumentDiagnostics ?
                _diagnosticProvider.GetAllDiagnosticsAsync(project, CancellationToken) :
                _diagnosticProvider.GetProjectDiagnosticsAsync(project, CancellationToken);
            return await GetFilteredDiagnosticsAsync(getDiagnosticsTask, this.DiagnosticIds).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets a new <see cref="FixAllContext"/> with the given cancellationToken.
        /// </summary>
        public FixAllContext WithCancellationToken(CancellationToken cancellationToken)
        {
            // TODO: We should change this API to be a virtual method, as the class is not sealed.
            //       For now, special case FixMultipleContext.
            var fixMultipleContext = this as FixMultipleContext;
            if (fixMultipleContext != null)
            {
                return fixMultipleContext.WithCancellationToken(cancellationToken);
            }

            if (this.CancellationToken == cancellationToken)
            {
                return this;
            }

            return new FixAllContext(
                this.Document,
                this.Project,
                this.CodeFixProvider,
                this.Scope,
                this.CodeActionEquivalenceKey,
                this.DiagnosticIds,
                _diagnosticProvider,
                cancellationToken);
        }

        internal static FixAllContext Create(
            Document document,
            FixAllProviderInfo fixAllProviderInfo,
            CodeFixProvider originalFixProvider,
            IEnumerable<Diagnostic> originalFixDiagnostics,
            Func<Document, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getDocumentDiagnosticsAsync,
            Func<Project, bool, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getProjectDiagnosticsAsync,
            CancellationToken cancellationToken)
        {
            var diagnosticIds = GetFixAllDiagnosticIds(fixAllProviderInfo, originalFixDiagnostics).ToImmutableHashSet();
            var diagnosticProvider = new FixAllDiagnosticProvider(diagnosticIds, getDocumentDiagnosticsAsync, getProjectDiagnosticsAsync);
            return new FixAllContext(
                document: document,
                codeFixProvider: originalFixProvider,
                scope: FixAllScope.Document,
                codeActionEquivalenceKey: null,
                diagnosticIds: diagnosticIds,
                fixAllDiagnosticProvider: diagnosticProvider,
                cancellationToken: cancellationToken);
        }

        internal static FixAllContext Create(
            Project project,
            FixAllProviderInfo fixAllProviderInfo,
            CodeFixProvider originalFixProvider,
            IEnumerable<Diagnostic> originalFixDiagnostics,
            Func<Document, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getDocumentDiagnosticsAsync,
            Func<Project, bool, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getProjectDiagnosticsAsync,
            CancellationToken cancellationToken)
        {
            var diagnosticIds = GetFixAllDiagnosticIds(fixAllProviderInfo, originalFixDiagnostics).ToImmutableHashSet();
            var diagnosticProvider = new FixAllDiagnosticProvider(diagnosticIds, getDocumentDiagnosticsAsync, getProjectDiagnosticsAsync);
            return new FixAllContext(
                project: project,
                codeFixProvider: originalFixProvider,
                scope: FixAllScope.Project,
                codeActionEquivalenceKey: null, diagnosticIds: diagnosticIds,
                fixAllDiagnosticProvider: diagnosticProvider,
                cancellationToken: cancellationToken);
        }

        private static IEnumerable<string> GetFixAllDiagnosticIds(FixAllProviderInfo fixAllProviderInfo, IEnumerable<Diagnostic> originalFixDiagnostics)
        {
            return originalFixDiagnostics
                .Where(fixAllProviderInfo.CanBeFixed)
                .Select(d => d.Id);
        }

        // Internal for testing purposes.
        internal class FixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private readonly ImmutableHashSet<string> _diagnosticIds;

            /// <summary>
            /// Delegate to fetch diagnostics for any given document within the given fix all scope.
            /// This delegate is invoked by <see cref="GetDocumentDiagnosticsAsync(Document, CancellationToken)"/> with the given <see cref="_diagnosticIds"/> as arguments.
            /// </summary>
            private readonly Func<Document, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> _getDocumentDiagnosticsAsync;

            /// <summary>
            /// Delegate to fetch diagnostics for any given project within the given fix all scope.
            /// This delegate is invoked by <see cref="GetProjectDiagnosticsAsync(Project, CancellationToken)"/> and <see cref="GetAllDiagnosticsAsync(Project, CancellationToken)"/>
            /// with the given <see cref="_diagnosticIds"/> as arguments.
            /// The boolean argument to the delegate indicates whether or not to return location-based diagnostics, i.e.
            /// (a) False => Return only diagnostics with <see cref="Location.None"/>.
            /// (b) True => Return all project diagnostics, regardless of whether or not they have a location.
            /// </summary>
            private readonly Func<Project, bool, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> _getProjectDiagnosticsAsync;

            public FixAllDiagnosticProvider(
                ImmutableHashSet<string> diagnosticIds,
                Func<Document, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getDocumentDiagnosticsAsync,
                Func<Project, bool, ImmutableHashSet<string>, CancellationToken, Task<IEnumerable<Diagnostic>>> getProjectDiagnosticsAsync)
            {
                _diagnosticIds = diagnosticIds;
                _getDocumentDiagnosticsAsync = getDocumentDiagnosticsAsync;
                _getProjectDiagnosticsAsync = getProjectDiagnosticsAsync;
            }

            public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            {
                return _getDocumentDiagnosticsAsync(document, _diagnosticIds, cancellationToken);
            }

            public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return _getProjectDiagnosticsAsync(project, true, _diagnosticIds, cancellationToken);
            }

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return _getProjectDiagnosticsAsync(project, false, _diagnosticIds, cancellationToken);
            }
        }
    }
}
