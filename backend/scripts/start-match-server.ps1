param(
    [int]$Port = 8787,
    [switch]$Background
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "DreamGate.MatchServer"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK is required."
}

$env:PORT = "$Port"
$healthUrl = "http://localhost:$Port/health"

try {
    $existing = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2
    Write-Host "[OK] Match server already listening on port ${Port}: $($existing | ConvertTo-Json -Compress)"
    exit 0
}
catch {
    # not running; continue startup
}

$publishDir = Join-Path $root "publish\match-server"
Push-Location $project
Write-Host "Publishing Dream Gate authoritative match server..."
dotnet publish -c Release -o $publishDir -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "[WARN] Publish failed (server may be running). Attempting to start existing binary..."
}

if ($Background) {
    $logDir = Join-Path $root "logs"
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
    $logPath = Join-Path $logDir "match-server.log"
    Write-Host "Starting match server in background on port $Port..."
    $proc = Start-Process -FilePath "dotnet" -ArgumentList (Join-Path $publishDir "DreamGate.MatchServer.dll") -WorkingDirectory $publishDir -PassThru -RedirectStandardOutput $logPath -RedirectStandardError (Join-Path $logDir "match-server.err.log")
    Start-Sleep -Seconds 5
    try {
        $health = Invoke-RestMethod -Uri "http://localhost:$Port/health" -TimeoutSec 5
        Write-Host "[OK] Match server running (pid $($proc.Id)): $($health | ConvertTo-Json -Compress)"
        Write-Host "Log: $logPath"
    }
    catch {
        Write-Host "[WARN] Server started but health check failed. See log: $logPath"
    }
}
else {
    Write-Host "Starting Dream Gate authoritative match server on port $Port..."
    dotnet (Join-Path $publishDir "DreamGate.MatchServer.dll")
}
Pop-Location