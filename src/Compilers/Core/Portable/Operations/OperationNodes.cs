﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.Operations
{
    /// <summary>
    /// Use this to create IOperation when we don't have proper specific IOperation yet for given language construct
    /// </summary>
    internal abstract class BaseNoneOperation : OperationOld
    {
        protected BaseNoneOperation(SemanticModel semanticModel, SyntaxNode syntax, ConstantValue constantValue, bool isImplicit, ITypeSymbol type) :
            base(OperationKind.None, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitNoneOperation(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitNoneOperation(this, argument);
        }
    }

    internal class NoneOperation : BaseNoneOperation
    {
        public NoneOperation(ImmutableArray<IOperation> children, SemanticModel semanticModel, SyntaxNode syntax, ConstantValue constantValue, bool isImplicit, ITypeSymbol type) :
            base(semanticModel, syntax, constantValue, isImplicit, type)
        {
            Children = SetParentOperation(children, this);
        }

        public override IEnumerable<IOperation> Children { get; }
    }

    internal abstract class LazyNoneOperation : BaseNoneOperation
    {
        private ImmutableArray<IOperation> _lazyChildrenInterlocked;

        public LazyNoneOperation(SemanticModel semanticModel, SyntaxNode node, ConstantValue constantValue, bool isImplicit, ITypeSymbol type) :
            base(semanticModel, node, constantValue: constantValue, isImplicit: isImplicit, type)
        {
        }

        protected abstract ImmutableArray<IOperation> GetChildren();

        public override IEnumerable<IOperation> Children
        {
            get
            {
                if (_lazyChildrenInterlocked.IsDefault)
                {
                    ImmutableArray<IOperation> children = GetChildren();
                    SetParentOperation(children, this);
                    ImmutableInterlocked.InterlockedInitialize(ref _lazyChildrenInterlocked, children);
                }

                return _lazyChildrenInterlocked;
            }

        }
    }

#nullable enable
    internal partial class ConversionOperation
    {
        public IMethodSymbol? OperatorMethod => Conversion.MethodSymbol;
    }
#nullable disable

    internal abstract partial class BaseInvalidOperation : OperationOld, IInvalidOperation
    {
        protected BaseInvalidOperation(SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(OperationKind.Invalid, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }
        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitInvalid(this);
        }
        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitInvalid(this, argument);
        }
    }

    internal sealed partial class InvalidOperation : BaseInvalidOperation, IInvalidOperation
    {
        public InvalidOperation(ImmutableArray<IOperation> children, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
            // we don't allow null children.
            Debug.Assert(children.All(o => o != null));
            Children = SetParentOperation(children, this);
        }
        public override IEnumerable<IOperation> Children { get; }
    }

    internal abstract class LazyInvalidOperation : BaseInvalidOperation, IInvalidOperation
    {
        private ImmutableArray<IOperation> _lazyChildrenInterlocked;

        public LazyInvalidOperation(SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(semanticModel, syntax, type, constantValue, isImplicit)
        {
        }

        protected abstract ImmutableArray<IOperation> CreateChildren();

        public override IEnumerable<IOperation> Children
        {
            get
            {
                if (_lazyChildrenInterlocked.IsDefault)
                {
                    ImmutableArray<IOperation> children = CreateChildren();
                    SetParentOperation(children, this);
                    ImmutableInterlocked.InterlockedInitialize(ref _lazyChildrenInterlocked, children);
                }

                return _lazyChildrenInterlocked;
            }
        }
    }

    internal sealed class FlowAnonymousFunctionOperation : OperationOld, IFlowAnonymousFunctionOperation
    {
        public readonly ControlFlowGraphBuilder.Context Context;
        public readonly IAnonymousFunctionOperation Original;

        public FlowAnonymousFunctionOperation(in ControlFlowGraphBuilder.Context context, IAnonymousFunctionOperation original, bool isImplicit) :
            base(OperationKind.FlowAnonymousFunction, semanticModel: null, original.Syntax, original.Type, original.GetConstantValue(), isImplicit)
        {
            Context = context;
            Original = original;
        }
        public IMethodSymbol Symbol => Original.Symbol;
        public override IEnumerable<IOperation> Children
        {
            get
            {
                return ImmutableArray<IOperation>.Empty;
            }
        }
        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitFlowAnonymousFunction(this);
        }
        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitFlowAnonymousFunction(this, argument);
        }
    }

