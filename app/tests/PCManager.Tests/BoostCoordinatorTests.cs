using PCManager.Core.Models;
using PCManager.Services.Abstractions;
using PCManager.Services.Boost;
using Xunit;

namespace PCManager.Tests;

public sealed class BoostCoordinatorTests
{
    private sealed class FakeBoost : IPcBoostService
    {
        public int TrimCalls { get; private set; }
        public int TempCalls { get; private set; }
        public bool FailTrim { get; set; }

        public MemorySnapshot GetMemorySnapshot() => new()
        {
            LoadPercent = 50,
            AvailableBytes = 8UL * 1024 * 1024 * 1024,
            TotalPhysicalBytes = 16UL * 1024 * 1024 * 1024,
        };

        public async Task<MemoryTrimResult> OptimizeAsync(CancellationToken cancellationToken = default)
        {
            TrimCalls++;
            await Task.Delay(20, cancellationToken);

            if (FailTrim)
            {
                throw new InvalidOperationException("trim exploded");
            }

            var before = new MemorySnapshot
            {
                LoadPercent = 60,
                AvailableBytes = 6UL * 1024 * 1024 * 1024,
                TotalPhysicalBytes = 16UL * 1024 * 1024 * 1024,
            };
            var after = new MemorySnapshot
            {
                LoadPercent = 40,
                AvailableBytes = 10UL * 1024 * 1024 * 1024,
                TotalPhysicalBytes = 16UL * 1024 * 1024 * 1024,
            };
            return new MemoryTrimResult { Before = before, After = after, WorkingSetsTrimmed = 12 };
        }

        public Task<long> CleanTempFilesAsync(CancellationToken cancellationToken = default)
        {
            TempCalls++;
            return Task.FromResult(3L * 1024 * 1024);
        }
    }

    [Fact]
    public async Task ManualBoostTrimsAndDeletesTemporaryFiles()
    {
        var boost = new FakeBoost();
        var coordinator = new BoostCoordinator(boost, new TestLogger());

        await coordinator.RunAsync(BoostTrigger.Manual);

        Assert.Equal(BoostPhase.Completed, coordinator.Phase);
        Assert.Equal(1, boost.TrimCalls);
        Assert.Equal(1, boost.TempCalls);
        Assert.Equal(3L * 1024 * 1024, coordinator.TempFreedBytes);
        Assert.Contains("12 working sets", coordinator.ResultText);
    }

    [Fact]
    public async Task AutomaticBoostNeverTouchesFiles()
    {
        var boost = new FakeBoost();
        var coordinator = new BoostCoordinator(boost, new TestLogger());

        await coordinator.RunAsync(BoostTrigger.Automatic);

        Assert.Equal(BoostPhase.Completed, coordinator.Phase);
        Assert.Equal(1, boost.TrimCalls);
        Assert.Equal(0, boost.TempCalls);
        Assert.Equal(0, coordinator.TempFreedBytes);
    }

    [Fact]
    public void OnlyManualBoostAllowsTempCleanup()
    {
        Assert.True(BoostPolicy.AllowsTempCleanup(BoostTrigger.Manual));
        Assert.False(BoostPolicy.AllowsTempCleanup(BoostTrigger.Automatic));
    }

    [Fact]
    public async Task FailureEndsInTheFailedPhaseWithTheReason()
    {
        var boost = new FakeBoost { FailTrim = true };
        var coordinator = new BoostCoordinator(boost, new TestLogger());

        await coordinator.RunAsync(BoostTrigger.Manual);

        Assert.Equal(BoostPhase.Failed, coordinator.Phase);
        Assert.Contains("trim exploded", coordinator.Headline);
        Assert.Equal(0, boost.TempCalls);
    }

    [Fact]
    public async Task DuplicatePressesDoNotQueueASecondRun()
    {
        var boost = new FakeBoost();
        var coordinator = new BoostCoordinator(boost, new TestLogger());

        await Task.WhenAll(
            coordinator.RunAsync(BoostTrigger.Manual),
            coordinator.RunAsync(BoostTrigger.Manual));

        Assert.Equal(1, boost.TrimCalls);
    }

    [Fact]
    public async Task ChangedIsRaisedForEveryPhase()
    {
        var coordinator = new BoostCoordinator(new FakeBoost(), new TestLogger());
        var phases = new List<BoostPhase>();
        coordinator.Changed += (_, _) => phases.Add(coordinator.Phase);

        await coordinator.RunAsync(BoostTrigger.Manual);

        Assert.Contains(BoostPhase.Measuring, phases);
        Assert.Contains(BoostPhase.Working, phases);
        Assert.Equal(BoostPhase.Completed, phases[^1]);
    }

    [Fact]
    public async Task CancelStopsAnActiveRunAndRemainsHonest()
    {
        var boost = new FakeBoost();
        var coordinator = new BoostCoordinator(boost, new TestLogger());

        Task run = coordinator.RunAsync(BoostTrigger.Manual);
        coordinator.Cancel();
        await run;

        Assert.Equal(BoostPhase.Idle, coordinator.Phase);
        Assert.Equal("Boost cancelled.", coordinator.Headline);
    }

    [Fact]
    public async Task CompletedResultReturnsToIdleAfterSettlement()
    {
        var coordinator = new BoostCoordinator(new FakeBoost(), new TestLogger());

        await coordinator.RunAsync(BoostTrigger.Automatic);
        Assert.Equal(BoostPhase.Completed, coordinator.Phase);

        await Task.Delay(TimeSpan.FromMilliseconds(2100));
        Assert.Equal(BoostPhase.Idle, coordinator.Phase);
    }
}
