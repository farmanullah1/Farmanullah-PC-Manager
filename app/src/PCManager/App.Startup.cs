using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PCManager.Core;
using PCManager.Services.Abstractions;
using PCManager.ViewModels;
using PCManager.Localization;
using PCManager.Views;

namespace PCManager;

/// <summary>
/// U5 startup orchestration: the splash is shown before the shell exists, every stage it
/// reports is a real step (settings, services, theme, shell), and it closes with a fade once
/// the shell is up — no earlier than 600 ms and never kept past 3 s.
/// </summary>
public partial class App
{
    private SplashWindow? _splash;
    private bool _shellShown;

    private async Task StartShellAsync(string[] args)
    {
        Microsoft.Extensions.DependencyInjection.ServiceProvider services = _services!;
        IAppLogger logger = services.GetRequiredService<IAppLogger>();

        try
        {
            ISettingsService settings = services.GetRequiredService<ISettingsService>();
            IThemeService theme = services.GetRequiredService<IThemeService>();

            LocalizedStrings localization =
                AppLocalization.Apply(settings.Current.Language);

            // The splash must already use the user's palette.
            theme.Apply(settings.Current.Theme);

            bool wantsSplash = settings.Current.ShowSplash
                && !args.Any(argument =>
                    string.Equals(argument, "--no-splash", StringComparison.OrdinalIgnoreCase));

            var clock = Stopwatch.StartNew();

            if (wantsSplash)
            {
                _splash = new SplashWindow(settings.IsFirstRun);
                _splash.Show();
                _ = EnforceSplashLimitAsync(clock);
            }

            _splash?.SetStage("Loading settings…", 12);
            await Dispatcher.Yield(DispatcherPriority.Background);

            MigrateLegacyData(logger);
            _splash?.SetStage("Preparing services…", 40);
            await Dispatcher.Yield(DispatcherPriority.Background);

            EnableDiagnostics();
            CreateTrayIcon();
            _splash?.SetStage("Applying theme…", 60);
            await Dispatcher.Yield(DispatcherPriority.Background);

            _splash?.SetStage("Building the shell…", 78);
            await Dispatcher.Yield(DispatcherPriority.Background);

            var window = services.GetRequiredService<MainWindow>();
            window.FlowDirection =
                localization.RightToLeft
                    ? FlowDirection.RightToLeft
                    : FlowDirection.LeftToRight;
            MainWindow = window;

            _splash?.SetStage("Ready", 100);
            await Dispatcher.Yield(DispatcherPriority.Background);

            window.Closing += OnMainWindowClosing;
            window.Show();
            _shellShown = true;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            // W3: the toolbar is part of the running app, not a page extra; restore it before the
            // main window can be closed to the tray.
            if (settings.Current.ShowFloatingToolbar)
            {
                services.GetRequiredService<IFloatingToolbarHost>().Show();
                logger.Info("Floating toolbar restored from settings.");
            }

            // Build the remaining page visuals at idle so later transitions are warm.
            window.PrewarmPages();

            if (_splash is { } splash)
            {
                _splash = null; // stage updates are done; the watchdog must not close it now
                TimeSpan wait = SplashTiming.DelayFor(clock.Elapsed);

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait);
                }

                await splash.CloseAnimatedAsync();
                logger.Info(splash.WasSkipped
                    ? "Splash dismissed by the user."
                    : $"Splash closed after {clock.ElapsedMilliseconds} ms.");
            }

            logger.Info($"{AppInfo.ProductName} started (pid {Environment.ProcessId}).");
        }
        catch (Exception ex)
        {
            logger.Error("Startup failed.", ex);

            if (_splash is { } splash)
            {
                _splash = null;
                splash.Close();
            }

            if (MainWindow is null)
            {
                try
                {
                    var fallback = services.GetRequiredService<MainWindow>();
                    MainWindow = fallback;
                    fallback.Show();
                    _shellShown = true;
                    ShutdownMode = ShutdownMode.OnMainWindowClose;
                }
                catch (Exception second)
                {
                    logger.Error("The main window could not be created.", second);
                    Shutdown();
                }
            }
        }
    }

    /// <summary>
    /// W3 lightweight mode: tray + floating toolbar only. No splash, no main window, no shell
    /// services; used by <c>--toolbar-only</c> and by "exit but keep the toolbar".
    /// </summary>
    private void StartToolbarOnly(string reason)
    {
        _toolbarOnly = true;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Microsoft.Extensions.DependencyInjection.ServiceProvider services = _services!;
        IAppLogger logger = services.GetRequiredService<IAppLogger>();

        try
        {
            ISettingsService settings = services.GetRequiredService<ISettingsService>();
            services.GetRequiredService<IThemeService>().Apply(settings.Current.Theme);
            settings.Current.ShowFloatingToolbar = true;
            settings.Save();

            CreateTrayIcon(toolbarOnly: true);
            services.GetRequiredService<IFloatingToolbarHost>().Show();

            logger.Info($"Toolbar-only mode started ({reason}); pid {Environment.ProcessId}.");
        }
        catch (Exception ex)
        {
            logger.Error("Toolbar-only mode failed.", ex);
            _isExiting = true;
            Shutdown();
        }
    }

    /// <summary>Closes a splash that would otherwise stay past the 3 s budget.</summary>
    private async Task EnforceSplashLimitAsync(Stopwatch clock)
    {
        TimeSpan remaining = SplashTiming.MaximumVisible - clock.Elapsed;

        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        if (_shellShown || _splash is not { } splash)
        {
            return;
        }

        _splash = null;
        splash.Close();
    }

    private void MigrateLegacyData(IAppLogger logger)
    {
        try
        {
            _services?.GetService<ILegacyIdentityMigrationService>()?.MigrateIfNecessary();
        }
        catch (Exception ex)
        {
            logger.Error($"Legacy migration failed: {ex.Message}", ex);
        }
    }
}
