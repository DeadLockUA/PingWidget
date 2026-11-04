namespace PingWidget.Core.Metrics;

public enum MetricKind { CpuLoad, CpuTemp, RamUsed, GpuLoad, VramUsed, GpuTemp }

/// <summary>
/// One sample of a metric. <see cref="Value"/> is a percent (load, RAM, VRAM) or °C (temperatures);
/// null when the metric could not be read. RAM/VRAM also carry absolute amounts in GB.
/// </summary>
public readonly record struct MetricReading(double? Value, double? UsedGb = null, double? TotalGb = null)
{
    public static MetricReading Missing => default;
}

/// <summary>A hardware sensor flattened to plain strings, so selection logic doesn't depend on the sensor library.</summary>
public sealed record SensorReading(string HardwareType, string HardwareName, string SensorType, string Name, float? Value);
