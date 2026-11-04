using PingWidget.Core;

namespace PingWidget.Tests;

public class SampleHistoryTests
{
    private static PingSample S(long? rtt) => new(DateTime.UnixEpoch, rtt);

    [Fact]
    public void Add_BeyondCapacity_DropsOldest()
    {
        var h = new SampleHistory(3);
        foreach (var v in new long[] { 1, 2, 3, 4 }) h.Add(S(v));
        Assert.Equal(new long?[] { 2, 3, 4 }, h.Snapshot().Select(s => s.RoundtripMs));
        Assert.Equal(3, h.Count);
    }

    [Fact]
    public void Snapshot_IsCopy()
    {
        var h = new SampleHistory(3);
        h.Add(S(1));
        var snap = h.Snapshot();
        h.Add(S(2));
        Assert.Single(snap);
    }

    [Fact]
    public void DefaultCapacity_Is120()
    {
        var h = new SampleHistory();
        for (var i = 0; i < 200; i++) h.Add(S(i));
        Assert.Equal(120, h.Count);
        Assert.Equal(80, h.Snapshot()[0].RoundtripMs);
    }
}
