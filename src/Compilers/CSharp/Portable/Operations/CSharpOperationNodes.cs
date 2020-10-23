﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Operations
{

    internal sealed class CSharpLazyNoneOperation : LazyNoneOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _boundNode;

        public CSharpLazyNoneOperation(CSharpOperationFactory operationFactory, BoundNode boundNode, SemanticModel semanticModel, SyntaxNode node, ConstantValue constantValue, bool isImplicit, ITypeSymbol type) :
            base(semanticModel, node, constantValue: constantValue, isImplicit: isImplicit, type)
        {
            _operationFactory = operationFactory;
            _boundNode = boundNode;
        }

        protected override ImmutableArray<IOperation> GetChildren() => _operationFactory.GetIOperationChildren(_boundNode);
    }


    internal sealed class CSharpLazyNonePatternOperation : LazyNoneOperation, IPatternOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundPattern _boundNode;

        public CSharpLazyNonePatternOperation(CSharpOperationFactory operationFactory, BoundPattern boundNode, SemanticModel semanticModel, SyntaxNode node, bool isImplicit) :
            base(semanticModel, node, constantValue: null, isImplicit: isImplicit, type: null)
        {
            _operationFactory = operationFactory;
            _boundNode = boundNode;
        }

        public ITypeSymbol InputType => _boundNode.InputType.GetITypeSymbol(NullableAnnotation.None);

        public ITypeSymbol NarrowedType => _boundNode.NarrowedType.GetITypeSymbol(NullableAnnotation.None);

        protected override ImmutableArray<IOperation> GetChildren() => _operationFactory.GetIOperationChildren(_boundNode);
    }

    internal sealed class CSharpLazySimpleAssignmentOperation : LazySimpleAssignmentOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundAssignmentOperator _assignmentOperator;

        internal CSharpLazySimpleAssignmentOperation(CSharpOperationFactory operationFactory, BoundAssignmentOperator assignment, bool isRef, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(isRef, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _assignmentOperator = assignment;
        }

        protected override IOperation CreateTarget()
        {
            return _operationFactory.Create(_assignmentOperator.Left);
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_assignmentOperator.Right);
        }
    }

    internal sealed class CSharpLazyDeconstructionAssignmentOperation : LazyDeconstructionAssignmentOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundDeconstructionAssignmentOperator _deconstructionAssignment;

        internal CSharpLazyDeconstructionAssignmentOperation(CSharpOperationFactory operationFactory, BoundDeconstructionAssignmentOperator deconstructionAssignment, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _deconstructionAssignment = deconstructionAssignment;
        }

        protected override IOperation CreateTarget()
        {
            return _operationFactory.Create(_deconstructionAssignment.Left);
        }

        protected override IOperation CreateValue()
        {
            // Skip the synthetic deconstruction conversion wrapping the right operand.
            return _operationFactory.Create(_deconstructionAssignment.Right.Operand);
        }
    }

    internal sealed class CSharpLazyCompoundAssignmentOperation : LazyCompoundAssignmentOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundCompoundAssignmentOperator _compoundAssignmentOperator;

        internal CSharpLazyCompoundAssignmentOperation(CSharpOperationFactory operationFactory, BoundCompoundAssignmentOperator compoundAssignmentOperator, IConvertibleConversion inConversionConvertible, IConvertibleConversion outConversionConvertible, BinaryOperatorKind operatorKind, bool isLifted, bool isChecked, IMethodSymbol operatorMethod, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(inConversionConvertible, outConversionConvertible, operatorKind, isLifted, isChecked, operatorMethod, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _compoundAssignmentOperator = compoundAssignmentOperator;
        }

        protected override IOperation CreateTarget()
        {
            return _operationFactory.Create(_compoundAssignmentOperator.Left);
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_compoundAssignmentOperator.Right);
        }
    }

    internal sealed class CSharpLazyConversionOperation : LazyConversionOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _operand;

        internal CSharpLazyConversionOperation(CSharpOperationFactory operationFactory, BoundNode operand, IConvertibleConversion convertibleConversion, bool isTryCast, bool isChecked, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(convertibleConversion, isTryCast, isChecked, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _operand = operand;
        }

        protected override IOperation CreateOperand()
        {
            return _operationFactory.Create(_operand);
        }
    }

    internal sealed class CSharpLazyEventReferenceOperation : LazyEventReferenceOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _instance;

        internal CSharpLazyEventReferenceOperation(CSharpOperationFactory operationFactory, BoundNode instance, IEventSymbol @event, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(@event, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _instance = instance;
        }

        protected override IOperation CreateInstance()
        {
            return _operationFactory.CreateReceiverOperation(_instance, Event.GetSymbol());
        }
    }

    internal sealed class CSharpLazyVariableInitializerOperation : LazyVariableInitializerOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _value;

        internal CSharpLazyVariableInitializerOperation(CSharpOperationFactory operationFactory, BoundNode value, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(locals: ImmutableArray<ILocalSymbol>.Empty, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _value = value;
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_value);
        }
    }

    internal sealed class CSharpLazyFieldInitializerOperation : LazyFieldInitializerOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _value;

        internal CSharpLazyFieldInitializerOperation(CSharpOperationFactory operationFactory, BoundNode value, ImmutableArray<ILocalSymbol> locals, ImmutableArray<IFieldSymbol> initializedFields, OperationKind kind, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(initializedFields, locals, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _value = value;
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_value);
        }
    }

    internal sealed class CSharpLazyFieldReferenceOperation : LazyFieldReferenceOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _instance;

        internal CSharpLazyFieldReferenceOperation(CSharpOperationFactory operationFactory, BoundNode instance, IFieldSymbol field, bool isDeclaration, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(field, isDeclaration, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _instance = instance;
        }

        protected override IOperation CreateInstance()
        {
            return _operationFactory.CreateReceiverOperation(_instance, Field.GetSymbol());
        }
    }

    internal sealed class CSharpLazyForEachLoopOperation : LazyForEachLoopOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundForEachStatement _forEachStatement;

        internal CSharpLazyForEachLoopOperation(CSharpOperationFactory operationFactory, BoundForEachStatement forEachStatement, ImmutableArray<ILocalSymbol> locals, ILabelSymbol continueLabel, ILabelSymbol exitLabel, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(forEachStatement.AwaitOpt != null, LoopKind.ForEach, locals, continueLabel, exitLabel, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _forEachStatement = forEachStatement;
        }

        protected override IOperation CreateLoopControlVariable()
        {
            return _operationFactory.CreateBoundForEachStatementLoopControlVariable(_forEachStatement);
        }

        protected override IOperation CreateCollection()
        {
            return _operationFactory.Create(_forEachStatement.Expression);
        }

        protected override ImmutableArray<IOperation> CreateNextVariables()
        {
            return ImmutableArray<IOperation>.Empty;
        }

        protected override IOperation CreateBody()
        {
            return _operationFactory.Create(_forEachStatement.Body);
        }

        protected override ForEachLoopOperationInfo CreateLoopInfo()
        {
            return _operationFactory.GetForEachLoopOperatorInfo(_forEachStatement);
        }
    }

    internal sealed class CSharpLazyForLoopOperation : LazyForLoopOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundForStatement _forStatement;

        internal CSharpLazyForLoopOperation(CSharpOperationFactory operationFactory, BoundForStatement forStatement, ImmutableArray<ILocalSymbol> locals, ImmutableArray<ILocalSymbol> conditionLocals, ILabelSymbol continueLabel, ILabelSymbol exitLabel, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(locals, conditionLocals, continueLabel, exitLabel, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _forStatement = forStatement;
        }

        protected override ImmutableArray<IOperation> CreateBefore()
        {
            return _operationFactory.CreateFromArray<BoundStatement, IOperation>(_operationFactory.ToStatements(_forStatement.Initializer));
        }

        protected override IOperation CreateCondition()
        {
            return _operationFactory.Create(_forStatement.Condition);
        }

        protected override ImmutableArray<IOperation> CreateAtLoopBottom()
        {
            return _operationFactory.CreateFromArray<BoundStatement, IOperation>(_operationFactory.ToStatements(_forStatement.Increment));
        }

        protected override IOperation CreateBody()
        {
            return _operationFactory.Create(_forStatement.Body);
        }
    }

    internal sealed class CSharpLazyInterpolatedStringTextOperation : LazyInterpolatedStringTextOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundLiteral _text;

        internal CSharpLazyInterpolatedStringTextOperation(CSharpOperationFactory operationFactory, BoundLiteral text, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _text = text;
        }

        protected override IOperation CreateText()
        {
            return _operationFactory.CreateBoundLiteralOperation(_text, @implicit: true);
        }
    }

    internal sealed class CSharpLazyInterpolationOperation : LazyInterpolationOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundStringInsert _stringInsert;

        internal CSharpLazyInterpolationOperation(CSharpOperationFactory operationFactory, BoundStringInsert stringInsert, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _stringInsert = stringInsert;
        }

        protected override IOperation CreateExpression()
        {
            return _operationFactory.Create(_stringInsert.Value);
        }

        protected override IOperation CreateAlignment()
        {
            return _operationFactory.Create(_stringInsert.Alignment);
        }

        protected override IOperation CreateFormatString()
        {
            return _operationFactory.Create(_stringInsert.Format);
        }
    }

    internal sealed class CSharpLazyInvalidOperation : LazyInvalidOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly IBoundInvalidNode _node;

        internal CSharpLazyInvalidOperation(CSharpOperationFactory operationFactory, IBoundInvalidNode node, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _node = node;
        }

        protected override ImmutableArray<IOperation> CreateChildren()
        {
            return _operationFactory.CreateFromArray<BoundNode, IOperation>(_node.InvalidNodeChildren);
        }
    }

    internal sealed class CSharpLazyDynamicMemberReferenceOperation : LazyDynamicMemberReferenceOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _instance;

        internal CSharpLazyDynamicMemberReferenceOperation(CSharpOperationFactory operationFactory, BoundNode instance, string memberName, ImmutableArray<ITypeSymbol> typeArguments, ITypeSymbol containingType, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(memberName, typeArguments, containingType, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _instance = instance;
        }

        protected override IOperation CreateInstance()
        {
            return _operationFactory.Create(_instance);
        }
    }

    internal sealed class CSharpLazyMethodReferenceOperation : LazyMethodReferenceOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _instance;

        internal CSharpLazyMethodReferenceOperation(CSharpOperationFactory operationFactory, BoundNode instance, IMethodSymbol method, bool isVirtual, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(method, isVirtual, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _instance = instance;
        }

        protected override IOperation CreateInstance()
        {
            return _operationFactory.CreateReceiverOperation(_instance, Method.GetSymbol());
        }
    }

    internal sealed class CSharpLazyCoalesceAssignmentOperation : LazyCoalesceAssignmentOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNullCoalescingAssignmentOperator _nullCoalescingAssignmentOperator;

        internal CSharpLazyCoalesceAssignmentOperation(CSharpOperationFactory operationFactory, BoundNullCoalescingAssignmentOperator nullCoalescingAssignmentOperator, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _nullCoalescingAssignmentOperator = nullCoalescingAssignmentOperator;
        }

        protected override IOperation CreateTarget()
        {
            return _operationFactory.Create(_nullCoalescingAssignmentOperator.LeftOperand);
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_nullCoalescingAssignmentOperator.RightOperand);
        }
    }

    internal sealed class CSharpLazyWithExpressionOperation : LazyWithOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundWithExpression _withExpression;

        internal CSharpLazyWithExpressionOperation(CSharpOperationFactory operationFactory, BoundWithExpression withExpression, IMethodSymbol cloneMethod, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(cloneMethod, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _withExpression = withExpression;
        }

        protected override IObjectOrCollectionInitializerOperation CreateInitializer()
        {
            return (IObjectOrCollectionInitializerOperation)_operationFactory.Create(_withExpression.InitializerExpression);
        }

        protected override IOperation CreateOperand()
        {
            return _operationFactory.Create(_withExpression.Receiver);
        }
    }

    internal sealed class CSharpLazyParameterInitializerOperation : LazyParameterInitializerOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _value;

        internal CSharpLazyParameterInitializerOperation(CSharpOperationFactory operationFactory, BoundNode value, ImmutableArray<ILocalSymbol> locals, IParameterSymbol parameter, OperationKind kind, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(parameter, locals, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _value = value;
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_value);
        }
    }

    internal sealed class CSharpLazyPropertyInitializerOperation : LazyPropertyInitializerOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _value;

        internal CSharpLazyPropertyInitializerOperation(CSharpOperationFactory operationFactory, BoundNode value, ImmutableArray<ILocalSymbol> locals, ImmutableArray<IPropertySymbol> initializedProperties, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(initializedProperties, locals, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _value = value;
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_value);
        }
    }

    internal sealed class CSharpLazyPropertyReferenceOperation : LazyPropertyReferenceOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _propertyReference;
        private readonly bool _isObjectOrCollectionInitializer;

        internal CSharpLazyPropertyReferenceOperation(CSharpOperationFactory operationFactory, BoundNode propertyReference, bool isObjectOrCollectionInitializer, IPropertySymbol property, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(property, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _propertyReference = propertyReference;
            _isObjectOrCollectionInitializer = isObjectOrCollectionInitializer;
        }

        protected override IOperation CreateInstance()
        {
            return _operationFactory.CreateBoundPropertyReferenceInstance(_propertyReference);
        }

        protected override ImmutableArray<IArgumentOperation> CreateArguments()
        {
            return _propertyReference is null || _propertyReference.Kind == BoundKind.PropertyAccess ? ImmutableArray<IArgumentOperation>.Empty : _operationFactory.DeriveArguments(_propertyReference, _isObjectOrCollectionInitializer);
        }
    }

    internal sealed class CSharpLazySingleValueCaseClauseOperation : LazySingleValueCaseClauseOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _value;

        internal CSharpLazySingleValueCaseClauseOperation(CSharpOperationFactory operationFactory, BoundNode value, ILabelSymbol label, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(CaseKind.SingleValue, label, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _value = value;
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_value);
        }
    }

    internal sealed class CSharpLazyDynamicObjectCreationOperation : LazyDynamicObjectCreationOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundDynamicObjectCreationExpression _dynamicObjectCreationExpression;

        internal CSharpLazyDynamicObjectCreationOperation(CSharpOperationFactory operationFactory, BoundDynamicObjectCreationExpression dynamicObjectCreationExpression, ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _dynamicObjectCreationExpression = dynamicObjectCreationExpression;
        }

        protected override ImmutableArray<IOperation> CreateArguments()
        {
            return _operationFactory.CreateFromArray<BoundExpression, IOperation>(_dynamicObjectCreationExpression.Arguments);
        }

        protected override IObjectOrCollectionInitializerOperation CreateInitializer()
        {
            return (IObjectOrCollectionInitializerOperation)_operationFactory.Create(_dynamicObjectCreationExpression.InitializerExpressionOpt);
        }
    }

    internal sealed class CSharpLazyDynamicInvocationOperation : LazyDynamicInvocationOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundDynamicInvocableBase _dynamicInvocable;

        internal CSharpLazyDynamicInvocationOperation(CSharpOperationFactory operationFactory, BoundDynamicInvocableBase dynamicInvocable, ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _dynamicInvocable = dynamicInvocable;
        }

        protected override IOperation CreateOperation()
        {
            return _operationFactory.CreateBoundDynamicInvocationExpressionReceiver(_dynamicInvocable.Expression);
        }

        protected override ImmutableArray<IOperation> CreateArguments()
        {
            return _operationFactory.CreateFromArray<BoundExpression, IOperation>(_dynamicInvocable.Arguments);
        }
    }

    internal sealed class CSharpLazyDynamicIndexerAccessOperation : LazyDynamicIndexerAccessOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundExpression _indexer;

        internal CSharpLazyDynamicIndexerAccessOperation(CSharpOperationFactory operationFactory, BoundExpression indexer, ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _indexer = indexer;
        }

        protected override IOperation CreateOperation()
        {
            return _operationFactory.CreateBoundDynamicIndexerAccessExpressionReceiver(_indexer);
        }

        protected override ImmutableArray<IOperation> CreateArguments()
        {
            return _operationFactory.CreateBoundDynamicIndexerAccessArguments(_indexer);
        }
    }

    internal sealed class CSharpLazyVariableDeclaratorOperation : LazyVariableDeclaratorOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundLocalDeclaration _localDeclaration;

        internal CSharpLazyVariableDeclaratorOperation(CSharpOperationFactory operationFactory, BoundLocalDeclaration localDeclaration, ILocalSymbol symbol, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(symbol, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _localDeclaration = localDeclaration;
        }

        protected override IVariableInitializerOperation CreateInitializer()
        {
            return _operationFactory.CreateVariableDeclaratorInitializer(_localDeclaration, Syntax);
        }

        protected override ImmutableArray<IOperation> CreateIgnoredArguments()
        {
            return _operationFactory.CreateFromArray<BoundExpression, IOperation>(_localDeclaration.ArgumentsOpt);
        }
    }

    internal sealed class CSharpLazyVariableDeclarationOperation : LazyVariableDeclarationOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _localDeclaration;

        internal CSharpLazyVariableDeclarationOperation(CSharpOperationFactory operationFactory, BoundNode localDeclaration, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _localDeclaration = localDeclaration;
        }

        protected override ImmutableArray<IVariableDeclaratorOperation> CreateDeclarators()
        {
            return _operationFactory.CreateVariableDeclarator(_localDeclaration, Syntax);
        }

        protected override IVariableInitializerOperation CreateInitializer()
        {
            return null;
        }

        protected override ImmutableArray<IOperation> CreateIgnoredDimensions()
        {
            return _operationFactory.CreateIgnoredDimensions(_localDeclaration, Syntax);
        }
    }

    internal sealed class CSharpLazyWhileLoopOperation : LazyWhileLoopOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundConditionalLoopStatement _conditionalLoopStatement;

        internal CSharpLazyWhileLoopOperation(CSharpOperationFactory operationFactory, BoundConditionalLoopStatement conditionalLoopStatement, ImmutableArray<ILocalSymbol> locals, ILabelSymbol continueLabel, ILabelSymbol exitLabel, bool conditionIsTop, bool conditionIsUntil, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(conditionIsTop, conditionIsUntil, LoopKind.While, locals, continueLabel, exitLabel, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _conditionalLoopStatement = conditionalLoopStatement;
        }

        protected override IOperation CreateCondition()
        {
            return _operationFactory.Create(_conditionalLoopStatement.Condition);
        }

        protected override IOperation CreateBody()
        {
            return _operationFactory.Create(_conditionalLoopStatement.Body);
        }

        protected override IOperation CreateIgnoredCondition()
        {
            return null;
        }
    }

    internal sealed class CSharpLazyConstantPatternOperation : LazyConstantPatternOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _value;

        internal CSharpLazyConstantPatternOperation(ITypeSymbol inputType, ITypeSymbol narrowedType, CSharpOperationFactory operationFactory, BoundNode value, SemanticModel semanticModel, SyntaxNode syntax, bool isImplicit) :
            base(inputType, narrowedType, semanticModel, syntax, type: null, constantValue: null, isImplicit)
        {
            _operationFactory = operationFactory;
            _value = value;
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_value);
        }
    }

    internal sealed class CSharpLazyRelationalPatternOperation : LazyRelationalPatternOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _value;

        internal CSharpLazyRelationalPatternOperation(ITypeSymbol inputType, ITypeSymbol narrowedType, CSharpOperationFactory operationFactory, BinaryOperatorKind operatorKind, BoundNode value, SemanticModel semanticModel, SyntaxNode syntax, bool isImplicit) :
            base(operatorKind, inputType, narrowedType, semanticModel, syntax, type: null, constantValue: null, isImplicit)
        {
            _operationFactory = operationFactory;
            _value = value;
        }

        protected override IOperation CreateValue()
        {
            return _operationFactory.Create(_value);
        }
    }

    /// <summary>
    /// Represents a C# negated pattern.
    /// </summary>
    internal sealed partial class CSharpLazyNegatedPatternOperation : LazyNegatedPatternOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNegatedPattern _boundNegatedPattern;

        public CSharpLazyNegatedPatternOperation(
            CSharpOperationFactory operationFactory,
            BoundNegatedPattern boundNegatedPattern,
            SemanticModel semanticModel)
            : base(inputType: boundNegatedPattern.InputType.GetPublicSymbol(),
                   narrowedType: boundNegatedPattern.NarrowedType.GetPublicSymbol(),
                   semanticModel: semanticModel,
                   syntax: boundNegatedPattern.Syntax,
                   type: null,
                   constantValue: null,
                   isImplicit: boundNegatedPattern.WasCompilerGenerated)
        {
            _operationFactory = operationFactory;
            _boundNegatedPattern = boundNegatedPattern;
        }
        protected override IPatternOperation CreatePattern()
        {
            return (IPatternOperation)_operationFactory.Create(_boundNegatedPattern.Negated);
        }
    }
    /// <summary>
    /// Represents a C# binary pattern.
    /// </summary>
    internal sealed partial class CSharpLazyBinaryPatternOperation : LazyBinaryPatternOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundBinaryPattern _boundBinaryPattern;

        public CSharpLazyBinaryPatternOperation(
            CSharpOperationFactory operationFactory,
            BoundBinaryPattern boundBinaryPattern,
            SemanticModel semanticModel)
            : base(operatorKind: boundBinaryPattern.Disjunction ? BinaryOperatorKind.Or : BinaryOperatorKind.And,
                   inputType: boundBinaryPattern.InputType.GetPublicSymbol(),
                   narrowedType: boundBinaryPattern.NarrowedType.GetPublicSymbol(),
                   semanticModel: semanticModel,
                   syntax: boundBinaryPattern.Syntax,
                   type: null,
                   constantValue: null,
                   isImplicit: boundBinaryPattern.WasCompilerGenerated)
        {
            _operationFactory = operationFactory;
            _boundBinaryPattern = boundBinaryPattern;
        }
        protected override IPatternOperation CreateLeftPattern()
        {
            return (IPatternOperation)_operationFactory.Create(_boundBinaryPattern.Left);
        }
        protected override IPatternOperation CreateRightPattern()
        {
            return (IPatternOperation)_operationFactory.Create(_boundBinaryPattern.Right);
        }
    }

    /// <summary>
    /// Represents a C# recursive pattern.
    /// </summary>
    internal sealed partial class CSharpLazyRecursivePatternOperation : LazyRecursivePatternOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundRecursivePattern _boundRecursivePattern;

        public CSharpLazyRecursivePatternOperation(
            CSharpOperationFactory operationFactory,
            BoundRecursivePattern boundRecursivePattern,
            SemanticModel semanticModel)
            : base(inputType: boundRecursivePattern.InputType.GetPublicSymbol(),
                   narrowedType: boundRecursivePattern.NarrowedType.GetPublicSymbol(),
                   matchedType: (boundRecursivePattern.DeclaredType?.Type ?? boundRecursivePattern.InputType.StrippedType()).GetPublicSymbol(),
                   deconstructSymbol: boundRecursivePattern.DeconstructMethod.GetPublicSymbol(),
                   declaredSymbol: boundRecursivePattern.Variable.GetPublicSymbol(),
                   semanticModel: semanticModel,
                   syntax: boundRecursivePattern.Syntax,
                   type: null,
                   constantValue: null,
                   isImplicit: boundRecursivePattern.WasCompilerGenerated)
        {
            _operationFactory = operationFactory;
            _boundRecursivePattern = boundRecursivePattern;

        }
        protected override ImmutableArray<IPatternOperation> CreateDeconstructionSubpatterns()
        {
            return _boundRecursivePattern.Deconstruction.IsDefault ? ImmutableArray<IPatternOperation>.Empty :
                _boundRecursivePattern.Deconstruction.SelectAsArray<BoundSubpattern, CSharpOperationFactory, IPatternOperation>((p, fac) => (IPatternOperation)fac.Create(p.Pattern), _operationFactory);
        }
        protected override ImmutableArray<IPropertySubpatternOperation> CreatePropertySubpatterns()
        {
            return _boundRecursivePattern.Properties.IsDefault ? ImmutableArray<IPropertySubpatternOperation>.Empty :
                _boundRecursivePattern.Properties.SelectAsArray<BoundSubpattern, CSharpLazyRecursivePatternOperation, IPropertySubpatternOperation>((p, recursivePattern) => recursivePattern._operationFactory.CreatePropertySubpattern(p, recursivePattern.MatchedType), this);
        }
    }

    /// <summary>
    /// Represents a C# recursive pattern using ITuple.
    /// </summary>
    internal sealed partial class CSharpLazyITuplePatternOperation : LazyRecursivePatternOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundITuplePattern _boundITuplePattern;

        public CSharpLazyITuplePatternOperation(CSharpOperationFactory operationFactory, BoundITuplePattern boundITuplePattern, SemanticModel semanticModel)
            : base(inputType: boundITuplePattern.InputType.GetPublicSymbol(),
                   narrowedType: boundITuplePattern.NarrowedType.GetPublicSymbol(),
                   matchedType: boundITuplePattern.InputType.StrippedType().GetPublicSymbol(),
                   deconstructSymbol: boundITuplePattern.GetLengthMethod.ContainingType.GetPublicSymbol(),
                   declaredSymbol: null,
                   semanticModel: semanticModel,
                   syntax: boundITuplePattern.Syntax,
                   type: null,
                   constantValue: null,
                   isImplicit: boundITuplePattern.WasCompilerGenerated)
        {
            _operationFactory = operationFactory;
            _boundITuplePattern = boundITuplePattern;

        }
        protected override ImmutableArray<IPatternOperation> CreateDeconstructionSubpatterns()
        {
            return _boundITuplePattern.Subpatterns.IsDefault ? ImmutableArray<IPatternOperation>.Empty :
                _boundITuplePattern.Subpatterns.SelectAsArray<BoundSubpattern, CSharpOperationFactory, IPatternOperation>((p, fac) => (IPatternOperation)fac.Create(p.Pattern), _operationFactory);
        }
        protected override ImmutableArray<IPropertySubpatternOperation> CreatePropertySubpatterns()
        {
            return ImmutableArray<IPropertySubpatternOperation>.Empty;
        }
    }

    internal sealed class CSharpLazyPatternCaseClauseOperation : LazyPatternCaseClauseOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundSwitchLabel _patternSwitchLabel;

        internal CSharpLazyPatternCaseClauseOperation(CSharpOperationFactory operationFactory, BoundSwitchLabel patternSwitchLabel, ILabelSymbol label, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(label, semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _patternSwitchLabel = patternSwitchLabel;
        }

        protected override IPatternOperation CreatePattern()
        {
            return (IPatternOperation)_operationFactory.Create(_patternSwitchLabel.Pattern);
        }

        protected override IOperation CreateGuard()
        {
            return _operationFactory.Create(_patternSwitchLabel.WhenClause);
        }
    }

    internal sealed class CSharpLazyMethodBodyOperation : LazyMethodBodyOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNonConstructorMethodBody _methodBody;

        internal CSharpLazyMethodBodyOperation(CSharpOperationFactory operationFactory, BoundNonConstructorMethodBody methodBody, SemanticModel semanticModel, SyntaxNode syntax) :
            base(semanticModel, syntax, type: null, constantValue: null, isImplicit: false)
        {
            _operationFactory = operationFactory;
            _methodBody = methodBody;
        }

        protected override IBlockOperation CreateBlockBody()
        {
            return (IBlockOperation)_operationFactory.Create(_methodBody.BlockBody);
        }

        protected override IBlockOperation CreateExpressionBody()
        {
            return (IBlockOperation)_operationFactory.Create(_methodBody.ExpressionBody);
        }
    }

    internal sealed class CSharpLazyConstructorBodyOperation : LazyConstructorBodyOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundConstructorMethodBody _constructorMethodBody;

        internal CSharpLazyConstructorBodyOperation(CSharpOperationFactory operationFactory, BoundConstructorMethodBody constructorMethodBody, ImmutableArray<ILocalSymbol> locals, SemanticModel semanticModel, SyntaxNode syntax) :
            base(locals, semanticModel, syntax, type: null, constantValue: null, isImplicit: false)
        {
            _operationFactory = operationFactory;
            _constructorMethodBody = constructorMethodBody;
        }

        protected override IOperation CreateInitializer()
        {
            return _operationFactory.Create(_constructorMethodBody.Initializer);
        }

        protected override IBlockOperation CreateBlockBody()
        {
            return (IBlockOperation)_operationFactory.Create(_constructorMethodBody.BlockBody);
        }

        protected override IBlockOperation CreateExpressionBody()
        {
            return (IBlockOperation)_operationFactory.Create(_constructorMethodBody.ExpressionBody);
        }
    }

    internal sealed class CSharpLazyNoPiaObjectCreationOperation : LazyNoPiaObjectCreationOperation
    {
        private readonly CSharpOperationFactory _operationFactory;
        private readonly BoundNode _initializer;

        internal CSharpLazyNoPiaObjectCreationOperation(CSharpOperationFactory operationFactory, BoundNode initializer, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            _operationFactory = operationFactory;
            _initializer = initializer;
        }

        protected override IObjectOrCollectionInitializerOperation CreateInitializer()
        {
            return (IObjectOrCollectionInitializerOperation)_operationFactory.Create(_initializer);
        }
    }
}
