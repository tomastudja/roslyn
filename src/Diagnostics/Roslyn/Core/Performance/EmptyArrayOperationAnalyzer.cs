﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Semantics;
using Roslyn.Diagnostics.Analyzers;

namespace Microsoft.CodeAnalysis.Performance
{
    /// <summary>Base type for an analyzer that looks for empty array allocations and recommends their replacement.</summary>
    public class EmptyArrayOperationAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>Diagnostic category "Performance".</summary>
        private const string PerformanceCategory = "Performance";

        /// <summary>The name of the array type.</summary>
        internal const string ArrayTypeName = "System.Array"; // using instead of GetSpecialType to make more testable

        /// <summary>The name of the Empty method on System.Array.</summary>
        internal const string ArrayEmptyMethodName = "Empty";

        private static LocalizableString localizableTitle = new LocalizableResourceString(nameof(RoslynDiagnosticsResources.UseArrayEmptyDescription), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources));
        private static LocalizableString localizableMessage = new LocalizableResourceString(nameof(RoslynDiagnosticsResources.UseArrayEmptyMessage), RoslynDiagnosticsResources.ResourceManager, typeof(RoslynDiagnosticsResources));
        
        /// <summary>The diagnostic descriptor used when Array.Empty should be used instead of a new array allocation.</summary>
        internal static readonly DiagnosticDescriptor UseArrayEmptyDescriptor = new DiagnosticDescriptor(
            RoslynDiagnosticIds.UseArrayEmptyRuleId,
            localizableTitle,
            localizableMessage,
            PerformanceCategory,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>Gets the set of supported diagnostic descriptors from this analyzer.</summary>
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get { return ImmutableArray.Create(UseArrayEmptyDescriptor); }
        }

        public sealed override void Initialize(AnalysisContext context)
        {
            // When compilation begins, check whether Array.Empty<T> is available.
            // Only if it is, register the syntax node action provided by the derived implementations.
            context.RegisterCompilationStartAction(ctx =>
            {
                INamedTypeSymbol typeSymbol = ctx.Compilation.GetTypeByMetadataName(ArrayTypeName);
                if (typeSymbol != null && typeSymbol.DeclaredAccessibility == Accessibility.Public)
                {
                    IMethodSymbol methodSymbol = typeSymbol.GetMembers(ArrayEmptyMethodName).FirstOrDefault() as IMethodSymbol;
                    if (methodSymbol != null && methodSymbol.DeclaredAccessibility == Accessibility.Public &&
                        methodSymbol.IsStatic && methodSymbol.Arity == 1 && methodSymbol.Parameters.Length == 0)
                    {
                        RegisterOperationAction(ctx);
                    }
                }
            });
        }

        /// <summary>Reports a diagnostic warning for an array creation that should be replaced.</summary>
        /// <param name="context">The context.</param>
        /// <param name="arrayCreationExpression">The array creation expression to be replaced.</param>
        internal void Report(OperationAnalysisContext context, SyntaxNode arrayCreationExpression)
        {
            context.ReportDiagnostic(Diagnostic.Create(UseArrayEmptyDescriptor, arrayCreationExpression.GetLocation()));
        }

        /// <summary>Called once at compilation start to register actions in the compilation context.</summary>
        /// <param name="context">The analysis context.</param>
        internal void RegisterOperationAction(CompilationStartAnalysisContext context)
        {
            context.RegisterOperationAction(
                (operationContext) =>
                    {
                        IArrayCreation arrayCreation = (IArrayCreation)operationContext.Operation;

                        // ToDo: Need to suppress analysis of array creation expressions within attribute applications.

                        // Detect array creation expression that have rank 1 and size 0. Such expressions
                        // can be replaced with Array.Empty<T>(), provided that the element type can be a generic type argument.

                        if (arrayCreation.DimensionSizes.Length == 1
                            //// Pointer types can't be generic type arguments.
                            && arrayCreation.ElementType.TypeKind != TypeKind.Pointer)
                        {
                            object arrayLength = arrayCreation.DimensionSizes[0].ConstantValue;
                            if (arrayLength != null &&
                                arrayLength is int &&
                                (int)arrayLength == 0)
                            {
                                Report(operationContext, arrayCreation.Syntax);
                            }
                        }
                    },
                OperationKind.ArrayCreation);
        }
    }
}