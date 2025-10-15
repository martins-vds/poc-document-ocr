#!/usr/bin/env pwsh
param(
    [Parameter(Mandatory=$true)]
    [string]$FunctionAppName
)

$ErrorActionPreference = "Stop"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Deploying Azure Function App" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Navigate to the source directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$SrcDir = Join-Path $ProjectRoot "src"

Set-Location $SrcDir

Write-Host ""
Write-Host "Function App Name: $FunctionAppName" -ForegroundColor White
Write-Host ""

# Check if Azure Functions Core Tools is available
if (-not (Get-Command func -ErrorAction SilentlyContinue)) {
    Write-Host "Error: Azure Functions Core Tools (func) is not installed" -ForegroundColor Red
    Write-Host "Please install it from: https://docs.microsoft.com/azure/azure-functions/functions-run-local" -ForegroundColor Yellow
    exit 1
}

# Check if user is logged into Azure CLI
try {
    az account show | Out-Null
} catch {
    Write-Host "Error: Not logged into Azure CLI" -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

Write-Host "Step 1: Building the function app..." -ForegroundColor Yellow
dotnet clean
dotnet restore
dotnet build --configuration Release

Write-Host ""
Write-Host "Step 2: Deploying to Azure Functions..." -ForegroundColor Yellow
func azure functionapp publish $FunctionAppName --dotnet-isolated

Write-Host ""
Write-Host "================================" -ForegroundColor Green
Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host "Function App: $FunctionAppName" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""
Write-Host "To view logs, run:" -ForegroundColor Cyan
Write-Host "  func azure functionapp logstream $FunctionAppName" -ForegroundColor White
