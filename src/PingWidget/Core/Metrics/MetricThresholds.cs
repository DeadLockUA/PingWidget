namespace PingWidget.Core.Metrics;

/// <summary>Red-line thresholds for the hardware rows: percent for load/RAM/VRAM, °C for temperatures.</summary>
public sealed class MetricThresholds
{
    public const int MinPercent = 1, MaxPercent = 100, MinTemp = 1, MaxTemp = 150;

    public int CpuLoad { get; set; } = 90;
    public int CpuTemp { get; set; } = 80;
    public int RamUsed { get; set; } = 90;
    public int GpuLoad { get; set; } = 90;
    public int VramUsed { get; set; } = 90;
    public int GpuTemp { get; set; } = 80;

    public static bool IsTemperature(MetricKind kind) => kind is MetricKind.CpuTemp or MetricKind.GpuTemp;

    public int Of(MetricKind kind) => kind switch
    {
        MetricKind.CpuLoad => CpuLoad,
        MetricKind.CpuTemp => CpuTemp,
        MetricKind.RamUsed => RamUsed,
        MetricKind.GpuLoad => GpuLoad,
        MetricKind.VramUsed => VramUsed,
        MetricKind.GpuTemp => GpuTemp,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public void Set(MetricKind kind, int value)
    {
        switch (kind)
        {
            case MetricKind.CpuLoad: CpuLoad = value; break;
            case MetricKind.CpuTemp: CpuTemp = value; break;
            case MetricKind.RamUsed: RamUsed = value; break;
            case MetricKind.GpuLoad: GpuLoad = value; break;
            case MetricKind.VramUsed: VramUsed = value; break;
            case MetricKind.GpuTemp: GpuTemp = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    public static bool IsInRange(MetricKind kind, int value) => IsTemperature(kind)
        ? value is >= MinTemp and <= MaxTemp
        : value is >= MinPercent and <= MaxPercent;

    /// <summary>Copy with every value clamped into its allowed range.</summary>
    public MetricThresholds Sanitized()
    {
        var copy = new MetricThresholds();
        foreach (var k in Enum.GetValues<MetricKind>())
            copy.Set(k, IsTemperature(k) ? Math.Clamp(Of(k), MinTemp, MaxTemp) : Math.Clamp(Of(k), MinPercent, MaxPercent));
        return copy;
    }
}
