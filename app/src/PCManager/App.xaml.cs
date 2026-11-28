using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PCManager.Cli;
using PCManager.Core;
using PCManager.Core.Debloat;
using PCManager.Core.Models;
using PCManager.Motion;
using PCManager.Services;
using MotionKit = PCManager.Motion.Motion;
using PCManager.Services.Abstractions;
using PCManager.Services.DependencyInjection;
using PCManager.ViewModels;
using PCManager.Views;

namespace PCManager;

/// <summary>
/// Composition root.
///
/// Identity rules enforced here:
///   - AppUserModelID "FarmanullahPCManager.Desktop.8A4F2C1E" (separate taskbar identity),
///   - single-instance mutex "Global\FarmanullahPCManager_SingleInstance_8A4F2C1E",
///   - tray tooltip "Farmanullah PC Manager",
///   - all state under %LOCALAPPDATA%\FarmanullahPCManager.
/// No URI protocol is registered anywhere in the app.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private Mutex? _singleInstance;
    private SingleInstanceChannel? _channel;
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _toolbarOnly;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(argument =>
                string.Equals(argument, "--exit", StringComparison.OrdinalIgnoreCase)))
        {
            SingleInstanceChannel.TrySendExit(
                SingleInstanceChannel.BuildPipeName(),
                TimeSpan.FromSeconds(3));
            Shutdown(0);
            return;
        }

        // Own taskbar group / notification identity.
        AppIdentity.ApplyAppUserModelId(AppInfo.AppUserModelId);

        if (!TryAcquireSingleInstance())
        {
            // Another FPM owns the mutex (full UI or toolbar-only). Ask it to show itself, then leave;
            // never start a competing shell or a second toolbar.
            bool forwarded = SingleInstanceChannel.TrySendShow(
                SingleInstanceChannel.BuildPipeName(), TimeSpan.FromSeconds(3));
            Shutdown(forwarded ? 0 : 1);
            return;
        }

        // Explicit during startup so closing the splash cannot terminate the app; the shell
        // switches this back to OnMainWindowClose once it is visible.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var collection = new ServiceCollection();

        collection.AddPCManagerServices();

        // WPF-only services.
        collection.AddSingleton<IThemeService, ThemeService>();
        collection.AddSingleton<IMotionPolicy, AppMotionPolicy>();
        collection.AddSingleton<IFloatingToolbarHost, FloatingToolbarHost>();
        collection.AddSingleton<IUserConfirmationService, MessageBoxConfirmationService>();

        // One Boost run shared by Home and the floating toolbar (U7/W5).
        collection.AddSingleton<PCManager.Services.Boost.BoostCoordinator>();

        // One battery presenter shared by Home, the toolbar and Settings (T1).
        collection.AddSingleton<BatteryStatusViewModel>();
        collection.AddSingleton<TemperatureStatusViewModel>();
        collection.AddSingleton<PackageListViewModel>();
        collection.AddSingleton<AndroidPackageViewModel>();
        collection.AddSingleton<AndroidDebloatViewModel>();
        collection.AddSingleton<DebloatViewModel>();

        // Navigation is a broker so pages never reference the shell.
        collection.AddSingleton<NavigationService>();
        collection.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

        // Page view models.
        collection.AddSingleton<HomeViewModel>();
        collection.AddSingleton<ProtectionViewModel>();
        collection.AddSingleton<StorageViewModel>();
        collection.AddSingleton<AppsViewModel>();
        collection.AddSingleton<ToolboxViewModel>();
        collection.AddSingleton<MemoryViewModel>();
        collection.AddSingleton<SettingsViewModel>();

        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<MainWindow>();

        // Floating toolbar window + its view model.
        collection.AddSingleton<FloatingToolbarViewModel>();
        collection.AddSingleton<FloatingToolbarWindow>();

        _services = collection.BuildServiceProvider();

        // Hand-off channel: a second launch asks this process to show its window.
        _channel = new SingleInstanceChannel();
        _channel.ShowRequested += OnShowRequested;
        _channel.ExitRequested += OnExitRequested;
        _channel.Start();

        // One motion policy for the whole shell: system settings, app settings, power and RDP.
        MotionKit.UsePolicy(_services.GetRequiredService<IMotionPolicy>());

        // Debloat CLI shares the exact planner, engine and journal as the page.
        if (DebloatCommandLine.IsDebloatCommand(e.Args))
        {
            int exitCode = Task.Run(() => DebloatCli.RunAsync(
                    e.Args,
                    _services.GetRequiredService<PCManager.Services.Debloat.DebloatEngine>(),
                    _services.GetRequiredService<PCManager.Services.Debloat.IDebloatProfileService>()))
                .GetAwaiter()
                .GetResult();

            Environment.Exit(exitCode);
            return;
        }

        // Cleanup CLI shares the exact same engine as the UI. --scan deletes nothing;
        // --clean requires an explicit --ids list.
        if (CleanupCli.IsCleanupCommand(e.Args))
        {
            // Run on the thread pool: awaiting on the UI thread would capture the WPF
            // dispatcher context and deadlock before the first continuation.
            int exitCode = Task.Run(() => CleanupCli.RunAsync(
                    e.Args,
                    _services.GetRequiredService<ICleanupScanner>(),
                    _services.GetRequiredService<ICleanupCommandParser>(),
                    _services.GetRequiredService<ICleanupExecutor>(),
                    _services.GetRequiredService<ICleanupLog>(),
                    _services.GetRequiredService<IRestorePointService>()))
                .GetAwaiter()
                .GetResult();

            try
            {
                Console.Out.Flush();
            }
            catch
            {
                // No console attached.
            }

            Environment.Exit(exitCode);
            return;
        }

        // W3: lightweight mode — toolbar + tray only, no main window, no splash.
        if (e.Args.Any(argument =>
                string.Equals(argument, "--toolbar-only", StringComparison.OrdinalIgnoreCase)))
        {
            StartToolbarOnly("requested on the command line");
            return;
        }

        // Dev-only inventory review surface. Never shown in normal use.
        if (e.Args.Any(argument => string.Equals(argument, "--icon-gallery", StringComparison.OrdinalIgnoreCase)))
        {
            var gallery = new IconGalleryWindow(_services.GetRequiredService<IThemeService>());
            MainWindow = gallery;
            gallery.Show();
            return;
        }

        // Splash first, then the staged startup (settings -> services -> theme -> shell).
        _ = StartShellAsync(e.Args);
    }

    /// <summary>
    /// Global\... mutex: a machine-wide single-instance guard, entirely distinct from
    /// Microsoft PC Manager's, so the two utilities can run at the same time.
    /// </summary>
    private bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstance = new Mutex(initiallyOwned: true, AppInfo.SingleInstanceMutexName, out bool created);

            if (created)
            {
                return true;
            }

            _singleInstance.Dispose();
            _singleInstance = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // A locked-down token cannot create a Global object; fall back to a session name.
            _singleInstance = new Mutex(initiallyOwned: true, "FarmanullahPCManager_SingleInstance_8A4F2C1E", out bool created);
            return created;
        }
    }

    private void CreateTrayIcon(bool toolbarOnly = false)
    {
        try
        {
            _tray = new System.Windows.Forms.NotifyIcon
            {
                Icon = LoadTrayIcon() ?? System.Drawing.SystemIcons.Shield,
                Text = AppInfo.ProductName, // shell tooltip, max 63 characters
                Visible = true
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open FPM", IconRasterizer.ToBitmap("Logo", 16), (_, _) => RequestShow());

            if (toolbarOnly)
            {
                menu.Items.Add("Hide toolbar", IconRasterizer.ToBitmap("Toolbox", 16), (_, _) => HideToolbarFromTray());
            }
            else
            {
                menu.Items.Add("Boost now", IconRasterizer.ToBitmap("Memory", 16), async (_, _) => await TrayBoostAsync());
            }

            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => RequestExit());
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += (_, _) => RequestShow();
        }
        catch (Exception ex)
        {
            _services?.GetService<IAppLogger>()?.Error("Tray icon creation failed.", ex);
        }
    }

    private void RequestShow()
    {
        if (_toolbarOnly)
        {
            HandOffToFullUi();
            return;
        }

        ShowMainWindow();
    }

    /// <summary>Tray Exit (W3): ask or remember, then exit all, or exit and leave the toolbar running.</summary>
    private void RequestExit()
    {
        IAppLogger? logger = _services?.GetService<IAppLogger>();
        ISettingsService? settings = _services?.GetService<ISettingsService>();

        if (settings is null)
        {
            ExitNow();
            return;
        }

        bool toolbarEnabled = settings.Current.ShowFloatingToolbar;
        ExitAction action = AppLifetimePolicy.DecideExit(
            settings.Current.ExitPreference, toolbarEnabled, _toolbarOnly);

        if (action == ExitAction.Prompt)
        {
            var dialog = new ExitDialog();
            dialog.ShowDialog();

            if (dialog.Choice is null)
            {
                return;
            }

            action = dialog.Choice.Value;

            if (dialog.Remember)
            {
                settings.Current.ExitPreference = action == ExitAction.ExitAll
                    ? ExitPreference.ExitAll
                    : ExitPreference.ExitKeepToolbar;
                settings.Save();
            }
        }

        if (action == ExitAction.ExitKeepToolbar && !_toolbarOnly)
        {
            settings.Current.ShowFloatingToolbar = true;
            settings.Save();
            logger?.Info("Exit requested: keeping the toolbar in a separate toolbar-only process.");
            ReleaseInstanceResources();
            StartToolbarOnlyProcess();
            _isExiting = true;
            Shutdown();
            return;
        }

        if (_toolbarOnly)
        {
            // Exiting toolbar-only mode turns the toolbar setting off so it does not reappear silently.
            settings.Current.ShowFloatingToolbar = false;
            settings.Save();
        }

        ExitNow();
    }

    private void ExitNow()
    {
        _isExiting = true;
        Shutdown();
    }

    private static void StartToolbarOnlyProcess()
    {
        string? executable = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(executable, "--toolbar-only")
        {
            UseShellExecute = true,
        });
    }

    /// <summary>Toolbar-only (or a hidden window) asked to open the full UI: hand over and leave.</summary>
    private void HandOffToFullUi()
    {
        _services?.GetService<IAppLogger>()?.Info("Hand-off: starting the full UI and ending toolbar-only mode.");
        ReleaseInstanceResources();

        string? executable = Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(executable))
        {
            Process.Start(new ProcessStartInfo(executable, "--no-splash")
            {
                UseShellExecute = true,
            });
        }

        _isExiting = true;
        Shutdown();
    }

    private void HideToolbarFromTray()
    {
        if (_services?.GetService<ISettingsService>() is { } settings)
        {
            settings.Current.ShowFloatingToolbar = false;
            settings.Save();
        }

        _services?.GetService<IFloatingToolbarHost>()?.Hide();
        _services?.GetService<IAppLogger>()?.Info("Toolbar hidden from the tray; the icon stays for Open FPM and Exit.");
    }

    private void ReleaseInstanceResources()
    {
        _channel?.Dispose();
        _channel = null;

        if (_singleInstance is not null)
        {
            try { _singleInstance.ReleaseMutex(); } catch { /* not owned */ }
            _singleInstance.Dispose();
            _singleInstance = null;
        }
    }

    private void OnShowRequested()
    {
        _services?.GetService<IAppLogger>()?.Info(
            "Single-instance hand-off received; showing FPM instead of starting a second process.");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_toolbarOnly)
            {
                HandOffToFullUi();
                return;
            }

            ShowMainWindow();
        }));
    }

    private void OnExitRequested()
    {
        _services?.GetService<IAppLogger>()?.Info(
            "Installer or uninstaller requested a graceful FPM shutdown.");
        Dispatcher.BeginInvoke(new Action(ExitNow));
    }

    /// <summary>Main window close (W3): hide to tray while the toolbar runs, otherwise exit.</summary>
    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting || sender is not Window window)
        {
            return;
        }

        ISettingsService? settings = _services?.GetService<ISettingsService>();

        CloseAction action = settings is null
            ? CloseAction.Exit
            : AppLifetimePolicy.DecideClose(
                settings.Current.CloseToTray, settings.Current.ShowFloatingToolbar, _isExiting);

        if (action == CloseAction.HideToTray)
        {
            e.Cancel = true;
            window.Hide();
            _services?.GetService<IAppLogger>()?.Info(
                "Main window closed to the tray; the toolbar keeps FPM running.");
            return;
        }

        _isExiting = true;
    }

    private static System.Drawing.Icon? LoadTrayIcon()
    {
        try
        {
            string? executable = Environment.ProcessPath;
            return executable is null
                ? null
                : System.Drawing.Icon.ExtractAssociatedIcon(executable);
        }
        catch
        {
            return null;
        }
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        MainWindow.Show();

        if (MainWindow.WindowState == WindowState.Minimized)
        {
            MainWindow.WindowState = WindowState.Normal;
        }

        MainWindow.Activate();
    }

    private async Task TrayBoostAsync()
    {
        if (_services?.GetService<IPcBoostService>() is not { } boost)
        {
            return;
        }

        try
        {
            var trim = await boost.OptimizeAsync();
            _tray?.ShowBalloonTip(3000, AppInfo.ProductName,
                $"{trim.WorkingSetsTrimmed} working sets trimmed. Available memory {trim.AvailableDeltaText}.",
                System.Windows.Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _services?.GetService<IAppLogger>()?.Error("Tray boost failed.", ex);
        }
    }

    /// <summary>
    /// Diagnostics switch (off by default): PCMANAGER_TRACE=&lt;file&gt; records WPF binding
    /// warnings there; unhandled exceptions always go to the app log.
    /// </summary>
    private void EnableDiagnostics()
    {
        string? path = Environment.GetEnvironmentVariable("PCMANAGER_TRACE");

        if (!string.IsNullOrWhiteSpace(path))
        {
            try
            {
                PresentationTraceSources.Refresh();
                var listener = new TextWriterTraceListener(path) { TraceOutputOptions = TraceOptions.None };
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
                PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
                PresentationTraceSources.ResourceDictionarySource.Listeners.Add(listener);
                PresentationTraceSources.ResourceDictionarySource.Switch.Level = SourceLevels.Warning;
                Trace.AutoFlush = true;
                listener.Flush();
            }
            catch
            {
                // Diagnostics must never prevent the app from starting.
            }
        }

        DispatcherUnhandledException += (_, args) =>
            _services?.GetService<IAppLogger>()?.Error("Unhandled UI exception.", args.Exception);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                _services?.GetService<IAppLogger>()?.Error("Unhandled exception.", exception);
            }
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<IAppLogger>()?.Info($"{AppInfo.ProductName} exiting.");

        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }

        _services?.Dispose();
        _services = null;

        ReleaseInstanceResources();

        base.OnExit(e);
    }
}
