<#
.SYNOPSIS
  Build and run the Document OCR test suites.

.PARAMETER Scope
  Which suite to run: 'Unit', 'Integration', or 'All' (default).

.PARAMETER NoBuild
  Skip the dotnet build step.

.PARAMETER Filter
  Optional xUnit/dotnet-test filter expression.

.PARAMETER Coverage
  Collect code coverage via the cross-platform `XPlat Code Coverage` collector.

.PARAMETER ExtraArgs
  Extra arguments forwarded verbatim to `dotnet test`.

.EXAMPLE
  ./scripts/run-tests.ps1
  ./scripts/run-tests.ps1 -Scope Unit
  ./scripts/run-tests.ps1 -Scope Integration -Filter 'FullyQualifiedName~PdfControllerTests'
  ./scripts/run-tests.ps1 -Coverage
#>
[CmdletBinding()]
param(
  [ValidateSet('Unit', 'Integration', 'All')]
  [string]$Scope = 'All',
  [switch]$NoBuild,
  [string]$Filter,
  [switch]$Coverage,
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$unitProject = Join-Path $repoRoot 'tests/DocumentOcr.UnitTests/DocumentOcr.UnitTests.csproj'
$integrationProject = Join-Path $repoRoot 'tests/DocumentOcr.IntegrationTests/DocumentOcr.IntegrationTests.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
  throw ".NET 10 SDK ('dotnet') not found in PATH."
}

$targets = switch ($Scope) {
  'Unit' { @($unitProject) }
  'Integration' { @($integrationProject) }
  'All' { @($unitProject, $integrationProject) }
}

Push-Location $repoRoot
try {
  foreach ($project in $targets) {
    if (-not $NoBuild) {
      Write-Host "==> dotnet build $project"
      dotnet build $project
      if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
    }

    $testArgs = @($project)
    if (-not $NoBuild) { $testArgs += '--no-build' }
    if ($Filter) { $testArgs += @('--filter', $Filter) }
    if ($Coverage) { $testArgs += @('--collect', 'XPlat Code Coverage') }
    if ($ExtraArgs) { $testArgs += $ExtraArgs }

    Write-Host "==> dotnet test $($testArgs -join ' ')"
    & dotnet test @testArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }
}
finally {
  Pop-Location
}

$testArgs = @($project)
if (-not $NoBuild) { $testArgs += '--no-build' }
if ($Filter) { $testArgs += @('--filter', $Filter) }
if ($Coverage) { $testArgs += @('--collect', 'XPlat Code Coverage') }
if ($ExtraArgs) { $testArgs += $ExtraArgs }

Write-Host "==> dotnet test $($testArgs -join ' ')"
& dotnet test @testArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
}
finally {
  Pop-Location
}
