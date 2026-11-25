namespace PingWidget.Core;

/// <summary>Where the minimized widget sits on a horizontal taskbar (all values in DIPs).</summary>
public static class TaskbarPlacement
{
    /// <summary>
    /// Keeps the saved horizontal position if given, otherwise puts the widget just left of the tray area;
    /// either way it stays between the taskbar's left edge and the tray, centred vertically.
    /// </summary>
    public static (double Left, double Top) Resolve(ScreenArea taskbar, double? trayLeft, double width, double height,
        double? savedLeft, double gap = 8)
    {
        var maxLeft = Math.Max(taskbar.Left, (trayLeft ?? taskbar.Right) - width - (savedLeft is null ? gap : 0));
        var left = Math.Clamp(savedLeft ?? maxLeft, taskbar.Left, maxLeft);
        var top = taskbar.Top + Math.Max(0, (taskbar.Height - height) / 2);
        return (left, top);
    }
}
