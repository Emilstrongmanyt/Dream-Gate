$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sqlPath = Join-Path $root "supabase\migrations\001_ranked_pvp.sql"

if (-not (Test-Path $sqlPath)) {
    Write-Error "Migration file not found: $sqlPath"
}

$sql = Get-Content $sqlPath -Raw
Set-Clipboard -Value $sql

Write-Host "Copied ranked PvP migration SQL to clipboard."
Write-Host ""
Write-Host "Paste it in Supabase Dashboard:"
Write-Host "  1. Open https://supabase.com/dashboard"
Write-Host "  2. Select your Dream Gate project"
Write-Host "  3. SQL Editor -> New query"
Write-Host "  4. Paste (Ctrl+V) and click Run"
Write-Host ""
Write-Host "Migration file: $sqlPath"