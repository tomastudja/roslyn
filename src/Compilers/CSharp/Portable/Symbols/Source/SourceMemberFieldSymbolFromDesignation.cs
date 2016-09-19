﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Symbols
{
    /// <summary>
    /// Represents expression variables declared in a global statement.
    /// </summary>
    internal class SourceMemberFieldSymbolFromDesignation : SourceMemberFieldSymbol
    {
        private TypeSymbol _lazyType;

        internal SourceMemberFieldSymbolFromDesignation(
            SourceMemberContainerTypeSymbol containingType,
            SingleVariableDesignationSyntax designation,
            DeclarationModifiers modifiers)
            : base(containingType, modifiers, designation.Identifier.ValueText, designation.GetReference(), designation.Identifier.GetLocation())
        {
            Debug.Assert(DeclaredAccessibility == Accessibility.Private);
        }

        internal static SourceMemberFieldSymbolFromDesignation Create(
                SourceMemberContainerTypeSymbol containingType,
                SingleVariableDesignationSyntax designation,
                DeclarationModifiers modifiers,
                FieldSymbol containingFieldOpt,
                SyntaxNode nodeToBind)
        {
            Debug.Assert(nodeToBind.Kind() == SyntaxKind.VariableDeclarator || nodeToBind is ExpressionSyntax);
            var typeSyntax = ((TypedVariableComponentSyntax)designation.Parent).Type;
            return typeSyntax.IsVar
                ? new SourceMemberFieldSymbolFromDesignationWithEnclosingContext(containingType, designation, modifiers, containingFieldOpt, nodeToBind)
                : new SourceMemberFieldSymbolFromDesignation(containingType, designation, modifiers);
        }

        protected override SyntaxList<AttributeListSyntax> AttributeDeclarationSyntaxList
        {
            get
            {
                return default(SyntaxList<AttributeListSyntax>);
            }
        }

        public SingleVariableDesignationSyntax VariableDesignation
        {
            get
            {
                return (SingleVariableDesignationSyntax)this.SyntaxNode;
            }
        }
        
        protected override TypeSyntax TypeSyntax
        {
            get
            {
                return ((TypedVariableComponentSyntax)VariableDesignation.Parent).Type;
            }
        }

        protected override SyntaxTokenList ModifiersTokenList
        {
            get
            {
                return default(SyntaxTokenList);
            }
        }

        internal override TypeSymbol GetFieldType(ConsList<FieldSymbol> fieldsBeingBound)
        {
            Debug.Assert(fieldsBeingBound != null);

            if ((object)_lazyType != null)
            {
                return _lazyType;
            }

            var designation = VariableDesignation;
            var typeSyntax = TypeSyntax;

            var compilation = this.DeclaringCompilation;

            var diagnostics = DiagnosticBag.GetInstance();
            TypeSymbol type;

            var binderFactory = compilation.GetBinderFactory(SyntaxTree);
            var binder = binderFactory.GetBinder(typeSyntax);

            bool isVar;
            type = binder.BindType(typeSyntax, diagnostics, out isVar);

            Debug.Assert((object)type != null || isVar);

            if (isVar && !fieldsBeingBound.ContainsReference(this))
            {
                InferFieldType(fieldsBeingBound, binder);
                Debug.Assert((object)_lazyType != null);
            }
            else
            {
                if (isVar)
                {
                    diagnostics.Add(ErrorCode.ERR_RecursivelyTypedVariable, this.ErrorLocation, this);
                    type = binder.CreateErrorType("var");
                }

                SetType(compilation, diagnostics, type);
            }

            diagnostics.Free();
            return _lazyType;
        }

        /// <summary>
        /// Can add some diagnostics into <paramref name="diagnostics"/>. 
        /// </summary>
        private void SetType(CSharpCompilation compilation, DiagnosticBag diagnostics, TypeSymbol type)
        {
            TypeSymbol originalType = _lazyType;

            // In the event that we race to set the type of a field, we should
            // always deduce the same type, unless the cached type is an error.

            Debug.Assert((object)originalType == null ||
                originalType.IsErrorType() ||
                originalType == type);

            if ((object)Interlocked.CompareExchange(ref _lazyType, type, null) == null)
            {
                TypeChecks(type, diagnostics);

                compilation.DeclarationDiagnostics.AddRange(diagnostics);
                state.NotePartComplete(CompletionPart.Type);
            }
        }

        /// <summary>
        /// Can add some diagnostics into <paramref name="diagnostics"/>. 
        /// </summary>
        internal void SetType(TypeSymbol type, DiagnosticBag diagnostics)
        {
            SetType(DeclaringCompilation, diagnostics, type);
        }

        protected virtual void InferFieldType(ConsList<FieldSymbol> fieldsBeingBound, Binder binder)
        {
            throw ExceptionUtilities.Unreachable;
        }

        protected override ConstantValue MakeConstantValue(HashSet<SourceFieldSymbolWithSyntaxReference> dependencies, bool earlyDecodingWellKnownAttributes, DiagnosticBag diagnostics)
        {
            return null;
        }

        public override bool HasInitializer
        {
            get
            {
                return false;
            }
        }

        private class SourceMemberFieldSymbolFromDesignationWithEnclosingContext : SourceMemberFieldSymbolFromDesignation
        {
            private readonly FieldSymbol _containingFieldOpt;
            private readonly SyntaxReference _nodeToBind;


            internal SourceMemberFieldSymbolFromDesignationWithEnclosingContext(
                SourceMemberContainerTypeSymbol containingType,
                SingleVariableDesignationSyntax designation,
                DeclarationModifiers modifiers,
                FieldSymbol containingFieldOpt,
                SyntaxNode nodeToBind)
                : base(containingType, designation, modifiers)
            {
                Debug.Assert(nodeToBind.Kind() == SyntaxKind.VariableDeclarator || nodeToBind is ExpressionSyntax);
                _containingFieldOpt = containingFieldOpt;
                _nodeToBind = nodeToBind.GetReference();
            }

            protected override void InferFieldType(ConsList<FieldSymbol> fieldsBeingBound, Binder binder)
            {
                var nodeToBind = _nodeToBind.GetSyntax();

                if ((object)_containingFieldOpt != null && nodeToBind.Kind() != SyntaxKind.VariableDeclarator)
                {
                    binder = binder.WithContainingMemberOrLambda(_containingFieldOpt).WithAdditionalFlags(BinderFlags.FieldInitializer);
                }

                fieldsBeingBound = new ConsList<FieldSymbol>(this, fieldsBeingBound);

                binder = new ImplicitlyTypedFieldBinder(binder, fieldsBeingBound);
                var diagnostics = DiagnosticBag.GetInstance();

                switch (nodeToBind.Kind())
                {
                    case SyntaxKind.VariableDeclarator:
                        // This occurs, for example, in
                        // int x, y[out var Z, 1 is int I];
                        // for (int x, y[out var Z, 1 is int I]; ;) {}
                        binder.BindDeclaratorArguments((VariableDeclaratorSyntax)nodeToBind, diagnostics);
                        break;

                    default:
                        binder.BindExpression((ExpressionSyntax)nodeToBind, diagnostics);
                        break;
                }

                diagnostics.Free();
            }
        }
    }
}