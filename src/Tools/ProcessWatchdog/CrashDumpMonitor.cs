﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;

namespace ProcessWatchdog
{
    internal static class CrashDumpMonitor
    {
        public static Process MonitorProcess(int processId, string description, string outputFolder, string procDumpPath)
        {
            // Make sure everything is fully qualified as we are passing this to other processes
            outputFolder = Path.GetFullPath(outputFolder);
            string dumpFileName = GenerateCrashDumpFileName(description, outputFolder);

            var processStartInfo = new ProcessStartInfo
            {
                Arguments = GetProcDumpArgumentsForMonitoring(processId, dumpFileName),
                CreateNoWindow = true,
                FileName = procDumpPath,
                UseShellExecute = false
            };

            return Process.Start(processStartInfo);
        }

        private static string GenerateCrashDumpFileName(string description, string outputFolder)
        {
            var outputFolderInfo = new DirectoryInfo(outputFolder);
            if (!outputFolderInfo.Exists)
            {
                outputFolderInfo.Create();
                ConsoleUtils.LogMessage(Resources.InfoCreatedOutputFolder, outputFolderInfo.FullName);
            }

            var fileName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_ff") + " - " + description + ".dmp";
            fileName = Path.Combine(outputFolderInfo.FullName, fileName);
            return fileName;
        }

        private static string GetProcDumpArgumentsForMonitoring(int pid, string dumpFileName)
        {
            // Here's what these arguments mean:
            //
            // -g:  Run as a native debugger in a managed process (no interop).
            // -e:  Dump when an unhandled exception happens.
            // -b:  Dump when a breakpoint (__int 3 / Debugger.Break()) is encountered.
            // -t:  Dump when process terminates.
            // -ma: Create a full memory dump.
            //
            // without -g, procdump will not catch unhandled managed exception since
            // the CLR will always handle unhandled exceptions. From procdump's point of
            // view, there is no such thing as an unhandled exception for a managed app.

            return $"-accepteula -g -e -b -t -ma {pid} \"{dumpFileName}\"";
        }
    }
}
