param(
    [ValidateSet("Light", "Dark")]
    [string]$Theme = "Light",
    [ValidateSet(560, 1280)]
    [int]$Width = 560
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
[void][System.Reflection.Assembly]::LoadWithPartialName("System.Drawing")
$drawingAssembly = [System.Drawing.Bitmap].Assembly.Location
Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class FpmCapture
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static Bitmap Capture(IntPtr hwnd)
    {
        Rect rect;
        if (!GetWindowRect(hwnd, out rect))
        {
            throw new InvalidOperationException("Could not read the window rectangle.");
        }

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            try
            {
                if (!PrintWindow(hwnd, hdc, 2))
                {
                    throw new InvalidOperationException("PrintWindow failed.");
                }
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
        }
        return bitmap;
    }
}
"@ -ReferencedAssemblies $drawingAssembly

$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot "app\src\PCManager\bin\Debug\net8.0-windows\FPM.exe"
$outputDir = Join-Path $repoRoot "docs\images"
$settingsPath = Join-Path $env:LOCALAPPDATA "FPM\settings.json"
$backupPath = Join-Path ([IO.Path]::GetTempPath()) ("fpm-r5-settings-" + [Guid]::NewGuid().ToString("N") + ".json")

if (-not (Test-Path $exePath)) {
    throw "FPM.exe was not built: $exePath"
}
if (Get-Process FPM -ErrorAction SilentlyContinue) {
    throw "Stop FPM before capturing README screenshots."
}

New-Item -ItemType Directory -Force $outputDir | Out-Null
$hadSettings = Test-Path $settingsPath
if ($hadSettings) {
    Copy-Item $settingsPath $backupPath -Force
}

$proc = $null
try {
    $settings = if ($hadSettings) {
        Get-Content $settingsPath -Raw | ConvertFrom-Json
    } else {
        [ordered]@{}
    }
    $settings.Theme = if ($Theme -eq "Dark") { 2 } else { 1 }
    $settings.ShowFloatingToolbar = $true
    $settings.ToolbarDock = 3
    $settings.ToolbarPinned = $true
    $settings.ToolbarTopmost = $true
    $settings.CloseToTray = $false
    $settings.ShowSplash = $false
    $settings.MainWindowWidth = $Width
    $settings.MainWindowHeight = if ($Width -eq 560) { 780 } else { 900 }
    $settings.MainWindowMaximized = $false
    $settings.ReduceMotion = $true
    $settings | ConvertTo-Json -Depth 12 | Set-Content $settingsPath -Encoding UTF8

    $proc = Start-Process -FilePath $exePath -ArgumentList "--no-splash" -PassThru
    $deadline = (Get-Date).AddSeconds(40)
    while ((Get-Date) -lt $deadline -and $proc.MainWindowHandle -eq 0 -and -not $proc.HasExited) {
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
    }
    if ($proc.HasExited) {
        throw "FPM exited during startup: $($proc.ExitCode)"
    }
    if ($proc.MainWindowHandle -eq 0) {
        throw "FPM did not create a main window."
    }

    $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)))
    if (-not $window) {
        throw "FPM automation window was not found."
    }

    function Invoke-Named($root, [string]$name) {
        $matches = $root.FindAll(
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
                } catch {}
            }
        }
        return $false
    }

    function Save-Capture([string]$name) {
        $bitmap = [FpmCapture]::Capture([IntPtr]$proc.MainWindowHandle)
        try {
            $path = Join-Path $outputDir ($name + "-" + $Theme.ToLowerInvariant() + ".png")
            $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            "captured $path"
        } finally {
            $bitmap.Dispose()
        }
    }

    Start-Sleep -Seconds 3
    Save-Capture "home"

    if (Invoke-Named $window "Memory") {
        Start-Sleep -Seconds 3
        Save-Capture "memory"
    }

    if (Invoke-Named $window "Storage") {
        Start-Sleep -Seconds 3
        Save-Capture "storage"
    }

    if (Invoke-Named $window "Debloat") {
        Start-Sleep -Seconds 3
        if (Invoke-Named $window "Windows tab") {
            Start-Sleep -Seconds 2
            Save-Capture "debloat-windows"
        }
        if (Invoke-Named $window "Android tab") {
            Start-Sleep -Seconds 3
            Save-Capture "debloat-android"
        }
    }

    Start-Sleep -Seconds 2
    $children = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($child in $children) {
        try {
            if ($child.Current.ProcessId -ne $proc.Id) {
                continue
            }
            $hwnd = [IntPtr]$child.GetCurrentPropertyValue(
                [System.Windows.Automation.AutomationElement]::NativeWindowHandleProperty)
            if ($hwnd -eq [IntPtr]$proc.MainWindowHandle) {
                continue
            }
            $name = $child.Current.Name
            if ($name -like "*Toolbar*") {
                $bitmap = [FpmCapture]::Capture($hwnd)
                try {
                    $bitmap.Save(
                        (Join-Path $outputDir "toolbar-docked.png"),
                        [System.Drawing.Imaging.ImageFormat]::Png)
                    "captured toolbar-docked.png"
                } finally {
                    $bitmap.Dispose()
                }
                break
            }
        } catch {}
    }

    & $exePath "--exit" | Out-Null
    Start-Sleep -Seconds 2
    if (-not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }
} finally {
    if ($proc -and -not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }
    if ($hadSettings) {
        Copy-Item $backupPath $settingsPath -Force
        Remove-Item $backupPath -Force -ErrorAction SilentlyContinue
    } else {
        Remove-Item $settingsPath -Force -ErrorAction SilentlyContinue
    }
}
