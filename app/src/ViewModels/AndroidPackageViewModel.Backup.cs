using System.Diagnostics;
using System.Text;
using Microsoft.Win32;
using PCManager.Core;
using PCManager.Core.Android;
using PCManager.Services.Android;

namespace PCManager.ViewModels;

public sealed partial class AndroidPackageViewModel
{
    private void RecommendedPreset()
    {
        foreach (AndroidPackageRow row in Packages)
        {
            row.IsSelected = row.RemovalText.Equals(
                "Recommended",
                StringComparison.OrdinalIgnoreCase);
        }
        UpdateSelection();
        ResultText = "Recommended preset selected. Review before applying.";
    }

    private async Task CreateSnapshotAsync()
    {
        if (_device is null || SelectedUser is null || _scan is null)
        {
            return;
        }

        AndroidPackageSnapshot snapshot =
            await _backup.CreateSnapshotAsync(_device, SelectedUser, _scan);
        RefreshSnapshots();
        ResultText = $"Snapshot created: {snapshot.Id}";
    }

    private async Task RestoreSnapshotAsync()
    {
        if (_device is null || SelectedUser is null || SelectedSnapshot is null)
        {
            return;
        }
        if (!_confirmation.Confirm(
                "Restore Android package snapshot",
                $"Restore snapshot {SelectedSnapshot.Id} for this user?\n\n" +
                "Missing packages are reported rather than assumed restored."))
        {
            ResultText = "Snapshot restore cancelled.";
            return;
        }

        string path = Path.Combine(
            AppInfo.DataFolder,
            "android",
            "backups",
            SelectedSnapshot.Id + ".json");
        AndroidPackageSnapshot? snapshot =
            await _backup.ReadSnapshotAsync(path);
        if (snapshot is null)
        {
            ResultText = "Snapshot is unavailable.";
            return;
        }

        PackageBatchResult result =
            await _backup.RestoreSnapshotAsync(_device, SelectedUser, snapshot);
        ResultText =
            "Snapshot restore: " + result.Message + "\n" +
            $"{result.OkCount} applied · {result.AlreadyCount} already · " +
            $"{result.FailedCount} failed · {result.BlockedCount} blocked";
        await RefreshAsync();
    }

    private void RefreshSnapshots()
    {
        Snapshots.Clear();
        foreach (AndroidPackageSnapshot snapshot in _backup.ListSnapshots())
        {
            Snapshots.Add(snapshot);
        }
        SelectedSnapshot ??= Snapshots.FirstOrDefault();
    }

    private void ExportText()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Android package selection",
            FileName = "uad_exported_selection.txt",
            Filter = "UAD selection (*.txt)|*.txt|Text (*.txt)|*.txt",
            AddExtension = true,
        };
        if (dialog.ShowDialog() == true)
        {
            _ = ExportSelectionAsync(dialog.FileName, true);
        }
    }

    private void ExportJson()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export FPM Android package selection",
            FileName = "fpm_android_selection.json",
            Filter = "FPM selection (*.json)|*.json|JSON (*.json)|*.json",
            AddExtension = true,
        };
        if (dialog.ShowDialog() == true)
        {
            _ = ExportSelectionAsync(dialog.FileName, false);
        }
    }

    private async Task ExportSelectionAsync(
        string path,
        bool upstreamTextFormat)
    {
        string[] ids = Packages
            .Where(row => row.IsSelected)
            .Select(row => row.Id)
            .ToArray();
        await _backup.ExportSelectionAsync(path, ids, upstreamTextFormat);
        ResultText = $"Selection exported: {ids.Length} package id(s).";
    }

    private void ImportSelection()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Android package selection",
            Filter = "Package selections (*.txt;*.json)|*.txt;*.json|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog() == true)
        {
            _ = ImportSelectionAsync(dialog.FileName);
        }
    }

    private async Task ImportSelectionAsync(string path)
    {
        UadListCatalog catalog = await _lists.LoadAsync();
        AndroidSelectionImportResult result =
            await _backup.ImportSelectionAsync(path, catalog);
        HashSet<string> ids =
            result.PackageIds.ToHashSet(StringComparer.Ordinal);
        foreach (AndroidPackageRow row in Packages)
        {
            row.IsSelected = ids.Contains(row.Id);
        }
        UpdateSelection();
        ResultText = result.Message;
    }

    private void OpenLog()
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(_backup.LogFilePath)!);
            if (!File.Exists(_backup.LogFilePath))
            {
                File.WriteAllText(
                    _backup.LogFilePath,
                    "FPM Android action log" + Environment.NewLine,
                    Encoding.UTF8);
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = _backup.LogFilePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ResultText = "Could not open the Android action log: " + ex.Message;
        }
    }
}
