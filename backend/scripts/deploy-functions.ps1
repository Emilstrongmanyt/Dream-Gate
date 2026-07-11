. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Stop"
Import-BackendCli
$root = Get-BackendRoot

if (-not (Get-Command supabase -ErrorAction SilentlyContinue)) {
    Write-Error "Supabase CLI is not installed. Run .\scripts\install-supabase-cli.ps1"
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

if ($env:UGS_BRIDGE_SECRET) {
    Write-Host "Setting UGS_BRIDGE_SECRET secret..."
    supabase secrets set "UGS_BRIDGE_SECRET=$($env:UGS_BRIDGE_SECRET)"
}

if ($env:UNITY_PROJECT_ID) {
    Write-Host "Setting UNITY_PROJECT_ID secret..."
    supabase secrets set "UNITY_PROJECT_ID=$($env:UNITY_PROJECT_ID)"
}

Write-Host "Deploying edge functions..."
supabase functions deploy matchmaking --no-verify-jwt
supabase functions deploy apply-match-result --no-verify-jwt
supabase functions deploy ugs-session --no-verify-jwt

Write-Host "Functions deployed."
Pop-Location