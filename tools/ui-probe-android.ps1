param(
    [string]$Exe = "app/src/PCManager/bin/Debug/net8.0-windows/FPM.exe"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $root $Exe
if (-not (Test-Path $exePath)) { "FPM.exe not found: $exePath"; exit 1 }

if (Get-Process FPM -ErrorAction SilentlyContinue) {
    "FPM is already running; stop the recorded instance before probing."
    exit 1
}

$settingsPath = Join-Path $env:LOCALAPPDATA "FPM\settings.json"
$backupPath = Join-Path ([IO.Path]::GetTempPath()) "fpm-settings-backup.json"
$hadSettings = Test-Path $settingsPath
if ($hadSettings) { Copy-Item $settingsPath $backupPath -Force }

$proc = $null
try {
    if ($hadSettings) {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        $settings.ShowFloatingToolbar = $false
        $settings.CloseToTray = $false
        $settings | ConvertTo-Json -Depth 12 | Set-Content $settingsPath -Encoding UTF8
    }

    $proc = Start-Process -FilePath $exePath -ArgumentList "--no-splash" -PassThru
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline -and $proc.MainWindowHandle -eq 0 -and -not $proc.HasExited) {
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
    }

    if ($proc.HasExited) { "FPM exited during startup: $($proc.ExitCode)"; exit 2 }
    if ($proc.MainWindowHandle -eq 0) { "FPM window did not appear"; exit 2 }

    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))

    if (-not $window) { "FPM automation window not found"; exit 2 }

    function Find-Named($rootElement, [string]$name) {
        return $rootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty, $name)))
    }

    function Invoke-Named($rootElement, [string]$name) {
        $matches = $rootElement.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty, $name)))

        foreach ($element in $matches) {
            try {
                $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
                return $true
            } catch {
                try {
                    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
                    return $true
                } catch { }
            }
        }

        return $false
    }

    if (-not (Invoke-Named $window "Debloat")) { "Debloat navigation not found"; exit 3 }
    Start-Sleep -Seconds 2

    if (-not (Invoke-Named $window "Android tab")) { "Android tab not found"; exit 4 }
    Start-Sleep -Seconds 4
    "android tab invoked"

    $texts = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Text)))

    $status = $null
    $path = $null
    $onboarding = $null
    $license = $null
    foreach ($item in $texts) {
        $name = $item.Current.Name
        if ($name -like "ADB status:*") { $status = $name }
        if ($name -like "ADB:*") { $path = $name }
        if ($name -like "*Android SDK License Agreement*" -and $name -notlike "I accept*") { $license = $name }
        if ($name -like "No Android device is connected*" -or
            $name -like "The phone is connected but not authorized*" -or
            $name -like "The device is offline*" -or
            $name -like "Windows denied USB access*") { $onboarding = $name }
    }

    if (-not $status) { "ADB status text not found"; exit 5 }
    "status: $status"
    if (-not $path) { "ADB path text not found"; exit 5 }
    "path: $path"

    if ($status -notlike "ADB status: NoDevice*" -and $status -notlike "ADB status: Ready*") {
        "unexpected ADB state for this probe: $status"
        exit 6
    }

    if (-not $onboarding) { "onboarding explanation not found"; exit 6 }
    "onboarding: $onboarding"
    if (-not $license) { "Android SDK license notice not found"; exit 6 }
    "license notice present"

    foreach ($name in @(
        "Android device",
        "Official platform-tools download",
        "Accept Android SDK license",
        "ADB path override",
        "Refresh ADB",
        "Download platform-tools",
        "Android devices",
        "Connected devices",
        "I understand Android package risk",
        "Android user profile",
        "Refresh Android packages",
        "Search Android packages",
        "Android package list filter",
        "Android removal tier filter",
        "Android package state filter",
        "Show selected Android packages only",
        "Select all visible Android packages",
        "Clear Android package selection",
        "Invert visible Android package selection",
        "Android packages",
        "Disable Android packages instead of uninstall",
        "Android Expert mode",
        "Exact protected package confirmation",
        "Review Android package batch",
        "Apply Android package batch",
        "Undo Android package batch",
        "Android package batch result",
        "Apply Android recommended preset",
        "Export Android selection text",
        "Export Android selection JSON",
        "Import Android package selection",
        "Open Android action log",
        "Android package snapshots",
        "Create Android package snapshot",
        "Restore Android package snapshot")) {
        if (-not (Find-Named $window $name)) { "missing Android control: $name"; exit 7 }
        "control present: $name"
    }

    # A2: the embedded list must be visible and validated counts must be read, not inferred.
    if (-not (Find-Named $window "UAD package list")) { "UAD package list heading missing"; exit 8 }
    if (-not (Find-Named $window "Check for list updates")) { "UAD update command missing"; exit 8 }

    $uadStatus = $null
    $uadInfo = $null
    $allTexts = $window.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Text)))
    foreach ($item in $allTexts) {
        $name = $item.Current.Name
        if ($name -like "UAD package list:*") { $uadStatus = $name }
        if ($name -like "*source rows*" -and $name -like "*duplicate*") { $uadInfo = $name }
    }

    if (-not $uadStatus -or $uadStatus -notlike "*2,394 package ids ready*") {
        "unexpected UAD list status: $uadStatus"; exit 8
    }
    "UAD status: $uadStatus"
    if (-not $uadInfo -or $uadInfo -notlike "*2,400 source rows*") {
        "unexpected UAD list info: $uadInfo"; exit 8
    }
    "UAD info: $uadInfo"
    "A2 package-list controls and embedded fallback visible"

    "UI probe passed"
    exit 0
}
finally {
    if ($proc -and -not $proc.HasExited) {
        $null = $proc.CloseMainWindow()
        if (-not $proc.WaitForExit(5000)) {
            Stop-Process -Id $proc.Id -Force
            "FPM did not close gracefully; stopped recorded PID $($proc.Id)"
        }
    }

    if ($hadSettings) {
        Copy-Item $backupPath $settingsPath -Force
    }

    Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
}
