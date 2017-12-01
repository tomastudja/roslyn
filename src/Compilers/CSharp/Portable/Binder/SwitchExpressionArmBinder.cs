﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal class SwitchExpressionArmBinder : Binder
    {
        private readonly SwitchExpressionArmSyntax _arm;
        private readonly ExpressionVariableBinder _armScopeBinder;
        private readonly SwitchExpressionBinder _switchExpressionBinder;

        public SwitchExpressionArmBinder(SwitchExpressionArmSyntax arm, ExpressionVariableBinder armScopeBinder, SwitchExpressionBinder switchExpressionBinder) : base(armScopeBinder)
        {
            this._arm = arm;
            this._armScopeBinder = armScopeBinder;
            this._switchExpressionBinder = switchExpressionBinder;
        }

        internal override BoundSwitchExpressionCase BindSwitchExpressionArm(SwitchExpressionArmSyntax node, DiagnosticBag diagnostics)
        {
            Debug.Assert(node == _arm);
            var caseBinder = this.GetBinder(node);
            var hasErrors = _switchExpressionBinder.SwitchGoverningExpression.HasErrors;
            var locals = _armScopeBinder.Locals;
            var pattern = caseBinder.BindPattern(node.Pattern, _switchExpressionBinder.SwitchGoverningType, hasErrors, diagnostics);
            var guard = node.WhenClause != null
                ? caseBinder.BindBooleanExpression((ExpressionSyntax)node.WhenClause.Condition, diagnostics)
                : null;
            var result = caseBinder.BindValue((ExpressionSyntax)node.Expression, diagnostics, BindValueKind.RValue);
            return new BoundSwitchExpressionCase(node, locals, pattern, guard, result, hasErrors);
        }
    }
}
