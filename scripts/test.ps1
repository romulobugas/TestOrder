#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$null = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'Docker is not running or not installed. Tests require Docker for Testcontainers.'
}

Write-Host 'Running tests...'
dotnet test TestOrder.slnx
exit $LASTEXITCODE
