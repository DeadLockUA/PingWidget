using PingWidget.Core.Metrics;

namespace PingWidget.Tests;

internal sealed class FakeHardwareSource : IHardwareSource
{
    private int _inFlight;
    public int PeakInFlight;
    public int Disposed;
    public IReadOnlyList<SensorReading> DefaultSensors() => [new SensorReading("Cpu", "cpu", "Load", "CPU Total", 46.27f)];

    public Func<IReadOnlyList<SensorReading>> Read { get; set; }

    public FakeHardwareSource() => Read = DefaultSensors;

    public IReadOnlyList<SensorReading> ReadSensors()
    {
        var now = Interlocked.Increment(ref _inFlight);
        if (now > PeakInFlight) PeakInFlight = now;
        try { return Read(); }
        finally { Interlocked.Decrement(ref _inFlight); }
    }

    public void Dispose() => Disposed++;
}

public class HardwareMonitorTests
{
    [Fact]
    public async Task Tick_AppendsPickedReadings()
    {
        using var m = new HardwareMonitor(() => new FakeHardwareSource());
        await m.TickAsync();
        Assert.Equal(46.27, m.HistoryFor(MetricKind.CpuLoad).Snapshot().Single().Value!.Value, 2);
        Assert.Null(m.HistoryFor(MetricKind.GpuTemp).Snapshot().Single().Value);
    }

    [Fact]
    public async Task ReadThrows_AllMissing_NoThrow()
    {
        using var m = new HardwareMonitor(() => new FakeHardwareSource { Read = () => throw new InvalidOperationException("driver") });
        await m.TickAsync();
        foreach (var k in Enum.GetValues<MetricKind>())
            Assert.Null(m.HistoryFor(k).Snapshot().Single().Value);
    }

    [Fact]
    public async Task FactoryThrows_AllMissing_RetriesAfter30SkippedTicks()
    {
        var calls = 0;
        using var m = new HardwareMonitor(() =>
        {
            calls++;
            if (calls == 1) throw new InvalidOperationException("open failed");
            return new FakeHardwareSource();
        });
        for (var i = 0; i < 31; i++) await m.TickAsync();
        Assert.Equal(1, calls);
        Assert.All(m.HistoryFor(MetricKind.CpuLoad).Snapshot(), r => Assert.Null(r.Value));
        await m.TickAsync();
        Assert.Equal(2, calls);
        Assert.NotNull(m.HistoryFor(MetricKind.CpuLoad).Snapshot()[^1].Value);
    }

    [Fact]
    public async Task ReadThrows_SourceDisposedAndRecreated()
    {
        var created = new List<FakeHardwareSource>();
        var first = true;
        using var m = new HardwareMonitor(() =>
        {
            var src = new FakeHardwareSource();
            if (first) { src.Read = () => throw new InvalidOperationException(); first = false; }
            created.Add(src);
            return src;
        });
        await m.TickAsync();
        await m.TickAsync();
        Assert.Equal(2, created.Count);
        Assert.Equal(1, created[0].Disposed);
        Assert.NotNull(m.HistoryFor(MetricKind.CpuLoad).Snapshot()[^1].Value);
    }

    [Fact]
    public async Task SlowSource_TicksDoNotOverlap()
    {
        var src = new FakeHardwareSource();
        src.Read = () => { Thread.Sleep(500); return []; };
        using var m = new HardwareMonitor(() => src);
        m.Start(TimeSpan.FromMilliseconds(50));
        await Task.Delay(300);
        m.Stop();
        Assert.Equal(1, src.PeakInFlight);
    }

    [Fact]
    public async Task HungRead_TimesOutToMissing_AndIsNotRestarted()
    {
        using var gate = new ManualResetEventSlim(false);
        var calls = 0;
        var src = new FakeHardwareSource();
        src.Read = () => { Interlocked.Increment(ref calls); gate.Wait(); return src.DefaultSensors(); };
        using var m = new HardwareMonitor(() => src, readTimeout: TimeSpan.FromMilliseconds(200));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await m.TickAsync();
        await m.TickAsync();
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
        Assert.Equal(1, calls);
        Assert.All(m.HistoryFor(MetricKind.CpuLoad).Snapshot(), r => Assert.Null(r.Value));
        gate.Set();
        await Task.Delay(100);
        await m.TickAsync();
        Assert.NotNull(m.HistoryFor(MetricKind.CpuLoad).Snapshot()[^1].Value);
    }

    [Fact]
    public async Task Updated_RaisedAfterTick()
    {
        using var m = new HardwareMonitor(() => new FakeHardwareSource());
        var raised = 0;
        m.Updated += (_, _) => raised++;
        await m.TickAsync();
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Dispose_DisposesSource()
    {
        var src = new FakeHardwareSource();
        var m = new HardwareMonitor(() => src);
        await m.TickAsync();
        m.Dispose();
        Assert.Equal(1, src.Disposed);
    }

    [Fact]
    public void LhmHardwareSource_ReadsCpuLoadOnThisMachine()
    {
        using var src = new LhmHardwareSource();
        src.ReadSensors();
        Thread.Sleep(300);
        Assert.NotNull(SensorPicker.Pick(src.ReadSensors())[MetricKind.CpuLoad].Value);
    }
}
