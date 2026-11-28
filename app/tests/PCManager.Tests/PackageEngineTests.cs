using PCManager.Core.Android;
using PCManager.Services.Android;
using Xunit;

namespace PCManager.Tests;

public sealed class PackageEngineTests
{
    [Fact]
    public async Task Scan_joins_states_system_metadata_and_the_four_queries()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.example.app", "com.example.game", "com.example.disabled", "com.example.third"),
            Packages("com.example.app", "com.example.game", "com.example.disabled"),
            Packages("com.example.app", "com.example.third"),
            Packages("com.example.disabled"));
        PackageEngine engine = Engine(executor);

        PackageScanResult result = await engine.ScanAsync(Device(), 0);

        Assert.Equal(PackageScanStatus.Ready, result.Status);
        PackageScan scan = result.Scan!;
        Assert.Equal(4, scan.Packages.Count);
        Assert.Equal(PackageState.Enabled, scan.ById["com.example.app"].State);
        Assert.Equal(PackageState.Uninstalled, scan.ById["com.example.game"].State);
        Assert.Equal(PackageState.Disabled, scan.ById["com.example.disabled"].State);
        Assert.True(scan.ById["com.example.app"].IsSystem);
        Assert.False(scan.ById["com.example.third"].IsSystem);
        Assert.Equal("Example app", scan.ById["com.example.app"].Description);
        Assert.Equal("Unlisted", scan.ById["com.example.third"].ListText);
        Assert.Equal(
            new[]
            {
                PackageListFilter.All,
                PackageListFilter.System,
                PackageListFilter.Enabled,
                PackageListFilter.Disabled,
            },
            executor.Commands.Select(command => command.ListFilter));
        Assert.All(executor.Commands, command => Assert.Equal(0, command.UserId));
    }

    [Fact]
    public async Task Scan_failure_is_typed_and_disconnect_is_not_an_empty_ready_scan()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.example.app"),
            new AdbProcessResult(1, "", "error: no devices/emulators found"));
        PackageEngine engine = Engine(executor);

        PackageScanResult result = await engine.ScanAsync(Device(), 0);

        Assert.Equal(PackageScanStatus.Disconnected, result.Status);
        Assert.Null(result.Scan);
        Assert.Contains("no devices", result.Message);
    }

    [Fact]
    public async Task Apply_continues_after_a_normal_failure_and_verifies_successes()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.example.app", "com.example.game"),
            Packages(),
            Packages("com.example.app", "com.example.game"),
            Packages(),
            new AdbProcessResult(1, "", "adb: failed"),
            Success(),
            Packages("com.example.app", "com.example.game"),
            Packages(),
            Packages(),
            Packages("com.example.app", "com.example.game"));
        PackageEngine engine = Engine(executor);

        PackageBatchResult result = await engine.ApplyAsync(
            Device(),
            0,
            new[]
            {
                new PackageActionRequest("com.example.app", PackageAction.Disable),
                new PackageActionRequest("com.example.game", PackageAction.Disable),
            });

        Assert.Equal(PackageBatchStatus.Completed, result.Status);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(1, result.OkCount);
        Assert.True(result.Verified);
        Assert.Equal(PackageState.Disabled, result.Results[1].After);
        Assert.Contains("1 succeeded", result.Message);
    }

    [Fact]
    public async Task Apply_stops_on_device_disconnect_and_counts_remaining_requests()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.example.app", "com.example.game"),
            Packages(),
            Packages("com.example.app", "com.example.game"),
            Packages(),
            new AdbProcessResult(1, "", "error: device offline"));
        PackageEngine engine = Engine(executor);

        PackageBatchResult result = await engine.ApplyAsync(
            Device(),
            0,
            new[]
            {
                new PackageActionRequest("com.example.app", PackageAction.Disable),
                new PackageActionRequest("com.example.game", PackageAction.Disable),
            });

        Assert.Equal(PackageBatchStatus.StoppedDisconnected, result.Status);
        Assert.Equal(1, result.RemainingCount);
        Assert.Single(result.Results);
        Assert.False(result.Verified);
        Assert.Equal(5, executor.Commands.Count);
    }

    [Fact]
    public async Task Apply_reports_already_without_a_mutating_command()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.example.app"),
            Packages(),
            Packages(),
            Packages("com.example.app"));
        PackageEngine engine = Engine(executor);

        PackageBatchResult result = await engine.ApplyAsync(
            Device(),
            0,
            new[] { new PackageActionRequest("com.example.app", PackageAction.Disable) });

        PackageActionResult item = Assert.Single(result.Results);
        Assert.Equal(PackageResultStatus.Already, item.Status);
        Assert.Equal(PackageState.Disabled, item.After);
        Assert.Equal(4, executor.Commands.Count);
    }

    [Fact]
    public async Task Apply_blocks_denylisted_packages_without_the_exact_expert_confirmation()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.android.systemui", "com.example.app"),
            Packages("com.android.systemui"),
            Packages("com.android.systemui", "com.example.app"),
            Packages());
        PackageEngine engine = Engine(executor);

        PackageBatchResult result = await engine.ApplyAsync(
            Device(),
            0,
            new[] { new PackageActionRequest("com.android.systemui", PackageAction.Disable) },
            new PackageApplyOptions(ExpertMode: true, ExpertConfirmation: "wrong"));

        PackageActionResult item = Assert.Single(result.Results);
        Assert.Equal(PackageResultStatus.BlockedDenylist, item.Status);
        Assert.DoesNotContain(executor.Commands, command => command.Kind == AdbCommandKind.DisablePackage);
    }

    [Fact]
    public async Task Apply_accepts_exact_denylist_confirmation_and_disables()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.android.settings"),
            Packages("com.android.settings"),
            Packages("com.android.settings"),
            Packages(),
            Success(),
            Packages("com.android.settings"),
            Packages("com.android.settings"),
            Packages(),
            Packages("com.android.settings"));
        PackageEngine engine = Engine(executor);

        PackageBatchResult result = await engine.ApplyAsync(
            Device(),
            0,
            new[] { new PackageActionRequest("com.android.settings", PackageAction.Disable) },
            new PackageApplyOptions(ExpertMode: true, ExpertConfirmation: "com.android.settings"));

        PackageActionResult item = Assert.Single(result.Results);
        Assert.Equal(PackageResultStatus.Ok, item.Status);
        Assert.Equal(PackageState.Disabled, item.After);
        Assert.True(result.Verified);
    }

    [Fact]
    public async Task Disable_instead_of_uninstall_emits_the_disable_command()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.example.app"),
            Packages("com.example.app"),
            Packages("com.example.app"),
            Packages(),
            Success(),
            Packages("com.example.app"),
            Packages("com.example.app"),
            Packages(),
            Packages("com.example.app"));
        PackageEngine engine = Engine(executor);

        PackageBatchResult result = await engine.ApplyAsync(
            Device(),
            0,
            new[] { new PackageActionRequest("com.example.app", PackageAction.Uninstall) },
            new PackageApplyOptions(DisableInsteadOfUninstall: true, ExpertMode: false));

        PackageActionResult item = Assert.Single(result.Results);
        Assert.Equal(PackageAction.Uninstall, item.RequestedAction);
        Assert.Equal(PackageAction.Disable, item.Action);
        Assert.Contains(executor.Commands, command => command.Kind == AdbCommandKind.DisablePackage);
        Assert.DoesNotContain(executor.Commands, command => command.Kind == AdbCommandKind.UninstallPackage);
        Assert.Equal(PackageState.Disabled, item.After);
    }

    [Fact]
    public async Task Separate_devices_keep_actions_on_their_requested_serial()
    {
        var executor = new QueueAdbExecutor(
            Packages("com.example.app"),
            Packages("com.example.app"),
            Packages(),
            Packages("com.example.app"),
            Packages("com.example.app"),
            Packages("com.example.app"),
            Packages(),
            Packages("com.example.app"));
        PackageEngine engine = Engine(executor);

        await engine.ApplyAsync(
            Device(),
            0,
            new[] { new PackageActionRequest("com.example.app", PackageAction.Disable) });
        await engine.ApplyAsync(
            Device("SERIAL456"),
            10,
            new[] { new PackageActionRequest("com.example.app", PackageAction.Disable) });

        Assert.All(executor.Devices.Take(4), device => Assert.Equal("SERIAL123", device.Serial));
        Assert.All(executor.Devices.Skip(4), device => Assert.Equal("SERIAL456", device.Serial));
        Assert.All(executor.Commands, command =>
            Assert.Contains(command.Serial, new[] { "SERIAL123", "SERIAL456" }));
    }

    [Fact]
    public async Task Dependency_warnings_are_reported_without_changing_the_batch()
    {
        UadPackageEntry app = new(
            "com.example.app",
            UadListKind.Misc,
            "Example app",
            new[] { "com.example.base" },
            new[] { "com.example.host" },
            Array.Empty<string>(),
            UadRemovalTier.Advanced);
        UadListCatalog catalog = new(
            new[] { app },
            UadListOrigin.Embedded,
            "hash",
            1,
            0,
            "Test");
        var executor = new QueueAdbExecutor(
            Packages("com.example.app"),
            Packages(),
            Packages(),
            Packages("com.example.app"));
        PackageEngine engine = Engine(executor, catalog);

        PackageBatchResult result = await engine.ApplyAsync(
            Device(),
            0,
            new[] { new PackageActionRequest("com.example.app", PackageAction.Disable) });

        Assert.Equal(2, result.DependencyWarnings.Count);
        Assert.Contains(result.DependencyWarnings, warning => warning.RelatedIds.Contains("com.example.base"));
        Assert.Contains(result.DependencyWarnings, warning => warning.RelatedIds.Contains("com.example.host"));
        Assert.Equal(PackageResultStatus.Already, Assert.Single(result.Results).Status);
    }

    private static PackageEngine Engine(QueueAdbExecutor executor, UadListCatalog? catalog = null) =>
        new(new FakePackageListProvider(catalog ?? Catalog()), executor, new FakeLogger());

    private static AdbDevice Device(string serial = "SERIAL123") => new(
        serial,
        AdbDeviceState.Device,
        "device",
        "Test Phone",
        "test",
        "test",
        "1",
        AdbConnectionKind.Usb);

    private static AdbProcessResult Success() => new(0, "", "");

    private static AdbProcessResult Packages(params string[] ids) =>
        new(0, string.Join('\n', ids.Select(id => $"package:{id}")), "");

    private static UadListCatalog Catalog()
    {
        UadPackageEntry app = new(
            "com.example.app",
            UadListKind.Misc,
            "Example app",
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            UadRemovalTier.Advanced);
        return new UadListCatalog(
            new[] { app },
            UadListOrigin.Embedded,
            "hash",
            1,
            0,
            "Test");
    }

    private sealed class QueueAdbExecutor(params AdbProcessResult[] results) : IAdbCommandExecutor
    {
        private readonly Queue<AdbProcessResult> _results = new(results);

        public List<AdbCommand> Commands { get; } = new();
        public List<AdbDevice> Devices { get; } = new();

        public Task<AdbProcessResult> ExecuteAsync(
            AdbDevice device,
            AdbCommand command,
            CancellationToken cancellationToken = default)
        {
            Devices.Add(device);
            Commands.Add(command);
            return Task.FromResult(_results.Count > 0
                ? _results.Dequeue()
                : new AdbProcessResult(0, "", ""));
        }
    }

    private sealed class FakePackageListProvider(UadListCatalog catalog) : IPackageListProvider
    {
        public UadListCatalog? Current => catalog;
        public string? LoadWarning => null;
        public string CacheFilePath => "unused";

        public Task<UadListCatalog> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(catalog);

        public Task<UadListUpdateResult> UpdateAsync(
            bool userInitiated,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UadListUpdateResult(
                UadListUpdateStatus.Unavailable, "fake", catalog, null, null));
    }
}
