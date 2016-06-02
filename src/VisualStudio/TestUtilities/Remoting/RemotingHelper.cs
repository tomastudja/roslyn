﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.LanguageServices.CSharp.Interactive;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Roslyn.Hosting.Diagnostics.Waiters;

namespace Roslyn.VisualStudio.Test.Utilities.Remoting
{
    /// <summary>
    /// Provides a set of helper functions for accessing services in the Visual Studio host process.
    /// </summary>
    /// <remarks>
    /// These methods should be executed Visual Studio host via the <see cref="VisualStudioInstance.ExecuteOnHostProcess"/> method.
    /// </remarks>
    internal static class RemotingHelper
    {
        private static readonly Guid RoslynPackageId = new Guid("6cf2e545-6109-4730-8883-cf43d7aec3e1");

        private static readonly string[] SupportedLanguages = new string[] { LanguageNames.CSharp, LanguageNames.VisualBasic };

        public static IComponentModel ComponentModel => GetGlobalService<IComponentModel>(typeof(SComponentModel));

        public static IInteractiveWindow CSharpInteractiveWindow => CSharpVsInteractiveWindow.InteractiveWindow;

        public static VisualStudioWorkspace VisualStudioWorkspace => ComponentModel.GetService<VisualStudioWorkspace>();

        private static IVsInteractiveWindow CSharpVsInteractiveWindow => InvokeOnUIThread(() => CSharpVsInteractiveWindowProvider.Open(0, true));

        private static CSharpVsInteractiveWindowProvider CSharpVsInteractiveWindowProvider => ComponentModel.GetService<CSharpVsInteractiveWindowProvider>();

        private static Application CurrentApplication => Application.Current;

        private static Dispatcher CurrentApplicationDispatcher => CurrentApplication.Dispatcher;

        private static ExportProvider DefaultComponentModelExportProvider => ComponentModel.DefaultExportProvider;

        public static DTE DTE => GetGlobalService<DTE>(typeof(SDTE));

        private static ServiceProvider GlobalServiceProvider => ServiceProvider.GlobalProvider;

        private static IVsShell VsShell => GetGlobalService<IVsShell>(typeof(SVsShell));

        private static IVsTextManager VsTextManager => GetGlobalService<IVsTextManager>(typeof(SVsTextManager));

        public static void ActivateMainWindow()
        {
            InvokeOnUIThread(() =>
            {
                var activeVisualStudioWindow = (IntPtr)IntegrationHelper.RetryRpcCall(() => DTE.ActiveWindow.HWnd);
                Debug.WriteLine($"DTE.ActiveWindow.HWnd = {activeVisualStudioWindow}");

                if (activeVisualStudioWindow == IntPtr.Zero)
                {
                    activeVisualStudioWindow = (IntPtr)IntegrationHelper.RetryRpcCall(() => DTE.MainWindow.HWnd);
                    Debug.WriteLine($"DTE.MainWindow.HWnd = {activeVisualStudioWindow}");
                }

                IntegrationHelper.SetForegroundWindow(activeVisualStudioWindow);
            });
        }

        private static TestingOnly_WaitingService WaitingService => DefaultComponentModelExportProvider.GetExport<TestingOnly_WaitingService>().Value;

        public static void WaitForAsyncOperations(string featuresToWaitFor, bool waitForWorkspaceFirst = true)
        {
            WaitingService.WaitForAsyncOperations(featuresToWaitFor, waitForWorkspaceFirst);
        }

        public static void WaitForAllAsyncOperations()
        {
            WaitingService.WaitForAllAsyncOperations();
        }

        public static void CleanupWaitingService()
        {
            var asynchronousOperationWaiterExports = DefaultComponentModelExportProvider.GetExports<IAsynchronousOperationWaiter>();

            if (!asynchronousOperationWaiterExports.Any())
            {
                throw new InvalidOperationException("The test waiting service could not be located.");
            }

            WaitingService.EnableActiveTokenTracking(true);
        }

        public static void CleanupWorkspace()
        {
            LoadRoslynPackage();
            VisualStudioWorkspace.TestHookPartialSolutionsDisabled = true;
        }

        public static void WaitForSystemIdle()
        {
            CurrentApplicationDispatcher.Invoke(() => { }, DispatcherPriority.SystemIdle);
        }

        /// <summary>
        /// Waiting for the application to 'idle' means that it is done pumping messages (including WM_PAINT).
        /// </summary>
        public static void WaitForApplicationIdle()
        {
            CurrentApplicationDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }

        private static T GetGlobalService<T>(Type serviceType)
        {
            return InvokeOnUIThread(() =>
            {
                return (T)(GlobalServiceProvider.GetService(serviceType));
            });
        }

        public static void InvokeOnUIThread(Action action)
        {
            CurrentApplicationDispatcher.Invoke(action);
        }

        public static T InvokeOnUIThread<T>(Func<T> action)
        {
            return CurrentApplicationDispatcher.Invoke(action);
        }

        private static void LoadRoslynPackage()
        {
            var roslynPackageGuid = RoslynPackageId;
            IVsPackage roslynPackage = null;

            var hresult = VsShell.LoadPackage(ref roslynPackageGuid, out roslynPackage);
            Marshal.ThrowExceptionForHR(hresult);
        }
    }
}
