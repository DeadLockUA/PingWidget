using System.Diagnostics;

namespace PingWidget.Core.Metrics;

/// <summary>Samples hardware metrics once per interval, off the UI thread, into one history per metric.</summary>
public sealed class HardwareMonitor : IDisposable
{
    /// <summary>After the source fails to open, this many ticks are skipped before trying again.</summary>
    public const int RetryAfterTicks = 30;

    private readonly Func<IHardwareSource> _sourceFactory;
    private readonly Dictionary<MetricKind, RingBuffer<MetricReading>> _histories =
        Enum.GetValues<MetricKind>().ToDictionary(k => k, _ => new RingBuffer<MetricReading>());
    private readonly object _sourceLock = new();
    private IHardwareSource? _source;
    private int _skipTicks;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>A read slower than this counts as "no data"; it is not restarted while still running.</summary>
    public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromSeconds(2);

    private readonly TimeSpan _readTimeout;
    private readonly object _pendingLock = new();
    private Task<IReadOnlyDictionary<MetricKind, MetricReading>?>? _pendingRead;

    public HardwareMonitor(Func<IHardwareSource> sourceFactory, TimeSpan? readTimeout = null)
    {
        _sourceFactory = sourceFactory;
        _readTimeout = readTimeout ?? DefaultReadTimeout;
    }

    public event EventHandler? Updated;

    public RingBuffer<MetricReading> HistoryFor(MetricKind kind) => _histories[kind];

    public async Task TickAsync()
    {
        Task<IReadOnlyDictionary<MetricKind, MetricReading>?> read;
        lock (_pendingLock)
        {
            // A read still hanging from an earlier tick is awaited again, never duplicated.
            if (_pendingRead is not { IsCompleted: false }) _pendingRead = Task.Run(ReadOnce);
            read = _pendingRead;
        }

        IReadOnlyDictionary<MetricKind, MetricReading>? picked = null;
        try
        {
            var finished = await Task.WhenAny(read, Task.Delay(_readTimeout)).ConfigureAwait(false);
            if (finished == read) picked = await read.ConfigureAwait(false);
            else Debug.WriteLine("Hardware read timed out");
        }
        catch (Exception e)
        {
            Debug.WriteLine($"HardwareMonitor tick failed: {e}");
        }

        foreach (var (kind, history) in _histories)
            history.Add(picked is not null && picked.TryGetValue(kind, out var r) ? r : MetricReading.Missing);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyDictionary<MetricKind, MetricReading>? ReadOnce()
    {
        lock (_sourceLock)
        {
            if (_disposed) return null;
            if (_source is null)
            {
                if (_skipTicks > 0)
                {
                    _skipTicks--;
                    return null;
                }
                try { _source = _sourceFactory(); }
                catch (Exception e)
                {
                    Debug.WriteLine($"Hardware source failed to open: {e}");
                    _skipTicks = RetryAfterTicks;
                    return null;
                }
            }

            try { return SensorPicker.Pick(_source.ReadSensors()); }
            catch (Exception e)
            {
                Debug.WriteLine($"Hardware read failed: {e}");
                DisposeSource();
                return null;
            }
        }
    }

    private void DisposeSource()
    {
        try { _source?.Dispose(); }
        catch (Exception e) { Debug.WriteLine($"Hardware source dispose failed: {e}"); }
        _source = null;
    }

    /// <summary>Ticks immediately, then once per interval; ticks never overlap (same pattern as PingMonitor).</summary>
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
                catch (Exception e) { Debug.WriteLine($"HardwareMonitor loop: {e}"); }
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
        // Don't hang shutdown behind a stuck sensor read.
        if (!Monitor.TryEnter(_sourceLock, TimeSpan.FromMilliseconds(500))) return;
        try
        {
            _disposed = true;
            DisposeSource();
        }
        finally { Monitor.Exit(_sourceLock); }
    }
}
