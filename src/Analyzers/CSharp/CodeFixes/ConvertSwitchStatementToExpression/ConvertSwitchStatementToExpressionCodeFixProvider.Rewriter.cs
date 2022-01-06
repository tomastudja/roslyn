﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.ConvertSwitchStatementToExpression
{
    using static ConvertSwitchStatementToExpressionHelpers;
    using static SyntaxFactory;

    internal sealed partial class ConvertSwitchStatementToExpressionCodeFixProvider
    {
        private sealed class Rewriter : CSharpSyntaxVisitor<ExpressionSyntax>
        {
            private readonly SemanticModel _semanticModel;
            private readonly bool _isAllThrowStatements;
            private readonly CancellationToken _cancellationToken;

            private ExpressionSyntax? _assignmentTargetOpt;

            private Rewriter(SemanticModel semanticModel, bool isAllThrowStatements, CancellationToken cancellationToken)
            {
                _semanticModel = semanticModel;
                _isAllThrowStatements = isAllThrowStatements;
                _cancellationToken = cancellationToken;
            }

            public static StatementSyntax Rewrite(
                SwitchStatementSyntax switchStatement,
                SemanticModel model,
                ITypeSymbol declaratorToRemoveTypeOpt,
                SyntaxKind nodeToGenerate,
                bool shouldMoveNextStatementToSwitchExpression,
                bool generateDeclaration,
                CancellationToken cancellationToken)
            {
                if (switchStatement.ContainsDirectives)
                {
                    // Do not rewrite statements with preprocessor directives
                    return switchStatement;
                }

                var rewriter = new Rewriter(model, isAllThrowStatements: nodeToGenerate == SyntaxKind.ThrowStatement, cancellationToken);

                // Rewrite the switch statement as a switch expression.
                var switchExpression = rewriter.RewriteSwitchStatement(
                    switchStatement, topLevel: true,
                    allowMoveNextStatementToSwitchExpression: shouldMoveNextStatementToSwitchExpression);

                // Generate the final statement to wrap the switch expression, e.g. a "return" or an assignment.
                return rewriter.GetFinalStatement(switchExpression,
                    switchStatement.SwitchKeyword.LeadingTrivia, declaratorToRemoveTypeOpt, nodeToGenerate, generateDeclaration);
            }

            private StatementSyntax GetFinalStatement(
                ExpressionSyntax switchExpression,
                SyntaxTriviaList leadingTrivia,
                ITypeSymbol declaratorToRemoveTypeOpt,
                SyntaxKind nodeToGenerate,
                bool generateDeclaration)
            {
                switch (nodeToGenerate)
                {
                    case SyntaxKind.ReturnStatement:
                        return ReturnStatement(
                            Token(leadingTrivia, SyntaxKind.ReturnKeyword, trailing: default),
                            switchExpression,
                            Token(SyntaxKind.SemicolonToken));
                    case SyntaxKind.ThrowStatement:
                        return ThrowStatement(
                            Token(leadingTrivia, SyntaxKind.ThrowKeyword, trailing: default),
                            switchExpression,
                            Token(SyntaxKind.SemicolonToken));
                }

                Debug.Assert(SyntaxFacts.IsAssignmentExpression(nodeToGenerate));
                Debug.Assert(_assignmentTargetOpt != null);

                return generateDeclaration
                    ? GenerateVariableDeclaration(switchExpression, declaratorToRemoveTypeOpt)
                    : GenerateAssignment(switchExpression, nodeToGenerate, leadingTrivia);
            }

            private ExpressionStatementSyntax GenerateAssignment(ExpressionSyntax switchExpression, SyntaxKind assignmentKind, SyntaxTriviaList leadingTrivia)
            {
                Contract.ThrowIfNull(_assignmentTargetOpt);

                return ExpressionStatement(
                    AssignmentExpression(assignmentKind,
                        left: _assignmentTargetOpt,
                        right: switchExpression))
                    .WithLeadingTrivia(leadingTrivia);
            }

            private StatementSyntax GenerateVariableDeclaration(ExpressionSyntax switchExpression, ITypeSymbol declaratorToRemoveTypeOpt)
            {
                Contract.ThrowIfFalse(_assignmentTargetOpt is IdentifierNameSyntax);

                // There is a probability that we cannot use var if the declaration type is a reference type or nullable type.
                // In these cases, we generate the explicit type for now and decide later whether or not to use var.
                var cannotUseVar = declaratorToRemoveTypeOpt != null && (declaratorToRemoveTypeOpt.IsReferenceType || declaratorToRemoveTypeOpt.IsNullable());
                var type = cannotUseVar ? declaratorToRemoveTypeOpt!.GenerateTypeSyntax() : IdentifierName("var");

                return LocalDeclarationStatement(
                    VariableDeclaration(
                        type,
                        variables: SingletonSeparatedList(
                                    VariableDeclarator(
                                        identifier: ((IdentifierNameSyntax)_assignmentTargetOpt).Identifier,
                                        argumentList: null,
                                        initializer: EqualsValueClause(switchExpression)))));
            }

            private SwitchExpressionArmSyntax GetSwitchExpressionArm(SwitchSectionSyntax node)
            {
                return SwitchExpressionArm(
                    pattern: GetPattern(node.Labels, out var whenClauseOpt),
                    whenClause: whenClauseOpt,
                    expression: RewriteStatements(node.Statements));
            }

            private static PatternSyntax GetPattern(SyntaxList<SwitchLabelSyntax> switchLabels, out WhenClauseSyntax? whenClauseOpt)
            {
                if (switchLabels.Count == 1)
                    return GetPattern(switchLabels[0], out whenClauseOpt);

                if (switchLabels.Any(label => IsDefaultSwitchLabel(label)))
                {
                    // original group had a catch-all label.  just convert to a discard _ to indicate the same.
                    whenClauseOpt = null;
                    return DiscardPattern();
                }

                // Multiple labels, and no catch-all merge them using an 'or' pattern.
                var totalPattern = GetPattern(switchLabels[0], out var whenClauseUnused);
                Debug.Assert(whenClauseUnused == null, "We should not have offered to convert multiple cases if any have a when clause");

                for (var i = 1; i < switchLabels.Count; i++)
                {
                    var nextPatternPart = GetPattern(switchLabels[i], out whenClauseUnused);
                    Debug.Assert(whenClauseUnused == null, "We should not have offered to convert multiple cases if any have a when clause");

                    totalPattern = BinaryPattern(SyntaxKind.OrPattern, totalPattern.Parenthesize(), nextPatternPart.Parenthesize());
                }

                whenClauseOpt = null;
                return totalPattern;
            }

            private static PatternSyntax GetPattern(SwitchLabelSyntax switchLabel, out WhenClauseSyntax? whenClauseOpt)
            {
                switch (switchLabel.Kind())
                {
                    case SyntaxKind.CasePatternSwitchLabel:
                        var node = (CasePatternSwitchLabelSyntax)switchLabel;
                        whenClauseOpt = node.WhenClause;
                        return node.Pattern;

                    case SyntaxKind.CaseSwitchLabel:
                        whenClauseOpt = null;
                        return ConstantPattern(((CaseSwitchLabelSyntax)switchLabel).Value);

                    case SyntaxKind.DefaultSwitchLabel:
                        whenClauseOpt = null;
                        return DiscardPattern();

                    case var value:
                        throw ExceptionUtilities.UnexpectedValue(value);
                }
            }

            public override ExpressionSyntax VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                _assignmentTargetOpt ??= node.Left;
                return CastIfChangeInRuntimeRepresentation(node.Right);
            }

            private ExpressionSyntax CastIfChangeInRuntimeRepresentation(ExpressionSyntax node)
            {
                var typeInfo = _semanticModel.GetTypeInfo(node, _cancellationToken);
                if (typeInfo.ConvertedType is not null &&
                    typeInfo.Type is not null &&
                    !Equals(typeInfo.ConvertedType, typeInfo.Type))
                {
                    var conversion = _semanticModel.Compilation.ClassifyConversion(typeInfo.Type, typeInfo.ConvertedType);
                    if (!conversion.IsIdentityOrImplicitReference())
                        return node.Cast(typeInfo.ConvertedType);
                }

                return node;
            }

            private ExpressionSyntax RewriteStatements(SyntaxList<StatementSyntax> statements)
            {
                Debug.Assert(statements.Count is 1 or 2);
                Debug.Assert(!statements[0].IsKind(SyntaxKind.BreakStatement));
                var result = Visit(statements[0]);
                Contract.ThrowIfNull(result);
                return result;
            }

            public override ExpressionSyntax VisitSwitchStatement(SwitchStatementSyntax node)
                => RewriteSwitchStatement(node, topLevel: false);

            private ExpressionSyntax RewriteSwitchStatement(
                SwitchStatementSyntax node,
                bool topLevel,
                bool allowMoveNextStatementToSwitchExpression = true)
            {
                var switchArms = node.Sections
                    // The default label must come last in the switch expression.
                    .OrderBy(section => section.Labels.Any(label => IsDefaultSwitchLabel(label)))
                    .Select(s =>
                        (tokensForLeadingTrivia: new[] { s.Labels[0].GetFirstToken(), s.Labels[0].GetLastToken() },
                         tokensForTrailingTrivia: new[] { s.Statements[0].GetFirstToken(), s.Statements[0].GetLastToken() },
                         armExpression: GetSwitchExpressionArm(s)))
                    .ToList();

                if (allowMoveNextStatementToSwitchExpression)
                {
                    var nextStatement = node.GetNextStatement();
                    if (nextStatement.IsKind(SyntaxKind.ThrowStatement, SyntaxKind.ReturnStatement))
                    {
                        var armExpression = Visit(nextStatement);
                        Contract.ThrowIfNull(armExpression);

                        switchArms.Add(
                            (tokensForLeadingTrivia: new[] { nextStatement.GetFirstToken() },
                             tokensForTrailingTrivia: new[] { nextStatement.GetLastToken() },
                             SwitchExpressionArm(DiscardPattern(), armExpression)));
                    }
                }
                // add explicit cast if necessary 
                var switchStatement = topLevel ? AddCastIfNecessary(node) : node;

                return SwitchExpression(
                    switchStatement.Expression.Parenthesize(),
                    Token(leading: default, SyntaxKind.SwitchKeyword, node.CloseParenToken.TrailingTrivia),
                    Token(leading: default, SyntaxKind.OpenBraceToken, node.OpenBraceToken.TrailingTrivia),
                    SeparatedList(
                        switchArms.Select(t => t.armExpression.WithLeadingTrivia(t.tokensForLeadingTrivia.GetTrivia().FilterComments(addElasticMarker: false))),
                        switchArms.Select(t => Token(SyntaxKind.CommaToken).WithTrailingTrivia(t.tokensForTrailingTrivia.GetTrivia().FilterComments(addElasticMarker: true)))),
                    Token(SyntaxKind.CloseBraceToken));
            }

            private SwitchStatementSyntax AddCastIfNecessary(SwitchStatementSyntax node)
            {
                // If the swith statement expression is being implicitly converted then we need to explicitly cast the expression
                // before rewriting as a switch expression
                var expressionType = _semanticModel.GetSymbolInfo(node.Expression).Symbol.GetSymbolType();
                var expressionConvertedType = _semanticModel.GetTypeInfo(node.Expression).ConvertedType;

                if (expressionConvertedType != null &&
                    !SymbolEqualityComparer.Default.Equals(expressionConvertedType, expressionType))
                {
                    return node.Update(node.SwitchKeyword, node.OpenParenToken,
                        node.Expression.Cast(expressionConvertedType).WithAdditionalAnnotations(Formatter.Annotation),
                        node.CloseParenToken, node.OpenBraceToken,
                        node.Sections, node.CloseBraceToken);
                }

                return node;
            }

            public override ExpressionSyntax VisitReturnStatement(ReturnStatementSyntax node)
            {
                Contract.ThrowIfNull(node.Expression);
                return CastIfChangeInRuntimeRepresentation(node.Expression);
            }

            public override ExpressionSyntax VisitThrowStatement(ThrowStatementSyntax node)
            {
                Contract.ThrowIfNull(node.Expression);
                // If this is an all-throw switch statement, we return the expression rather than
                // creating a throw expression so we can wrap the switch expression inside a throw expression.
                return _isAllThrowStatements ? node.Expression : ThrowExpression(node.Expression);
            }

            public override ExpressionSyntax VisitExpressionStatement(ExpressionStatementSyntax node)
            {
                var result = Visit(node.Expression);
                Contract.ThrowIfNull(result);
                return result;
            }

            public override ExpressionSyntax DefaultVisit(SyntaxNode node)
                => throw ExceptionUtilities.UnexpectedValue(node.Kind());
        }
    }
}
