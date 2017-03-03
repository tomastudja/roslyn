﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Debugging;
using Microsoft.CodeAnalysis.Emit;
using Roslyn.Utilities;

namespace Microsoft.Cci
{
    internal sealed class CustomDebugInfoWriter
    {
        private MethodDefinitionHandle _methodWithModuleInfo;
        private IMethodBody _methodBodyWithModuleInfo;

        private MethodDefinitionHandle _previousMethodWithUsingInfo;
        private IMethodBody _previousMethodBodyWithUsingInfo;

        private readonly PdbWriter _pdbWriter;

        public CustomDebugInfoWriter(PdbWriter pdbWriter)
        {
            Debug.Assert(pdbWriter != null);
            _pdbWriter = pdbWriter;
        }

        /// <summary>
        /// Returns true if the namespace scope for this method should be forwarded to another method.
        /// Returns non-null <paramref name="forwardToMethod"/> if the forwarding should be done directly via UsingNamespace,
        /// null if the forwarding is done via custom debug info.
        /// </summary>
        public bool ShouldForwardNamespaceScopes(EmitContext context, IMethodBody methodBody, MethodDefinitionHandle methodHandle, out IMethodDefinition forwardToMethod)
        {
            if (ShouldForwardToPreviousMethodWithUsingInfo(context, methodBody))
            {
                // SerializeNamespaceScopeMetadata will do the actual forwarding in case this is a CSharp method.
                // VB on the other hand adds a "@methodtoken" to the scopes instead.
                if (context.Module.GenerateVisualBasicStylePdb)
                {
                    forwardToMethod = _previousMethodBodyWithUsingInfo.MethodDefinition;
                }
                else
                {
                    forwardToMethod = null;
                }

                return true;
            }

            _previousMethodBodyWithUsingInfo = methodBody;
            _previousMethodWithUsingInfo = methodHandle;
            forwardToMethod = null;
            return false;
        }

        public byte[] SerializeMethodDebugInfo(EmitContext context, IMethodBody methodBody, MethodDefinitionHandle methodHandle, bool emitEncInfo, bool suppressNewCustomDebugInfo, out bool emitExternNamespaces)
        {
            emitExternNamespaces = false;

            // CONSIDER: this may not be the same "first" method as in Dev10, but
            // it shouldn't matter since all methods will still forward to a method
            // containing the appropriate information.
            if (_methodBodyWithModuleInfo == null)
            {
                // This module level information could go on every method (and does in
                // the edit-and-continue case), but - as an optimization - we'll just
                // put it on the first method we happen to encounter and then put a
                // reference to the first method's token in every other method (so they
                // can find the information).
                if (context.Module.GetAssemblyReferenceAliases(context).Any())
                {
                    _methodWithModuleInfo = methodHandle;
                    _methodBodyWithModuleInfo = methodBody;
                    emitExternNamespaces = true;
                }
            }

            var pooledBuilder = PooledBlobBuilder.GetInstance();
            var encoder = new CustomDebugInfoEncoder(pooledBuilder);

            // NOTE: This is an attempt to match Dev10's apparent behavior.  For iterator methods (i.e. the method
            // that appears in source, not the synthesized ones), Dev10 only emits the ForwardIterator and IteratorLocal
            // custom debug info (e.g. there will be no information about the usings that were in scope).
            // NOTE: There seems to be an unusual behavior in ISymUnmanagedWriter where, if all the methods in a type are
            // iterator methods, no custom debug info is emitted for any method.  Adding a single non-iterator
            // method causes the custom debug info to be produced for all methods (including the iterator methods).
            // Since we are making the same ISymUnmanagedWriter calls as Dev10, we see the same behavior (i.e. this
            // is not a regression).
            if (methodBody.StateMachineTypeName != null)
            {
                encoder.AddForwardIteratorInfo(methodBody.StateMachineTypeName);
            }
            else
            {
                SerializeNamespaceScopeMetadata(ref encoder, context, methodBody);

                encoder.AddStateMachineHoistedLocalScopes(methodBody.StateMachineHoistedLocalScopes);
            }

            if (!suppressNewCustomDebugInfo)
            {
                SerializeDynamicLocalInfo(ref encoder, methodBody);
                SerializeTupleElementNames(ref encoder, methodBody);

                if (emitEncInfo)
                {
                    var encMethodInfo = MetadataWriter.GetEncMethodDebugInfo(methodBody);
                    SerializeCustomDebugInformation(ref encoder, encMethodInfo);
                }
            }

            byte[] result = encoder.ToArray();
            pooledBuilder.Free();
            return result;
        }