#nullable enable
    internal abstract partial class BaseMemberReferenceOperation : IMemberReferenceOperation
    {
        public abstract ISymbol Member { get; }
    }

    internal sealed partial class MethodReferenceOperation
    {
        public override ISymbol Member => Method;
    }

    internal sealed partial class PropertyReferenceOperation
    {
        public override ISymbol Member => Property;
    }

    internal sealed partial class EventReferenceOperation
    {
        public override ISymbol Member => Event;
    }

    internal sealed partial class FieldReferenceOperation
    {
        public override ISymbol Member => Field;
    }

    internal sealed partial class RangeCaseClauseOperation
    {
        public override CaseKind CaseKind => CaseKind.Range;
    }

    internal sealed partial class SingleValueCaseClauseOperation
    {
        public override CaseKind CaseKind => CaseKind.SingleValue;
    }

    internal sealed partial class RelationalCaseClauseOperation
    {
        public override CaseKind CaseKind => CaseKind.Relational;
    }

    internal sealed partial class DefaultCaseClauseOperation
    {
        public override CaseKind CaseKind => CaseKind.Default;
    }

    internal sealed partial class PatternCaseClauseOperation
    {
        public override CaseKind CaseKind => CaseKind.Pattern;
    }
#nullable disable

    internal abstract partial class HasDynamicArgumentsExpression : OperationOld
    {
        protected HasDynamicArgumentsExpression(OperationKind operationKind, ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(operationKind, semanticModel, syntax, type, constantValue, isImplicit)
        {
            ArgumentNames = argumentNames;
            ArgumentRefKinds = argumentRefKinds;
        }

        public ImmutableArray<string> ArgumentNames { get; }
        public ImmutableArray<RefKind> ArgumentRefKinds { get; }
        public abstract ImmutableArray<IOperation> Arguments { get; }
    }

    internal abstract partial class BaseDynamicObjectCreationOperation : HasDynamicArgumentsExpression, IDynamicObjectCreationOperation
    {
        public BaseDynamicObjectCreationOperation(ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(OperationKind.DynamicObjectCreation, argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }
        public override IEnumerable<IOperation> Children
        {
            get
            {
                foreach (var argument in Arguments)
                {
                    if (argument != null)
                    {
                        yield return argument;
                    }
                }
                if (Initializer != null)
                {
                    yield return Initializer;
                }
            }
        }
        public abstract IObjectOrCollectionInitializerOperation Initializer { get; }
        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitDynamicObjectCreation(this);
        }
        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitDynamicObjectCreation(this, argument);
        }
    }

    internal sealed partial class DynamicObjectCreationOperation : BaseDynamicObjectCreationOperation, IDynamicObjectCreationOperation
    {
        public DynamicObjectCreationOperation(ImmutableArray<IOperation> arguments, ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, IObjectOrCollectionInitializerOperation initializer, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
            Arguments = SetParentOperation(arguments, this);
            Initializer = SetParentOperation(initializer, this);
        }
        public override ImmutableArray<IOperation> Arguments { get; }
        public override IObjectOrCollectionInitializerOperation Initializer { get; }
    }

    internal abstract class LazyDynamicObjectCreationOperation : BaseDynamicObjectCreationOperation, IDynamicObjectCreationOperation
    {
        private ImmutableArray<IOperation> _lazyArgumentsInterlocked;
        private IObjectOrCollectionInitializerOperation _lazyInitializerInterlocked = s_unsetObjectOrCollectionInitializer;

        public LazyDynamicObjectCreationOperation(ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }

        protected abstract ImmutableArray<IOperation> CreateArguments();
        protected abstract IObjectOrCollectionInitializerOperation CreateInitializer();

        public override ImmutableArray<IOperation> Arguments
        {
            get
            {
                if (_lazyArgumentsInterlocked.IsDefault)
                {
                    ImmutableArray<IOperation> arguments = CreateArguments();
                    SetParentOperation(arguments, this);
                    ImmutableInterlocked.InterlockedInitialize(ref _lazyArgumentsInterlocked, arguments);
                }

                return _lazyArgumentsInterlocked;
            }
        }

        public override IObjectOrCollectionInitializerOperation Initializer
        {
            get
            {
                if (_lazyInitializerInterlocked == s_unsetObjectOrCollectionInitializer)
                {
                    IObjectOrCollectionInitializerOperation initializer = CreateInitializer();
                    SetParentOperation(initializer, this);
                    Interlocked.CompareExchange(ref _lazyInitializerInterlocked, initializer, s_unsetObjectOrCollectionInitializer);
                }

                return _lazyInitializerInterlocked;
            }
        }
    }

    internal abstract partial class BaseDynamicInvocationOperation : HasDynamicArgumentsExpression, IDynamicInvocationOperation
    {
        public BaseDynamicInvocationOperation(ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(OperationKind.DynamicInvocation, argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }
        public override IEnumerable<IOperation> Children
        {
            get
            {
                if (Operation != null)
                {
                    yield return Operation;
                }
                foreach (var argument in Arguments)
                {
                    if (argument != null)
                    {
                        yield return argument;
                    }
                }
            }
        }
        public abstract IOperation Operation { get; }
        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitDynamicInvocation(this);
        }
        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitDynamicInvocation(this, argument);
        }
    }

    internal sealed partial class DynamicInvocationOperation : BaseDynamicInvocationOperation, IDynamicInvocationOperation
    {
        public DynamicInvocationOperation(IOperation operation, ImmutableArray<IOperation> arguments, ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
            Operation = SetParentOperation(operation, this);
            Arguments = SetParentOperation(arguments, this);
        }
        public override IOperation Operation { get; }
        public override ImmutableArray<IOperation> Arguments { get; }
    }

    internal abstract class LazyDynamicInvocationOperation : BaseDynamicInvocationOperation, IDynamicInvocationOperation
    {
        private IOperation _lazyOperationInterlocked = s_unset;
        private ImmutableArray<IOperation> _lazyArgumentsInterlocked;

        public LazyDynamicInvocationOperation(ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }

        protected abstract IOperation CreateOperation();
        protected abstract ImmutableArray<IOperation> CreateArguments();

        public override IOperation Operation
        {
            get
            {
                if (_lazyOperationInterlocked == s_unset)
                {
                    IOperation operation = CreateOperation();
                    SetParentOperation(operation, this);
                    Interlocked.CompareExchange(ref _lazyOperationInterlocked, operation, s_unset);
                }

                return _lazyOperationInterlocked;
            }
        }

        public override ImmutableArray<IOperation> Arguments
        {
            get
            {
                if (_lazyArgumentsInterlocked.IsDefault)
                {
                    ImmutableArray<IOperation> arguments = CreateArguments();
                    SetParentOperation(arguments, this);
                    ImmutableInterlocked.InterlockedInitialize(ref _lazyArgumentsInterlocked, arguments);
                }

                return _lazyArgumentsInterlocked;
            }
        }
    }

    internal abstract partial class BaseDynamicIndexerAccessOperation : HasDynamicArgumentsExpression, IDynamicIndexerAccessOperation
    {
        public BaseDynamicIndexerAccessOperation(ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(OperationKind.DynamicIndexerAccess, argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }
        public override IEnumerable<IOperation> Children
        {
            get
            {
                if (Operation != null)
                {
                    yield return Operation;
                }
                foreach (var argument in Arguments)
                {
                    if (argument != null)
                    {
                        yield return argument;
                    }
                }
            }
        }
        public abstract IOperation Operation { get; }
        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitDynamicIndexerAccess(this);
        }
        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitDynamicIndexerAccess(this, argument);
        }
    }

    internal sealed partial class DynamicIndexerAccessOperation : BaseDynamicIndexerAccessOperation, IDynamicIndexerAccessOperation
    {
        public DynamicIndexerAccessOperation(IOperation operation, ImmutableArray<IOperation> arguments, ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
            Operation = SetParentOperation(operation, this);
            Arguments = SetParentOperation(arguments, this);
        }
        public override IOperation Operation { get; }
        public override ImmutableArray<IOperation> Arguments { get; }
    }

    internal abstract class LazyDynamicIndexerAccessOperation : BaseDynamicIndexerAccessOperation, IDynamicIndexerAccessOperation
    {
        private IOperation _lazyOperationInterlocked = s_unset;
        private ImmutableArray<IOperation> _lazyArgumentsInterlocked;

        public LazyDynamicIndexerAccessOperation(ImmutableArray<string> argumentNames, ImmutableArray<RefKind> argumentRefKinds, SemanticModel semanticModel, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue, bool isImplicit) :
            base(argumentNames, argumentRefKinds, semanticModel, syntax, type, constantValue, isImplicit)
        {
        }

        protected abstract IOperation CreateOperation();
        protected abstract ImmutableArray<IOperation> CreateArguments();

        public override IOperation Operation
        {
            get
            {
                if (_lazyOperationInterlocked == s_unset)
                {
                    IOperation operation = CreateOperation();
                    SetParentOperation(operation, this);
                    Interlocked.CompareExchange(ref _lazyOperationInterlocked, operation, s_unset);
                }

                return _lazyOperationInterlocked;
            }
        }

        public override ImmutableArray<IOperation> Arguments
        {
            get
            {
                if (_lazyArgumentsInterlocked.IsDefault)
                {
                    ImmutableArray<IOperation> arguments = CreateArguments();
                    SetParentOperation(arguments, this);
                    ImmutableInterlocked.InterlockedInitialize(ref _lazyArgumentsInterlocked, arguments);
                }

                return _lazyArgumentsInterlocked;
            }
        }
    }

