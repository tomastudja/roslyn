﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.ImplementInterface;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Simplification;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.ImplementInterface
{
    [ExportLanguageService(typeof(IImplementInterfaceService), LanguageNames.CSharp), Shared]
    internal class CSharpImplementInterfaceService : AbstractImplementInterfaceService
    {
        [ImportingConstructor]
        public CSharpImplementInterfaceService()
        {
        }

        protected override bool TryInitializeState(
            Document document, SemanticModel model, SyntaxNode node, CancellationToken cancellationToken,
            out SyntaxNode classOrStructDecl, out INamedTypeSymbol classOrStructType, out IEnumerable<INamedTypeSymbol> interfaceTypes)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                if (node is TypeSyntax interfaceNode && interfaceNode.Parent is BaseTypeSyntax baseType &&
                    baseType.IsParentKind(SyntaxKind.BaseList) &&
                    baseType.Type == interfaceNode)
                {
                    if (interfaceNode.Parent.Parent.IsParentKind(SyntaxKind.ClassDeclaration) ||
                        interfaceNode.Parent.Parent.IsParentKind(SyntaxKind.StructDeclaration))
                    {
                        var interfaceSymbolInfo = model.GetSymbolInfo(interfaceNode, cancellationToken);
                        if (interfaceSymbolInfo.CandidateReason != CandidateReason.WrongArity)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (interfaceSymbolInfo.GetAnySymbol() is INamedTypeSymbol interfaceType && interfaceType.TypeKind == TypeKind.Interface)
                            {
                                classOrStructDecl = interfaceNode.Parent.Parent.Parent as TypeDeclarationSyntax;
                                classOrStructType = model.GetDeclaredSymbol(classOrStructDecl, cancellationToken) as INamedTypeSymbol;
                                interfaceTypes = SpecializedCollections.SingletonEnumerable(interfaceType);

                                return interfaceTypes != null && classOrStructType != null;
                            }
                        }
                    }
                }
            }

            classOrStructDecl = null;
            classOrStructType = null;
            interfaceTypes = null;
            return false;
        }

        protected override bool CanImplementImplicitly => true;

        protected override bool HasHiddenExplicitImplementation => true;

        private static ClassDeclarationSyntax GetClassDeclarationAt(SyntaxNode root, int position)
        {
            var node = root.FindToken(position).Parent.FirstAncestorOrSelf((SyntaxNode n) => n.IsKind(SyntaxKind.ClassDeclaration));
            return node as ClassDeclarationSyntax;
        }

        protected override Document ImplementDisposePattern(
            Document document, SyntaxNode root,
            INamedTypeSymbol classSymbol, IFieldSymbol disposedValueField,
            int position, bool explicitly)
        {
            var classDecl = GetClassDeclarationAt(root, position);

            // Generate the IDisposable boilerplate code.  The generated code cannot be one giant resource string
            // because of the need to parse, format, and simplify the result; during pseudo-localized builds, resource
            // strings are given a special prefix and suffix that will break the parser, hence the requirement to
            // localize the comments individually.
            var code = $@"
    {(classSymbol.IsSealed ? "" : "protected virtual ")}void Dispose(bool disposing)
    {{
        if (!{disposedValueField.Name})
        {{
            if (disposing)
            {{
                // {FeaturesResources.TODO_colon_dispose_managed_state_managed_objects}
            }}

            // {CSharpFeaturesResources.TODO_colon_free_unmanaged_resources_unmanaged_objects_and_override_a_finalizer_below}
            // {FeaturesResources.TODO_colon_set_large_fields_to_null}
            {disposedValueField.Name} = true;
        }}
    }}

    // {CSharpFeaturesResources.TODO_colon_override_a_finalizer_only_if_Dispose_bool_disposing_above_has_code_to_free_unmanaged_resources}
    // ~{classDecl.Identifier.Value}()
    // {{
    //   // {CSharpFeaturesResources.Do_not_change_this_code_Put_cleanup_code_in_Dispose_bool_disposing_above}
    //   Dispose(false);
    // }}

    {(explicitly ? "void System.IDisposable." : "public void ")}Dispose()
    {{
        // {CSharpFeaturesResources.Do_not_change_this_code_Put_cleanup_code_in_Dispose_bool_disposing_above}
        Dispose(true);";

            if (classSymbol.IsSealed)
            {
                code += @$"
        // {CSharpFeaturesResources.TODO_colon_uncomment_the_following_line_if_the_finalizer_is_overridden_above}
        // GC.SuppressFinalize(this);
    }}
";
            }
            else
            {
                code += @$"
        GC.SuppressFinalize(this);
    }}
";
            }

            var decls = SyntaxFactory.ParseSyntaxTree(code)
                .GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>()
                .Select(decl => decl.WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation))
                .ToArray();

            // Ensure that open and close brace tokens are generated in case they are missing.
            var newNode = classDecl.EnsureOpenAndCloseBraceTokens().AddMembers(decls);

            return document.WithSyntaxRoot(root.ReplaceNode(classDecl, newNode));
        }
    }
}
