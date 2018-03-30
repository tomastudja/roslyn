﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal partial class LocalRewriter
    {
        public override BoundNode VisitPatternSwitchStatement(BoundPatternSwitchStatement node)
        {
            return PatternSwitchLocalRewriter.Rewrite(this, node);
        }

        private class PatternSwitchLocalRewriter : BasePatternSwitchLocalRewriter
        {
            public static BoundStatement Rewrite(LocalRewriter localRewriter, BoundPatternSwitchStatement node)
            {
                var rewriter = new PatternSwitchLocalRewriter(node, localRewriter);
                BoundStatement result = rewriter.LowerPatternSwitchStatement(node);
                rewriter.Free();
                return result;
            }

            /// <summary>
            /// Map from switch section's syntax to the lowered code for the section. The code for a section
            /// includes the code to assign to the pattern variables and evaluate the when clause. Since a
            /// when clause can yield a false value, it can jump back to a label in the lowered decision dag.
            /// </summary>
            private Dictionary<SyntaxNode, ArrayBuilder<BoundStatement>> _switchSections => base._switchArms;

            /// <summary>
            /// A map from section syntax to the first label in that section.
            /// </summary>
            private Dictionary<SyntaxNode, LabelSymbol> _sectionLabels = PooledDictionary<SyntaxNode, LabelSymbol>.GetInstance();

            /// <summary>
            /// We revise the returned label for a leaf so that all leaves in the same switch section are given the same label.
            /// This enables the switch emitter to produce better code.
            /// </summary>
            protected override LabelSymbol GetDagNodeLabel(BoundDecisionDagNode dag)
            {
                var result = base.GetDagNodeLabel(dag);
                if (dag is BoundLeafDecisionDagNode d)
                {
                    SyntaxNode section = d.Syntax.Parent;

                    // It is possible that the leaf represents a compiler-generated default for a switch statement in the EE.
                    // In that case d.Syntax is the whole switch statement, and its parent is null. We are only interested
                    // in leaves that result from explicit switch case labels in a switch section.
                    if (section?.Kind() == SyntaxKind.SwitchSection)
                    {
                        if (_sectionLabels.TryGetValue(section, out LabelSymbol replacementLabel))
                        {
                            return replacementLabel;
                        }

                        _sectionLabels.Add(section, result);
                    }
                }

                return result;
            }

            private PatternSwitchLocalRewriter(BoundPatternSwitchStatement node, LocalRewriter localRewriter)
                : base(localRewriter, localRewriter.VisitExpression(node.Expression), node.SwitchSections.SelectAsArray(section => section.Syntax), node.DecisionDag)
            {
            }

            private BoundStatement LowerPatternSwitchStatement(BoundPatternSwitchStatement node)
            {
                var reachableLabels = node.DecisionDag.ReachableLabels;

                _factory.Syntax = node.Syntax;
                var result = ArrayBuilder<BoundStatement>.GetInstance();

                // The set of variables attached to the outer block
                var outerVariables = ArrayBuilder<LocalSymbol>.GetInstance();
                outerVariables.AddRange(node.InnerLocals);

                // EnC: We need to insert a hidden sequence point to handle function remapping in case
                // the containing method is edited while methods invoked in the expression are being executed.
                var expression = _loweredInput;
                if (!node.WasCompilerGenerated && _localRewriter.Instrument)
                {
                    var instrumentedExpression = _localRewriter._instrumenter.InstrumentSwitchStatementExpression(node, expression, _factory);
                    if (expression.ConstantValue == null)
                    {
                        expression = instrumentedExpression;
                    }
                    else
                    {
                        // If the expression is a constant, we leave it alone (the decision dag lowering code needs
                        // to see that constant). But we add an additional leading statement with the instrumented expression.
                        result.Add(_factory.ExpressionStatement(instrumentedExpression));
                    }
                }

                // Assign the input to a temp
                BoundExpression inputTemp = _tempAllocator.GetTemp(base._inputTemp);
                if (inputTemp == expression)
                {
                    // In this case we would just be assigning the variable to itself, so we need generate no code.
                    // This arises due to an optimization by which we use pattern variables as pattern-matching temps.
                }
                else
                {
                    result.Add(_factory.Assignment(inputTemp, expression));
                }

                // then add the rest of the lowered dag that references that input
                result.Add(_factory.Block(this._loweredDecisionDag.ToImmutable()));

                // A branch to the default label when no switch case matches is included in the
                // decision dag, so the code in `result` is unreachable at this point.

                // Lower each switch section.
                foreach (BoundPatternSwitchSection section in node.SwitchSections)
                {
                    _factory.Syntax = section.Syntax;
                    ArrayBuilder<BoundStatement> sectionBuilder = _switchSections[section.Syntax];
                    foreach (BoundPatternSwitchLabel switchLabel in section.SwitchLabels)
                    {
                        sectionBuilder.Add(_factory.Label(switchLabel.Label));
                    }

                    // Lifetime of these locals is expanded to the entire switch body, as it is possible to capture
                    // them in a different section by using a local function as an intermediary.
                    outerVariables.AddRange(section.Locals);

                    // Add the translated body of the switch section
                    sectionBuilder.AddRange(_localRewriter.VisitList(section.Statements));

                    // By the semantics of the switch statement, the end of each section is required to be unreachable.
                    // So we can just seal the block and there is no need to follow it by anything.
                    ImmutableArray<BoundStatement> statements = sectionBuilder.ToImmutableAndFree();

                    if (section.Locals.IsEmpty)
                    {
                        result.Add(_factory.StatementList(statements));
                    }
                    else
                    {
                        // Note the scope of the locals, even though they are included for the purposes of
                        // closure analysis in the enclosing scope.
                        result.Add(new BoundScope(section.Syntax, section.Locals, statements));
                    }
                }

                // Dispatch temps are in scope throughout the switch statement, as they are used
                // both in the dispatch section to hold temporary values from the translation of
                // the decision dag, and in the branches where the temp values are assigned to the
                // pattern variables of matched patterns.
                outerVariables.AddRange(_tempAllocator.AllTemps());

                _factory.Syntax = node.Syntax;
                result.Add(_factory.Label(node.BreakLabel));
                BoundStatement translatedSwitch = _factory.Block(outerVariables.ToImmutableAndFree(), node.InnerLocalFunctions, result.ToImmutableAndFree());

                // Only add instrumentation (such as a sequence point) if the node is not compiler-generated.
                if (!node.WasCompilerGenerated && _localRewriter.Instrument)
                {
                    translatedSwitch = _localRewriter._instrumenter.InstrumentPatternSwitchStatement(node, translatedSwitch);
                }

                return translatedSwitch;
            }
        }
    }
}
