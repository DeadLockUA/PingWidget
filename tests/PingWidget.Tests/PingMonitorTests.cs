using System.Collections.Concurrent;
using PingWidget.Core;

namespace PingWidget.Tests;

internal sealed class FakePinger : IPinger
{
    private int _inFlight;
    public int PeakInFlight;
    public int Calls;
    public Func<string, Task<long?>> Respond { get; set; } = _ => Task.FromResult<long?>(1);

    public async Task<long?> PingAsync(string address, int timeoutMs)
    {
        Interlocked.Increment(ref Calls);
        var now = Interlocked.Increment(ref _inFlight);
        InterlockedMax(ref PeakInFlight, now);
        try { return await Respond(address); }
        finally { Interlocked.Decrement(ref _inFlight); }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value &&
               Interlocked.CompareExchange(ref target, value, current) != current) { }
    }
}

public class PingMonitorTests
{
    private static readonly HostConfig A = new("10.0.0.1", 10);
    private static readonly HostConfig B = new("10.0.0.2", 20);
    private static readonly HostConfig C = new("10.0.0.3", 30);

    private static PingMonitor Create(FakePinger p) => new(p, () => DateTime.UnixEpoch);

    [Fact]
    public async Task Tick_AppendsOneSamplePerHost()
    {
        var p = new FakePinger { Respond = a => Task.FromResult<long?>(a == A.Address ? 7 : 30) };
        using var m = Create(p);
        m.SetHosts([A, B]);
        await m.TickAsync();
        Assert.Equal(new long?[] { 7 }, m.HistoryFor(A.Address).Snapshot().Select(s => s.RoundtripMs));
        Assert.Equal(new long?[] { 30 }, m.HistoryFor(B.Address).Snapshot().Select(s => s.RoundtripMs));
    }

    [Fact]
    public async Task Tick_PingerReturnsNull_RecordsLost()
    {
        var p = new FakePinger { Respond = _ => Task.FromResult<long?>(null) };
        using var m = Create(p);
        m.SetHosts([A]);
        await m.TickAsync();
        Assert.True(m.HistoryFor(A.Address).Snapshot().Single().IsLost);
    }

    [Fact]
    public async Task Tick_PingerThrows_RecordsLost()
    {
        var p = new FakePinger { Respond = _ => throw new InvalidOperationException("boom") };
        using var m = Create(p);
        m.SetHosts([A]);
        await m.TickAsync();
        Assert.True(m.HistoryFor(A.Address).Snapshot().Single().IsLost);
    }

    [Fact]
    public async Task SystemPinger_InvalidHost_ReturnsNull()
    {
        Assert.Null(await new SystemPinger().PingAsync("no-such-host.invalid", 1000));
    }

    [Fact]
    public async Task SystemPinger_GarbageAddress_ReturnsNull()
    {
        Assert.Null(await new SystemPinger().PingAsync("not an address!!", 1000));
    }

    [Fact]
    public async Task SystemPinger_Loopback_ReturnsValue()
    {
        Assert.NotNull(await new SystemPinger().PingAsync("127.0.0.1", 1000));
    }

    [Fact]
    public async Task SetHosts_KeepsHistoryForUnchangedHosts()
    {
        using var m = Create(new FakePinger());
        m.SetHosts([A, B]);
        await m.TickAsync();
        m.SetHosts([A with { ThresholdMs = 99 }, C]);
        Assert.Equal(1, m.HistoryFor(A.Address).Count);
        Assert.Equal(0, m.HistoryFor(C.Address).Count);
        Assert.Throws<KeyNotFoundException>(() => m.HistoryFor(B.Address));
        Assert.Equal(99, m.Hosts[0].ThresholdMs);
    }

    [Fact]
    public async Task SetHosts_MatchesAddressCaseInsensitiveAndTrimmed()
    {
        using var m = Create(new FakePinger());
        m.SetHosts([new HostConfig("Router.Local", 10)]);
        await m.TickAsync();
        m.SetHosts([new HostConfig(" router.local ", 10)]);
        Assert.Equal(1, m.HistoryFor("router.local").Count);
    }

    [Fact]
    public async Task SetHosts_DuringTick_DoesNotThrow()
    {
        var gate = new TaskCompletionSource<long?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var p = new FakePinger { Respond = _ => gate.Task };
        using var m = Create(p);
        m.SetHosts([A, B]);
        var tick = m.TickAsync();
        m.SetHosts([C]);
        gate.SetResult(5);
        await tick;
        Assert.Equal(0, m.HistoryFor(C.Address).Count);
        Assert.Throws<KeyNotFoundException>(() => m.HistoryFor(A.Address));
    }

    [Fact]
    public async Task SlowHost_TicksDoNotOverlap()
    {
        var p = new FakePinger
        {
            Respond = async a => { if (a == A.Address) await Task.Delay(1000); return 1; },
        };
        using var m = Create(p);
        m.SetHosts([A, B]);
        m.Start(TimeSpan.FromMilliseconds(100));
        await Task.Delay(350);
        m.Stop();
        Assert.True(m.HistoryFor(A.Address).Count <= 1);
        Assert.True(p.PeakInFlight <= 2, $"peak in flight {p.PeakInFlight}");
        Assert.True(p.Calls <= 2, $"calls {p.Calls}");
    }

    [Fact]
    public async Task HangingHost_TickStillFinishesWithinTimeout()
    {
        var never = new TaskCompletionSource<long?>();
        var p = new FakePinger { Respond = a => a == A.Address ? never.Task : Task.FromResult<long?>(3) };
        using var m = Create(p);
        m.SetHosts([A, B]);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await m.TickAsync();
        Assert.True(sw.ElapsedMilliseconds < PingMonitor.TimeoutMs + 800, $"tick took {sw.ElapsedMilliseconds} ms");
        Assert.True(m.HistoryFor(A.Address).Snapshot().Single().IsLost);
        Assert.Equal(3, m.HistoryFor(B.Address).Snapshot().Single().RoundtripMs);
    }

    [Fact]
    public async Task HangingHost_NotPingedAgainWhileStillInFlight()
    {
        var never = new TaskCompletionSource<long?>();
        var callsA = 0;
        var p = new FakePinger
        {
            Respond = a =>
            {
                if (a != A.Address) return Task.FromResult<long?>(3);
                Interlocked.Increment(ref callsA);
                return never.Task;
            },
        };
        using var m = Create(p);
        m.SetHosts([A, B]);
        await m.TickAsync();
        await m.TickAsync();
        Assert.Equal(1, callsA);
        Assert.Equal(2, m.HistoryFor(A.Address).Count);
        Assert.All(m.HistoryFor(A.Address).Snapshot(), s => Assert.True(s.IsLost));
        Assert.Equal(2, m.HistoryFor(B.Address).Count);
    }

    [Fact]
    public async Task Start_TicksRepeatedly()
    {
        using var m = Create(new FakePinger());
        m.SetHosts([A]);
        m.Start(TimeSpan.FromMilliseconds(50));
        await Task.Delay(400);
        m.Stop();
        Assert.True(m.HistoryFor(A.Address).Count >= 3);
    }

    [Fact]
    public async Task Updated_RaisedAfterTick()
    {
        using var m = Create(new FakePinger());
        m.SetHosts([A]);
        var raised = 0;
        m.Updated += (_, _) => raised++;
        await m.TickAsync();
        Assert.Equal(1, raised);
    }
}
