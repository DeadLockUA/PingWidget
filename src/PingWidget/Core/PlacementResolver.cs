namespace PingWidget.Core;

/// <summary>A monitor's work area in WPF device-independent units.</summary>
public readonly record struct ScreenArea(double Left, double Top, double Width, double Height, bool IsPrimary)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public bool Contains(double x, double y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

public static class PlacementResolver
{
    /// <summary>
    /// Restores the saved position if its top-left point is on a connected screen (clamped so the card fits);
    /// otherwise returns the bottom-right corner of the primary screen.
    /// With <paramref name="snapToNearest"/> (used after a drag), a position off every screen is clamped onto the
    /// screen nearest to the card's centre instead of jumping to the primary screen.
    /// </summary>
    public static (double Left, double Top) Resolve(double? savedLeft, double? savedTop, double cardWidth, double cardHeight,
        IReadOnlyList<ScreenArea> screens, double margin = 12, bool snapToNearest = false)
    {
        if (screens.Count == 0) return (savedLeft ?? 0, savedTop ?? 0);

        if (savedLeft is double x && savedTop is double y)
        {
            foreach (var s in screens)
            {
                if (s.Contains(x, y)) return ClampOnto(s, x, y, cardWidth, cardHeight);
            }

            if (snapToNearest)
            {
                double cx = x + cardWidth / 2, cy = y + cardHeight / 2;
                var nearest = screens.MinBy(s => DistanceSquared(s, cx, cy));
                return ClampOnto(nearest, x, y, cardWidth, cardHeight);
            }
        }

        var primary = screens.FirstOrDefault(s => s.IsPrimary, screens[0]);
        return (primary.Right - cardWidth - margin, primary.Bottom - cardHeight - margin);
    }

    private static (double, double) ClampOnto(ScreenArea s, double x, double y, double w, double h) =>
        (Clamp(x, s.Left, s.Right - w), Clamp(y, s.Top, s.Bottom - h));

    private static double DistanceSquared(ScreenArea s, double px, double py)
    {
        var dx = Math.Max(0, Math.Max(s.Left - px, px - s.Right));
        var dy = Math.Max(0, Math.Max(s.Top - py, py - s.Bottom));
        return dx * dx + dy * dy;
    }

    private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(v, Math.Max(min, max)));
}
