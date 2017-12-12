﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Test.Utilities
{
    public static class IlasmUtilities
    {
        public static DisposableFile CreateTempAssembly(string declarations, bool prependDefaultHeader = true)
        {
            IlasmTempAssembly(declarations, prependDefaultHeader, includePdb: false, assemblyPath: out var assemblyPath, pdbPath: out var pdbPath);
            Assert.NotNull(assemblyPath);
            Assert.Null(pdbPath);
            return new DisposableFile(assemblyPath);
        }

        private static string GetIlasmPath()
        {
            if (CoreClrShim.AssemblyLoadContext.Type == null)
            {
                return Path.Combine(
                    Path.GetDirectoryName(CorLightup.Desktop.GetAssemblyLocation(typeof(object).GetTypeInfo().Assembly)),
                    "ilasm.exe");
            }
            else
            {
                var ilasmExeName = PlatformInformation.IsWindows ? "ilasm.exe" : "ilasm";

                var directory = AppContext.BaseDirectory;
                string path = null;
                while (directory != null && !File.Exists(path = Path.Combine(directory, "Binaries", "Tools", "ILAsm", ilasmExeName)))
                {
                    directory = Path.GetDirectoryName(directory);
                }

                if (directory == null)
                {
                    throw new NotSupportedException("Unable to find CoreCLR ilasm tool. Has the Microsoft.NETCore.ILAsm package been published to ./Binaries/Tools?");
                }

                return path;
            }
        }

        private static readonly string IlasmPath = GetIlasmPath();

        public static void IlasmTempAssembly(string declarations, bool appendDefaultHeader, bool includePdb, out string assemblyPath, out string pdbPath)
        {
            if (declarations == null) throw new ArgumentNullException(nameof(declarations));

            using (var sourceFile = new DisposableFile(extension: ".il"))
            {
                string sourceFileName = Path.GetFileNameWithoutExtension(sourceFile.Path);

                assemblyPath = Path.Combine(
                    TempRoot.Root,
                    Path.ChangeExtension(Path.GetFileName(sourceFile.Path), "dll"));

                string completeIL;
                if (appendDefaultHeader)
                {
                    const string corLibName = "mscorlib";
                    const string corLibVersion = "4:0:0:0";
                    const string corLibKey = "B7 7A 5C 56 19 34 E0 89";

                    completeIL =
$@".assembly '{sourceFileName}' {{}} 

.assembly extern {corLibName} 
{{
  .publickeytoken = ({corLibKey})
  .ver {corLibVersion}
}} 

{declarations}";
                }
                else
                {
                    completeIL = declarations.Replace("<<GeneratedFileName>>", sourceFileName);
                }

                sourceFile.WriteAllText(completeIL);

                var arguments = $"\"{sourceFile.Path}\" -DLL -out=\"{assemblyPath}\"";

                if (includePdb && !MonoHelpers.IsRunningOnMono())
                {
                    pdbPath = Path.ChangeExtension(assemblyPath, "pdb");
                    arguments += string.Format(" -PDB=\"{0}\"", pdbPath);
                }
                else
                {
                    pdbPath = null;
                }

                var program = IlasmPath;
                if (MonoHelpers.IsRunningOnMono())
                {
                    arguments = string.Format("{0} {1}", IlasmPath, arguments);
                    arguments = arguments.Replace("\"", "");
                    arguments = arguments.Replace("=", ":");
                    program = "mono";
                }

                var result = ProcessUtilities.Run(program, arguments);

                if (result.ContainsErrors)
                {
                    throw new ArgumentException(
                        "The provided IL cannot be compiled." + Environment.NewLine +
                        program + " " + arguments + Environment.NewLine +
                        result,
                        nameof(declarations));
                }
            }
        }
    }
}
