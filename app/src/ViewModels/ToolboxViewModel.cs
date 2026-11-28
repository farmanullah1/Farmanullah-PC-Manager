using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.ViewModels;

/// <summary>
/// Toolbox: the floating-toolbar switch and the tool grid (screenshot, recorder,
/// calculator, notepad, DNS flush, quick links).
/// </summary>
public sealed class ToolboxViewModel : ViewModelBase
{
    private readonly IToolboxService _toolbox;
    private readonly ISettingsService _settings;
    private readonly IFloatingToolbarHost _toolbarHost;
    private readonly IAppLogger _logger;
    private readonly IToolboxLayoutService _layout;

    public ToolboxViewModel(
        IToolboxService toolbox,
        ISettingsService settings,
        IFloatingToolbarHost toolbarHost,
        IAppLogger logger,
        IToolboxLayoutService layout)
    {
        _toolbox = toolbox;
        _settings = settings;
        _toolbarHost = toolbarHost;
        _logger = logger;
        _layout = layout;
        _layout.Changed += (_, _) => RefreshLayout();

        Tools = new ObservableCollection<ToolboxTool>(_toolbox.GetTools());
        LayoutItems = new ObservableCollection<ToolboxLayoutEntry>();
        LaunchToolCommand = new AsyncRelayCommand<string>(LaunchAsync, _ => !IsBusy);
        MoveLayoutUpCommand = new RelayCommand<string>(id =>
        {
            if (!string.IsNullOrEmpty(id)) _layout.Move(id, -1);
        });
        MoveLayoutDownCommand = new RelayCommand<string>(id =>
        {
            if (!string.IsNullOrEmpty(id)) _layout.Move(id, 1);
        });
        ToggleLayoutCommand = new RelayCommand<ToolboxLayoutEntry>(entry =>
        {
            if (entry is not null) _layout.SetEnabled(entry.Id, entry.Enabled);
        });
        ResetLayoutCommand = new RelayCommand(() =>
        {
            _layout.Reset();
            StatusMessage = "Toolbox layout reset to defaults.";
        });
        ExportLayoutCommand = new AsyncRelayCommand(ExportLayoutAsync);
        ImportLayoutCommand = new AsyncRelayCommand(ImportLayoutAsync);
        StatusMessage = "Pick a tool to run it.";
        RefreshLayout();
    }

    public ObservableCollection<ToolboxTool> Tools { get; }
    public ObservableCollection<ToolboxLayoutEntry> LayoutItems { get; }

    public AsyncRelayCommand<string> LaunchToolCommand { get; }
    public RelayCommand<string> MoveLayoutUpCommand { get; }
    public RelayCommand<string> MoveLayoutDownCommand { get; }
    public RelayCommand<ToolboxLayoutEntry> ToggleLayoutCommand { get; }
    public RelayCommand ResetLayoutCommand { get; }
    public AsyncRelayCommand ExportLayoutCommand { get; }
    public AsyncRelayCommand ImportLayoutCommand { get; }

    /// <summary>Shows/hides the always-on-top pill and remembers the choice.</summary>
    public bool ShowFloatingToolbar
    {
        get => _settings.Current.ShowFloatingToolbar;
        set
        {
            if (_settings.Current.ShowFloatingToolbar == value)
            {
                return;
            }

            _settings.Current.ShowFloatingToolbar = value;
            _settings.Save();

            try
            {
                if (value)
                {
                    _toolbarHost.Show();
                }
                else
                {
                    _toolbarHost.Hide();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                _logger.Error("Floating toolbar toggle failed.", ex);
            }

            OnPropertyChanged();
            StatusMessage = value ? "Floating toolbar shown." : "Floating toolbar hidden.";
        }
    }

    private void RefreshLayout()
    {
        LayoutItems.Clear();
        foreach (ToolboxLayoutEntry entry in _layout.Layout)
        {
            LayoutItems.Add(entry);
        }
    }

    private async Task ExportLayoutAsync()
    {
        string path = Path.Combine(AppInfo.DataFolder, "toolbox.layout.export.json");
        try
        {
            await _layout.ExportAsync(path);
            StatusMessage = "Toolbox layout exported to " + path;
        }
        catch (Exception ex)
        {
            StatusMessage = "Layout export failed: " + ex.Message;
            _logger.Error("Toolbox layout export failed.", ex);
        }
    }

    private async Task ImportLayoutAsync()
    {
        string path = Path.Combine(AppInfo.DataFolder, "toolbox.layout.export.json");
        try
        {
            bool imported = await _layout.ImportAsync(path);
            StatusMessage = imported
                ? "Toolbox layout imported."
                : "Layout import unavailable: choose a valid exported layout.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Layout import failed: " + ex.Message;
            _logger.Error("Toolbox layout import failed.", ex);
        }
    }

    public override Task LoadAsync()
    {
        // Reflect the saved state without re-triggering the setter.
        if (_settings.Current.ShowFloatingToolbar && !_toolbarHost.IsVisible)
        {
            _toolbarHost.Show();
        }

        return Task.CompletedTask;
    }

    protected override void OnBusyChanged() => LaunchToolCommand.NotifyCanExecuteChanged();

    private async Task LaunchAsync(string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId))
        {
            return;
        }

        IsBusy = true;

        try
        {
            OperationResult result = await _toolbox.LaunchAsync(toolId);
            StatusMessage = result.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _logger.Error($"Tool '{toolId}' failed.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
