using PingWidget.Core;

namespace PingWidget.Tests;

public class TaskbarPlacementTests
{
    // 1920x1080 screen, 48 DIP taskbar at the bottom, tray area starts at x=1600.
    private static readonly ScreenArea Taskbar = new(0, 1032, 1920, 48, true);

    [Fact]
    public void NoSaved_JustLeftOfTray_VerticallyCentered() =>
        Assert.Equal((1600d - 160 - 8, 1036d), TaskbarPlacement.Resolve(Taskbar, 1600, 160, 40, null));

    [Fact]
    public void NoSaved_NoTray_NearRightEdge() =>
        Assert.Equal((1920d - 160 - 8, 1036d), TaskbarPlacement.Resolve(Taskbar, null, 160, 40, null));

    [Fact]
    public void Saved_Kept() =>
        Assert.Equal((900d, 1036d), TaskbarPlacement.Resolve(Taskbar, 1600, 160, 40, 900));

    [Fact]
    public void SavedOverTray_ClampedLeftOfTray() =>
        Assert.Equal((1440d, 1036d), TaskbarPlacement.Resolve(Taskbar, 1600, 160, 40, 1550));

    [Fact]
    public void SavedOffLeftEdge_ClampedToTaskbarStart() =>
        Assert.Equal((0d, 1036d), TaskbarPlacement.Resolve(Taskbar, 1600, 160, 40, -300));

    [Fact]
    public void TallerThanTaskbar_AlignedToTop() =>
        Assert.Equal((900d, 1032d), TaskbarPlacement.Resolve(Taskbar, 1600, 160, 60, 900));
}
