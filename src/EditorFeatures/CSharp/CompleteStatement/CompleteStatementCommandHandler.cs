﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editor.Implementation.AutomaticCompletion;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Text.Shared.Extensions;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using VSCommanding = Microsoft.VisualStudio.Commanding;

namespace Microsoft.CodeAnalysis.Editor.CSharp.CompleteStatement
{
    /// <summary>
    /// When user types <c>;</c> in a statement, closing delimiters and semicolon are added and caret is placed after the semicolon
    /// </summary>
    [Export(typeof(VSCommanding.ICommandHandler))]
    [ContentType(ContentTypeNames.CSharpContentType)]
    [Name(nameof(CompleteStatementCommandHandler))]
    [Order(After = PredefinedCommandHandlerNames.Completion)]
    internal sealed class CompleteStatementCommandHandler : IChainedCommandHandler<TypeCharCommandArgs>
    {
        private readonly ITextUndoHistoryRegistry _undoHistoryRegistry;
        private readonly IEditorOperationsFactoryService _editorOperationsFactoryService;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public CompleteStatementCommandHandler(ITextUndoHistoryRegistry undoHistoryRegistry, IEditorOperationsFactoryService editorOperationsFactoryService)
        {
            _undoHistoryRegistry = undoHistoryRegistry;
            _editorOperationsFactoryService = editorOperationsFactoryService;
        }

        public string DisplayName => CSharpEditorResources.Complete_statement_on_semicolon;

        public void ExecuteCommand(TypeCharCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            BeforeExecuteCommand(args, executionContext);
            nextCommandHandler();
        }

        private void BeforeExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext executionContext)
        {
            if (args.TypedChar != ';')
            {
                return;
            }

            var caret = args.TextView.GetCaretPoint(args.SubjectBuffer);
            if (!caret.HasValue)
            {
                return;
            }

            var isCaretAtEndOfLine = ((SnapshotPoint)caret).Position == ((SnapshotPoint)caret).GetContainingLine().End;
            var document = caret.Value.Snapshot.GetOpenDocumentInCurrentContextWithChanges();
            if (document == null)
      
            {
                return;
            }

            var root = document.GetSyntaxRootSynchronously(executionContext.OperationContext.UserCancellationToken);
            var caretPosition = caret.Value.Position;

            var token = root.FindToken(caretPosition);

            var currentNode = token.Parent;

            // If cursor is right before an opening delimiter, start with node outside of delimiters. 
            // This covers cases like `obj.ToString$()`, where `token` references `(` but the caret isn't actually 
            // inside the argument list.
            if (token.IsKind(SyntaxKind.OpenBraceToken, SyntaxKind.OpenBracketToken, SyntaxKind.OpenParenToken)
                && token.Span.Start >= caretPosition)
            {
                currentNode = currentNode.Parent;
            }

            if (currentNode == null)
            {
                return;
            }

            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();
            if (!LooksLikeNodeInArgumentListForStatementCompletion(currentNode, syntaxFacts, isCaretAtEndOfLine))
            {
                return;
            }

            // verify all delimeters exist until you reach statement syntax that requires a semicolon
            while (!IsStatementOrFieldDeclaration(currentNode, syntaxFacts))
            {
                if (!ClosingDelimiterExistsIfNeeded(currentNode))
                {
                    // A required delimiter is missing; do not treat semicolon as statement completion
                    return;
                }

                if (currentNode.Parent == null)
                {
                    return;
                }

                currentNode = currentNode.Parent;
            }

            // if the statement syntax itself requires a closing delimeter, verify it is there
                if (StatementClosingDelimiterIsMissing(currentNode))
                {
                    // Example: missing final `)` in `do { } while (x$$`
                    return;
                }

            var semicolonPosition = GetSemicolonLocation(currentNode, caretPosition);

            // Place cursor after the statement
            args.TextView.TryMoveCaretToAndEnsureVisible(args.SubjectBuffer.CurrentSnapshot.GetPoint(GetEndPosition(root, semicolonPosition, currentNode.Kind())));
        }

        private int GetSemicolonLocation(SyntaxNode currentNode, int caretPosition)
        {
            if (currentNode.IsKind(SyntaxKind.ForStatement))
            {
                // in for statements, semicolon can go after initializer or after condition, depending on where the caret is located
                var forStatementSyntax = (ForStatementSyntax)currentNode;
                if (caretPosition > forStatementSyntax.Condition.SpanStart  && caretPosition < forStatementSyntax.Condition.Span.End)
                {
                    return forStatementSyntax.Condition.FullSpan.End;
                }

                if (caretPosition > forStatementSyntax.Declaration.Span.Start && caretPosition < forStatementSyntax.Declaration.Span.End)
                {
                    return forStatementSyntax.Declaration.FullSpan.End;
                }

                return forStatementSyntax.Incrementors.FullSpan.End;
            }

            return currentNode.Span.End;
        }

