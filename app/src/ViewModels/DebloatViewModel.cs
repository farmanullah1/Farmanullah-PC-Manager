using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core.Debloat;
using PCManager.Services.Debloat;

namespace PCManager.ViewModels;

/// <summary>
/// D3 Windows/Apps debloat page. Selection, presets, review, detect, apply and undo all go
/// through the catalog planner and the journaled engine; no command is built by the UI.
/// </summary>
public sealed partial class DebloatViewModel : ViewModelBase
{
    private readonly DebloatEngine _engine;
    private readonly AndroidDebloatViewModel _android;
    private readonly List<DebloatTweakRow> _allRows = new();
    private readonly HashSet<string> _appliedIds = new(StringComparer.Ordinal);
    private readonly int _osBuild = Environment.OSVersion.Version.Build;
    private readonly bool _isWindows11 = Environment.OSVersion.Version.Build >= 22000;
    private DebloatCatalog? _catalog;
    private List<DebloatApp> _allApps = new();
    private bool _applyingPreset;
    private bool _pageVisible;

    public DebloatViewModel(
        DebloatEngine engine,
        AndroidDebloatViewModel android,
        IDebloatProfileService? profiles = null)
    {
        _engine = engine;
        _android = android;

        ShowWindowsCommand = new RelayCommand(() =>
        {
            IsWindowsTab = true;
            IsAppsTab = false;
            IsAndroidTab = false;
        });
        ShowAppsCommand = new RelayCommand(() =>
        {
            IsWindowsTab = false;
            IsAppsTab = true;
            IsAndroidTab = false;
        });
        ShowAndroidCommand = new RelayCommand(() =>
        {
            IsWindowsTab = false;
            IsAppsTab = false;
            IsAndroidTab = true;
        });
        SelectAllVisibleCommand = new RelayCommand(SelectAllVisible, () => !IsBusy);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => !IsBusy);
        ReviewCommand = new RelayCommand(OpenReview, () => HasSelection && !IsBusy);
        CloseReviewCommand = new RelayCommand(() => IsReviewOpen = false);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, () => HasSelection && !IsBusy);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => !IsBusy && CanUndo);
        DetectCommand = new AsyncRelayCommand(DetectAsync, () => HasSelection && !IsBusy);
        InitializeAdvanced(profiles);
    }

    public ObservableCollection<DebloatCategoryRow> Categories { get; } = new();
    public ObservableCollection<DebloatTweakRow> Tweaks { get; } = new();
    public ObservableCollection<DebloatPlanRow> PlanRows { get; } = new();
    public ObservableCollection<string> ReviewItems { get; } = new();
    public ObservableCollection<DebloatPreset> Presets { get; } = new(DebloatPresetCatalog.All);
    public ObservableCollection<DebloatAppRow> Apps { get; } = new();
    public ObservableCollection<string> HistoryRows { get; } = new();
    public ObservableCollection<string> AppFilters { get; } = new() { "All", "Recommended safe", "Optional", "Unsafe" };

    public AndroidDebloatViewModel Android => _android;

    public RelayCommand ShowWindowsCommand { get; }
    public RelayCommand ShowAppsCommand { get; }
    public RelayCommand ShowAndroidCommand { get; }
    public RelayCommand SelectAllVisibleCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand ReviewCommand { get; }
    public RelayCommand CloseReviewCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand UndoCommand { get; }
    public AsyncRelayCommand DetectCommand { get; }

    [ObservableProperty]
    private DebloatCategoryRow? _selectedCategory;

    [ObservableProperty]
    private DebloatPreset? _selectedPreset;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _appSearchText = string.Empty;

    [ObservableProperty]
    private string _selectedAppFilter = "All";

    [ObservableProperty]
    private bool _isWindowsTab = true;

    [ObservableProperty]
    private bool _isAppsTab;

    [ObservableProperty]
    private bool _isAndroidTab;

    [ObservableProperty]
    private bool _createRestorePoint = true;

    [ObservableProperty]
    private string _osText = "Windows";

    [ObservableProperty]
    private string _catalogStatus = "Catalog not loaded.";

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private string _planSummary = "Plan: no tweaks selected.";

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isReviewOpen;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _runningText = string.Empty;

    [ObservableProperty]
    private bool _isReportVisible;

    [ObservableProperty]
    private string _reportHeadline = string.Empty;

    [ObservableProperty]
    private string _reportDetail = string.Empty;

    [ObservableProperty]
    private bool _reportIsFailure;

    [ObservableProperty]
    private bool _reportRolledBack;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private string _historyText = "No debloat runs recorded yet.";

    [ObservableProperty]
    private bool _historyCanUndo;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorText = string.Empty;

    public string AppsSummary => $"{_allApps.Count} catalog app(s) · live installed detection is not wired yet";

    public override Task LoadAsync()
    {
        if (_catalog is not null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _catalog = DebloatCatalogLoader.LoadEmbedded();
            OsText = $"Windows {(_isWindows11 ? "11" : "10")} · build {_osBuild}";
            CatalogStatus = $"{_catalog.Tweaks.Count} tweaks · {_catalog.Apps.Count} apps · catalog {_catalog.Version}";

            Categories.Add(new DebloatCategoryRow("All tweaks", _catalog.Tweaks.Count, isAll: true));
            foreach (DebloatCategory category in _catalog.Categories)
            {
                Categories.Add(new DebloatCategoryRow(
                    category.Name,
                    _catalog.Tweaks.Count(tweak => tweak.Category == category.Name)));
            }

            _allApps = _catalog.Apps.OrderBy(app => app.Title).ToList();
            RebuildApps();

            SelectedCategory = Categories[0];
            RebuildTweaks();
            SelectPreset("recommended");
            RefreshHistory();
            LoadTargets();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorText = $"The embedded debloat catalog could not be loaded: {ex.Message}";
            CatalogStatus = "No catalog";
            StatusMessage = ErrorText;
        }

        return Task.CompletedTask;
    }

    partial void OnSelectedCategoryChanged(DebloatCategoryRow? value) => RebuildTweaks();

    partial void OnSearchTextChanged(string value) => RebuildTweaks();

    partial void OnAppSearchTextChanged(string value) => RebuildApps();

    partial void OnSelectedAppFilterChanged(string value) => RebuildApps();

    partial void OnIsAndroidTabChanged(bool value) =>
        _android.SetVisible(_pageVisible && value);

    public void SetPageVisible(bool visible)
    {
        _pageVisible = visible;
        _android.SetVisible(visible && IsAndroidTab);
    }

    partial void OnSelectedPresetChanged(DebloatPreset? value)
    {
        if (_applyingPreset || value is null || value.Id == DebloatPresetCatalog.CustomId)
        {
            return;
        }

        SelectPreset(value.Id);
    }

    private void SelectPreset(string presetId)
    {
        if (_catalog is null)
        {
            return;
        }

        var selected = DebloatPresetCatalog.Select(_catalog, presetId).ToHashSet(StringComparer.Ordinal);

        _applyingPreset = true;
        foreach (DebloatTweakRow row in _allRows)
        {
            row.IsSelected = selected.Contains(row.Tweak.Id);
        }

        SelectedPreset = Presets.FirstOrDefault(preset => preset.Id == presetId);
        _applyingPreset = false;
        RecomputePlan();
    }

    private void RebuildTweaks()
    {
        if (_catalog is null)
        {
            return;
        }

        IReadOnlyList<DebloatTweak> filtered = DebloatPageCatalog.Filter(
            _catalog,
            SelectedCategory is { IsAll: false } category ? category.Name : null,
            SearchText);

        Tweaks.Clear();

        foreach (DebloatTweak tweak in filtered)
        {
            DebloatTweakRow? existing = _allRows.FirstOrDefault(row => row.Tweak.Id == tweak.Id);

            if (existing is null)
            {
                DebloatPlanItem planItem = _engine
                    .Plan(new[] { tweak.Id }, _osBuild, _isWindows11)
                    .Items[0];

                existing = new DebloatTweakRow(tweak, planItem.IsRunnable, planItem.Reason);
                existing.IsApplied = _appliedIds.Contains(tweak.Id);
                existing.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(DebloatTweakRow.IsSelected))
                    {
                        if (!_applyingPreset)
                        {
                            SelectedPreset = Presets.First(preset => preset.Id == DebloatPresetCatalog.CustomId);
                        }

                        RecomputePlan();
                    }
                };
                _allRows.Add(existing);
            }

            Tweaks.Add(existing);
        }
    }

    private void RecomputePlan()
    {
        if (_catalog is null)
        {
            return;
        }

        List<string> ids = _allRows
            .Where(row => row.IsSelected && row.IsSelectable)
            .Select(row => row.Tweak.Id)
            .ToList();

        DebloatPlan plan = _engine.Plan(ids, _osBuild, _isWindows11);

        PlanRows.Clear();
        foreach (DebloatPlanItem item in plan.Items)
        {
            PlanRows.Add(new DebloatPlanRow(item));
        }

        SelectedCount = ids.Count;
        HasSelection = ids.Count > 0;
        PlanSummary = "Plan: " + DebloatPageCatalog.BuildImpactSummary(plan);
        StatusMessage = HasSelection
            ? $"{ids.Count} tweak(s) selected."
            : "Select at least one tweak, or choose a preset.";

        if (!HasSelection)
        {
            IsReviewOpen = false;
        }

        RefreshCommandStates();
    }

    private void SelectAllVisible()
    {
        _applyingPreset = true;
        foreach (DebloatTweakRow row in Tweaks.Where(row => row.IsSelectable))
        {
            row.IsSelected = true;
        }

        _applyingPreset = false;
        SelectedPreset = Presets.First(preset => preset.Id == DebloatPresetCatalog.CustomId);
        RecomputePlan();
    }

    private void ClearSelection()
    {
        _applyingPreset = true;
        foreach (DebloatTweakRow row in _allRows)
        {
            row.IsSelected = false;
        }

        _applyingPreset = false;
        SelectedPreset = Presets.First(preset => preset.Id == DebloatPresetCatalog.CustomId);
        RecomputePlan();
    }

    private void OpenReview()
    {
        ReviewItems.Clear();

        foreach (DebloatTweakRow row in _allRows.Where(row => row.IsSelected && row.IsSelectable))
        {
            ReviewItems.Add($"{row.Title} · {row.RiskText}");
            ReviewItems.Add(DebloatPageCatalog.DescribeActions(row.Tweak));
        }

        IsReviewOpen = ReviewItems.Count > 0;
        StatusMessage = IsReviewOpen
            ? "Review the typed changes, then apply. The helper captures the current values first."
            : "Nothing selected can be applied.";
    }

    private void RebuildApps()
    {
        string query = AppSearchText.Trim();
        IEnumerable<DebloatApp> apps = _allApps;

        if (SelectedAppFilter switch
        {
            "Recommended safe" => "safe",
            "Optional" => "optional",
            "Unsafe" => "unsafe",
            _ => null,
        } is { } recommendation)
        {
            apps = apps.Where(app => app.Recommendation == recommendation);
        }

        if (query.Length > 0)
        {
            apps = apps.Where(app =>
                app.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                app.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        Apps.Clear();
        foreach (DebloatApp app in apps)
        {
            var row = new DebloatAppRow(app) { IsSelected = _selectedAppIds.Contains(app.Id) };
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(DebloatAppRow.IsSelected))
                {
                    if (row.IsSelected)
                    {
                        _selectedAppIds.Add(app.Id);
                    }
                    else
                    {
                        _selectedAppIds.Remove(app.Id);
                    }
                }
            };
            Apps.Add(row);
        }
    }

    private void RefreshCommandStates()
    {
        SelectAllVisibleCommand.NotifyCanExecuteChanged();
        ClearSelectionCommand.NotifyCanExecuteChanged();
        ReviewCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        DetectCommand.NotifyCanExecuteChanged();
        UndoCommand.NotifyCanExecuteChanged();
        DriftCheckCommand.NotifyCanExecuteChanged();
    }

    protected override void OnBusyChanged() => RefreshCommandStates();
}
