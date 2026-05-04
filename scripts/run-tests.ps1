<#
.SYNOPSIS
  Build and run the Document OCR unit-test suite.

.PARAMETER NoBuild
  Skip the dotnet build step.

.PARAMETER Filter
  Optional xUnit/dotnet-test filter expression (e.g. 'FullyQualifiedName~DocumentSchemaMapperServiceTests').

.PARAMETER Coverage
  Collect code coverage via the cross-platform `XPlat Code Coverage` collector.

.PARAMETER ExtraArgs
  Extra arguments forwarded verbatim to `dotnet test`.

.EXAMPLE
  ./scripts/run-tests.ps1
  ./scripts/run-tests.ps1 -Filter 'FullyQualifiedName~DocumentSchemaMapperServiceTests'
  ./scripts/run-tests.ps1 -Coverage
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [string]$Filter,
    [switch]$Coverage,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$testProject = Join-Path $repoRoot 'tests/DocumentOcr.Tests.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK ('dotnet') not found in PATH."
}

Push-Location $repoRoot
try {
    if (-not $NoBuild) {
        Write-Host "==> dotnet build $testProject"
        dotnet build $testProject
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
    }

    $testArgs = @($testProject)
    if (-not $NoBuild) { $testArgs += '--no-build' }
    if ($Filter) { $testArgs += @('--filter', $Filter) }
    if ($Coverage) { $testArgs += @('--collect', 'XPlat Code Coverage') }
    if ($ExtraArgs) { $testArgs += $ExtraArgs }

    Write-Host "==> dotnet test $($testArgs -join ' ')"
    & dotnet test @testArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
