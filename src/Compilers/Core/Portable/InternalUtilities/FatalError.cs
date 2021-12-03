﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

#if NET20
// Some APIs referenced by documentation comments are not available on .NET Framework 2.0.
#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved
#endif

namespace Microsoft.CodeAnalysis.ErrorReporting
{
    internal static class FatalError
    {
        public delegate void ErrorReporterHandler(Exception exception, ErrorSeverity severity, bool forceDump);
        private static ErrorReporterHandler? s_handler;

#pragma warning disable IDE0052 // Remove unread private members - We want to hold onto last exception to make investigation easier
        private static Exception? s_reportedException;
        private static string? s_reportedExceptionMessage;
#pragma warning restore IDE0052

        /// <summary>
        /// Set by the host to handle an error report; this may crash the process or report telemetry.
        /// </summary>
        [DisallowNull]
        public static ErrorReporterHandler? Handler
        {
            get
            {
                return s_handler;
            }

            set
            {
                if (s_handler != value)
                {
                    Debug.Assert(s_handler == null, "Handler already set");
                    s_handler = value;
                }
            }
        }

        // Same as setting the Handler property except that it avoids the assert.  This is useful in 
        // test code which needs to verify the handler is called in specific cases and will continually
        // overwrite this value.
        public static void OverwriteHandler(ErrorReporterHandler? value)
        {
            s_handler = value;
        }

        // In the result provider, we aren't copying our handler to somewhere else, so we don't
        // need this method. It's too much of a challenge to shared code to work in
        // old versions of the runtime since APIs changed over time.
#if !NET20 && !NETSTANDARD1_3 

        /// <summary>
        /// Copies the handler in this instance to the linked copy of this type in this other assembly.
        /// </summary>
        /// <remarks>
        /// This file is in linked into multiple layers, but we want to ensure that all layers have the same copy.
        /// This lets us copy the handler in this instance into the same in another instance.</remarks>
        public static void CopyHandlerTo(Assembly assembly)
        {
            var targetType = assembly.GetType(typeof(FatalError).FullName!, throwOnError: true)!;
            var targetHandlerProperty = targetType.GetProperty(nameof(FatalError.Handler), BindingFlags.Static | BindingFlags.Public)!;
            if (Handler is not null)
            {
                // We need to convert the delegate type to the type in the linked copy since they won't have identity.
                var convertedDelegate = Delegate.CreateDelegate(targetHandlerProperty.PropertyType, Handler.Target, method: Handler.Method);
                targetHandlerProperty.SetValue(obj: null, value: convertedDelegate);
            }
            else
            {
                targetHandlerProperty.SetValue(obj: null, value: null);
            }
        }

#endif

        private static bool IsCurrentOperationBeingCancelled(Exception exception, CancellationToken cancellationToken)
            => exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

        /// <summary>
        /// Use in an exception filter to report an error (by calling <see cref="Handler"/>), unless the
        /// operation has been cancelled. The exception is never caught.
        /// </summary>
        /// <returns><see langword="false"/> to avoid catching the exception.</returns>
        [DebuggerHidden]
        public static bool ReportAndPropagateUnlessCanceled(Exception exception, ErrorSeverity severity = ErrorSeverity.Uncategorized)
        {
            if (exception is OperationCanceledException)
            {
                return false;
            }

            return ReportAndPropagate(exception, severity);
        }

        /// <summary>
        /// <para>Use in an exception filter to report an error (by calling <see cref="Handler"/>), unless the
        /// operation has been cancelled at the request of <paramref name="contextCancellationToken"/>. The exception is
        /// never caught.</para>
        ///
        /// <para>Cancellable operations are only expected to throw <see cref="OperationCanceledException"/> if the
        /// applicable <paramref name="contextCancellationToken"/> indicates cancellation is requested by setting
        /// <see cref="CancellationToken.IsCancellationRequested"/>. Unexpected cancellation, i.e. an
        /// <see cref="OperationCanceledException"/> which occurs without <paramref name="contextCancellationToken"/>
        /// requesting cancellation, is treated as an error by this method.</para>
        ///
        /// <para>This method does not require <see cref="OperationCanceledException.CancellationToken"/> to match
        /// <paramref name="contextCancellationToken"/>, provided cancellation is expected per the previous
        /// paragraph.</para>
        /// </summary>
        /// <param name="contextCancellationToken">A <see cref="CancellationToken"/> which will have
        /// <see cref="CancellationToken.IsCancellationRequested"/> set if cancellation is expected.</param>
        /// <returns><see langword="false"/> to avoid catching the exception.</returns>
        [DebuggerHidden]
        public static bool ReportAndPropagateUnlessCanceled(Exception exception, CancellationToken contextCancellationToken, ErrorSeverity severity = ErrorSeverity.Uncategorized)
        {
            if (IsCurrentOperationBeingCancelled(exception, contextCancellationToken))
            {
                return false;
            }

            return ReportAndPropagate(exception, severity);
        }

