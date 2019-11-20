﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.ExtractMethod;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.ExtractMethod
{
    internal partial class CSharpMethodExtractor
    {
        private partial class CSharpCodeGenerator
        {
            public class SingleStatementCodeGenerator : CSharpCodeGenerator
            {
                public SingleStatementCodeGenerator(
                    InsertionPoint insertionPoint,
                    SelectionResult selectionResult,
                    AnalyzerResult analyzerResult,
                    bool localFunction)
                    : base(insertionPoint, selectionResult, analyzerResult, localFunction)
                {
                }

                public static bool IsExtractMethodOnSingleStatement(SelectionResult code)
                {
                    var result = (CSharpSelectionResult)code;
                    var firstStatement = result.GetFirstStatement();
                    var lastStatement = result.GetLastStatement();

                    return firstStatement == lastStatement || firstStatement.Span.Contains(lastStatement.Span);
                }

                protected override SyntaxToken CreateMethodName(bool localFunction)
                {
                    // change this to more smarter one.
                    var semanticModel = this.SemanticDocument.SemanticModel;
                    var nameGenerator = new UniqueNameGenerator(semanticModel);
                    var scope = this.CSharpSelectionResult.GetContainingScope();

                    // If extracting a local function, we want to ensure all local variables are considered when generating a unique name.
                    if (localFunction)
                    {
                        scope = this.CSharpSelectionResult.GetFirstTokenInSelection().Parent;
                    }

                    return SyntaxFactory.Identifier(nameGenerator.CreateUniqueMethodName(scope, "NewMethod"));
                }

                protected override IEnumerable<StatementSyntax> GetInitialStatementsForMethodDefinitions()
                {
                    Contract.ThrowIfFalse(IsExtractMethodOnSingleStatement(this.CSharpSelectionResult));

                    return SpecializedCollections.SingletonEnumerable<StatementSyntax>(this.CSharpSelectionResult.GetFirstStatement());
                }

                protected override SyntaxNode GetOutermostCallSiteContainerToProcess(CancellationToken cancellationToken)
                {
                    var callSiteContainer = GetCallSiteContainerFromOutermostMoveInVariable(cancellationToken);
                    if (callSiteContainer != null)
                    {
                        return callSiteContainer;
                    }
                    else
                    {
                        var firstStatement = this.CSharpSelectionResult.GetFirstStatement();
                        return firstStatement.Parent;
                    }
                }

                protected override SyntaxNode GetFirstStatementOrInitializerSelectedAtCallSite()
                {
                    return this.CSharpSelectionResult.GetFirstStatement();
                }

                protected override SyntaxNode GetLastStatementOrInitializerSelectedAtCallSite()
                {
                    // it is a single statement case. either first statement is same as last statement or
                    // last statement belongs (embedded statement) to the first statement.
                    return this.CSharpSelectionResult.GetFirstStatement();
                }

                protected override Task<SyntaxNode> GetStatementOrInitializerContainingInvocationToExtractedMethodAsync(
                    SyntaxAnnotation callSiteAnnotation, CancellationToken cancellationToken)
                {
                    var statement = GetStatementContainingInvocationToExtractedMethodWorker();
                    return Task.FromResult<SyntaxNode>(statement.WithAdditionalAnnotations(callSiteAnnotation));
                }
            }
        }
    }
}
