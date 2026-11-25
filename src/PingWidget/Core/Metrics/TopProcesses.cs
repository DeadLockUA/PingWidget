using System.Text.RegularExpressions;

namespace PingWidget.Core.Metrics;

/// <summary>One running process. <see cref="CpuTime"/> is cumulative, in 100 ns units.</summary>
public readonly record struct ProcessSample(int Pid, string Name, long CpuTime, long PrivateBytes);

public sealed record ProcessSnapshot(IReadOnlyList<ProcessSample> Processes, IReadOnlyDictionary<int, double> GpuLoadByPid);

/// <summary>Reads running processes. May throw; callers treat failures as "no data".</summary>
public interface IProcessSource : IDisposable
{
    ProcessSnapshot Read();
}

/// <summary>Name of the heaviest app per resource; null when unknown or when nothing uses the resource.</summary>
public sealed record TopProcesses(string? Cpu, string? Ram, string? Gpu)
{
    public static readonly TopProcesses None = new(null, null, null);
}

public static class TopProcessPicker
{
    /// <summary>Processes sharing a name count as one app, so a browser's many processes rank by their total.</summary>
    public static TopProcesses Pick(
        IReadOnlyList<ProcessSample>? previous,
        IReadOnlyList<ProcessSample> current,
        IReadOnlyDictionary<int, double> gpuLoadByPid)
    {
        var cpuBefore = previous?.ToDictionary(p => p.Pid, p => p.CpuTime);
        return new TopProcesses(
            cpuBefore is null ? null : Top(current, p => Math.Max(0, p.CpuTime - cpuBefore.GetValueOrDefault(p.Pid))),
            Top(current, p => p.PrivateBytes),
            Top(current, p => gpuLoadByPid.GetValueOrDefault(p.Pid)));
    }

    private static string? Top(IEnumerable<ProcessSample> processes, Func<ProcessSample, double> amount) =>
        processes
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.Key, Amount: g.Sum(amount)))
            .Where(x => x.Amount > 0)
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Name)
            .FirstOrDefault();
}

/// <summary>Reads Windows "GPU Engine" instance names such as pid_1816_luid_0x0_0x15bc3_phys_0_eng_0_engtype_3d.</summary>
public static partial class GpuEngineParser
{
    [GeneratedRegex(@"^pid_(\d+)_(luid_.+?_phys_\d+)_eng_(\d+)_", RegexOptions.IgnoreCase)]
    private static partial Regex EngineInstance();

    /// <summary>The adapter with the most dedicated memory in use: the discrete GPU on a laptop with two.</summary>
    public static string? BusiestAdapter(IEnumerable<KeyValuePair<string, double>> dedicatedUsage) =>
        dedicatedUsage.OrderByDescending(a => a.Value).Select(a => a.Key).FirstOrDefault();

    /// <summary>Per process, the load of its busiest engine, which is how Task Manager reports it.</summary>
    public static IReadOnlyDictionary<int, double> LoadByPid(IEnumerable<KeyValuePair<string, double>> engines, string? adapter)
    {
        var perEngine = new Dictionary<(int Pid, string Engine), double>();
        foreach (var (instance, value) in engines)
        {
            var m = EngineInstance().Match(instance);
            if (!m.Success || !int.TryParse(m.Groups[1].ValueSpan, out var pid)) continue;
            if (adapter is not null && !m.Groups[2].Value.Equals(adapter, StringComparison.OrdinalIgnoreCase)) continue;
            var key = (pid, m.Groups[3].Value);
            perEngine[key] = perEngine.GetValueOrDefault(key) + value;
        }
        return perEngine.GroupBy(e => e.Key.Pid).ToDictionary(g => g.Key, g => g.Max(e => e.Value));
    }
}
