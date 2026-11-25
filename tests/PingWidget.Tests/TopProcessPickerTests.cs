using PingWidget.Core.Metrics;

namespace PingWidget.Tests;

public class TopProcessPickerTests
{
    private static readonly Dictionary<int, double> NoGpu = [];

    private static ProcessSample P(int pid, string name, long cpu = 0, long bytes = 0) => new(pid, name, cpu, bytes);

    [Fact]
    public void Cpu_RanksByTimeUsedSinceLastSample_NotLifetimeTotal()
    {
        ProcessSample[] before = [P(1, "old", cpu: 1000), P(2, "busy", cpu: 10)];
        ProcessSample[] after = [P(1, "old", cpu: 1001), P(2, "busy", cpu: 60)];
        Assert.Equal("busy", TopProcessPicker.Pick(before, after, NoGpu).Cpu);
    }

    [Fact]
    public void Cpu_WithoutPreviousSample_IsUnknown() =>
        Assert.Null(TopProcessPicker.Pick(null, [P(1, "a", cpu: 50)], NoGpu).Cpu);

    [Fact]
    public void Cpu_NobodyRan_IsUnknown()
    {
        ProcessSample[] same = [P(1, "a", cpu: 50)];
        Assert.Null(TopProcessPicker.Pick(same, same, NoGpu).Cpu);
    }

    [Fact]
    public void Cpu_ReusedPidWithSmallerTime_CountsAsNothing()
    {
        ProcessSample[] before = [P(1, "gone", cpu: 500), P(2, "steady", cpu: 0)];
        ProcessSample[] after = [P(1, "reused", cpu: 400), P(2, "steady", cpu: 5)];
        Assert.Equal("steady", TopProcessPicker.Pick(before, after, NoGpu).Cpu);
    }

    [Fact]
    public void Cpu_SumsAllProcessesOfOneApp()
    {
        ProcessSample[] before = [P(1, "chrome"), P(2, "chrome"), P(3, "game")];
        ProcessSample[] after = [P(1, "chrome", cpu: 30), P(2, "chrome", cpu: 30), P(3, "game", cpu: 50)];
        Assert.Equal("chrome", TopProcessPicker.Pick(before, after, NoGpu).Cpu);
    }

    [Fact]
    public void Ram_SumsAllProcessesOfOneApp()
    {
        ProcessSample[] now = [P(1, "chrome", bytes: 300), P(2, "chrome", bytes: 300), P(3, "game", bytes: 500)];
        Assert.Equal("chrome", TopProcessPicker.Pick(null, now, NoGpu).Ram);
    }

    [Fact]
    public void Ram_NoProcesses_IsUnknown() => Assert.Null(TopProcessPicker.Pick(null, [], NoGpu).Ram);

    [Fact]
    public void Gpu_NamesTheHeaviestPid()
    {
        ProcessSample[] now = [P(1, "dwm"), P(2, "game")];
        var gpu = new Dictionary<int, double> { [1] = 4, [2] = 60 };
        Assert.Equal("game", TopProcessPicker.Pick(null, now, gpu).Gpu);
    }

    [Fact]
    public void Gpu_PidThatAlreadyExited_IsIgnored()
    {
        var gpu = new Dictionary<int, double> { [1] = 4, [99] = 60 };
        Assert.Equal("dwm", TopProcessPicker.Pick(null, [P(1, "dwm")], gpu).Gpu);
    }

    [Fact]
    public void Gpu_AllIdle_IsUnknown()
    {
        var gpu = new Dictionary<int, double> { [1] = 0 };
        Assert.Null(TopProcessPicker.Pick(null, [P(1, "dwm")], gpu).Gpu);
    }
}
