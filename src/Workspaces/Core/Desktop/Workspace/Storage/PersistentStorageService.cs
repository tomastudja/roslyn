// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.SolutionSize;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Storage
{
    /// <summary>
    /// A service that enables storing and retrieving of information associated with solutions,
    /// projects or documents across runtime sessions.
    /// </summary>
    internal abstract partial class AbstractPersistentStorageService : IPersistentStorageService
    {
        /// <summary>
        /// threshold to start to use a DB (50MB)
        /// </summary>
        private const int SolutionSizeThreshold = 50 * 1024 * 1024;

        protected readonly IOptionService OptionService;
        private readonly SolutionSizeTracker _solutionSizeTracker;

        private readonly object _lookupAccessLock;
        private readonly Dictionary<string, AbstractPersistentStorage> _lookup;
        private readonly bool _testing;

        private string _lastSolutionPath;

        private SolutionId _primarySolutionId;
        private AbstractPersistentStorage _primarySolutionStorage;

        protected AbstractPersistentStorageService(
            IOptionService optionService,
            SolutionSizeTracker solutionSizeTracker)
        {
            OptionService = optionService;
            _solutionSizeTracker = solutionSizeTracker;

            _lookupAccessLock = new object();
            _lookup = new Dictionary<string, AbstractPersistentStorage>();

            _lastSolutionPath = null;

            _primarySolutionId = null;
            _primarySolutionStorage = null;
        }

        protected AbstractPersistentStorageService(IOptionService optionService, bool testing) 
            : this(optionService)
        {
            _testing = true;
        }

        protected AbstractPersistentStorageService(IOptionService optionService) 
            : this(optionService, solutionSizeTracker: null)
        {
        }

        protected abstract string GetDatabaseFilePath(string workingFolderPath);

        protected abstract bool TryCreatePersistentStorage(
            string workingFolderPath, string solutionPath,
            out AbstractPersistentStorage persistentStorage);

        public IPersistentStorage GetStorage(Solution solution)
        {
            if (!ShouldUseDatabase(solution))
            {
                return NoOpPersistentStorage.Instance;
            }

            // can't use cached information
            if (!string.Equals(solution.FilePath, _lastSolutionPath, StringComparison.OrdinalIgnoreCase))
            {
                // check whether the solution actually exist on disk
                if (!File.Exists(solution.FilePath))
                {
                    return NoOpPersistentStorage.Instance;
                }
            }

            // cache current result.
            _lastSolutionPath = solution.FilePath;

            // get working folder path
            var workingFolderPath = GetWorkingFolderPath(solution);
            if (workingFolderPath == null)
            {
                // we don't have place to save db file. don't use db
                return NoOpPersistentStorage.Instance;
            }

            return GetStorage(solution, workingFolderPath);
        }

        private IPersistentStorage GetStorage(Solution solution, string workingFolderPath)
        {
            lock (_lookupAccessLock)
            {
                // see whether we have something we can use
                if (_lookup.TryGetValue(solution.FilePath, out var storage))
                {
                    // previous attempt to create db storage failed.
                    if (storage == null && !SolutionSizeAboveThreshold(solution))
                    {
                        return NoOpPersistentStorage.Instance;
                    }

                    // everything seems right, use what we have
                    if (storage?.WorkingFolderPath == workingFolderPath)
                    {
                        storage.AddRefUnsafe();
                        return storage;
                    }
                }

                // either this is the first time, or working folder path has changed.
                // remove existing one
                _lookup.Remove(solution.FilePath);

                var dbFile = GetDatabaseFilePath(workingFolderPath);
                if (!File.Exists(dbFile) && !SolutionSizeAboveThreshold(solution))
                {
                    _lookup.Add(solution.FilePath, storage);
                    return NoOpPersistentStorage.Instance;
                }

                // try create new one
                storage = TryCreatePersistentStorage(workingFolderPath, solution.FilePath);
                _lookup.Add(solution.FilePath, storage);

                if (storage != null)
                {
                    RegisterPrimarySolutionStorageIfNeeded(solution, storage);

                    storage.AddRefUnsafe();
                    return storage;
                }

                return NoOpPersistentStorage.Instance;
            }
        }

        private bool ShouldUseDatabase(Solution solution)
        {
            if (_testing)
            {
                return true;
            }

            // we only use database for primary solution. (Ex, forked solution will not use database)
            if (solution.BranchId != solution.Workspace.PrimaryBranchId || solution.FilePath == null)
            {
                return false;
            }

            return true;
        }

        private bool SolutionSizeAboveThreshold(Solution solution)
        {
            if (_testing)
            {
                return true;
            }

            if (_solutionSizeTracker == null)
            {
                return false;
            }

            var size = _solutionSizeTracker.GetSolutionSize(solution.Workspace, solution.Id);
            return size > SolutionSizeThreshold;
        }

        private void RegisterPrimarySolutionStorageIfNeeded(Solution solution, AbstractPersistentStorage storage)
        {
            if (_primarySolutionStorage != null || solution.Id != _primarySolutionId)
            {
                return;
            }

            // hold onto the primary solution when it is used the first time.
            _primarySolutionStorage = storage;
            storage.AddRefUnsafe();
        }

        private string GetWorkingFolderPath(Solution solution)
        {
            if (_testing)
            {
                return Path.Combine(Path.GetDirectoryName(solution.FilePath), ".vs", Path.GetFileNameWithoutExtension(solution.FilePath));
            }

            var locationService = solution.Workspace.Services.GetService<IPersistentStorageLocationService>();
            return locationService?.GetStorageLocation(solution);
        }

        private AbstractPersistentStorage TryCreatePersistentStorage(string workingFolderPath, string solutionPath)
        {
            if (TryCreatePersistentStorage(workingFolderPath, solutionPath, out var persistentStorage))
            {
                return persistentStorage;
            }

            // first attempt could fail if there was something wrong with existing db.
            // try one more time in case the first attempt fixed the problem.
            if (TryCreatePersistentStorage(workingFolderPath, solutionPath, out persistentStorage))
            {
                return persistentStorage;
            }

            // okay, can't recover, then use no op persistent service 
            // so that things works old way (cache everything in memory)
            return null;
        }

        protected void Release(AbstractPersistentStorage storage)
        {
            lock (_lookupAccessLock)
            {
                if (storage.ReleaseRefUnsafe())
                {
                    _lookup.Remove(storage.SolutionFilePath);
                    storage.Close();
                }
            }
        }

        public void RegisterPrimarySolution(SolutionId solutionId)
        {
            // don't create database storage file right away. it will be
            // created when first C#/VB project is added
            lock (_lookupAccessLock)
            {
                Contract.ThrowIfTrue(_primarySolutionStorage != null);

                // just reset solutionId as long as there is no storage has created.
                _primarySolutionId = solutionId;
            }
        }

        public void UnregisterPrimarySolution(SolutionId solutionId, bool synchronousShutdown)
        {
            AbstractPersistentStorage storage = null;
            lock (_lookupAccessLock)
            {
                if (_primarySolutionId == null)
                {
                    // primary solution is never registered or already unregistered
                    Contract.ThrowIfTrue(_primarySolutionStorage != null);
                    return;
                }

                Contract.ThrowIfFalse(_primarySolutionId == solutionId);

                _primarySolutionId = null;
                if (_primarySolutionStorage == null)
                {
                    // primary solution is registered but no C#/VB project was added
                    return;
                }

                storage = _primarySolutionStorage;
                _primarySolutionStorage = null;
            }

            if (storage != null)
            {
                if (synchronousShutdown)
                {
                    // dispose storage outside of the lock
                    storage.Dispose();
                }
                else
                {
                    // make it to shutdown asynchronously
                    Task.Run(() => storage.Dispose());
                }
            }
        }
    }
}
