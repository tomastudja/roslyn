﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using static TestReferences.NetFx;
using static Roslyn.Test.Utilities.TestMetadata;
using Microsoft.CodeAnalysis.Test.Resources.Proprietary;
using Basic.Reference.Assemblies;

namespace Roslyn.Test.Utilities
{
    /// <summary>
    /// Base class for all unit test classes.
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        private TempRoot _temp;

        protected TestBase()
        {
        }

        public static string GetUniqueName()
        {
            return Guid.NewGuid().ToString("D");
        }

        public TempRoot Temp
        {
            get
            {
                if (_temp == null)
                {
                    _temp = new TempRoot();
                }

                return _temp;
            }
        }

        public virtual void Dispose()
        {
            if (_temp != null)
            {
                _temp.Dispose();
            }
        }

        #region Metadata References

        /// <summary>
        /// Helper for atomically acquiring and saving a metadata reference. Necessary
        /// if the acquired reference will ever be used in object identity comparisons.
        /// </summary>
        private static MetadataReference GetOrCreateMetadataReference(ref MetadataReference field, Func<MetadataReference> getReference)
        {
            if (field == null)
            {
                Interlocked.CompareExchange(ref field, getReference(), null);
            }
            return field;
        }

        private static readonly Lazy<MetadataReference[]> s_lazyDefaultVbReferences = new Lazy<MetadataReference[]>(
            () => new[] { Net40.mscorlib, Net40.System, Net40.SystemCore, Net40.MicrosoftVisualBasic },
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference[] DefaultVbReferences => s_lazyDefaultVbReferences.Value;

        private static readonly Lazy<MetadataReference[]> s_lazyLatestVbReferences = new Lazy<MetadataReference[]>(
            () => new[] { Net451.mscorlib, Net451.System, Net451.SystemCore, Net451.MicrosoftVisualBasic },
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference[] LatestVbReferences => s_lazyLatestVbReferences.Value;

        public static readonly AssemblyName RuntimeCorLibName = RuntimeUtilities.IsCoreClrRuntime
            ? new AssemblyName("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51")
            : new AssemblyName("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

        /// <summary>
        /// The array of 7 metadataimagereferences that are required to compile
        /// against windows.winmd (including windows.winmd itself).
        /// </summary>
        private static readonly Lazy<MetadataReference[]> s_winRtRefs = new Lazy<MetadataReference[]>(
            () =>
            {
                var winmd = AssemblyMetadata.CreateFromImage(TestResources.WinRt.Windows).GetReference(display: "Windows");

                var windowsruntime =
                    AssemblyMetadata.CreateFromImage(ProprietaryTestResources.v4_0_30319_17929.System_Runtime_WindowsRuntime).GetReference(display: "System.Runtime.WindowsRuntime.dll");

                var runtime =
                    AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemRuntime).GetReference(display: "System.Runtime.dll");

                var objectModel =
                    AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemObjectModel).GetReference(display: "System.ObjectModel.dll");

                var uixaml = AssemblyMetadata.CreateFromImage(ProprietaryTestResources.v4_0_30319_17929.System_Runtime_WindowsRuntime_UI_Xaml).
                    GetReference(display: "System.Runtime.WindowsRuntime.UI.Xaml.dll");

                var interop = AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemRuntimeInteropServicesWindowsRuntime).
                    GetReference(display: "System.Runtime.InteropServices.WindowsRuntime.dll");

                //Not mentioned in the adapter doc but pointed to from System.Runtime, so we'll put it here.
                var system = AssemblyMetadata.CreateFromImage(ResourcesNet451.System).GetReference(display: "System.dll");

                var mscor = AssemblyMetadata.CreateFromImage(ResourcesNet451.mscorlib).GetReference(display: "mscorlib");

                return new MetadataReference[] { winmd, windowsruntime, runtime, objectModel, uixaml, interop, system, mscor };
            },
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference[] WinRtRefs => s_winRtRefs.Value;

        /// <summary>
        /// The array of minimal references for portable library (mscorlib.dll and System.Runtime.dll)
        /// </summary>
        private static readonly Lazy<MetadataReference[]> s_portableRefsMinimal = new Lazy<MetadataReference[]>(
            () => new MetadataReference[] { MscorlibPP7Ref, SystemRuntimePP7Ref },
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference[] PortableRefsMinimal => s_portableRefsMinimal.Value;

        /// <summary>
        /// Reference to an assembly that defines LINQ operators.
        /// </summary>
        public static MetadataReference LinqAssemblyRef => SystemCoreRef;

        /// <summary>
        /// Reference to an assembly that defines ExtensionAttribute.
        /// </summary>
        public static MetadataReference ExtensionAssemblyRef => SystemCoreRef;

        private static readonly Lazy<MetadataReference> s_systemCoreRef =
            new Lazy<MetadataReference>(
                () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemCore).GetReference(display: "System.Core.v4_0_30319.dll"),
                LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemCoreRef => s_systemCoreRef.Value;

        private static readonly Lazy<MetadataReference> s_systemCoreRef_v4_0_30319_17929 = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemCore).GetReference(display: "System.Core.v4_0_30319_17929.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemCoreRef_v4_0_30319_17929 => s_systemCoreRef_v4_0_30319_17929.Value;

        private static readonly Lazy<MetadataReference> s_systemCoreRef_v46 = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet461.SystemCore).GetReference(display: "System.Core.v4_6_1038_0.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemCoreRef_v46 => s_systemCoreRef_v4_0_30319_17929.Value;

        private static readonly Lazy<MetadataReference> s_systemWindowsFormsRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemWindowsForms).GetReference(display: "System.Windows.Forms.v4_0_30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemWindowsFormsRef => s_systemWindowsFormsRef.Value;

        private static readonly Lazy<MetadataReference> s_systemDrawingRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemDrawing).GetReference(display: "System.Drawing.v4_0_30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemDrawingRef => s_systemDrawingRef.Value;

        private static readonly Lazy<MetadataReference> s_systemDataRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemData).GetReference(display: "System.Data.v4_0_30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemDataRef => s_systemDataRef.Value;

        private static readonly Lazy<MetadataReference> s_mscorlibRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.mscorlib).GetReference(display: "mscorlib.v4_0_30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference MscorlibRef => s_mscorlibRef.Value;

        private static readonly Lazy<MetadataReference> s_mscorlibRefPortable = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ProprietaryTestResources.v4_0_30319.mscorlib_portable).GetReference(display: "mscorlib.v4_0_30319.portable.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference MscorlibRefPortable => s_mscorlibRefPortable.Value;

        private static readonly Lazy<MetadataReference> s_aacorlibRef = new Lazy<MetadataReference>(
            () =>
            {
                var source = TestResources.NetFX.aacorlib_v15_0_3928.aacorlib_v15_0_3928_cs;
                var syntaxTree = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseSyntaxTree(source);

                var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

                var compilation = CSharpCompilation.Create("aacorlib.v15.0.3928.dll", new[] { syntaxTree }, null, compilationOptions);

                Stream dllStream = new MemoryStream();
                var emitResult = compilation.Emit(dllStream);
                if (!emitResult.Success)
                {
                    emitResult.Diagnostics.Verify();
                }
                dllStream.Seek(0, SeekOrigin.Begin);

                return AssemblyMetadata.CreateFromStream(dllStream).GetReference(display: "mscorlib.v4_0_30319.dll");
            },
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference AacorlibRef => s_aacorlibRef.Value;

        public static MetadataReference MscorlibRef_v20 => Net20.mscorlib;

        public static MetadataReference MscorlibRef_v4_0_30316_17626 => Net451.mscorlib;

        private static readonly Lazy<MetadataReference> s_mscorlibRef_v46 = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet461.mscorlib).GetReference(display: "mscorlib.v4_6_1038_0.dll", filePath: @"Z:\FxReferenceAssembliesUri"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference MscorlibRef_v46 => s_mscorlibRef_v46.Value;

        /// <summary>
        /// Reference to an mscorlib silverlight assembly in which the System.Array does not contain the special member LongLength.
        /// </summary>
        private static readonly Lazy<MetadataReference> s_mscorlibRef_silverlight = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ProprietaryTestResources.silverlight_v5_0_5_0.mscorlib_v5_0_5_0_silverlight).GetReference(display: "mscorlib.v5.0.5.0_silverlight.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference MscorlibRefSilverlight => s_mscorlibRef_silverlight.Value;

        public static MetadataReference MinCorlibRef => TestReferences.NetFx.Minimal.mincorlib;

        public static MetadataReference MinAsyncCorlibRef => TestReferences.NetFx.Minimal.minasynccorlib;

        public static MetadataReference ValueTupleRef => TestReferences.NetFx.ValueTuple.tuplelib;

        public static MetadataReference MsvbRef => Net451.MicrosoftVisualBasic;

        public static MetadataReference MsvbRef_v4_0_30319_17929 => Net451.MicrosoftVisualBasic;

        public static MetadataReference CSharpRef => CSharpDesktopRef;

        private static readonly Lazy<MetadataReference> s_desktopCSharpRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.MicrosoftCSharp).GetReference(display: "Microsoft.CSharp.v4.0.30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference CSharpDesktopRef => s_desktopCSharpRef.Value;

        private static readonly Lazy<MetadataReference> s_std20Ref = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(NetStandard20.Resources.netstandard).GetReference(display: "netstandard20.netstandard.dll"),
            LazyThreadSafetyMode.PublicationOnly);

        public static MetadataReference NetStandard20Ref => s_std20Ref.Value;

        private static readonly Lazy<MetadataReference> s_46NetStandardFacade = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesBuildExtensions.NetStandardToNet461).GetReference(display: "netstandard20.netstandard.dll"),
            LazyThreadSafetyMode.PublicationOnly);

