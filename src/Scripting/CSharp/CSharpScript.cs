// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Scripting.CSharp
{
    /// <summary>
    /// A factory for creating and running C# scripts.
    /// </summary>
    public static class CSharpScript
    {
        /// <summary>
        /// Create a new C# script.
        /// </summary>
        /// <param name="code">The source code of the script.</param>
        /// <param name="options">The script options.</param>
        /// <param name="globalsType">Type of global object.</param>
        /// <typeparam name="T">The return type of the script</typeparam>
        public static Script<T> Create<T>(string code, ScriptOptions options = null, Type globalsType = null)
        {
            return new Script<T>(CSharpScriptCompiler.Instance, code, options, globalsType, null, null);
        }

        /// <summary>
        /// Create a new C# script.
        /// </summary>
        /// <param name="code">The source code of the script.</param>
        /// <param name="options">The script options.</param>
        /// <param name="globalsType">Type of global object.</param>
        public static Script<object> Create(string code, ScriptOptions options = null, Type globalsType = null)
        {
            return Create<object>(code, options, globalsType);
        }

        /// <summary>
        /// Run a C# script.
        /// </summary>
        /// <param name="code">The source code of the script.</param>
        /// <param name="options">The script options.</param>
        /// <param name="globals">An object instance whose members can be accessed by the script as global variables.</param>
        /// <param name="globalsType">Type of global object, <paramref name="globals"/>.GetType() is used if not specified.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <typeparam name="T">The return type of the submission</typeparam>
        public static Task<ScriptState<T>> RunAsync<T>(string code, ScriptOptions options = null, object globals = null, Type globalsType = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Create<T>(code, options, globalsType ?? globals?.GetType()).RunAsync(globals, cancellationToken);
        }

        /// <summary>
        /// Run a C# script.
        /// </summary>
        /// <param name="code">The source code of the script.</param>
        /// <param name="options">The script options.</param>
        /// <param name="globals">An object instance whose members can be accessed by the script as global variables.</param>
        /// <param name="globalsType">Type of global object, <paramref name="globals"/>.GetType() is used if not specified.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static Task<ScriptState<object>> RunAsync(string code, ScriptOptions options = null, object globals = null, Type globalsType = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return RunAsync<object>(code, options, globals, globalsType, cancellationToken);
        }

        /// <summary>
        /// Run a C# script and return its resulting value.
        /// </summary>
        /// <param name="code">The source code of the script.</param>
        /// <param name="options">The script options.</param>
        /// <param name="globals">An object instance whose members can be accessed by the script as global variables.</param>
        /// <param name="globalsType">Type of global object, <paramref name="globals"/>.GetType() is used if not specified.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <typeparam name="T">The return type of the submission</typeparam>
        /// <return>Returns the value returned by running the script.</return>
        public static Task<T> EvaluateAsync<T>(string code, ScriptOptions options = null, object globals = null, Type globalsType = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return RunAsync<T>(code, options, globals, globalsType, cancellationToken).GetEvaluationResultAsync();
        }

        /// <summary>
        /// Run a C# script and return its resulting value.
        /// </summary>
        /// <param name="code">The source code of the script.</param>
        /// <param name="options">The script options.</param>
        /// <param name="globals">An object instance whose members can be accessed by the script as global variables.</param>
        /// <param name="globalsType">Type of global object, <paramref name="globals"/>.GetType() is used if not specified.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <return>Returns the value returned by running the script.</return>
        public static Task<object> EvaluateAsync(string code, ScriptOptions options = null, object globals = null, Type globalsType = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return EvaluateAsync<object>(code, options, globals, globalsType, cancellationToken);
        }
    }
}

