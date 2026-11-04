namespace PingWidget.Core.Metrics;

/// <summary>Maps raw sensors (LibreHardwareMonitor naming) to the six metrics shown on the card.</summary>
public static class SensorPicker
{
    private static readonly string[] CpuTempNames = ["Core (Tctl/Tdie)", "Core (Tdie)", "Core (Tctl)", "CPU Package"];

    public static IReadOnlyDictionary<MetricKind, MetricReading> Pick(IReadOnlyList<SensorReading> sensors)
    {
        var result = Enum.GetValues<MetricKind>().ToDictionary(k => k, _ => MetricReading.Missing);

        var cpu = sensors.Where(s => s.HardwareType == "Cpu").ToList();
        result[MetricKind.CpuLoad] = new(Valid(Find(cpu, "Load", "CPU Total")));
        result[MetricKind.CpuTemp] = new(CpuTempNames.Select(n => Temp(Find(cpu, "Temperature", n))).FirstOrDefault(v => v is not null));

        var memoryHw = sensors.Where(s => s.HardwareType == "Memory" && s.HardwareName != "Virtual Memory")
            .Select(s => s.HardwareName).Distinct()
            .OrderBy(n => n == "Total Memory" ? 0 : 1).FirstOrDefault();
        if (memoryHw is not null)
        {
            var mem = sensors.Where(s => s.HardwareType == "Memory" && s.HardwareName == memoryHw).ToList();
            var percent = Valid(Find(mem, "Load", "Memory"));
            var used = Valid(Find(mem, "Data", "Memory Used"));
            var available = Valid(Find(mem, "Data", "Memory Available"));
            if (percent is not null)
                result[MetricKind.RamUsed] = used is not null && available is not null && used + available > 0
                    ? new(percent, used, used + available)
                    : new(percent);
        }

        // The discrete card is the one with the most dedicated memory (an AMD iGPU can be listed before an NVIDIA dGPU);
        // Intel breaks ties last.
        var gpuHw = sensors.Where(s => s.HardwareType is "GpuNvidia" or "GpuAmd" or "GpuIntel")
            .Select(s => (s.HardwareType, s.HardwareName)).Distinct()
            .OrderByDescending(h => DedicatedMemoryMb(sensors, h.HardwareType, h.HardwareName))
            .ThenBy(h => h.HardwareType == "GpuIntel" ? 1 : 0)
            .FirstOrDefault();
        if (gpuHw.HardwareType is not null)
        {
            var gpu = sensors.Where(s => s.HardwareType == gpuHw.HardwareType && s.HardwareName == gpuHw.HardwareName).ToList();
            result[MetricKind.GpuLoad] = new(Valid(Find(gpu, "Load", "GPU Core")));
            result[MetricKind.GpuTemp] = new(Temp(Find(gpu, "Temperature", "GPU Core")));
            result[MetricKind.VramUsed] =
                Vram(Valid(Find(gpu, "SmallData", "GPU Memory Used")), Valid(Find(gpu, "SmallData", "GPU Memory Total")))
                ?? Vram(Valid(Find(gpu, "SmallData", "D3D Dedicated Memory Used")), Valid(Find(gpu, "SmallData", "D3D Dedicated Memory Total")))
                ?? MetricReading.Missing;
        }

        return result;
    }

    private static double DedicatedMemoryMb(IReadOnlyList<SensorReading> sensors, string type, string name)
    {
        var gpu = sensors.Where(s => s.HardwareType == type && s.HardwareName == name).ToList();
        return Valid(Find(gpu, "SmallData", "GPU Memory Total")) ?? Valid(Find(gpu, "SmallData", "D3D Dedicated Memory Total")) ?? 0;
    }

    private static MetricReading? Vram(double? usedMb, double? totalMb) =>
        usedMb is double u && totalMb is double t && t > 0 ? new MetricReading(u / t * 100, u / 1024, t / 1024) : null;

    private static float? Find(List<SensorReading> sensors, string type, string name) =>
        sensors.FirstOrDefault(s => s.SensorType == type && s.Name == name)?.Value;

    private static double? Valid(float? v) => v is float f && float.IsFinite(f) ? f : null;

    /// <summary>Without admin rights LHM reports CPU temperature as 0: treat non-positive temperatures as unavailable.</summary>
    private static double? Temp(float? v) => Valid(v) is double d && d > 0 ? d : null;
}
