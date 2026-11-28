using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PCManager.Core;
using PCManager.Core.Android;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Android;

public interface IAndroidBackupService
{
    Task<AndroidPackageSnapshot> CreateSnapshotAsync(AdbDevice device, AdbUser user, PackageScan scan, CancellationToken cancellationToken = default);
    IReadOnlyList<AndroidPackageSnapshot> ListSnapshots();
    Task<AndroidPackageSnapshot?> ReadSnapshotAsync(string path, CancellationToken cancellationToken = default);
    Task<PackageBatchResult> RestoreSnapshotAsync(AdbDevice device, AdbUser user, AndroidPackageSnapshot snapshot, CancellationToken cancellationToken = default);
    Task AppendLogAsync(AdbDevice device, AdbUser user, IReadOnlyList<PackageActionResult> results, CancellationToken cancellationToken = default);
    Task<AndroidSelectionImportResult> ImportSelectionAsync(string path, UadListCatalog catalog, CancellationToken cancellationToken = default);
    Task ExportSelectionAsync(string path, IReadOnlyList<string> packageIds, bool upstreamTextFormat, CancellationToken cancellationToken = default);
    string LogFilePath { get; }
}

public sealed record AndroidSelectionImportResult(IReadOnlyList<string> PackageIds, IReadOnlyList<string> UnknownIds, string Message);

public sealed class AndroidBackupService : IAndroidBackupService
{
    private readonly IPackageEngine _engine;
    private readonly IAppLogger _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _backupDirectory;
    private readonly string _logFilePath;

    public AndroidBackupService(
        IPackageEngine engine,
        IAppLogger logger,
        string? backupDirectory = null,
        string? logFilePath = null)
    {
        _engine = engine;
        _logger = logger;
        _backupDirectory = backupDirectory ??
            Path.Combine(AppInfo.DataFolder, "android", "backups");
        _logFilePath = logFilePath ??
            Path.Combine(AppInfo.DataFolder, "android", "android_actions.log");
    }

    public string LogFilePath => _logFilePath;

