namespace PCManager.Core.Models;

/// <summary>An entry in the Toolbox grid.</summary>
public sealed class ToolboxTool
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon3DKey { get; set; } = string.Empty;
}

public sealed class ToolboxLayoutEntry
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon3DKey { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ToolboxLayoutKind Kind { get; set; } = ToolboxLayoutKind.Tool;
    public string Status { get; set; } = string.Empty;
}

public enum ToolboxLayoutKind
{
    Metric,
    Action,
    Tool,
}

public sealed class ToolboxLayoutFile
{
    public int Version { get; set; } = 1;
    public List<ToolboxLayoutEntry> Entries { get; set; } = new();
}

/// <summary>The browser Windows currently uses for http/https links.</summary>
public sealed class DefaultBrowserSetting
{
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
}

/// <summary>Persistent, user-editable application settings.</summary>
public sealed class AppSettings
{
    public ThemeOption Theme { get; set; } = ThemeOption.System;
    public bool AutoRefreshProcessList { get; set; } = true;
    public long LargeFileThresholdBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>Mirrors the HKCU Run value so the Settings toggle survives restarts.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>"Smart Boost" trims memory automatically when load passes the threshold.</summary>
    public bool SmartBoostEnabled { get; set; } = true;

    /// <summary>Memory load percentage that triggers a Smart Boost.</summary>
    public int SmartBoostThresholdPercent { get; set; } = 75;

    /// <summary>Shows the always-on-top floating toolbar pill.</summary>
    public bool ShowFloatingToolbar { get; set; }

    /// <summary>Persisted X coordinate of the floating toolbar (null for default placement).</summary>
    public double? FloatingToolbarLeft { get; set; }

    /// <summary>Persisted Y coordinate of the floating toolbar (null for default placement).</summary>
    public double? FloatingToolbarTop { get; set; }

    /// <summary>Persisted width of the main window (defaults to 560 standard panel).</summary>
    public double MainWindowWidth { get; set; } = 560;

    /// <summary>Persisted height of the main window (defaults to 780 standard panel).</summary>
    public double MainWindowHeight { get; set; } = 780;

    /// <summary>Persisted X coordinate of the main window.</summary>
    public double? MainWindowLeft { get; set; }

    /// <summary>Persisted Y coordinate of the main window.</summary>
    public double? MainWindowTop { get; set; }

    /// <summary>Whether the main window was maximized when last closed.</summary>
    public bool MainWindowMaximized { get; set; }

    /// <summary>
    /// W1: placement per monitor device and DPI, keyed by <see cref="WindowBoundsTable.Key"/>.
    /// The legacy MainWindow* fields above remain for files written by earlier builds.
    /// </summary>
    public Dictionary<string, WindowBounds> WindowBoundsByMonitor { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>W4: edge the floating toolbar is docked to.</summary>
    public DockEdge ToolbarDock { get; set; }

    /// <summary>W4: keep the docked drawer expanded after the pointer leaves.</summary>
    public bool ToolbarPinned { get; set; }

    /// <summary>W4: toolbar opacity, 0.6–1.0.</summary>
    public double ToolbarOpacity { get; set; } = 1.0;

    /// <summary>W4: always-on-top toggle.</summary>
    public bool ToolbarTopmost { get; set; } = true;

    /// <summary>W4: floating toolbar position per monitor and DPI.</summary>
    public Dictionary<string, WindowBounds> ToolbarBoundsByMonitor { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>W3: closing the main window hides it to the tray while the toolbar runs (default on).</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>W3: remembered answer to the tray Exit question.</summary>
    public ExitPreference ExitPreference { get; set; } = ExitPreference.Ask;

    /// <summary>W3: start at sign-in in toolbar-only mode when the toolbar was left running.</summary>
    public bool RestoreToolbarAtSignIn { get; set; } = true;

    /// <summary>Follow the main window to whichever virtual desktop is active (W2, off by default).</summary>
    public bool MainWindowFollowDesktops { get; set; }

    /// <summary>Keep the floating toolbar on the active virtual desktop (W2, on by default).</summary>
    public bool ToolbarFollowDesktops { get; set; } = true;

    /// <summary>Days a file may stay in the quarantine before the retention pass removes it.</summary>
    public int QuarantineRetentionDays { get; set; } = 7;

    /// <summary>Active application language code ("en" or "ur").</summary>
    public string Language { get; set; } = "en";

    /// <summary>Whether local usage tracking is enabled for stale app discovery.</summary>
    public bool UsageTrackingEnabled { get; set; } = true;

    /// <summary>Upper bound for the whole quarantine, evicting the oldest entries first.</summary>
    public long QuarantineMaxBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>User-chosen http(s) endpoint for the download speed test.</summary>
    public string SpeedTestEndpoint { get; set; } = "https://speed.cloudflare.com/__down?bytes=10000000";

    /// <summary>App-level reduce motion: keep fades, drop slides, scales, springs and pulses.</summary>
    public bool ReduceMotion { get; set; }

    /// <summary>T2: temperature display unit; the stored value is always Celsius.</summary>
    public TemperatureUnit TemperatureUnit { get; set; }

    /// <summary>Low Resource Mode, reserved for the battery-friendly profile; downgrades motion.</summary>
    public bool LowResourceModeEnabled { get; set; }

    /// <summary>Shows the startup splash unless the app was launched with --no-splash.</summary>
    public bool ShowSplash { get; set; } = true;

    /// <summary>A1: optional user-configured adb.exe (or its folder). Empty means search FPM/SDK/PATH.</summary>
    public string AdbPath { get; set; } = string.Empty;

    /// <summary>A3: devices that refuse uninstall use disable-user instead; reviewed by the user first.</summary>
    public bool AndroidDisableInsteadOfUninstall { get; set; }

    /// <summary>A4: Android debloat safety warning was acknowledged.</summary>
    public bool AndroidSafetyAcknowledged { get; set; }
}

public enum ThemeOption
{
    Default = 0,
    Light = 1,
    Dark = 2,
    System = 3
}
