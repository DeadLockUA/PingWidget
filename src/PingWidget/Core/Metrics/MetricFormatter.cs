using System.Globalization;

namespace PingWidget.Core.Metrics;

public static class MetricFormatter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>"46%", "54°C", "18.5/31.9 GB 58%", or "—" when the value is unavailable.</summary>
    public static string Format(MetricKind kind, MetricReading r)
    {
        if (r.Value is not double v) return "—";
        return kind switch
        {
            MetricKind.CpuTemp or MetricKind.GpuTemp => string.Create(Inv, $"{v:0}°C"),
            MetricKind.RamUsed or MetricKind.VramUsed when r.UsedGb is double u && r.TotalGb is double t =>
                string.Create(Inv, $"{u:0.#}/{t:0.#} GB {v:0}%"),
            _ => string.Create(Inv, $"{v:0}%"),
        };
    }
}
