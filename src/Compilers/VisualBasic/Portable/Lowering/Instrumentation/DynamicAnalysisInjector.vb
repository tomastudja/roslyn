﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports Microsoft.CodeAnalysis.CodeGen
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Roslyn.Utilities
Imports System.Collections.Immutable
Imports System.Diagnostics

Namespace Microsoft.CodeAnalysis.VisualBasic

    ''' <summary>
    ''' This type provides means for instrumenting compiled methods for dynamic analysis.
    ''' It can be combined with other <see cref= "Instrumenter" /> s.
    ''' </summary>
    Friend NotInheritable Class DynamicAnalysisInjector
        Inherits CompoundInstrumenter

        Private ReadOnly _method As MethodSymbol
        Private ReadOnly _methodBody As BoundStatement
        Private ReadOnly _createPayload As MethodSymbol
        Private ReadOnly _spansBuilder As ArrayBuilder(Of SourceSpan)
        Private _dynamisAnalysisSpans As ImmutableArray(Of SourceSpan) = ImmutableArray(Of SourceSpan).Empty
        Private ReadOnly _methodEntryInstrumentation As BoundStatement
        Private ReadOnly _payloadType As ArrayTypeSymbol
        Private ReadOnly _methodPayload As LocalSymbol
        Private ReadOnly _diagnostics As DiagnosticBag
        Private ReadOnly _debugDocumentProvider As DebugDocumentProvider
        Private ReadOnly _methodHasExplicitBlock As Boolean
        Private ReadOnly _factory As SyntheticBoundNodeFactory

        Public Shared Function TryCreate(method As MethodSymbol, methodBody As BoundStatement, factory As SyntheticBoundNodeFactory, diagnostics As DiagnosticBag, debugDocumentProvider As DebugDocumentProvider, previous As Instrumenter) As DynamicAnalysisInjector
            Dim createPayload As MethodSymbol = GetCreatePayload(factory.Compilation, methodBody.Syntax, diagnostics)

            ' Do Not instrument the instrumentation helpers if they are part of the current compilation (which occurs only during testing). GetCreatePayload will fail with an infinite recursion if it Is instrumented.
            If DirectCast(createPayload, Object) IsNot Nothing AndAlso Not method.IsImplicitlyDeclared AndAlso Not method.Equals(createPayload) Then
                Return New DynamicAnalysisInjector(method, methodBody, factory, createPayload, diagnostics, debugDocumentProvider, previous)
            End If

            Return Nothing
        End Function

        Private Sub New(methoD As MethodSymbol, methodBody As BoundStatement, factory As SyntheticBoundNodeFactory, createPayload As MethodSymbol, diagnostics As DiagnosticBag, debugDocumentProvider As DebugDocumentProvider, previous As Instrumenter)
            MyBase.New(previous)
            _createPayload = createPayload
            _method = methoD
            _methodBody = methodBody
            _spansBuilder = ArrayBuilder(Of SourceSpan).GetInstance()
            Dim payloadElementType As TypeSymbol = factory.SpecialType(SpecialType.System_Boolean)
            _payloadType = ArrayTypeSymbol.CreateVBArray(payloadElementType, ImmutableArray(Of CustomModifier).Empty, 1, factory.Compilation.Assembly)
            _methodPayload = factory.SynthesizedLocal(_payloadType, kind:=SynthesizedLocalKind.InstrumentationPayload, syntax:=methodBody.Syntax)
            _diagnostics = diagnostics
            _debugDocumentProvider = debugDocumentProvider
            _methodHasExplicitBlock = MethodHasExplicitBlock(methoD)
            _factory = factory

            ' The first point indicates entry into the method And has the span of the method definition.
            _methodEntryInstrumentation = AddAnalysisPoint(MethodDeclarationIfAvailable(methodBody.Syntax), factory)
        End Sub

        Public Overrides Function CreateBlockPrologue(original As BoundBlock, ByRef synthesizedLocal As LocalSymbol) As BoundStatement
            Dim previousPrologue As BoundStatement = MyBase.CreateBlockPrologue(original, synthesizedLocal)
