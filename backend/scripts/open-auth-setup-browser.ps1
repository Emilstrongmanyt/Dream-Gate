param(
    [string]$ProjectRef = "hekknzzbudmkwxtzxkdi"
)

$ErrorActionPreference = "Stop"

$dashboard = "https://supabase.com/dashboard/project/$ProjectRef/auth"
$urls = @(
    "$dashboard/url-configuration",
    "$dashboard/providers",
    "https://console.cloud.google.com/apis/credentials",
    "https://developer.apple.com/account/resources/identifiers/list",
    "https://console.twilio.com/"
)

Write-Host "Opening Dream Gate auth setup pages in your default browser..."
foreach ($url in $urls) {
    Write-Host "  $url"
    Start-Process $url
    Start-Sleep -Milliseconds 400
}

Write-Host ""
Write-Host "Supabase URL configuration:"
Write-Host "  Add redirect URL: com.solodreams.dreamgate://auth/callback"
Write-Host ""
Write-Host "Supabase providers:"
Write-Host "  Google -> client ID + secret from Google Cloud"
Write-Host "  Apple  -> client IDs: com.solodreams.dreamgate, allow users without email ON"
Write-Host "  Phone  -> enable + connect Twilio (or other SMS provider)"
Write-Host ""
Write-Host "Google Cloud OAuth client:"
Write-Host "  Authorized redirect URI: https://$ProjectRef.supabase.co/auth/v1/callback"
Write-Host ""
Write-Host "Apple Developer:"
Write-Host "  App ID com.solodreams.dreamgate -> Sign in with Apple enabled"