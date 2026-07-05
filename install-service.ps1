# Shushka Receipt — install / update / uninstall script
# Run as Administrator on the POS machine.
#
# Layout after install:
#   C:\Program Files\Shushka\ShushkaReceipt.exe   — self-contained exe (no .NET required)
#   C:\ProgramData\Shushka\appsettings.json        — live config (writable at runtime)
#   C:\ProgramData\Shushka\receipt-jobs.log        — log file
#
# Why Task Scheduler instead of sc.exe:
#   Windows Services run in Session 0, which has no desktop.
#   Tray icons and popup forms require the user's desktop session.
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

$TaskName      = "ShushkaReceipt"
$InstallDir    = "C:\Program Files\Shushka"
$ConfigDir     = "C:\ProgramData\Shushka"
$ExePath       = "$InstallDir\ShushkaReceipt.exe"
$ConfigDest    = "$ConfigDir\appsettings.json"
$ProjectDir    = "$PSScriptRoot\src\ShushkaReceipt"
$ConfigSource  = "$ProjectDir\appsettings.json"

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
    Write-Host "Task removed. Files kept in $InstallDir and $ConfigDir."
    exit 0
}

# ── Publish ────────────────────────────────────────────────────────────────

Write-Host "Publishing ShushkaReceipt (self-contained, single file)..."
dotnet publish "$ProjectDir\ShushkaReceipt.csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output "$InstallDir"

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed — aborting."
    exit 1
}

Write-Host "Published to $InstallDir"

# ── Copy config to ProgramData (first install only — never overwrite) ──────

New-Item -ItemType Directory -Force -Path $ConfigDir | Out-Null

if (-not (Test-Path $ConfigDest)) {
    Copy-Item $ConfigSource $ConfigDest
    Write-Host "Config copied to $ConfigDest"
    Write-Host "  -> Edit $ConfigDest to set StorePhone, OwnerPhone, ThermalPrinterName, etc."
} else {
    Write-Host "Config already exists at $ConfigDest — not overwritten."
}

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

$CurrentUser = "$env:USERDOMAIN\$env:USERNAME"

$Action  = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $InstallDir
$Trigger = New-ScheduledTaskTrigger -AtLogOn -User $CurrentUser

$Settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit (New-TimeSpan -Hours 0) `
    -RestartCount 10 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -MultipleInstances IgnoreNew

$Principal = New-ScheduledTaskPrincipal `
    -UserId $CurrentUser `
    -LogonType Interactive `
    -RunLevel Highest

Register-ScheduledTask `
    -TaskName  $TaskName `
    -Action    $Action `
    -Trigger   $Trigger `
    -Settings  $Settings `
    -Principal $Principal `
    -Description "Shushka digital receipt service — intercepts POS print jobs and delivers via WhatsApp." `
    | Out-Null

Start-ScheduledTask -TaskName $TaskName

Write-Host ""
Write-Host "ShushkaReceipt installed and started." -ForegroundColor Green
Write-Host "Exe:    $ExePath"
Write-Host "Config: $ConfigDest"
Write-Host "Check the system tray — you should see a green circle icon."
Write-Host "Task auto-starts at every logon for: $CurrentUser"
