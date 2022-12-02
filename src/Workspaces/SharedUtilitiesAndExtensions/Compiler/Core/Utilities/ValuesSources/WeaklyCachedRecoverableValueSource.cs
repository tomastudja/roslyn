﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Host
{
    /// <summary>
    /// This class is a <see cref="ValueSource{T}"/> that holds onto a value weakly, 
    /// but can save its value and recover it on demand if needed.
    /// 
    /// The initial value comes from the <see cref="ValueSource{T}"/> specified in the constructor.
    /// Derived types implement SaveAsync and RecoverAsync.
    /// </summary>
    internal abstract class WeaklyCachedRecoverableValueSource<T> : ValueSource<T> where T : class
    {
        // enforce saving in a queue so save's don't overload the thread pool.
        private static Task s_latestTask = Task.CompletedTask;
        private static readonly NonReentrantLock s_taskGuard = new();

        private SemaphoreSlim? _lazyGate; // Lazily created. Access via the Gate property
        private bool _saved;

        private WeakReference<T>? _weakReference;
        private T? _initialValue;

        public WeaklyCachedRecoverableValueSource(T initialValue)
            => _initialValue = initialValue;

        /// <summary>
        /// Override this to save the state of the instance so it can be recovered.
        /// This method will only ever be called once.
        /// </summary>
        protected abstract Task SaveAsync(T instance, CancellationToken cancellationToken);

        /// <summary>
        /// Override this method to implement asynchronous recovery semantics.
        /// This method may be called multiple times.
        /// </summary>
        protected abstract Task<T> RecoverAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Override this method to implement synchronous recovery semantics.
        /// This method may be called multiple times.
        /// </summary>
        protected abstract T Recover(CancellationToken cancellationToken);

        private SemaphoreSlim Gate => LazyInitialization.EnsureInitialized(ref _lazyGate, SemaphoreSlimFactory.Instance);

        public override bool TryGetValue([NotNullWhen(true)] out T? value)
        {
            // See if we still have the constant value stored.  If so, we can trivially return that.
            value = _initialValue;
            if (_initialValue != null)
                return true;

            // If not, see if it's something someone else is holding into, and is available through the weak-ref.
            value = null;
            var weakReference = _weakReference;
            return weakReference != null && weakReference.TryGetTarget(out value) && value != null;
        }

        public override T GetValue(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetValue(out var instance))
            {
                using (Gate.DisposableWait(cancellationToken))
                {
                    if (!TryGetValue(out instance))
                    {
                        instance = Recover(cancellationToken);
                        EnqueueSaveTask_NoLock(instance);
                    }
                }
            }

            return instance;
        }

        public override async Task<T> GetValueAsync(CancellationToken cancellationToken)
        {
            if (!TryGetValue(out var instance))
            {
                using (await Gate.DisposableWaitAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (!TryGetValue(out instance))
                    {
                        instance = await RecoverAsync(cancellationToken).ConfigureAwait(false);
                        EnqueueSaveTask_NoLock(instance);
                    }
                }
            }

            return instance;
        }

        /// <summary>
        /// Kicks off the work to save this instance to secondary storage at some point in the future.  Once that save
        /// occurs successfully, we will drop our cached data and return values from that storage instead.
        /// </summary>
        private void EnqueueSaveTask_NoLock(T instance)
        {
            Contract.ThrowIfTrue(Gate.CurrentCount != 0);

            _weakReference ??= new WeakReference<T>(instance);
            _weakReference.SetTarget(instance);

            // Ensure we only save once.
            if (!_saved)
            {
                _saved = true;
                using (s_taskGuard.DisposableWait())
                {
                    // force all save tasks to be in sequence so we don't hog all the threads
                    s_latestTask = SaveAndResetInitialValue(s_latestTask);
                }
            }

            return;

            async Task SaveAndResetInitialValue(Task previousTask)
            {
                // First wait for the prior task in the chain to be done.  Ignore all errors from prior tasks.  They
                // do not affect if we run or not.
                await previousTask.NoThrowAwaitableInternal(captureContext: false);

                // Now defer to our subclass to actually save the instance to secondary storage.
                await SaveAsync(instance, CancellationToken.None).ConfigureAwait(false);

                // Only set _initialValue to null if the saveTask completed successfully. If the save did not complete,
                // we want to keep it around to service future requests.  Once we do clear out this value, then all
                // future request will either retrieve the value from the weak reference (if anyone else is holding onto
                // it), or will recover from the 
                using (Gate.DisposableWait(CancellationToken.None))
                {
                    _initialValue = null;
                }
            }
        }
    }
}
