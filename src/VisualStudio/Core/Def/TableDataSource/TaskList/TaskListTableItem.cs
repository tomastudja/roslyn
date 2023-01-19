﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.TaskList;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.TableDataSource
{
    internal sealed class TaskListTableItem : TableItem
    {
        public readonly TaskListItem Data;

        private TaskListTableItem(
            Workspace workspace,
            TaskListItem data,
            string? projectName,
            Guid projectGuid,
            string[] projectNames,
            Guid[] projectGuids)
            : base(workspace, projectName, projectGuid, projectNames, projectGuids)
        {
            Data = data;
        }

        public static TaskListTableItem Create(Workspace workspace, TaskListItem data)
        {
            GetProjectNameAndGuid(workspace, data.DocumentId.ProjectId, out var projectName, out var projectGuid);
            return new TaskListTableItem(workspace, data, projectName, projectGuid, projectNames: Array.Empty<string>(), projectGuids: Array.Empty<Guid>());
        }

        public override TableItem WithAggregatedData(string[] projectNames, Guid[] projectGuids)
            => new TaskListTableItem(Workspace, Data, projectName: null, projectGuid: Guid.Empty, projectNames, projectGuids);

        public override DocumentId DocumentId
            => Data.DocumentId;

        public override ProjectId ProjectId
            => Data.DocumentId.ProjectId;

        public override LinePosition GetOriginalPosition()
            => Data.Span.StartLinePosition;

        public override string GetOriginalFilePath()
            => Data.Span.Path;

        public override bool EqualsIgnoringLocation(TableItem other)
        {
            if (other is not TaskListTableItem otherTodoItem)
            {
                return false;
            }

            var data = Data;
            var otherData = otherTodoItem.Data;
            return data.DocumentId == otherData.DocumentId && data.Message == otherData.Message;
        }

        /// <summary>
        /// Used to group diagnostics that only differ in the project they come from.
        /// We want to avoid displaying diagnostic multuple times when it is reported from 
        /// multi-targeted projects and/or files linked to multiple projects.
        /// </summary>
        internal sealed class GroupingComparer : IEqualityComparer<TaskListItem>, IEqualityComparer<TaskListTableItem>
        {
            public static readonly GroupingComparer Instance = new();

            public bool Equals(TaskListItem left, TaskListItem right)
                // We don't need to compare OriginalFilePath since TODO items are only aggregated within a single file.
                => left.Span == right.Span;

            public int GetHashCode(TaskListItem data)
                => data.Span.GetHashCode();

            public bool Equals(TaskListTableItem left, TaskListTableItem right)
            {
                if (ReferenceEquals(left, right))
                {
                    return true;
                }

                if (left is null || right is null)
                {
                    return false;
                }

                return Equals(left.Data, right.Data);
            }

            public int GetHashCode(TaskListTableItem item)
                => GetHashCode(item.Data);
        }
    }
}
