using System.Diagnostics;

namespace PingWidget.Core.Metrics;

/// <summary>Finds the heaviest processes on a background loop; a failed or hung read clears the names.</summary>
public sealed class TopProcessMonitor : IDisposable
{
    public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(2);

    private readonly Func<IProcessSource> _sourceFactory;
    private readonly TimeSpan _readTimeout;
    private readonly object _sourceLock = new();
    private readonly object _pendingLock = new();
    private Task<TopProcesses>? _pendingRead;
    private IProcessSource? _source;
    private IReadOnlyList<ProcessSample>? _previous;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private volatile TopProcesses _current = TopProcesses.None;

    public TopProcessMonitor(Func<IProcessSource> sourceFactory, TimeSpan? readTimeout = null)
    {
        _sourceFactory = sourceFactory;
        _readTimeout = readTimeout ?? DefaultReadTimeout;
    }

    public TopProcesses Current => _current;

    public async Task TickAsync()
    {
        Task<TopProcesses> read;
        lock (_pendingLock)
        {
            // A read still hanging from an earlier tick is awaited again, never duplicated.
            if (_pendingRead is not { IsCompleted: false }) _pendingRead = Task.Run(ReadOnce);
            read = _pendingRead;
        }

        var finished = await Task.WhenAny(read, Task.Delay(_readTimeout)).ConfigureAwait(false);
        _current = finished == read ? await read.ConfigureAwait(false) : TopProcesses.None;
    }

    private TopProcesses ReadOnce()
    {
        lock (_sourceLock)
        {
            if (_disposed) return TopProcesses.None;
            try
            {
                _source ??= _sourceFactory();
                var snapshot = _source.Read();
                var top = TopProcessPicker.Pick(_previous, snapshot.Processes, snapshot.GpuLoadByPid);
                _previous = snapshot.Processes;
                return top;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Process read failed: {e}");
                DisposeSource();
                return TopProcesses.None;
            }
        }
    }

    private void DisposeSource()
    {
        try { _source?.Dispose(); }
        catch (Exception e) { Debug.WriteLine($"Process source dispose failed: {e}"); }
        _source = null;
        _previous = null;
    }

    /// <summary>Ticks immediately, then once per interval.</summary>
    public void Start(TimeSpan interval)
    {
        Stop();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = RunAsync(interval, cts.Token);
    }

    private async Task RunAsync(TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                try { await TickAsync().ConfigureAwait(false); }
                catch (Exception e) { Debug.WriteLine($"TopProcessMonitor loop: {e}"); }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) { }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        Stop();
        // Don't hang shutdown behind a stuck read.
        if (!Monitor.TryEnter(_sourceLock, TimeSpan.FromMilliseconds(500))) return;
        try
        {
            _disposed = true;
            DisposeSource();
        }
        finally { Monitor.Exit(_sourceLock); }
    }
}
