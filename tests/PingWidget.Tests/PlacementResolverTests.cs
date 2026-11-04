using PingWidget.Core;

namespace PingWidget.Tests;

public class PlacementResolverTests
{
    private static readonly ScreenArea Primary = new(0, 0, 1920, 1040, true);
    private static readonly ScreenArea Right = new(1920, 0, 1920, 1040, false);
    private static readonly ScreenArea Left = new(-1920, 0, 1920, 1040, false);

    [Fact]
    public void NoSaved_BottomRightOfPrimary() =>
        Assert.Equal((1608d, 878d), PlacementResolver.Resolve(null, null, 300, 150, [Right, Primary]));

    [Fact]
    public void SavedOnSecondary_Kept() =>
        Assert.Equal((2500d, 100d), PlacementResolver.Resolve(2500, 100, 300, 150, [Primary, Right]));

    [Fact]
    public void SavedOnDisconnectedMonitor_FallsBack() =>
        Assert.Equal((1608d, 878d), PlacementResolver.Resolve(2500, 100, 300, 150, [Primary]));

    [Fact]
    public void SavedPartlyOffEdge_Clamped() =>
        Assert.Equal((1620d, 890d), PlacementResolver.Resolve(1900, 1000, 300, 150, [Primary]));

    [Fact]
    public void NegativeCoordinatesMonitor_Kept() =>
        Assert.Equal((-1000d, 200d), PlacementResolver.Resolve(-1000, 200, 300, 150, [Primary, Left]));

    [Fact]
    public void NoPrimaryFlag_UsesFirstScreen() =>
        Assert.Equal((1608d, 878d), PlacementResolver.Resolve(null, null, 300, 150, [Primary with { IsPrimary = false }]));
}
