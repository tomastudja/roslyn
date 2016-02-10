﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Semantics;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
<<<<<<< HEAD
    partial class BoundStatement : IOperation
=======
    internal partial class BoundStatement : IStatement
>>>>>>> upstream/master
    {
        OperationKind IOperation.Kind => this.StatementKind;

        bool IOperation.IsInvalid => this.HasErrors;

        SyntaxNode IOperation.Syntax => this.Syntax;

        protected abstract OperationKind StatementKind { get; }

        public abstract void Accept(OperationVisitor visitor);

        public abstract TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument);
    }

    internal partial class BoundBlock : IBlockStatement
    {
        private static readonly ConditionalWeakTable<BoundBlock, object> s_blockStatementsMappings =
            new ConditionalWeakTable<BoundBlock, object>();

        ImmutableArray<IOperation> IBlockStatement.Statements
        {
            get
            {
                // This is to filter out operations of kind None.
<<<<<<< HEAD
                return (ImmutableArray<IOperation>) s_blockStatementsMappings.GetValue(this,
                    blockStatement => { return blockStatement.Statements.AsImmutable<IOperation>().WhereAsArray(statement => statement.Kind != OperationKind.None); }
=======
                return (ImmutableArray<IStatement>)s_blockStatementsMappings.GetValue(this,
                    blockStatement => { return blockStatement.Statements.AsImmutable<IStatement>().WhereAsArray(statement => statement.Kind != OperationKind.None); }
>>>>>>> upstream/master
                    );
            }
        }

        ImmutableArray<ILocalSymbol> IBlockStatement.Locals => this.Locals.As<ILocalSymbol>();

        protected override OperationKind StatementKind => OperationKind.BlockStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitBlockStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitBlockStatement(this, argument);
        }
    }

    internal partial class BoundContinueStatement : IBranchStatement
    {
        ILabelSymbol IBranchStatement.Target => this.Label;

        protected override OperationKind StatementKind => OperationKind.ContinueStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitBranchStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitBranchStatement(this, argument);
        }
    }

    internal partial class BoundBreakStatement : IBranchStatement
    {
        ILabelSymbol IBranchStatement.Target => this.Label;

        protected override OperationKind StatementKind => OperationKind.BreakStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitBranchStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitBranchStatement(this, argument);
        }
    }

<<<<<<< HEAD
    partial class BoundYieldBreakStatement : IReturnStatement
=======
    internal partial class BoundYieldBreakStatement
>>>>>>> upstream/master
    {
        IExpression IReturnStatement.ReturnedValue => null;

        protected override OperationKind StatementKind => OperationKind.YieldBreakStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitYieldBreakStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitYieldBreakStatement(this, argument);
        }
    }

    internal partial class BoundGotoStatement : IBranchStatement
    {
        ILabelSymbol IBranchStatement.Target => this.Label;

        protected override OperationKind StatementKind => OperationKind.GoToStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitBranchStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitBranchStatement(this, argument);
        }
    }

<<<<<<< HEAD
    partial class BoundNoOpStatement : IEmptyStatement
=======
    internal partial class BoundNoOpStatement
