using PCManager.Core.Formatting;
using PCManager.Core.Models;
using PCManager.Services.Abstractions;

namespace PCManager.Services.Boost;

/// <summary>
/// One Boost run for the whole app (U7/W5). Home and the floating toolbar both call this, so they
/// share the state machine, the temporary-file rule and the result text instead of each running
/// their own trim.
/// </summary>
public sealed class BoostCoordinator
{
    private readonly IPcBoostService _boost;
    private readonly IAppLogger _logger;
    private readonly object _gate = new();
    private CancellationTokenSource? _runCancellation;

    public BoostCoordinator(IPcBoostService boost, IAppLogger logger)
    {
        _boost = boost;
        _logger = logger;
    }

    public event EventHandler? Changed;

    public BoostPhase Phase { get; private set; } = BoostPhase.Idle;

    public string Headline { get; private set; } = "Ready";

    public string ResultText { get; private set; } = string.Empty;

    public long TempFreedBytes { get; private set; }

    public MemoryTrimResult? LastResult { get; private set; }

    public bool IsBusy => BoostPolicy.IsBusy(Phase);

    public void Cancel()
    {
        lock (_gate)
        {
            if (!IsBusy)
            {
                return;
            }

            _runCancellation?.Cancel();
        }
    }

    /// <summary>Runs one Boost. A second press while busy is ignored rather than queued.</summary>
    public async Task RunAsync(BoostTrigger trigger, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (IsBusy)
            {
                return;
            }

            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            SetPhase(BoostPhase.Measuring, "Measuring memory…");
        }

        CancellationToken runToken = _runCancellation!.Token;
        try
        {
            MemorySnapshot before = _boost.GetMemorySnapshot();
            SetPhase(BoostPhase.Working, "Trimming process working sets…");

            MemoryTrimResult trim = await _boost.OptimizeAsync(runToken).ConfigureAwait(false);
            long freed = 0;

            // The shared rule: only an explicit Boost press may delete temporary files.
            if (BoostPolicy.AllowsTempCleanup(trigger))
            {
                SetPhase(BoostPhase.Working, "Releasing temporary files…");
                freed = await _boost.CleanTempFilesAsync(runToken).ConfigureAwait(false);
            }

            LastResult = trim;
            TempFreedBytes = freed;
            ResultText =
                $"{trim.WorkingSetsTrimmed} working sets · {ByteFormatter.Format(freed)} temp removed";
            SetPhase(BoostPhase.Completed, Describe(trim, freed));
            _ = ReturnToIdleAfterResultAsync();

            _logger.Info(
                $"Boost ({trigger}): {trim.WorkingSetsTrimmed} working sets trimmed, available " +
                $"{before.AvailableBytes} -> {trim.After.AvailableBytes} bytes, {freed} bytes of temp removed.");
        }
        catch (OperationCanceledException)
        {
            SetPhase(BoostPhase.Idle, "Boost cancelled.");
        }
        catch (Exception ex)
        {
            SetPhase(BoostPhase.Failed, $"Boost failed: {ex.Message}");
            _logger.Error("Boost failed.", ex);
        }
        finally
        {
            lock (_gate)
            {
                _runCancellation?.Dispose();
                _runCancellation = null;
            }
        }
    }

    private async Task ReturnToIdleAfterResultAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        lock (_gate)
        {
            if (Phase == BoostPhase.Completed)
            {
                SetPhase(BoostPhase.Idle, "Ready");
            }
        }
    }

    private static string Describe(MemoryTrimResult trim, long freed)
    {
        string memory =
            $"{ByteFormatter.Format((long)trim.Before.AvailableBytes)} → " +
            $"{ByteFormatter.Format((long)trim.After.AvailableBytes)} available";
        return $"{trim.WorkingSetsTrimmed} working sets trimmed · {ByteFormatter.Format(freed)} temp removed · {memory}";
    }

    private void SetPhase(BoostPhase phase, string headline)
    {
        Phase = phase;
        Headline = headline;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
