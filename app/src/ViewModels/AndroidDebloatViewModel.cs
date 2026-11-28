using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Android;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Android;

namespace PCManager.ViewModels;

/// <summary>
/// A1 onboarding surface inside Debloat → Android. It locates/verifies adb, gates the pinned
/// download behind explicit consent, shows typed device failures and reads device identity only.
/// The 2 s poll runs only while this tab is visible.
/// </summary>
public sealed partial class AndroidDebloatViewModel : ObservableObject, IDisposable
{
    private readonly IAdbService _adb;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer _timer;
    private bool _visible;
    private bool _applying;

    public AndroidDebloatViewModel(
        IAdbService adb,
        ISettingsService settings,
        PackageListViewModel packageList,
        AndroidPackageViewModel packageManagement)
    {
        _adb = adb;
        _settings = settings;
        PackageList = packageList;
        PackageManagement = packageManagement;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTick;

        AdbPathOverride = settings.Current.AdbPath;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsRefreshing);
        UsePathCommand = new AsyncRelayCommand(UsePathAsync, () => !IsRefreshing);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, () => ConsentGiven && !IsRefreshing);
        PairWirelessCommand = new AsyncRelayCommand(PairWirelessAsync,
            () => !IsRefreshing && HasValidPairingInput);
        ConnectWirelessCommand = new AsyncRelayCommand(ConnectWirelessAsync,
            () => !IsRefreshing && HasValidHostAndPort);
        DisconnectWirelessCommand = new AsyncRelayCommand(DisconnectWirelessAsync,
            () => !IsRefreshing && SelectedDevice is { IsUsable: true });
    }

    public ObservableCollection<AdbDevice> Devices { get; } = new();
    public ObservableCollection<string> UserRows { get; } = new();
    public PackageListViewModel PackageList { get; }
    public AndroidPackageViewModel PackageManagement { get; }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand UsePathCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }
    public AsyncRelayCommand PairWirelessCommand { get; }
    public AsyncRelayCommand ConnectWirelessCommand { get; }
    public AsyncRelayCommand DisconnectWirelessCommand { get; }

    public string VersionText => $"Google platform-tools {AdbPackage.Version} · SHA-256 {AdbPackage.Sha256[..12]}…";
    public string LicenseText => AdbPackage.LicenseNotice;
    public string LicenseUrl => AdbPackage.LicenseUrl;

    [ObservableProperty]
    private string _adbPathOverride = string.Empty;

    [ObservableProperty]
    private string _adbStatusText = "ADB status: not checked.";

    [ObservableProperty]
    private string _adbPathText = "ADB: not located";

    [ObservableProperty]
    private string _onboardingText = AdbOnboarding.For(AdbStatusKind.Missing);

    [ObservableProperty]
    private string _deviceInfoText = "No Android device selected.";

    [ObservableProperty]
    private string _profileText = "Profiles: not read.";

    [ObservableProperty]
    private string _deviceCountText = "No device listed";

    [ObservableProperty]
    private bool _consentGiven;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private AdbDevice? _selectedDevice;

    [ObservableProperty]
    private string _wirelessHost = string.Empty;

    [ObservableProperty]
    private string _wirelessPort = "5555";

    [ObservableProperty]
    private string _pairingPort = string.Empty;

    [ObservableProperty]
    private string _pairingCode = string.Empty;

    [ObservableProperty]
    private string _wirelessStatusText = "Wireless ADB: not used.";

    [ObservableProperty]
    private string _deviceHealthText = "Health pre-check: refresh after selecting a device.";

    partial void OnConsentGivenChanged(bool value) => RefreshCommands();

    partial void OnIsRefreshingChanged(bool value) => RefreshCommands();

    partial void OnWirelessHostChanged(string value) => RefreshCommands();
    partial void OnWirelessPortChanged(string value) => RefreshCommands();
    partial void OnPairingPortChanged(string value) => RefreshCommands();
    partial void OnPairingCodeChanged(string value) => RefreshCommands();

    partial void OnSelectedDeviceChanged(AdbDevice? value)
    {
        RefreshCommands();
        if (_applying)
        {
            return;
        }

        if (value is null)
        {
            DeviceInfoText = "No Android device selected.";
            return;
        }

        _ = LoadDeviceAsync(value);
    }

    public void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }

        _visible = visible;

        if (visible)
        {
            _timer.Start();
            _ = RefreshAsync();
            _ = PackageList.LoadAsync();
        }
        else
        {
            _timer.Stop();
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private async Task RefreshAsync()
    {
        if (IsRefreshing || !_visible)
        {
            return;
        }

        IsRefreshing = true;
        AdbStatusText = "ADB status: checking…";

        try
        {
            Apply(await _adb.RefreshAsync());
        }
        catch (Exception ex)
        {
            AdbStatusText = "ADB status: failed — " + ex.Message;
            OnboardingText = AdbOnboarding.For(AdbStatusKind.ProcessFailed);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task UsePathAsync()
    {
        _settings.Current.AdbPath = AdbPathOverride.Trim();
        _settings.Save();

        IsRefreshing = true;

        try
        {
            AdbLocateResult locate = await _adb.LocateAsync();

            if (locate.Kind != AdbStatusKind.Ready)
            {
                AdbStatusText = "ADB status: " + locate.Kind;
                AdbPathText = locate.ExecutablePath is null
                    ? "ADB: not located"
                    : "ADB: " + locate.ExecutablePath;
                OnboardingText = AdbOnboarding.For(locate.Kind);
                return;
            }

            Apply(await _adb.RefreshAsync());
        }
        catch (Exception ex)
        {
            AdbStatusText = "ADB status: failed — " + ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task DownloadAsync()
    {
        if (!ConsentGiven)
        {
            OnboardingText = AdbOnboarding.For(AdbStatusKind.Declined);
            return;
        }

        IsRefreshing = true;
        IsDownloading = true;
        AdbStatusText = "ADB status: downloading Google platform-tools…";

        try
        {
            Apply(await _adb.InstallPlatformToolsAsync(consented: true));
        }
        catch (Exception ex)
        {
            AdbStatusText = "ADB status: download failed — " + ex.Message;
            OnboardingText = AdbOnboarding.For(AdbStatusKind.DownloadFailed);
        }
        finally
        {
            IsDownloading = false;
            IsRefreshing = false;
        }
    }

    private void Apply(AdbSnapshot snapshot)
    {
        AdbStatusText = $"ADB status: {snapshot.Kind} — {snapshot.Message}";
        AdbPathText = snapshot.ExecutablePath is null
            ? "ADB: not located"
            : "ADB: " + snapshot.ExecutablePath;
        OnboardingText = AdbOnboarding.For(snapshot.Kind);

        _applying = true;
        Devices.Clear();
        foreach (AdbDevice device in snapshot.Devices)
        {
            Devices.Add(device);
        }

        DeviceCountText = snapshot.Devices.Count == 0
            ? "No device listed"
            : $"{snapshot.Devices.Count} device(s) · {snapshot.Devices.Count(device => device.IsUsable)} authorized";

        SelectedDevice = snapshot.Devices.FirstOrDefault(device => device.IsUsable)
            ?? snapshot.Devices.FirstOrDefault();
        _applying = false;

        if (SelectedDevice is null)
        {
            DeviceInfoText = "No Android device selected.";
            ProfileText = "Profiles: not read.";
        }
        else
        {
            _ = LoadDeviceAsync(SelectedDevice);
        }
    }

    private async Task LoadDeviceAsync(AdbDevice device)
    {
        if (!device.IsUsable)
        {
            DeviceInfoText = $"{device.DisplayName} · {device.StateText}. Authorize the phone to read device info.";
            ProfileText = "Profiles: not read.";
            PackageManagement.SetDevice(null, Array.Empty<AdbUser>());
            return;
        }

        DeviceInfoText = "Reading device identity…";
        ProfileText = "Profiles: reading…";

        try
        {
            AdbDeviceInfo? info = await _adb.GetDeviceInfoAsync(device);
            IReadOnlyList<AdbUser> users = await _adb.GetUsersAsync(device);
            PackageManagement.SetDevice(device, users);

            DeviceInfoText = info is null
                ? "Device identity is unavailable; refresh after authorizing the phone."
                : $"{info.DisplayName} · {info.Brand}\n" +
                  $"Android {info.AndroidVersion} (SDK {info.SdkLevel}) · {info.Transport}\n" +
                  $"Serial {info.Serial} · patch {info.SecurityPatch} · build {info.Incremental}";

            UserRows.Clear();
            foreach (AdbUser user in users)
            {
                UserRows.Add(
                    $"{(user.IsWorkProfile ? "Work profile" : "User")} · id {user.Id} · {user.Name}" +
                    $" · flags {user.Flags}{(user.IsRunning ? " · running" : string.Empty)}");
            }

            ProfileText = users.Count == 0
                ? "Profiles: none reported."
                : $"{users.Count} profile(s) · {users.Count(user => user.IsWorkProfile)} work profile(s)";

            AdbProcessResult battery = await _adb.GetBatteryAsync(device);
            int? batteryPercent = battery.Success
                ? AdbParsers.ParseBatteryPercent(battery.StandardOutput)
                : null;
            string batteryText = batteryPercent is null
                ? "Battery: unavailable"
                : $"Battery: {batteryPercent}%";
            DeviceHealthText =
                $"{batteryText} · USB debugging authorized · screen-unlock status unavailable · " +
                (device.Connection == AdbConnectionKind.Wireless
                    ? "Wireless connection stable"
                    : "USB connection stable") +
                (batteryPercent is < 30
                    ? " · Warning: below 30%; charging is recommended."
                    : " · Apply is allowed.");
        }
        catch (Exception ex)
        {
            DeviceInfoText = "Device identity failed: " + ex.Message;
            ProfileText = "Profiles: not read.";
        }
    }

    private bool HasValidHostAndPort
    {
        get
        {
            return AdbCommandLine.IsPrivateIpAddress(WirelessHost) &&
                   int.TryParse(WirelessPort, out int port) &&
                   port is >= 1 and <= 65535;
        }
    }

    private bool HasValidPairingInput
    {
        get
        {
            return AdbCommandLine.IsPrivateIpAddress(WirelessHost) &&
                   int.TryParse(PairingPort, out int port) &&
                   port is >= 1 and <= 65535 &&
                   PairingCode.Length == 6 &&
                   PairingCode.All(char.IsDigit);
        }
    }

    private async Task PairWirelessAsync()
    {
        if (!HasValidPairingInput ||
            !int.TryParse(PairingPort, out int port))
        {
            WirelessStatusText =
                "Wireless ADB: use a private IPv4 address and a 6-digit pairing code.";
            return;
        }

        IsRefreshing = true;
        WirelessStatusText = "Wireless ADB: pairing…";
        try
        {
            AdbProcessResult result =
                await _adb.PairWirelessAsync(
                    WirelessHost.Trim(),
                    port,
                    PairingCode.Trim());
            WirelessStatusText = result.Success
                ? "Wireless ADB: paired. Enter the connect port, then press Connect."
                : "Wireless ADB pairing failed: " + result.Combined;
        }
        catch (Exception ex)
        {
            WirelessStatusText = "Wireless ADB pairing failed: " + ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task ConnectWirelessAsync()
    {
        if (!HasValidHostAndPort ||
            !int.TryParse(WirelessPort, out int port))
        {
            WirelessStatusText =
                "Wireless ADB: use a private IPv4 address and port 1–65535.";
            return;
        }

        IsRefreshing = true;
        WirelessStatusText = "Wireless ADB: connecting…";
        try
        {
            AdbProcessResult result =
                await _adb.ConnectWirelessAsync(
                    WirelessHost.Trim(),
                    port);
            WirelessStatusText = result.Success
                ? "Wireless ADB: connected."
                : "Wireless ADB connection failed: " + result.Combined;
            if (result.Success)
            {
                Apply(await _adb.RefreshAsync());
            }
        }
        catch (Exception ex)
        {
            WirelessStatusText = "Wireless ADB connection failed: " + ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task DisconnectWirelessAsync()
    {
        if (SelectedDevice is not { Connection: AdbConnectionKind.Wireless })
        {
            WirelessStatusText =
                "Wireless ADB: disconnect is available only for a wireless-selected device.";
            return;
        }

        IsRefreshing = true;
        WirelessStatusText = "Wireless ADB: disconnecting…";
        try
        {
            AdbProcessResult result =
                await _adb.DisconnectWirelessAsync(SelectedDevice);
            WirelessStatusText = result.Success
                ? "Wireless ADB: disconnected."
                : "Wireless ADB disconnect failed: " + result.Combined;
            Apply(await _adb.RefreshAsync());
        }
        catch (Exception ex)
        {
            WirelessStatusText = "Wireless ADB disconnect failed: " + ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (!IsRefreshing)
        {
            await RefreshAsync();
        }
    }

    private void RefreshCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        UsePathCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        PairWirelessCommand.NotifyCanExecuteChanged();
        ConnectWirelessCommand.NotifyCanExecuteChanged();
        DisconnectWirelessCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(VersionText));
    }
}