>>>>>>> upstream/master
    {
        protected override OperationKind StatementKind => OperationKind.EmptyStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitEmptyStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitEmptyStatement(this, argument);
        }
    }

    internal partial class BoundIfStatement : IIfStatement
    {
        IExpression IIfStatement.Condition => this.Condition;

        IOperation IIfStatement.IfTrue => this.Consequence;

        IOperation IIfStatement.IfFalse => this.AlternativeOpt;

        protected override OperationKind StatementKind => OperationKind.IfStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitIfStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitIfStatement(this, argument);
        }
    }

    internal partial class BoundWhileStatement : IWhileUntilLoopStatement
    {
        bool IWhileUntilLoopStatement.IsTopTest => true;

        bool IWhileUntilLoopStatement.IsWhile => true;

        IExpression IForWhileUntilLoopStatement.Condition => this.Condition;

        LoopKind ILoopStatement.LoopKind => LoopKind.WhileUntil;

        IOperation ILoopStatement.Body => this.Body;

        protected override OperationKind StatementKind => OperationKind.LoopStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitWhileUntilLoopStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitWhileUntilLoopStatement(this, argument);
        }
    }

    internal partial class BoundDoStatement : IWhileUntilLoopStatement
    {
        bool IWhileUntilLoopStatement.IsTopTest => false;

        bool IWhileUntilLoopStatement.IsWhile => true;

        IExpression IForWhileUntilLoopStatement.Condition => this.Condition;

        LoopKind ILoopStatement.LoopKind => LoopKind.WhileUntil;

        IOperation ILoopStatement.Body => this.Body;

        protected override OperationKind StatementKind => OperationKind.LoopStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitWhileUntilLoopStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitWhileUntilLoopStatement(this, argument);
        }
    }

    internal partial class BoundForStatement : IForLoopStatement
    {
        ImmutableArray<IOperation> IForLoopStatement.Before => ToStatements(this.Initializer);

        ImmutableArray<IOperation> IForLoopStatement.AtLoopBottom => ToStatements(this.Increment);

        ImmutableArray<ILocalSymbol> IForLoopStatement.Locals => this.OuterLocals.As<ILocalSymbol>();

        IExpression IForWhileUntilLoopStatement.Condition => this.Condition;

        LoopKind ILoopStatement.LoopKind => LoopKind.For;

        IOperation ILoopStatement.Body => this.Body;

        protected override OperationKind StatementKind => OperationKind.LoopStatement;

<<<<<<< HEAD
        ImmutableArray<IOperation> ToStatements(BoundStatement statement)
=======
        private ImmutableArray<IStatement> ToStatements(BoundStatement statement)
>>>>>>> upstream/master
        {
            BoundStatementList statementList = statement as BoundStatementList;
            if (statementList != null)
            {
                return statementList.Statements.As<IOperation>();
            }
            else if (statement == null)
            {
                return ImmutableArray<IOperation>.Empty;
            }

            return ImmutableArray.Create<IOperation>(statement);
        }

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitForLoopStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitForLoopStatement(this, argument);
        }
    }

    internal partial class BoundForEachStatement : IForEachLoopStatement
    {
        ILocalSymbol IForEachLoopStatement.IterationVariable => this.IterationVariable;

        IExpression IForEachLoopStatement.Collection => this.Expression;

        LoopKind ILoopStatement.LoopKind => LoopKind.ForEach;

        IOperation ILoopStatement.Body => this.Body;

        protected override OperationKind StatementKind => OperationKind.LoopStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitForEachLoopStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitForEachLoopStatement(this, argument);
        }
    }

    internal partial class BoundSwitchStatement : ISwitchStatement
    {
        private static readonly ConditionalWeakTable<BoundSwitchStatement, object> s_switchSectionsMappings =
            new ConditionalWeakTable<BoundSwitchStatement, object>();

        IExpression ISwitchStatement.Value => this.BoundExpression;

        ImmutableArray<ICase> ISwitchStatement.Cases
        {
            get
            {
                return (ImmutableArray<ICase>)s_switchSectionsMappings.GetValue(this,
                    switchStatement =>
                    {
                        return switchStatement.SwitchSections.SelectAsArray(switchSection => (ICase)new SwitchSection(switchSection));
                    });
            }
        }

        protected override OperationKind StatementKind => OperationKind.SwitchStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitSwitchStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitSwitchStatement(this, argument);
        }

        private sealed class SwitchSection : ICase
        {
            public SwitchSection(BoundSwitchSection boundNode)
            {
                this.Body = boundNode.Statements.As<IOperation>();
                this.Clauses = boundNode.BoundSwitchLabels.As<ICaseClause>();
                this.IsInvalid = boundNode.HasErrors;
                this.Syntax = boundNode.Syntax;
            }

            public ImmutableArray<IOperation> Body { get; }

            public ImmutableArray<ICaseClause> Clauses { get; }

            public bool IsInvalid { get; }

            OperationKind IOperation.Kind => OperationKind.SwitchSection;

            public SyntaxNode Syntax { get; }

            void IOperation.Accept(OperationVisitor visitor)
            {
                visitor.VisitCase(this);
            }

            TResult IOperation.Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
            {
                return visitor.VisitCase(this, argument);
            }
        }
    }

    internal partial class BoundSwitchLabel : ISingleValueCaseClause
    {
        IExpression ISingleValueCaseClause.Value => this.ExpressionOpt;

        BinaryOperationKind ISingleValueCaseClause.Equality
        {
            get
            {
                BoundExpression caseValue = this.ExpressionOpt;
                if (caseValue != null)
                {
                    switch (caseValue.Type.SpecialType)
                    {
                        case SpecialType.System_Int32:
                        case SpecialType.System_Int64:
                        case SpecialType.System_UInt32:
                        case SpecialType.System_UInt64:
                        case SpecialType.System_UInt16:
                        case SpecialType.System_Int16:
                        case SpecialType.System_SByte:
                        case SpecialType.System_Byte:
                        case SpecialType.System_Char:
                            return BinaryOperationKind.IntegerEquals;

                        case SpecialType.System_Boolean:
                            return BinaryOperationKind.BooleanEquals;

                        case SpecialType.System_String:
                            return BinaryOperationKind.StringEquals;
                    }

                    if (caseValue.Type.TypeKind == TypeKind.Enum)
                    {
                        return BinaryOperationKind.EnumEquals;
                    }
                }

                return BinaryOperationKind.None;
            }
        }

        CaseKind ICaseClause.CaseKind => this.ExpressionOpt != null ? CaseKind.SingleValue : CaseKind.Default;

        OperationKind IOperation.Kind => OperationKind.SingleValueCaseClause;

        bool IOperation.IsInvalid => this.HasErrors;

        SyntaxNode IOperation.Syntax => this.Syntax;

        void IOperation.Accept(OperationVisitor visitor)
        {
            visitor.VisitSingleValueCaseClause(this);
        }

        TResult IOperation.Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitSingleValueCaseClause(this, argument);
        }
    }

    internal partial class BoundTryStatement : ITryStatement
    {
        IBlockStatement ITryStatement.Body => this.TryBlock;

        ImmutableArray<ICatch> ITryStatement.Catches => this.CatchBlocks.As<ICatch>();

        IBlockStatement ITryStatement.FinallyHandler => this.FinallyBlockOpt;

        protected override OperationKind StatementKind => OperationKind.TryStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitTryStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitTryStatement(this, argument);
        }
    }

    internal partial class BoundCatchBlock : ICatch
    {
        IBlockStatement ICatch.Handler => this.Body;

        ITypeSymbol ICatch.CaughtType => this.ExceptionTypeOpt;

        IExpression ICatch.Filter => this.ExceptionFilterOpt;

        ILocalSymbol ICatch.ExceptionLocal => this.LocalOpt;

        OperationKind IOperation.Kind => OperationKind.CatchHandler;

        bool IOperation.IsInvalid => this.Body.HasErrors || (this.ExceptionFilterOpt != null && this.ExceptionFilterOpt.HasErrors);

        SyntaxNode IOperation.Syntax => this.Syntax;

        void IOperation.Accept(OperationVisitor visitor)
        {
            visitor.VisitCatch(this);
        }

        TResult IOperation.Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitCatch(this, argument);
        }
    }

    internal partial class BoundFixedStatement : IFixedStatement
    {
        IVariableDeclarationStatement IFixedStatement.Variables => this.Declarations;

        IOperation IFixedStatement.Body => this.Body;

        protected override OperationKind StatementKind => OperationKind.FixedStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitFixedStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitFixedStatement(this, argument);
        }
    }

    internal partial class BoundUsingStatement : IUsingWithDeclarationStatement, IUsingWithExpressionStatement
    {
        IVariableDeclarationStatement IUsingWithDeclarationStatement.Declaration => this.DeclarationsOpt;

        IExpression IUsingWithExpressionStatement.Value => this.ExpressionOpt;

        IOperation IUsingStatement.Body => this.Body;

        protected override OperationKind StatementKind => this.ExpressionOpt != null ? OperationKind.UsingWithExpressionStatement : OperationKind.UsingWithDeclarationStatement;

        public override void Accept(OperationVisitor visitor)
        {
            if (this.StatementKind == OperationKind.UsingWithExpressionStatement)
            {
                visitor.VisitUsingWithExpressionStatement(this);
            }
            else
            {
                visitor.VisitUsingWithDeclarationStatement(this);
            }
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return this.StatementKind == OperationKind.UsingWithExpressionStatement
                    ? visitor.VisitUsingWithExpressionStatement(this, argument)
                    : visitor.VisitUsingWithDeclarationStatement(this, argument);
        }
    }

    internal partial class BoundThrowStatement : IThrowStatement
    {
        IExpression IThrowStatement.ThrownObject => this.ExpressionOpt;

        protected override OperationKind StatementKind => OperationKind.ThrowStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitThrowStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitThrowStatement(this, argument);
        }
    }

    internal partial class BoundReturnStatement : IReturnStatement
    {
        IExpression IReturnStatement.ReturnedValue => this.ExpressionOpt;

        protected override OperationKind StatementKind => OperationKind.ReturnStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitReturnStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitReturnStatement(this, argument);
        }
    }

    internal partial class BoundYieldReturnStatement : IReturnStatement
    {
        IExpression IReturnStatement.ReturnedValue => this.Expression;

        protected override OperationKind StatementKind => OperationKind.YieldReturnStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitReturnStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitReturnStatement(this, argument);
        }
    }

    internal partial class BoundLockStatement : ILockStatement
    {
        IExpression ILockStatement.LockedObject => this.Argument;

        IOperation ILockStatement.Body => this.Body;

        protected override OperationKind StatementKind => OperationKind.LockStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitLockStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitLockStatement(this, argument);
        }
    }