    public async Task<AndroidPackageSnapshot> CreateSnapshotAsync(
        AdbDevice device,
        AdbUser user,
        PackageScan scan,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_backupDirectory);
        DateTimeOffset now = DateTimeOffset.Now;
        string serialHash = HashSerial(device.Serial);
        string id = $"{serialHash[..12]}_{user.Id}_{now:yyyyMMddHHmmssfff}";
        var snapshot = new AndroidPackageSnapshot(
            id,
            now,
            serialHash,
            device.Model ?? device.DisplayName,
            user.Id,
            user.Name,
            scan.Packages
                .Select(package => new AndroidPackageSnapshotEntry(package.Id, package.State))
                .OrderBy(entry => entry.PackageId, StringComparer.Ordinal)
                .ToArray());
        await WriteJsonAsync(
            Path.Combine(_backupDirectory, id + ".json"),
            snapshot,
            cancellationToken).ConfigureAwait(false);
        _logger.Info($"Android package snapshot created: {id}.");
        return snapshot;
    }

    public IReadOnlyList<AndroidPackageSnapshot> ListSnapshots()
    {
        if (!Directory.Exists(_backupDirectory))
        {
            return Array.Empty<AndroidPackageSnapshot>();
        }

        var snapshots = new List<AndroidPackageSnapshot>();
        foreach (string path in Directory
                     .EnumerateFiles(_backupDirectory, "*.json")
                     .OrderByDescending(path => path, StringComparer.Ordinal))
        {
            try
            {
                AndroidPackageSnapshot? snapshot =
                    ReadJsonSnapshotAsync(path).GetAwaiter().GetResult();
                if (snapshot is not null)
                {
                    snapshots.Add(snapshot);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Ignored an unreadable Android snapshot.", ex);
            }
        }
        return snapshots;
    }

    public async Task<AndroidPackageSnapshot?> ReadSnapshotAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await ReadJsonSnapshotAsync(path).ConfigureAwait(false);
    }

    public async Task<PackageBatchResult> RestoreSnapshotAsync(
        AdbDevice device,
        AdbUser user,
        AndroidPackageSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PackageActionRequest> requests = snapshot.Packages
            .Select(entry => new PackageActionRequest(
                entry.PackageId,
                entry.State switch
                {
                    PackageState.Enabled => PackageAction.Enable,
                    PackageState.Disabled => PackageAction.Disable,
                    PackageState.Uninstalled => PackageAction.Restore,
                    _ => PackageAction.Enable,
                }))
            .ToArray();
        PackageBatchResult result = await _engine.ApplyAsync(
            device,
            user.Id,
            requests,
            new PackageApplyOptions(),
            cancellationToken).ConfigureAwait(false);
        await AppendLogAsync(device, user, result.Results, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    public async Task AppendLogAsync(
        AdbDevice device,
        AdbUser user,
        IReadOnlyList<PackageActionResult> results,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
        RotateLogIfNeeded();
        IEnumerable<string> lines = results.Select(result =>
            $"{DateTimeOffset.Now:O}\tserial={HashSerial(device.Serial)}\t" +
            $"model={Sanitize(device.Model ?? device.DisplayName)}\tuser={user.Id}\t" +
            $"package={result.PackageId}\taction={result.Action}\t" +
            $"result={result.Status}:{Sanitize(result.Message)}");
        await File.AppendAllLinesAsync(
            _logFilePath,
            lines,
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<AndroidSelectionImportResult> ImportSelectionAsync(
        string path,
        UadListCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        string text = await File.ReadAllTextAsync(
            path,
            Encoding.UTF8,
            cancellationToken).ConfigureAwait(false);
        HashSet<string> ids;
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            JsonElement array = root.ValueKind == JsonValueKind.Object &&
                                 root.TryGetProperty("packages", out JsonElement packages)
                ? packages
                : root;
            ids = array.ValueKind == JsonValueKind.Array
                ? array.EnumerateArray()
                    .Select(element => element.ValueKind == JsonValueKind.String
                        ? element.GetString()
                        : element.TryGetProperty("id", out JsonElement id)
                            ? id.GetString()
                            : null)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
        else
        {
            ids = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
        }

        string[] known = ids
            .Where(id => catalog.ById.ContainsKey(id) ||
                         AdbCommandLine.IsValidPackageId(id))
            .ToArray();
        string[] unknown = ids
            .Where(id => !catalog.ById.ContainsKey(id))
            .ToArray();
        return new AndroidSelectionImportResult(
            known,
            unknown,
            unknown.Length == 0
                ? $"{known.Length} package id(s) imported."
                : $"{known.Length} package id(s) imported; {unknown.Length} unknown id(s) ignored.");
    }

    public async Task ExportSelectionAsync(
        string path,
        IReadOnlyList<string> packageIds,
        bool upstreamTextFormat,
        CancellationToken cancellationToken = default)
    {
        string[] ids = packageIds
            .Where(AdbCommandLine.IsValidPackageId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (upstreamTextFormat)
        {
            await File.WriteAllLinesAsync(
                path,
                ids,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await WriteJsonAsync(path, new { packages = ids }, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<AndroidPackageSnapshot?> ReadJsonSnapshotAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AndroidPackageSnapshot>(
            stream,
            JsonOptions).ConfigureAwait(false);
    }

    private async Task WriteJsonAsync(
        string path,
        object value,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);
        await JsonSerializer.SerializeAsync(
            stream,
            value,
            value.GetType(),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private void RotateLogIfNeeded()
    {
        if (!File.Exists(_logFilePath))
        {
            return;
        }
        if (new FileInfo(_logFilePath).Length < 5 * 1024 * 1024)
        {
            return;
        }
        File.Move(_logFilePath, _logFilePath + ".1", overwrite: true);
    }

    private static string HashSerial(string serial)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(serial));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
}
