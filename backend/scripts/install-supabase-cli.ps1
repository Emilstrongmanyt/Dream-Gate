$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$toolsDir = Join-Path $root "tools"
$cliPath = Join-Path $toolsDir "supabase.exe"

if (Test-Path $cliPath) {
    Write-Host "Supabase CLI already installed: $cliPath"
    & $cliPath --version
    exit 0
}

New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null

$release = Invoke-RestMethod -Uri "https://api.github.com/repos/supabase/cli/releases/latest"
$asset = $release.assets | Where-Object { $_.name -like "supabase_*_windows_amd64.zip" } | Select-Object -First 1
if (-not $asset) {
    throw "Could not find Windows Supabase CLI asset in latest release."
}

$zipPath = Join-Path $toolsDir $asset.name
Write-Host "Downloading $($asset.name)..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -UseBasicParsing

if ((Get-Item $zipPath).Length -lt 1MB) {
    throw "Download failed or file is too small: $zipPath"
}

Expand-Archive -Path $zipPath -DestinationPath $toolsDir -Force
Remove-Item $zipPath -Force

if (-not (Test-Path $cliPath)) {
    throw "supabase.exe not found after extraction."
}

Write-Host "Installed Supabase CLI to $cliPath"
& $cliPath --version
Write-Host ""
Write-Host "Add to PATH for this session:"
Write-Host "  `$env:PATH = `"$toolsDir;`$env:PATH`""