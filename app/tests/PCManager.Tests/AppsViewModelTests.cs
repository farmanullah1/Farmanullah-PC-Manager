using PCManager.Core.Models;
using PCManager.ViewModels;
using Xunit;

namespace PCManager.Tests;

public sealed class AppsViewModelTests
{
    [Fact]
    public void Process_filter_is_case_insensitive_and_name_sort_is_stable()
    {
        RunningAppViewModel[] apps =
        {
            App("Code", 100, 20),
            App("Chrome", 500, 10),
            App("Calendar", 200, 5),
        };

        IReadOnlyList<RunningAppViewModel> filtered =
            AppsViewModel.FilterAndSortProcesses(apps, "c", "Name");

        Assert.Equal(new[] { "Calendar", "Chrome", "Code" }, filtered.Select(app => app.Name));
    }

    [Fact]
    public void Process_sort_supports_memory_and_cpu()
    {
        RunningAppViewModel[] apps =
        {
            App("Small", 100, 30),
            App("Large", 900, 10),
            App("Busy", 200, 80),
        };

        Assert.Equal("Large",
            AppsViewModel.FilterAndSortProcesses(apps, null, "Memory").First().Name);
        Assert.Equal("Busy",
            AppsViewModel.FilterAndSortProcesses(apps, null, "CPU").First().Name);
    }

    [Fact]
    public void Process_filter_can_hide_every_matching_row_without_fake_results()
    {
        RunningAppViewModel[] apps =
        {
            App("Code", 100, 20),
            App("Chrome", 500, 10),
        };

        Assert.Empty(
            AppsViewModel.FilterAndSortProcesses(apps, "missing", "Memory"));
    }

    private static RunningAppViewModel App(string name, long memory, double cpu) =>
        new(
            new ProcessInfo
            {
                ProcessId = name.GetHashCode(),
                Name = name,
                MemoryBytes = memory,
                CpuPercent = cpu,
            },
            _ => Task.CompletedTask);
}
