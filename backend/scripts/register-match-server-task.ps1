param(
    [switch]$Unregister
)

$ErrorActionPreference = "Stop"
$taskName = "DreamGateMatchServer"
$scriptPath = Join-Path $PSScriptRoot "ensure-match-server.ps1"

if ($Unregister) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Removed scheduled task: $taskName"
    exit 0
}

$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

try {
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
    Write-Host "[OK] Registered logon task: $taskName"
    Write-Host "Match server will auto-start when you sign in to Windows."
}
catch {
    Write-Host "[SKIP] Could not register scheduled task (admin rights may be required)."
    Write-Host "       Start manually with: .\scripts\ensure-match-server.ps1"
}
Write-Host "Remove task with: .\scripts\register-match-server-task.ps1 -Unregister"