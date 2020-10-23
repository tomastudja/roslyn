﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Operations
{
    internal sealed partial class OperationCloner : OperationVisitor<object?, IOperation>
#nullable disable
    {
        public IOperation Visit(IOperation operation)
        {
            return Visit(operation, argument: null);
        }

        internal override IOperation VisitNoneOperation(IOperation operation, object argument)
        {
            return new NoneOperation(VisitArray(operation.Children.ToImmutableArray()), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.GetConstantValue(), operation.IsImplicit, operation.Type);
        }

        public override IOperation VisitVariableDeclarationGroup(IVariableDeclarationGroupOperation operation, object argument)
        {
            return new VariableDeclarationGroupOperation(VisitArray(operation.Declarations), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitVariableDeclarator(IVariableDeclaratorOperation operation, object argument)
        {
            return new VariableDeclaratorOperation(operation.Symbol, Visit(operation.Initializer), VisitArray(operation.IgnoredArguments), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitVariableDeclaration(IVariableDeclarationOperation operation, object argument)
        {
            return new VariableDeclarationOperation(VisitArray(operation.Declarators), Visit(operation.Initializer), VisitArray(operation.IgnoredDimensions), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitConversion(IConversionOperation operation, object argument)
        {
            return new ConversionOperation(Visit(operation.Operand), ((BaseConversionOperation)operation).ConversionConvertible, operation.IsTryCast, operation.IsChecked, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitSingleValueCaseClause(ISingleValueCaseClauseOperation operation, object argument)
        {
            return new SingleValueCaseClauseOperation(operation.Label, Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitRelationalCaseClause(IRelationalCaseClauseOperation operation, object argument)
        {
            return new RelationalCaseClauseOperation(Visit(operation.Value), operation.Relation, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitRangeCaseClause(IRangeCaseClauseOperation operation, object argument)
        {
            return new RangeCaseClauseOperation(Visit(operation.MinimumValue), Visit(operation.MaximumValue), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitDefaultCaseClause(IDefaultCaseClauseOperation operation, object argument)
        {
            return new DefaultCaseClauseOperation(operation.Label, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitWhileLoop(IWhileLoopOperation operation, object argument)
        {
            return new WhileLoopOperation(Visit(operation.Condition), Visit(operation.Body), Visit(operation.IgnoredCondition), operation.Locals, operation.ContinueLabel, operation.ExitLabel, operation.ConditionIsTop, operation.ConditionIsUntil, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitForLoop(IForLoopOperation operation, object argument)
        {
            return new ForLoopOperation(VisitArray(operation.Before), Visit(operation.Condition), VisitArray(operation.AtLoopBottom), operation.Locals, operation.ConditionLocals,
                operation.ContinueLabel, operation.ExitLabel, Visit(operation.Body), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitForToLoop(IForToLoopOperation operation, object argument)
        {
            return new ForToLoopOperation(operation.Locals, operation.IsChecked, ((BaseForToLoopOperation)operation).Info, operation.ContinueLabel, operation.ExitLabel,
                                          Visit(operation.LoopControlVariable), Visit(operation.InitialValue), Visit(operation.LimitValue), Visit(operation.StepValue),
                                          Visit(operation.Body), VisitArray(operation.NextVariables), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type,
                                          operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitForEachLoop(IForEachLoopOperation operation, object argument)
        {
            return new ForEachLoopOperation(operation.Locals, operation.ContinueLabel, operation.ExitLabel, Visit(operation.LoopControlVariable),
                                            Visit(operation.Collection), VisitArray(operation.NextVariables), operation.IsAsynchronous, Visit(operation.Body), ((BaseForEachLoopOperation)operation).Info,
                                            ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        internal override IOperation VisitWithStatement(IWithStatementOperation operation, object argument)
        {
            return new WithStatementOperation(Visit(operation.Body), Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitFieldReference(IFieldReferenceOperation operation, object argument)
        {
            return new FieldReferenceOperation(operation.Field, operation.IsDeclaration, Visit(operation.Instance), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitMethodReference(IMethodReferenceOperation operation, object argument)
        {
            return new MethodReferenceOperation(operation.Method, operation.IsVirtual, Visit(operation.Instance), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitPropertyReference(IPropertyReferenceOperation operation, object argument)
        {
            return new PropertyReferenceOperation(operation.Property, VisitArray(operation.Arguments), Visit(operation.Instance), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitEventReference(IEventReferenceOperation operation, object argument)
        {
            return new EventReferenceOperation(operation.Event, Visit(operation.Instance), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitCompoundAssignment(ICompoundAssignmentOperation operation, object argument)
        {
            var compoundAssignment = (BaseCompoundAssignmentOperation)operation;
            return new CompoundAssignmentOperation(compoundAssignment.InConversionConvertible, compoundAssignment.OutConversionConvertible, operation.OperatorKind, operation.IsLifted, operation.IsChecked, operation.OperatorMethod, Visit(operation.Target), Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitCoalesceAssignment(ICoalesceAssignmentOperation operation, object argument)
        {
            return new CoalesceAssignmentOperation(Visit(operation.Target), Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitFlowAnonymousFunction(IFlowAnonymousFunctionOperation operation, object argument)
        {
            var anonymous = (FlowAnonymousFunctionOperation)operation;
            return new FlowAnonymousFunctionOperation(in anonymous.Context, anonymous.Original, operation.IsImplicit);
        }

        public override IOperation VisitFieldInitializer(IFieldInitializerOperation operation, object argument)
        {
            return new FieldInitializerOperation(operation.InitializedFields, operation.Locals, Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitVariableInitializer(IVariableInitializerOperation operation, object argument)
        {
            return new VariableInitializerOperation(Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitPropertyInitializer(IPropertyInitializerOperation operation, object argument)
        {
            return new PropertyInitializerOperation(operation.InitializedProperties, operation.Locals, Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitParameterInitializer(IParameterInitializerOperation operation, object argument)
        {
            return new ParameterInitializerOperation(operation.Parameter, operation.Locals, Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitSimpleAssignment(ISimpleAssignmentOperation operation, object argument)
        {
            return new SimpleAssignmentOperation(operation.IsRef, Visit(operation.Target), Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitDeconstructionAssignment(IDeconstructionAssignmentOperation operation, object argument)
        {
            return new DeconstructionAssignmentOperation(Visit(operation.Target), Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitDynamicMemberReference(IDynamicMemberReferenceOperation operation, object argument)
        {
            return new DynamicMemberReferenceOperation(Visit(operation.Instance), operation.MemberName, operation.TypeArguments, operation.ContainingType, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitDynamicObjectCreation(IDynamicObjectCreationOperation operation, object argument)
        {
            return new DynamicObjectCreationOperation(VisitArray(operation.Arguments), ((HasDynamicArgumentsExpression)operation).ArgumentNames, ((HasDynamicArgumentsExpression)operation).ArgumentRefKinds, Visit(operation.Initializer), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitDynamicInvocation(IDynamicInvocationOperation operation, object argument)
        {
            return new DynamicInvocationOperation(Visit(operation.Operation), VisitArray(operation.Arguments), ((HasDynamicArgumentsExpression)operation).ArgumentNames, ((HasDynamicArgumentsExpression)operation).ArgumentRefKinds, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitDynamicIndexerAccess(IDynamicIndexerAccessOperation operation, object argument)
        {
            return new DynamicIndexerAccessOperation(Visit(operation.Operation), VisitArray(operation.Arguments), ((HasDynamicArgumentsExpression)operation).ArgumentNames, ((HasDynamicArgumentsExpression)operation).ArgumentRefKinds, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        internal override IOperation VisitNoPiaObjectCreation(INoPiaObjectCreationOperation operation, object argument)
        {
            return new NoPiaObjectCreationOperation(Visit(operation.Initializer), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitInvalid(IInvalidOperation operation, object argument)
        {
            return new InvalidOperation(VisitArray(operation.Children.ToImmutableArray()), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitInterpolatedStringText(IInterpolatedStringTextOperation operation, object argument)
        {
            return new InterpolatedStringTextOperation(Visit(operation.Text), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitInterpolation(IInterpolationOperation operation, object argument)
        {
            return new InterpolationOperation(Visit(operation.Expression), Visit(operation.Alignment), Visit(operation.FormatString), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitConstantPattern(IConstantPatternOperation operation, object argument)
        {
            return new ConstantPatternOperation(operation.InputType, operation.NarrowedType, Visit(operation.Value), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.IsImplicit);
        }

        public override IOperation VisitDeclarationPattern(IDeclarationPatternOperation operation, object argument)
        {
            return new DeclarationPatternOperation(
                operation.MatchedType,
                operation.MatchesNull,
                operation.DeclaredSymbol,
                operation.InputType,
                operation.NarrowedType,
                ((Operation)operation).OwningSemanticModel,
                operation.Syntax,
                operation.Type,
                operation.GetConstantValue(),
                operation.IsImplicit);
        }

        public override IOperation VisitRecursivePattern(IRecursivePatternOperation operation, object argument)
        {
            return new RecursivePatternOperation(
                operation.InputType,
                operation.NarrowedType,
                operation.MatchedType,
                operation.DeconstructSymbol,
                VisitArray(operation.DeconstructionSubpatterns),
                VisitArray(operation.PropertySubpatterns),
                operation.DeclaredSymbol,
                ((Operation)operation).OwningSemanticModel,
                operation.Syntax,
                operation.IsImplicit);
        }

        public override IOperation VisitPatternCaseClause(IPatternCaseClauseOperation operation, object argument)
        {
            return new PatternCaseClauseOperation(operation.Label, Visit(operation.Pattern), Visit(operation.Guard), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitConstructorBodyOperation(IConstructorBodyOperation operation, object argument)
        {
            return new ConstructorBodyOperation(operation.Locals, ((Operation)operation).OwningSemanticModel, operation.Syntax, Visit(operation.Initializer), Visit(operation.BlockBody), Visit(operation.ExpressionBody));
        }

        public override IOperation VisitMethodBodyOperation(IMethodBodyOperation operation, object argument)
        {
            return new MethodBodyOperation(((Operation)operation).OwningSemanticModel, operation.Syntax, Visit(operation.BlockBody), Visit(operation.ExpressionBody));
        }

        public override IOperation VisitDiscardPattern(IDiscardPatternOperation operation, object argument)
        {
            return new DiscardPatternOperation(operation.InputType, operation.NarrowedType, operation.SemanticModel, operation.Syntax, operation.IsImplicit);
        }

        public override IOperation VisitFlowCapture(IFlowCaptureOperation operation, object argument)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override IOperation VisitFlowCaptureReference(IFlowCaptureReferenceOperation operation, object argument)
        {
            return new FlowCaptureReferenceOperation(operation.Id, operation.Syntax, operation.Type, constantValue: operation.GetConstantValue());
        }

        public override IOperation VisitIsNull(IIsNullOperation operation, object argument)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override IOperation VisitCaughtException(ICaughtExceptionOperation operation, object argument)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override IOperation VisitStaticLocalInitializationSemaphore(IStaticLocalInitializationSemaphoreOperation operation, object argument)
        {
            throw ExceptionUtilities.Unreachable;
        }

        public override IOperation VisitUsingDeclaration(IUsingDeclarationOperation operation, object argument)
        {
            return new UsingDeclarationOperation(Visit(operation.DeclarationGroup), operation.IsAsynchronous, ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }

        public override IOperation VisitWith(IWithOperation operation, object argument)
        {
            return new WithOperation(Visit(operation.Operand), operation.CloneMethod, Visit(operation.Initializer), ((Operation)operation).OwningSemanticModel, operation.Syntax, operation.Type, operation.GetConstantValue(), operation.IsImplicit);
        }
    }
}
