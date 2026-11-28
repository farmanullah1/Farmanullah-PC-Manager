using Microsoft.Extensions.DependencyInjection;
using PCManager.Services.Abstractions;
using PCManager.Services.Boost;
using PCManager.Services.Cleaners;
using PCManager.Services.Cleanup;
using PCManager.Services.Elevation;
using PCManager.Services.Infrastructure;
using PCManager.Services.Memory;
using PCManager.Services.Processes;
using PCManager.Services.Protection;
using PCManager.Services.Settings;
using PCManager.Services.Startup;
using PCManager.Services.Storage;
using PCManager.Services.Toolbox;

namespace PCManager.Services.DependencyInjection;

/// <summary>
/// Single place where every service is registered.
///
/// To add a tool or cleaner: write the interface + implementation in this project, add
/// one line below, then inject it into the view model that exposes it.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPCManagerServices(this IServiceCollection services)
    {
        // --- Isolated single-purpose services -----------------------------
        services.AddSingleton<IAppLogger, AppLogger>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAppStartupService, AppStartupService>();
        services.AddSingleton<ITempFileCleaner, TempFileCleaner>();
        services.AddSingleton<IBrowserCacheCleaner, BrowserCacheCleaner>();
        services.AddSingleton<IRecycleBinService, RecycleBinService>();
        services.AddSingleton<IMemoryOptimizer, MemoryOptimizer>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<IStartupManager, StartupManager>();
        services.AddSingleton<IStorageAnalyzer, StorageAnalyzer>();
        services.AddSingleton<IStorageSenseService, StorageSenseService>();
        services.AddSingleton<IDuplicateFinderService, DuplicateFinderService>();
        services.AddSingleton<IFileRecycler, ShellFileRecycler>();
        services.AddSingleton<IDuplicateRemovalService, DuplicateRemovalService>();
        services.AddSingleton<IInstalledAppService, InstalledAppService>();
        services.AddSingleton<IAppxPackageService, AppxPackageService>();
        services.AddSingleton<ISystemRepairService, SystemRepairService>();
        services.AddSingleton<IDefenderStatusService, DefenderStatusService>();
        services.AddSingleton<IWindowsUpdateStatusService, WindowsUpdateStatusService>();
        services.AddSingleton<IToolLauncher, ToolLauncher>();
        services.AddSingleton<IDefaultBrowserService, DefaultBrowserService>();

        // --- Two-phase cleanup engine (Phase 1 read-only, Phase 2 approved) ---
        services.AddSingleton<IWhitelistService, WhitelistService>();
        services.AddSingleton<ISafetyGuard, SafetyGuard>();
        services.AddSingleton<ICleanupRuleCatalog, CleanupRuleCatalog>();
        services.AddSingleton<ICleanupScanner, CleanupScanner>();
        services.AddSingleton<ICleanupCommandParser, CleanupCommandParser>();
        services.AddSingleton<IQuarantineService, QuarantineService>();
        services.AddSingleton<ICleanupExecutor, CleanupExecutor>();
        services.AddSingleton<ICleanupLog, CleanupLog>();
        services.AddSingleton<ICleanupHistoryService, CleanupHistoryService>();
        services.AddSingleton<ICleanupReportExporter, CleanupReportExporter>();

        // --- Elevated helper (one fixed command per request, UAC on demand) ---
        services.AddSingleton<IElevationService>(sp =>
            new ElevationService(sp.GetRequiredService<IAppLogger>()));
        services.AddSingleton<IRestorePointService, RestorePointService>();

        // D2: typed debloat planning, journaling and helper execution.
        services.AddSingleton<PCManager.Services.Debloat.IDebloatExecutor, PCManager.Services.Debloat.HelperDebloatExecutor>();
        services.AddSingleton<PCManager.Services.Debloat.DebloatEngine>();
        services.AddSingleton<PCManager.Services.Debloat.IDebloatProfileService>(
            sp => new PCManager.Services.Debloat.DebloatProfileService(
                sp.GetRequiredService<IAppLogger>()));

        // A1: locate/verify adb, consent-gated pinned download, read-only device discovery.
        services.AddSingleton<PCManager.Services.Android.IAdbRunner, PCManager.Services.Android.ProcessAdbRunner>();
        services.AddSingleton<PCManager.Services.Android.IAdbSignatureVerifier,
            PCManager.Services.Android.AuthenticodeAdbSignatureVerifier>();
        services.AddSingleton<PCManager.Services.Android.IAdbPackageSource,
            PCManager.Services.Android.HttpAdbPackageSource>();
        services.AddSingleton<PCManager.Services.Android.IAdbProvisioner>(sp =>
            new PCManager.Services.Android.AdbProvisioner(
                sp.GetRequiredService<PCManager.Services.Android.IAdbPackageSource>(),
                sp.GetRequiredService<PCManager.Services.Android.IAdbSignatureVerifier>()));
        services.AddSingleton<PCManager.Services.Android.AdbService>();
        services.AddSingleton<PCManager.Services.Android.IAdbService>(sp =>
            sp.GetRequiredService<PCManager.Services.Android.AdbService>());
        services.AddSingleton<PCManager.Services.Android.IAdbCommandExecutor>(sp =>
            sp.GetRequiredService<PCManager.Services.Android.AdbService>());

        // A2: embedded/cached UAD list, validated; HTTPS only on an explicit update command.
        services.AddSingleton<PCManager.Services.Android.IUadListSource,
            PCManager.Services.Android.HttpUadListSource>();
        services.AddSingleton<PCManager.Services.Android.IPackageListProvider,
            PCManager.Services.Android.UadListProvider>();

        // A3: read-only package state, typed actions, sequential batches and verification.
        services.AddSingleton<PCManager.Services.Android.IPackageEngine,
            PCManager.Services.Android.PackageEngine>();

        // A5: Android snapshots, action log and selection import/export.
        services.AddSingleton<PCManager.Services.Android.IAndroidBackupService,
            PCManager.Services.Android.AndroidBackupService>();
        services.AddSingleton<PCManager.Services.Android.IPackageEngine,
            PCManager.Services.Android.PackageEngine>();

        // --- FPM Services (Migration, Memory Diagnostics, Unified Cleanup, Network) ---
        services.AddSingleton<ILegacyIdentityMigrationService, PCManager.Services.Infrastructure.LegacyIdentityMigrationService>();
        services.AddSingleton<MemoryDiagnosticsService>();
        services.AddSingleton<IMemoryDiagnosticsService>(sp => sp.GetRequiredService<MemoryDiagnosticsService>());
        services.AddSingleton<IMemoryReliefService>(sp => sp.GetRequiredService<MemoryDiagnosticsService>());
        services.AddSingleton<IUnifiedCleanupCoordinator, UnifiedCleanupCoordinator>();
        services.AddSingleton<INetworkThroughputService, PCManager.Services.Network.NetworkThroughputService>();

        // T1: one battery sampler for Home, the toolbar and Settings.
        services.AddSingleton<PCManager.Services.Sensors.IBatteryProbe, PCManager.Services.Sensors.WindowsBatteryProbe>();
        services.AddSingleton<PCManager.Services.Sensors.ITemperatureProbe, PCManager.Services.Sensors.WindowsTemperatureProbe>();
        services.AddSingleton<ISensorService, PCManager.Services.Sensors.SensorService>();
        services.AddSingleton<IStorageIntelligenceService, PCManager.Services.Storage.StorageIntelligenceService>();
        services.AddSingleton<IAppLeftoverScanner, PCManager.Services.Apps.AppLeftoverScanner>();
        services.AddSingleton<INetworkDiagnosticsService, PCManager.Services.Network.NetworkDiagnosticsService>();
        services.AddSingleton<INetworkSpeedTestService, PCManager.Services.Network.NetworkSpeedTestService>();
        services.AddSingleton<IPopupMonitorService, PCManager.Services.Protection.PopupMonitorService>();
        services.AddSingleton<IHostsBlockService, PCManager.Services.Protection.HostsBlockService>();
        services.AddSingleton<IMaintenanceReadinessService, PCManager.Services.Infrastructure.MaintenanceReadinessService>();

        // --- Page facades -------------------------------------------------
        services.AddSingleton<IPcBoostService, PcBoostService>();
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<IToolboxService, ToolboxService>();
        services.AddSingleton<IToolboxLayoutService, ToolboxLayoutService>();
        services.AddSingleton<IProtectionService, ProtectionService>();

        return services;
    }
}