        /// <summary>
        /// Examines the enclosing statement-like syntax for an expression which is eligible for statement completion.
        /// </summary>
        /// <remarks>
        /// <para>This method tries to identify <paramref name="currentNode"/> as a node located within an argument
        /// list, where the immediately-containing statement resembles an "expression statement". This method returns
        /// <see langword="true"/> if the node matches a recognizable pattern of this form.</para>
        /// </remarks>
        private static bool LooksLikeNodeInArgumentListForStatementCompletion(SyntaxNode currentNode, 
            ISyntaxFactsService syntaxFacts, bool isCaretAtEndOfLine)
        {
            // work our way up the tree, looking for a node of interest within the current statement
            bool nodeFound = false;
            while (!IsStatementOrFieldDeclaration(currentNode, syntaxFacts))
            {
                if (currentNode.IsKind(SyntaxKind.ArgumentList, SyntaxKind.ArrayRankSpecifier, SyntaxKind.ParenthesizedExpression))
                {
                    // It's a node of interest
                    nodeFound = true;
                }

                // No special action is performed at this time if `;` is typed inside a string, including
                // interpolated strings.  
                if (IsInAString(currentNode, isCaretAtEndOfLine))
                {
                    return false;
                }

                // We reached the root without finding a statement
                if (currentNode.Parent == null)
                {
                    return false;
                }

                currentNode = currentNode.Parent;
            }

            // if we never found a statement, or a node of interest, or the statement kind is not a candidate for completion, return
            if (currentNode == null || !nodeFound || !StatementIsACandidate(currentNode))
            {
                return false;
            }

            Debug.Assert(currentNode != null);
            return true;
        }

        private static bool IsStatementOrFieldDeclaration(SyntaxNode currentNode, ISyntaxFactsService syntaxFacts)
            => (syntaxFacts.IsStatement(currentNode) || currentNode.IsKind(SyntaxKind.FieldDeclaration));

        private static bool IsInAString(SyntaxNode currentNode, bool isCaretAtEndOfLine)
            // If caret is at the end of the line, it is outside the string
            => (currentNode.IsKind(SyntaxKind.InterpolatedStringExpression, SyntaxKind.StringLiteralExpression)
                && !isCaretAtEndOfLine);

        private static bool StatementIsACandidate(SyntaxNode currentNode)
        {
            // if the statement kind ends in a semicolon, return true
            switch (currentNode.Kind())
            {
                case SyntaxKind.DoStatement:
                case SyntaxKind.ExpressionStatement:
                case SyntaxKind.GotoCaseStatement:
                case SyntaxKind.LocalDeclarationStatement:
                case SyntaxKind.ReturnStatement:
                case SyntaxKind.ThrowStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.FieldDeclaration:
                    return true;
                default:
                    return false;
            }
        }

