. "$PSScriptRoot\_common.ps1"
$ErrorActionPreference = "Continue"

$envValues = Import-BackendEnv
if (-not $envValues["SUPABASE_URL"] -or -not $envValues["SUPABASE_SERVICE_ROLE_KEY"]) {
    throw "Missing SUPABASE_URL or SUPABASE_SERVICE_ROLE_KEY in backend/.env"
}

$base = $envValues["SUPABASE_URL"].TrimEnd('/')
$key = $envValues["SUPABASE_SERVICE_ROLE_KEY"]
$headers = @{ apikey = $key; Authorization = "Bearer $key" }

$tables = @(
    "player_profiles",
    "match_queue",
    "matches",
    "match_slots",
    "match_history",
    "player_achievements"
)

Write-Host "Verifying ranked PvP migration..."
Write-Host ""

$ok = 0
foreach ($table in $tables) {
    try {
        $null = Invoke-WebRequest -Uri "$base/rest/v1/$table`?select=*&limit=0" -Headers $headers -UseBasicParsing -TimeoutSec 15
        Write-Host "[OK] $table"
        $ok++
    }
    catch {
        Write-Host "[FAIL] $table"
    }
}

Write-Host ""
if ($ok -eq $tables.Count) {
    Write-Host "Migration verified ($ok/$($tables.Count) tables)."
    exit 0
}

Write-Host "Migration incomplete ($ok/$($tables.Count) tables)."
Write-Host "Run SQL from: backend/supabase/migrations/001_ranked_pvp.sql"
exit 1