# Shushka Receipt - POS machine setup script
# Run as Administrator on the cash computer.
#
# Before running:
#   1. Copy ShushkaReceipt.exe  ->  C:\Program Files\Shushka\ShushkaReceipt.exe
#   2. Copy appsettings.json    ->  C:\ProgramData\Shushka\appsettings.json
#   3. Run this script as Administrator
#
# Usage:
#   .\register-task.ps1            - register and start the task
#   .\register-task.ps1 -Uninstall - stop and remove the task

param([switch]$Uninstall)

$TaskName   = "ShushkaReceipt"
$ExePath    = "C:\Program Files\Shushka\ShushkaReceipt.exe"
$ConfigDir  = "C:\ProgramData\Shushka"
$ConfigPath = "$ConfigDir\appsettings.json"

# -- Uninstall ----------------------------------------------------------------

if ($Uninstall) {
    $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($task -and $task.State -eq 'Running') { Stop-ScheduledTask -TaskName $TaskName }
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Task removed. Files in 'C:\Program Files\Shushka\' and '$ConfigDir' were kept."
    exit 0
}

# -- Verify files exist -------------------------------------------------------

if (-not (Test-Path $ExePath)) {
    Write-Error "Exe not found: $ExePath`nCopy ShushkaReceipt.exe there first."
    exit 1
}

if (-not (Test-Path $ConfigPath)) {
    Write-Error "Config not found: $ConfigPath`nCopy appsettings.json there first."
    exit 1
}

# -- Grant the logged-on user write access to the config folder ---------------
# The app writes logs and saves phone numbers there at runtime.
# ProgramData is read-only for standard users by default.

$CurrentUser = (Get-CimInstance -ClassName Win32_ComputerSystem).UserName
if ([string]::IsNullOrEmpty($CurrentUser)) {
    # Fallback if CIM query returns empty (e.g. run from a service context)
    $CurrentUser = "$env:USERDOMAIN\$env:USERNAME"
}

Write-Host "Granting write access on '$ConfigDir' to: $CurrentUser"
icacls $ConfigDir /grant "${CurrentUser}:(OI)(CI)M" | Out-Null
if (-not $?) {
    Write-Warning "Could not set folder permissions. The app may not be able to write logs or save settings."
}

# -- Register Task Scheduler task ---------------------------------------------

$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($task -and $task.State -eq 'Running') { Stop-ScheduledTask -TaskName $TaskName }
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

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
    -RunLevel  Limited

Register-ScheduledTask `
    -TaskName   $TaskName `
    -Action     $Action `
    -Trigger    $Trigger `
    -Settings   $Settings `
    -Principal  $Principal `
    -Description "Shushka digital receipt service." `
    | Out-Null

if (-not $?) {
    Write-Error "Failed to register the task. Make sure PowerShell is running as Administrator."
    exit 1
}

Start-ScheduledTask -TaskName $TaskName

Write-Host ""
Write-Host "Shushka installed and running!" -ForegroundColor Green
Write-Host "Check the system tray - you should see a green circle."
Write-Host "Task will auto-start at every logon for: $CurrentUser"
Write-Host ""
Write-Host "Config file: $ConfigPath"
Write-Host "Edit it to set StorePhone, OwnerPhone, ThermalPrinterName."
