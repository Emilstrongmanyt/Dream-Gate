param(
    [string]$MatchServerUrl = "http://localhost:8787",
    [string]$SupabaseUrl,
    [string]$AnonKey
)

. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Continue"
$envValues = Read-BackendEnv
if ($envValues.ContainsKey("MATCH_SERVER_URL") -and -not $PSBoundParameters.ContainsKey("MatchServerUrl")) {
    $MatchServerUrl = $envValues["MATCH_SERVER_URL"]
}
if ($envValues.ContainsKey("SUPABASE_URL") -and -not $PSBoundParameters.ContainsKey("SupabaseUrl")) {
    $SupabaseUrl = $envValues["SUPABASE_URL"]
}
if ($envValues.ContainsKey("SUPABASE_ANON_KEY") -and -not $PSBoundParameters.ContainsKey("AnonKey")) {
    $AnonKey = $envValues["SUPABASE_ANON_KEY"]
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
    $envValues = Read-BackendEnv
    $testKey = $envValues["SUPABASE_SERVICE_ROLE_KEY"]
    if ([string]::IsNullOrWhiteSpace($testKey)) {
        $testKey = $AnonKey
    }

    try {
        $headers = @{ apikey = $testKey; Authorization = "Bearer $testKey" }
        $rest = Invoke-WebRequest -Uri "$SupabaseUrl/rest/v1/" -Headers $headers -TimeoutSec 10 -UseBasicParsing
        Write-Host "[OK] Supabase REST reachable (status $($rest.StatusCode))"
    }
    catch {
        Write-Host "[FAIL] Supabase REST - $($_.Exception.Message)"
    }

    try {
        $auth = Invoke-RestMethod -Uri "$SupabaseUrl/auth/v1/health" -Headers @{ apikey = $AnonKey } -TimeoutSec 10
        Write-Host "[OK] Supabase Auth: $($auth.name) $($auth.version)"
    }
    catch {
        Write-Host "[FAIL] Supabase Auth - $($_.Exception.Message)"
    }
}
else {
    Write-Host "[SKIP] Supabase not configured in .env yet"
}

Write-Host ""
Write-Host "Done."