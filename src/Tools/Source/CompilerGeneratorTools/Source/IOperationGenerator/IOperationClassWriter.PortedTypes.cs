// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace IOperationGenerator
{
    // PROTOTYPE(iop): Delete this after migration
    internal sealed partial class IOperationClassWriter
    {
        private static readonly HashSet<string> UnportedTypes = new()
        {
            "IInvalidOperation",
            "IVariableDeclarationGroupOperation",
            "IDynamicObjectCreationOperation",
            "IDynamicMemberReferenceOperation",
            "IDynamicInvocationOperation",
            "IDynamicIndexerAccessOperation",
            "IVariableDeclaratorOperation",
            "IVariableDeclarationOperation",
            "IInterpolatedStringContentOperation",
            "IInterpolatedStringTextOperation",
            "IInterpolationOperation",
            "IPatternOperation",
            "IConstantPatternOperation",
            "IDeclarationPatternOperation",
            "IMethodBodyBaseOperation",
            "IMethodBodyOperation",
            "IConstructorBodyOperation",
            "IFlowCaptureOperation",
            "IFlowCaptureReferenceOperation",
            "IIsNullOperation",
            "ICaughtExceptionOperation",
            "IStaticLocalInitializationSemaphoreOperation",
            "IFlowAnonymousFunctionOperation",
            "IRecursivePatternOperation",
            "IDiscardPatternOperation",
            "INegatedPatternOperation",
            "IBinaryPatternOperation",
            "ITypePatternOperation",
            "IRelationalPatternOperation",
        };
    }
}
