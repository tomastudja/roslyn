﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Scripting
{
    /// <summary>
    /// Represents a runtime execution context for scripts.
    /// </summary>
    internal sealed class ScriptBuilder
    {
        /// <summary>
        /// Unique prefix for generated assemblies.
        /// </summary>
        /// <remarks>
        /// The full names of uncollectible assemblies generated by this context must be unique,
        /// so that we can resolve references among them. Note that CLR can load two different assemblies of the very same 
        /// identity into the same load context.
        /// 
        /// We are using a certain naming scheme for the generated assemblies (a fixed name prefix followed by a number). 
        /// If we allowed the compiled code to add references that match this exact pattern it might happen that 
        /// the user supplied reference identity conflicts with the identity we use for our generated assemblies and 
        /// the AppDomain assembly resolve event won't be able to correctly identify the target assembly.
        /// 
        /// To avoid this problem we use a prefix for assemblies we generate that is unlikely to conflict with user specified references.
        /// We also check that no user provided references are allowed to be used in the compiled code and report an error ("reserved assembly name").
        /// </remarks>
        private static readonly string s_globalAssemblyNamePrefix;
        private static int s_engineIdDispenser;
        private int _submissionIdDispenser = -1;
        private readonly string _assemblyNamePrefix;

        private readonly InteractiveAssemblyLoader _assemblyLoader;

        private static readonly EmitOptions s_EmitOptionsWithDebuggingInformation = new EmitOptions(
            debugInformationFormat: PdbHelpers.GetPlatformSpecificDebugInformationFormat(),
            pdbChecksumAlgorithm: default(HashAlgorithmName));

        static ScriptBuilder()
        {
            s_globalAssemblyNamePrefix = "\u211B*" + Guid.NewGuid().ToString();
        }

        public ScriptBuilder(InteractiveAssemblyLoader assemblyLoader)
        {
            Debug.Assert(assemblyLoader != null);

            _assemblyNamePrefix = s_globalAssemblyNamePrefix + "#" + Interlocked.Increment(ref s_engineIdDispenser).ToString();
            _assemblyLoader = assemblyLoader;
        }

        public int GenerateSubmissionId(out string assemblyName, out string typeName)
        {
            int id = Interlocked.Increment(ref _submissionIdDispenser);
            string idAsString = id.ToString();
            assemblyName = _assemblyNamePrefix + "-" + idAsString;
            typeName = "Submission#" + idAsString;
            return id;
        }

        /// <exception cref="CompilationErrorException">Compilation has errors.</exception>
        internal Func<object[], Task<T>> CreateExecutor<T>(ScriptCompiler compiler, Compilation compilation, bool emitDebugInformation, CancellationToken cancellationToken)
        {
            var diagnostics = DiagnosticBag.GetInstance();
            try
            {
                // get compilation diagnostics first.
                diagnostics.AddRange(compilation.GetParseDiagnostics(cancellationToken));
                ThrowIfAnyCompilationErrors(diagnostics, compiler.DiagnosticFormatter);
                diagnostics.Clear();

                var executor = Build<T>(compilation, diagnostics, emitDebugInformation, cancellationToken);

                // emit can fail due to compilation errors or because there is nothing to emit:
                ThrowIfAnyCompilationErrors(diagnostics, compiler.DiagnosticFormatter);

                executor ??= (s) => Task.FromResult(default(T));

                return executor;
            }
            finally
            {
                diagnostics.Free();
            }
        }

        private static void ThrowIfAnyCompilationErrors(DiagnosticBag diagnostics, DiagnosticFormatter formatter)
        {
            if (diagnostics.IsEmptyWithoutResolution)
            {
                return;
            }
            var filtered = diagnostics.AsEnumerable().Where(d => d.Severity == DiagnosticSeverity.Error).AsImmutable();
            if (filtered.IsEmpty)
            {
                return;
            }
            throw new CompilationErrorException(
                formatter.Format(filtered[0], CultureInfo.CurrentCulture),
                filtered);
        }

        /// <summary>
        /// Builds a delegate that will execute just this scripts code.
        /// </summary>
        private Func<object[], Task<T>> Build<T>(
            Compilation compilation,
            DiagnosticBag diagnostics,
            bool emitDebugInformation,
            CancellationToken cancellationToken)
        {
            var entryPoint = compilation.GetEntryPoint(cancellationToken);

            using (var peStream = new MemoryStream())
            using (var pdbStreamOpt = emitDebugInformation ? new MemoryStream() : null)
            {
                var emitResult = Emit(peStream, pdbStreamOpt, compilation, GetEmitOptions(emitDebugInformation), cancellationToken);
                diagnostics.AddRange(emitResult.Diagnostics);

                if (!emitResult.Success)
                {
                    return null;
                }

                // let the loader know where to find assemblies:
                foreach (var referencedAssembly in compilation.GetBoundReferenceManager().GetReferencedAssemblies())
                {
                    var path = (referencedAssembly.Key as PortableExecutableReference)?.FilePath;
                    if (path != null)
                    {
                        // TODO: Should the #r resolver return contract metadata and runtime assembly path -
                        // Contract assembly used in the compiler, RT assembly path here.
                        _assemblyLoader.RegisterDependency(referencedAssembly.Value.Identity, path);
                    }
                }

                peStream.Position = 0;

                if (pdbStreamOpt != null)
                {
                    pdbStreamOpt.Position = 0;
                }

                var assembly = _assemblyLoader.LoadAssemblyFromStream(peStream, pdbStreamOpt);
                var runtimeEntryPoint = GetEntryPointRuntimeMethod(entryPoint, assembly);

                return runtimeEntryPoint.CreateDelegate<Func<object[], Task<T>>>();
            }
        }

        // internal for testing
        internal static EmitOptions GetEmitOptions(bool emitDebugInformation)
            => emitDebugInformation ? s_EmitOptionsWithDebuggingInformation : EmitOptions.Default;

        // internal for testing
        internal static EmitResult Emit(
            Stream peStream,
            Stream pdbStreamOpt,
            Compilation compilation,
            EmitOptions options,
            CancellationToken cancellationToken)
        {
            return compilation.Emit(
                peStream: peStream,
                pdbStream: pdbStreamOpt,
                xmlDocumentationStream: null,
                win32Resources: null,
                manifestResources: null,
                options: options,
                cancellationToken: cancellationToken);
        }

        internal static MethodInfo GetEntryPointRuntimeMethod(IMethodSymbol entryPoint, Assembly assembly)
        {
            string entryPointTypeName = MetadataHelpers.BuildQualifiedName(entryPoint.ContainingNamespace.MetadataName, entryPoint.ContainingType.MetadataName);
            string entryPointMethodName = entryPoint.MetadataName;

            var entryPointType = assembly.GetType(entryPointTypeName, throwOnError: true, ignoreCase: false).GetTypeInfo();
            return entryPointType.GetDeclaredMethod(entryPointMethodName);
        }
    }
}
