namespace PingWidget.Core;

public readonly record struct ChartSegment(double X1, double Y1, double X2, double Y2, bool AboveThreshold);

public sealed record ChartLayout(IReadOnlyList<ChartSegment> Segments, IReadOnlyList<double> LossBarsX, double ThresholdY);

/// <summary>Turns a sample history into line segments for the heartbeat chart. Newest sample sits at the right edge.</summary>
public static class ChartGeometry
{
    public static ChartLayout Compute(IReadOnlyList<PingSample> samples, int thresholdMs, double width, double height, int capacity = 120)
    {
        var values = new double?[samples.Count];
        for (var i = 0; i < samples.Count; i++) values[i] = samples[i].RoundtripMs;
        return Compute(values, thresholdMs, width, height, capacity);
    }

    /// <summary>
    /// Null values are gaps drawn as loss bars. With <paramref name="fixedMax"/> the scale is fixed (values above it
    /// are clamped to the top); otherwise it is max(threshold * 2.5, largest value).
    /// </summary>
    public static ChartLayout Compute(IReadOnlyList<double?> values, double threshold, double width, double height,
        int capacity = 120, double? fixedMax = null)
    {
        var first = Math.Max(0, values.Count - capacity);
        var count = values.Count - first;
        var step = width / (capacity - 1);

        double max;
        if (fixedMax is double fm) max = fm;
        else
        {
            max = threshold * 2.5;
            for (var i = first; i < values.Count; i++)
                if (values[i] is double v && v > max) max = v;
        }
        if (max <= 0) max = 1;

        double Y(double v) => height - 2 - (Math.Clamp(v, 0, max) / max) * (height - 4);
        double X(int i) => width - (count - 1 - (i - first)) * step;

        var segments = new List<ChartSegment>();
        var lossBars = new List<double>();
        for (var i = first; i < values.Count; i++)
        {
            if (values[i] is not double cur)
            {
                lossBars.Add(X(i));
                continue;
            }
            if (i > first && values[i - 1] is double prev)
                segments.Add(new ChartSegment(X(i - 1), Y(prev), X(i), Y(cur), Math.Max(prev, cur) > threshold));
        }

        return new ChartLayout(segments, lossBars, Y(threshold));
    }
}
