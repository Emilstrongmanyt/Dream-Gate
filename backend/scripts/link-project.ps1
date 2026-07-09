. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Stop"
Import-BackendCli

$root = Get-BackendRoot
$envValues = Import-BackendEnv

if (-not $envValues["PROJECT_REF"]) {
    throw "PROJECT_REF is missing in backend/.env. Run .\scripts\configure-backend.ps1 first."
}

Push-Location $root
Write-Host "Linking Supabase project $($envValues['PROJECT_REF'])..."
supabase link --project-ref $envValues["PROJECT_REF"]
Pop-Location

Write-Host "Linked. Next: .\scripts\setup-supabase.ps1"