// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Options;

namespace Microsoft.CodeAnalysis.SolutionCrawler
{
    internal static class InternalSolutionCrawlerOptions
    {
        private const string LocalRegistryPath = @"Roslyn\Internal\SolutionCrawler\";

        public static readonly Option<bool> SolutionCrawler = new Option<bool>(nameof(InternalSolutionCrawlerOptions), "Solution Crawler", defaultValue: true,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "Solution Crawler"));

        public static readonly Option<bool> DirectDependencyPropagationOnly = new Option<bool>(nameof(InternalSolutionCrawlerOptions), "Project propagation only on direct dependency", defaultValue: true,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "Project propagation only on direct dependency"));

        public static readonly Option<int> ActiveFileWorkerBackOffTimeSpanInMS = new Option<int>(nameof(InternalSolutionCrawlerOptions), "Active file worker backoff timespan in ms", defaultValue: 400,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "Active file worker backoff timespan in ms"));

        public static readonly Option<int> AllFilesWorkerBackOffTimeSpanInMS = new Option<int>(nameof(InternalSolutionCrawlerOptions), "All files worker backoff timespan in ms", defaultValue: 1500,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "All files worker backoff timespan in ms"));

        public static readonly Option<int> EntireProjectWorkerBackOffTimeSpanInMS = new Option<int>(nameof(InternalSolutionCrawlerOptions), "Entire project analysis worker backoff timespan in ms", defaultValue: 5000,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "Entire project analysis worker backoff timespan in ms"));

        public static readonly Option<int> SemanticChangeBackOffTimeSpanInMS = new Option<int>(nameof(InternalSolutionCrawlerOptions), "Semantic change backoff timespan in ms", defaultValue: 100,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "Semantic change backoff timespan in ms"));

        public static readonly Option<int> ProjectPropagationBackOffTimeSpanInMS = new Option<int>(nameof(InternalSolutionCrawlerOptions), "Project propagation backoff timespan in ms", defaultValue: 500,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "Project propagation backoff timespan in ms"));

        public static readonly Option<int> PreviewBackOffTimeSpanInMS = new Option<int>(nameof(InternalSolutionCrawlerOptions), "Preview backoff timespan in ms", defaultValue: 500,
            persistences: new LocalUserProfilePersistence(LocalRegistryPath + "Preview backoff timespan in ms"));
    }
}
