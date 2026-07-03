#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Assert-DockerAvailable {
    $null = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker is not running or not installed. Start Docker Desktop and try again.'
    }
}

function Wait-MySqlReady {
    param(
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    Write-Host 'Waiting for MySQL to accept connections...'

    while ((Get-Date) -lt $deadline) {
        $health = docker compose ps mysql --format '{{.Health}}' 2>$null
        if ($health -eq 'healthy') {
            Write-Host 'MySQL is ready.'
            return
        }

        $ping = docker compose exec -T mysql mysqladmin ping -h localhost -uroot -ptestorder 2>&1
        if ($LASTEXITCODE -eq 0 -and ($ping -match 'mysqld is alive')) {
            Write-Host 'MySQL is ready.'
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "MySQL did not become ready within $TimeoutSeconds seconds."
}

Assert-DockerAvailable

Write-Host 'Starting MySQL with Docker Compose...'
docker compose up -d mysql
if ($LASTEXITCODE -ne 0) {
    throw 'docker compose up failed.'
}

Wait-MySqlReady

Write-Host 'Building solution...'
dotnet build TestOrder.slnx
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host 'Starting API (foreground). Press Ctrl+C to stop.'
dotnet run --project src/TestOrder.Api