        public static MetadataReference Net46StandardFacade => s_46NetStandardFacade.Value;

        private static readonly Lazy<MetadataReference> s_systemRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.System).GetReference(display: "System.v4_0_30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemRef => s_systemRef.Value;

        private static readonly Lazy<MetadataReference> s_systemRef_v46 = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet461.System).GetReference(display: "System.v4_6_1038_0.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemRef_v46 => s_systemRef_v46.Value;

        private static readonly Lazy<MetadataReference> s_systemRef_v4_0_30319_17929 = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.System).GetReference(display: "System.v4_0_30319_17929.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemRef_v4_0_30319_17929 => s_systemRef_v4_0_30319_17929.Value;

        private static readonly Lazy<MetadataReference> s_systemRef_v20 = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet20.System).GetReference(display: "System.v2_0_50727.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemRef_v20 => s_systemRef_v20.Value;

        private static readonly Lazy<MetadataReference> s_systemXmlRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemXml).GetReference(display: "System.Xml.v4_0_30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemXmlRef => s_systemXmlRef.Value;

        private static readonly Lazy<MetadataReference> s_systemXmlLinqRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemXmlLinq).GetReference(display: "System.Xml.Linq.v4_0_30319.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemXmlLinqRef => s_systemXmlLinqRef.Value;

        private static readonly Lazy<MetadataReference> s_mscorlibFacadeRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.mscorlib).GetReference(display: "mscorlib.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference MscorlibFacadeRef => s_mscorlibFacadeRef.Value;

        private static readonly Lazy<MetadataReference> s_systemRuntimeFacadeRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemRuntime).GetReference(display: "System.Runtime.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemRuntimeFacadeRef => s_systemRuntimeFacadeRef.Value;

        private static readonly Lazy<MetadataReference> s_systemThreadingFacadeRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemThreading).GetReference(display: "System.Threading.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemThreadingFacadeRef => s_systemThreadingTasksFacadeRef.Value;

        private static readonly Lazy<MetadataReference> s_systemThreadingTasksFacadeRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ResourcesNet451.SystemThreadingTasks).GetReference(display: "System.Threading.Tasks.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemThreadingTaskFacadeRef => s_systemThreadingTasksFacadeRef.Value;

        private static readonly Lazy<MetadataReference> s_mscorlibPP7Ref = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ProprietaryTestResources.ReferenceAssemblies_PortableProfile7.mscorlib).GetReference(display: "mscorlib.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference MscorlibPP7Ref => s_mscorlibPP7Ref.Value;

        private static readonly Lazy<MetadataReference> s_systemRuntimePP7Ref = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(ProprietaryTestResources.ReferenceAssemblies_PortableProfile7.System_Runtime).GetReference(display: "System.Runtime.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference SystemRuntimePP7Ref => s_systemRuntimePP7Ref.Value;

        private static readonly Lazy<MetadataReference> s_FSharpTestLibraryRef = new Lazy<MetadataReference>(
            () => AssemblyMetadata.CreateFromImage(TestResources.General.FSharpTestLibrary).GetReference(display: "FSharpTestLibrary.dll"),
            LazyThreadSafetyMode.PublicationOnly);
        public static MetadataReference FSharpTestLibraryRef => s_FSharpTestLibraryRef.Value;

        public static readonly MetadataReference InvalidRef = new TestMetadataReference(fullPath: @"R:\Invalid.dll");

        #endregion

        #region Diagnostics

        internal static DiagnosticDescription Diagnostic(
            object code,
            string squiggledText = null,
            object[] arguments = null,
            LinePosition? startLocation = null,
            Func<SyntaxNode, bool> syntaxNodePredicate = null,
            bool argumentOrderDoesNotMatter = false,
            bool isSuppressed = false)
        {
            return TestHelpers.Diagnostic(
                code,
                squiggledText,
                arguments,
                startLocation,
                syntaxNodePredicate,
                argumentOrderDoesNotMatter,
                isSuppressed: isSuppressed);
        }

        internal static DiagnosticDescription Diagnostic(
           object code,
           XCData squiggledText,
           object[] arguments = null,
           LinePosition? startLocation = null,
           Func<SyntaxNode, bool> syntaxNodePredicate = null,
           bool argumentOrderDoesNotMatter = false,
           bool isSuppressed = false)
        {
            return TestHelpers.Diagnostic(
                code,
                squiggledText,
                arguments,
                startLocation,
                syntaxNodePredicate,
                argumentOrderDoesNotMatter,
                isSuppressed: isSuppressed);
        }

        #endregion
    }
}
