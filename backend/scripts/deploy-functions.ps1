$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command supabase -ErrorAction SilentlyContinue)) {
    Write-Error "Supabase CLI is not installed."
}

if (-not (Test-Path "$root\.env")) {
    Write-Error "Missing backend/.env. Copy .env.example and fill in values."
}

Push-Location $root

Get-Content "$root\.env" | ForEach-Object {
    if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
        [System.Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2].Trim())
    }
}

if ($env:MATCH_SERVER_URL) {
    Write-Host "Setting MATCH_SERVER_URL secret..."
    supabase secrets set "MATCH_SERVER_URL=$($env:MATCH_SERVER_URL)"
}

Write-Host "Deploying edge functions..."
supabase functions deploy matchmaking --no-verify-jwt
supabase functions deploy apply-match-result --no-verify-jwt

Write-Host "Functions deployed."
Pop-Location