$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "DreamGate.MatchServer"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK is required. Install .NET 8 SDK."
}

Push-Location $project
Write-Host "Starting Dream Gate authoritative match server..."
dotnet run
Pop-Location