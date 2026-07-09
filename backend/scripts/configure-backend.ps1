param(
    [string]$SupabaseUrl,
    [string]$AnonKey,
    [string]$ServiceRoleKey,
    [string]$ProjectRef,
    [string]$MatchServerUrl = "http://localhost:8787",
    [switch]$EnableCloud,
    [switch]$FromEnv,
    [switch]$LocalOnly
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $root ".env"
$assetPath = Join-Path $root "..\Assets\Resources\BackendSettings.asset"

function Read-DotEnv([string]$path) {
    $values = @{}
    if (-not (Test-Path $path)) {
        return $values
    }

    Get-Content $path | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            $values[$matches[1].Trim()] = $matches[2].Trim()
        }
    }
    return $values
}

function Write-DotEnv([hashtable]$values, [string]$path) {
    @(
        "# Dream Gate backend configuration"
        "SUPABASE_URL=$($values.SUPABASE_URL)"
        "SUPABASE_ANON_KEY=$($values.SUPABASE_ANON_KEY)"
        "SUPABASE_SERVICE_ROLE_KEY=$($values.SUPABASE_SERVICE_ROLE_KEY)"
        "MATCH_SERVER_URL=$($values.MATCH_SERVER_URL)"
        "PROJECT_REF=$($values.PROJECT_REF)"
    ) -join "`n" | Out-File -FilePath $path -Encoding ascii
}

function Escape-UnityYamlString([string]$value) {
    if ([string]::IsNullOrEmpty($value)) {
        return ""
    }

    if ($value -match '[:#]' -or $value.Contains('"')) {
        $escaped = $value.Replace('"', '\"')
        return """$escaped"""
    }

    return $value
}

function Write-UnityTextFile([string]$path, [string]$content) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, $content, $utf8NoBom)
}

function Update-BackendSettingsAsset(
    [bool]$useCloud,
    [string]$supabaseUrl,
    [string]$anonKey,
    [string]$matchServerUrl
) {
    if (-not (Test-Path $assetPath)) {
        throw "BackendSettings.asset not found at $assetPath"
    }

    $content = Get-Content $assetPath -Raw
    if ($content.StartsWith([char]0xFEFF)) {
        $content = $content.Substring(1)
    }

    $content = $content -replace 'useCloudBackend: \d', "useCloudBackend: $(if ($useCloud) { 1 } else { 0 })"
    $content = $content -replace 'supabaseUrl:.*', "supabaseUrl: $(Escape-UnityYamlString $supabaseUrl)"
    $content = $content -replace 'supabaseAnonKey:.*', "supabaseAnonKey: $(Escape-UnityYamlString $anonKey)"
    $content = $content -replace 'matchServerWebSocketUrl:.*', "matchServerWebSocketUrl: $(Escape-UnityYamlString $matchServerUrl)"
    Write-UnityTextFile $assetPath $content
}

$envValues = Read-DotEnv $envFile
if ($FromEnv -or (-not $SupabaseUrl -and $envValues.ContainsKey("SUPABASE_URL"))) {
    if (-not $PSBoundParameters.ContainsKey("SupabaseUrl") -and $envValues.ContainsKey("SUPABASE_URL")) {
        $SupabaseUrl = $envValues["SUPABASE_URL"]
    }
    if (-not $PSBoundParameters.ContainsKey("AnonKey") -and $envValues.ContainsKey("SUPABASE_ANON_KEY")) {
        $AnonKey = $envValues["SUPABASE_ANON_KEY"]
    }
    if (-not $PSBoundParameters.ContainsKey("ServiceRoleKey") -and $envValues.ContainsKey("SUPABASE_SERVICE_ROLE_KEY")) {
        $ServiceRoleKey = $envValues["SUPABASE_SERVICE_ROLE_KEY"]
    }
    if (-not $PSBoundParameters.ContainsKey("ProjectRef") -and $envValues.ContainsKey("PROJECT_REF")) {
        $ProjectRef = $envValues["PROJECT_REF"]
    }
    if (-not $PSBoundParameters.ContainsKey("MatchServerUrl") -and $envValues.ContainsKey("MATCH_SERVER_URL")) {
        $MatchServerUrl = $envValues["MATCH_SERVER_URL"]
    }
}

if ($LocalOnly) {
    $EnableCloud = $false
    if (-not $SupabaseUrl) { $SupabaseUrl = "" }
    if (-not $AnonKey) { $AnonKey = "" }
    if (-not $ServiceRoleKey) { $ServiceRoleKey = "" }
    if (-not $ProjectRef) { $ProjectRef = "" }
}
elseif (-not $SupabaseUrl -or -not $AnonKey) {
    Write-Host ""
    Write-Host "Dream Gate backend setup needs your Supabase project values."
    Write-Host "Get them from: https://supabase.com/dashboard/project/_/settings/api"
    Write-Host "Or run with -LocalOnly to configure only the match server URL."
    Write-Host ""
    $SupabaseUrl = Read-Host "Supabase URL (https://xxxx.supabase.co)"
    $AnonKey = Read-Host "Supabase anon public key"
    $ServiceRoleKey = Read-Host "Supabase service_role key (for deploy scripts)"
    $ProjectRef = Read-Host "Project ref (from dashboard URL)"
    if (-not $MatchServerUrl) {
        $MatchServerUrl = Read-Host "Match server URL"
    }
    $EnableCloud = $true
}

if (-not $LocalOnly -and -not $EnableCloud.IsPresent) {
    $EnableCloud = $true
}

$saveValues = @{
    SUPABASE_URL = $SupabaseUrl
    SUPABASE_ANON_KEY = $AnonKey
    SUPABASE_SERVICE_ROLE_KEY = $ServiceRoleKey
    MATCH_SERVER_URL = $MatchServerUrl
    PROJECT_REF = $ProjectRef
}
Write-DotEnv $saveValues $envFile
Update-BackendSettingsAsset $EnableCloud $SupabaseUrl $AnonKey $MatchServerUrl

Write-Host ""
Write-Host "Saved backend/.env"
Write-Host "Updated Assets/Resources/BackendSettings.asset"
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Run SQL migration in Supabase Dashboard -> SQL Editor:"
Write-Host "     backend/supabase/migrations/001_ranked_pvp.sql"
Write-Host "  2. Configure mobile auth (disable confirm email for TestFlight): .\scripts\configure-supabase-auth.ps1"
Write-Host "  3. Deploy edge functions (Supabase Dashboard -> Edge Functions, or install CLI later)"
Write-Host "  4. Start match server: .\scripts\start-match-server.ps1"
Write-Host "  5. Test health: .\scripts\test-backend.ps1"