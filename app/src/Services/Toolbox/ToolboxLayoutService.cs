using System.Text.Json;
using PCManager.Core;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Toolbox;

public sealed class ToolboxLayoutService : IToolboxLayoutService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _gate = new();
    private readonly List<ToolboxLayoutEntry> _entries;

    public ToolboxLayoutService()
        : this(Path.Combine(AppInfo.DataFolder, "toolbox.layout.json"))
    {
    }

    public ToolboxLayoutService(string path)
    {
        _path = path;
        _entries = LoadOrDefault();
    }

    public IReadOnlyList<ToolboxLayoutEntry> Layout
    {
        get
        {
            lock (_gate)
            {
                return _entries.Select(Clone).ToArray();
            }
        }
    }

    public event EventHandler? Changed;

    public void SetEnabled(string id, bool enabled)
    {
        ToolboxLayoutEntry? entry = Find(id);
        if (entry is null)
        {
            return;
        }

        if (entry.Enabled == enabled)
        {
            return;
        }

        entry.Enabled = enabled;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Move(string id, int offset)
    {
        lock (_gate)
        {
            int index = _entries.FindIndex(entry =>
                string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
            int target = index + offset;
            if (index < 0 || target < 0 || target >= _entries.Count)
            {
                return false;
            }

            ToolboxLayoutEntry entry = _entries[index];
            _entries.RemoveAt(index);
            _entries.Insert(target, entry);
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Reset()
    {
        lock (_gate)
        {
            _entries.Clear();
            _entries.AddRange(DefaultLayout());
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ExportAsync(string path, CancellationToken cancellationToken = default)
    {
        ToolboxLayoutFile file = Snapshot();
        await WriteAsync(path, file, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        ToolboxLayoutFile? file = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (file is null || file.Version != 1)
        {
            return false;
        }

        Dictionary<string, ToolboxLayoutEntry> allowed =
            DefaultLayout().ToDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase);
        var entries = new List<ToolboxLayoutEntry>();
        var importedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ToolboxLayoutEntry entry in file.Entries)
        {
            if (!allowed.TryGetValue(entry.Id, out ToolboxLayoutEntry? defaultEntry))
            {
                continue;
            }
            if (!importedIds.Add(entry.Id))
            {
                continue;
            }

            entries.Add(new ToolboxLayoutEntry
            {
                Id = defaultEntry.Id,
                Title = defaultEntry.Title,
                Icon3DKey = defaultEntry.Icon3DKey,
                Enabled = entry.Enabled,
                Kind = defaultEntry.Kind,
            });
        }

        if (entries.Count != allowed.Count)
        {
            return false;
        }

        lock (_gate)
        {
            _entries.Clear();
            _entries.AddRange(entries);
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private ToolboxLayoutEntry? Find(string id)
    {
        lock (_gate)
        {
            return _entries.FirstOrDefault(entry =>
                string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    private ToolboxLayoutFile Snapshot()
    {
        lock (_gate)
        {
            return new ToolboxLayoutFile
            {
                Version = 1,
                Entries = _entries.Select(Clone).ToList(),
            };
        }
    }

    private List<ToolboxLayoutEntry> LoadOrDefault()
    {
        try
        {
            if (File.Exists(_path))
            {
                using FileStream stream = File.OpenRead(_path);
                ToolboxLayoutFile? file =
                    JsonSerializer.Deserialize<ToolboxLayoutFile>(stream, SerializerOptions);
                if (file is { Version: 1 } &&
                    IsCompleteLayout(file.Entries))
                {
                    return file.Entries.Select(Clone).ToList();
                }
            }
        }
        catch
        {
        }

        return DefaultLayout();
    }

    private static bool IsCompleteLayout(IEnumerable<ToolboxLayoutEntry> entries)
    {
        HashSet<string> allowed =
            DefaultLayout().Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> supplied =
            entries.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return supplied.SetEquals(allowed);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temp = _path + ".tmp";
            File.WriteAllText(temp,
                JsonSerializer.Serialize(Snapshot(), SerializerOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch
        {
        }
    }

    private static async Task WriteAsync(
        string path,
        ToolboxLayoutFile file,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(file, SerializerOptions),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ToolboxLayoutFile?> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ToolboxLayoutFile>(
            stream,
            SerializerOptions,
            cancellationToken).ConfigureAwait(false);
    }

    private static ToolboxLayoutEntry Clone(ToolboxLayoutEntry entry) =>
        new()
        {
            Id = entry.Id,
            Title = entry.Title,
            Icon3DKey = entry.Icon3DKey,
            Enabled = entry.Enabled,
            Kind = entry.Kind,
            Status = entry.Status,
        };

    private static List<ToolboxLayoutEntry> DefaultLayout() =>
        new()
        {
            new() { Id = "metric_ram", Title = "RAM", Icon3DKey = "Memory", Kind = ToolboxLayoutKind.Metric, Status = "Live memory percentage" },
            new() { Id = "metric_battery", Title = "Battery", Icon3DKey = "Battery", Kind = ToolboxLayoutKind.Metric, Status = "Battery status or no-battery reason" },
            new() { Id = "metric_temperature", Title = "Temperature", Icon3DKey = "Temperature", Kind = ToolboxLayoutKind.Metric, Status = "System thermal zone or unavailable reason" },
            new() { Id = "action_boost", Title = "Boost", Icon3DKey = "Boost", Kind = ToolboxLayoutKind.Action, Status = "Manual memory/temp action" },
            new() { Id = "tool_screenshot", Title = "Screenshot", Icon3DKey = "Screenshot", Kind = ToolboxLayoutKind.Tool, Status = "Capture every screen" },
            new() { Id = "tool_notepad", Title = "Notepad", Icon3DKey = "Notepad", Kind = ToolboxLayoutKind.Tool, Status = "Open Notepad" },
            new() { Id = "tool_calculator", Title = "Calculator", Icon3DKey = "Calculator", Kind = ToolboxLayoutKind.Tool, Status = "Open Calculator" },
            new() { Id = "tool_taskmgr", Title = "Task Manager", Icon3DKey = "ProcessList", Kind = ToolboxLayoutKind.Tool, Status = "Open Task Manager" },
            new() { Id = "tool_quick_links", Title = "Quick links", Icon3DKey = "QuickLinks", Kind = ToolboxLayoutKind.Tool, Status = "Open configured web links" },
        };
}
