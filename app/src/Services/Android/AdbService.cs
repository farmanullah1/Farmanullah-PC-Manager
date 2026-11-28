using PCManager.Core.Android;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Android;

/// <summary>Runs a typed command on an already-discovered, usable device.</summary>
public interface IAdbCommandExecutor
{
    Task<AdbProcessResult> ExecuteAsync(
        AdbDevice device,
        AdbCommand command,
        CancellationToken cancellationToken = default);
}

public interface IAdbService
{
    string? ExecutablePath { get; }
    bool ServerStartedByFpm { get; }

    Task<AdbLocateResult> LocateAsync(CancellationToken cancellationToken = default);

    Task<AdbSnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    Task<AdbSnapshot> InstallPlatformToolsAsync(
        bool consented,
        CancellationToken cancellationToken = default);

    Task<AdbDeviceInfo?> GetDeviceInfoAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdbUser>> GetUsersAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default);

    Task<AdbProcessResult> PairWirelessAsync(
        string host,
        int port,
        string pairingCode,
        CancellationToken cancellationToken = default);

    Task<AdbProcessResult> ConnectWirelessAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default);

    Task<AdbProcessResult> DisconnectWirelessAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default);

    Task<AdbProcessResult> GetBatteryAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default);

    void Shutdown();
}

