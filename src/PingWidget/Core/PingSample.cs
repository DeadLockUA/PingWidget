namespace PingWidget.Core;

/// <summary>One ping result. <see cref="RoundtripMs"/> is null when the packet was lost.</summary>
public readonly record struct PingSample(DateTime Time, long? RoundtripMs)
{
    public bool IsLost => RoundtripMs is null;
}
