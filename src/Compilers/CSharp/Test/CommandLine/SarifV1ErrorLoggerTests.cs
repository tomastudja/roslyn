﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

using static Microsoft.CodeAnalysis.CommonDiagnosticAnalyzers;
using static Microsoft.CodeAnalysis.DiagnosticExtensions;
using static Roslyn.Test.Utilities.SharedResourceHelpers;

namespace Microsoft.CodeAnalysis.CSharp.CommandLine.UnitTests
{
    [Trait(Traits.Feature, Traits.Features.SarifErrorLogging)]
    public class SarifV1ErrorLoggerTests : SarifErrorLoggerTests
    {
        protected override string[] VersionSpecificArguments => new string[0];

        internal override string GetExpectedOutputForNoDiagnostics(CommonCompiler cmd)
        {
            var expectedHeader = GetExpectedErrorLogHeader(cmd);
            var expectedIssues = @"
      ""results"": [
      ]
    }
  ]
}";
            return expectedHeader + expectedIssues;
        }

        [ConditionalFact(typeof(WindowsOnly), Reason = "https://github.com/dotnet/roslyn/issues/30289")]
        public void NoDiagnostics()
        {
            NoDiagnosticsImpl();
        }

        internal override string GetExpectedOutputForSimpleCompilerDiagnostics(CommonCompiler cmd, string sourceFile)
        {
            var expectedHeader = GetExpectedErrorLogHeader(cmd);
            var expectedIssues = string.Format(@"
      ""results"": [
        {{
          ""ruleId"": ""CS5001"",
          ""level"": ""error"",
          ""message"": ""Program does not contain a static 'Main' method suitable for an entry point""
        }},
        {{
          ""ruleId"": ""CS0169"",
          ""level"": ""warning"",
          ""message"": ""The field 'C.x' is never used"",
          ""locations"": [
            {{
              ""resultFile"": {{
                ""uri"": ""{0}"",
                ""region"": {{
                  ""startLine"": 4,
                  ""startColumn"": 17,
                  ""endLine"": 4,
                  ""endColumn"": 18
                }}
              }}
            }}
          ],
          ""properties"": {{
            ""warningLevel"": 3
          }}
        }}
      ],
      ""rules"": {{
        ""CS0169"": {{
          ""id"": ""CS0169"",
          ""shortDescription"": ""Field is never used"",
          ""defaultLevel"": ""warning"",
          ""properties"": {{
            ""category"": ""Compiler"",
            ""isEnabledByDefault"": true,
            ""tags"": [
              ""Compiler"",
              ""Telemetry""
            ]
          }}
        }},
        ""CS5001"": {{
          ""id"": ""CS5001"",
          ""defaultLevel"": ""error"",
          ""properties"": {{
            ""category"": ""Compiler"",
            ""isEnabledByDefault"": true,
            ""tags"": [
              ""Compiler"",
              ""Telemetry"",
              ""NotConfigurable""
            ]
          }}
        }}
      }}
    }}
  ]
}}", AnalyzerForErrorLogTest.GetUriForPath(sourceFile));

            return expectedHeader + expectedIssues;
        }

        [ConditionalFact(typeof(WindowsOnly), Reason = "https://github.com/dotnet/roslyn/issues/30289")]
        public void SimpleCompilerDiagnostics()
        {
            SimpleCompilerDiagnosticsImpl();
        }

        internal override string GetExpectedOutputForSimpleCompilerDiagnosticsSuppressed(CommonCompiler cmd, string sourceFile)
        {
            var expectedHeader = GetExpectedErrorLogHeader(cmd);
            var expectedIssues = string.Format(@"
      ""results"": [
        {{
          ""ruleId"": ""CS5001"",
          ""level"": ""error"",
          ""message"": ""Program does not contain a static 'Main' method suitable for an entry point""
        }},
        {{
          ""ruleId"": ""CS0169"",
          ""level"": ""warning"",
          ""message"": ""The field 'C.x' is never used"",
          ""suppressionStates"": [
            ""suppressedInSource""
          ],
          ""locations"": [
            {{
              ""resultFile"": {{
                ""uri"": ""{0}"",
                ""region"": {{
                  ""startLine"": 5,
                  ""startColumn"": 17,
                  ""endLine"": 5,
                  ""endColumn"": 18
                }}
              }}
            }}
          ],
          ""properties"": {{
            ""warningLevel"": 3
          }}
        }}
      ],
      ""rules"": {{
        ""CS0169"": {{
          ""id"": ""CS0169"",
          ""shortDescription"": ""Field is never used"",
          ""defaultLevel"": ""warning"",
          ""properties"": {{
            ""category"": ""Compiler"",
            ""isEnabledByDefault"": true,
            ""tags"": [
              ""Compiler"",
              ""Telemetry""
            ]
          }}
        }},
        ""CS5001"": {{
          ""id"": ""CS5001"",
          ""defaultLevel"": ""error"",
          ""properties"": {{
            ""category"": ""Compiler"",
            ""isEnabledByDefault"": true,
            ""tags"": [
              ""Compiler"",
              ""Telemetry"",
              ""NotConfigurable""
            ]
          }}
        }}
      }}
    }}
  ]
}}", AnalyzerForErrorLogTest.GetUriForPath(sourceFile));

            return expectedHeader + expectedIssues;
        }

        [ConditionalFact(typeof(WindowsOnly), Reason = "https://github.com/dotnet/roslyn/issues/30289")]
        public void SimpleCompilerDiagnosticsSuppressed()
        {
            SimpleCompilerDiagnosticsSuppressedImpl();
        }

        [ConditionalFact(typeof(WindowsOnly), Reason = "https://github.com/dotnet/roslyn/issues/30289")]
        public void AnalyzerDiagnosticsWithAndWithoutLocation()
        {
            var source = @"
public class C
{
}";
            var sourceFile = Temp.CreateFile().WriteAllText(source).Path;
            var outputDir = Temp.CreateDirectory();
            var errorLogFile = Path.Combine(outputDir.Path, "ErrorLog.txt");
            var outputFilePath = Path.Combine(outputDir.Path, "test.dll");

            var cmd = CreateCSharpCompiler(null, WorkingDirectory, new[] {
                "/nologo", "/t:library", $"/out:{outputFilePath}", sourceFile, "/preferreduilang:en", $"/errorlog:{errorLogFile}" },
               analyzers: ImmutableArray.Create<DiagnosticAnalyzer>(new AnalyzerForErrorLogTest()));

            var outWriter = new StringWriter(CultureInfo.InvariantCulture);

            var exitCode = cmd.Run(outWriter);
            var actualConsoleOutput = outWriter.ToString().Trim();

            Assert.Contains(AnalyzerForErrorLogTest.Descriptor1.Id, actualConsoleOutput);
            Assert.Contains(AnalyzerForErrorLogTest.Descriptor2.Id, actualConsoleOutput);
            Assert.NotEqual(0, exitCode);

            var actualOutput = File.ReadAllText(errorLogFile).Trim();

            var expectedHeader = GetExpectedErrorLogHeader(cmd);
            var expectedIssues = AnalyzerForErrorLogTest.GetExpectedV1ErrorLogResultsAndRulesText(cmd.Compilation);
            var expectedText = expectedHeader + expectedIssues;
            Assert.Equal(expectedText, actualOutput);

            CleanupAllGeneratedFiles(sourceFile);
            CleanupAllGeneratedFiles(outputFilePath);
            CleanupAllGeneratedFiles(errorLogFile);
        }
    }
}
