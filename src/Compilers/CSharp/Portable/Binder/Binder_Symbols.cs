﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.RuntimeMembers;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal partial class Binder
    {
        /// <summary>
        /// Binds the type for the syntax taking into account possibility of "var" type.
        /// </summary>
        /// <param name="syntax">Type syntax to bind.</param>
        /// <param name="diagnostics">Diagnostics.</param>
        /// <param name="isVar">
        /// Set to false if syntax binds to a type in the current context and true if
        /// syntax is "var" and it binds to "var" keyword in the current context.
        /// </param>
        /// <returns>
        /// Bound type if syntax binds to a type in the current context and
        /// null if syntax binds to "var" keyword in the current context.
        /// </returns>
        internal TypeSymbolWithAnnotations BindTypeOrVarKeyword(TypeSyntax syntax, DiagnosticBag diagnostics, out bool isVar)
        {
            var symbol = BindTypeOrAliasOrVarKeyword(syntax, diagnostics, out isVar);
            Debug.Assert(isVar == symbol.IsDefault);
            return isVar ? default : UnwrapAlias(symbol, diagnostics, syntax).Type;
        }

        /// <summary>
        /// Binds the type for the syntax taking into account possibility of "unmanaged" type.
        /// </summary>
        /// <param name="syntax">Type syntax to bind.</param>
        /// <param name="diagnostics">Diagnostics.</param>
        /// <param name="isUnmanaged">
        /// Set to false if syntax binds to a type in the current context and true if
        /// syntax is "unmanaged" and it binds to "unmanaged" keyword in the current context.
        /// </param>
        /// <returns>
        /// Bound type if syntax binds to a type in the current context and
        /// null if syntax binds to "unmanaged" keyword in the current context.
        /// </returns>
        internal TypeSymbolWithAnnotations BindTypeOrUnmanagedKeyword(TypeSyntax syntax, DiagnosticBag diagnostics, out bool isUnmanaged)
        {
            var symbol = BindTypeOrAliasOrUnmanagedKeyword(syntax, diagnostics, out isUnmanaged);
            Debug.Assert(isUnmanaged == symbol.IsDefault);
            return isUnmanaged ? default : UnwrapAlias(symbol, diagnostics, syntax).Type;
        }

        /// <summary>
        /// Binds the type for the syntax taking into account possibility of "var" type.
        /// </summary>
        /// <param name="syntax">Type syntax to bind.</param>
        /// <param name="diagnostics">Diagnostics.</param>
        /// <param name="isVar">
        /// Set to false if syntax binds to a type in the current context and true if
        /// syntax is "var" and it binds to "var" keyword in the current context.
        /// </param>
        /// <param name="alias">Alias symbol if syntax binds to an alias.</param>
        /// <returns>
        /// Bound type if syntax binds to a type in the current context and
        /// null if syntax binds to "var" keyword in the current context.
        /// </returns>
        internal TypeSymbolWithAnnotations BindTypeOrVarKeyword(TypeSyntax syntax, DiagnosticBag diagnostics, out bool isVar, out AliasSymbol alias)
        {
            var symbol = BindTypeOrAliasOrVarKeyword(syntax, diagnostics, out isVar);
            Debug.Assert(isVar == symbol.IsDefault);
            if (isVar)
            {
                alias = null;
                return default;
            }
            else
            {
                return UnwrapAlias(symbol, out alias, diagnostics, syntax).Type;
            }
        }

        /// <summary>
        /// Binds the type for the syntax taking into account possibility of "var" type.
        /// If the syntax binds to an alias symbol to a type, it returns the alias symbol.
        /// </summary>
        /// <param name="syntax">Type syntax to bind.</param>
        /// <param name="diagnostics">Diagnostics.</param>
        /// <param name="isVar">
        /// Set to false if syntax binds to a type or alias to a type in the current context and true if
        /// syntax is "var" and it binds to "var" keyword in the current context.
        /// </param>
        /// <returns>
        /// Bound type or alias if syntax binds to a type or alias to a type in the current context and
        /// null if syntax binds to "var" keyword in the current context.
        /// </returns>
        private NamespaceOrTypeOrAliasSymbolWithAnnotations BindTypeOrAliasOrVarKeyword(TypeSyntax syntax, DiagnosticBag diagnostics, out bool isVar)
        {
            if (syntax.IsVar)
            {
                var symbol = BindTypeOrAliasOrKeyword((IdentifierNameSyntax)syntax, diagnostics, out isVar);

                if (isVar)
                {
                    CheckFeatureAvailability(syntax, MessageID.IDS_FeatureImplicitLocal, diagnostics);
                }

                return symbol;
            }
            else
            {
                isVar = false;
                return BindTypeOrAlias(syntax, diagnostics, basesBeingResolved: null);
            }
        }

        private NamespaceOrTypeOrAliasSymbolWithAnnotations BindTypeOrAliasOrUnmanagedKeyword(TypeSyntax syntax, DiagnosticBag diagnostics, out bool isUnmanaged)
        {
            if (syntax.IsUnmanaged)
            {
                var symbol = BindTypeOrAliasOrKeyword((IdentifierNameSyntax)syntax, diagnostics, out isUnmanaged);

                if (isUnmanaged)
                {
                    CheckFeatureAvailability(syntax, MessageID.IDS_FeatureUnmanagedGenericTypeConstraint, diagnostics);
                }

                return symbol;
            }
            else
            {
                isUnmanaged = false;
                return BindTypeOrAlias(syntax, diagnostics, basesBeingResolved: null);
            }
        }

        /// <summary>
        /// Binds the type for the syntax taking into account possibility of the type being a keyword.
        /// If the syntax binds to an alias symbol to a type, it returns the alias symbol.
        /// PREREQUISITE: syntax should be checked to match the keyword, like <see cref="TypeSyntax.IsVar"/> or <see cref="TypeSyntax.IsUnmanaged"/>.
        /// Otherwise, call <see cref="Binder.BindTypeOrAlias(ExpressionSyntax, DiagnosticBag, ConsList{TypeSymbol})"/> instead.
        /// </summary>
        private NamespaceOrTypeOrAliasSymbolWithAnnotations BindTypeOrAliasOrKeyword(IdentifierNameSyntax syntax, DiagnosticBag diagnostics, out bool isKeyword)
        {
            return BindTypeOrAliasOrKeyword(((IdentifierNameSyntax)syntax).Identifier, syntax, diagnostics, out isKeyword);
        }

        private NamespaceOrTypeOrAliasSymbolWithAnnotations BindTypeOrAliasOrKeyword(SyntaxToken identifier, SyntaxNode syntax, DiagnosticBag diagnostics, out bool isKeyword)
        {
            // Keywords can only be IdentifierNameSyntax
            var identifierValueText = identifier.ValueText;
            Symbol symbol = null;

            // Perform name lookup without generating diagnostics as it could possibly be a keyword in the current context.
            var lookupResult = LookupResult.GetInstance();
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            this.LookupSymbolsInternal(lookupResult, identifierValueText, arity: 0, useSiteDiagnostics: ref useSiteDiagnostics, basesBeingResolved: null, options: LookupOptions.NamespacesOrTypesOnly, diagnose: false);

            // We have following possible cases for lookup:

            //  1) LookupResultKind.Empty: must be a keyword

            //  2) LookupResultKind.Viable:
            //      a) Single viable result that corresponds to 1) a non-error type: cannot be a keyword
            //                                                  2) an error type: must be a keyword
            //      b) Single viable result that corresponds to namespace: must be a keyword
            //      c) Multi viable result (ambiguous result), we must return an error type: cannot be a keyword

            // 3) Non viable, non empty lookup result: must be a keyword

            // BREAKING CHANGE:     Case (2)(c) is a breaking change from the native compiler.
            // BREAKING CHANGE:     Native compiler interprets lookup with ambiguous result to correspond to bind
            // BREAKING CHANGE:     to "var" keyword (isVar = true), rather than reporting an error.
            // BREAKING CHANGE:     See test SemanticErrorTests.ErrorMeansSuccess_var() for an example.

            switch (lookupResult.Kind)
            {
                case LookupResultKind.Empty:
                    // Case (1)
                    isKeyword = true;
                    symbol = null;
                    break;

                case LookupResultKind.Viable:
                    // Case (2)
                    DiagnosticBag resultDiagnostics = DiagnosticBag.GetInstance();
                    bool wasError;
                    symbol = ResultSymbol(
                        lookupResult,
                        identifierValueText,
                        arity: 0,
                        where: syntax,
                        diagnostics: resultDiagnostics,
                        suppressUseSiteDiagnostics: false,
                        wasError: out wasError);

                    // Here, we're mimicking behavior of dev10.  If the identifier fails to bind
                    // as a type, even if the reason is (e.g.) a type/alias conflict, then treat
                    // it as the contextual keyword.
                    if (wasError && lookupResult.IsSingleViable)
                    {
                        // NOTE: don't report diagnostics - we're not going to use the lookup result.
                        resultDiagnostics.Free();
                        // Case (2)(a)(2)
                        goto default;
                    }

                    diagnostics.AddRange(resultDiagnostics);
                    resultDiagnostics.Free();

                    if (lookupResult.IsSingleViable)
                    {
                        var type = UnwrapAlias(symbol, diagnostics, syntax) as TypeSymbol;

                        if ((object)type != null)
                        {
                            // Case (2)(a)(1)
                            isKeyword = false;
                        }
                        else
                        {
                            // Case (2)(b)
                            Debug.Assert(UnwrapAliasNoDiagnostics(symbol) is NamespaceSymbol);
                            isKeyword = true;
                            symbol = null;
                        }
                    }
                    else
                    {
                        // Case (2)(c)
                        isKeyword = false;
                    }

                    break;

                default:
                    // Case (3)
                    isKeyword = true;
                    symbol = null;
                    break;
            }

            lookupResult.Free();

            return NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(IsNullableEnabled(identifier), symbol);
        }

        // Binds the given expression syntax as Type.
        // If the resulting symbol is an Alias to a Type, it unwraps the alias
        // and returns it's target type.
        internal TypeSymbolWithAnnotations BindType(ExpressionSyntax syntax, DiagnosticBag diagnostics, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            var symbol = BindTypeOrAlias(syntax, diagnostics, basesBeingResolved);
            return UnwrapAlias(symbol, diagnostics, syntax, basesBeingResolved).Type;
        }

        // Binds the given expression syntax as Type.
        // If the resulting symbol is an Alias to a Type, it stores the AliasSymbol in
        // the alias parameter, unwraps the alias and returns it's target type.
        internal TypeSymbolWithAnnotations BindType(ExpressionSyntax syntax, DiagnosticBag diagnostics, out AliasSymbol alias, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            var symbol = BindTypeOrAlias(syntax, diagnostics, basesBeingResolved);
            return UnwrapAlias(symbol, out alias, diagnostics, syntax, basesBeingResolved).Type;
        }

        // Binds the given expression syntax as Type or an Alias to Type
        // and returns the resultant symbol.
        // NOTE: This method doesn't unwrap aliases.
        internal NamespaceOrTypeOrAliasSymbolWithAnnotations BindTypeOrAlias(ExpressionSyntax syntax, DiagnosticBag diagnostics, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            Debug.Assert(diagnostics != null);

            var symbol = BindNamespaceOrTypeOrAliasSymbol(syntax, diagnostics, basesBeingResolved, basesBeingResolved != null);

            // symbol must be a TypeSymbol or an Alias to a TypeSymbol
            if (symbol.IsType ||
                (symbol.IsAlias && UnwrapAliasNoDiagnostics(symbol.Symbol, basesBeingResolved) is TypeSymbol))
            {
                if (symbol.IsType)
                {
                    // Obsolete alias targets are reported in UnwrapAlias, but if it was a type (not an
                    // alias to a type) we report the obsolete type here.
                    symbol.Type.ReportDiagnosticsIfObsolete(this, syntax, diagnostics);
                }

                return symbol;
            }

            var diagnosticInfo = diagnostics.Add(ErrorCode.ERR_BadSKknown, syntax.Location, syntax, symbol.Symbol.GetKindText(), MessageID.IDS_SK_TYPE.Localize());
            return TypeSymbolWithAnnotations.Create(new ExtendedErrorTypeSymbol(GetContainingNamespaceOrType(symbol.Symbol), symbol.Symbol, LookupResultKind.NotATypeOrNamespace, diagnosticInfo));
        }

        /// <summary>
        /// The immediately containing namespace or named type, or the global
        /// namespace if containing symbol is neither a namespace or named type.
        /// </summary>
        private NamespaceOrTypeSymbol GetContainingNamespaceOrType(Symbol symbol)
        {
            return symbol.ContainingNamespaceOrType() ?? this.Compilation.Assembly.GlobalNamespace;
        }

        internal Symbol BindNamespaceAliasSymbol(IdentifierNameSyntax node, DiagnosticBag diagnostics)
        {
            if (node.Identifier.Kind() == SyntaxKind.GlobalKeyword)
            {
                return this.Compilation.GlobalNamespaceAlias;
            }
            else
            {
                bool wasError;
                var plainName = node.Identifier.ValueText;
                var result = LookupResult.GetInstance();
                HashSet<DiagnosticInfo> useSiteDiagnostics = null;
                this.LookupSymbolsWithFallback(result, plainName, 0, ref useSiteDiagnostics, null, LookupOptions.NamespaceAliasesOnly);
                diagnostics.Add(node, useSiteDiagnostics);

                Symbol bindingResult = ResultSymbol(result, plainName, 0, node, diagnostics, false, out wasError, options: LookupOptions.NamespaceAliasesOnly);
                result.Free();

                return bindingResult;
            }
        }

        internal NamespaceOrTypeOrAliasSymbolWithAnnotations BindNamespaceOrTypeSymbol(ExpressionSyntax syntax, DiagnosticBag diagnostics, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            return BindNamespaceOrTypeSymbol(syntax, diagnostics, basesBeingResolved, basesBeingResolved != null);
        }

        internal NamespaceOrTypeOrAliasSymbolWithAnnotations BindNamespaceOrTypeSymbol(ExpressionSyntax syntax, DiagnosticBag diagnostics, ConsList<TypeSymbol> basesBeingResolved, bool suppressUseSiteDiagnostics)
        {
            var result = BindNamespaceOrTypeOrAliasSymbol(syntax, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics);
            Debug.Assert(!result.IsDefault);

            return UnwrapAlias(result, diagnostics, syntax, basesBeingResolved);
        }

        internal NamespaceOrTypeOrAliasSymbolWithAnnotations BindNamespaceOrTypeOrAliasSymbol(ExpressionSyntax syntax, DiagnosticBag diagnostics, ConsList<TypeSymbol> basesBeingResolved, bool suppressUseSiteDiagnostics)
        {
            switch (syntax.Kind())
            {
                case SyntaxKind.NullableType:
                    {
                        var nullableSyntax = (NullableTypeSyntax)syntax;
                        TypeSyntax typeArgumentSyntax = nullableSyntax.ElementType;
                        TypeSymbolWithAnnotations typeArgument = BindType(typeArgumentSyntax, diagnostics, basesBeingResolved);
                        TypeSymbolWithAnnotations constructedType = typeArgument.SetIsAnnotated(Compilation);

                        reportNullableReferenceTypesIfNeeded(nullableSyntax.QuestionToken, typeArgument);

                        if (!ShouldCheckConstraints)
                        {
                            diagnostics.Add(new LazyUseSiteDiagnosticsInfoForNullableType(constructedType), syntax.GetLocation());
                        }
                        else if (constructedType.IsNullableType())
                        {
                            ReportUseSiteDiagnostics(constructedType.TypeSymbol.OriginalDefinition, diagnostics, syntax);
                            var type = (NamedTypeSymbol)constructedType.TypeSymbol;
                            var location = syntax.Location;
                            var conversions = this.Conversions.WithNullability(includeNullability: true);
                            type.CheckConstraints(this.Compilation, conversions, location, diagnostics);
                        }
                        else if (constructedType.TypeSymbol.IsUnconstrainedTypeParameter())
                        {
                            diagnostics.Add(ErrorCode.ERR_NullableUnconstrainedTypeParameter, syntax.Location);
                        }

                        return constructedType;
                    }

                case SyntaxKind.PredefinedType:
                    {
                        var predefinedType = (PredefinedTypeSyntax)syntax;
                        var type = BindPredefinedTypeSymbol(predefinedType, diagnostics);
                        return TypeSymbolWithAnnotations.Create(IsNullableEnabled(predefinedType.Keyword), type);
                    }

                case SyntaxKind.IdentifierName:
                    return BindNonGenericSimpleNamespaceOrTypeOrAliasSymbol((IdentifierNameSyntax)syntax, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics, qualifierOpt: null);

                case SyntaxKind.GenericName:
                    return BindGenericSimpleNamespaceOrTypeOrAliasSymbol((GenericNameSyntax)syntax, diagnostics, basesBeingResolved, qualifierOpt: null);

                case SyntaxKind.AliasQualifiedName:
                    {
                        var node = (AliasQualifiedNameSyntax)syntax;
                        var bindingResult = BindNamespaceAliasSymbol(node.Alias, diagnostics);
                        var alias = bindingResult as AliasSymbol;
                        NamespaceOrTypeSymbol left = ((object)alias != null) ? alias.Target : (NamespaceOrTypeSymbol)bindingResult;

                        if (left.Kind == SymbolKind.NamedType)
                        {
                            return TypeSymbolWithAnnotations.Create(new ExtendedErrorTypeSymbol(left, LookupResultKind.NotATypeOrNamespace, diagnostics.Add(ErrorCode.ERR_ColColWithTypeAlias, node.Alias.Location, node.Alias.Identifier.Text)));
                        }

                        return this.BindSimpleNamespaceOrTypeOrAliasSymbol(node.Name, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics, left);
                    }

                case SyntaxKind.QualifiedName:
                    {
                        var node = (QualifiedNameSyntax)syntax;
                        return BindQualifiedName(node.Left, node.Right, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics);
                    }

                case SyntaxKind.SimpleMemberAccessExpression:
                    {
                        var node = (MemberAccessExpressionSyntax)syntax;
                        return BindQualifiedName(node.Expression, node.Name, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics);
                    }

                case SyntaxKind.ArrayType:
                    {
                        var node = (ArrayTypeSyntax)syntax;
                        TypeSymbolWithAnnotations type = BindType(node.ElementType, diagnostics, basesBeingResolved);
                        if (type.IsStatic)
                        {
                            // CS0719: '{0}': array elements cannot be of static type
                            Error(diagnostics, ErrorCode.ERR_ArrayOfStaticClass, node.ElementType, type.TypeSymbol);
                        }

                        if (ShouldCheckConstraints)
                        {
                            if (type.IsRestrictedType())
                            {
                                // CS0611: Array elements cannot be of type '{0}'
                                Error(diagnostics, ErrorCode.ERR_ArrayElementCantBeRefAny, node.ElementType, type.TypeSymbol);
                            }
                        }
                        else
                        {
                            diagnostics.Add(new LazyArrayElementCantBeRefAnyDiagnosticInfo(type), node.ElementType.GetLocation());
                        }

                        for (int i = node.RankSpecifiers.Count - 1; i >= 0; i--)
                        {
                            var a = node.RankSpecifiers[i];
                            var array = ArrayTypeSymbol.CreateCSharpArray(this.Compilation.Assembly, type, a.Rank);

                            if (a.QuestionToken.IsKind(SyntaxKind.QuestionToken))
                            {
                                type = TypeSymbolWithAnnotations.Create(array, NullableAnnotation.Annotated);
                                reportNullableReferenceTypesIfNeeded(a.QuestionToken);
                            }
                            else
                            {
                                type = TypeSymbolWithAnnotations.Create(IsNullableEnabled(a.CloseBracketToken), array);
                            }
                        }

                        return type;
                    }

                case SyntaxKind.PointerType:
                    {
                        var node = (PointerTypeSyntax)syntax;
                        var elementType = BindType(node.ElementType, diagnostics, basesBeingResolved);
                        ReportUnsafeIfNotAllowed(node, diagnostics);

                        // Checking BinderFlags.GenericConstraintsClause to prevent cycles in binding
                        if (Flags.HasFlag(BinderFlags.GenericConstraintsClause) && elementType.TypeKind == TypeKind.TypeParameter)
                        {
                            // Invalid constraint type. A type used as a constraint must be an interface, a non-sealed class or a type parameter.
                            Error(diagnostics, ErrorCode.ERR_BadConstraintType, node);
                        }
                        else if (elementType.IsManagedType)
                        {
                            // "Cannot take the address of, get the size of, or declare a pointer to a managed type ('{0}')"
                            Error(diagnostics, ErrorCode.ERR_ManagedAddr, node, elementType.TypeSymbol);
                        }

                        return TypeSymbolWithAnnotations.Create(new PointerTypeSymbol(elementType));
                    }

                case SyntaxKind.OmittedTypeArgument:
                    {
                        return BindTypeArgument((TypeSyntax)syntax, diagnostics, basesBeingResolved);
                    }

                case SyntaxKind.TupleType:
                    {
                        var tupleTypeSyntax = (TupleTypeSyntax)syntax;
                        return TypeSymbolWithAnnotations.Create(IsNullableEnabled(tupleTypeSyntax.CloseParenToken), BindTupleType(tupleTypeSyntax, diagnostics));
                    }

                case SyntaxKind.RefType:
                    {
                        // ref needs to be handled by the caller
                        var refTypeSyntax = (RefTypeSyntax)syntax;
                        var refToken = refTypeSyntax.RefKeyword;
                        if (!syntax.HasErrors)
                        {
                            diagnostics.Add(ErrorCode.ERR_UnexpectedToken, refToken.GetLocation(), refToken.ToString());
                        }

                        return BindNamespaceOrTypeOrAliasSymbol(refTypeSyntax.Type, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics);
                    }

                default:
                    throw ExceptionUtilities.UnexpectedValue(syntax.Kind());
            }

            void reportNullableReferenceTypesIfNeeded(SyntaxToken questionToken, TypeSymbolWithAnnotations typeArgument = default)
            {
                bool isNullableEnabled = IsNullableEnabled(questionToken);

                // Inside a method body or other executable code, we can question IsValueType without causing cycles.
                if (!typeArgument.IsNull && !ShouldCheckConstraints)
                {
                    diagnostics.Add(new LazyMissingNonNullTypesContextDiagnosticInfo(Compilation, isNullableEnabled, typeArgument), questionToken.GetLocation());
                }
                else
                {
                    DiagnosticInfo info = LazyMissingNonNullTypesContextDiagnosticInfo.ReportNullableReferenceTypesIfNeeded(Compilation, isNullableEnabled, typeArgument);

                    if (!(info is null))
                    {
                        diagnostics.Add(info, questionToken.GetLocation());
                    }
                }
            }
        }

        private TypeSymbol BindTupleType(TupleTypeSyntax syntax, DiagnosticBag diagnostics)
        {
            int numElements = syntax.Elements.Count;
            var types = ArrayBuilder<TypeSymbolWithAnnotations>.GetInstance(numElements);
            var locations = ArrayBuilder<Location>.GetInstance(numElements);
            ArrayBuilder<string> elementNames = null;

            // set of names already used
            var uniqueFieldNames = PooledHashSet<string>.GetInstance();
            bool hasExplicitNames = false;

            for (int i = 0; i < numElements; i++)
            {
                var argumentSyntax = syntax.Elements[i];

                var argumentType = BindType(argumentSyntax.Type, diagnostics);
                types.Add(argumentType);

                string name = null;
                SyntaxToken nameToken = argumentSyntax.Identifier;

                if (nameToken.Kind() == SyntaxKind.IdentifierToken)
                {
                    name = nameToken.ValueText;

                    // validate name if we have one
                    hasExplicitNames = true;
                    CheckTupleMemberName(name, i, nameToken, diagnostics, uniqueFieldNames);
                    locations.Add(nameToken.GetLocation());
                }
                else
                {
                    locations.Add(argumentSyntax.Location);
                }

                CollectTupleFieldMemberName(name, i, numElements, ref elementNames);
            }

            uniqueFieldNames.Free();

            if (hasExplicitNames)
            {
                // If the tuple type with names is bound we must have the TupleElementNamesAttribute to emit
                // it is typically there though, if we have ValueTuple at all
                // and we need System.String as well

                // Report diagnostics if System.String doesn't exist
                this.GetSpecialType(SpecialType.System_String, diagnostics, syntax);

                if (!Compilation.HasTupleNamesAttributes)
                {
                    var info = new CSDiagnosticInfo(ErrorCode.ERR_TupleElementNamesAttributeMissing,
                        AttributeDescription.TupleElementNamesAttribute.FullName);
                    Error(diagnostics, info, syntax);
                }
            }

            var typesArray = types.ToImmutableAndFree();
            var locationsArray = locations.ToImmutableAndFree();

            if (typesArray.Length < 2)
            {
                throw ExceptionUtilities.UnexpectedValue(typesArray.Length);
            }

            return TupleTypeSymbol.Create(syntax.Location,
                                            typesArray,
                                            locationsArray,
                                            elementNames == null ?
                                                default(ImmutableArray<string>) :
                                                elementNames.ToImmutableAndFree(),
                                            this.Compilation,
                                            this.ShouldCheckConstraints,
                                            errorPositions: default(ImmutableArray<bool>),
                                            syntax: syntax,
                                            diagnostics: diagnostics);
        }

        private static void CollectTupleFieldMemberName(string name, int elementIndex, int tupleSize, ref ArrayBuilder<string> elementNames)
        {
            // add the name to the list
            // names would typically all be there or none at all
            // but in case we need to handle this in error cases
            if (elementNames != null)
            {
                elementNames.Add(name);
            }
            else
            {
                if (name != null)
                {
                    elementNames = ArrayBuilder<string>.GetInstance(tupleSize);
                    for (int j = 0; j < elementIndex; j++)
                    {
                        elementNames.Add(null);
                    }
                    elementNames.Add(name);
                }
            }
        }

        private static bool CheckTupleMemberName(string name, int index, SyntaxNodeOrToken syntax, DiagnosticBag diagnostics, PooledHashSet<string> uniqueFieldNames)
        {
            int reserved = TupleTypeSymbol.IsElementNameReserved(name);
            if (reserved == 0)
            {
                Error(diagnostics, ErrorCode.ERR_TupleReservedElementNameAnyPosition, syntax, name);
                return false;
            }
            else if (reserved > 0 && reserved != index + 1)
            {
                Error(diagnostics, ErrorCode.ERR_TupleReservedElementName, syntax, name, reserved);
                return false;
            }
            else if (!uniqueFieldNames.Add(name))
            {
                Error(diagnostics, ErrorCode.ERR_TupleDuplicateElementName, syntax);
                return false;
            }
            return true;
        }

        private NamedTypeSymbol BindPredefinedTypeSymbol(PredefinedTypeSyntax node, DiagnosticBag diagnostics)
        {
            return GetSpecialType(node.Keyword.Kind().GetSpecialType(), diagnostics, node);
        }

        /// <summary>
        /// Binds a simple name or the simple name portion of a qualified name. 
        /// </summary>
        private NamespaceOrTypeOrAliasSymbolWithAnnotations BindSimpleNamespaceOrTypeOrAliasSymbol(
            SimpleNameSyntax syntax,
            DiagnosticBag diagnostics,
            ConsList<TypeSymbol> basesBeingResolved,
            bool suppressUseSiteDiagnostics,
            NamespaceOrTypeSymbol qualifierOpt = null)
        {
            // Note that the comment above is a small lie; there is no such thing as the "simple name portion" of 
            // a qualified alias member expression. A qualified alias member expression has the form
            // "identifier :: identifier optional-type-arguments" -- the right hand side of which
            // happens to match  the syntactic form of a simple name. As a convenience, we analyze the 
            // right hand side of the "::" here because it is so similar to a simple name; the left hand
            // side is in qualifierOpt.

            switch (syntax.Kind())
            {
                default:
                    return TypeSymbolWithAnnotations.Create(new ExtendedErrorTypeSymbol(qualifierOpt ?? this.Compilation.Assembly.GlobalNamespace, string.Empty, arity: 0, errorInfo: null));

                case SyntaxKind.IdentifierName:
                    return BindNonGenericSimpleNamespaceOrTypeOrAliasSymbol((IdentifierNameSyntax)syntax, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics, qualifierOpt);

                case SyntaxKind.GenericName:
                    return BindGenericSimpleNamespaceOrTypeOrAliasSymbol((GenericNameSyntax)syntax, diagnostics, basesBeingResolved, qualifierOpt);
            }
        }

        private static bool IsViableType(LookupResult result)
        {
            if (!result.IsMultiViable)
            {
                return false;
            }

            foreach (var s in result.Symbols)
            {
                switch (s.Kind)
                {
                    case SymbolKind.Alias:
                        if (((AliasSymbol)s).Target.Kind == SymbolKind.NamedType) return true;
                        break;
                    case SymbolKind.NamedType:
                    case SymbolKind.TypeParameter:
                        return true;
                }
            }

            return false;
        }

        protected NamespaceOrTypeOrAliasSymbolWithAnnotations BindNonGenericSimpleNamespaceOrTypeOrAliasSymbol(
            IdentifierNameSyntax node,
            DiagnosticBag diagnostics,
            ConsList<TypeSymbol> basesBeingResolved,
            bool suppressUseSiteDiagnostics,
            NamespaceOrTypeSymbol qualifierOpt)
        {
            var identifierValueText = node.Identifier.ValueText;

            // If we are here in an error-recovery scenario, say, "goo<int, >(123);" then
            // we might have an 'empty' simple name. In that case do not report an 
            // 'unable to find ""' error; we've already reported an error in the parser so
            // just bail out with an error symbol.

            if (string.IsNullOrWhiteSpace(identifierValueText))
            {
                return TypeSymbolWithAnnotations.Create(new ExtendedErrorTypeSymbol(
                    Compilation.Assembly.GlobalNamespace, identifierValueText, 0,
                    new CSDiagnosticInfo(ErrorCode.ERR_SingleTypeNameNotFound)));
            }

            var errorResult = CreateErrorIfLookupOnTypeParameter(node.Parent, qualifierOpt, identifierValueText, 0, diagnostics);
            if ((object)errorResult != null)
            {
                return TypeSymbolWithAnnotations.Create(errorResult);
            }

            var result = LookupResult.GetInstance();
            LookupOptions options = GetSimpleNameLookupOptions(node, node.Identifier.IsVerbatimIdentifier());

            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            this.LookupSymbolsSimpleName(result, qualifierOpt, identifierValueText, 0, basesBeingResolved, options, diagnose: true, useSiteDiagnostics: ref useSiteDiagnostics);
            diagnostics.Add(node, useSiteDiagnostics);

            Symbol bindingResult;
            // If we were looking up the identifier "dynamic" at the topmost level and didn't find anything good,
            // we actually have the type dynamic (assuming /langversion is at least 4).
            if ((object)qualifierOpt == null &&
                (node.Parent == null ||
                 node.Parent.Kind() != SyntaxKind.Attribute && // dynamic not allowed as an attribute type
                 SyntaxFacts.IsInTypeOnlyContext(node)) &&
                node.Identifier.ValueText == "dynamic" &&
                !IsViableType(result) &&
                ((CSharpParseOptions)node.SyntaxTree.Options).LanguageVersion >= MessageID.IDS_FeatureDynamic.RequiredVersion())
            {
                bindingResult = Compilation.DynamicType;
                ReportUseSiteDiagnosticForDynamic(diagnostics, node);
            }
            else
            {
                bool wasError;

                bindingResult = ResultSymbol(result, identifierValueText, 0, node, diagnostics, suppressUseSiteDiagnostics, out wasError, qualifierOpt, options);
                if (bindingResult.Kind == SymbolKind.Alias)
                {
                    var aliasTarget = ((AliasSymbol)bindingResult).GetAliasTarget(basesBeingResolved);
                    if (aliasTarget.Kind == SymbolKind.NamedType && ((NamedTypeSymbol)aliasTarget).ContainsDynamic())
                    {
                        ReportUseSiteDiagnosticForDynamic(diagnostics, node);
                    }
                }
            }

            result.Free();
            return NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(IsNullableEnabled(node.Identifier), bindingResult);
        }

        private void ReportUseSiteDiagnosticForDynamic(DiagnosticBag diagnostics, IdentifierNameSyntax node)
        {
            // Dynamic type might be bound in a declaration context where we need to synthesize the DynamicAttribute.
            // Here we report the use site error (ERR_DynamicAttributeMissing) for missing DynamicAttribute type or it's constructors.
            //                  
            // BREAKING CHANGE: Native compiler reports ERR_DynamicAttributeMissing at emit time when synthesizing DynamicAttribute.
            //                  Currently, in Roslyn we don't support reporting diagnostics while synthesizing attributes, these diagnostics are reported at bind time.
            //                  Hence, we report this diagnostic here. Note that DynamicAttribute has two constructors, and either of them may be used while
            //                  synthesizing the DynamicAttribute (see DynamicAttributeEncoder.Encode method for details).
            //                  However, unlike the native compiler which reports use site diagnostic only for the specific DynamicAttribute constructor which is going to be used,
            //                  we report it for both the constructors and also for boolean type (used by the second constructor).
            //                  This is a breaking change for the case where only one of the two constructor of DynamicAttribute is missing, but we never use it for any of the synthesized DynamicAttributes.
            //                  However, this seems like a very unlikely scenario and an acceptable break.

            if (node.IsTypeInContextWhichNeedsDynamicAttribute())
            {
                if (!Compilation.HasDynamicEmitAttributes())
                {
                    // CONSIDER:    Native compiler reports error CS1980 for each syntax node which binds to dynamic type, we do the same by reporting a diagnostic here.
                    //              However, this means we generate multiple duplicate diagnostics, when a single one would suffice.
                    //              We may want to consider adding an "Unreported" flag to the DynamicTypeSymbol to suppress duplicate CS1980.

                    // CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type '{0}' cannot be found. Are you missing a reference?
                    var info = new CSDiagnosticInfo(ErrorCode.ERR_DynamicAttributeMissing, AttributeDescription.DynamicAttribute.FullName);
                    Symbol.ReportUseSiteDiagnostic(info, diagnostics, node.Location);
                }

                this.GetSpecialType(SpecialType.System_Boolean, diagnostics, node);
            }
        }

        // Gets the name lookup options for simple generic or non-generic name.
        private static LookupOptions GetSimpleNameLookupOptions(NameSyntax node, bool isVerbatimIdentifier)
        {
            if (SyntaxFacts.IsAttributeName(node))
            {
                //  SPEC:   By convention, attribute classes are named with a suffix of Attribute. 
                //  SPEC:   An attribute-name of the form type-name may either include or omit this suffix.
                //  SPEC:   If an attribute class is found both with and without this suffix, an ambiguity 
                //  SPEC:   is present, and a compile-time error results. If the attribute-name is spelled
                //  SPEC:   such that its right-most identifier is a verbatim identifier (§2.4.2), then only
                //  SPEC:   an attribute without a suffix is matched, thus enabling such an ambiguity to be resolved.

                return isVerbatimIdentifier ? LookupOptions.VerbatimNameAttributeTypeOnly : LookupOptions.AttributeTypeOnly;
            }
            else
            {
                return LookupOptions.NamespacesOrTypesOnly;
            }
        }

        private static Symbol UnwrapAliasNoDiagnostics(Symbol symbol, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            if (symbol.Kind == SymbolKind.Alias)
            {
                return ((AliasSymbol)symbol).GetAliasTarget(basesBeingResolved);
            }

            return symbol;
        }

        private NamespaceOrTypeOrAliasSymbolWithAnnotations UnwrapAlias(NamespaceOrTypeOrAliasSymbolWithAnnotations symbol, DiagnosticBag diagnostics, SyntaxNode syntax, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            if (symbol.IsAlias)
            {
                AliasSymbol discarded;
                return NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(symbol.IsNullableEnabled, (NamespaceOrTypeSymbol)UnwrapAlias(symbol.Symbol, out discarded, diagnostics, syntax, basesBeingResolved));
            }

            return symbol;
        }

        private NamespaceOrTypeOrAliasSymbolWithAnnotations UnwrapAlias(NamespaceOrTypeOrAliasSymbolWithAnnotations symbol, out AliasSymbol alias, DiagnosticBag diagnostics, SyntaxNode syntax, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            if (symbol.IsAlias)
            {
                return NamespaceOrTypeOrAliasSymbolWithAnnotations.CreateUnannotated(symbol.IsNullableEnabled, (NamespaceOrTypeSymbol)UnwrapAlias(symbol.Symbol, out alias, diagnostics, syntax, basesBeingResolved));
            }

            alias = null;
            return symbol;
        }

        private Symbol UnwrapAlias(Symbol symbol, DiagnosticBag diagnostics, SyntaxNode syntax, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            AliasSymbol discarded;
            return UnwrapAlias(symbol, out discarded, diagnostics, syntax, basesBeingResolved);
        }

        private Symbol UnwrapAlias(Symbol symbol, out AliasSymbol alias, DiagnosticBag diagnostics, SyntaxNode syntax, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            Debug.Assert(syntax != null);
            Debug.Assert(diagnostics != null);

            if (symbol.Kind == SymbolKind.Alias)
            {
                alias = (AliasSymbol)symbol;
                var result = alias.GetAliasTarget(basesBeingResolved);
                var type = result as TypeSymbol;
                if ((object)type != null)
                {
                    // pass args in a value tuple to avoid allocating a closure
                    var args = (this, diagnostics, syntax);
                    type.VisitType((typePart, argTuple, isNested) =>
                    {
                        argTuple.Item1.ReportDiagnosticsIfObsolete(argTuple.diagnostics, typePart, argTuple.syntax, hasBaseReceiver: false);
                        return false;
                    }, args);
                }

                return result;
            }

            alias = null;
            return symbol;
        }

        private TypeSymbolWithAnnotations BindGenericSimpleNamespaceOrTypeOrAliasSymbol(
            GenericNameSyntax node,
            DiagnosticBag diagnostics,
            ConsList<TypeSymbol> basesBeingResolved,
            NamespaceOrTypeSymbol qualifierOpt)
        {
            // We are looking for a namespace, alias or type name and the user has given
            // us an identifier followed by a type argument list. Therefore they
            // must expect the result to be a generic type, and not a namespace or alias.

            // The result of this method will therefore always be a type symbol of the 
            // correct arity, though it might have to be an error type.

            // We might be asked to bind a generic simple name of the form "T<,,,>", 
            // which is only legal in the context of "typeof(T<,,,>)". If we are given
            // no type arguments and we are not in such a context, we'll give an error.

            // If we do have type arguments, then the result of this method will always
            // be a generic type symbol constructed with the given type arguments.

            // There are a number of possible error conditions. First, errors involving lookup:
            //
            // * Lookup could fail to find anything at all.
            // * Lookup could find a type of the wrong arity
            // * Lookup could find something but it is not a type.
            // 
            // Second, we could be asked to resolve an unbound type T<,,,> when
            // not in a context where it is legal to do so. Note that this is 
            // intended an improvement over the analysis performed by the 
            // native compiler; in the native compiler we catch bad uses of unbound 
            // types at parse time, not at semantic analysis time. That means that
            // we end up giving confusing "unexpected comma" or "expected type"
            // errors when it would be more informative to the user to simply
            // tell them that an unbound type is not legal in this position. 
            //
            // This also means that we can get semantic analysis of the open
            // type in the IDE even in what would have been a syntax error case
            // in the native compiler.
            //
            // We need a heuristic to deal with the situation where both kinds of errors
            // are potentially in play: what if someone says "typeof(Bogus<>.Blah<int>)"?
            // There are two errors there: first, that Bogus is not found, not a type,
            // or not of the appropriate arity, and second, that it is illegal to make
            // a partially unbound type.
            //
            // The heuristic we will use is that the former kind of error takes priority
            // over the latter; if the meaning of "Bogus<>" cannot be successfully 
            // determined then there is no point telling the user that in addition,
            // it is syntactically wrong. Moreover, at this point we do not know what they
            // mean by the remainder ".Blah<int>" of the expression and so it seems wrong to 
            // deduce more errors from it. 

            var plainName = node.Identifier.ValueText;

            SeparatedSyntaxList<TypeSyntax> typeArguments = node.TypeArgumentList.Arguments;

            bool isUnboundTypeExpr = node.IsUnboundGenericName;
            LookupOptions options = GetSimpleNameLookupOptions(node, isVerbatimIdentifier: false);

            NamedTypeSymbol unconstructedType = LookupGenericTypeName(
                diagnostics, basesBeingResolved, qualifierOpt, node, plainName, node.Arity, options);
            NamedTypeSymbol resultType;

            if (isUnboundTypeExpr)
            {
                if (!IsUnboundTypeAllowed(node))
                {
                    // If we already have an error type then skip reporting that the unbound type is illegal.
                    if (!unconstructedType.IsErrorType())
                    {
                        // error CS7003: Unexpected use of an unbound generic name
                        diagnostics.Add(ErrorCode.ERR_UnexpectedUnboundGenericName, node.Location);
                    }

                    resultType = unconstructedType.Construct(
                        UnboundArgumentErrorTypeSymbol.CreateTypeArguments(
                            unconstructedType.TypeParameters,
                            node.Arity,
                            errorInfo: null),
                        unbound: false);
                }
                else
                {
                    resultType = unconstructedType.AsUnboundGenericType();
                }
            }
            else
            {
                // It's not an unbound type expression, so we must have type arguments, and we have a 
                // generic type of the correct arity in hand (possibly an error type). Bind the type 
                // arguments and construct the final result. 
                resultType = ConstructNamedType(
                    unconstructedType,
                    node,
                    typeArguments,
                    BindTypeArguments(typeArguments, diagnostics, basesBeingResolved),
                    basesBeingResolved,
                    diagnostics);
            }

            if (options.IsAttributeTypeLookup())
            {
                // Generic type cannot be an attribute type.
                // Parser error has already been reported, just wrap the result type with error type symbol.
                Debug.Assert(unconstructedType.IsErrorType());
                Debug.Assert(resultType.IsErrorType());
                resultType = new ExtendedErrorTypeSymbol(GetContainingNamespaceOrType(resultType), resultType,
                    LookupResultKind.NotAnAttributeType, errorInfo: null);
            }

            return TypeSymbolWithAnnotations.Create(IsNullableEnabled(node.TypeArgumentList.GreaterThanToken), resultType);
        }

        private NamedTypeSymbol LookupGenericTypeName(
            DiagnosticBag diagnostics,
            ConsList<TypeSymbol> basesBeingResolved,
            NamespaceOrTypeSymbol qualifierOpt,
            GenericNameSyntax node,
            string plainName,
            int arity,
            LookupOptions options)
        {
            var errorResult = CreateErrorIfLookupOnTypeParameter(node.Parent, qualifierOpt, plainName, arity, diagnostics);
            if ((object)errorResult != null)
            {
                return errorResult;
            }

            var lookupResult = LookupResult.GetInstance();
            HashSet<DiagnosticInfo> useSiteDiagnostics = null;
            this.LookupSymbolsSimpleName(lookupResult, qualifierOpt, plainName, arity, basesBeingResolved, options, diagnose: true, useSiteDiagnostics: ref useSiteDiagnostics);
            diagnostics.Add(node, useSiteDiagnostics);

            bool wasError;
            Symbol lookupResultSymbol = ResultSymbol(lookupResult, plainName, arity, node, diagnostics, (basesBeingResolved != null), out wasError, qualifierOpt, options);

            // As we said in the method above, there are three cases here:
            //
            // * Lookup could fail to find anything at all.
            // * Lookup could find a type of the wrong arity
            // * Lookup could find something but it is not a type.
            //
            // In the first two cases we will be given back an error type symbol of the appropriate arity.
            // In the third case we will be given back the symbol -- say, a local variable symbol.
            //
            // In all three cases the appropriate error has already been reported. (That the
            // type was not found, that the generic type found does not have that arity, that 
            // the non-generic type found cannot be used with a type argument list, or that
            // the symbol found is not something that takes type arguments. )

            // The first thing to do is to make sure that we have some sort of generic type in hand.
            // (Note that an error type symbol is always a generic type.) 

            NamedTypeSymbol type = lookupResultSymbol as NamedTypeSymbol;

            if ((object)type == null)
            {
                // We did a lookup with a generic arity, filtered to types and namespaces. If
                // we got back something other than a type, there had better be an error info
                // for us.
                Debug.Assert(lookupResult.Error != null);
                type = new ExtendedErrorTypeSymbol(
                    GetContainingNamespaceOrType(lookupResultSymbol),
                    ImmutableArray.Create<Symbol>(lookupResultSymbol),
                    lookupResult.Kind,
                    lookupResult.Error,
                    arity);
            }

            lookupResult.Free();

            return type;
        }

        private ExtendedErrorTypeSymbol CreateErrorIfLookupOnTypeParameter(
            CSharpSyntaxNode node,
            NamespaceOrTypeSymbol qualifierOpt,
            string name,
            int arity,
            DiagnosticBag diagnostics)
        {
            if (((object)qualifierOpt != null) && (qualifierOpt.Kind == SymbolKind.TypeParameter))
            {
                var diagnosticInfo = new CSDiagnosticInfo(ErrorCode.ERR_LookupInTypeVariable, qualifierOpt);
                diagnostics.Add(diagnosticInfo, node.Location);
                return new ExtendedErrorTypeSymbol(this.Compilation, name, arity, diagnosticInfo, unreported: false);
            }

            return null;
        }

        private ImmutableArray<TypeSymbolWithAnnotations> BindTypeArguments(SeparatedSyntaxList<TypeSyntax> typeArguments, DiagnosticBag diagnostics, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            Debug.Assert(typeArguments.Count > 0);
            var args = ArrayBuilder<TypeSymbolWithAnnotations>.GetInstance();
            foreach (var argSyntax in typeArguments)
            {
                args.Add(BindTypeArgument(argSyntax, diagnostics, basesBeingResolved));
            }

            return args.ToImmutableAndFree();
        }

        private TypeSymbolWithAnnotations BindTypeArgument(TypeSyntax typeArgument, DiagnosticBag diagnostics, ConsList<TypeSymbol> basesBeingResolved = null)
        {
            // Unsafe types can never be type arguments, but there's a special error code for that.
            var binder = this.WithAdditionalFlags(BinderFlags.SuppressUnsafeDiagnostics);

            var arg = typeArgument.Kind() == SyntaxKind.OmittedTypeArgument
                ? TypeSymbolWithAnnotations.Create(UnboundArgumentErrorTypeSymbol.Instance)
                : binder.BindType(typeArgument, diagnostics, basesBeingResolved);

            return arg;
        }

        /// <remarks>
        /// Keep check and error in sync with ConstructBoundMethodGroupAndReportOmittedTypeArguments.
        /// </remarks>
        private NamedTypeSymbol ConstructNamedTypeUnlessTypeArgumentOmitted(SyntaxNode typeSyntax, NamedTypeSymbol type, SeparatedSyntaxList<TypeSyntax> typeArgumentsSyntax, ImmutableArray<TypeSymbolWithAnnotations> typeArguments, DiagnosticBag diagnostics)
        {
            if (typeArgumentsSyntax.Any(SyntaxKind.OmittedTypeArgument))
            {
                // Note: lookup won't have reported this, since the arity was correct.
                // CONSIDER: the text of this error message makes sense, but we might want to add a separate code.
                Error(diagnostics, ErrorCode.ERR_BadArity, typeSyntax, type, MessageID.IDS_SK_TYPE.Localize(), typeArgumentsSyntax.Count);

                // If the syntax looks like an unbound generic type, then they probably wanted the definition.
                // Give an error indicating that the syntax is incorrect and then use the definition.
                // CONSIDER: we could construct an unbound generic type symbol, but that would probably be confusing
                // outside a typeof.
                return type;
            }
            else
            {
                // we pass an empty basesBeingResolved here because this invocation is not on any possible path of
                // infinite recursion in binding base clauses.
                return ConstructNamedType(type, typeSyntax, typeArgumentsSyntax, typeArguments, basesBeingResolved: null, diagnostics: diagnostics);
            }
        }

        /// <remarks>
        /// Keep check and error in sync with ConstructNamedTypeUnlessTypeArgumentOmitted.
        /// </remarks>
        private static BoundMethodOrPropertyGroup ConstructBoundMemberGroupAndReportOmittedTypeArguments(
            SyntaxNode syntax,
            SeparatedSyntaxList<TypeSyntax> typeArgumentsSyntax,
            ImmutableArray<TypeSymbolWithAnnotations> typeArguments,
            BoundExpression receiver,
            string plainName,
            ArrayBuilder<Symbol> members,
            LookupResult lookupResult,
            BoundMethodGroupFlags methodGroupFlags,
            bool hasErrors,
            DiagnosticBag diagnostics)
        {
            if (!hasErrors && lookupResult.IsMultiViable && typeArgumentsSyntax.Any(SyntaxKind.OmittedTypeArgument))
            {
                // Note: lookup won't have reported this, since the arity was correct.
                // CONSIDER: the text of this error message makes sense, but we might want to add a separate code.
                Error(diagnostics, ErrorCode.ERR_BadArity, syntax, plainName, MessageID.IDS_MethodGroup.Localize(), typeArgumentsSyntax.Count);
                hasErrors = true;
            }

            Debug.Assert(members.Count > 0);

            switch (members[0].Kind)
            {
                case SymbolKind.Method:
                    return new BoundMethodGroup(
                        syntax,
                        typeArguments,
                        receiver,
                        plainName,
                        members.SelectAsArray(s_toMethodSymbolFunc),
                        lookupResult,
                        methodGroupFlags,
                        hasErrors);

                case SymbolKind.Property:
                    return new BoundPropertyGroup(
                        syntax,
                        members.SelectAsArray(s_toPropertySymbolFunc),
                        receiver,
                        lookupResult.Kind,
                        hasErrors);

                default:
                    throw ExceptionUtilities.UnexpectedValue(members[0].Kind);
            }
        }

        private static readonly Func<Symbol, MethodSymbol> s_toMethodSymbolFunc = s => (MethodSymbol)s;
        private static readonly Func<Symbol, PropertySymbol> s_toPropertySymbolFunc = s => (PropertySymbol)s;

        private NamedTypeSymbol ConstructNamedType(
            NamedTypeSymbol type,
            SyntaxNode typeSyntax,
            SeparatedSyntaxList<TypeSyntax> typeArgumentsSyntax,
            ImmutableArray<TypeSymbolWithAnnotations> typeArguments,
            ConsList<TypeSymbol> basesBeingResolved,
            DiagnosticBag diagnostics)
        {
            Debug.Assert(!typeArguments.IsEmpty);
            type = type.Construct(typeArguments);

            if (ShouldCheckConstraints && ConstraintsHelper.RequiresChecking(type))
            {
                bool includeNullability = Compilation.IsFeatureEnabled(MessageID.IDS_FeatureNullableReferenceTypes);
                var conversions = this.Conversions.WithNullability(includeNullability);
                type.CheckConstraintsForNonTuple(conversions, typeSyntax, typeArgumentsSyntax, this.Compilation, basesBeingResolved, diagnostics);
            }

            type = (NamedTypeSymbol)TupleTypeSymbol.TransformToTupleIfCompatible(type);

            return type;
        }

        /// <summary>
        /// Check generic type constraints unless the type is used as part of a type or method
        /// declaration. In those cases, constraints checking is handled by the caller.
        /// </summary>
        private bool ShouldCheckConstraints
        {
            get
            {
                return !this.Flags.Includes(BinderFlags.SuppressConstraintChecks);
            }
        }

        private NamespaceOrTypeOrAliasSymbolWithAnnotations BindQualifiedName(
            ExpressionSyntax leftName,
            SimpleNameSyntax rightName,
            DiagnosticBag diagnostics,
            ConsList<TypeSymbol> basesBeingResolved,
            bool suppressUseSiteDiagnostics)
        {
            var left = BindNamespaceOrTypeSymbol(leftName, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics: false).NamespaceOrTypeSymbol;
            ReportDiagnosticsIfObsolete(diagnostics, left, leftName, hasBaseReceiver: false);

            bool isLeftUnboundGenericType = left.Kind == SymbolKind.NamedType &&
                ((NamedTypeSymbol)left).IsUnboundGenericType;

            if (isLeftUnboundGenericType)
            {
                // If left name bound to an unbound generic type,
                // we want to perform right name lookup within
                // left's original named type definition.
                left = ((NamedTypeSymbol)left).OriginalDefinition;
            }

            // since the name is qualified, it cannot result in a using alias symbol, only a type or namespace
            var right = this.BindSimpleNamespaceOrTypeOrAliasSymbol(rightName, diagnostics, basesBeingResolved, suppressUseSiteDiagnostics, left);

            // If left name bound to an unbound generic type
            // and right name bound to a generic type, we must
            // convert right to an unbound generic type.
            if (isLeftUnboundGenericType)
            {
                var namedTypeRight = right.Symbol as NamedTypeSymbol;
                if ((object)namedTypeRight != null && namedTypeRight.IsGenericType)
                {
                    TypeSymbolWithAnnotations type = right.Type;
                    right = type.WithTypeAndModifiers(namedTypeRight.AsUnboundGenericType(), type.CustomModifiers);
                }
            }

            return right;
        }

        internal NamedTypeSymbol GetSpecialType(SpecialType typeId, DiagnosticBag diagnostics, SyntaxNode node)
        {
            return GetSpecialType(this.Compilation, typeId, node, diagnostics);
        }

        internal static NamedTypeSymbol GetSpecialType(CSharpCompilation compilation, SpecialType typeId, SyntaxNode node, DiagnosticBag diagnostics)
        {
            NamedTypeSymbol typeSymbol = compilation.GetSpecialType(typeId);
            Debug.Assert((object)typeSymbol != null, "Expect an error type if special type isn't found");
            ReportUseSiteDiagnostics(typeSymbol, diagnostics, node);
            return typeSymbol;
        }

        /// <summary>
        /// This is a layer on top of the Compilation version that generates a diagnostic if the special
        /// member isn't found.
        /// </summary>
        internal Symbol GetSpecialTypeMember(SpecialMember member, DiagnosticBag diagnostics, SyntaxNode syntax)
        {
            Symbol memberSymbol;
            return TryGetSpecialTypeMember(this.Compilation, member, syntax, diagnostics, out memberSymbol)
                ? memberSymbol
                : null;
        }

        internal static bool TryGetSpecialTypeMember<TSymbol>(CSharpCompilation compilation, SpecialMember specialMember, SyntaxNode syntax, DiagnosticBag diagnostics, out TSymbol symbol)
            where TSymbol : Symbol
        {
            symbol = (TSymbol)compilation.GetSpecialTypeMember(specialMember);
            if ((object)symbol == null)
            {
                MemberDescriptor descriptor = SpecialMembers.GetDescriptor(specialMember);
                diagnostics.Add(ErrorCode.ERR_MissingPredefinedMember, syntax.Location, descriptor.DeclaringTypeMetadataName, descriptor.Name);
                return false;
            }

            var useSiteDiagnostic = symbol.GetUseSiteDiagnosticForSymbolOrContainingType();
            if (useSiteDiagnostic != null)
            {
                Symbol.ReportUseSiteDiagnostic(useSiteDiagnostic, diagnostics, new SourceLocation(syntax));
            }

            return true;
        }

        /// <summary>
        /// Reports use-site diagnostics for the specified symbol.
        /// </summary>
        /// <returns>
        /// True if there was an error among the reported diagnostics
        /// </returns>
        internal static bool ReportUseSiteDiagnostics(Symbol symbol, DiagnosticBag diagnostics, SyntaxNode node)
        {
            DiagnosticInfo info = symbol.GetUseSiteDiagnostic();
            return info != null && Symbol.ReportUseSiteDiagnostic(info, diagnostics, node.Location);
        }

        internal static bool ReportUseSiteDiagnostics(Symbol symbol, DiagnosticBag diagnostics, SyntaxToken token)
        {
            DiagnosticInfo info = symbol.GetUseSiteDiagnostic();
            return info != null && Symbol.ReportUseSiteDiagnostic(info, diagnostics, token.GetLocation());
        }

        /// <summary>
        /// Reports use-site diagnostics for the specified symbol.
        /// </summary>
        /// <returns>
        /// True if there was an error among the reported diagnostics
        /// </returns>
        internal static bool ReportUseSiteDiagnostics(Symbol symbol, DiagnosticBag diagnostics, Location location)
        {
            DiagnosticInfo info = symbol.GetUseSiteDiagnostic();
            return info != null && Symbol.ReportUseSiteDiagnostic(info, diagnostics, location);
        }

        /// <summary>
        /// This is a layer on top of the Compilation version that generates a diagnostic if the well-known
        /// type isn't found.
        /// </summary>
        internal NamedTypeSymbol GetWellKnownType(WellKnownType type, DiagnosticBag diagnostics, SyntaxNode node)
        {
            NamedTypeSymbol typeSymbol = this.Compilation.GetWellKnownType(type);
            Debug.Assert((object)typeSymbol != null, "Expect an error type if well-known type isn't found");
            ReportUseSiteDiagnostics(typeSymbol, diagnostics, node);
            return typeSymbol;
        }

        /// <summary>
        /// This is a layer on top of the Compilation version that generates a diagnostic if the well-known
        /// type isn't found.
        /// </summary>
        internal NamedTypeSymbol GetWellKnownType(WellKnownType type, ref HashSet<DiagnosticInfo> useSiteDiagnostics)
        {
            NamedTypeSymbol typeSymbol = this.Compilation.GetWellKnownType(type);
            Debug.Assert((object)typeSymbol != null, "Expect an error type if well-known type isn't found");
            HashSetExtensions.InitializeAndAdd(ref useSiteDiagnostics, typeSymbol.GetUseSiteDiagnostic());
            return typeSymbol;
        }

        /// <summary>
        /// Retrieves a well-known type member and reports diagnostics.
        /// </summary>
        /// <returns>Null if the symbol is missing.</returns>
        internal static Symbol GetWellKnownTypeMember(CSharpCompilation compilation, WellKnownMember member, DiagnosticBag diagnostics, Location location = null, SyntaxNode syntax = null, bool isOptional = false)
        {
            Debug.Assert((syntax != null) ^ (location != null));

            DiagnosticInfo useSiteDiagnostic;
            Symbol memberSymbol = GetWellKnownTypeMember(compilation, member, out useSiteDiagnostic, isOptional);

            if (useSiteDiagnostic != null)
            {
                // report the diagnostic only for non-optional members:
                Symbol.ReportUseSiteDiagnostic(useSiteDiagnostic, diagnostics, location ?? syntax.Location);
            }

            return memberSymbol;
        }

        internal static Symbol GetWellKnownTypeMember(CSharpCompilation compilation, WellKnownMember member, out DiagnosticInfo diagnosticInfo, bool isOptional = false)
        {
            Symbol memberSymbol = compilation.GetWellKnownTypeMember(member);

            if ((object)memberSymbol != null)
            {
                diagnosticInfo = memberSymbol.GetUseSiteDiagnosticForSymbolOrContainingType();
                if (diagnosticInfo != null)
                {
                    // Dev11 reports use-site diagnostics even for optional symbols that are found.
                    // We decided to silently ignore bad optional symbols.

                    // Report errors only for non-optional members:
                    if (isOptional)
                    {
                        var severity = diagnosticInfo.Severity;

                        // ignore warnings:
                        diagnosticInfo = null;

                        // if the member is optional and bad for whatever reason ignore it:
                        if (severity == DiagnosticSeverity.Error)
                        {
                            return null;
                        }
                    }
                }
            }
            else if (!isOptional)
            {
                // member is missing
                MemberDescriptor memberDescriptor = WellKnownMembers.GetDescriptor(member);
                diagnosticInfo = new CSDiagnosticInfo(ErrorCode.ERR_MissingPredefinedMember, memberDescriptor.DeclaringTypeMetadataName, memberDescriptor.Name);
            }
            else
            {
                diagnosticInfo = null;
            }

            return memberSymbol;
        }

        private class ConsistentSymbolOrder : IComparer<Symbol>
        {
            public static readonly ConsistentSymbolOrder Instance = new ConsistentSymbolOrder();
            public int Compare(Symbol fst, Symbol snd)
            {
                if (snd == fst) return 0;
                if ((object)fst == null) return -1;
                if ((object)snd == null) return 1;
                if (snd.Name != fst.Name) return string.CompareOrdinal(fst.Name, snd.Name);
                if (snd.Kind != fst.Kind) return (int)fst.Kind - (int)snd.Kind;
                int aLocationsCount = !snd.Locations.IsDefault ? snd.Locations.Length : 0;
                int bLocationsCount = fst.Locations.Length;
                if (aLocationsCount != bLocationsCount) return aLocationsCount - bLocationsCount;
                if (aLocationsCount == 0 && bLocationsCount == 0) return Compare(fst.ContainingSymbol, snd.ContainingSymbol);
                Location la = snd.Locations[0];
                Location lb = fst.Locations[0];
                if (la.IsInSource != lb.IsInSource) return la.IsInSource ? 1 : -1;
                int containerResult = Compare(fst.ContainingSymbol, snd.ContainingSymbol);
                if (!la.IsInSource) return containerResult;
                if (containerResult == 0 && la.SourceTree == lb.SourceTree) return lb.SourceSpan.Start - la.SourceSpan.Start;
                return containerResult;
            }
        }

        // return the type or namespace symbol in a lookup result, or report an error.
        internal Symbol ResultSymbol(
            LookupResult result,
            string simpleName,
            int arity,
            SyntaxNode where,
            DiagnosticBag diagnostics,
            bool suppressUseSiteDiagnostics,
            out bool wasError,
            NamespaceOrTypeSymbol qualifierOpt = null,
            LookupOptions options = default(LookupOptions))
        {
            Debug.Assert(where != null);
            Debug.Assert(diagnostics != null);

            var symbols = result.Symbols;
            wasError = false;

            if (result.IsMultiViable)
            {
                if (symbols.Count > 1)
                {
                    // gracefully handle symbols.Count > 2
                    symbols.Sort(ConsistentSymbolOrder.Instance);

                    var originalSymbols = symbols.ToImmutable();

                    for (int i = 0; i < symbols.Count; i++)
                    {
                        symbols[i] = UnwrapAlias(symbols[i], diagnostics, where);
                    }

                    BestSymbolInfo secondBest;
                    BestSymbolInfo best = GetBestSymbolInfo(symbols, out secondBest);

                    Debug.Assert(!best.IsNone);
                    Debug.Assert(!secondBest.IsNone);

                    if (best.IsFromCompilation && !secondBest.IsFromCompilation)
                    {
                        var srcSymbol = symbols[best.Index];
                        var mdSymbol = symbols[secondBest.Index];

                        object arg0;

                        if (best.IsFromSourceModule)
                        {
                            arg0 = srcSymbol.Locations.First().SourceTree.FilePath;
                        }
                        else
                        {
                            Debug.Assert(best.IsFromAddedModule);
                            arg0 = srcSymbol.ContainingModule;
                        }

                        //if names match, arities match, and containing symbols match (recursively), ...
                        if (srcSymbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameArityFormat) ==
                            mdSymbol.ToDisplayString(SymbolDisplayFormat.QualifiedNameArityFormat))
                        {
                            if (srcSymbol.Kind == SymbolKind.Namespace && mdSymbol.Kind == SymbolKind.NamedType)
                            {
                                // ErrorCode.WRN_SameFullNameThisNsAgg: The namespace '{1}' in '{0}' conflicts with the imported type '{3}' in '{2}'. Using the namespace defined in '{0}'.
                                diagnostics.Add(ErrorCode.WRN_SameFullNameThisNsAgg, where.Location, originalSymbols,
                                    arg0,
                                    srcSymbol,
                                    mdSymbol.ContainingAssembly,
                                    mdSymbol);

                                return originalSymbols[best.Index];
                            }
                            else if (srcSymbol.Kind == SymbolKind.NamedType && mdSymbol.Kind == SymbolKind.Namespace)
                            {
                                // ErrorCode.WRN_SameFullNameThisAggNs: The type '{1}' in '{0}' conflicts with the imported namespace '{3}' in '{2}'. Using the type defined in '{0}'.
                                diagnostics.Add(ErrorCode.WRN_SameFullNameThisAggNs, where.Location, originalSymbols,
                                    arg0,
                                    srcSymbol,
                                    GetContainingAssembly(mdSymbol),
                                    mdSymbol);

                                return originalSymbols[best.Index];
                            }
                            else if (srcSymbol.Kind == SymbolKind.NamedType && mdSymbol.Kind == SymbolKind.NamedType)
                            {
                                // WRN_SameFullNameThisAggAgg: The type '{1}' in '{0}' conflicts with the imported type '{3}' in '{2}'. Using the type defined in '{0}'.
                                diagnostics.Add(ErrorCode.WRN_SameFullNameThisAggAgg, where.Location, originalSymbols,
                                    arg0,
                                    srcSymbol,
                                    mdSymbol.ContainingAssembly,
                                    mdSymbol);

                                return originalSymbols[best.Index];
                            }
                            else
                            {
                                // namespace would be merged with the source namespace:
                                Debug.Assert(!(srcSymbol.Kind == SymbolKind.Namespace && mdSymbol.Kind == SymbolKind.Namespace));
                            }
                        }
                    }

                    var first = symbols[best.Index];
                    var second = symbols[secondBest.Index];

                    Debug.Assert(originalSymbols[best.Index] != originalSymbols[secondBest.Index] || options.IsAttributeTypeLookup(),
                        "This kind of ambiguity is only possible for attributes.");

                    Debug.Assert(first != second || originalSymbols[best.Index] != originalSymbols[secondBest.Index],
                        "Why does the LookupResult contain the same symbol twice?");

                    CSDiagnosticInfo info;
                    bool reportError;

                    //if names match, arities match, and containing symbols match (recursively), ...
                    if (first != second &&
                        first.ToDisplayString(SymbolDisplayFormat.QualifiedNameArityFormat) ==
                            second.ToDisplayString(SymbolDisplayFormat.QualifiedNameArityFormat))
                    {
                        // suppress reporting the error if we found multiple symbols from source module
                        // since an error has already been reported from the declaration
                        reportError = !(best.IsFromSourceModule && secondBest.IsFromSourceModule);

                        if (first.Kind == SymbolKind.NamedType && second.Kind == SymbolKind.NamedType)
                        {
                            if (first.OriginalDefinition == second.OriginalDefinition)
                            {
                                // We imported different generic instantiations of the same generic type
                                // and have an ambiguous reference to a type nested in it
                                reportError = true;

                                // '{0}' is an ambiguous reference between '{1}' and '{2}'
                                info = new CSDiagnosticInfo(ErrorCode.ERR_AmbigContext, originalSymbols,
                                    new object[] {
                                        (where as NameSyntax)?.ErrorDisplayName() ?? simpleName,
                                        new FormattedSymbol(first, SymbolDisplayFormat.CSharpErrorMessageFormat),
                                        new FormattedSymbol(second, SymbolDisplayFormat.CSharpErrorMessageFormat) });
                            }
                            else
                            {
                                Debug.Assert(!best.IsFromCorLibrary);

                                // ErrorCode.ERR_SameFullNameAggAgg: The type '{1}' exists in both '{0}' and '{2}'
                                info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameAggAgg, originalSymbols,
                                    new object[] { first.ContainingAssembly, first, second.ContainingAssembly });

                                // Do not report this error if the first is declared in source and the second is declared in added module,
                                // we already reported declaration error about this name collision.
                                // Do not report this error if both are declared in added modules,
                                // we will report assembly level declaration error about this name collision.
                                if (secondBest.IsFromAddedModule)
                                {
                                    Debug.Assert(best.IsFromCompilation);
                                    reportError = false;
                                }
                                else if (this.Flags.Includes(BinderFlags.IgnoreCorLibraryDuplicatedTypes) &&
                                    secondBest.IsFromCorLibrary)
                                {
                                    // Ignore duplicate types from the cor library if necessary.
                                    // (Specifically the framework assemblies loaded at runtime in
                                    // the EE may contain types also available from mscorlib.dll.)
                                    return first;
                                }
                            }
                        }
                        else if (first.Kind == SymbolKind.Namespace && second.Kind == SymbolKind.NamedType)
                        {
                            // ErrorCode.ERR_SameFullNameNsAgg: The namespace '{1}' in '{0}' conflicts with the type '{3}' in '{2}'
                            info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameNsAgg, originalSymbols,
                                new object[] { GetContainingAssembly(first), first, second.ContainingAssembly, second });

                            // Do not report this error if namespace is declared in source and the type is declared in added module,
                            // we already reported declaration error about this name collision.
                            if (best.IsFromSourceModule && secondBest.IsFromAddedModule)
                            {
                                reportError = false;
                            }
                        }
                        else if (first.Kind == SymbolKind.NamedType && second.Kind == SymbolKind.Namespace)
                        {
                            if (!secondBest.IsFromCompilation || secondBest.IsFromSourceModule)
                            {
                                // ErrorCode.ERR_SameFullNameNsAgg: The namespace '{1}' in '{0}' conflicts with the type '{3}' in '{2}'
                                info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameNsAgg, originalSymbols,
                                    new object[] { GetContainingAssembly(second), second, first.ContainingAssembly, first });
                            }
                            else
                            {
                                Debug.Assert(secondBest.IsFromAddedModule);

                                // ErrorCode.ERR_SameFullNameThisAggThisNs: The type '{1}' in '{0}' conflicts with the namespace '{3}' in '{2}'
                                object arg0;

                                if (best.IsFromSourceModule)
                                {
                                    arg0 = first.Locations.First().SourceTree.FilePath;
                                }
                                else
                                {
                                    Debug.Assert(best.IsFromAddedModule);
                                    arg0 = first.ContainingModule;
                                }

                                ModuleSymbol arg2 = second.ContainingModule;

                                // Merged namespaces that span multiple modules don't have a containing module,
                                // so just use module with the smallest ordinal from the containing assembly.
                                if ((object)arg2 == null)
                                {
                                    foreach (NamespaceSymbol ns in ((NamespaceSymbol)second).ConstituentNamespaces)
                                    {
                                        if (ns.ContainingAssembly == Compilation.Assembly)
                                        {
                                            ModuleSymbol module = ns.ContainingModule;

                                            if ((object)arg2 == null || arg2.Ordinal > module.Ordinal)
                                            {
                                                arg2 = module;
                                            }
                                        }
                                    }
                                }

                                Debug.Assert(arg2.ContainingAssembly == Compilation.Assembly);

                                info = new CSDiagnosticInfo(ErrorCode.ERR_SameFullNameThisAggThisNs, originalSymbols,
                                    new object[] { arg0, first, arg2, second });
                            }
                        }
                        else if (first.Kind == SymbolKind.RangeVariable && second.Kind == SymbolKind.RangeVariable)
                        {
                            // We will already have reported a conflicting range variable declaration.
                            info = new CSDiagnosticInfo(ErrorCode.ERR_AmbigMember, originalSymbols,
                                new object[] { first, second });
                        }
                        else
                        {
                            // TODO: this is not an appropriate error message here, but used as a fallback until the
                            // appropriate diagnostics are implemented.
                            // '{0}' is an ambiguous reference between '{1}' and '{2}'
                            //info = diagnostics.Add(ErrorCode.ERR_AmbigContext, location, readOnlySymbols,
                            //    whereText,
                            //    first,
                            //    second);

                            // CS0229: Ambiguity between '{0}' and '{1}'
                            info = new CSDiagnosticInfo(ErrorCode.ERR_AmbigMember, originalSymbols,
                                new object[] { first, second });

                            reportError = true;
                        }
                    }
                    else
                    {
                        Debug.Assert(originalSymbols[best.Index].Name != originalSymbols[secondBest.Index].Name ||
                                     originalSymbols[best.Index] != originalSymbols[secondBest.Index],
                            "Why was the lookup result viable if it contained non-equal symbols with the same name?");

                        reportError = true;

                        if (first is NamespaceOrTypeSymbol && second is NamespaceOrTypeSymbol)
                        {
                            if (options.IsAttributeTypeLookup() &&
                                first.Kind == SymbolKind.NamedType &&
                                second.Kind == SymbolKind.NamedType &&
                                originalSymbols[best.Index].Name != originalSymbols[secondBest.Index].Name && // Use alias names, if available.
                                Compilation.IsAttributeType((NamedTypeSymbol)first) &&
                                Compilation.IsAttributeType((NamedTypeSymbol)second))
                            {
                                //  SPEC:   If an attribute class is found both with and without Attribute suffix, an ambiguity 
                                //  SPEC:   is present, and a compile-time error results.

                                info = new CSDiagnosticInfo(ErrorCode.ERR_AmbiguousAttribute, originalSymbols,
                                    new object[] { (where as NameSyntax)?.ErrorDisplayName() ?? simpleName, first, second });
                            }
                            else
                            {
                                // '{0}' is an ambiguous reference between '{1}' and '{2}'
                                info = new CSDiagnosticInfo(ErrorCode.ERR_AmbigContext, originalSymbols,
                                    new object[] {
                                        (where as NameSyntax)?.ErrorDisplayName() ?? simpleName,
                                        new FormattedSymbol(first, SymbolDisplayFormat.CSharpErrorMessageFormat),
                                        new FormattedSymbol(second, SymbolDisplayFormat.CSharpErrorMessageFormat) });
                            }
                        }
                        else
                        {
                            // CS0229: Ambiguity between '{0}' and '{1}'
                            info = new CSDiagnosticInfo(ErrorCode.ERR_AmbigMember, originalSymbols,
                                new object[] { first, second });
                        }
                    }

                    wasError = true;

                    if (reportError)
                    {
                        diagnostics.Add(info, where.Location);
                    }

                    return new ExtendedErrorTypeSymbol(
                        GetContainingNamespaceOrType(originalSymbols[0]),
                        originalSymbols,
                        LookupResultKind.Ambiguous,
                        info,
                        arity);
                }
                else
                {
                    // Single viable result.
                    var singleResult = symbols[0];

                    // Cannot reference System.Void directly.
                    var singleType = singleResult as TypeSymbol;
                    if ((object)singleType != null && singleType.PrimitiveTypeCode == Cci.PrimitiveTypeCode.Void && simpleName == "Void")
                    {
                        wasError = true;
                        var errorInfo = new CSDiagnosticInfo(ErrorCode.ERR_SystemVoid);
                        diagnostics.Add(errorInfo, where.Location);
                        singleResult = new ExtendedErrorTypeSymbol(GetContainingNamespaceOrType(singleResult), singleResult, LookupResultKind.NotReferencable, errorInfo); // UNDONE: Review resultkind.
                    }
                    // Check for bad symbol.
                    else
                    {
                        if (singleResult.Kind == SymbolKind.NamedType &&
                            ((SourceModuleSymbol)this.Compilation.SourceModule).AnyReferencedAssembliesAreLinked)
                        {
                            // Complain about unembeddable types from linked assemblies.
                            Emit.NoPia.EmbeddedTypesManager.IsValidEmbeddableType((NamedTypeSymbol)singleResult, where, diagnostics);
                        }

                        if (!suppressUseSiteDiagnostics)
                        {
                            wasError = ReportUseSiteDiagnostics(singleResult, diagnostics, where);
                        }
                        else if (singleResult.Kind == SymbolKind.ErrorType)
                        {
                            // We want to report ERR_CircularBase error on the spot to make sure 
                            // that the right location is used for it.
                            var errorType = (ErrorTypeSymbol)singleResult;

                            if (errorType.Unreported)
                            {
                                DiagnosticInfo errorInfo = errorType.ErrorInfo;

                                if (errorInfo != null && errorInfo.Code == (int)ErrorCode.ERR_CircularBase)
                                {
                                    wasError = true;
                                    diagnostics.Add(errorInfo, where.Location);
                                    singleResult = new ExtendedErrorTypeSymbol(GetContainingNamespaceOrType(errorType), errorType.Name, errorType.Arity, errorInfo, unreported: false);
                                }
                            }
                        }
                    }

                    return singleResult;
                }
            }

            // Below here is the error case; no viable symbols found (but maybe one or more non-viable.)
            wasError = true;

            if (result.Kind == LookupResultKind.Empty)
            {
                string aliasOpt = null;
                SyntaxNode node = where;
                while (node is ExpressionSyntax)
                {
                    if (node.Kind() == SyntaxKind.AliasQualifiedName)
                    {
                        aliasOpt = ((AliasQualifiedNameSyntax)node).Alias.Identifier.ValueText;
                        break;
                    }
                    node = node.Parent;
                }

                CSDiagnosticInfo info = NotFound(where, simpleName, arity, (where as NameSyntax)?.ErrorDisplayName() ?? simpleName, diagnostics, aliasOpt, qualifierOpt, options);
                return new ExtendedErrorTypeSymbol(qualifierOpt ?? Compilation.Assembly.GlobalNamespace, simpleName, arity, info);
            }

            Debug.Assert(symbols.Count > 0);

            // Report any errors we encountered with the symbol we looked up.
            if (!suppressUseSiteDiagnostics)
            {
                for (int i = 0; i < symbols.Count; i++)
                {
                    ReportUseSiteDiagnostics(symbols[i], diagnostics, where);
                }
            }

            // result.Error might be null if we have already generated parser errors,
            // e.g. when generic name is used for attribute name.
            if (result.Error != null &&
                ((object)qualifierOpt == null || qualifierOpt.Kind != SymbolKind.ErrorType)) // Suppress cascading.
            {
                diagnostics.Add(new CSDiagnostic(result.Error, where.Location));
            }

            if ((symbols.Count > 1) || (symbols[0] is NamespaceOrTypeSymbol || symbols[0] is AliasSymbol) ||
                result.Kind == LookupResultKind.NotATypeOrNamespace || result.Kind == LookupResultKind.NotAnAttributeType)
            {
                // Bad type or namespace (or things expected as types/namespaces) are packaged up as error types, preserving the symbols and the result kind.
                // We do this if there are multiple symbols too, because just returning one would be losing important information, and they might
                // be of different kinds.
                return new ExtendedErrorTypeSymbol(GetContainingNamespaceOrType(symbols[0]), symbols.ToImmutable(), result.Kind, result.Error, arity);
            }
            else
            {
                // It's a single non-type-or-namespace; error was already reported, so just return it.
                return symbols[0];
            }
        }

        private static AssemblySymbol GetContainingAssembly(Symbol symbol)
        {
            // Merged namespaces that span multiple assemblies don't have a containing assembly,
            // so just use the containing assembly of the first constituent.
            return symbol.ContainingAssembly ?? ((NamespaceSymbol)symbol).ConstituentNamespaces.First().ContainingAssembly;
        }

        [Flags]
        private enum BestSymbolLocation
        {
            None,
            FromSourceModule,
            FromAddedModule,
            FromReferencedAssembly,
            FromCorLibrary,
        }

        [DebuggerDisplay("Location = {_location}, Index = {_index}")]
        private struct BestSymbolInfo
        {
            private readonly BestSymbolLocation _location;
            private readonly int _index;

            /// <summary>
            /// Returns -1 if None.
            /// </summary>
            public int Index
            {
                get
                {
                    return IsNone ? -1 : _index;
                }
            }

            public bool IsFromSourceModule
            {
                get
                {
                    return _location == BestSymbolLocation.FromSourceModule;
                }
            }

            public bool IsFromAddedModule
            {
                get
                {
                    return _location == BestSymbolLocation.FromAddedModule;
                }
            }

            public bool IsFromCompilation
            {
                get
                {
                    return (_location == BestSymbolLocation.FromSourceModule) || (_location == BestSymbolLocation.FromAddedModule);
                }
            }

            public bool IsNone
            {
                get
                {
                    return _location == BestSymbolLocation.None;
                }
            }

            public bool IsFromCorLibrary
            {
                get
                {
                    return _location == BestSymbolLocation.FromCorLibrary;
                }
            }

            public BestSymbolInfo(BestSymbolLocation location, int index)
            {
                Debug.Assert(location != BestSymbolLocation.None);
                _location = location;
                _index = index;
            }

            /// <summary>
            /// Prefers symbols from source module, then from added modules, then from referenced assemblies.
            /// Returns true if values were swapped.
            /// </summary>
            public static bool Sort(ref BestSymbolInfo first, ref BestSymbolInfo second)
            {
                if (IsSecondLocationBetter(first._location, second._location))
                {
                    BestSymbolInfo temp = first;
                    first = second;
                    second = temp;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Returns true if the second is a better location than the first.
            /// </summary>
            public static bool IsSecondLocationBetter(BestSymbolLocation firstLocation, BestSymbolLocation secondLocation)
            {
                Debug.Assert(secondLocation != 0);
                return (firstLocation == BestSymbolLocation.None) || (firstLocation > secondLocation);
            }
        }

        /// <summary>
        /// Prefer symbols from source module, then from added modules, then from referenced assemblies.
        /// </summary>
        private BestSymbolInfo GetBestSymbolInfo(ArrayBuilder<Symbol> symbols, out BestSymbolInfo secondBest)
        {
            BestSymbolInfo first = default(BestSymbolInfo);
            BestSymbolInfo second = default(BestSymbolInfo);
            var compilation = this.Compilation;

            for (int i = 0; i < symbols.Count; i++)
            {
                var symbol = symbols[i];
                BestSymbolLocation location;

                if (symbol.Kind == SymbolKind.Namespace)
                {
                    location = BestSymbolLocation.None;
                    foreach (var ns in ((NamespaceSymbol)symbol).ConstituentNamespaces)
                    {
                        var current = GetLocation(compilation, ns);
                        if (BestSymbolInfo.IsSecondLocationBetter(location, current))
                        {
                            location = current;
                            if (location == BestSymbolLocation.FromSourceModule)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    location = GetLocation(compilation, symbol);
                }

                var third = new BestSymbolInfo(location, i);
                if (BestSymbolInfo.Sort(ref second, ref third))
                {
                    BestSymbolInfo.Sort(ref first, ref second);
                }
            }

            Debug.Assert(!first.IsNone);
            Debug.Assert(!second.IsNone);

            secondBest = second;
            return first;
        }

        private static BestSymbolLocation GetLocation(CSharpCompilation compilation, Symbol symbol)
        {
            var containingAssembly = symbol.ContainingAssembly;
            if (containingAssembly == compilation.SourceAssembly)
            {
                return (symbol.ContainingModule == compilation.SourceModule) ?
                    BestSymbolLocation.FromSourceModule :
                    BestSymbolLocation.FromAddedModule;
            }
            else
            {
                return (containingAssembly == containingAssembly.CorLibrary) ?
                    BestSymbolLocation.FromCorLibrary :
                    BestSymbolLocation.FromReferencedAssembly;
            }
        }

        /// <remarks>
        /// This is only intended to be called when the type isn't found (i.e. not when it is found but is inaccessible, has the wrong arity, etc).
        /// </remarks>
        private CSDiagnosticInfo NotFound(SyntaxNode where, string simpleName, int arity, string whereText, DiagnosticBag diagnostics, string aliasOpt, NamespaceOrTypeSymbol qualifierOpt, LookupOptions options)
        {
            var location = where.Location;
            // Lookup totally ignores type forwarders, but we want the type lookup diagnostics
            // to distinguish between a type that can't be found and a type that is only present
            // as a type forwarder.  We'll look for type forwarders in the containing and
            // referenced assemblies and report more specific diagnostics if they are found.
            AssemblySymbol forwardedToAssembly;

            // for attributes, suggest both, but not for verbatim name
            if (options.IsAttributeTypeLookup() && !options.IsVerbatimNameAttributeTypeLookup())
            {
                // just recurse one level, so cheat and OR verbatim name option :)
                NotFound(where, simpleName, arity, whereText + "Attribute", diagnostics, aliasOpt, qualifierOpt, options | LookupOptions.VerbatimNameAttributeTypeOnly);
            }

            if ((object)qualifierOpt != null)
            {
                if (qualifierOpt.IsType)
                {
                    var errorQualifier = qualifierOpt as ErrorTypeSymbol;
                    if ((object)errorQualifier != null && errorQualifier.ErrorInfo != null)
                    {
                        return (CSDiagnosticInfo)errorQualifier.ErrorInfo;
                    }

                    return diagnostics.Add(ErrorCode.ERR_DottedTypeNameNotFoundInAgg, location, whereText, qualifierOpt);
                }
                else
                {
                    Debug.Assert(qualifierOpt.IsNamespace);

                    forwardedToAssembly = GetForwardedToAssembly(simpleName, arity, ref qualifierOpt, diagnostics, location);

                    if (ReferenceEquals(qualifierOpt, Compilation.GlobalNamespace))
                    {
                        Debug.Assert(aliasOpt == null || aliasOpt == SyntaxFacts.GetText(SyntaxKind.GlobalKeyword));
                        return (object)forwardedToAssembly == null
                            ? diagnostics.Add(ErrorCode.ERR_GlobalSingleTypeNameNotFound, location, whereText, qualifierOpt)
                            : diagnostics.Add(ErrorCode.ERR_GlobalSingleTypeNameNotFoundFwd, location, whereText, forwardedToAssembly);
                    }
                    else
                    {
                        object container = qualifierOpt;

                        // If there was an alias (e.g. A::C) and the given qualifier is the global namespace of the alias,
                        // then use the alias name in the error message, since it's more helpful than "<global namespace>".
                        if (aliasOpt != null && qualifierOpt.IsNamespace && ((NamespaceSymbol)qualifierOpt).IsGlobalNamespace)
                        {
                            container = aliasOpt;
                        }

                        return (object)forwardedToAssembly == null
                            ? diagnostics.Add(ErrorCode.ERR_DottedTypeNameNotFoundInNS, location, whereText, container)
                            : diagnostics.Add(ErrorCode.ERR_DottedTypeNameNotFoundInNSFwd, location, whereText, container, forwardedToAssembly);
                    }
                }
            }

            if (options == LookupOptions.NamespaceAliasesOnly)
            {
                return diagnostics.Add(ErrorCode.ERR_AliasNotFound, location, whereText);
            }

            if ((where as IdentifierNameSyntax)?.Identifier.Text == "var" && !options.IsAttributeTypeLookup())
            {
                var code = (where.Parent is QueryClauseSyntax) ? ErrorCode.ERR_TypeVarNotFoundRangeVariable : ErrorCode.ERR_TypeVarNotFound;
                return diagnostics.Add(code, location);
            }

            forwardedToAssembly = GetForwardedToAssembly(simpleName, arity, ref qualifierOpt, diagnostics, location);

            if ((object)forwardedToAssembly != null)
            {
                return qualifierOpt == null
                    ? diagnostics.Add(ErrorCode.ERR_SingleTypeNameNotFoundFwd, location, whereText, forwardedToAssembly)
                    : diagnostics.Add(ErrorCode.ERR_DottedTypeNameNotFoundInNSFwd, location, whereText, qualifierOpt, forwardedToAssembly);
            }

            return diagnostics.Add(ErrorCode.ERR_SingleTypeNameNotFound, location, whereText);
        }

        protected virtual AssemblySymbol GetForwardedToAssemblyInUsingNamespaces(string metadataName, ref NamespaceOrTypeSymbol qualifierOpt, DiagnosticBag diagnostics, Location location)
        {
            return Next?.GetForwardedToAssemblyInUsingNamespaces(metadataName, ref qualifierOpt, diagnostics, location);
        }

        protected AssemblySymbol GetForwardedToAssembly(string fullName, DiagnosticBag diagnostics, Location location)
        {
            var metadataName = MetadataTypeName.FromFullName(fullName);
            foreach (var referencedAssembly in
                Compilation.Assembly.Modules[0].GetReferencedAssemblySymbols())
            {
                var forwardedType =
                    referencedAssembly.TryLookupForwardedMetadataType(ref metadataName);
                if ((object)forwardedType != null)
                {
                    if (forwardedType.Kind == SymbolKind.ErrorType)
                    {
                        DiagnosticInfo diagInfo = ((ErrorTypeSymbol)forwardedType).ErrorInfo;

                        if (diagInfo.Code == (int)ErrorCode.ERR_CycleInTypeForwarder)
                        {
                            Debug.Assert((object)forwardedType.ContainingAssembly != null, "How did we find a cycle if there was no forwarding?");
                            diagnostics.Add(ErrorCode.ERR_CycleInTypeForwarder, location, fullName, forwardedType.ContainingAssembly.Name);
                        }
                        else if (diagInfo.Code == (int)ErrorCode.ERR_TypeForwardedToMultipleAssemblies)
                        {
                            diagnostics.Add(diagInfo, location);
                            return null; // Cannot determine a suitable forwarding assembly
                        }
                    }

                    return forwardedType.ContainingAssembly;
                }
            }

            return null;
        }

        /// <summary>
        /// Look for a type forwarder for the given type in the containing assembly and any referenced assemblies.
        /// </summary>
        /// <param name="name">The name of the (potentially) forwarded type.</param>
        /// <param name="arity">The arity of the forwarded type.</param>
        /// <param name="qualifierOpt">The namespace of the potentially forwarded type. If none is provided, will
        /// try Usings of the current import for eligible namespaces and return the namespace of the found forwarder, 
        /// if any.</param>
        /// <param name="diagnostics">Will be used to report non-fatal errors during look up.</param>
        /// <param name="location">Location to report errors on.</param>
        /// <returns>Returns the Assembly to which the type is forwarded, or null if none is found.</returns>
        /// <remarks>
        /// Since this method is intended to be used for error reporting, it stops as soon as it finds
        /// any type forwarder (or an error to report). It does not check other assemblies for consistency or better results.
        /// </remarks>
        protected AssemblySymbol GetForwardedToAssembly(string name, int arity, ref NamespaceOrTypeSymbol qualifierOpt, DiagnosticBag diagnostics, Location location)
        {
            // If we are in the process of binding assembly level attributes, we might get into an infinite cycle
            // if any of the referenced assemblies forwards type to this assembly. Since forwarded types
            // are specified through assembly level attributes, an attempt to resolve the forwarded type
            // might require us to examine types forwarded by this assembly, thus binding assembly level 
            // attributes again. And the cycle continues. 
            // So, we won't do the analysis in this case, at the expense of better diagnostics.
            if ((this.Flags & BinderFlags.InContextualAttributeBinder) != 0)
            {
                var current = this;

                do
                {
                    var contextualAttributeBinder = current as ContextualAttributeBinder;

                    if (contextualAttributeBinder != null)
                    {
                        if ((object)contextualAttributeBinder.AttributeTarget != null &&
                            contextualAttributeBinder.AttributeTarget.Kind == SymbolKind.Assembly)
                        {
                            return null;
                        }

                        break;
                    }

                    current = current.Next;
                }
                while (current != null);
            }

            // NOTE: This won't work if the type isn't using CLS-style generic naming (i.e. `arity), but this code is
            // only intended to improve diagnostic messages, so false negatives in corner cases aren't a big deal.
            var metadataName = MetadataHelpers.ComposeAritySuffixedMetadataName(name, arity);
            var fullMetadataName = MetadataHelpers.BuildQualifiedName(qualifierOpt?.ToDisplayString(SymbolDisplayFormat.QualifiedNameOnlyFormat), metadataName);
            var result = GetForwardedToAssembly(fullMetadataName, diagnostics, location);
            if ((object)result != null)
            {
                return result;
            }

            if ((object)qualifierOpt == null)
            {
                return GetForwardedToAssemblyInUsingNamespaces(metadataName, ref qualifierOpt, diagnostics, location);
            }

            return null;
        }

        internal static bool CheckFeatureAvailability(SyntaxNode syntax, MessageID feature, DiagnosticBag diagnostics, Location locationOpt = null)
        {
            return CheckFeatureAvailability(syntax.SyntaxTree, feature, diagnostics, locationOpt ?? syntax.GetLocation());
        }

        internal static bool CheckFeatureAvailability(SyntaxTree tree, MessageID feature, DiagnosticBag diagnostics, Location location)
        {
            CSDiagnosticInfo error = GetFeatureAvailabilityDiagnosticInfo(tree, feature);

            if (error is null)
            {
                return true;
            }

            diagnostics.Add(new CSDiagnostic(error, location));
            return false;
        }

        private static CSDiagnosticInfo GetFeatureAvailabilityDiagnosticInfo(SyntaxTree tree, MessageID feature)
        {
            CSharpParseOptions options = (CSharpParseOptions)tree.Options;

            if (options.IsFeatureEnabled(feature))
            {
                return null;
            }

            string requiredFeature = feature.RequiredFeature();
            if (requiredFeature != null)
            {
                return new CSDiagnosticInfo(ErrorCode.ERR_FeatureIsExperimental, feature.Localize(), requiredFeature);
            }

            LanguageVersion availableVersion = options.LanguageVersion;
            LanguageVersion requiredVersion = feature.RequiredVersion();
            if (requiredVersion > availableVersion)
            {
                return new CSDiagnosticInfo(availableVersion.GetErrorCode(), feature.Localize(), new CSharpRequiredLanguageVersion(requiredVersion));
            }

            return null;
        }
    }
}
