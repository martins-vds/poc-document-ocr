#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Building Azure Function App" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

# Navigate to the source directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$SrcDir = Join-Path $ProjectRoot "src"

Set-Location $SrcDir

Write-Host ""
Write-Host "Step 1: Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean

Write-Host ""
Write-Host "Step 2: Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore

Write-Host ""
Write-Host "Step 3: Building the project..." -ForegroundColor Yellow
dotnet build --configuration Release

Write-Host ""
Write-Host "Step 4: Publishing the function app..." -ForegroundColor Yellow
dotnet publish --configuration Release --output ./publish

Write-Host ""
Write-Host "================================" -ForegroundColor Green
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Published to: $SrcDir\publish" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