        // internal for testing
        internal static void SerializeCustomDebugInformation(ref CustomDebugInfoEncoder encoder, EditAndContinueMethodDebugInformation debugInfo)
        {
            // PERF: note that we pass debugInfo as explicit parameter
            //       that is intentional to avoid capturing debugInfo as that 
            //       would result in a lot of delegate allocations here that are otherwise can be avoided.
            if (!debugInfo.LocalSlots.IsDefaultOrEmpty)
            {
                encoder.AddRecord(
                    CustomDebugInfoKind.EditAndContinueLocalSlotMap, 
                    debugInfo,
                    (info, builder) => info.SerializeLocalSlots(builder));
            }

            if (!debugInfo.Lambdas.IsDefaultOrEmpty)
            {
                encoder.AddRecord(
                    CustomDebugInfoKind.EditAndContinueLambdaMap,
                    debugInfo,
                    (info, builder) => info.SerializeLambdaMap(builder));
            }
        }
        
        private static ArrayBuilder<T> GetLocalInfoToSerialize<T>(
            IMethodBody methodBody,
            Func<ILocalDefinition, bool> filter,
            Func<LocalScope, ILocalDefinition, T> getInfo)
        {
            ArrayBuilder<T> builder = null;

            foreach (var local in methodBody.LocalVariables)
            {
                Debug.Assert(local.SlotIndex >= 0);
                if (filter(local))
                {
                    if (builder == null)
                    {
                        builder = ArrayBuilder<T>.GetInstance();
                    }
                    builder.Add(getInfo(default(LocalScope), local));
                }
            }

            foreach (var currentScope in methodBody.LocalScopes)
            {
                foreach (var localConstant in currentScope.Constants)
                {
                    Debug.Assert(localConstant.SlotIndex < 0);
                    if (filter(localConstant))
                    {
                        if (builder == null)
                        {
                            builder = ArrayBuilder<T>.GetInstance();
                        }
                        builder.Add(getInfo(currentScope, localConstant));
                    }
                }
            }

            return builder;
        }

        private static void SerializeDynamicLocalInfo(ref CustomDebugInfoEncoder encoder, IMethodBody methodBody)
        {
            if (!methodBody.HasDynamicLocalVariables)
            {
                return;
            }

            byte[] GetDynamicFlags(ILocalDefinition local)
            {
                var dynamicTransformFlags = local.DynamicTransformFlags;
                var flags = new byte[CustomDebugInfoEncoder.DynamicAttributeSize];
                for (int k = 0; k < dynamicTransformFlags.Length; k++)
                {
                    if ((bool)dynamicTransformFlags[k].Value)
                    {
                        flags[k] = 1;
                    }
                }

                return flags;
            }

            var dynamicLocals = GetLocalInfoToSerialize(
                methodBody,
                local =>
                {
                    var dynamicTransformFlags = local.DynamicTransformFlags;
                    return !dynamicTransformFlags.IsEmpty &&
                        dynamicTransformFlags.Length <= CustomDebugInfoEncoder.DynamicAttributeSize &&
                        local.Name.Length < CustomDebugInfoEncoder.IdentifierSize;
                },
                (scope, local) => (local.Name, GetDynamicFlags(local), local.DynamicTransformFlags.Length, (local.SlotIndex < 0) ? 0 : local.SlotIndex));

            if (dynamicLocals == null)
            {
                return;
            }

            encoder.AddDynamicLocals(dynamicLocals);
            dynamicLocals.Free();
        }

