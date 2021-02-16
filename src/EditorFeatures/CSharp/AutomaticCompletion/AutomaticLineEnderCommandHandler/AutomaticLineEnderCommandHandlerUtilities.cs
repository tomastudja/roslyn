﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.CSharp.AutomaticCompletion
{
    internal static class AutomaticLineEnderCommandHandlerUtilities
    {
        #region ShouldAddBrace

        // For namespace, make sure it has name there is no braces
        public static bool ShouldAddBraceForNamespaceDeclaration(NamespaceDeclarationSyntax namespaceDeclarationNode, int caretPosition)
            => !namespaceDeclarationNode.Name.IsMissing
               && HasNoBrace(namespaceDeclarationNode)
               && !WithinAttributeLists(namespaceDeclarationNode, caretPosition)
               && !WithinBraces(namespaceDeclarationNode, caretPosition);

        // For class/struct/enum ..., make sure it has name and there is no braces.
        public static bool ShouldAddBraceForBaseTypeDeclaration(BaseTypeDeclarationSyntax baseTypeDeclarationNode, int caretPosition)
            => !baseTypeDeclarationNode.Identifier.IsMissing
               && HasNoBrace(baseTypeDeclarationNode)
               && !WithinAttributeLists(baseTypeDeclarationNode, caretPosition)
               && !WithinBraces(baseTypeDeclarationNode, caretPosition);

        // For method, make sure it has a ParameterList, because later braces would be inserted after the Parameterlist
        public static bool ShouldAddBraceForBaseMethodDeclaration(BaseMethodDeclarationSyntax baseMethodDeclarationNode, int caretPosition)
            => baseMethodDeclarationNode.ExpressionBody == null
               && baseMethodDeclarationNode.Body == null
               && !baseMethodDeclarationNode.ParameterList.IsMissing
               && baseMethodDeclarationNode.SemicolonToken.IsMissing
               && !WithinAttributeLists(baseMethodDeclarationNode, caretPosition)
               && !WithinMethodBody(baseMethodDeclarationNode, caretPosition)
               // Make sure we don't insert braces for method in Interface.
               && !baseMethodDeclarationNode.IsParentKind(SyntaxKind.InterfaceDeclaration);

        // For local Function, make sure it has a ParameterList, because later braces would be inserted after the Parameterlist
        public static bool ShouldAddBraceForLocalFunctionStatement(LocalFunctionStatementSyntax localFunctionStatementNode, int caretPosition)
            => localFunctionStatementNode.ExpressionBody == null
               && localFunctionStatementNode.Body == null
               && !localFunctionStatementNode.ParameterList.IsMissing
               && !WithinAttributeLists(localFunctionStatementNode, caretPosition)
               && !WithinMethodBody(localFunctionStatementNode, caretPosition);

        public static bool ShouldAddBraceForObjectCreationExpression(ObjectCreationExpressionSyntax objectCreationExpressionNode)
            => objectCreationExpressionNode.Initializer == null;

        /// <summary>
        /// Add braces for field and event field if they only have one variable, semicolon is missing and don't have readonly keyword
        /// Example:
        /// public int Bar$$ =>
        /// public int Bar
        /// {
        ///      $$
        /// }
        /// This would change field to property, and change event field to event declaration.
        /// </summary>
        public static bool ShouldAddBraceForBaseFieldDeclaration(BaseFieldDeclarationSyntax baseFieldDeclarationNode)
            => baseFieldDeclarationNode.Declaration.Variables.Count == 1
               && baseFieldDeclarationNode.Declaration.Variables[0].Initializer == null
               && !baseFieldDeclarationNode.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)
               && baseFieldDeclarationNode.SemicolonToken.IsMissing;

        public static bool ShouldAddBraceForAccessorDeclaration(AccessorDeclarationSyntax accessorDeclarationNode)
            => accessorDeclarationNode.Body == null
               && accessorDeclarationNode.ExpressionBody == null
               && accessorDeclarationNode.SemicolonToken.IsMissing;

        // For indexer, switch, try and catch syntax node without braces, if it is the last child of its parent, it would
        // use its parent's close brace as its own.
        // Example:
        // class Bar
        // {
        //      int th$$is[int i]
        // }
        // In this case, parser would think the last '}' belongs to the indexer, not the class.
        // Therefore, only check if the open brace is missing for these 4 types of SyntaxNode
        public static bool ShouldAddBraceForIndexerDeclaration(IndexerDeclarationSyntax indexerDeclarationNode, int caretPosition)
        {
            if (WithinAttributeLists(indexerDeclarationNode, caretPosition) ||
                WithinBraces(indexerDeclarationNode.AccessorList, caretPosition))
            {
                return false;
            }

            // Make sure it has brackets
            var (openBracket, closeBracket) = indexerDeclarationNode.ParameterList.GetBrackets();
            if (openBracket.IsMissing || closeBracket.IsMissing)
            {
                return false;
            }

            // If both accessorList and body is empty
            if ((indexerDeclarationNode.AccessorList == null || indexerDeclarationNode.AccessorList.IsMissing)
                && indexerDeclarationNode.ExpressionBody == null)
            {
                return true;
            }

            return indexerDeclarationNode.AccessorList != null
               && indexerDeclarationNode.AccessorList.OpenBraceToken.IsMissing;
        }

        public static bool ShouldAddBraceForSwitchStatement(SwitchStatementSyntax switchStatementNode)
            => !switchStatementNode.OpenParenToken.IsMissing
               && !switchStatementNode.CloseParenToken.IsMissing
               && switchStatementNode.OpenBraceToken.IsMissing;

        public static bool ShouldAddBraceForTryStatement(TryStatementSyntax tryStatementNode, int caretPosition)
            => !tryStatementNode.TryKeyword.IsMissing
               && tryStatementNode.Block.OpenBraceToken.IsMissing
               && !tryStatementNode.Block.Span.Contains(caretPosition);

        public static bool ShouldAddBraceForCatchClause(CatchClauseSyntax catchClauseSyntax, int caretPosition)
            => !catchClauseSyntax.CatchKeyword.IsMissing
               && catchClauseSyntax.Block.OpenBraceToken.IsMissing
               && !catchClauseSyntax.Block.Span.Contains(caretPosition);

        public static bool ShouldAddBraceForFinallyClause(FinallyClauseSyntax finallyClauseNode, int caretPosition)
            => !finallyClauseNode.FinallyKeyword.IsMissing
               && finallyClauseNode.Block.OpenBraceToken.IsMissing
               && !finallyClauseNode.Block.Span.Contains(caretPosition);

        // For all the embeddedStatementOwners,
        // if the embeddedStatement is not block, insert the the braces if its statement is not block.
        public static bool ShouldAddBraceForDoStatement(DoStatementSyntax doStatementNode, int caretPosition)
            => !doStatementNode.DoKeyword.IsMissing
               && doStatementNode.Statement is not BlockSyntax
               && doStatementNode.DoKeyword.Span.Contains(caretPosition);

        public static bool ShouldAddBraceForCommonForEachStatement(CommonForEachStatementSyntax commonForEachStatementNode, int caretPosition)
            => commonForEachStatementNode.Statement is not BlockSyntax
               && !commonForEachStatementNode.OpenParenToken.IsMissing
               && !commonForEachStatementNode.CloseParenToken.IsMissing
               && !WithinEmbeddedStatement(commonForEachStatementNode, caretPosition);

        public static bool ShouldAddBraceForForStatement(ForStatementSyntax forStatementNode, int caretPosition)
            => forStatementNode.Statement is not BlockSyntax
               && !forStatementNode.OpenParenToken.IsMissing
               && !forStatementNode.CloseParenToken.IsMissing
               && !WithinEmbeddedStatement(forStatementNode, caretPosition);

        public static bool ShouldAddBraceForIfStatement(IfStatementSyntax ifStatementNode, int caretPosition)
            => ifStatementNode.Statement is not BlockSyntax
               && !ifStatementNode.OpenParenToken.IsMissing
               && !ifStatementNode.CloseParenToken.IsMissing
               && !WithinEmbeddedStatement(ifStatementNode, caretPosition);

        public static bool ShouldAddBraceForElseClause(ElseClauseSyntax elseClauseNode, int caretPosition)
        {
            // In case it is an else-if clause, if the statement is IfStatement, use its insertion statement
            // otherwise, use the end of the else keyword
            // Example:
            // Before: if (a)
            //         {
            //         } else i$$f (b)
            // After: if (a)
            //        {
            //        } else if (b)
            //        {
            //            $$
            //        }
            if (elseClauseNode.Statement is IfStatementSyntax ifStatementNode)
            {
                return ShouldAddBraceForIfStatement(ifStatementNode, caretPosition);
            }
            else
            {
                // Here it should be an elseClause
                // like:
                // if (a)
                // {
                // } els$$e {
                // }
                // So only check the its statement
                return elseClauseNode.Statement is not BlockSyntax && !WithinEmbeddedStatement(elseClauseNode, caretPosition);
            }
        }

        public static bool ShouldAddBraceForLockStatement(LockStatementSyntax lockStatementNode, int caretPosition)
            => lockStatementNode.Statement is not BlockSyntax
               && !lockStatementNode.OpenParenToken.IsMissing
               && !lockStatementNode.CloseParenToken.IsMissing
               && !WithinEmbeddedStatement(lockStatementNode, caretPosition);

        public static bool ShouldAddBraceForUsingStatement(UsingStatementSyntax usingStatementNode, int caretPosition)
            => usingStatementNode.Statement is not BlockSyntax
               && !usingStatementNode.OpenParenToken.IsMissing
               && !usingStatementNode.CloseParenToken.IsMissing
               && !WithinEmbeddedStatement(usingStatementNode, caretPosition);

        public static bool ShouldAddBraceForWhileStatement(WhileStatementSyntax whileStatementNode, int caretPosition)
            => whileStatementNode.Statement is not BlockSyntax
               && !whileStatementNode.OpenParenToken.IsMissing
               && !whileStatementNode.CloseParenToken.IsMissing
               && !WithinEmbeddedStatement(whileStatementNode, caretPosition);

        private static bool WithinAttributeLists(SyntaxNode node, int caretPosition)
        {
            var attributeLists = node.GetAttributeLists();
            return attributeLists.Span.Contains(caretPosition);
        }

        private static bool WithinBraces(SyntaxNode? node, int caretPosition)
        {
            var (openBrace, closeBrace) = node.GetBraces();
            return TextSpan.FromBounds(openBrace.SpanStart, closeBrace.Span.End).Contains(caretPosition);
        }

        private static bool WithinMethodBody(SyntaxNode node, int caretPosition)
        {
            if (node is BaseMethodDeclarationSyntax methodDeclarationNode)
            {
                return methodDeclarationNode.Body?.Span.Contains(caretPosition) ?? false;
            }

            if (node is LocalFunctionStatementSyntax localFunctionStatementNode)
            {
                return localFunctionStatementNode.Body?.Span.Contains(caretPosition) ?? false;
            }

            return false;
        }

        private static bool HasNoBrace(SyntaxNode node)
        {
            var (openBrace, closeBrace) = node.GetBraces();
            return openBrace.IsKind(SyntaxKind.None) && closeBrace.IsKind(SyntaxKind.None)
                || openBrace.IsMissing && closeBrace.IsMissing;
        }

        private static bool WithinEmbeddedStatement(SyntaxNode node, int caretPosition)
            => node.GetEmbeddedStatement()?.Span.Contains(caretPosition) ?? false;

        #endregion

        #region ShouldRemoveBrace

        // Remove the braces if the ObjectCreationExpression has an empty Initializer.
        public static bool ShouldRemoveBraceForObjectCreationExpression(ObjectCreationExpressionSyntax objectCreationExpressionNode)
        {
            var initializer = objectCreationExpressionNode.Initializer;
            return initializer != null && initializer.Expressions.IsEmpty();
        }

        // Only do this when it is an accessor in property
        // Since it is illegal to have something like
        // int this[int i] { get; set;}
        // event EventHandler Bar {add; remove;}
        public static bool ShouldRemoveBraceForAccessorDeclaration(AccessorDeclarationSyntax accessorDeclarationNode, int caretPosition)
            => accessorDeclarationNode.Body != null
               && accessorDeclarationNode.ExpressionBody == null
               && accessorDeclarationNode.Parent != null
               && accessorDeclarationNode.Parent.IsParentKind(SyntaxKind.PropertyDeclaration)
               && accessorDeclarationNode.Body.Span.Contains(caretPosition);

        // If a property just has an empty accessorList, like
        // int i $${ }
        // then remove the braces and change it to a field
        // int i;
        public static bool ShouldRemoveBraceForPropertyDeclaration(PropertyDeclarationSyntax propertyDeclarationNode, int caretPosition)
        {
            if (propertyDeclarationNode.AccessorList != null
                && propertyDeclarationNode.ExpressionBody == null)
            {
                var accessorList = propertyDeclarationNode.AccessorList;
                return accessorList.Span.Contains(caretPosition) && accessorList.Accessors.IsEmpty();
            }

            return false;
        }

        // If an event declaration just has an empty accessorList,
        // like
        // event EventHandler e$$  { }
        // then change it to a event field declaration
        // event EventHandler e;
        public static bool ShouldRemoveBraceForEventDeclaration(EventDeclarationSyntax eventDeclarationNode, int caretPosition)
        {
            var accessorList = eventDeclarationNode.AccessorList;
            return accessorList != null
                   && accessorList.Span.Contains(caretPosition)
                   && accessorList.Accessors.IsEmpty();
        }

        #endregion
    }
}
