# Shushka Receipt — install / update / uninstall script
# Run as Administrator on the POS machine.
#
# What this does:
#   1. dotnet publish → C:\ShushkaReceipt\bin\ShushkaReceipt.exe
#   2. Creates a Task Scheduler task that auto-starts at logon (user session,
#      so the tray icon and DispatchForm work correctly).
#
# Why Task Scheduler instead of sc.exe:
#   Windows Services run in Session 0, which has no desktop.
#   The tray icon and popup forms require the user's desktop session.
#   Task Scheduler with "run only when logged on" starts the process
#   in the correct session and handles auto-restart on crash.
#
# Usage:
#   .\install-service.ps1              — first install
#   .\install-service.ps1 -Update      — republish and restart
#   .\install-service.ps1 -Uninstall   — stop and remove

param(
    [switch]$Update,
    [switch]$Uninstall
)

$TaskName   = "ShushkaReceipt"
$InstallDir = "C:\ShushkaReceipt\bin"
$ExePath    = "$InstallDir\ShushkaReceipt.exe"
$ProjectDir = "$PSScriptRoot\src\ShushkaReceipt"

# ── Helpers ────────────────────────────────────────────────────────────────

function Stop-Task {
    $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($task -and $task.State -eq 'Running') {
        Stop-ScheduledTask -TaskName $TaskName
        Start-Sleep -Seconds 2
    }
}

# ── Uninstall ──────────────────────────────────────────────────────────────

if ($Uninstall) {
    Stop-Task
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "Task removed."
    exit 0
}

# ── Publish ────────────────────────────────────────────────────────────────

Write-Host "Publishing ShushkaReceipt..."
dotnet publish "$ProjectDir\ShushkaReceipt.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $InstallDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed — aborting."
    exit 1
}

Write-Host "Published to $InstallDir"

# ── Update — restart existing task ────────────────────────────────────────

if ($Update) {
    Stop-Task
    Start-ScheduledTask -TaskName $TaskName
    Write-Host "ShushkaReceipt updated and restarted." -ForegroundColor Green
    exit 0
}

# ── First install — create Task Scheduler task ────────────────────────────

Stop-Task
Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue

# The task runs as the current logged-in user in their desktop session.
$CurrentUser = "$env:USERDOMAIN\$env:USERNAME"

$Action  = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $InstallDir
$Trigger = New-ScheduledTaskTrigger -AtLogOn -User $CurrentUser

$Settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `   # no time limit
    -RestartCount 10 `
    -RestartInterval (New-TimeSpan -Minutes 1) `    # restart 1 min after crash
    -MultipleInstances IgnoreNew

$Principal = New-ScheduledTaskPrincipal `
    -UserId $CurrentUser `
    -LogonType Interactive `                         # must be Interactive for UI
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action   $Action `
    -Trigger  $Trigger `
    -Settings $Settings `
    -Principal $Principal `
    -Description "Shushka digital receipt service — intercepts POS print jobs and delivers via WhatsApp." `
    | Out-Null

# Start immediately (don't wait for next logon)
Start-ScheduledTask -TaskName $TaskName

Write-Host ""
Write-Host "ShushkaReceipt installed and started." -ForegroundColor Green
Write-Host "Check the system tray — you should see a green circle icon."
Write-Host "Task will auto-start at every logon for: $CurrentUser"
