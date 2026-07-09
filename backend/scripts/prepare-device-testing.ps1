param(
    [string]$LanIp,
    [int]$Port = 8787,
    [switch]$SkipFirewall
)

. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Stop"

if (-not $LanIp) {
    $LanIp = Get-LanIpAddress
}
if (-not $LanIp) {
    throw "Could not detect LAN IP. Pass -LanIp explicitly."
}

$deviceUrl = "http://${LanIp}:$Port"
$root = Get-BackendRoot

if (-not $SkipFirewall) {
    $ruleName = "Dream Gate Match Server"
    $existing = netsh advfirewall firewall show rule name="$ruleName" 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Opening Windows Firewall port $Port..."
        netsh advfirewall firewall add rule name="$ruleName" dir=in action=allow protocol=TCP localport=$Port | Out-Null
        Write-Host "[OK] Firewall rule added for TCP $Port"
    }
    else {
        Write-Host "[OK] Firewall rule already exists"
    }
}

& "$PSScriptRoot\configure-backend.ps1" -LocalOnly -MatchServerUrl $deviceUrl

Write-Host ""
Write-Host "Device testing configured."
Write-Host "  Match server URL: $deviceUrl"
Write-Host "  Phone must be on the same network as this PC."
Write-Host ""
Write-Host "Start server (if not running):"
Write-Host "  .\scripts\start-match-server.ps1 -Background"