        /// <summary>
        /// Use in an exception filter to report an error (by calling <see cref="Handler"/>) and catch
        /// the exception, unless the operation was cancelled.
        /// </summary>
        /// <returns><see langword="true"/> to catch the exception if the error was reported; otherwise,
        /// <see langword="false"/> to propagate the exception if the operation was cancelled.</returns>
        [DebuggerHidden]
        public static bool ReportAndCatchUnlessCanceled(Exception exception, ErrorSeverity severity = ErrorSeverity.Uncategorized)
        {
            if (exception is OperationCanceledException)
            {
                return false;
            }

            return ReportAndCatch(exception, severity);
        }

        /// <summary>
        /// <para>Use in an exception filter to report an error (by calling <see cref="Handler"/>) and
        /// catch the exception, unless the operation was cancelled at the request of
        /// <paramref name="contextCancellationToken"/>.</para>
        ///
        /// <para>Cancellable operations are only expected to throw <see cref="OperationCanceledException"/> if the
        /// applicable <paramref name="contextCancellationToken"/> indicates cancellation is requested by setting
        /// <see cref="CancellationToken.IsCancellationRequested"/>. Unexpected cancellation, i.e. an
        /// <see cref="OperationCanceledException"/> which occurs without <paramref name="contextCancellationToken"/>
        /// requesting cancellation, is treated as an error by this method.</para>
        ///
        /// <para>This method does not require <see cref="OperationCanceledException.CancellationToken"/> to match
        /// <paramref name="contextCancellationToken"/>, provided cancellation is expected per the previous
        /// paragraph.</para>
        /// </summary>
        /// <param name="contextCancellationToken">A <see cref="CancellationToken"/> which will have
        /// <see cref="CancellationToken.IsCancellationRequested"/> set if cancellation is expected.</param>
        /// <returns><see langword="true"/> to catch the exception if the error was reported; otherwise,
        /// <see langword="false"/> to propagate the exception if the operation was cancelled.</returns>
        [DebuggerHidden]
        public static bool ReportAndCatchUnlessCanceled(Exception exception, CancellationToken contextCancellationToken, ErrorSeverity severity = ErrorSeverity.Uncategorized)
        {
            if (IsCurrentOperationBeingCancelled(exception, contextCancellationToken))
            {
                return false;
            }

            return ReportAndCatch(exception, severity);
        }

        /// <summary>
        /// Use in an exception filter to report an error without catching the exception.
        /// The error is reported by calling <see cref="Handler"/>.
        /// </summary>
        /// <returns><see langword="false"/> to avoid catching the exception.</returns>
        [DebuggerHidden]
        public static bool ReportAndPropagate(Exception exception, ErrorSeverity severity = ErrorSeverity.Uncategorized)
        {
            Report(exception, severity);
            return false;
        }

        /// <summary>
        /// Report an error.
        /// Calls <see cref="Handler"/> and doesn't pass the exception through (the method returns true).
        /// This is generally expected to be used within an exception filter as that allows us to
        /// capture data at the point the exception is thrown rather than when it is handled.
        /// However, it can also be used outside of an exception filter. If the exception has not
        /// already been thrown the method will throw and catch it itself to ensure we get a useful
        /// stack trace.
        /// </summary>
        /// <returns>True to catch the exception.</returns>
        [DebuggerHidden]
        public static bool ReportAndCatch(Exception exception, ErrorSeverity severity = ErrorSeverity.Uncategorized)
        {
            Report(exception, severity);
            return true;
        }

        [DebuggerHidden]
        public static bool ReportWithDumpAndCatch(Exception exception, ErrorSeverity severity = ErrorSeverity.Uncategorized)
        {
            Report(exception, severity, forceDump: true);
            return true;
        }

        private static readonly object s_reportedMarker = new();

        private static void Report(Exception exception, ErrorSeverity severity = ErrorSeverity.Uncategorized, bool forceDump = false)
        {
            // hold onto last exception to make investigation easier
            s_reportedException = exception;
            s_reportedExceptionMessage = exception.ToString();

            if (s_handler == null)
            {
                return;
            }

            // only report exception once
            if (exception.Data[s_reportedMarker] != null)
            {
                return;
            }

#if !NET20
            if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1 && aggregate.InnerExceptions[0].Data[s_reportedMarker] != null)
            {
                return;
            }
#endif

            if (!exception.Data.IsReadOnly)
            {
                exception.Data[s_reportedMarker] = s_reportedMarker;
            }

            s_handler(exception, severity, forceDump);
        }
    }

    internal enum ErrorSeverity
    {
        Uncategorized,
        Diagnostic,
        General,
        Critical
    }
}
