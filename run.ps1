#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick debug script for full server + client testing
.DESCRIPTION
    1. Stops all running game instances
    2. Builds server + client
    3. Starts server
    4. Waits a bit, then starts client via Steam (ensures proper Steam authentication)
.NOTES
    Reads paths from local.build.props
    Client is ALWAYS launched via Steam to ensure proper authentication
#>

param(
    [switch]$NoBuild,
    [switch]$NoKill,
    [int]$ServerDelay = 5,
    [string]$SteamAppId = "3164500"
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  FULL DEBUG: Server + Client (via Steam)" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Parse local.build.props
Write-Host "Reading local.build.props..." -ForegroundColor Yellow
if (-not (Test-Path "local.build.props")) {
    Write-Host "ERROR: local.build.props not found!" -ForegroundColor Red
    exit 1
}

[xml]$buildProps = Get-Content "local.build.props"
$serverPath = $buildProps.Project.PropertyGroup.Il2CppGamePath
$clientPath = $buildProps.Project.PropertyGroup.MonoGamePath
$serverExe = Join-Path $serverPath "Schedule I.exe"
$clientExe = Join-Path $clientPath "Schedule I.exe"

Write-Host "Server: $serverPath" -ForegroundColor Gray
Write-Host "Client: $clientPath" -ForegroundColor Gray
Write-Host ""

# Step 1: Kill all instances
if (-not $NoKill) {
    Write-Host "[1/5] Stopping all game instances..." -ForegroundColor Yellow
    
    $allProcesses = Get-Process -Name "Schedule I" -ErrorAction SilentlyContinue
    
    if ($allProcesses) {
        $allProcesses | ForEach-Object {
            Write-Host "  Killing: $($_.Id) - $($_.Path)" -ForegroundColor Gray
            Stop-Process -Id $_.Id -Force
        }
        Start-Sleep -Seconds 2
        Write-Host "  ✓ All processes stopped" -ForegroundColor Green
    } else {
        Write-Host "  ✓ No processes running" -ForegroundColor Green
    }
    Write-Host ""
}

# Step 2: Build server
if (-not $NoBuild) {
    Write-Host "[2/5] Building server..." -ForegroundColor Yellow
    $buildOutput = dotnet build -c Mono_Server 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Server build FAILED!" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ Server built successfully" -ForegroundColor Green
    Write-Host ""
}

# Step 3: Start server
Write-Host "[3/5] Starting server..." -ForegroundColor Yellow

$startServerBat = Join-Path $serverPath "start_server.bat"

if (Test-Path $startServerBat) {
    Write-Host "  Using start_server.bat" -ForegroundColor Gray
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c start_server.bat" -WorkingDirectory $serverPath
} else {
    Write-Host "  Starting: $serverExe" -ForegroundColor Gray
    Start-Process -FilePath $serverExe -WorkingDirectory $serverPath
}

Write-Host "  ✓ Server started" -ForegroundColor Green
Write-Host ""

# Step 4: Build client
if (-not $NoBuild) {
    Write-Host "[4/5] Building client..." -ForegroundColor Yellow
    $buildOutput = dotnet build -c Mono_Client 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ✗ Client build FAILED!" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ Client built successfully" -ForegroundColor Green
    Write-Host ""
}

# Step 5: Wait then start client via Steam
Write-Host "[5/5] Waiting $ServerDelay seconds for server to initialize..." -ForegroundColor Yellow
for ($i = $ServerDelay; $i -gt 0; $i--) {
    Write-Host "  $i..." -ForegroundColor Gray -NoNewline
    Start-Sleep -Seconds 1
}
Write-Host ""
Write-Host ""

Write-Host "Starting client via Steam (AppID: $SteamAppId)..." -ForegroundColor Yellow
Write-Host "  NOTE: Client launched via Steam for proper authentication" -ForegroundColor Cyan
Start-Process "steam://rungameid/$SteamAppId"
Write-Host "  ✓ Client launch request sent to Steam" -ForegroundColor Green
Write-Host ""

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  ✓ Server and Client started!" -ForegroundColor Green
Write-Host "==================================================================" -ForegroundColor Cyan