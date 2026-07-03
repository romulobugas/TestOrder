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

function Test-PortInUse {
    param([int]$Port)

    $listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    return ($null -ne $listeners)
}

function Start-ServiceWindow {
    param(
        [Parameter(Mandatory)] [string]$Title,
        [Parameter(Mandatory)] [string]$Command,
        [string]$WorkingDirectory = $RepoRoot
    )

    # /k mantem a janela aberta apos o comando, para acompanhar os logs em tempo real.
    $commandLine = "title $Title && $Command"
    Start-Process -FilePath 'cmd.exe' -ArgumentList '/k', $commandLine -WorkingDirectory $WorkingDirectory | Out-Null
}

Assert-DockerAvailable

Write-Host 'Starting MySQL with Docker Compose...'
docker compose up -d mysql
if ($LASTEXITCODE -ne 0) {
    throw 'docker compose up failed.'
}

Wait-MySqlReady

$ApiPortInUse = Test-PortInUse -Port 5069
$WebPortInUse = Test-PortInUse -Port 5173

if ($ApiPortInUse) {
    Write-Warning 'Port 5069 is already in use. If this is a previous TestOrder API window, close it before rerunning; dotnet build may fail on Windows while the API executable is in use.'
}

Write-Host 'Building solution...'
dotnet build TestOrder.slnx
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$WebDir = Join-Path $RepoRoot 'src\TestOrder.Web'
$WebNodeModules = Join-Path $WebDir 'node_modules'
if (-not (Test-Path $WebNodeModules)) {
    Write-Host 'Frontend dependencies not found - running npm install (first run)...'
    Push-Location $WebDir
    npm install
    $npmInstallExitCode = $LASTEXITCODE
    Pop-Location
    if ($npmInstallExitCode -ne 0) {
        throw 'npm install failed.'
    }
}

if ($ApiPortInUse) {
    Write-Warning 'Port 5069 is still in use - the API window may fail to bind.'
}
if ($WebPortInUse) {
    Write-Warning 'Port 5173 is already in use - Vite will pick another port (check the "TestOrder - Web" window).'
}

Write-Host 'Opening service windows (MySQL logs, API, Web)...'

Start-ServiceWindow -Title 'TestOrder - MySQL' -Command 'docker compose logs -f mysql'
Start-ServiceWindow -Title 'TestOrder - API' -Command 'dotnet run --project src\TestOrder.Api'
Start-ServiceWindow -Title 'TestOrder - Web' -Command 'npm run dev' -WorkingDirectory $WebDir

Write-Host ''
Write-Host 'Services starting in separate windows:'
Write-Host '  Backend:  http://localhost:5069'
if ($WebPortInUse) {
    Write-Host '  Frontend: check the "TestOrder - Web" window for the Vite port'
} else {
    Write-Host '  Frontend: http://localhost:5173'
}
Write-Host '  MySQL:    localhost:3306'
Write-Host ''
Write-Host 'Close each window (or Ctrl+C inside it) to stop that service.'
