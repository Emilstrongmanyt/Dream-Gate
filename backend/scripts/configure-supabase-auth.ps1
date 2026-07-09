param(
    [string]$ProjectRef
)

. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Stop"

$envValues = Read-BackendEnv
if (-not $ProjectRef -and $envValues.ContainsKey("PROJECT_REF")) {
    $ProjectRef = $envValues["PROJECT_REF"]
}

if (-not $ProjectRef) {
    $ProjectRef = "hekknzzbudmkwxtzxkdi"
}

$dashboardBase = "https://supabase.com/dashboard/project/$ProjectRef/auth"

Write-Host ""
Write-Host "Dream Gate mobile auth uses Supabase email/password."
Write-Host "If registration emails contain a broken localhost link, update Supabase Auth settings:"
Write-Host ""
Write-Host "Recommended for TestFlight / mobile beta:"
Write-Host "  1. Open: $dashboardBase/providers"
Write-Host "  2. Email provider -> turn OFF 'Confirm email'"
Write-Host "  3. Save"
Write-Host ""
Write-Host "This lets players register and log in immediately in the app."
Write-Host ""
Write-Host "If you keep email confirmation enabled:"
Write-Host "  1. Open: $dashboardBase/url-configuration"
Write-Host "  2. Set Site URL to: https://$ProjectRef.supabase.co"
Write-Host "  3. Add Redirect URLs as needed (not used by the mobile app today)"
Write-Host "  4. Players must confirm email in a browser, then log in inside the app"
Write-Host ""
Write-Host "The game now shows a success message when signup succeeds but email confirmation is pending."
Write-Host ""