        private static bool SemicolonIsMissing(SyntaxNode currentNode)
        {
            switch (currentNode.Kind())
            {
                case SyntaxKind.LocalDeclarationStatement:
                    return ((LocalDeclarationStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.ReturnStatement:
                    return ((ReturnStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.VariableDeclaration:
                    return SemicolonIsMissing(currentNode.Parent);
                case SyntaxKind.ThrowStatement:
                    return ((ThrowStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.DoStatement:
                    return ((DoStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.GetAccessorDeclaration:
                case SyntaxKind.SetAccessorDeclaration:
                    return ((AccessorDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.FieldDeclaration:
                    return ((FieldDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.ForStatement:
                    return ((ForStatementSyntax)currentNode).FirstSemicolonToken.IsMissing;
                case SyntaxKind.ExpressionStatement:
                    return ((ExpressionStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.EmptyStatement:
                    return ((EmptyStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.GotoStatement:
                    return ((GotoStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.BreakStatement:
                    return ((BreakStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.ContinueStatement:
                    return ((ContinueStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.YieldReturnStatement:
                case SyntaxKind.YieldBreakStatement:
                    return ((YieldStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.LocalFunctionStatement:
                    return ((LocalFunctionStatementSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.NamespaceDeclaration:
                    return ((NamespaceDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.UsingDirective:
                    return ((UsingDirectiveSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.ExternAliasDirective:
                    return ((ExternAliasDirectiveSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.ClassDeclaration:
                    return ((ClassDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.StructDeclaration:
                    return ((StructDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.InterfaceDeclaration:
                    return ((InterfaceDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.EnumDeclaration:
                    return ((EnumDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.DelegateDeclaration:
                    return ((DelegateDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.EventFieldDeclaration:
                    return ((EventFieldDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.MethodDeclaration:
                    return ((MethodDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.OperatorDeclaration:
                    return ((OperatorDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.ConversionOperatorDeclaration:
                    return ((ConversionOperatorDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.ConstructorDeclaration:
                    return ((ConstructorDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.BaseConstructorInitializer:
                case SyntaxKind.ThisConstructorInitializer:
                    return ((ConstructorDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.DestructorDeclaration:
                    return ((DestructorDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.PropertyDeclaration:
                    return ((PropertyDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.IndexerDeclaration:
                    return ((IndexerDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                case SyntaxKind.AddAccessorDeclaration:
                    return ((AccessorDeclarationSyntax)currentNode).SemicolonToken.IsMissing;
                default:
                    // At this point, the node should be empty or its children should not end with a semicolon.
                    Debug.Assert(!currentNode.ChildNodesAndTokens().Any()
                        || !currentNode.ChildNodesAndTokens().Last().IsKind(SyntaxKind.SemicolonToken));
                    return false;
            }
        }

        /// <summary>
        /// Determines if a statement ends with a closing delimiter, and that closing delimiter exists.
        /// </summary>
        /// <remarks>
        /// <para>Statements such as <c>do { } while (expression);</c> contain embedded enclosing delimiters immediately
        /// preceding the semicolon. These delimiters are not part of the expression, but they behave like an argument
        /// list for the purposes of identifying relevant places for statement completion:</para>
        /// <list type="bullet">
        /// <item><description>The closing delimiter is typically inserted by the Automatic Brace Compeltion feature.</description></item>
        /// <item><description>It is not syntactically valid to place a semicolon <em>directly</em> within the delimiters.</description></item>
        /// </list>
        /// </remarks>
        /// <param name="currentNode"></param>
        /// <returns><see langword="true"/> if <paramref name="currentNode"/> is a statement that ends with a closing
        /// delimiter, and that closing delimiter exists in the source code; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool StatementClosingDelimiterIsMissing(SyntaxNode currentNode)
        {
            switch (currentNode.Kind())
            {
                case SyntaxKind.DoStatement:
                    var dostatement = (DoStatementSyntax)currentNode;
                    return dostatement.CloseParenToken.IsMissing;
                case SyntaxKind.ForStatement:
                    var forStatement = (ForStatementSyntax)currentNode;
                    return forStatement.CloseParenToken.IsMissing;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines if a syntax node includes all required closing delimiters.
        /// </summary>
        /// <remarks>
        /// <para>Some syntax nodes, such as parenthesized expressions, require a matching closing delimiter to end the
        /// syntax node. If this node is omitted from the source code, the parser will automatically insert a zero-width
        /// "missing" closing delimiter token to produce a valid syntax tree. This method determines if required closing
        /// delimiters are present in the original source.</para>
        /// </remarks>
        /// <param name="currentNode"></param>
        /// <returns>
        /// <list type="bullet">
        /// <item><description><see langword="true"/> if <paramref name="currentNode"/> requires a closing delimiter and the closing delimiter is present in the source (i.e. not missing)</description></item>
        /// <item><description><see langword="true"/> if <paramref name="currentNode"/> does not require a closing delimiter</description></item>
        /// <item><description>otherwise, <see langword="false"/>.</description></item>
        /// </list>
        /// </returns>
        private static bool ClosingDelimiterExistsIfNeeded(SyntaxNode currentNode)
        {
            switch (currentNode.Kind())
            {
                case SyntaxKind.ArgumentList:
                    var argumentList = (ArgumentListSyntax)currentNode;
                    return !argumentList.CloseParenToken.IsMissing;

                case SyntaxKind.ParenthesizedExpression:
                    var parenthesizedExpression = (ParenthesizedExpressionSyntax)currentNode;
                    return !parenthesizedExpression.CloseParenToken.IsMissing;

                case SyntaxKind.BracketedArgumentList:
                    var bracketedArgumentList = (BracketedArgumentListSyntax)currentNode;
                    return !bracketedArgumentList.CloseBracketToken.IsMissing;

                case SyntaxKind.ObjectInitializerExpression:
                    var initializerExpressionSyntax = (InitializerExpressionSyntax)currentNode;
                    return !initializerExpressionSyntax.CloseBraceToken.IsMissing;

                case SyntaxKind.ArrayRankSpecifier:
                    var arrayRankSpecifierSyntax = (ArrayRankSpecifierSyntax)currentNode;
                    return !arrayRankSpecifierSyntax.CloseBracketToken.IsMissing;
                default:
                    // Type of node does not require a closing delimiter
                    return true;
            }
        }

        private static int GetEndPosition(SyntaxNode root, int end, SyntaxKind nodeKind)
        {

            // If "end" is at the end of a line, the token has trailing end of line trivia.
            // We want to put our cursor before that trivia, so use previous token for placement.
            var token = root.FindToken(end);
            return token.GetPreviousToken().Span.End;
        }

        public VSCommanding.CommandState GetCommandState(TypeCharCommandArgs args, Func<VSCommanding.CommandState> nextCommandHandler) => nextCommandHandler();

    }
}
