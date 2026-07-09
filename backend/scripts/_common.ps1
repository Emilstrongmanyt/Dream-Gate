function Get-BackendRoot {
    return Split-Path -Parent $PSScriptRoot
}

function Import-BackendCli {
    $toolsDir = Join-Path (Get-BackendRoot) "tools"
    if (Test-Path (Join-Path $toolsDir "supabase.exe")) {
        $env:PATH = "$toolsDir;$env:PATH"
    }
}

function Read-BackendEnv {
    $envFile = Join-Path (Get-BackendRoot) ".env"
    $values = @{}
    if (-not (Test-Path $envFile)) {
        return $values
    }

    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.*)$') {
            $values[$matches[1].Trim()] = $matches[2].Trim()
        }
    }
    return $values
}

function Import-BackendEnv {
    $values = Read-BackendEnv
    foreach ($key in $values.Keys) {
        [System.Environment]::SetEnvironmentVariable($key, $values[$key])
    }
    return $values
}

function Get-LanIpAddress {
    $ip = Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } |
        Select-Object -First 1 -ExpandProperty IPAddress
    return $ip
}

function Get-PublicIpAddress {
    try {
        return (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 10).ToString().Trim()
    }
    catch {
        return Get-LanIpAddress
    }
}