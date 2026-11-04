using PingWidget.Core;

namespace PingWidget.Tests;

public class ChartGeometryTests
{
    private const double W = 119, H = 34;

    private static List<PingSample> Of(params long?[] rtts) =>
        rtts.Select(r => new PingSample(DateTime.UnixEpoch, r)).ToList();

    private static ChartLayout Compute(int threshold, params long?[] rtts) =>
        ChartGeometry.Compute(Of(rtts), threshold, W, H, capacity: 120);

    [Fact]
    public void Empty_NoSegments_ThresholdLineAt20()
    {
        var l = Compute(10);
        Assert.Empty(l.Segments);
        Assert.Empty(l.LossBarsX);
        Assert.Equal(20, l.ThresholdY, 6);
    }

    [Fact]
    public void NewestSampleAtRightEdge()
    {
        var l = Compute(10, 5, 5, 5);
        Assert.Equal(2, l.Segments.Count);
        Assert.Equal(117, l.Segments[0].X1, 6);
        Assert.Equal(119, l.Segments[^1].X2, 6);
    }

    [Fact]
    public void SegmentAboveThreshold_WhenEitherEndAbove()
    {
        var l = Compute(10, 5, 50, 5);
        Assert.Equal(2, l.Segments.Count);
        Assert.All(l.Segments, s => Assert.True(s.AboveThreshold));
        Assert.False(Compute(10, 5, 5).Segments.Single().AboveThreshold);
    }

    [Fact]
    public void LostSample_BreaksLineAndAddsBar()
    {
        var l = Compute(10, 5, null, 5);
        Assert.Empty(l.Segments);
        Assert.Equal(new[] { 118.0 }, l.LossBarsX);
    }

    [Fact]
    public void SpikeAboveScale_ScalesToTop()
    {
        var l = Compute(10, 5, 1000);
        var seg = l.Segments.Single();
        Assert.Equal(2, seg.Y2, 6);
        Assert.InRange(seg.Y1, 0, H);
        Assert.InRange(l.ThresholdY, 0, H);
    }

    [Fact]
    public void MoreSamplesThanCapacity_OnlyNewestDrawn()
    {
        var rtts = Enumerable.Repeat<long?>(5, 200).ToArray();
        var l = Compute(10, rtts);
        Assert.Equal(119, l.Segments.Count);
        Assert.Equal(0, l.Segments[0].X1, 6);
    }

    [Fact]
    public void Values_FixedMax_ScalesToFixedMax()
    {
        var l = ChartGeometry.Compute(new double?[] { 50, 50 }, 90, W, H, capacity: 120, fixedMax: 100);
        Assert.Equal(17, l.Segments.Single().Y1, 6);
    }

    [Fact]
    public void Values_FixedMax_ClampsAbove()
    {
        var l = ChartGeometry.Compute(new double?[] { 150, 150 }, 90, W, H, capacity: 120, fixedMax: 100);
        Assert.Equal(2, l.Segments.Single().Y2, 6);
    }

    [Fact]
    public void Values_Null_IsLossBar()
    {
        var l = ChartGeometry.Compute(new double?[] { 1, null, 1 }, 90, W, H, capacity: 120);
        Assert.Equal(new[] { 118.0 }, l.LossBarsX);
    }

    [Fact]
    public void Values_MarkMissingFalse_NoLossBarsAndNoSegments()
    {
        var l = ChartGeometry.Compute(new double?[] { 1, null, 1 }, 90, W, H, capacity: 120, fixedMax: 100, markMissing: false);
        Assert.Empty(l.LossBarsX);
        Assert.Empty(l.Segments);
    }
}