/// <summary>
/// Locates and verifies adb, lazily starts the server, parses read-only device state and owns
/// the server only when this process actually started it. No package action is possible here.
/// </summary>
public sealed class AdbService : IAdbService, IAdbCommandExecutor, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly IAdbRunner _runner;
    private readonly IAdbSignatureVerifier _signature;
    private readonly IAdbProvisioner _provisioner;
    private readonly IAppLogger _logger;
    private readonly AdbSearchPaths _paths;
    private string? _adbPath;
    private bool _serverReady;
    private bool _startedServer;

    public AdbService(
        ISettingsService settings,
        IAdbRunner runner,
        IAdbSignatureVerifier signature,
        IAdbProvisioner provisioner,
        IAppLogger logger,
        AdbSearchPaths? paths = null)
    {
        _settings = settings;
        _runner = runner;
        _signature = signature;
        _provisioner = provisioner;
        _logger = logger;
        _paths = paths ?? AdbSearchPaths.FromEnvironment();
    }

    public string? ExecutablePath => _adbPath;
    public bool ServerStartedByFpm => _startedServer;

    public Task<AdbLocateResult> LocateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AdbPathCandidate> candidates = AdbPathResolver.Resolve(
            _settings.Current.AdbPath,
            _paths);

        string? unverified = null;

        foreach (AdbPathCandidate candidate in candidates)
        {
            if (_signature.IsGoogleSigned(candidate.Path))
            {
                _adbPath = candidate.Path;
                _logger.Info($"ADB found via {candidate.Source}: {candidate.Path}");
                return Task.FromResult(new AdbLocateResult(
                    AdbStatusKind.Ready,
                    candidate.Path,
                    $"ADB found via {candidate.Source}: {candidate.Path}"));
            }

            unverified ??= candidate.Path;
        }

        _adbPath = null;

        if (unverified is not null)
        {
            return Task.FromResult(new AdbLocateResult(
                AdbStatusKind.Unverified,
                unverified,
                "The adb.exe found is not signed by Google. FPM will not run it."));
        }

        return Task.FromResult(new AdbLocateResult(
            AdbStatusKind.Missing,
            null,
            "adb.exe was not found in the configured path, FPM tools, an Android SDK, or PATH."));
    }

    public async Task<AdbSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        AdbLocateResult locate = await LocateAsync(cancellationToken).ConfigureAwait(false);

        if (locate.Kind != AdbStatusKind.Ready || locate.ExecutablePath is null)
        {
            return Snapshot(locate.Kind, locate.Message, locate.ExecutablePath, Array.Empty<AdbDevice>());
        }

        if (!await EnsureServerAsync(cancellationToken).ConfigureAwait(false))
        {
            return Snapshot(
                AdbStatusKind.ProcessFailed,
                "adb start-server did not succeed.",
                _adbPath,
                Array.Empty<AdbDevice>());
        }

        string executable = locate.ExecutablePath;
        AdbProcessResult result = await _runner
            .RunAsync(executable, AdbCommand.Devices(), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return Snapshot(
                AdbStatusKind.ProcessFailed,
                "adb devices -l failed: " + Message(result),
                _adbPath,
                Array.Empty<AdbDevice>());
        }

        IReadOnlyList<AdbDevice> devices = AdbParsers.ParseDevices(result.StandardOutput);
        AdbStatusKind kind = devices.Count == 0
            ? AdbStatusKind.NoDevice
            : devices.Any(device => device.IsUsable)
                ? AdbStatusKind.Ready
                : devices.Any(device => device.State == AdbDeviceState.Unauthorized)
                    ? AdbStatusKind.Unauthorized
                    : devices.Any(device => device.State == AdbDeviceState.Offline)
                        ? AdbStatusKind.Offline
                        : devices.Any(device => device.State == AdbDeviceState.NoPermissions)
                            ? AdbStatusKind.NoPermissions
                            : AdbStatusKind.ProcessFailed;

        string message = kind switch
        {
            AdbStatusKind.Ready => $"{devices.Count(device => device.IsUsable)} authorized device(s).",
            AdbStatusKind.NoDevice => "No Android device is connected.",
            AdbStatusKind.Unauthorized => "A device is waiting for USB debugging authorization.",
            AdbStatusKind.Offline => "A device is offline.",
            AdbStatusKind.NoPermissions => "Windows denied USB access to a device.",
            _ => "ADB returned no recognized device state.",
        };

        return Snapshot(kind, message, _adbPath, devices);
    }

    public async Task<AdbSnapshot> InstallPlatformToolsAsync(
        bool consented,
        CancellationToken cancellationToken = default)
    {
        if (!consented)
        {
            return Snapshot(
                AdbStatusKind.Declined,
                "No consent was given; no download was attempted.",
                null,
                Array.Empty<AdbDevice>());
        }

        AdbProvisionResult install = await _provisioner
            .InstallAsync(consented, cancellationToken)
            .ConfigureAwait(false);

        if (!install.Success)
        {
            return Snapshot(
                install.Kind,
                install.Message,
                install.ExecutablePath,
                Array.Empty<AdbDevice>());
        }

        _adbPath = install.ExecutablePath;
        _serverReady = false;
        return await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdbDeviceInfo?> GetDeviceInfoAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!device.IsUsable ||
            _adbPath is null ||
            !await EnsureServerAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        AdbProcessResult result = await _runner
            .RunAsync(_adbPath, AdbCommand.GetProperties(device.Serial), cancellationToken)
            .ConfigureAwait(false);

        return result.Success
            ? AdbParsers.BuildInfo(device, AdbParsers.ParseGetProp(result.StandardOutput))
            : null;
    }

    public async Task<IReadOnlyList<AdbUser>> GetUsersAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!device.IsUsable ||
            _adbPath is null ||
            !await EnsureServerAsync(cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<AdbUser>();
        }

        AdbProcessResult result = await _runner
            .RunAsync(_adbPath, AdbCommand.ListUsers(device.Serial), cancellationToken)
            .ConfigureAwait(false);

        return result.Success
            ? AdbParsers.ParseUsers(result.StandardOutput)
            : Array.Empty<AdbUser>();
    }

    public async Task<AdbProcessResult> PairWirelessAsync(
        string host,
        int port,
        string pairingCode,
        CancellationToken cancellationToken = default)
    {
        if (_adbPath is null)
        {
            AdbLocateResult locate = await LocateAsync(cancellationToken).ConfigureAwait(false);
            if (locate.Kind != AdbStatusKind.Ready)
            {
                return new AdbProcessResult(-1, string.Empty, locate.Message);
            }
        }

        if (!await EnsureServerAsync(cancellationToken).ConfigureAwait(false))
        {
            return new AdbProcessResult(-1, string.Empty, "ADB server could not start.");
        }

        AdbCommand command;
        try
        {
            command = AdbCommand.PairWireless(host, port, pairingCode);
        }
        catch (ArgumentException ex)
        {
            return new AdbProcessResult(-1, string.Empty, ex.Message);
        }
        return await _runner.RunAsync(_adbPath!, command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdbProcessResult> ConnectWirelessAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        if (_adbPath is null)
        {
            AdbLocateResult locate = await LocateAsync(cancellationToken).ConfigureAwait(false);
            if (locate.Kind != AdbStatusKind.Ready)
            {
                return new AdbProcessResult(-1, string.Empty, locate.Message);
            }
        }

        if (!await EnsureServerAsync(cancellationToken).ConfigureAwait(false))
        {
            return new AdbProcessResult(-1, string.Empty, "ADB server could not start.");
        }

        AdbCommand command;
        try
        {
            command = AdbCommand.ConnectWireless(host, port);
        }
        catch (ArgumentException ex)
        {
            return new AdbProcessResult(-1, string.Empty, ex.Message);
        }
        return await _runner.RunAsync(_adbPath!, command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdbProcessResult> DisconnectWirelessAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default)
    {
        if (_adbPath is null)
        {
            return new AdbProcessResult(-1, string.Empty, "ADB is not located.");
        }
        return await _runner.RunAsync(
            _adbPath,
            AdbCommand.DisconnectWireless(device.Serial),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdbProcessResult> GetBatteryAsync(
        AdbDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!device.IsUsable ||
            _adbPath is null ||
            !await EnsureServerAsync(cancellationToken).ConfigureAwait(false))
        {
            return new AdbProcessResult(-1, string.Empty, "Device is not authorized.");
        }

        return await _runner.RunAsync(
            _adbPath,
            AdbCommand.GetBattery(device.Serial),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdbProcessResult> ExecuteAsync(
        AdbDevice device,
        AdbCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!device.IsUsable)
        {
            return new AdbProcessResult(-1, string.Empty, "The Android device is not authorized.");
        }

        if (_adbPath is null || !await EnsureServerAsync(cancellationToken).ConfigureAwait(false))
        {
            return new AdbProcessResult(-1, string.Empty, "ADB is not ready.");
        }

        return await _runner.RunAsync(_adbPath, command, cancellationToken).ConfigureAwait(false);
    }

    public void Shutdown()
    {
        if (!_startedServer || _adbPath is null)
        {
            return;
        }

        try
        {
            _runner.RunAsync(_adbPath, AdbCommand.KillServer()).GetAwaiter().GetResult();
            _logger.Info("ADB server stopped because FPM started it.");
        }
        catch (Exception ex)
        {
            _logger.Error("ADB server could not be stopped.", ex);
        }
        finally
        {
            _startedServer = false;
            _serverReady = false;
        }
    }

    public void Dispose() => Shutdown();

    private async Task<bool> EnsureServerAsync(CancellationToken cancellationToken)
    {
        if (_serverReady)
        {
            return true;
        }

        if (_adbPath is null)
        {
            return false;
        }

        AdbProcessResult result = await _runner
            .RunAsync(_adbPath, AdbCommand.StartServer(), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            return false;
        }

        string output = result.Combined;
        _startedServer = output.Contains("daemon not running", StringComparison.OrdinalIgnoreCase) ||
                         output.Contains("daemon started successfully", StringComparison.OrdinalIgnoreCase);
        _serverReady = true;
        _logger.Info(_startedServer
            ? "ADB server started by FPM."
            : "ADB server was already running; FPM will not stop it.");
        return true;
    }

    private AdbSnapshot Snapshot(
        AdbStatusKind kind,
        string message,
        string? path,
        IReadOnlyList<AdbDevice> devices) =>
        new(kind, message, path, devices, _startedServer);

    private static string Message(AdbProcessResult result) =>
        result.Cancelled
            ? "cancelled"
            : result.TimedOut
                ? "timed out"
                : result.Combined.Length > 0
                    ? result.Combined
                    : $"exit code {result.ExitCode}";
}
