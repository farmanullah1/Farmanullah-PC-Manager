using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace PCManager.Services;

/// <summary>
/// Single-instance hand-off (W3). The first process listens on a per-user pipe; a second launch
/// sends <c>SHOW</c> and exits, so the running instance (full UI or toolbar-only) can respond
/// instead of a competing process starting.
/// </summary>
public sealed class SingleInstanceChannel : IDisposable
{
    public const string ShowMessage = "SHOW";
    public const string ExitMessage = "EXIT";

    private readonly string _pipeName;
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private NamedPipeServerStream? _pending;

    public SingleInstanceChannel()
        : this(BuildPipeName())
    {
    }

    public SingleInstanceChannel(string pipeName) => _pipeName = pipeName;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public string PipeName => _pipeName;

    public static string BuildPipeName()
    {
        string sid;

        try
        {
            sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        }
        catch (Exception)
        {
            // Locked-down token: still per-user enough for a session pipe.
            sid = Environment.UserName;
        }

        return $"FPM.Instance.{sid}";
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_cancellation is not null)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _ = Task.Run(() => ListenAsync(_cancellation.Token));
        }
    }

    /// <summary>True when another FPM instance answered and will show itself.</summary>
    public static bool TrySendShow(string pipeName, TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);

            client.Connect((int)Math.Max(100, timeout.TotalMilliseconds));

            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine(ShowMessage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool TrySendExit(string pipeName, TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);

            client.Connect((int)Math.Max(100, timeout.TotalMilliseconds));

            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            writer.WriteLine(ExitMessage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task ListenAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                lock (_gate)
                {
                    _pending = server;
                }

                await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                string? message = await reader.ReadLineAsync(token).ConfigureAwait(false);

                if (string.Equals(message, ShowMessage, StringComparison.Ordinal))
                {
                    ShowRequested?.Invoke();
                }
                else if (string.Equals(message, ExitMessage, StringComparison.Ordinal))
                {
                    ExitRequested?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // A dropped connection is normal; keep listening.
            }
            finally
            {
                lock (_gate)
                {
                    _pending?.Dispose();
                    _pending = null;
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _pending?.Dispose();
            _pending = null;
        }
    }
}