        private static void SerializeTupleElementNames(ref CustomDebugInfoEncoder encoder, IMethodBody methodBody)
        {
            var locals = GetLocalInfoToSerialize(
                methodBody,
                local => !local.TupleElementNames.IsEmpty,
                (scope, local) => (local, scope));

            if (locals == null)
            {
                return;
            }

            encoder.AddRecord(CustomDebugInfoKind.TupleElementNames, locals, SerializeTupleElementNames);
            locals.Free();
        }

        private static void SerializeTupleElementNames(ArrayBuilder<(ILocalDefinition, LocalScope)> locals, BlobBuilder builder)
        {
            builder.WriteInt32(locals.Count);
            foreach (var t in locals)
            {
                // bug in C# compiler (https://github.com/dotnet/roslyn/issues/17518):
                var (local, scope) = t;

                var tupleElementNames = local.TupleElementNames;
                builder.WriteInt32(tupleElementNames.Length);
                foreach (var tupleElementName in tupleElementNames)
                {
                    builder.WriteUTF8((string)tupleElementName.Value ?? string.Empty);
                    builder.WriteByte(0);
                }

                builder.WriteInt32(local.SlotIndex);
                builder.WriteInt32(scope.StartOffset);
                builder.WriteInt32(scope.EndOffset);
                builder.WriteUTF8(local.Name);
                builder.WriteByte(0);
            }
        }

        private void SerializeNamespaceScopeMetadata(ref CustomDebugInfoEncoder encoder, EmitContext context, IMethodBody methodBody)
        {
            if (context.Module.GenerateVisualBasicStylePdb)
            {
                return;
            }

            if (ShouldForwardToPreviousMethodWithUsingInfo(context, methodBody))
            {
                Debug.Assert(!ReferenceEquals(_previousMethodBodyWithUsingInfo, methodBody));
                encoder.AddForwardMethodInfo(_previousMethodWithUsingInfo);
                return;
            }

            var usingCounts = ArrayBuilder<int>.GetInstance();
            for (IImportScope scope = methodBody.ImportScope; scope != null; scope = scope.Parent)
            {
                usingCounts.Add(scope.GetUsedNamespaces().Length);
            }

            encoder.AddUsingGroups(usingCounts);
            usingCounts.Free();

            if (_methodBodyWithModuleInfo != null && !ReferenceEquals(_methodBodyWithModuleInfo, methodBody))
            {
                encoder.AddForwardModuleInfo(_methodWithModuleInfo);
            }
        }

        private bool ShouldForwardToPreviousMethodWithUsingInfo(EmitContext context, IMethodBody methodBody)
        {
            if (_previousMethodBodyWithUsingInfo == null ||
                ReferenceEquals(_previousMethodBodyWithUsingInfo, methodBody))
            {
                return false;
            }

            // VB includes method namespace in namespace scopes:
            if (context.Module.GenerateVisualBasicStylePdb)
            {
                if (_pdbWriter.GetOrCreateSerializedNamespaceName(_previousMethodBodyWithUsingInfo.MethodDefinition.ContainingNamespace) !=
                    _pdbWriter.GetOrCreateSerializedNamespaceName(methodBody.MethodDefinition.ContainingNamespace))
                {
                    return false;
                }
            }

            var previousScopes = _previousMethodBodyWithUsingInfo.ImportScope;

            // methods share the same import scope (common case for methods declared in the same file)
            if (methodBody.ImportScope == previousScopes)
            {
                return true;
            }

            // If methods are in different files they don't share the same scopes,
            // but the imports might be the same nevertheless.
            // Note: not comparing project-level imports since those are the same for all method bodies.
            var s1 = methodBody.ImportScope;
            var s2 = previousScopes;
            while (s1 != null && s2 != null)
            {
                if (!s1.GetUsedNamespaces().SequenceEqual(s2.GetUsedNamespaces()))
                {
                    return false;
                }

                s1 = s1.Parent;
                s2 = s2.Parent;
            }

            return s1 == s2;
        }
    }
}