#If False Then
            If _methodBody Is original Then
                _dynamicAnalysisSpans = _spansBuilder.ToImmutableAndFree()
                ' In the future there will be multiple analysis kinds.
                Const analysisKind As Integer = 0

                Dim modulePayloadType As ArrayTypeSymbol = ArrayTypeSymbol.CreateVBArray(_payloadType, ImmutableArray(Of CustomModifier).Empty, 1, _factory.Compilation.Assembly)

                ' Synthesize the initialization of the instrumentation payload array, using concurrency-safe code
                '
                ' Dim payload = PID.PayloadRootField[methodIndex]
                ' If payload Is Nothing Then
                '     payload = Instrumentation.CreatePayload(mvid, methodIndex, ref PID.PayloadRootField(methodIndex), payloadLength)
                ' End If

                Dim payloadInitialization As BoundStatement = _factory.Assignment(_factory.Local(_methodPayload), _factory.ArrayAccess(_factory.InstrumentationPayloadRoot(analysisKind, modulePayloadType), ImmutableArray.Create(_factory.MethodDefIndex(_method))))
                Dim mvid As BoundExpression = _factory.ModuleVersionId()
                Dim methodToken As BoundExpression = _factory.MethodDefIndex(_method)
                Dim payloadSlot As BoundExpression = _factory.ArrayAccess(_factory.InstrumentationPayloadRoot(analysisKind, modulePayloadType), ImmutableArray.Create(_factory.MethodDefIndex(_method)))
                Dim createPayloadCall As BoundStatement = _factory.Assignment(_factory.Local(_methodPayload), _factory.Call(null, _createPayload, mvid, methodToken, payloadSlot, _factory.Literal(_dynamicAnalysisSpans.Length)))

                Dim payloadNullTest As BoundExpression = _factory.Binary(BinaryOperatorKind.ObjectEqual, _factory.SpecialType(SpecialType.System_Boolean), _factory.Local(_methodPayload), _factory.Null(_payloadType))
                Dim payloadIf As BoundStatement = _factory.If(payloadNullTest, createPayloadCall)

                Debug.Assert(synthesizedLocal Is Nothing)
                synthesizedLocal = _methodPayload

                Return If(previousPrologue Is Nothing,
                           factory.StatementList(payloadInitialization, payloadIf, _methodEntryInstrumentation),
                           _factory.StatementList(payloadInitialization, payloadIf, _methodEntryInstrumentation, previousPrologue))
            End If
