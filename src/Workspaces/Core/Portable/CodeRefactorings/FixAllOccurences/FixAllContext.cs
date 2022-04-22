﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixesAndRefactorings;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using FixAllScope = Microsoft.CodeAnalysis.CodeFixes.FixAllScope;

namespace Microsoft.CodeAnalysis.CodeRefactorings
{
    /// <summary>
    /// Context for "Fix all occurrences" for code refactorings provided by each <see cref="CodeRefactoringProvider"/>.
    /// </summary>
    /// <remarks>
    /// TODO: Make public, tracked with https://github.com/dotnet/roslyn/issues/60703
    /// </remarks>
    internal sealed class FixAllContext : IFixAllContext
    {
        internal FixAllState State { get; }

        internal FixAllProvider FixAllProvider => State.FixAllProvider;

        /// <summary>
        /// Document within which fix all occurrences was triggered.
        /// </summary>
        public Document Document => State.Document!;

        /// <summary>
        /// Original selection span with which fix all occurences was triggered.
        /// </summary>
        public TextSpan SelectionSpan => State.SelectionSpan;

        /// <summary>
        /// Underlying <see cref="CodeRefactoringProvider"/> which triggered this fix all.
        /// </summary>
        public CodeRefactoringProvider CodeRefactoringProvider => State.Provider;

        /// <summary>
        /// <see cref="FixAllScope"/> to fix all occurrences.
        /// </summary>
        public FixAllScope Scope => State.Scope;

        /// <summary>
        /// The <see cref="CodeAction.EquivalenceKey"/> value expected of a <see cref="CodeAction"/> participating in this fix all.
        /// </summary>
        public string? CodeActionEquivalenceKey => State.CodeActionEquivalenceKey;

        /// <summary>
        /// CancellationToken for fix all session.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        internal IProgressTracker ProgressTracker { get; }

        /// <summary>
        /// Project to fix all occurrences.
        /// Note that this property will always be the containing project of <see cref="Document"/>
        /// for publicly exposed FixAllContext instance. However, we might create an intermediate FixAllContext
        /// with null <see cref="Document"/> and non-null Project, so we require this internal property for intermediate computation.
        /// </summary>
        public Project Project => State.Project;

        public Solution Solution => Project.Solution;

        #region IFixAllContext implementation
        IFixAllState IFixAllContext.State => this.State;

        IFixAllProvider? IFixAllContext.FixAllProvider => this.FixAllProvider;

        object IFixAllContext.Provider => this.CodeRefactoringProvider;

        IProgressTracker IFixAllContext.ProgressTracker => this.ProgressTracker;

        string IFixAllContext.GetDefaultFixAllTitle() => this.GetDefaultFixAllTitle();

        IFixAllContext IFixAllContext.With(
            Optional<(Document? document, Project project)> documentAndProject,
            Optional<FixAllScope> scope,
            Optional<string?> codeActionEquivalenceKey)
            => this.With(documentAndProject, scope, codeActionEquivalenceKey);
        #endregion

        internal FixAllContext(
            FixAllState state,
            IProgressTracker progressTracker,
            CancellationToken cancellationToken)
        {
            State = state;
            this.ProgressTracker = progressTracker;
            this.CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Gets a new <see cref="FixAllContext"/> with the given cancellationToken.
        /// </summary>
        public FixAllContext WithCancellationToken(CancellationToken cancellationToken)
        {
            if (this.CancellationToken == cancellationToken)
            {
                return this;
            }

            return new FixAllContext(State, this.ProgressTracker, cancellationToken);
        }

        /// <summary>
        /// Gets the spans to fix by document for the <see cref="Scope"/> for this fix all occurences fix.
        /// </summary>
        public Task<ImmutableDictionary<Document, ImmutableArray<TextSpan>>> GetFixAllSpansAsync(CancellationToken cancellationToken)
            => State.GetFixAllSpansAsync(cancellationToken);

        internal FixAllContext With(
            Optional<(Document? document, Project project)> documentAndProject = default,
            Optional<FixAllScope> scope = default,
            Optional<string?> codeActionEquivalenceKey = default)
        {
            var newState = State.With(documentAndProject, scope, codeActionEquivalenceKey);
            return State == newState ? this : new FixAllContext(newState, ProgressTracker, CancellationToken);
        }

        internal string GetDefaultFixAllTitle()
            => FixAllHelper.GetDefaultFixAllTitle(this.Scope, this.State.CodeActionTitle, this.Document, this.Project);
    }
}
