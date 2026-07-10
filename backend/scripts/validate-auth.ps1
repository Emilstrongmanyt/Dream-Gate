param(
    [string]$Email,
    [string]$Password = "testpass123"
)

$ErrorActionPreference = "Stop"

$settingsPath = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Assets\Resources\BackendSettings.asset"
if (-not (Test-Path $settingsPath)) {
    throw "BackendSettings.asset not found at $settingsPath"
}

$asset = Get-Content $settingsPath -Raw
if ($asset -notmatch 'supabaseUrl:\s*(\S+)') { throw "Could not read supabaseUrl from BackendSettings.asset" }
$supabaseUrl = $Matches[1]
if ($asset -notmatch 'supabaseAnonKey:\s*(\S+)') { throw "Could not read supabaseAnonKey from BackendSettings.asset" }
$supabaseAnonKey = $Matches[1]

$headers = @{
    apikey = $supabaseAnonKey
    Authorization = "Bearer $supabaseAnonKey"
    "Content-Type" = "application/json"
    Accept = "application/json"
}

if (-not $Email) {
    $Email = "dg-validate-$([Guid]::NewGuid().ToString('N').Substring(0, 8))@example.com"
}

Write-Host "Validating Supabase auth against $supabaseUrl"
Write-Host "Email: $Email"

$signupBody = @{
    email = $Email
    password = $Password
    data = @{ display_name = "ValidateUser" }
} | ConvertTo-Json -Compress

$signup = Invoke-RestMethod -Uri "$supabaseUrl/auth/v1/signup" -Method POST -Headers $headers -Body $signupBody
if (-not $signup.access_token) {
    throw "Signup response did not include access_token"
}

Write-Host "Signup OK. access_token length: $($signup.access_token.Length)"

$loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json -Compress
$login = Invoke-RestMethod -Uri "$supabaseUrl/auth/v1/token?grant_type=password" -Method POST -Headers $headers -Body $loginBody
if (-not $login.access_token) {
    throw "Login response did not include access_token"
}

Write-Host "Login OK. access_token length: $($login.access_token.Length)"
Write-Host "Auth validation passed."