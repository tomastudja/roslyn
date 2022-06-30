[CmdletBinding(PositionalBinding=$false)]
param (
  [switch]$ci
)

Set-StrictMode -version 2.0
$ErrorActionPreference="Stop"


Write-Host "Building Microsoft.CodeAnalysis.Features"
try {
    Invoke-Expression "./.dotnet/dotnet.exe build src/Features/Core/Portable/Microsoft.CodeAnalysis.Features.csproj -t:GenerateRulesMissingDocumentation -p:RoslynEnforceCodeStyle=false -p:RunAnalyzersDuringBuild=false -p:ContinuousIntegrationBuild=$ci -c Release"
}
catch {
    Write-Host $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    $host.SetShouldExit(1)
    exit 1
}
