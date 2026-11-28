using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>One running application with its own End command.</summary>
public sealed class RunningAppViewModel : ObservableObject
{
    private readonly Func<RunningAppViewModel, Task> _end;

    public RunningAppViewModel(ProcessInfo process, Func<RunningAppViewModel, Task> end)
    {
        Process = process;
        _end = end;
        EndCommand = new AsyncRelayCommand(() => _end(this));
    }

    public ProcessInfo Process { get; }
    public int ProcessId => Process.ProcessId;
    public string Name => Process.Name;
    public string PidText => $"PID {Process.ProcessId}";
    public string MemoryText => Process.MemoryText;
    public string CpuText => Process.CpuText;
    public AsyncRelayCommand EndCommand { get; }
}

/// <summary>One installed application with its uninstall command and leftovers scan command.</summary>
public sealed class InstalledAppViewModel : ObservableObject
{
    private readonly Func<InstalledAppViewModel, Task> _uninstall;
    private readonly Func<InstalledAppViewModel, Task>? _scanLeftovers;

    public InstalledAppViewModel(
        InstalledApp app,
        Func<InstalledAppViewModel, Task> uninstall,
        Func<InstalledAppViewModel, Task>? scanLeftovers = null)
    {
        App = app;
        _uninstall = uninstall;
        _scanLeftovers = scanLeftovers;
        UninstallCommand = new AsyncRelayCommand(() => _uninstall(this));
        ScanLeftoversCommand = new AsyncRelayCommand(() => _scanLeftovers?.Invoke(this) ?? Task.CompletedTask);
    }

    public InstalledApp App { get; }
    public string Name => App.Name;
    public string Subtitle => App.Subtitle;
    public string VersionText => string.IsNullOrWhiteSpace(App.Version) ? string.Empty : $"v{App.Version}";
    public string SizeText => App.SizeText;
    public AsyncRelayCommand UninstallCommand { get; }
    public AsyncRelayCommand ScanLeftoversCommand { get; }
}

/// <summary>One Store/MSIX package with a confirmation-gated uninstall command.</summary>
public sealed class StoreAppViewModel : ObservableObject
{
    private readonly Func<StoreAppViewModel, Task> _uninstall;

    public StoreAppViewModel(AppxPackage package, Func<StoreAppViewModel, Task> uninstall)
    {
        Package = package;
        _uninstall = uninstall;
        UninstallCommand = new AsyncRelayCommand(() => _uninstall(this));
    }

    public AppxPackage Package { get; }
    public string Name => Package.Name;
    public string Subtitle => Package.Subtitle;
    public string PackageFullName => Package.PackageFullName;
    public AsyncRelayCommand UninstallCommand { get; }
}

/// <summary>
/// Apps: running user applications (ending is refused for protected processes), startup
/// entries with real On/Off toggles, installed desktop apps and Store/MSIX packages.
/// </summary>
public sealed partial class AppsViewModel : ViewModelBase, IDisposable
{
    private const int MaxRows = 60;

    private readonly IProcessService _processes;
    private readonly IInstalledAppService _installedApps;
    private readonly IAppxPackageService _appxPackages;
    private readonly IUserConfirmationService _confirmation;
    private readonly ISettingsService _settings;
    private readonly IAppLogger _logger;
    private readonly IAppLeftoverScanner? _leftoverScanner;
    private readonly DispatcherTimer _timer;

    private IReadOnlyList<InstalledApp> _allInstalled = Array.Empty<InstalledApp>();
    private IReadOnlyList<AppxPackage> _allStore = Array.Empty<AppxPackage>();
    private IReadOnlyList<RunningAppViewModel> _allRunning = Array.Empty<RunningAppViewModel>();
    private bool _refreshing;

