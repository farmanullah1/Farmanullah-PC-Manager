using PCManager.Core.Android;
using PCManager.Services.Android;
using PCManager.Services.Abstractions;
using Xunit;

namespace PCManager.Tests;

public sealed class AndroidBackupServiceTests
{
    [Fact]
    public async Task Snapshot_create_and_restore_round_trip()
    {
        using var temp = new AndroidBackupTemp();
        var engine = new FakePackageEngine();
        var service = new AndroidBackupService(
            engine,
            new FakeLogger(),
            Path.Combine(temp.Path, "backups"),
            Path.Combine(temp.Path, "android_actions.log"));
        AdbDevice device = Device();
        AdbUser user = new(0, "Owner", "running", true, false);
        var scan = new PackageScan(
            device.Serial,
            0,
            new[]
            {
                new AndroidPackage(
                    "com.example.app",
                    PackageState.Disabled,
                    false,
                    null),
            });

        AndroidPackageSnapshot snapshot =
            await service.CreateSnapshotAsync(device, user, scan);
        AndroidPackageSnapshot? loaded =
            await service.ReadSnapshotAsync(
                Path.Combine(
                    temp.Path,
                    "backups",
                    snapshot.Id + ".json"));

        Assert.NotNull(loaded);
        Assert.Equal(snapshot.Id, loaded!.Id);
        Assert.Equal(PackageState.Disabled, loaded.Packages[0].State);

        PackageBatchResult restored =
            await service.RestoreSnapshotAsync(device, user, snapshot);
        Assert.Single(engine.Requests);
        Assert.Equal(PackageAction.Disable, engine.Requests[0].Action);
        Assert.Equal(PackageBatchStatus.Completed, restored.Status);
    }

    [Fact]
    public async Task Export_and_import_txt_round_trip_unknown_notice()
    {
        using var temp = new AndroidBackupTemp();
        var service = new AndroidBackupService(
            new FakePackageEngine(),
            new FakeLogger(),
            Path.Combine(temp.Path, "backups"),
            Path.Combine(temp.Path, "android_actions.log"));
        string path = Path.Combine(temp.Path, "uad_exported_selection.txt");
        await service.ExportSelectionAsync(
            path,
            new[] { "com.example.app", "bad id", "com.example.app" },
            true);

        UadListCatalog catalog = new(
            new[]
            {
                new UadPackageEntry(
                    "com.example.app",
                    UadListKind.Misc,
                    "Example",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    UadRemovalTier.Recommended),
            },
            UadListOrigin.Embedded,
            "hash",
            1,
            0,
            "Test");

        AndroidSelectionImportResult imported =
            await service.ImportSelectionAsync(path, catalog);

        Assert.Contains("com.example.app", imported.PackageIds);
        Assert.Equal("1 package id(s) imported.", imported.Message);
    }

    [Fact]
    public async Task Action_log_uses_hashed_serial_and_no_raw_serial()
    {
        using var temp = new AndroidBackupTemp();
        var service = new AndroidBackupService(
            new FakePackageEngine(),
            new FakeLogger(),
            Path.Combine(temp.Path, "backups"),
            Path.Combine(temp.Path, "android_actions.log"));
        AdbDevice device = Device();
        AdbUser user = new(0, "Owner", "running", true, false);
        await service.AppendLogAsync(
            device,
            user,
            new[]
            {
                new PackageActionResult(
                    "com.example.app",
                    PackageAction.Disable,
                    PackageAction.Disable,
                    PackageState.Enabled,
                    PackageState.Disabled,
                    PackageResultStatus.Ok,
                    "completed"),
            });

        string log = await File.ReadAllTextAsync(service.LogFilePath);
        Assert.DoesNotContain(device.Serial, log);
        Assert.Contains("serial=", log);
        Assert.Contains("package=com.example.app", log);
        Assert.Contains("action=Disable", log);
    }

    private static AdbDevice Device() => new(
        "SERIAL123",
        AdbDeviceState.Device,
        "device",
        "Test Phone",
        "test",
        "test",
        "1",
        AdbConnectionKind.Usb);

    private sealed class FakePackageEngine : IPackageEngine
    {
        public List<PackageActionRequest> Requests { get; } = new();

        public Task<PackageScanResult> ScanAsync(
            AdbDevice device,
            int userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PackageScanResult(
                PackageScanStatus.Ready,
                "ok",
                null));

        public Task<PackageBatchResult> ApplyAsync(
            AdbDevice device,
            int userId,
            IReadOnlyCollection<PackageActionRequest> requests,
            PackageApplyOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.AddRange(requests);
            return Task.FromResult(new PackageBatchResult(
                PackageBatchStatus.Completed,
                "completed",
                Array.Empty<PackageActionResult>(),
                0,
                true,
                null,
                Array.Empty<PackageDependencyWarning>()));
        }
    }

    private sealed class AndroidBackupTemp : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "fpm-a5-" + Guid.NewGuid().ToString("N"));

        public AndroidBackupTemp()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Temporary test cleanup is best effort.
            }
        }
    }

    private sealed class FakeLogger : IAppLogger
    {
        public string LogFilePath => "test";
        public void Info(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
