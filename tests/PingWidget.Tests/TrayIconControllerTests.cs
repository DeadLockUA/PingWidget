using PingWidget.Tray;

namespace PingWidget.Tests;

public class TrayIconControllerTests
{
    private static void RunSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { error = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null) throw new Exception("STA test failed", error);
    }

    [Fact]
    public void SetStartWithWindowsSilently_DoesNotRaiseEvent() => RunSta(() =>
    {
        using var tray = new TrayIconController();
        var raised = 0;
        tray.StartWithWindowsToggled += (_, _) => raised++;
        tray.SetStartWithWindowsSilently(true);
        Assert.True(tray.StartWithWindows);
        Assert.Equal(0, raised);
    });

    [Fact]
    public void RevertInsideHandler_DoesNotRecurse() => RunSta(() =>
    {
        // Simulates App's handler when the registry write fails: it reverts the checkbox.
        using var tray = new TrayIconController();
        var raised = 0;
        tray.StartWithWindowsToggled += (_, _) =>
        {
            if (++raised > 5) throw new InvalidOperationException("recursion");
            tray.SetStartWithWindowsSilently(!tray.StartWithWindows);
        };
        tray.StartWithWindows = true;
        Assert.Equal(1, raised);
        Assert.False(tray.StartWithWindows);
    });
}
