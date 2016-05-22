﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.Win32;
using Roslyn.Utilities;
using Roslyn.VisualStudio.Test.Utilities.Interop;

using Process = System.Diagnostics.Process;

namespace Roslyn.VisualStudio.Test.Utilities
{
    /// <summary>Provides some helper functions used by the other classes in the project.</summary>
    internal static class IntegrationHelper
    {
        public static bool AttachThreadInput(uint idAttach, uint idAttachTo)
        {
            var success = User32.AttachThreadInput(idAttach, idAttachTo, true);

            if (!success)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(hresult);
            }

            return success;
        }

        public static bool BlockInput()
        {
            var success = User32.BlockInput(true);

            if (!success)
            {
                var hresult = Marshal.GetHRForLastWin32Error();

                if (hresult == VSConstants.E_ACCESSDENIED)
                {
                    Debug.WriteLine("Input cannot be blocked because the system requires Administrative privileges.");
                }
                else
                {
                    Debug.WriteLine("Input cannot be blocked because another thread has blocked the input.");
                }
            }

            return success;
        }

        public static void CreateDirectory(string path, bool deleteExisting = false)
        {
            if (deleteExisting)
            {
                DeleteDirectoryRecursively(path);
            }

            Directory.CreateDirectory(path);
        }

        public static void DeleteDirectoryRecursively(string path)
        {
            if (Directory.Exists(path))
            {
                DirectoryExtensions.DeleteRecursively(path);
            }
        }

        public static bool DetachThreadInput(uint idAttach, uint idAttachTo)
        {
            var success = User32.AttachThreadInput(idAttach, idAttachTo, false);

            if (!success)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(hresult);
            }

