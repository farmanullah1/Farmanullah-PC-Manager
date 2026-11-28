using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Android;
using PCManager.Services.Abstractions;
using PCManager.Services.Android;

namespace PCManager.ViewModels;

public sealed partial class AndroidPackageViewModel : ObservableObject
{
    private readonly IPackageEngine _engine;
    private readonly IUserConfirmationService _confirmation;
    private readonly ISettingsService _settings;
    private readonly IAndroidBackupService _backup;
    private readonly IPackageListProvider _lists;
    private AdbDevice? _device;
    private PackageScan? _scan;
    private IReadOnlyList<PackageActionResult> _lastApplied =
        Array.Empty<PackageActionResult>();

    public AndroidPackageViewModel(
        IPackageEngine engine,
        IUserConfirmationService confirmation,
        ISettingsService settings,
        IAndroidBackupService backup,
        IPackageListProvider lists)
    {
        _engine = engine;
        _confirmation = confirmation;
        _settings = settings;
        _backup = backup;
        _lists = lists;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => CanRefresh);
        ReviewCommand = new RelayCommand(Review, () => HasSelection);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => HasSelection && IsReviewOpen);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => _lastApplied.Count > 0);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearCommand = new RelayCommand(Clear);
        InvertCommand = new RelayCommand(Invert);
        AcknowledgeSafetyCommand = new RelayCommand(() =>
        {
            SafetyAcknowledged = true;
        });
        RecommendedPresetCommand = new RelayCommand(RecommendedPreset);
        ExportTextCommand = new RelayCommand(ExportText);
        ExportJsonCommand = new RelayCommand(ExportJson);
        ImportSelectionCommand = new RelayCommand(ImportSelection);
        CreateSnapshotCommand = new AsyncRelayCommand(CreateSnapshotAsync,
            () => _device is { IsUsable: true } && SelectedUser is not null && _scan is not null);
        RestoreSnapshotCommand = new AsyncRelayCommand(RestoreSnapshotAsync,
            () => SelectedSnapshot is not null);
        OpenLogCommand = new RelayCommand(OpenLog);

        SafetyAcknowledged = settings.Current.AndroidSafetyAcknowledged;
        RefreshSnapshots();
    }

    public ObservableCollection<AdbUser> Users { get; } = new();
    public ObservableCollection<AndroidPackageRow> Packages { get; } = new();
    public ObservableCollection<AndroidPackageRow> VisiblePackages { get; } = new();
    public ObservableCollection<string> ListFilters { get; } =
        new(new[] { "All" }.Concat(Enum.GetNames(typeof(UadListKind))));
    public ObservableCollection<string> RemovalFilters { get; } =
        new(new[] { "All", "Recommended", "Advanced", "Expert", "Unsafe", "Unlisted" });
    public ObservableCollection<string> StateFilters { get; } =
        new(new[] { "All", "Enabled", "Disabled", "Uninstalled" });
    public ObservableCollection<AndroidPackageSnapshot> Snapshots { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ReviewCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand UndoCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand InvertCommand { get; }
    public RelayCommand AcknowledgeSafetyCommand { get; }
    public RelayCommand RecommendedPresetCommand { get; }
    public RelayCommand ExportTextCommand { get; }
    public RelayCommand ExportJsonCommand { get; }
    public RelayCommand ImportSelectionCommand { get; }
    public AsyncRelayCommand CreateSnapshotCommand { get; }
    public AsyncRelayCommand RestoreSnapshotCommand { get; }
    public RelayCommand OpenLogCommand { get; }

    [ObservableProperty] private AdbUser? _selectedUser;
    [ObservableProperty] private AndroidPackageRow? _focusedPackage;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedList = "All";
    [ObservableProperty] private string _selectedRemoval = "All";
    [ObservableProperty] private string _selectedState = "All";
    [ObservableProperty] private bool _selectedOnly;
    [ObservableProperty] private bool _expertMode;
    [ObservableProperty] private string _expertConfirmation = string.Empty;
    [ObservableProperty] private bool _disableInsteadOfUninstall;
    [ObservableProperty] private bool _safetyAcknowledged;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isReviewOpen;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _statusText = "Packages unavailable.";
    [ObservableProperty] private string _reviewText = "Select packages, then review.";
    [ObservableProperty] private string _resultText = "No package actions have run.";
    [ObservableProperty] private AndroidPackageSnapshot? _selectedSnapshot;

    private bool CanRefresh =>
        SafetyAcknowledged && _device is { IsUsable: true } &&
        SelectedUser is not null && !IsBusy;

    public void SetDevice(AdbDevice? device, IReadOnlyList<AdbUser> users)
    {
        _device = device;
        Users.Clear();
        foreach (AdbUser user in users)
        {
            Users.Add(user);
        }

        SelectedUser = users.FirstOrDefault(user => user.IsRunning)
            ?? users.FirstOrDefault();

        Packages.Clear();
        VisiblePackages.Clear();
        _scan = null;
        HasSelection = false;
        StatusText = device is null
            ? "Packages unavailable until a device is authorized."
            : "Device selected. Refresh packages to scan.";
        RefreshCommands();
    }

    partial void OnSafetyAcknowledgedChanged(bool value)
    {
        _settings.Current.AndroidSafetyAcknowledged = value;
        _settings.Save();
        RefreshCommands();
    }

    partial void OnSelectedUserChanged(AdbUser? value)
    {
        if (value is not null)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedListChanged(string value) => ApplyFilters();
    partial void OnSelectedRemovalChanged(string value) => ApplyFilters();
    partial void OnSelectedStateChanged(string value) => ApplyFilters();
    partial void OnSelectedOnlyChanged(bool value) => ApplyFilters();

    private async Task RefreshAsync()
    {
        if (!CanRefresh)
        {
            return;
        }

        IsBusy = true;
        IsReviewOpen = false;
        StatusText = "Scanning Android packages…";

        try
        {
            PackageScanResult result = await _engine.ScanAsync(
                _device!,
                SelectedUser!.Id);

            if (result.Status != PackageScanStatus.Ready || result.Scan is null)
            {
                StatusText = result.Message;
                Packages.Clear();
                VisiblePackages.Clear();
                return;
            }

            _scan = result.Scan;
            Packages.Clear();
            foreach (AndroidPackage package in result.Scan.Packages)
            {
                var row = new AndroidPackageRow(package);
                row.SelectionChanged += UpdateSelection;
                Packages.Add(row);
            }

            ApplyFilters();
            StatusText =
                $"{result.Scan.Packages.Count} package(s) · " +
                $"{result.Scan.EnabledCount} enabled · " +
                $"{result.Scan.DisabledCount} disabled · " +
                $"{result.Scan.UninstalledCount} uninstalled.";
        }
        catch (Exception ex)
        {
            StatusText = "Package scan failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private void ApplyFilters()
    {
        VisiblePackages.Clear();
        foreach (AndroidPackageRow row in Packages)
        {
            bool matches =
                (SelectedList == "All" ||
                 row.ListText.Equals(SelectedList, StringComparison.OrdinalIgnoreCase)) &&
                (SelectedRemoval == "All" ||
                 row.RemovalText.Equals(SelectedRemoval, StringComparison.OrdinalIgnoreCase)) &&
                (SelectedState == "All" ||
                 row.StateText.Equals(SelectedState, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(SearchText) ||
                 row.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                 row.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) &&
                (!SelectedOnly || row.IsSelected);
            if (matches)
            {
                VisiblePackages.Add(row);
            }
        }
        UpdateSelection();
    }

    private void Review()
    {
        AndroidPackageRow[] selected =
            Packages.Where(row => row.IsSelected).ToArray();
        if (selected.Length == 0)
        {
            ReviewText = "Nothing selected.";
            IsReviewOpen = false;
            return;
        }

        IReadOnlyList<PackageDependencyWarning> warnings =
            PackageDependencyWarnings.Build(selected.Select(row => row.Package));
        int protectedCount = selected.Count(row => row.Package.IsDenylisted());
        ReviewText =
            $"{selected.Length} package(s) selected · " +
            $"{protectedCount} protected/Unsafe block(s).";
        if (warnings.Count > 0)
        {
            ReviewText += "\n" +
                string.Join("\n", warnings.Select(warning => warning.Message));
        }
        IsReviewOpen = true;
    }

    private async Task ApplyAsync()
    {
        if (!SafetyAcknowledged || _device is null || SelectedUser is null)
        {
            return;
        }

        AndroidPackageRow[] selected =
            Packages.Where(row => row.IsSelected).ToArray();
        AndroidPackageRow[] blocked = selected
            .Where(row => row.Package.IsDenylisted())
            .Where(row => !ExpertMode ||
                          !string.Equals(
                              ExpertConfirmation,
                              row.Id,
                              StringComparison.Ordinal))
            .ToArray();

        if (blocked.Length > 0)
        {
            ReviewText =
                "Blocked: protected or Unsafe packages require Expert mode " +
                "and the exact package id.";
            return;
        }

        IReadOnlyList<PackageActionRequest> requests = selected
            .Select(row => new PackageActionRequest(
                row.Id,
                row.Package.State switch
                {
                    PackageState.Uninstalled => PackageAction.Restore,
                    PackageState.Enabled => PackageAction.Uninstall,
                    PackageState.Disabled => PackageAction.Enable,
                    _ => PackageAction.Enable,
                }))
            .ToArray();

        if (!_confirmation.Confirm(
                "Apply Android package changes",
                $"Apply {requests.Count} package action(s) to " +
                $"{_device.DisplayName}, user {SelectedUser.Name}?\n\n" +
                "FPM does not back up Android data. Removing OEM or system packages can make the device unstable."))
        {
            ResultText = "Batch cancelled. No package commands ran.";
            return;
        }

        IsBusy = true;
        IsReviewOpen = false;
        ResultText = "Applying package actions…";
        try
        {
            AdbDevice device = _device;
            if (_scan is not null)
            {
                await _backup.CreateSnapshotAsync(
                    device,
                    SelectedUser,
                    _scan);
            }

            PackageBatchResult result = await _engine.ApplyAsync(
                device,
                SelectedUser.Id,
                requests,
                new PackageApplyOptions(
                    DisableInsteadOfUninstall,
                    ExpertMode,
                    ExpertConfirmation));
            _lastApplied = result.Results;
            await _backup.AppendLogAsync(
                device,
                SelectedUser,
                result.Results);
            ResultText =
                $"{result.Message}\n{result.OkCount} applied · " +
                $"{result.AlreadyCount} already · {result.FailedCount} failed · " +
                $"{result.BlockedCount} blocked · verified: " +
                (result.Verified ? "yes" : "no");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ResultText = "Package batch failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private async Task UndoAsync()
    {
        if (_lastApplied.Count == 0 || _device is null || SelectedUser is null)
        {
            return;
        }

        IReadOnlyList<PackageActionRequest> inverse = _lastApplied
            .Where(result => result.Status == PackageResultStatus.Ok)
            .Select(result => new PackageActionRequest(
                result.PackageId,
                result.Before switch
                {
                    PackageState.Uninstalled => PackageAction.Restore,
                    PackageState.Disabled => PackageAction.Disable,
                    PackageState.Enabled => PackageAction.Enable,
                    _ => PackageAction.Enable,
                }))
            .ToArray();
        if (inverse.Count == 0)
        {
            ResultText = "Nothing in the last batch can be undone.";
            return;
        }
        if (!_confirmation.Confirm(
                "Undo Android package batch",
                $"Undo {inverse.Count} successful package action(s)?"))
        {
            ResultText = "Undo cancelled.";
            return;
        }

        PackageBatchResult result = await _engine.ApplyAsync(
            _device,
            SelectedUser.Id,
            inverse,
            new PackageApplyOptions(
                ExpertMode: ExpertMode,
                ExpertConfirmation: ExpertConfirmation));
        ResultText = "Undo: " + result.Message;
        await RefreshAsync();
    }

    private void RefreshCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ReviewCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
    }
}
