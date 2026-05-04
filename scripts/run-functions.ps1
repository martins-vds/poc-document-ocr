<#
.SYNOPSIS
  Build and run the Document OCR Function App locally.

.PARAMETER NoBuild
  Skip the dotnet build step.

.PARAMETER ExtraArgs
  Extra arguments forwarded verbatim to `func start`.

.EXAMPLE
  ./scripts/run-functions.ps1
  ./scripts/run-functions.ps1 -NoBuild
  ./scripts/run-functions.ps1 -ExtraArgs '--port','7072'
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectDir = Join-Path $repoRoot 'src/DocumentOcr.Processor'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK ('dotnet') not found in PATH."
}
if (-not (Get-Command func -ErrorAction SilentlyContinue)) {
    throw "Azure Functions Core Tools v4 ('func') not found in PATH."
}

if (-not (Test-Path (Join-Path $projectDir 'local.settings.json'))) {
    Write-Warning "$projectDir/local.settings.json is missing. Copy local.settings.json.template and fill it in."
}

Push-Location $projectDir
try {
    if (-not $NoBuild) {
        Write-Host "==> dotnet build $projectDir"
        dotnet build
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
    }
    Write-Host "==> func start $($ExtraArgs -join ' ')"
    & func start @ExtraArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