    public AppsViewModel(
        IProcessService processes,
        IInstalledAppService installedApps,
        IAppxPackageService appxPackages,
        IUserConfirmationService confirmation,
        ISettingsService settings,
        IAppLogger logger,
        IAppLeftoverScanner? leftoverScanner = null)
    {
        _processes = processes;
        _installedApps = installedApps;
        _appxPackages = appxPackages;
        _confirmation = confirmation;
        _settings = settings;
        _logger = logger;
        _leftoverScanner = leftoverScanner;

        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        RefreshStartupCommand = new AsyncRelayCommand(LoadStartupAsync, () => !IsBusy);
        ScanLeftoversCommand = new AsyncRelayCommand(() => ScanLeftoversAsync(LeftoverTargetApp), () => !IsBusy && !IsScanningLeftovers);
        CleanLeftoversCommand = new AsyncRelayCommand(CleanLeftoversAsync, () => !IsBusy && HasLeftoverItems);
        ExportRegBackupCommand = new AsyncRelayCommand(ExportRegBackupAsync, () => !IsBusy && HasLeftoverItems);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) =>
        {
            if (_settings.Current.AutoRefreshProcessList && !_refreshing && !IsBusy)
            {
                await RefreshProcessesAsync();
            }
        };
        _timer.Start();
    }

    public ObservableCollection<RunningAppViewModel> RunningApps { get; } = new();
    public ObservableCollection<RunningAppViewModel> VisibleRunningApps { get; } = new();
    public ObservableCollection<StartupAppViewModel> StartupApps { get; } = new();
    public ObservableCollection<InstalledAppViewModel> InstalledApps { get; } = new();
    public ObservableCollection<StoreAppViewModel> StoreApps { get; } = new();
    public ObservableCollection<AppLeftoverItem> LeftoverItems { get; } = new();
    public ObservableCollection<string> ProcessSortOptions { get; } =
        new(new[] { "Memory", "CPU", "Name" });

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand RefreshStartupCommand { get; }
    public AsyncRelayCommand ScanLeftoversCommand { get; }
    public AsyncRelayCommand CleanLeftoversCommand { get; }
    public AsyncRelayCommand ExportRegBackupCommand { get; }

    [ObservableProperty]
    private string _appSearch = string.Empty;

    [ObservableProperty]
    private string _processSearch = string.Empty;

    [ObservableProperty]
    private string _processSort = "Memory";

    [ObservableProperty]
    private string _installedSummary = "Reading installed applications...";

    [ObservableProperty]
    private string _storeAppsSummary = "Reading Store/MSIX packages...";

    [ObservableProperty]
    private string _processSummary = "Reading running applications...";

    [ObservableProperty]
    private string _leftoverTargetApp = string.Empty;

    [ObservableProperty]
    private string _leftoverSummary = "Scan for orphaned folders and registry remnants of uninstalled applications.";

    [ObservableProperty]
    private bool _hasLeftoverItems;

    [ObservableProperty]
    private bool _isScanningLeftovers;

    partial void OnAppSearchChanged(string value)
    {
        ApplyInstalledFilter();
        ApplyStoreFilter();
    }

    partial void OnProcessSearchChanged(string value) => ApplyProcessFilter();
    partial void OnProcessSortChanged(string value) => ApplyProcessFilter();

    public override async Task LoadAsync()
    {
        await RefreshProcessesAsync();
        await LoadStartupAsync();
        await LoadInstalledAsync();
        await LoadStoreAppsAsync();
    }

    protected override void OnBusyChanged()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        RefreshStartupCommand.NotifyCanExecuteChanged();
        ScanLeftoversCommand.NotifyCanExecuteChanged();
        CleanLeftoversCommand.NotifyCanExecuteChanged();
        ExportRegBackupCommand.NotifyCanExecuteChanged();
    }

    public static IReadOnlyList<RunningAppViewModel> FilterAndSortProcesses(
        IEnumerable<RunningAppViewModel> apps,
        string? search,
        string? sort)
    {
        IEnumerable<RunningAppViewModel> filtered = apps;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(app =>
                app.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        filtered = sort switch
        {
            "CPU" => filtered.OrderByDescending(app => app.Process.CpuPercent)
                             .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase),
            "Name" => filtered.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderByDescending(app => app.Process.MemoryBytes)
                          .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase),
        };

        return filtered.Take(MaxRows).ToArray();
    }

    private void ApplyProcessFilter()
    {
        VisibleRunningApps.Clear();
        foreach (RunningAppViewModel app in FilterAndSortProcesses(
                     _allRunning,
                     ProcessSearch,
                     ProcessSort))
        {
            VisibleRunningApps.Add(app);
        }
    }

    private async Task RefreshProcessesAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;

        try
        {
            IReadOnlyList<ProcessInfo> all = await _processes.GetProcessesAsync();

            // Only user-facing applications, and never ourselves or Microsoft PC Manager.
            List<ProcessInfo> apps = all
                .Where(p => !p.IsSystem && !AppInfo.IsProtectedProcessName(p.Name) && !string.IsNullOrWhiteSpace(p.Name))
                .OrderByDescending(p => p.MemoryBytes)
                .Take(MaxRows)
                .ToList();

            var running = new List<RunningAppViewModel>();
            foreach (ProcessInfo process in apps)
            {
                running.Add(new RunningAppViewModel(process, EndAsync));
            }
            _allRunning = running;
            RunningApps.Clear();
            foreach (RunningAppViewModel app in running)
            {
                RunningApps.Add(app);
            }
            ApplyProcessFilter();

            ProcessSummary = $"{apps.Count} applications · {apps.Sum(a => a.MemoryBytes) / (1024.0 * 1024.0 * 1024.0):0.00} GB in use";
        }
        catch (Exception ex)
        {
            ProcessSummary = $"Could not read processes: {ex.Message}";
            _logger.Error("Process refresh failed.", ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task EndAsync(RunningAppViewModel app)
    {
        OperationResult result = await _processes.EndProcessAsync(app.ProcessId);
        StatusMessage = result.Message;

        if (result.Success)
        {
            await RefreshProcessesAsync();
        }
    }

    private async Task LoadStartupAsync()
    {
        try
        {
            IReadOnlyList<StartupApp> apps = await _processes.GetStartupAppsAsync();

            StartupApps.Clear();
            foreach (StartupApp app in apps)
            {
                StartupApps.Add(new StartupAppViewModel(app, _processes, message => StatusMessage = message));
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not read startup programs: {ex.Message}";
            _logger.Error("Startup refresh failed.", ex);
        }
    }

    private async Task LoadInstalledAsync()
    {
        try
        {
            _allInstalled = await _installedApps.GetInstalledAppsAsync();
            ApplyInstalledFilter();
        }
        catch (Exception ex)
        {
            InstalledSummary = $"Could not read installed apps: {ex.Message}";
            _logger.Error("Installed apps refresh failed.", ex);
        }
    }

    private async Task LoadStoreAppsAsync()
    {
        try
        {
            _allStore = await _appxPackages.GetPackagesAsync();
            ApplyStoreFilter();
        }
        catch (Exception ex)
        {
            StoreAppsSummary = $"Could not read Store/MSIX packages: {ex.Message}";
            _logger.Error("Store/MSIX package refresh failed.", ex);
        }
    }

    private void ApplyInstalledFilter()
    {
        string query = AppSearch.Trim();

        IEnumerable<InstalledApp> filtered = _allInstalled;

        if (query.Length > 0)
        {
            filtered = filtered.Where(a =>
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        InstalledApps.Clear();
        foreach (InstalledApp app in filtered.Take(200))
        {
            InstalledApps.Add(new InstalledAppViewModel(app, UninstallAsync, a => ScanLeftoversForAppAsync(a)));
        }

        InstalledSummary = $"{_allInstalled.Count} installed · showing {InstalledApps.Count}";
    }

    private void ApplyStoreFilter()
    {
        string query = AppSearch.Trim();

        IEnumerable<AppxPackage> filtered = _allStore;

        if (query.Length > 0)
        {
            filtered = filtered.Where(package =>
                package.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                package.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                package.Version.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        StoreApps.Clear();
        foreach (AppxPackage package in filtered.Take(200))
        {
            StoreApps.Add(new StoreAppViewModel(package, UninstallStoreAsync));
        }

        StoreAppsSummary = $"{_allStore.Count} Store package(s) · showing {StoreApps.Count}";
    }

    private async Task UninstallAsync(InstalledAppViewModel app)
    {
        if (!_confirmation.Confirm(
                "Uninstall application",
                $"Start the uninstaller for {app.Name}?\n\nThe installer controls the rest of the process."))
        {
            return;
        }

        StatusMessage = $"The uninstaller for {app.Name} is starting. Waiting for it to finish...";

        OperationResult result = await _installedApps.UninstallAsync(app.App);
        StatusMessage = result.Message;

        // The uninstaller may have changed the list even when it did not finish cleanly.
        await LoadInstalledAsync();

        if (result.Success)
        {
            await ScanLeftoversAsync(app.Name, app.App.Publisher);
        }
    }

    public async Task ScanLeftoversForAppAsync(InstalledAppViewModel app)
    {
        LeftoverTargetApp = app.Name;
        await ScanLeftoversAsync(app.Name, app.App.Publisher);
    }

    public async Task ScanLeftoversAsync(string appName, string? publisher = null)
    {
        if (_leftoverScanner is null)
        {
            LeftoverSummary = "Leftovers scanner is not available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(appName))
        {
            LeftoverSummary = "Enter an application name to scan for leftovers.";
            return;
        }

        LeftoverTargetApp = appName;
        IsScanningLeftovers = true;
        LeftoverSummary = $"Scanning for orphaned files, folders, and registry keys for '{appName}'...";

        try
        {
            AppLeftoverScanResult result = await _leftoverScanner.ScanLeftoversAsync(appName, publisher);
            LeftoverItems.Clear();
            foreach (AppLeftoverItem item in result.Items)
            {
                LeftoverItems.Add(item);
            }

            HasLeftoverItems = LeftoverItems.Count > 0;
            LeftoverSummary = result.FoundAny
                ? $"Found {result.Items.Count} leftover remnants for '{appName}': {ByteFormatter.Format(result.TotalBytes)} in folders and {result.RegKeyCount} registry key(s)."
                : $"No orphaned remnants or leftovers detected for '{appName}'.";
        }
        catch (Exception ex)
        {
            LeftoverSummary = $"Leftovers scan failed: {ex.Message}";
            _logger.Error("Leftovers scan failed.", ex);
        }
        finally
        {
            IsScanningLeftovers = false;
            CleanLeftoversCommand.NotifyCanExecuteChanged();
            ExportRegBackupCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task CleanLeftoversAsync()
    {
        if (_leftoverScanner is null || LeftoverItems.Count == 0)
        {
            return;
        }

        int count = LeftoverItems.Count(i => i.IsSelected);
        if (count == 0)
        {
            StatusMessage = "No leftover items are selected.";
            return;
        }

        if (!_confirmation.Confirm(
                "Clean App Leftovers",
                $"Clean {count} selected leftover item(s)?\n\nFolders will be moved to the Windows Recycle Bin, and any registry keys will be backed up to a .reg file first."))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Cleaning app leftovers (Recycle Bin & registry backup)...";

        try
        {
            OperationResult result = await _leftoverScanner.CleanLeftoversAsync(LeftoverItems, createRegBackup: true);
            StatusMessage = result.Message;

            // Re-scan
            await ScanLeftoversAsync(LeftoverTargetApp);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Clean leftovers failed: {ex.Message}";
            _logger.Error("Clean leftovers failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportRegBackupAsync()
    {
        if (_leftoverScanner is null)
        {
            return;
        }

        var regKeys = LeftoverItems.Where(i => i.Kind == AppLeftoverKind.RegistryKey).Select(i => i.TargetPath).ToList();
        if (regKeys.Count == 0)
        {
            StatusMessage = "No registry remnants are in the leftovers list.";
            return;
        }

        string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FPM", "Backups");
        string backupFile = Path.Combine(backupDir, $"reg_leftovers_{DateTime.Now:yyyyMMdd_HHmmss}.reg");

        OperationResult result = await _leftoverScanner.ExportRegBackupAsync(regKeys, backupFile);
        StatusMessage = result.Message;
    }

    private async Task UninstallStoreAsync(StoreAppViewModel app)
    {
        if (!app.Package.CanUninstall)
        {
            StatusMessage = "That package is protected or is not removable by this app. Use Windows Settings.";
            return;
        }

        if (!_confirmation.Confirm(
                "Uninstall Store app",
                $"Remove {app.Name} for your current Windows account?\n\n{app.PackageFullName}\n\nThe package must be reinstalled from the Store or its installer."))
        {
            return;
        }

        OperationResult result = await _appxPackages.UninstallAsync(app.Package);
        StatusMessage = result.Message;

        if (result.Success)
        {
            await LoadInstalledAsync();
            await LoadStoreAppsAsync();
        }
    }

    public void Dispose() => _timer.Stop();
}
