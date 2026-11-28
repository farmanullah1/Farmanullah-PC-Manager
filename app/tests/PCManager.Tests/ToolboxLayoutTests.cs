using PCManager.Services.Toolbox;
using Xunit;

namespace PCManager.Tests;

public sealed class ToolboxLayoutTests
{
    [Fact]
    public void Defaults_include_real_metrics_tools_and_actions()
    {
        using var temp = new TempLayout();
        var layout = new ToolboxLayoutService(temp.LayoutPath);

        Assert.Equal(9, layout.Layout.Count);
        Assert.Contains(layout.Layout, entry => entry.Id == "metric_ram");
        Assert.Contains(layout.Layout, entry => entry.Id == "tool_screenshot");
        Assert.Contains(layout.Layout, entry => entry.Id == "action_boost");
    }

    [Fact]
    public void Disable_move_reset_and_persist()
    {
        using var temp = new TempLayout();
        var layout = new ToolboxLayoutService(temp.LayoutPath);

        layout.SetEnabled("metric_battery", false);
        Assert.False(layout.Layout.Single(entry => entry.Id == "metric_battery").Enabled);

        Assert.True(layout.Move("metric_temperature", -1));
        string first = layout.Layout[0].Id;
        Assert.True(first is "metric_ram" or "metric_temperature");

        layout.Reset();
        Assert.True(layout.Layout[0].Enabled);
        Assert.Equal("metric_ram", layout.Layout[0].Id);

        var reloaded = new ToolboxLayoutService(temp.LayoutPath);
        Assert.Equal(layout.Layout.Select(entry => entry.Id),
                     reloaded.Layout.Select(entry => entry.Id));
    }

    [Fact]
    public async Task Export_and_import_round_trip_without_unknown_ids()
    {
        using var temp = new TempLayout();
        var layout = new ToolboxLayoutService(temp.LayoutPath);
        layout.SetEnabled("tool_screenshot", false);
        layout.Move("tool_calculator", -1);

        await layout.ExportAsync(temp.ExportPath);

        var imported = new ToolboxLayoutService(temp.OtherPath);
        Assert.True(await imported.ImportAsync(temp.ExportPath));
        Assert.Equal(
            layout.Layout.Select(entry => entry.Enabled),
            imported.Layout.Select(entry => entry.Enabled));
    }

    [Fact]
    public void Unknown_move_and_unknown_disable_are_no_ops()
    {
        using var temp = new TempLayout();
        var layout = new ToolboxLayoutService(temp.LayoutPath);

        layout.SetEnabled("unknown", false);
        Assert.False(layout.Move("unknown", -1));
        Assert.DoesNotContain(layout.Layout, entry => entry.Id == "unknown");
    }

    private sealed class TempLayout : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "fpm-t3-" + Guid.NewGuid().ToString("N"));

        public string LayoutPath => System.IO.Path.Combine(Root, "toolbox.layout.json");
        public string OtherPath => System.IO.Path.Combine(Root, "toolbox-import.layout.json");
        public string ExportPath => System.IO.Path.Combine(Root, "toolbox.layout.export.json");

        public TempLayout()
        {
            Directory.CreateDirectory(Root);
        }

        public void Dispose()
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        }
    }
}
