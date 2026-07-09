param(
    [Parameter(Mandatory = $true)]
    [string]$SupabaseUrl,
    [Parameter(Mandatory = $true)]
    [string]$AnonKey,
    [Parameter(Mandatory = $true)]
    [string]$ServiceRoleKey,
    [Parameter(Mandatory = $true)]
    [string]$ProjectRef,
    [string]$MatchServerUrl,
    [switch]$SkipLink,
    [switch]$SkipMigration,
    [switch]$SkipDeploy
)

. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Stop"
Import-BackendCli

if (-not $MatchServerUrl) {
    $lanIp = Get-LanIpAddress
    if ($lanIp) {
        $MatchServerUrl = "http://${lanIp}:8787"
    }
    else {
        $MatchServerUrl = "http://localhost:8787"
    }
}

Write-Host "Step 1/5: Configure Unity + .env"
& "$PSScriptRoot\configure-backend.ps1" `
    -SupabaseUrl $SupabaseUrl `
    -AnonKey $AnonKey `
    -ServiceRoleKey $ServiceRoleKey `
    -ProjectRef $ProjectRef `
    -MatchServerUrl $MatchServerUrl `
    -EnableCloud

if (-not $SkipLink) {
    Write-Host ""
    Write-Host "Step 2/5: Link Supabase project"
    & "$PSScriptRoot\link-project.ps1"
}

if (-not $SkipMigration) {
    Write-Host ""
    Write-Host "Step 3/5: Apply database migration"
    & "$PSScriptRoot\setup-supabase.ps1"
}

if (-not $SkipDeploy) {
    Write-Host ""
    Write-Host "Step 4/5: Deploy edge functions"
    & "$PSScriptRoot\deploy-functions.ps1"
}

Write-Host ""
Write-Host "Step 5/5: Verify"
& "$PSScriptRoot\test-backend.ps1"
& "$PSScriptRoot\setup-status.ps1"