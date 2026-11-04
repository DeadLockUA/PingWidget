using System.Diagnostics;

namespace PingWidget.Core;

/// <summary>Pings every configured host once per interval and keeps a sample history per host.</summary>
public sealed class PingMonitor : IDisposable
{
    public const int TimeoutMs = 1000;

    private readonly IPinger _pinger;
    private readonly Func<DateTime> _clock;
    private readonly object _lock = new();
    private Dictionary<string, SampleHistory> _histories = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<HostConfig> _hosts = [];
    private CancellationTokenSource? _cts;

    public PingMonitor(IPinger pinger, Func<DateTime> clock)
    {
        _pinger = pinger;
        _clock = clock;
    }

    public event EventHandler? Updated;

    public IReadOnlyList<HostConfig> Hosts { get { lock (_lock) return _hosts; } }

    private static string Key(string address) => address.Trim();

    public void SetHosts(IReadOnlyList<HostConfig> hosts)
    {
        lock (_lock)
        {
            var next = new Dictionary<string, SampleHistory>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in hosts)
            {
                var key = Key(h.Address);
                if (next.ContainsKey(key)) continue;
                next[key] = _histories.TryGetValue(key, out var existing) ? existing : new SampleHistory();
            }
            _histories = next;
            _hosts = hosts.ToArray();
        }
    }

    public SampleHistory HistoryFor(string address)
    {
        lock (_lock)
        {
            return _histories.TryGetValue(Key(address), out var h)
                ? h
                : throw new KeyNotFoundException($"Host '{address}' is not monitored.");
        }
    }

    public async Task TickAsync()
    {
        var hosts = Hosts;
        var results = await Task.WhenAll(hosts.Select(h => SafePing(Key(h.Address)))).ConfigureAwait(false);
        var now = _clock();
        lock (_lock)
        {
            for (var i = 0; i < hosts.Count; i++)
            {
                // Hosts removed while the tick was in flight are simply dropped.
                if (_histories.TryGetValue(Key(hosts[i].Address), out var history))
                    history.Add(new PingSample(now, results[i]));
            }
        }
        Updated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Extra time allowed beyond the ICMP timeout (covers DNS resolution) before a ping counts as lost.</summary>
    private const int HardLimitSlackMs = 500;

    private readonly Dictionary<string, Task<long?>> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pings one host with a hard time limit, so a hanging lookup can't stall the other hosts.
    /// While a previous ping for the host is still running, no new one is started and the sample counts as lost.
    /// </summary>
    private async Task<long?> SafePing(string address)
    {
        Task<long?> ping;
        lock (_inFlight)
        {
            if (_inFlight.TryGetValue(address, out var previous) && !previous.IsCompleted) return null;
            ping = GuardedPing(address);
            _inFlight[address] = ping;
        }

        var finished = await Task.WhenAny(ping, Task.Delay(TimeoutMs + HardLimitSlackMs)).ConfigureAwait(false);
        return finished == ping ? await ping.ConfigureAwait(false) : null;
    }

    private async Task<long?> GuardedPing(string address)
    {
        try { return await _pinger.PingAsync(address, TimeoutMs).ConfigureAwait(false); }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Ticks immediately, then once per interval. Ticks run one at a time; periods missed while a
    /// slow tick was running are coalesced (PeriodicTimer semantics), so ticks never pile up.
    /// </summary>
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
                catch (Exception e) { Debug.WriteLine($"PingMonitor tick failed: {e}"); }
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

    public void Dispose() => Stop();
}
