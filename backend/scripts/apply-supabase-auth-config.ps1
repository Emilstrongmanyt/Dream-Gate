param(
    [string]$ProjectRef = "hekknzzbudmkwxtzxkdi"
)

$ErrorActionPreference = "Stop"
$backendRoot = Split-Path $PSScriptRoot -Parent
$supabaseExe = Join-Path $backendRoot "tools\supabase.exe"

if (-not (Test-Path $supabaseExe)) {
    throw "Supabase CLI not found at $supabaseExe"
}

Write-Host "Applying Dream Gate auth config to project $ProjectRef..."
Write-Host "  - Site URL: https://$ProjectRef.supabase.co"
Write-Host "  - Redirect: com.solodreams.dreamgate://auth/callback"
Write-Host "  - Apple native client ID: com.solodreams.dreamgate"
Write-Host "  - Phone signup enabled (SMS provider still required in dashboard)"
Write-Host ""

Push-Location $backendRoot
try {
    & $supabaseExe config push --project-ref $ProjectRef --yes
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Verifying public auth settings..."
. "$PSScriptRoot\_common.ps1"
$envValues = Read-BackendEnv
$anonKey = $envValues["SUPABASE_ANON_KEY"]
$settings = Invoke-RestMethod -Uri "https://$ProjectRef.supabase.co/auth/v1/settings" -Headers @{ apikey = $anonKey }
$settings.external | ConvertTo-Json -Compress | Write-Host
Write-Host ""
Write-Host "Still configure manually in the browser (secrets):"
Write-Host "  - Google OAuth client ID + secret"
Write-Host "  - Twilio (or other SMS provider) for phone login"
Write-Host "  - Apple Developer: Sign in with Apple on App ID"
Write-Host ""
& "$PSScriptRoot\open-auth-setup-browser.ps1" -ProjectRef $ProjectRef