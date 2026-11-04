using System.Windows;
using System.Windows.Media;
using PingWidget.Core;

namespace PingWidget.Widget;

/// <summary>Draws one host's heartbeat line from <see cref="ChartGeometry"/>.</summary>
public sealed class HeartbeatChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double?>), typeof(HeartbeatChart),
        new FrameworkPropertyMetadata(Array.Empty<double?>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThresholdProperty = DependencyProperty.Register(
        nameof(Threshold), typeof(double), typeof(HeartbeatChart),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Fixed top of the scale; NaN = automatic (as for ping).</summary>
    public static readonly DependencyProperty FixedMaxProperty = DependencyProperty.Register(
        nameof(FixedMax), typeof(double), typeof(HeartbeatChart),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Draw missing values as red loss bars (ping) or leave them empty (hardware sensors).</summary>
    public static readonly DependencyProperty MarkMissingProperty = DependencyProperty.Register(
        nameof(MarkMissing), typeof(bool), typeof(HeartbeatChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public bool MarkMissing
    {
        get => (bool)GetValue(MarkMissingProperty);
        set => SetValue(MarkMissingProperty, value);
    }

    public IReadOnlyList<double?> Values
    {
        get => (IReadOnlyList<double?>)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Threshold
    {
        get => (double)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public double FixedMax
    {
        get => (double)GetValue(FixedMaxProperty);
        set => SetValue(FixedMaxProperty, value);
    }

    private static readonly Color Red = Color.FromRgb(0xff, 0x4d, 0x4d);
    private static readonly Color Green = Color.FromRgb(0x4c, 0xd9, 0x7b);
    private static readonly Brush ChartBackground = Frozen(new SolidColorBrush(Color.FromRgb(0x14, 0x16, 0x1a)));
    private static readonly Pen GreenPen = Frozen(new Pen(new SolidColorBrush(Green), 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Pen RedPen = Frozen(new Pen(new SolidColorBrush(Red), 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
    private static readonly Pen ThresholdPen = Frozen(new Pen(new SolidColorBrush(Red) { Opacity = 0.35 }, 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) });
    private static readonly Brush LossBrush = Frozen(new SolidColorBrush(Red) { Opacity = 0.6 });

    private static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = RenderSize.Width;
        var h = RenderSize.Height;
        if (w <= 0 || h <= 0) return;

        dc.DrawRectangle(ChartBackground, null, new Rect(0, 0, w, h));
        var layout = ChartGeometry.Compute(Values ?? Array.Empty<double?>(), Threshold, w, h,
            fixedMax: double.IsNaN(FixedMax) ? null : FixedMax, markMissing: MarkMissing);

        dc.DrawLine(ThresholdPen, new Point(0, layout.ThresholdY), new Point(w, layout.ThresholdY));
        foreach (var x in layout.LossBarsX)
            dc.DrawRectangle(LossBrush, null, new Rect(Math.Clamp(x - 1.5, 0, Math.Max(0, w - 3)), 0, 3, h));
        foreach (var s in layout.Segments)
            dc.DrawLine(s.AboveThreshold ? RedPen : GreenPen, new Point(s.X1, s.Y1), new Point(s.X2, s.Y2));
    }
}
