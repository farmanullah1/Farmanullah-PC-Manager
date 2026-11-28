namespace PCManager.Core.Android;

public enum AdbDeviceState
{
    Device,
    Unauthorized,
    Offline,
    NoPermissions,
    Unknown,
}

public enum AdbConnectionKind
{
    Usb,
    Wireless,
    Emulator,
    Unknown,
}

public sealed record AdbDevice(
    string Serial,
    AdbDeviceState State,
    string RawState,
    string? Model,
    string? Product,
    string? Device,
    string? TransportId,
    AdbConnectionKind Connection)
{
    public bool IsUsable => State == AdbDeviceState.Device;

    public string DisplayName => Model is { Length: > 0 }
        ? $"{Model.Replace('_', ' ')} · {Serial}"
        : Serial;

    public string StateText => State switch
    {
        AdbDeviceState.Device => "Authorized",
        AdbDeviceState.Unauthorized => "Unauthorized",
        AdbDeviceState.Offline => "Offline",
        AdbDeviceState.NoPermissions => "No USB permission",
        _ => "Unknown",
    };
}

public sealed record AdbUser(int Id, string Name, string Flags, bool IsRunning, bool IsWorkProfile);

public sealed record AdbDeviceInfo(
    string Serial,
    string Brand,
    string Manufacturer,
    string Model,
    string Device,
    string AndroidVersion,
    string SdkLevel,
    string BuildFingerprint,
    string SecurityPatch,
    string Incremental,
    string DisplayName,
    string Transport);

public enum AdbStatusKind
{
    Ready,
    Missing,
    Unverified,
    Declined,
    DownloadFailed,
    ChecksumMismatch,
    NoDevice,
    Unauthorized,
    Offline,
    NoPermissions,
    ProcessFailed,
}

public sealed record AdbSnapshot(
    AdbStatusKind Kind,
    string Message,
    string? ExecutablePath,
    IReadOnlyList<AdbDevice> Devices,
    bool ServerStartedByFpm)
{
    public bool HasUsableDevice => Devices.Any(device => device.IsUsable);
}

public sealed record AdbLocateResult(AdbStatusKind Kind, string? ExecutablePath, string Message);

public sealed record AdbProvisionResult(AdbStatusKind Kind, string? ExecutablePath, string Message)
{
    public bool Success => Kind == AdbStatusKind.Ready;
}

public sealed record AdbHealthStatus(
    int? BatteryPercent,
    bool? AcPowered,
    bool? UsbPowered,
    bool DeviceUnlocked,
    bool UsbDebuggingAuthorized,
    bool ConnectionStable,
    string Message)
{
    public bool IsApplySafe =>
        BatteryPercent is null ||
        BatteryPercent >= 30;
}

public static class AdbOnboarding
{
    public static string For(AdbStatusKind kind) => kind switch
    {
        AdbStatusKind.Missing =>
            "ADB was not found. Set a path above, install Android platform-tools, or download Google's official package after accepting the Android SDK license. FPM never downloads automatically.",
        AdbStatusKind.Unverified =>
            "The adb.exe found is missing a valid Google Authenticode signature, so FPM will not run it. Choose another copy or download the official platform-tools package.",
        AdbStatusKind.Declined =>
            "Download declined. No network request was made and no files were changed.",
        AdbStatusKind.ChecksumMismatch =>
            "The downloaded archive did not match the pinned SHA-256 checksum. It was deleted before extraction; nothing was installed.",
        AdbStatusKind.DownloadFailed =>
            "The platform-tools download or extraction did not complete. Nothing was installed; check the network and try again.",
        AdbStatusKind.NoDevice =>
            "No Android device is connected. On the phone: enable Developer options, turn on USB debugging, install the OEM USB driver, then try another cable or USB port. Driver link: developer.android.com/studio/run/oem-usb#Drivers",
        AdbStatusKind.Unauthorized =>
            "The phone is connected but not authorized. Unlock it and accept the USB debugging prompt; tick “Always allow from this computer”.",
        AdbStatusKind.Offline =>
            "The device is offline. Reconnect the USB cable, unlock the phone, and refresh. If it stays offline, restart the ADB server.",
        AdbStatusKind.NoPermissions =>
            "Windows denied USB access to the device. Install the OEM USB driver and check the cable/port; FPM cannot fix a missing driver.",
        AdbStatusKind.ProcessFailed =>
            "ADB did not return a usable response. Refresh to retry; the app log has the exact command result.",
        _ => "The selected device is ready for read-only identity checks. Package scanning and actions belong to later Android cards.",
    };
}
