﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Roslyn.Utilities;

#pragma warning disable RS0013 // We need to invoke Diagnostic.Descriptor here to log all the metadata properties of the diagnostic.

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Used for logging all compiler diagnostics into a given <see cref="Stream"/>.
    /// This logger is responsible for closing the given stream on <see cref="Dispose"/>.
    /// The log format is SARIF (Static Analysis Results Interchange Format)
    ///
    /// https://sarifweb.azurewebsites.net
    /// https://github.com/sarif-standard/sarif-spec
    /// </summary>
    internal partial class ErrorLogger : IDisposable
    {
        // Internal for testing purposes.
        internal const string OutputFormatVersion = "1.0.0-beta.4";

        private readonly JsonWriter _writer;

        public ErrorLogger(Stream stream, string toolName, string toolFileVersion, Version toolAssemblyVersion)
        {
            Debug.Assert(stream != null);
            Debug.Assert(stream.Position == 0);

            _writer = new JsonWriter(new StreamWriter(stream));

            _writer.WriteObjectStart(); // root
            _writer.Write("version", OutputFormatVersion);

            _writer.WriteArrayStart("runs");
            _writer.WriteObjectStart(); // run

            WriteToolInfo(toolName, toolFileVersion, toolAssemblyVersion);

            _writer.WriteArrayStart("results");
        }

        private void WriteToolInfo(string name, string fileVersion, Version assemblyVersion)
        {
            _writer.WriteObjectStart("tool");
            _writer.Write("name", name);
            _writer.Write("version", assemblyVersion.ToString(fieldCount: 3));
            _writer.Write("fileVersion", fileVersion);
            _writer.WriteObjectEnd();
        }

        internal void LogDiagnostic(Diagnostic diagnostic, CultureInfo culture)
        {
            _writer.WriteObjectStart(); // result
            _writer.Write("ruleId", diagnostic.Id);
            _writer.Write("level", GetLevel(diagnostic.Severity));

            WriteLocations(diagnostic.Location, diagnostic.AdditionalLocations);

            string message = diagnostic.GetMessage(culture);
            if (string.IsNullOrEmpty(message))
            {
                message = "<None>";
            }

            _writer.Write("message", message);

            if (diagnostic.IsSuppressed)
            {
                _writer.WriteArrayStart("suppressionStates");
                _writer.Write("suppressedInSource");
                _writer.WriteArrayEnd();
            }

            WriteTags(diagnostic);

            WriteProperties(diagnostic, culture);

            _writer.WriteObjectEnd(); // result
        }

        private void WriteLocations(Location location, IReadOnlyList<Location> additionalLocations)
        {
            if (location.SourceTree != null)
            {
                _writer.WriteArrayStart("locations");
                _writer.WriteObjectStart(); // location
                _writer.WriteKey("analysisTarget");

                WritePhysicalLocation(location);

                _writer.WriteObjectEnd(); // location
                _writer.WriteArrayEnd(); // locations
            }

            // See https://github.com/dotnet/roslyn/issues/11228 for discussion around
            // whether this is the correct treatment of Diagnostic.AdditionalLocations
            // as SARIF relatedLocations.
            if (additionalLocations != null &&
                additionalLocations.Count > 0 &&
                additionalLocations.Any(l => l.SourceTree != null))
            {
                _writer.WriteArrayStart("relatedLocations");

                foreach (var additionalLocation in additionalLocations)
                {
                    if (additionalLocation.SourceTree != null)
                    {
                        _writer.WriteObjectStart(); // annotatedCodeLocation
                        _writer.WriteKey("physicalLocation");

                        WritePhysicalLocation(additionalLocation);

                        _writer.WriteObjectEnd(); // annotatedCodeLocation
                    }
                }

                _writer.WriteArrayEnd(); // relatedLocations
            }
        }

        private void WritePhysicalLocation(Location location)
        {
            Debug.Assert(location.SourceTree != null);

            
            _writer.WriteObjectStart();
            _writer.Write("uri", GetUri(location.SourceTree));

            // Note that SARIF lines and columns are 1-based, but FileLinePositionSpan is 0-based
            FileLinePositionSpan span = location.GetLineSpan();
            _writer.WriteObjectStart("region");
            _writer.Write("startLine", span.StartLinePosition.Line + 1);
            _writer.Write("startColumn", span.StartLinePosition.Character + 1);
            _writer.Write("endLine", span.EndLinePosition.Line + 1);
            _writer.Write("endColumn", span.EndLinePosition.Character + 1);
            _writer.WriteObjectEnd(); // region

            _writer.WriteObjectEnd();
        }

        private static string GetUri(SyntaxTree syntaxTree)
        {
            Uri uri;

            if (!Uri.TryCreate(syntaxTree.FilePath, UriKind.RelativeOrAbsolute, out uri))
            {
                // The only constraint on SyntaxTree.FilePath is that it can be interpreted by
                // various resolvers so there is no guarantee we can turn the arbitrary string
                // in to a URI. If our attempt to do so fails, use the original string as the
                // "URI".
                return syntaxTree.FilePath;
            }

            return uri.ToString();
        }

        private void WriteTags(Diagnostic diagnostic)
        {
            if (diagnostic.CustomTags.Count > 0)
            {
                _writer.WriteArrayStart("tags");

                foreach (string tag in diagnostic.CustomTags)
                {
                    _writer.Write(tag);
                }

                _writer.WriteArrayEnd();
            }
        }

        private void WriteProperties(Diagnostic diagnostic, CultureInfo culture)
        {
            _writer.WriteObjectStart("properties");

            _writer.Write("severity", diagnostic.Severity.ToString());

            if (diagnostic.Severity == DiagnosticSeverity.Warning)
            {
                _writer.Write("warningLevel", diagnostic.WarningLevel.ToString());
            }

            _writer.Write("defaultSeverity", diagnostic.DefaultSeverity.ToString());

            string title = diagnostic.Descriptor.Title.ToString(culture);
            if (!string.IsNullOrEmpty(title))
            {
                _writer.Write("title", title);
            }

            _writer.Write("category", diagnostic.Category);

            string helpLink = diagnostic.Descriptor.HelpLinkUri;
            if (!string.IsNullOrEmpty(helpLink))
            {
                _writer.Write("helpLink", helpLink);
            }

            _writer.Write("isEnabledByDefault", diagnostic.IsEnabledByDefault.ToString());

            foreach (var pair in diagnostic.Properties.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                _writer.Write("customProperties." + pair.Key, pair.Value);
            }

            _writer.WriteObjectEnd(); // properties
        }

        private static string GetLevel(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Info:
                    return "note";

                case DiagnosticSeverity.Error:
                    return "error";

                case DiagnosticSeverity.Warning:
                case DiagnosticSeverity.Hidden:
                default:
                    // note that in the hidden or default cases, we still write out the actual severity as a
                    // property so no information is lost. We have to conform to the SARIF spec for kind,
                    // which allows only pass, warning, error, or notApplicable.
                    return "warning";
            }
        }

        public void Dispose()
        {
            _writer.WriteArrayEnd();  // results
            _writer.WriteObjectEnd(); // run
            _writer.WriteArrayEnd();  // runs
            _writer.WriteObjectEnd(); // root
            _writer.Dispose();
        }
    }
}

#pragma warning restore RS0013