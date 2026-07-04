#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$ApiDir = Join-Path $RepoRoot 'src\TestOrder.Api'
$WebDir = Join-Path $RepoRoot 'src\TestOrder.Web'
$WorkerDir = Join-Path $RepoRoot 'src\TestOrder.OrderProcessor'

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

function Test-CommandContains {
    param(
        [AllowNull()] [string]$CommandLine,
        [Parameter(Mandatory)] [string]$Text
    )

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $false
    }

    return ($CommandLine.IndexOf($Text, [StringComparison]::OrdinalIgnoreCase) -ge 0)
}

function Test-IsTestOrderDevProcess {
    param(
        [Parameter(Mandatory)] $ProcessInfo
    )

    $commandLine = $ProcessInfo.CommandLine
    $name = $ProcessInfo.Name

    if (Test-CommandContains $commandLine 'TestOrder - MySQL') { return $true }
    if (Test-CommandContains $commandLine 'TestOrder - API') { return $true }
    if (Test-CommandContains $commandLine 'TestOrder - Web') { return $true }
    if (Test-CommandContains $commandLine 'TestOrder - Worker') { return $true }

    if (Test-CommandContains $commandLine $ApiDir) { return $true }
    if (Test-CommandContains $commandLine 'src\TestOrder.Api') { return $true }
    if (Test-CommandContains $commandLine 'TestOrder.Api.dll') { return $true }
    if (Test-CommandContains $commandLine 'TestOrder.Api.exe') { return $true }

    if (($name -in @('node.exe', 'esbuild.exe', 'cmd.exe')) -and
        (Test-CommandContains $commandLine $WebDir) -and
        ((Test-CommandContains $commandLine 'vite') -or
         (Test-CommandContains $commandLine 'esbuild') -or
         (Test-CommandContains $commandLine 'node_modules'))) {
        return $true
    }

    if (($name -in @('node.exe', 'cmd.exe')) -and
        ((Test-CommandContains $commandLine $WorkerDir) -or
         (Test-CommandContains $commandLine 'TestOrder.OrderProcessor'))) {
        return $true
    }

    return $false
}

function Stop-ProcessTree {
    param(
        [Parameter(Mandatory)] [int]$ProcessId
    )

    if ($ProcessId -eq $PID) {
        return
    }

    $children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        Stop-ProcessTree -ProcessId $child.ProcessId
    }

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if ($null -ne $process) {
        Write-Host "Stopping previous TestOrder process: $($process.ProcessName) ($ProcessId)"
        Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Stop-TestOrderDevProcesses {
    Write-Host 'Cleaning previous TestOrder service processes...'

    $currentProcessId = $PID
    $processes = Get-CimInstance Win32_Process |
        Where-Object { $_.ProcessId -ne $currentProcessId -and (Test-IsTestOrderDevProcess $_) } |
        Sort-Object ProcessId -Unique

    if ($null -eq $processes -or $processes.Count -eq 0) {
        Write-Host 'No previous TestOrder service processes found.'
        return
    }

    foreach ($processInfo in $processes) {
        Stop-ProcessTree -ProcessId $processInfo.ProcessId
    }

    Start-Sleep -Milliseconds 800
}

function Assert-PortAvailable {
    param(
        [Parameter(Mandatory)] [int]$Port,
        [Parameter(Mandatory)] [string]$ServiceName
    )

    $listeners = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($null -eq $listeners) {
        return
    }

    $owners = foreach ($listener in $listeners) {
        $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)" -ErrorAction SilentlyContinue
        if ($null -ne $processInfo) {
            "PID $($listener.OwningProcess): $($processInfo.CommandLine)"
        } else {
            "PID $($listener.OwningProcess): <process not found>"
        }
    }

    throw "$ServiceName port $Port is still in use after cleanup. Close it manually and rerun dev-up.ps1. Owners: $($owners -join ' | ')"
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

Stop-TestOrderDevProcesses
Assert-PortAvailable -Port 5069 -ServiceName 'API'
Assert-PortAvailable -Port 5173 -ServiceName 'Frontend'

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

$WorkerNodeModules = Join-Path $WorkerDir 'node_modules'
if (-not (Test-Path $WorkerNodeModules)) {
    Write-Host 'Worker dependencies not found - running npm install (first run)...'
    Push-Location $WorkerDir
    npm install
    $npmInstallExitCode = $LASTEXITCODE
    Pop-Location
    if ($npmInstallExitCode -ne 0) {
        throw 'npm install failed.'
    }
}

Write-Host 'Opening service windows (MySQL logs, API, Web, Worker)...'

Start-ServiceWindow -Title 'TestOrder - MySQL' -Command 'docker compose logs -f mysql'
Start-ServiceWindow -Title 'TestOrder - API' -Command 'dotnet run --project src\TestOrder.Api'
Start-ServiceWindow -Title 'TestOrder - Web' -Command 'npm run dev' -WorkingDirectory $WebDir
Start-ServiceWindow -Title 'TestOrder - Worker' -Command 'node index.js' -WorkingDirectory $WorkerDir

Write-Host ''
Write-Host 'Services starting in separate windows:'
Write-Host '  Backend:  http://localhost:5069'
Write-Host '  Frontend: http://localhost:5173'
Write-Host '  MySQL:    localhost:3306'
Write-Host '  Worker:   see "TestOrder - Worker" window'
Write-Host ''
Write-Host 'Close each window (or Ctrl+C inside it) to stop that service.'