<<<<<<< HEAD
    partial class BoundBadStatement : IInvalidStatement
=======
    internal partial class BoundBadStatement
>>>>>>> upstream/master
    {
        protected override OperationKind StatementKind => OperationKind.InvalidStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitInvalidStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitInvalidStatement(this, argument);
        }
    }

    internal partial class BoundLocalDeclaration : IVariableDeclarationStatement
    {
        private static readonly ConditionalWeakTable<BoundLocalDeclaration, object> s_variablesMappings =
            new ConditionalWeakTable<BoundLocalDeclaration, object>();

        ImmutableArray<IVariableDeclaration> IVariableDeclarationStatement.Variables
        {
            get
            {
<<<<<<< HEAD
                return (ImmutableArray<IVariableDeclaration>) s_variablesMappings.GetValue(this, 
                    declaration => ImmutableArray.Create<IVariableDeclaration>(new VariableDeclaration(declaration.LocalSymbol, declaration.InitializerOpt, declaration.Syntax)));
=======
                return (ImmutableArray<IVariable>)s_variablesMappings.GetValue(this,
                    declaration => ImmutableArray.Create<IVariable>(new VariableDeclaration(declaration.LocalSymbol, declaration.InitializerOpt, declaration.Syntax)));
>>>>>>> upstream/master
            }
        }

        protected override OperationKind StatementKind => OperationKind.VariableDeclarationStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitVariableDeclarationStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitVariableDeclarationStatement(this, argument);
        }
    }

    internal partial class BoundMultipleLocalDeclarations : IVariableDeclarationStatement
    {
        private static readonly ConditionalWeakTable<BoundMultipleLocalDeclarations, object> s_variablesMappings =
            new ConditionalWeakTable<BoundMultipleLocalDeclarations, object>();

        ImmutableArray<IVariableDeclaration> IVariableDeclarationStatement.Variables
        {
            get
            {
                return (ImmutableArray<IVariableDeclaration>)s_variablesMappings.GetValue(this,
                    multipleDeclarations =>
<<<<<<< HEAD
                        multipleDeclarations.LocalDeclarations.SelectAsArray(declaration => 
                            (IVariableDeclaration)new VariableDeclaration(declaration.LocalSymbol, declaration.InitializerOpt, declaration.Syntax)));
=======
                        multipleDeclarations.LocalDeclarations.SelectAsArray(declaration =>
                            (IVariable)new VariableDeclaration(declaration.LocalSymbol, declaration.InitializerOpt, declaration.Syntax)));
>>>>>>> upstream/master
            }
        }

        protected override OperationKind StatementKind => OperationKind.VariableDeclarationStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitVariableDeclarationStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitVariableDeclarationStatement(this, argument);
        }
    }

    internal partial class BoundLabelStatement : ILabelStatement
    {
        ILabelSymbol ILabelStatement.Label => this.Label;

        protected override OperationKind StatementKind => OperationKind.LabelStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitLabelStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitLabelStatement(this, argument);
        }
    }

    internal partial class BoundLabeledStatement : ILabeledStatement
    {
        IOperation ILabeledStatement.Labeled => this.Body;

        ILabelSymbol ILabelStatement.Label => this.Label;

        protected override OperationKind StatementKind => OperationKind.LabeledStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitLabeledStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitLabeledStatement(this, argument);
        }
    }

    internal partial class BoundExpressionStatement : IExpressionStatement
    {
        IExpression IExpressionStatement.Expression => this.Expression;

        protected override OperationKind StatementKind => OperationKind.ExpressionStatement;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitExpressionStatement(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitExpressionStatement(this, argument);
        }
    }

    internal partial class BoundSwitchSection
    {
        protected override OperationKind StatementKind => OperationKind.None;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitNoneOperation(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitNoneOperation(this, argument);
        }
    }

    internal partial class BoundStatementList
    {
        protected override OperationKind StatementKind => OperationKind.None;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitNoneOperation(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitNoneOperation(this, argument);
        }
    }

    internal partial class BoundConditionalGoto
    {
        protected override OperationKind StatementKind => OperationKind.None;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitNoneOperation(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitNoneOperation(this, argument);
        }
    }

    internal partial class BoundSequencePoint
    {
        protected override OperationKind StatementKind => OperationKind.None;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitNoneOperation(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitNoneOperation(this, argument);
        }
    }

    internal partial class BoundSequencePointWithSpan
    {
        protected override OperationKind StatementKind => OperationKind.None;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitNoneOperation(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitNoneOperation(this, argument);
        }
    }

    internal partial class BoundStateMachineScope
    {
        protected override OperationKind StatementKind => OperationKind.None;

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitNoneOperation(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitNoneOperation(this, argument);
        }
    }
}
