<#
.SYNOPSIS
  Build and run the Document OCR Blazor Web App locally.

.PARAMETER NoBuild
  Skip the dotnet build step.

.PARAMETER Urls
  Optional ASP.NET Core URLs (e.g. 'http://localhost:5000;https://localhost:5001').

.PARAMETER ExtraArgs
  Extra arguments forwarded verbatim to `dotnet run`.

.EXAMPLE
  ./scripts/run-webapp.ps1
  ./scripts/run-webapp.ps1 -Urls 'http://localhost:5000'
#>
[CmdletBinding()]
param(
    [switch]$NoBuild,
    [string]$Urls,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectDir = Join-Path $repoRoot 'src/DocumentOcr.WebApp'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK ('dotnet') not found in PATH."
}

if (-not (Test-Path (Join-Path $projectDir 'appsettings.Development.json'))) {
    Write-Warning "$projectDir/appsettings.Development.json is missing. Copy appsettings.Development.json.template and fill it in."
}

Push-Location $projectDir
try {
    if (-not $NoBuild) {
        Write-Host "==> dotnet build $projectDir"
        dotnet build
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
    }

    $runArgs = @('run', '--no-build')
    if ($Urls) { $runArgs += @('--urls', $Urls) }
    if ($ExtraArgs) { $runArgs += $ExtraArgs }

    Write-Host "==> dotnet $($runArgs -join ' ')"
    & dotnet @runArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
