﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.Emit;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
    public class HostedRuntimeEnvironment : IDisposable
    {
        private static readonly Dictionary<string, Guid> s_allModuleNames = new Dictionary<string, Guid>();

        private bool _disposed;
        private AppDomain _domain;
        private RuntimeAssemblyManager _assemblyManager;
        private ImmutableArray<Diagnostic> _lazyDiagnostics;
        private ModuleData _mainModule;
        private ImmutableArray<byte> _mainModulePdb;
        private List<ModuleData> _allModuleData;
        private readonly CompilationTestData _testData = new CompilationTestData();
        private readonly IEnumerable<ModuleData> _additionalDependencies;
        private bool _executeRequested;
        private bool _peVerifyRequested;

        public HostedRuntimeEnvironment(IEnumerable<ModuleData> additionalDependencies = null)
        {
            _additionalDependencies = additionalDependencies;
        }

        private void CreateAssemblyManager(IEnumerable<ModuleData> compilationDependencies, ModuleData mainModule)
        {
            var allModules = compilationDependencies;
            if (_additionalDependencies != null)
            {
                allModules = allModules.Concat(_additionalDependencies);
            }

            // We need to add the main module so that it gets checked against already loaded assembly names.
            // If an assembly is loaded directly via PEVerify(image) another assembly of the same full name
            // can't be loaded as a dependency (via Assembly.ReflectionOnlyLoad) in the same domain.
            if (mainModule != null)
            {
                allModules = allModules.Concat(new[] { mainModule });
            }

            allModules = allModules.ToArray();

            string conflict = DetectNameCollision(allModules);
            if (conflict != null && !MonoHelpers.IsRunningOnMono())
            {
                var appDomainProxyType = typeof(RuntimeAssemblyManager);
                var thisAssembly = appDomainProxyType.Assembly;

                AppDomain appDomain = null;
                RuntimeAssemblyManager manager;
                try
                {
                    appDomain = AppDomainUtils.Create("HostedRuntimeEnvironment");
                    manager = (RuntimeAssemblyManager)appDomain.CreateInstanceAndUnwrap(thisAssembly.FullName, appDomainProxyType.FullName);
                }
                catch
                {
                    if (appDomain != null)
                    {
                        AppDomain.Unload(appDomain);
                    }
                    throw;
                }

                _domain = appDomain;
                _assemblyManager = manager;
            }
            else
            {
                _assemblyManager = new RuntimeAssemblyManager();
            }

            _assemblyManager.AddModuleData(allModules);

            if (mainModule != null)
            {
                _assemblyManager.AddMainModuleMvid(mainModule.Mvid);
            }
        }

        // Determines if any of the given dependencies has the same name as already loaded assembly with different content.
        private static string DetectNameCollision(IEnumerable<ModuleData> modules)
        {
            lock (s_allModuleNames)
            {
                foreach (var module in modules)
                {
                    Guid mvid;
                    if (s_allModuleNames.TryGetValue(module.FullName, out mvid))
                    {
                        if (mvid != module.Mvid)
                        {
                            return module.FullName;
                        }
                    }
                }

                // only add new modules if there is no collision:
                foreach (var module in modules)
                {
                    s_allModuleNames[module.FullName] = module.Mvid;
                }
            }

            return null;
        }

        private static void EmitDependentCompilation(Compilation compilation,
                                                     List<ModuleData> dependencies,
                                                     DiagnosticBag diagnostics,
                                                     bool usePdbForDebugging = false)
        {
            ImmutableArray<byte> assembly, pdb;
            if (EmitCompilation(compilation, null, dependencies, diagnostics, null, out assembly, out pdb))
            {
                dependencies.Add(new ModuleData(compilation.Assembly.Identity,
                                                OutputKind.DynamicallyLinkedLibrary,
                                                assembly,
                                                pdb: usePdbForDebugging ? pdb : default(ImmutableArray<byte>),
                                                inMemoryModule: true));
            }
        }

        internal static void EmitReferences(Compilation compilation, List<ModuleData> dependencies, DiagnosticBag diagnostics)
        {
            var previousSubmission = compilation.ScriptCompilationInfo?.PreviousScriptCompilation;
            if (previousSubmission != null)
            {
                EmitDependentCompilation(previousSubmission, dependencies, diagnostics);
            }

            foreach (MetadataReference r in compilation.References)
            {
                CompilationReference compilationRef;
                PortableExecutableReference peRef;

                if ((compilationRef = r as CompilationReference) != null)
                {
                    EmitDependentCompilation(compilationRef.Compilation, dependencies, diagnostics);
                }
                else if ((peRef = r as PortableExecutableReference) != null)
                {
                    var metadata = peRef.GetMetadata();
                    bool isManifestModule = peRef.Properties.Kind == MetadataImageKind.Assembly;
                    foreach (var module in EnumerateModules(metadata))
                    {
                        ImmutableArray<byte> bytes = module.Module.PEReaderOpt.GetEntireImage().GetContent();
                        if (isManifestModule)
                        {
                            dependencies.Add(new ModuleData(((AssemblyMetadata)metadata).GetAssembly().Identity,
                                                            OutputKind.DynamicallyLinkedLibrary,
                                                            bytes,
                                                            pdb: default(ImmutableArray<byte>),
                                                            inMemoryModule: true));
                        }
                        else
                        {
                            dependencies.Add(new ModuleData(module.Name,
                                                            bytes,
                                                            pdb: default(ImmutableArray<byte>),
                                                            inMemoryModule: true));
                        }

                        isManifestModule = false;
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }

        private static IEnumerable<ModuleMetadata> EnumerateModules(Metadata metadata)
        {
            return (metadata.Kind == MetadataImageKind.Assembly) ? ((AssemblyMetadata)metadata).GetModules().AsEnumerable() : SpecializedCollections.SingletonEnumerable((ModuleMetadata)metadata);
        }

        internal static bool EmitCompilation(
            Compilation compilation,
            IEnumerable<ResourceDescription> manifestResources,
            List<ModuleData> dependencies,
            DiagnosticBag diagnostics,
            CompilationTestData testData,
            out ImmutableArray<byte> assembly,
            out ImmutableArray<byte> pdb
        )
        {
            assembly = default(ImmutableArray<byte>);
            pdb = default(ImmutableArray<byte>);

            EmitReferences(compilation, dependencies, diagnostics);

            using (var executableStream = new MemoryStream())
            {
                MemoryStream pdbStream = MonoHelpers.IsRunningOnMono()
                    ? null
                    : new MemoryStream();

                EmitResult result;
                try
                {
                    result = compilation.Emit(
                        executableStream,
                        pdbStream: pdbStream,
                        xmlDocumentationStream: null,
                        win32Resources: null,
                        manifestResources: manifestResources,
                        options: EmitOptions.Default,
                        debugEntryPoint: null,
                        testData: testData,
                        getHostDiagnostics: null,
                        cancellationToken: default(CancellationToken));
                }
                finally
                {
                    if (pdbStream != null)
                    {
                        pdb = pdbStream.ToImmutable();
                        pdbStream.Dispose();
                    }
                }

                diagnostics.AddRange(result.Diagnostics);
                assembly = executableStream.ToImmutable();

                return result.Success;
            }
        }

        public void Emit(
            Compilation mainCompilation,
            IEnumerable<ResourceDescription> manifestResources,
            bool usePdbForDebugging = false)
        {
            var diagnostics = DiagnosticBag.GetInstance();
            var dependencies = new List<ModuleData>();

            _testData.Methods.Clear();

            ImmutableArray<byte> mainImage, mainPdb;
            bool succeeded = EmitCompilation(mainCompilation, manifestResources, dependencies, diagnostics, _testData, out mainImage, out mainPdb);

            _lazyDiagnostics = diagnostics.ToReadOnlyAndFree();

            if (succeeded)
            {
                _mainModule = new ModuleData(mainCompilation.Assembly.Identity,
                                                 mainCompilation.Options.OutputKind,
                                                 mainImage,
                                                 pdb: usePdbForDebugging ? mainPdb : default(ImmutableArray<byte>),
                                                 inMemoryModule: true);
                _mainModulePdb = mainPdb;
                _allModuleData = dependencies;
                _allModuleData.Insert(0, _mainModule);
                CreateAssemblyManager(dependencies, _mainModule);
            }
            else
            {
                string dumpDir;
                RuntimeAssemblyManager.DumpAssemblyData(dependencies, out dumpDir);

                // This method MUST throw if compilation did not succeed.  If compilation succeeded and there were errors, that is bad.
                // Please see KevinH if you intend to change this behavior as many tests expect the Exception to indicate failure.
                throw new EmitException(_lazyDiagnostics, dumpDir); // ToArray for serializability.
            }
        }

        public int Execute(string moduleName, int expectedOutputLength, out string processOutput)
        {
            _executeRequested = true;

            try
            {
                return _assemblyManager.Execute(moduleName, expectedOutputLength, out processOutput);
            }
            catch (TargetInvocationException tie)
            {
                string dumpDir;
                _assemblyManager.DumpAssemblyData(out dumpDir);
                throw new ExecutionException(tie.InnerException, dumpDir);
            }
        }

        public int Execute(string moduleName, string expectedOutput)
        {
            string actualOutput;
            int exitCode = Execute(moduleName, expectedOutput.Length, out actualOutput);

            if (expectedOutput.Trim() != actualOutput.Trim())
            {
                string dumpDir;
                _assemblyManager.DumpAssemblyData(out dumpDir);
                throw new ExecutionException(expectedOutput, actualOutput, dumpDir);
            }

            return exitCode;
        }

        internal ImmutableArray<Diagnostic> GetDiagnostics()
        {
            if (_lazyDiagnostics.IsDefault)
            {
                throw new InvalidOperationException("You must call Emit before calling GetBuffer.");
            }

            return _lazyDiagnostics;
        }

        public ImmutableArray<byte> GetMainImage()
        {
            if (_mainModule == null)
            {
                throw new InvalidOperationException("You must call Emit before calling GetMainImage.");
            }

            return _mainModule.Image;
        }

        public ImmutableArray<byte> GetMainPdb()
        {
            if (_mainModule == null)
            {
                throw new InvalidOperationException("You must call Emit before calling GetMainPdb.");
            }

            return _mainModulePdb;
        }

        internal IList<ModuleData> GetAllModuleData()
        {
            if (_allModuleData == null)
            {
                throw new InvalidOperationException("You must call Emit before calling GetAllModuleData.");
            }

            return _allModuleData;
        }

        public void PeVerify()
        {
            _peVerifyRequested = true;

            if (_assemblyManager == null)
            {
                throw new InvalidOperationException("You must call Emit before calling PeVerify.");
            }

            _assemblyManager.PeVerifyModules(new[] { _mainModule.FullName });
        }

        internal string[] PeVerifyModules(string[] modulesToVerify, bool throwOnError = true)
        {
            _peVerifyRequested = true;

            if (_assemblyManager == null)
            {
                CreateAssemblyManager(new ModuleData[0], null);
            }

            return _assemblyManager.PeVerifyModules(modulesToVerify, throwOnError);
        }

        internal SortedSet<string> GetMemberSignaturesFromMetadata(string fullyQualifiedTypeName, string memberName)
        {
            if (_assemblyManager == null)
            {
                throw new InvalidOperationException("You must call Emit before calling GetMemberSignaturesFromMetadata.");
            }

            return _assemblyManager.GetMemberSignaturesFromMetadata(fullyQualifiedTypeName, memberName);
        }

        // A workaround for known bug DevDiv 369979 - don't unload the AppDomain if we may have loaded a module
        private bool IsSafeToUnloadDomain
        {
            get
            {
                if (_assemblyManager == null)
                {
                    return true;
                }

                return !(_assemblyManager.ContainsNetModules() && (_peVerifyRequested || _executeRequested));
            }
        }

        void IDisposable.Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_domain == null)
            {
                if (_assemblyManager != null)
                {
                    _assemblyManager.Dispose();
                    _assemblyManager = null;
                }
            }
            else
            {
                Debug.Assert(_assemblyManager != null);
                _assemblyManager.Dispose();

                if (IsSafeToUnloadDomain)
                {
                    AppDomain.Unload(_domain);
                }

                _assemblyManager = null;
                _domain = null;
            }

            _disposed = true;
        }

        internal CompilationTestData GetCompilationTestData()
        {
            if (_testData.Module == null)
            {
                throw new InvalidOperationException("You must call Emit before calling GetCompilationTestData.");
            }
            return _testData;
        }
    }
}
