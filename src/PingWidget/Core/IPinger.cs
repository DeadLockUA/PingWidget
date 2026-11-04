namespace PingWidget.Core;

public interface IPinger
{
    /// <summary>Round-trip time in ms, or null if the packet was lost. Never throws.</summary>
    Task<long?> PingAsync(string address, int timeoutMs);
}
