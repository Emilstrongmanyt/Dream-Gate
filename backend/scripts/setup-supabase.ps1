param(
    [switch]$Local
)

. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Stop"
Import-BackendCli
$root = Get-BackendRoot

if (-not (Get-Command supabase -ErrorAction SilentlyContinue)) {
    Write-Error "Supabase CLI is not installed. Run .\scripts\install-supabase-cli.ps1"
}

Push-Location $root

if ($Local) {
    Write-Host "Starting local Supabase..."
    supabase start
    supabase db reset
    Write-Host "Local Supabase ready. Studio: http://localhost:54323"
    Pop-Location
    exit 0
}

if (-not (Test-Path "$root\.env")) {
    Copy-Item "$root\.env.example" "$root\.env"
    Write-Host "Created backend/.env from .env.example. Fill in your Supabase keys, then re-run."
    Pop-Location
    exit 1
}

Write-Host "Applying migration to linked project..."
supabase db push

Write-Host "Done. Next: .\scripts\deploy-functions.ps1"
Pop-Location