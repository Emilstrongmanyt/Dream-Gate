param(
    [string]$PublicIp,
    [int]$Port = 8787,
    [switch]$SkipFirewall,
    [switch]$SkipSupabaseSecret
)

. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Stop"
Import-BackendCli

if (-not $PublicIp) {
    $PublicIp = Get-PublicIpAddress
}
if (-not $PublicIp) {
    throw "Could not detect public IP. Pass -PublicIp explicitly."
}

$publicUrl = "http://${PublicIp}:$Port"
$root = Get-BackendRoot
$envValues = Read-BackendEnv

if (-not $SkipFirewall) {
    $ruleName = "Dream Gate Match Server"
    $existing = netsh advfirewall firewall show rule name="$ruleName" 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Opening Windows Firewall TCP $Port for internet access..."
        netsh advfirewall firewall add rule name="$ruleName" dir=in action=allow protocol=TCP localport=$Port | Out-Null
        Write-Host "[OK] Firewall rule added"
    }
    else {
        Write-Host "[OK] Firewall rule already exists"
    }
}

& "$PSScriptRoot\configure-backend.ps1" `
    -SupabaseUrl $envValues["SUPABASE_URL"] `
    -AnonKey $envValues["SUPABASE_ANON_KEY"] `
    -ServiceRoleKey $envValues["SUPABASE_SERVICE_ROLE_KEY"] `
    -ProjectRef $envValues["PROJECT_REF"] `
    -MatchServerUrl $publicUrl `
    -EnableCloud

if (-not $SkipSupabaseSecret -and $envValues["PROJECT_REF"]) {
    Write-Host "Updating Supabase MATCH_SERVER_URL secret..."
    supabase secrets set --project-ref $envValues["PROJECT_REF"] "MATCH_SERVER_URL=$publicUrl"
    Write-Host "[OK] Supabase secret updated"
}

& "$PSScriptRoot\ensure-match-server.ps1" -Port $Port

Write-Host ""
Write-Host "Public match server URL: $publicUrl"
Write-Host ""
Write-Host "Phones on any network can use this URL after:"
Write-Host "  1. VPS/hosting firewall allows inbound TCP $Port"
Write-Host "  2. New TestFlight build is installed (iOS ATS plist included)"
Write-Host ""
Write-Host "Test from another network:"
Write-Host "  curl $publicUrl/health"