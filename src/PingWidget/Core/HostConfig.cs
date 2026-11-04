namespace PingWidget.Core;

public sealed record HostConfig(string Address, int ThresholdMs)
{
    public const int MinThreshold = 1;
    public const int MaxThreshold = 10000;
}
