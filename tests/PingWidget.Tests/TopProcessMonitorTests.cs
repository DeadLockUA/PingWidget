using System.Diagnostics;
using PingWidget.Core.Metrics;

namespace PingWidget.Tests;

internal sealed class FakeProcessSource : IProcessSource
{
    public int Disposed;
    public int Calls;
    public Func<int, ProcessSnapshot> OnRead { get; set; } = call => new(
        [new ProcessSample(1, "app", CpuTime: call * 100L, PrivateBytes: 500)],
        new Dictionary<int, double> { [1] = 30 });

    public ProcessSnapshot Read() => OnRead(Interlocked.Increment(ref Calls));
    public void Dispose() => Disposed++;
}

public class TopProcessMonitorTests
{
    [Fact]
    public void BeforeFirstTick_NothingKnown()
    {
        using var m = new TopProcessMonitor(() => new FakeProcessSource());
        Assert.Equal(TopProcesses.None, m.Current);
    }

    [Fact]
    public async Task FirstTick_RamAndGpu_CpuNeedsTwoSamples()
    {
        using var m = new TopProcessMonitor(() => new FakeProcessSource());
        await m.TickAsync();
        Assert.Equal(new TopProcesses(null, "app", "app"), m.Current);
    }

    [Fact]
    public async Task SecondTick_CpuKnown()
    {
        using var m = new TopProcessMonitor(() => new FakeProcessSource());
        await m.TickAsync();
        await m.TickAsync();
        Assert.Equal("app", m.Current.Cpu);
    }

    [Fact]
    public async Task ReadThrows_ClearsNames_ThenRecreatesSource()
    {
        var created = new List<FakeProcessSource>();
        using var m = new TopProcessMonitor(() =>
        {
            var src = new FakeProcessSource();
            if (created.Count == 0) src.OnRead = _ => throw new InvalidOperationException();
            created.Add(src);
            return src;
        });
        await m.TickAsync();
        Assert.Equal(TopProcesses.None, m.Current);
        Assert.Equal(1, created[0].Disposed);
        await m.TickAsync();
        Assert.Equal("app", m.Current.Ram);
    }

    [Fact]
    public async Task HungRead_ClearsNames_AndIsNotRestarted()
    {
        using var gate = new ManualResetEventSlim(false);
        var src = new FakeProcessSource();
        var normal = src.OnRead;
        src.OnRead = call => { gate.Wait(); return normal(call); };
        using var m = new TopProcessMonitor(() => src, readTimeout: TimeSpan.FromMilliseconds(100));
        await m.TickAsync();
        await m.TickAsync();
        Assert.Equal(TopProcesses.None, m.Current);
        Assert.Equal(1, src.Calls);
        gate.Set();
        await Task.Delay(100);
        await m.TickAsync();
        Assert.Equal("app", m.Current.Ram);
    }

    [Fact]
    public async Task Dispose_DisposesSource()
    {
        var src = new FakeProcessSource();
        var m = new TopProcessMonitor(() => src);
        await m.TickAsync();
        m.Dispose();
        Assert.Equal(1, src.Disposed);
    }

    [Fact]
    public void WindowsSource_SeesThisProcess_WithPrivateMemory()
    {
        using var src = new WindowsProcessSource();
        using var me = Process.GetCurrentProcess();
        var sample = src.Read().Processes.Single(p => p.Pid == me.Id);
        Assert.Equal(me.ProcessName, sample.Name);
        Assert.True(sample.PrivateBytes > 0);
    }

    [Fact]
    public void WindowsSource_CpuTimeGrowsWhenBusy()
    {
        using var src = new WindowsProcessSource();
        using var me = Process.GetCurrentProcess();
        long CpuTime() => src.Read().Processes.Single(p => p.Pid == me.Id).CpuTime;
        var before = CpuTime();
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 100) { }
        Assert.True(CpuTime() > before);
    }

    [Fact]
    public void WindowsSource_SeesSystemProcesses_AndSkipsIdle()
    {
        using var src = new WindowsProcessSource();
        var all = src.Read().Processes;
        Assert.Contains(all, p => p.Name == "System");
        Assert.DoesNotContain(all, p => p.Pid == 0);
    }

    [Fact]
    public void WindowsSource_GpuReadIsSafeOnConsecutiveCalls()
    {
        using var src = new WindowsProcessSource();
        src.Read();
        Thread.Sleep(200);
        Assert.All(src.Read().GpuLoadByPid, kv => Assert.True(kv.Key > 0 && kv.Value >= 0));
    }
}
