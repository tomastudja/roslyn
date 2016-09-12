﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.VisualStudio.LanguageServices.ProjectSystem
{
    /// <summary>
    /// Project context to initialize properties and items of a Workspace project created with <see cref="IWorkspaceProjectContextFactory.CreateProjectContext"/>. 
    /// </summary>
    internal interface IWorkspaceProjectContext : IDisposable
    {
        // Project properties.
        string DisplayName { get; set; }
        string ProjectFilePath { get; set; }
        Guid Guid { get; set; }
        bool LastDesignTimeBuildSucceeded { get; set; }
        string BinOutputPath { get; set; }

        // Options.
        void SetOptions(string commandLineForOptions);

        // References.
        void AddMetadataReference(string referencePath, MetadataReferenceProperties properties);
        void RemoveMetadataReference(string referencePath);
        void AddProjectReference(IWorkspaceProjectContext project, MetadataReferenceProperties properties);
        void RemoveProjectReference(IWorkspaceProjectContext project);
        void AddAnalyzerReference(string referencePath);
        void RemoveAnalyzerReference(string referencePath);

        // Files.
        void AddSourceFile(string filePath, bool isInCurrentContext = true, IEnumerable<string> folderNames = null, SourceCodeKind sourceCodeKind = SourceCodeKind.Regular);
        void RemoveSourceFile(string filePath);
        void AddAdditionalFile(string filePath, bool isInCurrentContext = true);
        void RemoveAdditionalFile(string filePath);
        void SetRuleSetFile(string filePath);
    }
}
