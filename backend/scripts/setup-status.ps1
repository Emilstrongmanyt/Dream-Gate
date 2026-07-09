. "$PSScriptRoot\_common.ps1"
Import-BackendCli

$root = Get-BackendRoot
$envValues = Read-BackendEnv
$assetPath = Join-Path $root "..\Assets\Resources\BackendSettings.asset"

Write-Host "Dream Gate Backend Setup Status"
Write-Host "================================"
Write-Host ""

function Show-Check([string]$label, [bool]$ok, [string]$detail) {
    $mark = if ($ok) { "[OK]" } else { "[--]" }
    Write-Host "$mark $label"
    if ($detail) {
        Write-Host "    $detail"
    }
}

$dotnetOk = [bool](Get-Command dotnet -ErrorAction SilentlyContinue)
Show-Check ".NET SDK" $dotnetOk $(if ($dotnetOk) { (dotnet --version) } else { "Install .NET SDK" })

$cliPath = Join-Path $root "tools\supabase.exe"
$cliOk = Test-Path $cliPath
Show-Check "Supabase CLI" $cliOk $(if ($cliOk) { & $cliPath --version } else { "Run .\scripts\install-supabase-cli.ps1" })

$matchOk = $false
$matchDetail = "Not running"
try {
    $health = Invoke-RestMethod -Uri "http://localhost:8787/health" -TimeoutSec 3
    $matchOk = $true
    $matchDetail = "http://localhost:8787 -> $($health | ConvertTo-Json -Compress)"
}
catch {
    $matchDetail = "Run .\scripts\ensure-match-server.ps1"
}
Show-Check "Match server" $matchOk $matchDetail

$lanIp = Get-LanIpAddress
if ($lanIp) {
    Write-Host "    Device URL (same network): http://${lanIp}:8787"
}

$envOk = $envValues.ContainsKey("SUPABASE_URL") -and -not [string]::IsNullOrWhiteSpace($envValues["SUPABASE_URL"])
Show-Check "backend/.env Supabase URL" $envOk $(if ($envOk) { $envValues["SUPABASE_URL"] } else { "Run .\scripts\configure-backend.ps1" })

$anonOk = $envValues.ContainsKey("SUPABASE_ANON_KEY") -and -not [string]::IsNullOrWhiteSpace($envValues["SUPABASE_ANON_KEY"])
Show-Check "backend/.env anon key" $anonOk $(if (-not $anonOk) { "Missing" })

$refOk = $envValues.ContainsKey("PROJECT_REF") -and -not [string]::IsNullOrWhiteSpace($envValues["PROJECT_REF"])
Show-Check "backend/.env project ref" $refOk $(if ($refOk) { $envValues["PROJECT_REF"] } else { "Missing" })

$assetCloud = $false
$assetServer = ""
if (Test-Path $assetPath) {
    $asset = Get-Content $assetPath -Raw
    $assetCloud = $asset -match 'useCloudBackend: 1'
    if ($asset -match 'matchServerWebSocketUrl:\s*(.+)$') {
        $assetServer = $matches[1].Trim()
    }
}
Show-Check "Unity cloud backend enabled" $assetCloud $(if (-not $assetCloud) { "Local auth + matchmaking; rated can still use match server" })
Show-Check "Unity match server URL" (-not [string]::IsNullOrWhiteSpace($assetServer)) $assetServer
if (-not [string]::IsNullOrWhiteSpace($assetServer)) {
    Write-Host "    Rated matches use authoritative server even without cloud backend"
}

$linked = Test-Path (Join-Path $root ".supabase")
Show-Check "Supabase project linked" $linked $(if (-not $linked) { "Run .\scripts\link-project.ps1 after login" })

$loggedIn = $false
if ($cliOk) {
    $null = & $cliPath projects list 2>&1
    $loggedIn = $LASTEXITCODE -eq 0
}
Show-Check "Supabase CLI logged in" $loggedIn $(if (-not $loggedIn) { "Run: supabase login" })

Write-Host ""
Write-Host "Next actions:"
if (-not $envOk) {
    Write-Host "  1. Create Supabase project and run .\scripts\configure-backend.ps1"
}
if (-not $linked -and $refOk) {
    Write-Host "  2. supabase login && .\scripts\link-project.ps1"
}
if ($envOk -and $linked) {
    Write-Host "  3. .\scripts\setup-supabase.ps1"
    Write-Host "  4. .\scripts\deploy-functions.ps1"
}
if (-not $matchOk) {
    Write-Host "  * .\scripts\start-match-server.ps1 -Background"
}
Write-Host ""