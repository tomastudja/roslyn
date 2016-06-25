﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal abstract partial class PreciseAbstractFlowPass<LocalState>
    {
        #region implementation for the old-style (no-patterns) variation of the switch statement.

        public override BoundNode VisitSwitchStatement(BoundSwitchStatement node)
        {
            // visit switch header
            LocalState breakState = VisitSwitchHeader(node);
            SetUnreachable();

            // visit switch block
            VisitSwitchBlock(node);
            IntersectWith(ref breakState, ref this.State);
            ResolveBreaks(breakState, node.BreakLabel);
            return null;
        }

        private LocalState VisitSwitchHeader(BoundSwitchStatement node)
        {
            // Initial value for the Break state for a switch statement is established as follows:
            //  Break state = UnreachableState if either of the following is true:
            //  (1) there is a default label, or
            //  (2) the switch expression is constant and there is a matching case label.
            //  Otherwise, the Break state = current state.

            // visit switch expression
            VisitRvalue(node.Expression);
            LocalState breakState = this.State;

            // For a switch statement, we simulate a possible jump to the switch labels to ensure that
            // the label is not treated as an unused label and a pending branch to the label is noted.

            // However, if switch expression is a constant, we must have determined the single target label
            // at bind time, i.e. node.ConstantTargetOpt, and we must simulate a jump only to this label.

            var constantTargetOpt = node.ConstantTargetOpt;
            if ((object)constantTargetOpt == null)
            {
                bool hasDefaultLabel = false;
                foreach (var section in node.SwitchSections)
                {
                    foreach (var boundSwitchLabel in section.SwitchLabels)
                    {
                        var label = boundSwitchLabel.Label;
                        hasDefaultLabel = hasDefaultLabel || label.IdentifierNodeOrToken.Kind() == SyntaxKind.DefaultSwitchLabel;
                        SetState(breakState.Clone());
                        var simulatedGoto = new BoundGotoStatement(node.Syntax, label);
                        VisitGotoStatement(simulatedGoto);
                    }
                }

                if (hasDefaultLabel)
                {
                    // Condition (1) for an unreachable break state is satisfied
                    breakState = UnreachableState();
                }
            }
            else if (!node.BreakLabel.Equals(constantTargetOpt))
            {
                SetState(breakState.Clone());
                var simulatedGoto = new BoundGotoStatement(node.Syntax, constantTargetOpt);
                VisitGotoStatement(simulatedGoto);

                // Condition (1) or (2) for an unreachable break state is satisfied
                breakState = UnreachableState();
            }

            return breakState;
        }

        private void VisitSwitchBlock(BoundSwitchStatement node)
        {
            var afterSwitchState = UnreachableState();
            var switchSections = node.SwitchSections;
            var iLastSection = (switchSections.Length - 1);
            // visit switch sections
            for (var iSection = 0; iSection <= iLastSection; iSection++)
            {
                VisitSwitchSection(switchSections[iSection], iSection == iLastSection);
                // Even though it is illegal for the end of a switch section to be reachable, in erroneous
                // code it may be reachable.  We treat that as an implicit break (branch to afterSwitchState).
                IntersectWith(ref afterSwitchState, ref this.State);
            }

            SetState(afterSwitchState);
        }

        public virtual BoundNode VisitSwitchSection(BoundSwitchSection node, bool lastSection)
        {
            return VisitSwitchSection(node);
        }

        public override BoundNode VisitSwitchSection(BoundSwitchSection node)
        {
            // visit switch section labels
            foreach (var boundSwitchLabel in node.SwitchLabels)
            {
                VisitRvalue(boundSwitchLabel.ExpressionOpt);
                VisitSwitchSectionLabel(boundSwitchLabel.Label, node);
            }

            // visit switch section body
            VisitStatementList(node);

            return null;
        }

        private void VisitSwitchSectionLabel(LabelSymbol label, BoundSwitchSection node)
        {
            VisitLabel(label, node);
        }

        #endregion implementation for the old-style (no-patterns) variation of the switch statement.

        #region implementation for the pattern-matching variation of the switch statement.

        public override BoundNode VisitPatternSwitchStatement(BoundPatternSwitchStatement node)
        {
            // visit switch header
            LocalState breakState = VisitPatternSwitchHeader(node);

            // visit switch block
            VisitPatternSwitchBlock(node);
            IntersectWith(ref breakState, ref this.State);
            ResolveBreaks(breakState, node.BreakLabel);
            return null;
        }

        private void VisitPatternSwitchBlock(BoundPatternSwitchStatement node)
        {
            var afterSwitchState = UnreachableState();
            var switchSections = node.SwitchSections;
            var iLastSection = (switchSections.Length - 1);

            // simulate the dispatch (setting pattern variables and jumping to labels) using the decision tree
            VisitDecisionTree(node.DecisionTree);

            // we always consider the default label reachable for flow analysis purposes.
            if (node.DefaultLabel != null)
            {
                _pendingBranches.Add(new PendingBranch(node.DefaultLabel, this.State));
            }

            // visit switch sections
            for (var iSection = 0; iSection <= iLastSection; iSection++)
            {
                VisitPatternSwitchSection(switchSections[iSection], node.Expression, iSection == iLastSection);
                // Even though it is illegal for the end of a switch section to be reachable, in erroneous
                // code it may be reachable.  We treat that as an implicit break (branch to afterSwitchState).
                IntersectWith(ref afterSwitchState, ref this.State);
            }

            SetState(afterSwitchState);
        }

        // Visit all the branches in the decision tree
        private void VisitDecisionTree(DecisionTree decisionTree)
        {
            if (decisionTree == null) return;
            switch (decisionTree.Kind)
            {
                case DecisionTree.DecisionKind.ByType:
                    {
                        var byType = (DecisionTree.ByType)decisionTree;
                        VisitDecisionTree(byType.WhenNull);
                        foreach (var kvp in byType.TypeAndDecision)
                        {
                            VisitDecisionTree(kvp.Value);
                        }
                        VisitDecisionTree(byType.Default);
                        return;
                    }
                case DecisionTree.DecisionKind.ByValue:
                    {
                        var byValue = (DecisionTree.ByValue)decisionTree;
                        var expressionIsConstant = byValue.Expression.ConstantValue != null;
                        bool valueHandled = false;
                        foreach (var kvp in byValue.ValueAndDecision)
                        {
                            if (!expressionIsConstant || Equals(byValue.Expression.ConstantValue, kvp.Key))
                            {
                                VisitDecisionTree(kvp.Value);
                                valueHandled = true;
                            }
                        }
                        if (!expressionIsConstant || !valueHandled) VisitDecisionTree(byValue.Default);
                        return;
                    }
                case DecisionTree.DecisionKind.Guarded:
                    {
                        VisitGuardedDecisionTree((DecisionTree.Guarded)decisionTree);
                        return;
                    }
                default:
                    throw ExceptionUtilities.UnexpectedValue(decisionTree.Kind);
            }
        }

        private void VisitGuardedDecisionTree(DecisionTree.Guarded guarded)
        {
            var initialState = this.State;
            SetState(initialState.Clone());

            // assign pattern variables
            VisitGuardedPattern(guarded);

            if (guarded.Guard != null)
            {
                VisitCondition(guarded.Guard);
                SetState(StateWhenTrue);
                // discard StateWhenFalse
            }

            // goto the label for the switch block
            _pendingBranches.Add(new PendingBranch(guarded.Label, this.State));

            // put the state back where we found it for the next case
            SetState(initialState);

            // Handle the "default" case when the guard fails
            VisitDecisionTree(guarded.Default);
        }

        protected virtual void VisitGuardedPattern(DecisionTree.Guarded guarded)
        {
        }

        /// <summary>
        /// Visit the switch expression, and return the initial break state.
        /// </summary>
        private LocalState VisitPatternSwitchHeader(BoundPatternSwitchStatement node)
        {
            // decide if the switch handles every input.
            bool hasDefaultLabel = node.DecisionTree.MatchIsComplete == true;

            // visit switch expression
            VisitRvalue(node.Expression);

            // return the exit state to use if no pattern matches
            if (hasDefaultLabel)
            {
                return UnreachableState();
            }
            else
            {
                return this.State;
            }
        }

        protected virtual void VisitPatternSwitchSection(BoundPatternSwitchSection node, BoundExpression switchExpression, bool isLastSection)
        {
            SetState(UnreachableState());
            foreach (var label in node.SwitchLabels)
            {
                VisitLabel(label.Label, node);
            }

            VisitStatementList(node);
        }

        #endregion implementation for the pattern-matching variation of the switch statement.
    }
}