            return success;
        }

        public static async Task DownloadFileAsync(string downloadUrl, string fileName)
        {
            using (var webClient = new WebClient())
            {
                await webClient.DownloadFileTaskAsync(downloadUrl, fileName).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        public static IntPtr GetForegroundWindow()
        {
            // Attempt to get the foreground window in a loop, as the User32 function can return IntPtr.Zero
            // in certain circumstances, such as when a window is losing activation.

            var foregroundWindow = IntPtr.Zero;

            do
            {
                foregroundWindow = User32.GetForegroundWindow();
            }
            while (foregroundWindow == IntPtr.Zero);

            return foregroundWindow;
        }

        /// <summary>Gets the Modal Window that is currently blocking interaction with the specified window or <see cref="IntPtr.Zero"/> if none exists.</summary>
        public static IntPtr GetModalWindowFromParentWindow(IntPtr parentWindow)
        {
            foreach (var topLevelWindow in GetTopLevelWindows())
            {
                // GetParent will return the parent or owner of the specified window, unless:
                //  * The window is a top-level window that is unowned
                //  * The window is a top-level does not have the WS_POPUP style
                //  * The owner window has the WS_POPUP style
                // GetWindow with GW_OWNER specified will return the owner window, but not the parent window
                // GetAncestor with GA_PARENT specified will return the parent window, but not the owner window
                if ((User32.GetParent(topLevelWindow) == parentWindow) ||
                    (User32.GetWindow(topLevelWindow, User32.GW_OWNER) == parentWindow) ||
                    (User32.GetAncestor(topLevelWindow, User32.GA_PARENT) == parentWindow))
                {
                    return topLevelWindow;
                }
            }

            return IntPtr.Zero;
        }

        public static object GetRegistryKeyValue(RegistryKey baseKey, string subKeyName, string valueName)
        {
            using (var registryKey = baseKey.OpenSubKey(subKeyName))
            {
                if (registryKey == null)
                {
                    throw new Exception($@"The specified registry key could not be found. Registry Key: '{baseKey}\{subKeyName}'");
                }

                return registryKey.GetValue(valueName);
            }
        }

        /// <summary>Gets the title text for the specified window.</summary>
        /// <remarks>GetWindowText() does not work across the process boundary.</remarks>
        public static string GetTitleForWindow(IntPtr window)
        {
            var titleLength = User32.SendMessage(window, User32.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);

            if (titleLength == IntPtr.Zero)
            {
                return string.Empty;
            }

            var title = new StringBuilder(titleLength.ToInt32() + 1);

            User32.SendMessage(window, User32.WM_GETTEXT, (IntPtr)(title.Capacity), title);
            return title.ToString();
        }

        public static IEnumerable<IntPtr> GetTopLevelWindows()
        {
            var topLevelWindows = new List<IntPtr>();

            var enumFunc = new User32.WNDENUMPROC((hWnd, lParam) =>
            {
                topLevelWindows.Add(hWnd);
                return true;
            });

            var success = User32.EnumWindows(enumFunc, IntPtr.Zero);

            if (!success)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(hresult);
            }

            return topLevelWindows;
        }

        /// <summary>Kills the specified process if it is not <c>null</c> and has not already exited.</summary>
        public static void KillProcess(Process process)
        {
            if (process != null && !process.HasExited)
            {
                process.Kill();
            }
        }

        /// <summary>Kills all processes matching the specified name.</summary>
        public static void KillProcess(string processName)
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                KillProcess(processName);
            }
        }

        public static void RetryRpcCall(Action action)
        {
            do
            {
                try
                {
                    action();
                    return;
                }
                catch (COMException exception) when ((exception.HResult == VSConstants.RPC_E_CALL_REJECTED) ||
                                                     (exception.HResult == VSConstants.RPC_E_SERVERCALL_RETRYLATER))
                {
                    // We'll just try again in this case
                }
            }
            while (true);
        }

        public static T RetryRpcCall<T>(Func<T> action)
        {
            T returnValue = default(T);
            RetryRpcCall(() =>
            {
                returnValue = action();
            });
            return returnValue;
        }

        public static void SetForegroundWindow(IntPtr window)
        {
            var foregroundWindow = GetForegroundWindow();

            if (window == foregroundWindow)
            {
                return;
            }

            var activeThreadId = User32.GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            var currentThreadId = Kernel32.GetCurrentThreadId();

            bool threadInputsAttached = false;

            try
            {
                // Attach the thread inputs so that 'SetActiveWindow' and 'SetFocus' work
                threadInputsAttached = AttachThreadInput(currentThreadId, activeThreadId);

                // Make the window a top-most window so it will appear above any existing top-most windows
                User32.SetWindowPos(window, (IntPtr)User32.HWND_TOPMOST, 0, 0, 0, 0, (User32.SWP_NOSIZE | User32.SWP_NOMOVE));

                // Move the window into the foreground as it may not have been achieved by the 'SetWindowPos' call
                var success = User32.SetForegroundWindow(window);

                if (!success)
                {
                    throw new InvalidOperationException("Setting the foreground window failed.");
                }

                // Ensure the window is 'Active' as it may not have been achieved by 'SetForegroundWindow'
                User32.SetActiveWindow(window);

                // Give the window the keyboard focus as it may not have been achieved by 'SetActiveWindow'
                User32.SetFocus(window);

                // Remove the 'Top-Most' qualification from the window
                User32.SetWindowPos(window, (IntPtr)User32.HWND_NOTOPMOST, 0, 0, 0, 0, (User32.SWP_NOSIZE | User32.SWP_NOMOVE));
            }
            finally
            {
                if (threadInputsAttached)
                {
                    // Finally, detach the thread inputs from eachother
                    DetachThreadInput(currentThreadId, activeThreadId);
                }
            }
        }

        public static void SendInput(User32.INPUT[] inputs)
        {
            // NOTE: This assumes that Visual Studio is the active foreground window.

            LogKeyboardInputs(inputs);

            var eventsInserted = User32.SendInput((uint)inputs.Length, inputs, User32.SizeOf_INPUT);

            if (eventsInserted == 0)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                throw new ExternalException("Sending input failed because input was blocked by another thread.", hresult);
            }
        }

        [Conditional("DEBUG")]
        private static void LogKeyboardInputs(User32.INPUT[] inputs)
        {
            foreach (var input in inputs)
            {
                switch (input.Type)
                {
                    case User32.INPUT_KEYBOARD:
                        LogKeyboardInput(input.ki);
                        break;
                    case User32.INPUT_MOUSE:
                        Debug.WriteLine("UNEXPECTED: Encountered mouse input");
                        break;
                    case User32.INPUT_HARDWARE:
                        Debug.WriteLine("UNEXPECTED: Encountered hardware input");
                        break;
                    default:
                        Debug.WriteLine($"ERROR: Encountered illegal input type: {input.Type}");
                        break;
                }
            }
        }

        [Conditional("DEBUG")]
        private static void LogKeyboardInput(User32.KEYBDINPUT input)
        {
            var isExtendedKey = (input.dwFlags & User32.KEYEVENTF_EXTENDEDKEY) != 0;
            var isKeyUp = (input.dwFlags & User32.KEYEVENTF_KEYUP) != 0;
            var isUnicode = (input.dwFlags & User32.KEYEVENTF_UNICODE) != 0;
            var isScanCode = (input.dwFlags & User32.KEYEVENTF_SCANCODE) != 0;

            if (isUnicode && input.wVk != 0)
            {
                Debug.WriteLine("UNEXPECTED: if KEYEVENTF_UNICODE flag is specified then wVk must be 0.");
                return;
            }

            var builder = SharedPools.Default<StringBuilder>().AllocateAndClear();

            builder.Append("Send Key: ");

            char ch;
            if (isUnicode || isScanCode)
            {
                builder.Append(input.wScan.ToString("x4"));
                ch = (char)input.wScan;
            }
            else
            {
                builder.Append(input.wVk.ToString("x4"));
                ch = (char)(User32.MapVirtualKey(input.wVk, User32.MAPVK_VK_TO_CHAR) & 0x0000ffff);
            }

            // Append code and printable character
            builder.Append(' ');
            AppendPrintableChar(ch, builder);

            if (!isUnicode && !isScanCode && input.wVk <= byte.MaxValue)
            {
                AppendVirtualKey((byte)input.wVk, builder);
            }

            // Append flags
            if (input.dwFlags == 0)
            {
                builder.Append("[none]");
            }
            else
            {
                builder.Append('[');

                if (isExtendedKey)
                {
                    AppendFlag("extended", builder);
                }

                if (isKeyUp)
                {
                    AppendFlag("key up", builder);
                }

                if (isUnicode)
                {
                    AppendFlag("unicode", builder);
                }

                if (isScanCode)
                {
                    AppendFlag("scan code", builder);
                }

                builder.Append(']');
            }

            Debug.WriteLine(builder.ToString());

            SharedPools.Default<StringBuilder>().ClearAndFree(builder);
        }

        private static void AppendPrintableChar(char ch, StringBuilder builder)
        {
            string text = GetPrintableCharText(ch);

            if (text != null)
            {
                builder.Append("'");
                builder.Append(text);
                builder.Append("' ");
            }
        }

        private static string GetPrintableCharText(char ch)
        {
            switch (ch)
            {
                case '\r':
                    return @"\r";
                case '\n':
                    return @"\n";
                case '\t':
                    return @"\t";
                case '\f':
                    return @"\f";
                case '\v':
                    return @"\v";
                default:
                    return !char.IsControl(ch)
                        ? new string(ch, 1)
                        : null;
            }
        }

        private static void AppendVirtualKey(byte virtualKey, StringBuilder builder)
        {
            if (Enum.IsDefined(typeof(EditorWindow.VirtualKey), virtualKey))
            {
                builder.Append('(');
                builder.Append(Enum.GetName(typeof(EditorWindow.VirtualKey), virtualKey));
                builder.Append(") ");
            }
        }

        [Conditional("DEBUG")]
        private static void AppendFlag(string flagText, StringBuilder builder)
        {
            if (builder.Length > 0 && builder[builder.Length - 1] != '[')
            {
                builder.Append(", ");
            }

            builder.Append(flagText);
        }

        public static bool TryDeleteDirectoryRecursively(string path)
        {
            try
            {
                DeleteDirectoryRecursively(path);
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Warning: Failed to recursively delete the specified directory. (Name: '{path}')");
                Debug.WriteLine($"\t{e}");
                return false;
            }
        }

        /// <summary>Locates the DTE object for the specified process.</summary>
        public static DTE TryLocateDteForProcess(Process process)
        {
            object dte = null;
            IRunningObjectTable runningObjectTable = null;
            IEnumMoniker enumMoniker = null;
            IBindCtx bindContext = null;
            var monikers = new IMoniker[1];
            var vsProgId = VisualStudioInstanceFactory.VsProgId;

            Ole32.GetRunningObjectTable(0, out runningObjectTable);
            runningObjectTable.EnumRunning(out enumMoniker);
            Ole32.CreateBindCtx(0, out bindContext);

            do
            {
                monikers[0] = null;

                var monikersFetched = 0u;
                var hresult = enumMoniker.Next(1, monikers, out monikersFetched);

                if (hresult == VSConstants.S_FALSE)
                {
                    // There's nothing further to enumerate, so fail
                    return null;
                }
                else
                {
                    Marshal.ThrowExceptionForHR(hresult);
                }

                var moniker = monikers[0];
                string fullDisplayName = null;

                moniker.GetDisplayName(bindContext, null, out fullDisplayName);

                var displayNameProcessId = 0;

                // FullDisplayName will look something like: <ProgID>:<ProccessId>
                if (!int.TryParse(fullDisplayName.Split(':').Last(), out displayNameProcessId))
                {
                    continue;
                }

                var displayName = fullDisplayName.Substring(0, (fullDisplayName.Length - (displayNameProcessId.ToString().Length + 1)));
                var fullProgId = vsProgId.StartsWith("!") ? vsProgId : $"!{vsProgId}";

                if (displayName.Equals(fullProgId, StringComparison.OrdinalIgnoreCase) &&
                    (displayNameProcessId == process.Id))
                {
                    runningObjectTable.GetObject(moniker, out dte);
                }
            }
            while (dte == null);

            return (DTE)(dte);
        }

        public static void UnblockInput()
        {
            var success = User32.BlockInput(false);

            if (!success)
            {
                var hresult = Marshal.GetHRForLastWin32Error();
                throw new ExternalException("Input cannot be unblocked because it was blocked by another thread.", hresult);
            }
        }

        public static async Task WaitForResultAsync<T>(Func<T> action, T expectedResult)
        {
            while (!action().Equals(expectedResult))
            {
                await Task.Yield();
            }
        }

        public static async Task<T> WaitForNotNullAsync<T>(Func<T> action) where T : class
        {
            var result = action();

            while (result == null)
            {
                await Task.Yield();
                result = action();
            }

            return result;
        }
    }
}
