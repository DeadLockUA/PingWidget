namespace PingWidget.Core;

public static class HealthEvaluator
{
    /// <summary>
    /// True when at least <paramref name="badCount"/> of the last <paramref name="window"/> samples
    /// are lost or above the threshold.
    /// </summary>
    public static bool IsUnhealthy(IReadOnlyList<PingSample> samples, int thresholdMs, int window = 5, int badCount = 3)
    {
        var bad = 0;
        for (var i = Math.Max(0, samples.Count - window); i < samples.Count; i++)
        {
            var s = samples[i];
            if (s.IsLost || s.RoundtripMs > thresholdMs) bad++;
        }
        return bad >= badCount;
    }
}
