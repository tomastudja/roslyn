﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    internal sealed class SynthesizedPrimaryConstructor : SourceConstructorSymbolBase
    {
        private IReadOnlyDictionary<ParameterSymbol, FieldSymbol>? _capturedParameters = null;

        public SynthesizedPrimaryConstructor(
             SourceMemberContainerTypeSymbol containingType,
             TypeDeclarationSyntax syntax) :
             base(containingType, syntax.Identifier.GetLocation(), syntax, isIterator: false)
        {
            Debug.Assert(syntax.Kind() is SyntaxKind.RecordDeclaration or SyntaxKind.RecordStructDeclaration or SyntaxKind.ClassDeclaration or SyntaxKind.StructDeclaration);
            Debug.Assert(containingType.HasPrimaryConstructor);
            Debug.Assert(containingType is SourceNamedTypeSymbol);
            Debug.Assert(containingType is IAttributeTargetSymbol);
            Debug.Assert(syntax.ParameterList != null);

            this.MakeFlags(
                MethodKind.Constructor,
                RefKind.None,
                containingType.IsAbstract ? DeclarationModifiers.Protected : DeclarationModifiers.Public,
                returnsVoid: true,
                // We consider synthesized constructors to have a body, since they effectively span the entire type, and
                // can do things like create constructor assignments that write into the fields/props of the type.
                hasAnyBody: true,
                isExpressionBodied: false,
                isExtensionMethod: false,
                isVarArg: syntax.ParameterList.Parameters.Any(static p => p.IsArgList),
                isNullableAnalysisEnabled: false); // IsNullableAnalysisEnabled uses containing type instead.
        }

        internal TypeDeclarationSyntax GetSyntax()
        {
            Debug.Assert(syntaxReferenceOpt != null);
            return (TypeDeclarationSyntax)syntaxReferenceOpt.GetSyntax();
        }

        protected override IAttributeTargetSymbol AttributeOwner
        {
            get { return (IAttributeTargetSymbol)ContainingType; }
        }

        protected override AttributeLocation AttributeLocationForLoadAndValidateAttributes
        {
            get { return AttributeLocation.Method; }
        }

        internal override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations()
        {
            return new OneOrMany<SyntaxList<AttributeListSyntax>>(((SourceNamedTypeSymbol)ContainingType).GetAttributeDeclarations());
        }

        protected override ParameterListSyntax GetParameterList()
        {
            return GetSyntax().ParameterList!;
        }

        protected override CSharpSyntaxNode? GetInitializer()
        {
            return GetSyntax().PrimaryConstructorBaseTypeIfClass;
        }

        public new SourceMemberContainerTypeSymbol ContainingType => (SourceMemberContainerTypeSymbol)base.ContainingType;

        protected override bool AllowRefOrOut => !(ContainingType is { IsRecord: true } or { IsRecordStruct: true });

        internal override bool IsNullableAnalysisEnabled()
        {
            return ContainingType.IsNullableEnabledForConstructorsAndInitializers(IsStatic);
        }

        protected override bool IsWithinExpressionOrBlockBody(int position, out int offset)
        {
            offset = -1;
            return false;
        }

        internal override ExecutableCodeBinder TryGetBodyBinder(BinderFactory? binderFactoryOpt = null, bool ignoreAccessibility = false)
        {
            TypeDeclarationSyntax typeDecl = GetSyntax();
            Debug.Assert(typeDecl.ParameterList is not null);
            InMethodBinder result = (binderFactoryOpt ?? this.DeclaringCompilation.GetBinderFactory(typeDecl.SyntaxTree)).GetPrimaryConstructorInMethodBinder(this);
            return new ExecutableCodeBinder(SyntaxNode, this, result.WithAdditionalFlags(ignoreAccessibility ? BinderFlags.IgnoreAccessibility : BinderFlags.None));
        }

        public IEnumerable<FieldSymbol> GetBackingFields()
        {
            IReadOnlyDictionary<ParameterSymbol, FieldSymbol> capturedParameters = GetCapturedParameters();

            if (capturedParameters.Count == 0)
            {
                return SpecializedCollections.EmptyEnumerable<FieldSymbol>();
            }

            return capturedParameters.OrderBy(static pair => pair.Key.Ordinal).Select(static pair => pair.Value);
        }

        public IReadOnlyDictionary<ParameterSymbol, FieldSymbol> GetCapturedParameters()
        {
            if (_capturedParameters != null)
            {
                return _capturedParameters;
            }

            if (ContainingType is { IsRecord: true } or { IsRecordStruct: true } || ParameterCount == 0)
            {
                _capturedParameters = SpecializedCollections.EmptyReadOnlyDictionary<ParameterSymbol, FieldSymbol>();
                return _capturedParameters;
            }

            Interlocked.CompareExchange(ref _capturedParameters, Binder.CapturedParametersFinder.GetCapturedParameters(this), null);
            return _capturedParameters;
        }

        internal override (CSharpAttributeData?, BoundAttribute?) EarlyDecodeWellKnownAttribute(ref EarlyDecodeWellKnownAttributeArguments<EarlyWellKnownAttributeBinder, NamedTypeSymbol, AttributeSyntax, AttributeLocation> arguments)
        {
            Debug.Assert(arguments.SymbolPart == AttributeLocation.Method);
            arguments.SymbolPart = AttributeLocation.None;
            var result = base.EarlyDecodeWellKnownAttribute(ref arguments);
            arguments.SymbolPart = AttributeLocation.Method;
            return result;
        }

        protected override void DecodeWellKnownAttributeImpl(ref DecodeWellKnownAttributeArguments<AttributeSyntax, CSharpAttributeData, AttributeLocation> arguments)
        {
            Debug.Assert(arguments.SymbolPart == AttributeLocation.Method);
            arguments.SymbolPart = AttributeLocation.None;
            base.DecodeWellKnownAttributeImpl(ref arguments);
            arguments.SymbolPart = AttributeLocation.Method;
        }

        internal override void PostDecodeWellKnownAttributes(ImmutableArray<CSharpAttributeData> boundAttributes, ImmutableArray<AttributeSyntax> allAttributeSyntaxNodes, BindingDiagnosticBag diagnostics, AttributeLocation symbolPart, WellKnownAttributeData decodedData)
        {
            Debug.Assert(symbolPart is AttributeLocation.Method or AttributeLocation.Return);
            base.PostDecodeWellKnownAttributes(boundAttributes, allAttributeSyntaxNodes, diagnostics, symbolPart is AttributeLocation.Method ? AttributeLocation.None : symbolPart, decodedData);
        }

        protected override bool ShouldBindAttributes(AttributeListSyntax attributeDeclarationSyntax, BindingDiagnosticBag diagnostics)
        {
            Debug.Assert(attributeDeclarationSyntax.Target is object);

            if (!base.ShouldBindAttributes(attributeDeclarationSyntax, diagnostics))
            {
                return false;
            }

            if (attributeDeclarationSyntax.SyntaxTree == SyntaxRef.SyntaxTree &&
                GetSyntax().AttributeLists.Contains(attributeDeclarationSyntax))
            {
                if (ContainingType is { IsRecord: true } or { IsRecordStruct: true })
                {
                    MessageID.IDS_FeaturePrimaryConstructors.CheckFeatureAvailability(diagnostics, attributeDeclarationSyntax, attributeDeclarationSyntax.Target.Identifier.GetLocation());
                }

                return true;
            }

            SyntaxToken target = attributeDeclarationSyntax.Target.Identifier;
            diagnostics.Add(ErrorCode.WRN_AttributeLocationOnBadDeclaration,
                            target.GetLocation(), target.ToString(), (AttributeOwner.AllowedAttributeLocations & ~AttributeLocation.Method).ToDisplayString());

            return false;
        }
    }
}
