﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.ForEachCast
{
    internal static class ForEachCastHelpers
    {
        public const string IsFixable = nameof(IsFixable);
    }

    internal abstract class AbstractForEachCastDiagnosticAnalyzer<TSyntaxKind, TForEachStatementSyntax> : AbstractBuiltInCodeStyleDiagnosticAnalyzer
        where TSyntaxKind : struct, Enum
        where TForEachStatementSyntax : SyntaxNode
    {
        public static readonly ImmutableDictionary<string, string?> s_isFixableProperties =
            ImmutableDictionary<string, string?>.Empty.Add(ForEachCastHelpers.IsFixable, ForEachCastHelpers.IsFixable);

        protected AbstractForEachCastDiagnosticAnalyzer(string language)
            : base(
                  diagnosticId: IDEDiagnosticIds.ForEachCastDiagnosticId,
                  EnforceOnBuildValues.ForEachCast,
                  option: null,
                  language: language,
                  title: new LocalizableResourceString(nameof(AnalyzersResources.Add_explicit_cast), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)),
                  messageFormat: new LocalizableResourceString(nameof(AnalyzersResources._0_statement_implicitly_converts_1_to_2_and_may_fail_at_runtime), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)))
        {
        }

        protected abstract ImmutableArray<TSyntaxKind> GetSyntaxKinds();
        protected abstract (CommonConversion conversion, ITypeSymbol? collectionElementType) GetForEachInfo(SemanticModel semanticModel, TForEachStatementSyntax node);

        public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
            => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

        protected override void InitializeWorker(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(context =>
            {
                var compilation = context.Compilation;
                var ienumerableType = compilation.IEnumerableType();
                var ienumerableOfTType = compilation.IEnumerableOfTType();
                if (ienumerableType != null && ienumerableOfTType != null)
                    context.RegisterSyntaxNodeAction(context => AnalyzeSyntax(context, ienumerableType, ienumerableOfTType), GetSyntaxKinds());
            });
        }

        protected void AnalyzeSyntax(
            SyntaxNodeAnalysisContext context, INamedTypeSymbol ienumerableType, INamedTypeSymbol ienumerableOfTType)
        {
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            if (context.Node is not TForEachStatementSyntax node)
                return;

            var option = context.GetOption(CodeStyleOptions2.ForEachExplicitCastInSource, semanticModel.Language);
            if (option.Value == ForEachExplicitCastInSourcePreference.Never)
                return;

            Contract.ThrowIfFalse(option.Value is ForEachExplicitCastInSourcePreference.Always or ForEachExplicitCastInSourcePreference.NonLegacy);

            if (semanticModel.GetOperation(node, cancellationToken) is not IForEachLoopOperation loopOperation)
                return;

            if (loopOperation.LoopControlVariable is not IVariableDeclaratorOperation variableDeclarator ||
                variableDeclarator.Symbol.Type is null)
            {
                return;
            }

            var collectionType = loopOperation.Collection.Type;
            var iterationType = loopOperation.Locals[0].Type;
            if (collectionType is null || iterationType is null)
                return;

            var (conversion, collectionElementType) = GetForEachInfo(semanticModel, node);

            // Don't bother checking conversions that are problematic for other reasons.
            if (!conversion.Exists)
                return;

            // If the conversion was implicit, then everything is ok.  Implicit conversions are safe and do not throw at runtime.
            if (conversion.IsImplicit)
                return;

            if (collectionElementType is null)
                return;

            // We had a conversion that was explicit.  These are potentially unsafe as they can throw at runtime.
            // Generally, we would like to notify the user about these.  However, we have different policies depending
            // on if we think this is a legacy API or not.  Legacy APIs are those built before generics, and thus often
            // have APIs that will just return `objects` and thus always need some sort of cast to get them to the right
            // type.  A good example of that is S.T.RegularExpressions.CaptureCollection.  Users will almost always
            // write this was `foreach (Capture capture in match.Captures)` and it would be annoying to force them to
            // change this.
            //
            // What we do want to warn on are things like: `foreach (IUnrelatedInterface iface in stronglyTypedCollection)`.
            //
            // So, to detect if we're in a legacy situation, we look for iterations that are returning an object-type
            // where the collection itself didn't implement `IEnumerable<T>` in some way.
            if (option.Value == ForEachExplicitCastInSourcePreference.NonLegacy)
            {
                if (IsLegacyAPI(ienumerableOfTType, collectionType, collectionElementType))
                    return;
            }

            // The user either always wants to write these casts explicitly, or they were calling a non-legacy API.
            // report the issue so they can insert the appropriate cast.

            // We can only fix this issue if the collection type implemented ienumerable.  Then we can add a .Cast
            // call to it.
            var isFixable = collectionType.AllInterfaces.Any(i => i.Equals(ienumerableType));

            var options = semanticModel.Compilation.Options;
            context.ReportDiagnostic(DiagnosticHelper.Create(
                Descriptor,
                node.GetFirstToken().GetLocation(),
                Descriptor.GetEffectiveSeverity(options),
                additionalLocations: null,
                properties: isFixable ? s_isFixableProperties : null,
                node.GetFirstToken().ToString(),
                collectionElementType.ToDisplayString(),
                iterationType.ToDisplayString()));
        }

        private static bool IsLegacyAPI(INamedTypeSymbol ienumerableOfTType, ITypeSymbol collectionType, ITypeSymbol collectionElementType)
        {
            return collectionElementType.SpecialType == SpecialType.System_Object &&
                !collectionType.AllInterfaces.Any(i => i.OriginalDefinition.Equals(ienumerableOfTType));
        }
    }
}
