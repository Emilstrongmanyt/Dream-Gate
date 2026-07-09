. "$PSScriptRoot\_common.ps1"
$root = Get-BackendRoot
$envValues = Read-BackendEnv

Write-Host "Manual Edge Function deployment (Supabase Dashboard)"
Write-Host "===================================================="
Write-Host ""
Write-Host "Use this if CLI deploy is unavailable. In Dashboard:"
Write-Host "  Edge Functions -> Create function"
Write-Host ""

$functions = @(
    @{ Name = "matchmaking"; Path = "supabase\functions\matchmaking\index.ts" },
    @{ Name = "apply-match-result"; Path = "supabase\functions\apply-match-result\index.ts" }
)

foreach ($fn in $functions) {
    $source = Join-Path $root $fn.Path
    if (-not (Test-Path $source)) {
        Write-Warning "Missing $($fn.Path)"
        continue
    }

    Write-Host "Function: $($fn.Name)"
    Write-Host "  Source: $source"
    Write-Host "  Deploy URL: $($envValues['SUPABASE_URL'])/functions/v1/$($fn.Name)"
    Write-Host ""
}

if ($envValues["MATCH_SERVER_URL"]) {
    Write-Host "Set project secret in Dashboard -> Edge Functions -> Secrets:"
    Write-Host "  MATCH_SERVER_URL=$($envValues['MATCH_SERVER_URL'])"
    Write-Host ""
}

Write-Host "Also set (auto-injected on hosted Supabase, verify in function settings):"
Write-Host "  SUPABASE_URL, SUPABASE_ANON_KEY, SUPABASE_SERVICE_ROLE_KEY"
Write-Host ""
Write-Host "Copying matchmaking source to clipboard..."
Set-Clipboard -Value (Get-Content (Join-Path $root "supabase\functions\matchmaking\index.ts") -Raw)
Write-Host "Paste into the matchmaking function editor, deploy, then re-run for apply-match-result."