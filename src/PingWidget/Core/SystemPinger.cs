using System.Net.NetworkInformation;

namespace PingWidget.Core;

public sealed class SystemPinger : IPinger
{
    public async Task<long?> PingAsync(string address, int timeoutMs)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, timeoutMs).ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
