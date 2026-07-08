param(
    [switch]$Local
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command supabase -ErrorAction SilentlyContinue)) {
    Write-Error "Supabase CLI is not installed. Install from https://supabase.com/docs/guides/cli"
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