using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Shell: the six-entry navigation rail (Settings pinned at the bottom), the hosted page
/// and the theme. Navigation from a page goes through <see cref="NavigationService"/> so
/// the quick-action cards never reference the shell.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly HomeViewModel _home;
    private readonly ProtectionViewModel _protection;
    private readonly StorageViewModel _storage;
    private readonly AppsViewModel _apps;
    private readonly MemoryViewModel _memory;
    private readonly DebloatViewModel _debloat;
    private readonly SettingsViewModel _settingsPage;

    public MainViewModel(
        HomeViewModel home,
        ProtectionViewModel protection,
        StorageViewModel storage,
        AppsViewModel apps,
        ToolboxViewModel toolbox,
        MemoryViewModel memory,
        DebloatViewModel debloat,
        SettingsViewModel settingsPage,
        NavigationService navigation,
        ISettingsService settings,
        IThemeService themeService)
    {
        _themeService = themeService;
        _home = home;
        _protection = protection;
        _storage = storage;
        _apps = apps;
        _memory = memory;
        _debloat = debloat;
        _settingsPage = settingsPage;

        Pages = new ObservableCollection<NavigationItem>
        {
            new(PageKeys.Home, "Home", "Boost and health", "Home", home),
            new(PageKeys.Memory, "Memory", "RAM and process relief", "Memory", memory),
            new(PageKeys.Protection, "Protection", "Security and updates", "Protection", protection),
            new(PageKeys.Storage, "Storage", "Cleanup and disks", "Storage", storage),
            new(PageKeys.Apps, "Apps", "Processes and startup", "Apps", apps),
            new(PageKeys.Toolbox, "Toolbox", "Everyday utilities", "Toolbar", toolbox),
            new(PageKeys.Debloat, "Debloat", "Windows tweaks and apps", "Debloat", debloat)
        };

        // Settings is pinned under the rail separator, so it lives in its own one-item list.
        SettingsItems = new ObservableCollection<NavigationItem>
        {
            new(PageKeys.Settings, "Settings", "Preferences and about", "Settings", settingsPage)
        };

        navigation.Handler = SelectPage;
        _themeService.Apply(settings.Current.Theme);

        _currentPage = Pages[0].Page;
        _selectedPage = Pages[0];

        ToggleCommandPaletteCommand = new RelayCommand(ToggleCommandPalette);
        CloseCommandPaletteCommand = new RelayCommand(() => IsCommandPaletteOpen = false);
        ExecutePaletteCommand = new RelayCommand<CommandPaletteEntry>(ExecuteCommand);
        GoBackCommand = new RelayCommand(GoBack, () => _navigationHistory.CanGoBack);
        GoForwardCommand = new RelayCommand(GoForward, () => _navigationHistory.CanGoForward);
        RefreshCurrentCommand = new AsyncRelayCommand(RefreshCurrentAsync);

        BuildCommandPalette();
    }

    public ObservableCollection<NavigationItem> Pages { get; }

    public ObservableCollection<NavigationItem> SettingsItems { get; }

    public ObservableCollection<CommandPaletteEntry> FilteredCommands { get; } = new();
    private readonly List<CommandPaletteEntry> _allCommands = new();

    public IRelayCommand ToggleCommandPaletteCommand { get; }
    public IRelayCommand CloseCommandPaletteCommand { get; }
    public IRelayCommand<CommandPaletteEntry> ExecutePaletteCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IRelayCommand GoForwardCommand { get; }
    public IRelayCommand RefreshCurrentCommand { get; }

    private readonly NavigationHistory<NavigationItem> _navigationHistory = new();
    private bool _isApplyingHistory;

    [ObservableProperty]
    private bool _isCommandPaletteOpen;

    [ObservableProperty]
    private string _commandPaletteQuery = string.Empty;

    partial void OnCommandPaletteQueryChanged(string value)
    {
        FilterCommands();
    }

    private void ToggleCommandPalette()
    {
        IsCommandPaletteOpen = !IsCommandPaletteOpen;
        if (IsCommandPaletteOpen)
        {
            CommandPaletteQuery = string.Empty;
            FilterCommands();
        }
    }

    private void ExecuteCommand(CommandPaletteEntry? entry)
    {
        if (entry is null) return;
        IsCommandPaletteOpen = false;
        entry.Action.Invoke();
    }

    private void FilterCommands()
    {
        FilteredCommands.Clear();
        string q = CommandPaletteQuery.Trim();
        var matches = string.IsNullOrWhiteSpace(q)
            ? _allCommands
            : _allCommands.Where(c => c.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                      c.Category.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (CommandPaletteEntry item in matches)
        {
            FilteredCommands.Add(item);
        }
    }

    private void BuildCommandPalette()
    {
        _allCommands.Clear();

        _allCommands.Add(new CommandPaletteEntry("Navigate to Home", "Navigation", "Ctrl+1", () => SelectPage(PageKeys.Home)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Memory", "Navigation", "Ctrl+2", () => SelectPage(PageKeys.Memory)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Protection", "Navigation", "Ctrl+3", () => SelectPage(PageKeys.Protection)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Storage", "Navigation", "Ctrl+4", () => SelectPage(PageKeys.Storage)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Apps", "Navigation", "Ctrl+5", () => SelectPage(PageKeys.Apps)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Toolbox", "Navigation", "Ctrl+6", () => SelectPage(PageKeys.Toolbox)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Debloat", "Navigation", "Ctrl+7", () => SelectPage(PageKeys.Debloat)));
        _allCommands.Add(new CommandPaletteEntry("Navigate to Settings", "Navigation", "Ctrl+,", () => SelectPage(PageKeys.Settings)));

        // Actions execute the same command the page's button runs; page-bound commands also
        // navigate first so their progress and result are visible.
        _allCommands.Add(new CommandPaletteEntry("Boost memory now", "Action", string.Empty, () => Invoke(_home.BoostCommand)));
        _allCommands.Add(new CommandPaletteEntry("Capture every screen", "Action", string.Empty, () => Invoke(_home.CaptureCommand)));
        _allCommands.Add(new CommandPaletteEntry("One-Click Cleanup scan", "Action", string.Empty, () => RunOn(PageKeys.Storage, _storage.ScanCleanupCommand)));
        _allCommands.Add(new CommandPaletteEntry("Analyze Component Store (DISM)", "Action", string.Empty, () => RunOn(PageKeys.Storage, _storage.AnalyzeDismCommand)));
        _allCommands.Add(new CommandPaletteEntry("Scan Downloads folder", "Action", string.Empty, () => RunOn(PageKeys.Storage, _storage.ScanDownloadsCommand)));
        _allCommands.Add(new CommandPaletteEntry("Find duplicate files", "Action", string.Empty, () => RunOn(PageKeys.Storage, _storage.FindDuplicatesCommand)));
        _allCommands.Add(new CommandPaletteEntry("Windows Defender quick scan", "Action", string.Empty, () => RunOn(PageKeys.Protection, _protection.StartQuickScanCommand)));
        _allCommands.Add(new CommandPaletteEntry("Flush DNS resolver cache", "Action", string.Empty, () => RunOn(PageKeys.Protection, _protection.FlushDnsCommand)));
        _allCommands.Add(new CommandPaletteEntry("Network diagnostics and latency test", "Action", string.Empty, () => RunOn(PageKeys.Protection, _protection.RunNetworkDiagnosticsCommand)));
        _allCommands.Add(new CommandPaletteEntry("Check for Windows updates", "Action", string.Empty, () => RunOn(PageKeys.Protection, _protection.CheckUpdatesCommand)));
        _allCommands.Add(new CommandPaletteEntry("Scan open windows (pop-ups)", "Action", string.Empty, () => RunOn(PageKeys.Protection, _protection.ScanPopupsCommand)));
        _allCommands.Add(new CommandPaletteEntry("Remove FPM hosts block", "Action", string.Empty, () => RunOn(PageKeys.Protection, _protection.RemoveHostsBlockCommand)));
        _allCommands.Add(new CommandPaletteEntry("Refresh process list", "Action", string.Empty, () => RunOn(PageKeys.Apps, _apps.RefreshCommand)));
        _allCommands.Add(new CommandPaletteEntry("Scan app leftovers", "Action", string.Empty, () => RunOn(PageKeys.Apps, _apps.ScanLeftoversCommand)));
        _allCommands.Add(new CommandPaletteEntry("Refresh memory diagnostics", "Action", string.Empty, () => RunOn(PageKeys.Memory, _memory.RefreshCommand)));
        _allCommands.Add(new CommandPaletteEntry("Run readiness check", "Action", string.Empty, () => RunOn(PageKeys.Settings, _settingsPage.RunReadinessCheckCommand)));
        _allCommands.Add(new CommandPaletteEntry("Review selected Windows debloat tweaks", "Action", string.Empty, () => RunOn(PageKeys.Debloat, _debloat.ReviewCommand)));

        FilterCommands();
    }

    private static void Invoke(IRelayCommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>
    /// Navigates to a page and then runs its command. Navigation kicks off the page's refresh,
    /// and most page commands are gated on "not busy", so wait for the guard instead of
    /// silently dropping the action.
    /// </summary>
    private async void RunOn(string pageKey, IRelayCommand command)
    {
        try
        {
            SelectPage(pageKey);

            for (int attempt = 0; attempt < 20 && !command.CanExecute(null); attempt++)
            {
                await Task.Delay(150);
            }

            Invoke(command);
        }
        catch (Exception)
        {
            // A palette action must never take the shell down; the page reports its own errors.
        }
    }

    public string ProductName => AppInfo.ProductName;

    public string VersionText => $"Version {AppInfo.Version}";

    [ObservableProperty]
    private bool _isExpandedSidebar;

    [ObservableProperty]
    private double _currentWindowWidth = 560;

    public void UpdateResponsiveLayout(double width)
    {
        CurrentWindowWidth = width;
        IsExpandedSidebar = width >= 960;
    }

    private ViewModelBase _currentPage;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    private NavigationItem _selectedPage;
    public NavigationItem SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value is null || ReferenceEquals(value, _selectedPage))
            {
                return;
            }

            if (!_isApplyingHistory)
            {
                _navigationHistory.Push(_selectedPage);
                GoBackCommand.NotifyCanExecuteChanged();
                GoForwardCommand.NotifyCanExecuteChanged();
            }

            SetProperty(ref _selectedPage, value);
            CurrentPage = value.Page;

            // First visit loads immediately; revisits refresh after the 260 ms transition so
            // data work never lands inside the incoming animation (U6).
            _ = value.Page.HasLoadedOnce
                ? ReloadAfterTransitionAsync(value.Page)
                : value.Page.EnsureLoadedAsync();
        }
    }

    public void GoBack()
    {
        if (!_navigationHistory.TryBack(_selectedPage, out NavigationItem? previous) ||
            previous is null)
        {
            return;
        }
        _isApplyingHistory = true;
        try
        {
            SelectedPage = previous;
        }
        finally
        {
            _isApplyingHistory = false;
        }

        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    public void GoForward()
    {
        if (!_navigationHistory.TryForward(_selectedPage, out NavigationItem? next) ||
            next is null)
        {
            return;
        }
        _isApplyingHistory = true;
        try
        {
            SelectedPage = next;
        }
        finally
        {
            _isApplyingHistory = false;
        }

        GoBackCommand.NotifyCanExecuteChanged();
        GoForwardCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshCurrentAsync()
    {
        await CurrentPage.LoadAsync();
    }

    /// <summary>Called by the window once it is on screen.</summary>
    public Task LoadAsync() => CurrentPage.EnsureLoadedAsync();

    private static async Task ReloadAfterTransitionAsync(ViewModelBase page)
    {
        await Task.Delay(260);
        await page.LoadAsync();
    }

    private void SelectPage(string pageKey)
    {
        NavigationItem? target = Pages.Concat(SettingsItems).FirstOrDefault(i => i.Key == pageKey);

        if (target is not null)
        {
            SelectedPage = target;
        }
    }
}
