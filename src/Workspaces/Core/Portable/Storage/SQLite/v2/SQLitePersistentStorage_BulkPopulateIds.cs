﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.PersistentStorage;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.SQLite.Interop;
using Microsoft.CodeAnalysis.SQLite.v2.Interop;
using Microsoft.CodeAnalysis.Storage;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.SQLite.v2
{
    internal partial class SQLitePersistentStorage
    {
        private readonly ConcurrentDictionary<ProjectId, object> _projectBulkPopulatedLock = new();
        private readonly HashSet<ProjectId> _projectBulkPopulatedMap = new();

        /// <remarks>
        /// We have a lot of ID information to put into the DB. IDs for all strings we intend to 
        /// intern, as well as compound IDs for our projects and documents. Inserting these 
        /// individually is far too slow as SQLite will lock the DB for each insert and will have
        /// to do all the journaling work to ensure ACID semantics.  To avoid that, we attempt
        /// to precompute all the information we'd need to put in the ID tables and perform it
        /// all at once per project.
        /// </remarks>
        private void BulkPopulateIds(SqlConnection connection, Solution? bulkLoadSnapshot, bool fetchStringTable)
        {
#if false
            // Can only bulk populate if we were given a snapshot we can walk to grab data from.
            if (bulkLoadSnapshot == null)
                return;

            if (bulkLoadSnapshot != null)
                return;

            foreach (var (_, projectState) in bulkLoadSnapshot.State.ProjectStates)
                BulkPopulateProjectIds(connection, bulkLoadSnapshot.State, projectState, fetchStringTable);
#endif
        }

        private void BulkPopulateProjectIds(SqlConnection connection, SolutionState bulkLoadSolution, ProjectState bulkLoadProject, bool fetchStringTable)
        {
#if false
            // Ensure that only one caller is trying to bulk populate a project at a time.
            var gate = _projectBulkPopulatedLock.GetOrAdd(bulkLoadProject.Id, _ => new object());
            lock (gate)
            {
                if (_projectBulkPopulatedMap.Contains(bulkLoadProject.Id))
                {
                    // We've already bulk processed this project.  No need to do so again.
                    return;
                }

                // Ensure our string table is up to date with the DB.  Note: we want to do this 
                // to prevent the following problem:
                //
                // 1) Process1 and Process2 are concurrently attempting to bulk populate the DB.  Process1
                // ends up populating the DB.  Process2 then tries to do the same, and gets a constraint
                // violation because it is trying to add the same strings as Process1 did.  Because of the
                // constraint violation, Process2 will back off to try again later.  Unless it actually gets
                // the current string table, it will keep having problems trying to bulk populate.
                if (fetchStringTable)
                {
                    if (!TryFetchStringTable(connection))
                    {
                        // Weren't able to fetch the string table.  Have to try this again
                        // later once the DB frees up.
                        return;
                    }
                }

                if (!BulkPopulateProjectIdsWorker(connection, bulkLoadSolution, bulkLoadProject))
                {
                    // Something went wrong.  Try to bulk populate this project later.
                    return;
                }

                // Successfully bulk populated.  Mark as such so we don't bother doing this again.
                _projectBulkPopulatedMap.Add(bulkLoadProject.Id);
            }
#endif
        }

#if false

        private static readonly ObjectPool<Dictionary<int, string>> s_dictionaryPool
            = SharedPools.Default<Dictionary<int, string>>();

        /// <summary>
        /// Returns 'true' if the bulk population succeeds, or false if it doesn't.
        /// </summary>
        private bool BulkPopulateProjectIdsWorker(SqlConnection connection, SolutionState solutionState, ProjectState projectState)
        {
            // First, in bulk, get string-ids for all the paths and names for the project and documents.
            if (!AddIndividualProjectAndDocumentComponentIds())
            {
                return false;
            }

            // Now, ensure we have the project id known locally.  We cannot do this until we've 
            // gotten all the IDs for the individual project components as the project ID is built
            // from a compound key using the IDs for the project's FilePath and Name.
            //
            // If this fails for any reason, we can't proceed.
            var projectId = TryGetProjectId(connection, ProjectKey.ToProjectKey(solutionState, projectState));
            if (projectId == null)
            {
                return false;
            }

            // Finally, in bulk, determine the final DB IDs for all our documents. We cannot do 
            // this until we have the project-id as the document IDs are built from a compound
            // ID including the project-id.
            return AddDocumentIds();

            // Local functions below.

            // Use local functions so that other members of this class don't accidentally use these.
            // There are invariants in the context of BulkPopulateProjectIdsWorker that these functions
            // can depend on.
            bool AddIndividualProjectAndDocumentComponentIds()
            {
                var stringsToAdd = new HashSet<string>();
                AddIfUnknownId(projectState.FilePath, stringsToAdd);
                AddIfUnknownId(projectState.Name, stringsToAdd);

                foreach (var (_, documentState) in projectState.DocumentStates)
                {
                    AddIfUnknownId(documentState.FilePath, stringsToAdd);
                    AddIfUnknownId(documentState.Name, stringsToAdd);
                }

                return AddStrings(stringsToAdd);
            }

            bool AddStrings(HashSet<string> stringsToAdd)
            {
                if (stringsToAdd.Count > 0)
                {
                    using var idToString = s_dictionaryPool.GetPooledObject();
                    try
                    {
                        connection.RunInTransaction(s_insertAllStrings, (this, stringsToAdd, connection, idToString.Object));
                    }
                    catch (SqlException ex) when (ex.Result == Result.CONSTRAINT)
                    {
                        // Constraint exceptions are possible as we may be trying bulk insert 
                        // strings while some other thread/process does the same.
                        return false;
                    }
                    catch (Exception ex)
                    {
                        // Something failed. Log the issue, and let the caller know we should stop
                        // with the bulk population.
                        StorageDatabaseLogger.LogException(ex);
                        return false;
                    }

                    // We succeeded inserting all the strings.  Ensure our local cache has all the
                    // values we added.
                    foreach (var (id, value) in idToString.Object)
                        _stringToIdMap[value] = id;
                }

                return true;
            }

            bool AddDocumentIds()
            {
                var stringsToAdd = new HashSet<string>();

                foreach (var (_, documentState) in projectState.DocumentStates)
                {
                    // Produce the string like "projId-docPathId-docNameId" so that we can get a
                    // unique ID for it.
                    AddIfUnknownId(GetDocumentIdString(documentState), stringsToAdd);
                }

                // Ensure we have unique IDs for all these document string ids.  If we fail to 
                // bulk import these strings, we can't proceed.
                if (!AddStrings(stringsToAdd))
                {
                    return false;
                }

                foreach (var (documentId, documentState) in projectState.DocumentStates)
                {
                    // Get the integral ID for this document.  It's safe to directly index into
                    // the map as we just successfully added these strings to the DB.
                    var id = _stringToIdMap[GetDocumentIdString(documentState)];
                    _documentIdToIdMap.TryAdd(documentId, id);
                }

                return true;
            }

            string GetDocumentIdString(DocumentState documentState)
            {
                Contract.ThrowIfNull(documentState.FilePath);

                // We should always be able to index directly into these maps.  This function is only
                // ever called after we called AddIndividualProjectAndDocumentComponentIds.
                var documentPathId = _stringToIdMap[documentState.FilePath ?? ""];
                var documentNameId = _stringToIdMap[documentState.Name];

                var documentIdString = SQLitePersistentStorage.GetDocumentIdString(
                    projectId.Value, documentPathId, documentNameId);
                return documentIdString;
            }

            void AddIfUnknownId(string? value, HashSet<string> stringsToAdd)
            {
                // Null strings are not supported at all.  Just ignore these. Any read/writes 
                // to null values will fail and will return 'false/null' to indicate failure
                // (which is part of the documented contract of the persistence layer API).
                if (value == null)
                {
                    return;
                }

                if (!_stringToIdMap.TryGetValue(value, out var id))
                {
                    stringsToAdd.Add(value);
                }
                else
                {
                    // We did know about this string.  However, we want to ensure that the 
                    // actual string instance we're pointing to is the one produced by the
                    // rest of the workspace, and not by the database.  This way we don't
                    // end up having tons of duplicate strings in the storage service.
                    //
                    // So overwrite whatever we have so far in the table so we can release
                    // the DB strings.
                    _stringToIdMap[value] = id;
                }
            }
        }

        private static readonly Action<(SQLitePersistentStorage self, HashSet<string> stringsToAdd, SqlConnection connection, Dictionary<int, string> idToString)> s_insertAllStrings =
            t =>
            {
                foreach (var value in t.stringsToAdd)
                {
                    var id = t.self.InsertStringIntoDatabase_MustRunInTransaction(t.connection, value);
                    t.idToString.Add(id, value);
                }
            };
#endif
    }
}
