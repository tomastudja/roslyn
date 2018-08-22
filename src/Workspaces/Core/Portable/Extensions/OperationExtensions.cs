﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    internal static partial class OperationExtensions
    {
        /// <summary>
        /// Returns the <see cref="ValueUsageInfo"/> for the given operation.
        /// This extension can be removed once https://github.com/dotnet/roslyn/issues/25057 is implemented.
        /// </summary>
        public static ValueUsageInfo GetValueUsageInfo(this IOperation operation)
        {
            /*
            |    code         | Read | Write | ReadableRef | WritableRef |
            | nameof(x)       |      |       |             |             |
            | x.Prop = 1      |      |  ✔️   |             |             |
            | x.Prop += 1     |  ✔️  |  ✔️   |             |             |
            | x.Prop++        |  ✔️  |  ✔️   |             |             |
            | Foo(x.Prop)     |  ✔️  |       |             |             |
            | Foo(x.Prop)*    |      |       |     ✔️      |             |
            | Foo(out x.Prop) |      |       |             |     ✔️      |
            | Foo(ref x.Prop) |      |       |     ✔️      |     ✔️      |

            * where void Foo(in T v)
            */

            if (operation.Parent is IAssignmentOperation assignmentOperation &&
                assignmentOperation.Target == operation)
            {
                return operation.Parent.Kind == OperationKind.CompoundAssignment
                    ?  ValueUsageInfo.ReadWrite
                    : ValueUsageInfo.Write;
            }
            else if (operation.Parent is IIncrementOrDecrementOperation)
            {
                return ValueUsageInfo.ReadWrite;
            }
            else if (operation.Parent is IParenthesizedOperation parenthesizedOperation)
            {
                return parenthesizedOperation.GetValueUsageInfo();
            }
            else if (operation.Parent is INameOfOperation ||
                operation.Parent is ITypeOfOperation ||
                operation.Parent is ISizeOfOperation)
            {
                return ValueUsageInfo.None;
            }
            else if (operation.Parent is IArgumentOperation argumentOperation)
            {
                switch (argumentOperation.Parameter.RefKind)
                {
                    case RefKind.RefReadOnly:
                        return ValueUsageInfo.ReadableRef;

                    case RefKind.Out:
                        return ValueUsageInfo.WritableRef;

                    case RefKind.Ref:
                        return ValueUsageInfo.ReadableWritableRef;

                    default:
                        return ValueUsageInfo.Read;
                }
            }
            else if (IsInLeftOfDeconstructionAssignment(operation))
            {
                return ValueUsageInfo.Write;
            }

            return ValueUsageInfo.Read;
        }

        private static bool IsInLeftOfDeconstructionAssignment(IOperation operation)
        {
            var previousOperation = operation;
            operation = operation.Parent;

            while (operation != null)
            {
                switch (operation.Kind)
                {
                    case OperationKind.DeconstructionAssignment:
                        var deconstructionAssignment = (IDeconstructionAssignmentOperation)operation;
                        return deconstructionAssignment.Target == previousOperation;

                    case OperationKind.Tuple:
                    case OperationKind.Conversion:
                    case OperationKind.Parenthesized:
                        previousOperation = operation;
                        operation = operation.Parent;
                        continue;

                    default:
                        return false;
                }
            }

            return false;
        }
    }
}