#End If

            Return previousPrologue
        End Function

        Public Overrides Function InstrumentExpressionStatement(original As BoundExpressionStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentExpressionStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentStopStatement(original As BoundStopStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentStopStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentEndStatement(original As BoundEndStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentEndStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentContinueStatement(original As BoundContinueStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentContinueStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentExitStatement(original As BoundExitStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentExitStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentGotoStatement(original As BoundGotoStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentGotoStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentRaiseEventStatement(original As BoundRaiseEventStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentRaiseEventStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentReturnStatement(original As BoundReturnStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentReturnStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentThrowStatement(original As BoundThrowStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentThrowStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentOnErrorStatement(original As BoundOnErrorStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentOnErrorStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentResumeStatement(original As BoundResumeStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentResumeStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentAddHandlerStatement(original As BoundAddHandlerStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentAddHandlerStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentRemoveHandlerStatement(original As BoundRemoveHandlerStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentRemoveHandlerStatement(original, rewritten))
        End Function

        Public Overrides Function InstrumentSyncLockObjectCapture(original As BoundSyncLockStatement, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentSyncLockObjectCapture(original, rewritten))
        End Function

        Public Overrides Function InstrumentWhileStatementConditionalGotoStart(original As BoundWhileStatement, ifConditionGotoStart As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentWhileStatementConditionalGotoStart(original, ifConditionGotoStart))
        End Function

        Public Overrides Function InstrumentDoLoopStatementEntryOrConditionalGotoStart(original As BoundDoLoopStatement, ifConditionGotoStartOpt As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentDoLoopStatementEntryOrConditionalGotoStart(original, ifConditionGotoStartOpt))
        End Function

        Public Overrides Function InstrumentIfStatementConditionalGoto(original As BoundIfStatement, condGoto As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentIfStatementConditionalGoto(original, condGoto))
        End Function

        Public Overrides Function CreateSelectStatementPrologue(original As BoundSelectStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.CreateSelectStatementPrologue(original))
        End Function

        Public Overrides Function InstrumentFieldOrPropertyInitializer(original As BoundFieldOrPropertyInitializer, rewritten As BoundStatement, symbolIndex As Integer, createTemporary As Boolean) As BoundStatement
            rewritten = MyBase.InstrumentFieldOrPropertyInitializer(original, rewritten, symbolIndex, createTemporary)
            Dim syntax As VisualBasicSyntaxNode = original.Syntax

            Select Case Syntax.Parent.Parent.Kind()
                Case SyntaxKind.VariableDeclarator, SyntaxKind.PropertyStatement
                    Return AddDynamicAnalysis(original, rewritten)

                Case Else
                    Throw ExceptionUtilities.UnexpectedValue(Syntax.Parent.Parent.Kind())
            End Select
        End Function

        Public Overrides Function InstrumentForEachLoopInitialization(original As BoundForEachStatement, initialization As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentForEachLoopInitialization(original, initialization))
        End Function

        Public Overrides Function InstrumentForLoopInitialization(original As BoundForToStatement, initialization As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentForLoopInitialization(original, initialization))
        End Function

        Public Overrides Function InstrumentLocalInitialization(original As BoundLocalDeclaration, rewritten As BoundStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.InstrumentLocalInitialization(original, rewritten))
        End Function

        Public Overrides Function CreateUsingStatementPrologue(original As BoundUsingStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.CreateUsingStatementPrologue(original))
        End Function

        Public Overrides Function CreateWithStatementPrologue(original As BoundWithStatement) As BoundStatement
            Return AddDynamicAnalysis(original, MyBase.CreateWithStatementPrologue(original))
        End Function

#If False Then

       
        public override BoundStatement CreateBlockPrologue(BoundBlock original, out LocalSymbol synthesizedLocal)
        {
            if (_methodBody == original)
            {
                _dynamicAnalysisSpans = _spansBuilder.ToImmutableAndFree();
                // In the future there will be multiple analysis kinds.
                const int analysisKind = 0;

                ArrayTypeSymbol modulePayloadType = ArrayTypeSymbol.CreateCSharpArray(_factory.Compilation.Assembly, _payloadType);

                // Synthesize the initialization of the instrumentation payload array, using concurrency-safe code:
                //
                // var payload = PID.PayloadRootField[methodIndex];
                // if (payload == null)
                //     payload = Instrumentation.CreatePayload(mvid, methodIndex, ref PID.PayloadRootField[methodIndex], payloadLength);

                BoundStatement payloadInitialization = _factory.Assignment(_factory.Local(_methodPayload), _factory.ArrayAccess(_factory.InstrumentationPayloadRoot(analysisKind, modulePayloadType), ImmutableArray.Create(_factory.MethodDefIndex(_method))));
                BoundExpression mvid = _factory.ModuleVersionId();
                BoundExpression methodToken = _factory.MethodDefIndex(_method);
                BoundExpression payloadSlot = _factory.ArrayAccess(_factory.InstrumentationPayloadRoot(analysisKind, modulePayloadType), ImmutableArray.Create(_factory.MethodDefIndex(_method)));
                BoundStatement createPayloadCall = _factory.Assignment(_factory.Local(_methodPayload), _factory.Call(null, _createPayload, mvid, methodToken, payloadSlot, _factory.Literal(_dynamicAnalysisSpans.Length)));

                BoundExpression payloadNullTest = _factory.Binary(BinaryOperatorKind.ObjectEqual, _factory.SpecialType(SpecialType.System_Boolean), _factory.Local(_methodPayload), _factory.Null(_payloadType));
                BoundStatement payloadIf = _factory.If(payloadNullTest, createPayloadCall);

                BoundStatement previousPrologue = base.CreateBlockPrologue(original, out synthesizedLocal);
                Debug.Assert(synthesizedLocal == null);

                synthesizedLocal = _methodPayload;

                return previousPrologue == null
                     ? _factory.StatementList(payloadInitialization, payloadIf, _methodEntryInstrumentation)
                     : _factory.StatementList(payloadInitialization, payloadIf, _methodEntryInstrumentation, previousPrologue);
            }

            synthesizedLocal = null;
            return null;
        }

        public ImmutableArray<SourceSpan> DynamicAnalysisSpans => _dynamicAnalysisSpans;

        public override BoundStatement InstrumentNoOpStatement(BoundNoOpStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentNoOpStatement(original, rewritten));
        }

        public override BoundStatement InstrumentBreakStatement(BoundBreakStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentBreakStatement(original, rewritten));
        }

        public override BoundStatement InstrumentContinueStatement(BoundContinueStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentContinueStatement(original, rewritten));
        }

        public override BoundStatement InstrumentExpressionStatement(BoundExpressionStatement original, BoundStatement rewritten)
        {
            rewritten = base.InstrumentExpressionStatement(original, rewritten);

            if (!_methodHasExplicitBlock)
            {
                // The assignment statement for a property set method defined without a block is compiler generated, but requires instrumentation.
                return CollectDynamicAnalysis(original, rewritten);
            }

            return AddDynamicAnalysis(original, rewritten);
        }

        public override BoundStatement InstrumentFieldOrPropertyInitializer(BoundExpressionStatement original, BoundStatement rewritten)
        {
            rewritten = base.InstrumentExpressionStatement(original, rewritten);
            CSharpSyntaxNode syntax = original.Syntax;

            switch (syntax.Parent.Parent.Kind())
            {
                case SyntaxKind.VariableDeclarator:
                case SyntaxKind.PropertyDeclaration:
                    return AddDynamicAnalysis(original, rewritten);

                default:
                    throw ExceptionUtilities.UnexpectedValue(syntax.Parent.Parent.Kind());
            }
        }

        public override BoundStatement InstrumentGotoStatement(BoundGotoStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentGotoStatement(original, rewritten));
        }

        public override BoundStatement InstrumentThrowStatement(BoundThrowStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentThrowStatement(original, rewritten));
        }

        public override BoundStatement InstrumentYieldBreakStatement(BoundYieldBreakStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentYieldBreakStatement(original, rewritten));
        }

        public override BoundStatement InstrumentYieldReturnStatement(BoundYieldReturnStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentYieldReturnStatement(original, rewritten));
        }

        public override BoundStatement InstrumentForEachStatementIterationVarDeclaration(BoundForEachStatement original, BoundStatement iterationVarDecl)
        {
            return AddDynamicAnalysis(original, base.InstrumentForEachStatementIterationVarDeclaration(original, iterationVarDecl));
        }
        
        public override BoundStatement InstrumentIfStatement(BoundIfStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentIfStatement(original, rewritten));
        }

        public override BoundStatement InstrumentWhileStatementConditionalGotoStart(BoundWhileStatement original, BoundStatement ifConditionGotoStart)
        {
            return AddDynamicAnalysis(original, base.InstrumentWhileStatementConditionalGotoStart(original, ifConditionGotoStart));
        }

        public override BoundStatement InstrumentLocalInitialization(BoundLocalDeclaration original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentLocalInitialization(original, rewritten));
        }

        public override BoundStatement InstrumentLockTargetCapture(BoundLockStatement original, BoundStatement lockTargetCapture)
        {
            return AddDynamicAnalysis(original, base.InstrumentLockTargetCapture(original, lockTargetCapture));
        }

        public override BoundStatement InstrumentReturnStatement(BoundReturnStatement original, BoundStatement rewritten)
        {
            rewritten = base.InstrumentReturnStatement(original, rewritten);

            // A synthesized return statement that does not return a value never requires instrumentation.
            // A property set method defined without a block has such a synthesized return statement.
            if (!_methodHasExplicitBlock && ((BoundReturnStatement)original).ExpressionOpt != null)
            {
                // The return statement for value-returning methods defined without a block is compiler generated, but requires instrumentation.
                return CollectDynamicAnalysis(original, rewritten);
            }

            return AddDynamicAnalysis(original, rewritten);
        }

        public override BoundStatement InstrumentSwitchStatement(BoundSwitchStatement original, BoundStatement rewritten)
        {
            return AddDynamicAnalysis(original, base.InstrumentSwitchStatement(original, rewritten));
        }

        public override BoundStatement InstrumentUsingTargetCapture(BoundUsingStatement original, BoundStatement usingTargetCapture)
        {
            return AddDynamicAnalysis(original, base.InstrumentUsingTargetCapture(original, usingTargetCapture));
        }
        
       

        
        
       

       
        
    }
}
#End If
        Private Function AddDynamicAnalysis(original As BoundStatement, rewritten As BoundStatement) As BoundStatement
            If Not original.WasCompilerGenerated Then
                Return CollectDynamicAnalysis(original, rewritten)
            End If

            Return rewritten
        End Function

        Private Function CollectDynamicAnalysis(original As BoundStatement, rewritten As BoundStatement) As BoundStatement
            Dim statementFactory As New SyntheticBoundNodeFactory(_factory.TopLevelMethod, _method, original.Syntax, _factory.CompilationState, _diagnostics)
            Return statementFactory.Block(ImmutableArray.Create(AddAnalysisPoint(SyntaxForSpan(original), statementFactory), rewritten))
        End Function

        Private Function AddAnalysisPoint(syntaxForSpan As VisualBasicSyntaxNode, statementFactory As SyntheticBoundNodeFactory) As BoundStatement
            ' Add an entry in the spans array.

            Dim location As Location = syntaxForSpan.GetLocation()
            Dim spanPosition As FileLinePositionSpan = location.GetMappedLineSpan()
            Dim path As String = spanPosition.Path
            If path.Length = 0 Then
                path = syntaxForSpan.SyntaxTree.FilePath
            End If

            Dim spansIndex As Integer = _spansBuilder.Count
            _spansBuilder.Add(New SourceSpan(_debugDocumentProvider.Invoke(path, ""), spanPosition.StartLinePosition.Line, spanPosition.StartLinePosition.Character, spanPosition.EndLinePosition.Line, spanPosition.EndLinePosition.Character))

            ' Generate "_payload(pointIndex) = True".

            Dim payloadCell As BoundArrayAccess = statementFactory.ArrayAccess(statementFactory.Local(_methodPayload, False), True, statementFactory.Literal(spansIndex))
            Return statementFactory.Assignment(payloadCell, statementFactory.Literal(True))
        End Function

        Private Shared Function SyntaxForSpan(statement As BoundStatement) As VisualBasicSyntaxNode
            SyntaxForSpan = statement.Syntax

            Select Case statement.Kind
                Case BoundKind.IfStatement
                    SyntaxForSpan = DirectCast(statement, BoundIfStatement).Condition.Syntax
                Case BoundKind.WhileStatement
                    SyntaxForSpan = DirectCast(statement, BoundWhileStatement).Condition.Syntax
                Case BoundKind.ForEachStatement
                    SyntaxForSpan = DirectCast(statement, BoundForEachStatement).Collection.Syntax
                Case BoundKind.DoLoopStatement
                    Dim condition As BoundExpression = DirectCast(statement, BoundDoLoopStatement).ConditionOpt
                    If condition IsNot Nothing Then
                        SyntaxForSpan = condition.Syntax
                    End If
                Case BoundKind.UsingStatement
                    Dim usingStatement As BoundUsingStatement = DirectCast(statement, BoundUsingStatement)
                    SyntaxForSpan = If(usingStatement.ResourceExpressionOpt IsNot Nothing, DirectCast(usingStatement.ResourceExpressionOpt, BoundNode), usingStatement).Syntax
                Case BoundKind.SyncLockStatement
                    SyntaxForSpan = DirectCast(statement, BoundSyncLockStatement).LockExpression.Syntax
                Case BoundKind.SelectStatement
                    SyntaxForSpan = DirectCast(statement, BoundSelectStatement).ExpressionStatement.Expression.Syntax
                Case Else
                    SyntaxForSpan = statement.Syntax
            End Select
        End Function

        Private Shared Function MethodHasExplicitBlock(method As MethodSymbol) As Boolean
            Dim asSourceMethod As SourceMethodSymbol = TryCast(method.OriginalDefinition, SourceMethodSymbol)
            If DirectCast(asSourceMethod, Object) IsNot Nothing Then
                Return TypeOf asSourceMethod.Syntax Is MethodBlockSyntax
            End If
            Return False
        End Function

        Private Shared Function GetCreatePayload(compilation As VisualBasicCompilation, syntax As VisualBasicSyntaxNode, diagnostics As DiagnosticBag) As MethodSymbol
            Return DirectCast(Binder.GetWellKnownTypeMember(compilation, WellKnownMember.Microsoft_CodeAnalysis_Runtime_Instrumentation__CreatePayload, syntax, diagnostics), MethodSymbol)
        End Function

        Private Shared Function MethodDeclarationIfAvailable(body As VisualBasicSyntaxNode) As VisualBasicSyntaxNode
            Dim parent As VisualBasicSyntaxNode = body.Parent
            If parent IsNot Nothing Then
                Select Case (parent.Kind())
                    Case SyntaxKind.FunctionBlock
                    Case SyntaxKind.SubBlock
                    Case SyntaxKind.PropertyBlock
                    Case SyntaxKind.GetAccessorBlock
                    Case SyntaxKind.SetAccessorBlock
                        Return parent
                End Select
            End If

            Return body
        End Function
    End Class
End Namespace
