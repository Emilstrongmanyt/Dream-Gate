param(
    [string]$MatchServerUrl = "http://localhost:8787",
    [string]$SupabaseUrl,
    [string]$AnonKey
)

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root ".env"

if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim()
            switch ($name) {
                "MATCH_SERVER_URL" { if (-not $MatchServerUrl) { $MatchServerUrl = $value } }
                "SUPABASE_URL" { if (-not $SupabaseUrl) { $SupabaseUrl = $value } }
                "SUPABASE_ANON_KEY" { if (-not $AnonKey) { $AnonKey = $value } }
            }
        }
    }
}

Write-Host "Testing Dream Gate backend..."
Write-Host ""

try {
    $health = Invoke-RestMethod -Uri "$MatchServerUrl/health" -TimeoutSec 5
    Write-Host "[OK] Match server: $($health | ConvertTo-Json -Compress)"
}
catch {
    Write-Host "[FAIL] Match server at $MatchServerUrl - $($_.Exception.Message)"
    Write-Host "       Start it with: .\scripts\start-match-server.ps1"
}

if ($SupabaseUrl -and $AnonKey) {
    try {
        $headers = @{ apikey = $AnonKey; Authorization = "Bearer $AnonKey" }
        $rest = Invoke-WebRequest -Uri "$SupabaseUrl/rest/v1/" -Headers $headers -TimeoutSec 10
        Write-Host "[OK] Supabase REST reachable (status $($rest.StatusCode))"
    }
    catch {
        Write-Host "[FAIL] Supabase REST - $($_.Exception.Message)"
    }
}
else {
    Write-Host "[SKIP] Supabase not configured in .env yet"
}

Write-Host ""
Write-Host "Done."