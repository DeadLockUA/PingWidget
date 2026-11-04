namespace PingWidget.Core;

public readonly record struct ChartSegment(double X1, double Y1, double X2, double Y2, bool AboveThreshold);

public sealed record ChartLayout(IReadOnlyList<ChartSegment> Segments, IReadOnlyList<double> LossBarsX, double ThresholdY);

/// <summary>Turns a sample history into line segments for the heartbeat chart. Newest sample sits at the right edge.</summary>
public static class ChartGeometry
{
    public static ChartLayout Compute(IReadOnlyList<PingSample> samples, int thresholdMs, double width, double height, int capacity = 120)
    {
        var first = Math.Max(0, samples.Count - capacity);
        var count = samples.Count - first;
        var step = width / (capacity - 1);

        double max = thresholdMs * 2.5;
        for (var i = first; i < samples.Count; i++)
            if (samples[i].RoundtripMs is long r && r > max) max = r;
        if (max <= 0) max = 1;

        double Y(double v) => height - 2 - (Math.Min(v, max) / max) * (height - 4);
        double X(int i) => width - (count - 1 - (i - first)) * step;

        var segments = new List<ChartSegment>();
        var lossBars = new List<double>();
        for (var i = first; i < samples.Count; i++)
        {
            if (samples[i].RoundtripMs is not long cur)
            {
                lossBars.Add(X(i));
                continue;
            }
            if (i > first && samples[i - 1].RoundtripMs is long prev)
                segments.Add(new ChartSegment(X(i - 1), Y(prev), X(i), Y(cur), Math.Max(prev, cur) > thresholdMs));
        }

        return new ChartLayout(segments, lossBars, Y(thresholdMs));
    }
}