#nullable enable
    internal sealed partial class ForEachLoopOperation
    {
        public override LoopKind LoopKind => LoopKind.ForEach;
    }

    internal sealed partial class ForLoopOperation
    {
        public override LoopKind LoopKind => LoopKind.For;
    }

    internal sealed partial class ForToLoopOperation
    {
        public override LoopKind LoopKind => LoopKind.ForTo;
    }

    internal sealed partial class WhileLoopOperation
    {
        public override IEnumerable<IOperation> Children
        {
            get
            {
                // PROTOTYPE(iop): Look at making the implementation of these better.
                if (_lazyChildren is null)
                {
                    var builder = ArrayBuilder<IOperation>.GetInstance(6);
                    if (ConditionIsTop) builder.AddIfNotNull(Condition);
                    builder.AddIfNotNull(Body);
                    if (!ConditionIsTop) builder.AddIfNotNull(Condition);
                    builder.AddIfNotNull(IgnoredCondition);
                    Interlocked.CompareExchange(ref _lazyChildren, builder.ToImmutableAndFree(), null);
                }

                return _lazyChildren;
            }
        }

        public override LoopKind LoopKind => LoopKind.While;
    }
#nullable disable

    internal sealed partial class ConstantPatternOperation : BaseConstantPatternOperation, IConstantPatternOperation
    {
        public ConstantPatternOperation(ITypeSymbol inputType, ITypeSymbol narrowedType, IOperation value, SemanticModel semanticModel, SyntaxNode syntax, bool isImplicit) :
            this(value, inputType, narrowedType, semanticModel, syntax, type: null, constantValue: null, isImplicit)
        { }
    }

    internal sealed partial class DeclarationPatternOperation : BasePatternOperation, IDeclarationPatternOperation
    {
        public DeclarationPatternOperation(
            ITypeSymbol inputType,
            ITypeSymbol narrowedType,
            ITypeSymbol matchedType,
            ISymbol declaredSymbol,
            bool matchesNull,
            SemanticModel semanticModel,
            SyntaxNode syntax,
            bool isImplicit)
            : this(matchedType, matchesNull, declaredSymbol, inputType, narrowedType, semanticModel, syntax, type: null, constantValue: null, isImplicit)
        { }
    }

    internal sealed partial class RecursivePatternOperation : BaseRecursivePatternOperation
    {
        public RecursivePatternOperation(
            ITypeSymbol inputType,
            ITypeSymbol narrowedType,
            ITypeSymbol matchedType,
            ISymbol deconstructSymbol,
            ImmutableArray<IPatternOperation> deconstructionSubpatterns,
            ImmutableArray<IPropertySubpatternOperation> propertySubpatterns,
            ISymbol declaredSymbol, SemanticModel semanticModel,
            SyntaxNode syntax,
            bool isImplicit) :
            this(matchedType, deconstructSymbol, deconstructionSubpatterns, propertySubpatterns, declaredSymbol, inputType, narrowedType, semanticModel, syntax, type: null, constantValue: null, isImplicit)
        { }
    }

    internal sealed partial class FlowCaptureReferenceOperation : OperationOld, IFlowCaptureReferenceOperation
    {
        public FlowCaptureReferenceOperation(int id, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue) :
            base(OperationKind.FlowCaptureReference, semanticModel: null, syntax: syntax, type: type, constantValue: constantValue, isImplicit: true)
        {
            Id = new CaptureId(id);
        }

        public FlowCaptureReferenceOperation(CaptureId id, SyntaxNode syntax, ITypeSymbol type, ConstantValue constantValue) :
            base(OperationKind.FlowCaptureReference, semanticModel: null, syntax: syntax, type: type, constantValue: constantValue, isImplicit: true)
        {
            Id = id;
        }
    }

    internal sealed partial class FlowCaptureOperation : OperationOld, IFlowCaptureOperation
    {
        public FlowCaptureOperation(int id, SyntaxNode syntax, IOperation value) :
            base(OperationKind.FlowCapture, semanticModel: null, syntax: syntax, type: null, constantValue: null, isImplicit: true)
        {
            Debug.Assert(value != null);
            Id = new CaptureId(id);
            Value = SetParentOperation(value, this);
        }

        public CaptureId Id { get; }
        public IOperation Value { get; }
        public override IEnumerable<IOperation> Children
        {
            get
            {
                yield return Value;
            }
        }

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitFlowCapture(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitFlowCapture(this, argument);
        }
    }

    internal sealed partial class IsNullOperation : OperationOld, IIsNullOperation
    {
        public IsNullOperation(SyntaxNode syntax, IOperation operand, ITypeSymbol type, ConstantValue constantValue) :
            base(OperationKind.IsNull, semanticModel: null, syntax: syntax, type: type, constantValue: constantValue, isImplicit: true)
        {
            Debug.Assert(operand != null);
            Operand = SetParentOperation(operand, this);
        }

        public IOperation Operand { get; }
        public override IEnumerable<IOperation> Children
        {
            get
            {
                yield return Operand;
            }
        }

        public override void Accept(OperationVisitor visitor)
        {
            visitor.VisitIsNull(this);
        }

        public override TResult Accept<TArgument, TResult>(OperationVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitIsNull(this, argument);
        }
    }

    internal sealed partial class CaughtExceptionOperation : OperationOld, ICaughtExceptionOperation
    {
        public CaughtExceptionOperation(SyntaxNode syntax, ITypeSymbol type) :
            this(semanticModel: null, syntax: syntax, type: type, constantValue: null, isImplicit: true)
        {
        }
    }

    internal sealed partial class StaticLocalInitializationSemaphoreOperation : OperationOld, IStaticLocalInitializationSemaphoreOperation
    {
        public StaticLocalInitializationSemaphoreOperation(ILocalSymbol local, SyntaxNode syntax, ITypeSymbol type) :
            base(OperationKind.StaticLocalInitializationSemaphore, semanticModel: null, syntax, type, constantValue: null, isImplicit: true)
        {
            Local = local;
        }
    }

    internal sealed partial class MethodBodyOperation : BaseMethodBodyOperation
    {
        public MethodBodyOperation(SemanticModel semanticModel, SyntaxNode syntax, IBlockOperation blockBody, IBlockOperation expressionBody) :
            this(blockBody, expressionBody, semanticModel, syntax, type: null, constantValue: null, isImplicit: false)
        { }
    }

    internal sealed partial class ConstructorBodyOperation : BaseConstructorBodyOperation
    {
        public ConstructorBodyOperation(ImmutableArray<ILocalSymbol> locals, SemanticModel semanticModel, SyntaxNode syntax,
                                        IOperation initializer, IBlockOperation blockBody, IBlockOperation expressionBody) :
            this(locals, initializer, blockBody, expressionBody, semanticModel, syntax, type: null, constantValue: null, isImplicit: false)
        { }
    }

    internal sealed partial class DiscardPatternOperation : BasePatternOperation, IDiscardPatternOperation
    {
        public DiscardPatternOperation(ITypeSymbol inputType, ITypeSymbol narrowedType, SemanticModel semanticModel, SyntaxNode syntax, bool isImplicit) :
            this(inputType, narrowedType, semanticModel, syntax, type: null, constantValue: null, isImplicit)
        { }

    }
}
