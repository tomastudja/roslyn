﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.LanguageService;
using Microsoft.CodeAnalysis.CSharp.Shared.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.UseCollectionInitializer;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.UseCollectionInitializer;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.UseCollectionExpression;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed partial class CSharpUseCollectionExpressionForBuilderDiagnosticAnalyzer
    : AbstractBuiltInCodeStyleDiagnosticAnalyzer
{
    private const string CreateBuilderName = nameof(ImmutableArray.CreateBuilder);
    private const string GetInstanceName = nameof(ArrayBuilder<int>.GetInstance);

    public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
        => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

    private static readonly DiagnosticDescriptor s_descriptor = CreateDescriptorWithId(
        IDEDiagnosticIds.UseCollectionExpressionForBuilderDiagnosticId,
        EnforceOnBuildValues.UseCollectionExpressionForBuilder,
        new LocalizableResourceString(nameof(AnalyzersResources.Simplify_collection_initialization), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)),
        new LocalizableResourceString(nameof(AnalyzersResources.Collection_initialization_can_be_simplified), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)),
        isUnnecessary: false);

    private static readonly DiagnosticDescriptor s_unnecessaryCodeDescriptor = CreateDescriptorWithId(
        IDEDiagnosticIds.UseCollectionExpressionForBuilderDiagnosticId,
        EnforceOnBuildValues.UseCollectionExpressionForBuilder,
        new LocalizableResourceString(nameof(AnalyzersResources.Simplify_collection_initialization), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)),
        new LocalizableResourceString(nameof(AnalyzersResources.Collection_initialization_can_be_simplified), AnalyzersResources.ResourceManager, typeof(AnalyzersResources)),
        isUnnecessary: true);

    public CSharpUseCollectionExpressionForBuilderDiagnosticAnalyzer()
        : base(ImmutableDictionary<DiagnosticDescriptor, IOption2>.Empty
                .Add(s_descriptor, CodeStyleOptions2.PreferCollectionExpression)
                .Add(s_unnecessaryCodeDescriptor, CodeStyleOptions2.PreferCollectionExpression))
    {
    }

    protected override void InitializeWorker(AnalysisContext context)
        => context.RegisterCompilationStartAction(context =>
        {
            var compilation = context.Compilation;
            if (!compilation.LanguageVersion().SupportsCollectionExpressions())
                return;

            // We wrap the SyntaxNodeAction within a CodeBlockStartAction, which allows us to
            // get callbacks for object creation expression nodes, but analyze nodes across the entire code block
            // and eventually report fading diagnostics with location outside this node.
            // Without the containing CodeBlockStartAction, our reported diagnostic would be classified
            // as a non-local diagnostic and would not participate in lightbulb for computing code fixes.
            context.RegisterCodeBlockStartAction<SyntaxKind>(context =>
            {
                context.RegisterSyntaxNodeAction(
                    context => AnalyzeInvocationExpression(context),
                    SyntaxKind.InvocationExpression);
            });
        });

    private static void AnalyzeInvocationExpression(
        SyntaxNodeAnalysisContext context)
    {
        var semanticModel = context.SemanticModel;
        var invocationExpression = (InvocationExpressionSyntax)context.Node;
        var cancellationToken = context.CancellationToken;

        // no point in analyzing if the option is off.
        var option = context.GetAnalyzerOptions().PreferCollectionExpression;
        if (!option.Value)
            return;

        if (AnalyzeInvocation(semanticModel, invocationExpression, cancellationToken) is not { } analysisResult)
            return;

        var locations = ImmutableArray.Create(invocationExpression.GetLocation());
        context.ReportDiagnostic(DiagnosticHelper.Create(
            s_descriptor,
            analysisResult.DiagnosticLocation,
            option.Notification.Severity,
            additionalLocations: locations,
            properties: null));

        FadeOurCode(context, analysisResult, locations);
    }

    private static void FadeOurCode(SyntaxNodeAnalysisContext context, AnalysisResult analysisResult, ImmutableArray<Location> locations)
    {
        var additionalUnnecessaryLocations = ImmutableArray.Create(
            analysisResult.LocalDeclarationStatement.GetLocation());

        context.ReportDiagnostic(DiagnosticHelper.CreateWithLocationTags(
            s_unnecessaryCodeDescriptor,
            additionalUnnecessaryLocations[0],
            ReportDiagnostic.Default,
            additionalLocations: locations,
            additionalUnnecessaryLocations: additionalUnnecessaryLocations,
            properties: null));

        foreach (var statementMatch in analysisResult.Matches)
        {
            additionalUnnecessaryLocations = UseCollectionInitializerHelpers.GetLocationsToFade(
                CSharpSyntaxFacts.Instance, statementMatch);
            if (additionalUnnecessaryLocations.IsDefaultOrEmpty)
                continue;

            // Report the diagnostic at the first unnecessary location. This is the location where the code fix
            // will be offered.
            context.ReportDiagnostic(DiagnosticHelper.CreateWithLocationTags(
                s_unnecessaryCodeDescriptor,
                additionalUnnecessaryLocations[0],
                ReportDiagnostic.Default,
                additionalLocations: locations,
                additionalUnnecessaryLocations: additionalUnnecessaryLocations,
                properties: null));
        }
    }

    public static AnalysisResult? AnalyzeInvocation(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocationExpression,
        CancellationToken cancellationToken)
    {
        // Looking for `XXX.CreateBuilder(...)`
        // Or `ArrayBuilder<>.GetInstance`
        if (invocationExpression.Expression is not MemberAccessExpressionSyntax(SyntaxKind.SimpleMemberAccessExpression) memberAccessExpression ||
            memberAccessExpression.Expression is not SimpleNameSyntax)
        {
            return null;
        }

        if (memberAccessExpression.Name.Identifier.ValueText is not CreateBuilderName and not GetInstanceName)
            return null;

        if (memberAccessExpression.Name.Identifier.ValueText == GetInstanceName &&
            memberAccessExpression.Expression is not GenericNameSyntax { Identifier.ValueText: nameof(ArrayBuilder<int>) })
        {
            return null;
        }

        var createSymbol = semanticModel.GetSymbolInfo(memberAccessExpression, cancellationToken).Symbol;
        if (createSymbol is not IMethodSymbol { IsStatic: true } createMethod)
            return null;

        var factoryType = semanticModel.GetSymbolInfo(memberAccessExpression.Expression, cancellationToken).Symbol as INamedTypeSymbol;
        if (factoryType is null)
            return null;

        // has to be the form: `Builder b = XXX.CreateBuilder();` or
        //                     `var _ = XXX.CreateBuilder(out var builder);
        if (invocationExpression.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax localDeclarationStatement } } declarator })
            return null;

        var arguments = invocationExpression.ArgumentList.Arguments;
        var argumentIndex = 0;
        if (createMethod.Parameters is [{ Name: "capacity" or "initialCapacity" }, ..])
            argumentIndex++;

        // If it's of the form `var x = XXX.CreateBuilder()` or `var x = XXX.CreateBuilder(capacity)` then we're looking
        // for usages of 'x'.  However, if it's `var x = XXX.CreateBuilder(out var y)` or `var x =
        // XXX.CreateBuilder(capacity, out var y)` then we're looking for usages of 'y'.
        var identifier =
            argumentIndex == arguments.Count ? declarator.Identifier :
            argumentIndex == arguments.Count - 1 && arguments[argumentIndex] is { RefKindKeyword.RawKind: (int)SyntaxKind.OutKeyword, Expression: DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax singleVariable } }
                ? singleVariable.Identifier
                : default;
        if (identifier == default)
            return null;

        var state = new UpdateExpressionState<ExpressionSyntax, StatementSyntax>(
            semanticModel,
            CSharpSyntaxFacts.Instance,
            invocationExpression,
            identifier,
            initializedSymbol: semanticModel.GetDeclaredSymbol(declarator, cancellationToken));

        using var _ = ArrayBuilder<Match<StatementSyntax>>.GetInstance(out var matches);

        // Now walk all the statement after the local declaration.
        using var enumerator = state.GetSubsequentStatements().GetEnumerator();
        while (enumerator.MoveNext())
        {
            var siblingStatement = enumerator.Current;

            // See if it's one of the statement forms that can become a collection expression element.
            var match = state.TryAnalyzeStatementForCollectionExpression(
                CSharpUpdateExpressionSyntaxHelper.Instance, siblingStatement, cancellationToken);
            if (match != null)
            {
                matches.Add(match.Value);
                continue;
            }

            // Had to have at least one match, otherwise do not convert this.
            if (matches.Count == 0)
                return null;

            // Now, look for something in the current statement indicating we're converting the builder to the final form.
            var creationExpression = TryGetCreationExpression(identifier, siblingStatement);
            if (creationExpression is null)
                return null;

            // Now, ensure that no subsequent statements reference the builder anymore.
            while (enumerator.MoveNext())
            {
                if (state.NodeContainsValuePatternOrReferencesInitializedSymbol(enumerator.Current, cancellationToken))
                    return null;
            }

            // Make sure we can actually use a collection expression in place of the created collection.
            if (!UseCollectionExpressionHelpers.CanReplaceWithCollectionExpression(
                    semanticModel, creationExpression, skipVerificationForReplacedNode: true, cancellationToken))
            {
                return null;
            }

            // Looks good.  We can convert this.
            return new(memberAccessExpression.Name.Identifier.GetLocation(), localDeclarationStatement, creationExpression, matches.ToImmutable());
        }

        return null;
    }

    private static InvocationExpressionSyntax? TryGetCreationExpression(SyntaxToken identifier, StatementSyntax statement)
    {
        // Look for code like `builder.ToImmutable()` in this statement.
        foreach (var identifierName in statement.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (identifierName.Identifier.ValueText == identifier.ValueText &&
                identifierName.Parent is MemberAccessExpressionSyntax(SyntaxKind.SimpleMemberAccessExpression) memberAccess &&
                memberAccess.Expression == identifierName &&
                memberAccess.Parent is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } invocationExpression &&
                memberAccess.Name.Identifier.ValueText
                    is nameof(ImmutableArray<int>.Builder.ToImmutable)
                    or nameof(ImmutableArray<int>.Builder.MoveToImmutable)
                    or nameof(ImmutableArray<int>.Builder.ToArray)
                    or nameof(ArrayBuilder<int>.ToImmutableAndClear)
                    or nameof(ArrayBuilder<int>.ToImmutableAndFree)
                    or nameof(ArrayBuilder<int>.ToArrayAndFree)
                    or nameof(Enumerable.ToList))
            {
                return invocationExpression;
            }
        }

        return null;
    }

    /// <summary>
    /// Result of analyzing an <c>XXX.CreateBuilder</c> expression to see if it can be replaced with a collection
    /// expression.
    /// </summary>
    /// <param name="DiagnosticLocation">The location to put the diagnostic to tell they user they can convert this
    /// expression.</param>
    /// <param name="LocalDeclarationStatement">The declaration of the builder.  Will be removed if the user chooses to make
    /// the change.</param>
    /// <param name="CreationExpression">The location of the code like <c>builder.ToImmutable()</c> that will actually be
    /// replaced with the collection expression</param>
    /// <param name="Matches">The statements that are mutating the builder that will be converted into elements in the final
    /// collection expression.</param>
    public readonly record struct AnalysisResult(
        Location DiagnosticLocation,
        LocalDeclarationStatementSyntax LocalDeclarationStatement,
        InvocationExpressionSyntax CreationExpression,
        ImmutableArray<Match<StatementSyntax>> Matches);
}
