param(
    [int]$Port = 8787,
    [switch]$Restart
)

. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Continue"

$healthUrl = "http://localhost:$Port/health"
$running = $false

try {
    $health = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 3
    $running = $true
    if (-not $Restart) {
        Write-Host "[OK] Match server already running: $($health | ConvertTo-Json -Compress)"
        Write-Host "To rebuild and restart with latest code: .\scripts\ensure-match-server.ps1 -Restart"
        exit 0
    }
    Write-Host "Stopping existing match server on port $Port..."
    $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($conn) {
        Stop-Process -Id $conn.OwningProcess -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}
catch {
    if ($Restart) {
        Write-Host "No running match server found."
    }
}

& "$PSScriptRoot\start-match-server.ps1" -Background -Port $Port