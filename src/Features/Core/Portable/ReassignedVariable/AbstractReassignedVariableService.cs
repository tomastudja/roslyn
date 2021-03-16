﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Collections;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.ReassignedVariable
{
    internal abstract class AbstractReassignedVariableService<
        TParameterSyntax,
        TVariableDeclaratorSyntax,
        TVariableSyntax,
        TSingleVariableDesignation,
        TIdentifierNameSyntax>
        : IReassignedVariableService
        where TParameterSyntax : SyntaxNode
        where TVariableDeclaratorSyntax : SyntaxNode
        where TVariableSyntax : SyntaxNode
        where TSingleVariableDesignation : SyntaxNode
        where TIdentifierNameSyntax : SyntaxNode
    {
        protected abstract void AddVariables(TVariableDeclaratorSyntax declarator, ref TemporaryArray<TVariableSyntax> temporaryArray);
        protected abstract SyntaxNode GetParentScope(SyntaxNode localDeclaration);
        protected abstract SyntaxNode GetMemberBlock(SyntaxNode methodOrPropertyDeclaration);

        protected abstract SyntaxToken GetIdentifierOfVariable(TVariableSyntax variable);
        protected abstract SyntaxToken GetIdentifierOfSingleVariableDesignation(TSingleVariableDesignation variable);

        public async Task<ImmutableArray<TextSpan>> GetReassignedVariablesAsync(
            Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var semanticFacts = document.GetRequiredLanguageService<ISemanticFactsService>();
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();

            var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            using var _1 = PooledDictionary<ISymbol, bool>.GetInstance(out var symbolToIsReassigned);
            using var _2 = ArrayBuilder<TextSpan>.GetInstance(out var result);
            using var _3 = ArrayBuilder<SyntaxNode>.GetInstance(out var stack);

            // First, walk through all identifiers in the provided span.  If they refer to a local/param, then determine
            // if that's a local/param that's reassigned or not.  Note that the local/param they refer to may not be in the
            // span being asked about.
            stack.Add(root.FindNode(span));

            while (stack.Count > 0)
            {
                var current = stack.Last();
                stack.RemoveLast();

                if (current.Span.IntersectsWith(span))
                {
                    ProcessNode(current);

                    foreach (var child in current.ChildNodesAndTokens())
                    {
                        if (child.IsNode)
                            stack.Add(child.AsNode()!);
                    }
                }
            }

            result.RemoveDuplicates();
            return result.ToImmutable();

            void ProcessNode(SyntaxNode node)
            {
                switch (node)
                {
                    case TIdentifierNameSyntax identifier:
                        ProcessIdentifier(identifier);
                        break;
                    case TParameterSyntax parameter:
                        ProcessParameter(parameter);
                        break;
                    case TVariableDeclaratorSyntax variable:
                        ProcessVariable(variable);
                        break;
                    case TSingleVariableDesignation designation:
                        ProcessSingleVariableDesignation(designation);
                        break;
                }
            }

            void ProcessIdentifier(TIdentifierNameSyntax identifier)
            {
                // Don't bother even looking at identifiers that aren't standalone (i.e. they're not on the left of some
                // expression).  These could not refer to locals or fields.
                if (syntaxFacts.GetStandaloneExpression(identifier) != identifier)
                    return;

                var symbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
                if (IsSymbolReassigned(symbol))
                    result.Add(identifier.Span);
            }

            void ProcessParameter(TParameterSyntax parameterSyntax)
            {
                var parameter = semanticModel.GetDeclaredSymbol(parameterSyntax, cancellationToken) as IParameterSymbol;
                if (IsSymbolReassigned(parameter))
                    result.Add(syntaxFacts.GetIdentifierOfParameter(parameterSyntax).Span);
            }

            void ProcessVariable(TVariableDeclaratorSyntax declarator)
            {
                using var variables = TemporaryArray<TVariableSyntax>.Empty;
                AddVariables(declarator, ref variables.AsRef());

                foreach (var variable in variables)
                {
                    var local = semanticModel.GetDeclaredSymbol(variable, cancellationToken) as ILocalSymbol;
                    if (IsSymbolReassigned(local))
                        result.Add(GetIdentifierOfVariable(variable).Span);
                }
            }

            void ProcessSingleVariableDesignation(TSingleVariableDesignation designation)
            {
                var local = semanticModel.GetDeclaredSymbol(designation, cancellationToken) as ILocalSymbol;
                if (IsSymbolReassigned(local))
                    result.Add(GetIdentifierOfSingleVariableDesignation(designation).Span);
            }

            bool IsSymbolReassigned([NotNullWhen(true)] ISymbol? symbol)
            {
                // Note: we don't need to test range variables, and they are never reassignable.
                if (symbol is not IParameterSymbol and not ILocalSymbol)
                    return false;

                if (!symbolToIsReassigned.TryGetValue(symbol, out var reassignedResult))
                {
                    reassignedResult = ComputeIsAssigned(symbol);
                    symbolToIsReassigned[symbol] = reassignedResult;
                }

                return reassignedResult;
            }

            bool ComputeIsAssigned(ISymbol symbol)
            {
                return symbol switch
                {
                    IParameterSymbol parameter => ComputeParameterIsAssigned(parameter),
                    ILocalSymbol local => ComputeLocalIsAssigned(local),
                    _ => throw ExceptionUtilities.UnexpectedValue(symbol.Kind),
                };
            }

            bool ComputeParameterIsAssigned(IParameterSymbol parameter)
            {
                if (parameter.Locations.Length == 0)
                    return false;

                var parameterLocation = parameter.Locations[0];
                if (parameterLocation.SourceTree != semanticModel.SyntaxTree)
                    return false;

                var methodOrProperty = parameter.ContainingSymbol;

                if (methodOrProperty is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet } method)
                    methodOrProperty = method.AssociatedSymbol as IPropertySymbol;

                if (methodOrProperty is not IMethodSymbol and not IPropertySymbol)
                    return false;

                if (methodOrProperty.DeclaringSyntaxReferences.Length == 0)
                    return false;

                var methodOrPropertyDeclaration = methodOrProperty.DeclaringSyntaxReferences.First().GetSyntax(cancellationToken);

                // Potentially a reference to a parameter in another file.
                if (methodOrPropertyDeclaration.SyntaxTree != semanticModel.SyntaxTree)
                    return false;

                return AnalyzePotentialMatches(parameter, parameterLocation.SourceSpan, GetMemberBlock(methodOrPropertyDeclaration));
            }

            bool ComputeLocalIsAssigned(ILocalSymbol local)
            {
                if (local.DeclaringSyntaxReferences.Length == 0)
                    return false;

                var localDeclaration = local.DeclaringSyntaxReferences.First().GetSyntax(cancellationToken);
                if (localDeclaration.SyntaxTree != semanticModel.SyntaxTree)
                {
                    Contract.Fail("Local did not come from same file that we were analyzing?");
                    return false;
                }

                // Get the scope the local is declared in.
                var parentScope = GetParentScope(localDeclaration);
                return AnalyzePotentialMatches(local, localDeclaration.Span, parentScope);
            }

            bool AnalyzePotentialMatches(ISymbol localOrParameter, TextSpan localOrParameterDeclarationSpan, SyntaxNode parentScope)
            {
                // Now, walk the scope, looking for all usages of the local.  See if any are a reassignment.
                using var _ = ArrayBuilder<SyntaxNode>.GetInstance(out var stack);
                stack.Push(parentScope);

                while (stack.Count != 0)
                {
                    var current = stack.Last();
                    stack.RemoveLast();

                    foreach (var child in current.ChildNodesAndTokens())
                    {
                        if (child.IsNode)
                            stack.Add(child.AsNode()!);
                    }

                    // Ignore any nodes before the decl.
                    if (current.SpanStart <= localOrParameterDeclarationSpan.Start)
                        continue;

                    // Only examine identifiers.
                    if (current is not TIdentifierNameSyntax id)
                        continue;

                    // Ignore identifiers that don't match the local name.
                    var idToken = syntaxFacts.GetIdentifierOfSimpleName(id);
                    if (!syntaxFacts.StringComparer.Equals(idToken.ValueText, localOrParameter.Name))
                        continue;

                    // Ignore identifiers that bind to another symbol.
                    var symbol = semanticModel.GetSymbolInfo(id, cancellationToken).Symbol;
                    if (!AreEquivalent(localOrParameter, symbol))
                        continue;

                    // Ok, we have a reference to the local.  See if it was assigned on entry.  If not, we don't care about
                    // this reference. As an assignment here doesn't mean it was reassigned.
                    var dataFlow = semanticModel.AnalyzeDataFlow(id);
                    if (!DefinitelyAssignedOnEntry(dataFlow, localOrParameter))
                        continue;

                    // This was a variable that was already assigned prior to this location.  See if this location is
                    // considered a write.
                    if (semanticFacts.IsWrittenTo(semanticModel, id, cancellationToken))
                        return true;
                }

                return false;
            }

            bool AreEquivalent(ISymbol localOrParameter, ISymbol? symbol)
            {
                if (symbol == null)
                    return false;

                if (localOrParameter.Equals(symbol))
                    return true;

                if (localOrParameter.Kind != symbol.Kind)
                    return false;

                // Special case for property parameters.  When we bind to references, we'll bind to the parameters on
                // the accessor methods.  We need to map these back to the property parameter to see if we have a hit.
                if (localOrParameter is IParameterSymbol { ContainingSymbol: IPropertySymbol property } parameter)
                {
                    var getParameter = property.GetMethod?.Parameters[parameter.Ordinal];
                    var setParameter = property.SetMethod?.Parameters[parameter.Ordinal];
                    return Equals(getParameter, symbol) || Equals(setParameter, symbol);
                }

                return false;
            }

            bool DefinitelyAssignedOnEntry(DataFlowAnalysis? analysis, ISymbol? localOrParameter)
            {
                if (analysis == null)
                    return false;

                if (localOrParameter == null)
                    return false;

                if (analysis.DefinitelyAssignedOnEntry.Contains(localOrParameter))
                    return true;

                // Special case for property parameters.  When we bind to references, we'll bind to the parameters on
                // the accessor methods.  We need to map these back to the property parameter to see if we have a hit.
                if (localOrParameter is IParameterSymbol { ContainingSymbol: IPropertySymbol property } parameter)
                {
                    var getParameter = property.GetMethod?.Parameters[parameter.Ordinal];
                    var setParameter = property.SetMethod?.Parameters[parameter.Ordinal];
                    return DefinitelyAssignedOnEntry(analysis, getParameter) ||
                           DefinitelyAssignedOnEntry(analysis, setParameter);
                }

                return false;
            }
        }
    }
}
