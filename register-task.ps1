# Shushka Receipt — POS machine setup script
# Run as Administrator on the cash computer.
#
# Before running:
#   1. Copy ShushkaReceipt.exe  →  C:\Program Files\Shushka\ShushkaReceipt.exe
#   2. Copy appsettings.json    →  C:\ProgramData\Shushka\appsettings.json
#      (edit the config file first: set StorePhone, OwnerPhone, ThermalPrinterName)
#   3. Run this script as Administrator
#
# Usage:
#   .\register-task.ps1            — register and start the task
#   .\register-task.ps1 -Uninstall — stop and remove the task

param([switch]$Uninstall)

$TaskName   = "ShushkaReceipt"
$ExePath    = "C:\Program Files\Shushka\ShushkaReceipt.exe"
$ConfigDir  = "C:\ProgramData\Shushka"
$ConfigPath = "$ConfigDir\appsettings.json"

# ── Uninstall ──────────────────────────────────────────────────────────────

if ($Uninstall) {
    $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($task -and $task.State -eq 'Running') { Stop-ScheduledTask -TaskName $TaskName }
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Task removed. Files in 'C:\Program Files\Shushka\' and '$ConfigDir' were kept."
    exit 0
}

# ── Verify files exist ─────────────────────────────────────────────────────

if (-not (Test-Path $ExePath)) {
    Write-Error "Exe not found: $ExePath`nCopy ShushkaReceipt.exe there first."
    exit 1
}

if (-not (Test-Path $ConfigPath)) {
    Write-Error "Config not found: $ConfigPath`nCopy appsettings.json there first."
    exit 1
}

# ── Register Task Scheduler task ───────────────────────────────────────────

$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($task -and $task.State -eq 'Running') { Stop-ScheduledTask -TaskName $TaskName }
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

$CurrentUser = "$env:USERDOMAIN\$env:USERNAME"

$Action  = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory "C:\Program Files\Shushka"
$Trigger = New-ScheduledTaskTrigger -AtLogOn -User $CurrentUser

$Settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
    -RestartCount 10 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew

$Principal = New-ScheduledTaskPrincipal `
    -UserId    $CurrentUser `
    -LogonType Interactive `
    -RunLevel  Highest

Register-ScheduledTask `
    -TaskName   $TaskName `
    -Action     $Action `
    -Trigger    $Trigger `
    -Settings   $Settings `
    -Principal  $Principal `
    -Description "Shushka digital receipt service." `
    | Out-Null

Start-ScheduledTask -TaskName $TaskName

Write-Host ""
Write-Host "Shushka installed and running!" -ForegroundColor Green
Write-Host "Check the system tray — you should see a green circle."
Write-Host "Task will auto-start at every logon for: $CurrentUser"
Write-Host ""
Write-Host "Config file: $ConfigPath"
Write-Host "Edit it to set StorePhone, OwnerPhone, ThermalPrinterName."
