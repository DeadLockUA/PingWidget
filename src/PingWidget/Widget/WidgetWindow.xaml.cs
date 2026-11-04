using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PingWidget.Core;
using PingWidget.Core.Metrics;

namespace PingWidget.Widget;

public partial class WidgetWindow : Window
{
    private readonly PingMonitor _monitor;
    private readonly HardwareMonitor _hardware;
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    private readonly ObservableCollection<HostRowViewModel> _rows = [];
    private IReadOnlyList<HostConfig>? _shownHosts;
    private readonly ObservableCollection<MetricRowViewModel> _cpuRows = [];
    private readonly ObservableCollection<MetricRowViewModel> _ramRows = [];
    private readonly ObservableCollection<MetricRowViewModel> _gpuRows = [];

    public WidgetWindow(PingMonitor monitor, HardwareMonitor hardware, AppSettings settings, Action saveSettings)
    {
        InitializeComponent();
        _monitor = monitor;
        _hardware = hardware;
        _settings = settings;
        _saveSettings = saveSettings;
        Rows.ItemsSource = _rows;
        CpuRows.ItemsSource = _cpuRows;
        RamRows.ItemsSource = _ramRows;
        GpuRows.ItemsSource = _gpuRows;

        RefreshRows();
        RebuildMetricRows();
        _monitor.Updated += OnMonitorUpdated;
        _hardware.Updated += OnHardwareUpdated;
        Loaded += (_, _) => PlaceOnScreen(_settings.WindowLeft, _settings.WindowTop);
        // Card grows when hosts are added: keep it fully on its screen.
        SizeChanged += (_, _) => { if (IsLoaded) PlaceOnScreen(Left, Top, snapToNearest: true); };
    }

    private void OnMonitorUpdated(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshRows);

    private void OnHardwareUpdated(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshMetricRows);

    /// <summary>Recreates the hardware rows with the current thresholds from settings.</summary>
    public void RebuildMetricRows()
    {
        var t = _settings.MetricThresholds;
        MetricRowViewModel Row(MetricKind k, string label) => new(k, label, t.Of(k));

        _cpuRows.Clear();
        _cpuRows.Add(Row(MetricKind.CpuLoad, "Load"));
        _cpuRows.Add(Row(MetricKind.CpuTemp, "Temp"));
        _ramRows.Clear();
        _ramRows.Add(Row(MetricKind.RamUsed, "Used"));
        _gpuRows.Clear();
        _gpuRows.Add(Row(MetricKind.GpuLoad, "Load"));
        _gpuRows.Add(Row(MetricKind.VramUsed, "VRAM"));
        _gpuRows.Add(Row(MetricKind.GpuTemp, "Temp"));
        RefreshMetricRows();
    }

    private void RefreshMetricRows()
    {
        foreach (var row in _cpuRows.Concat(_ramRows).Concat(_gpuRows))
            row.Readings = _hardware.HistoryFor(row.Kind).Snapshot();
    }

    /// <summary>Rebuilds rows if the host list changed, then pulls the latest samples.</summary>
    public void RefreshRows()
    {
        var hosts = _monitor.Hosts;
        if (!ReferenceEquals(hosts, _shownHosts))
        {
            _shownHosts = hosts;
            _rows.Clear();
            foreach (var h in hosts) _rows.Add(new HostRowViewModel(h.Address.Trim(), h.ThresholdMs));
        }
        foreach (var row in _rows)
        {
            try { row.Samples = _monitor.HistoryFor(row.Address).Snapshot(); }
            catch (KeyNotFoundException) { /* host list changed meanwhile; the next refresh rebuilds rows */ }
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Tool window: hidden from Alt-Tab. No-activate: clicking or dragging never steals focus.
        var hwnd = new WindowInteropHelper(this).Handle;
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    protected override void OnClosed(EventArgs e)
    {
        _monitor.Updated -= OnMonitorUpdated;
        _hardware.Updated -= OnHardwareUpdated;
        base.OnClosed(e);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        PlaceOnScreen(Left, Top, snapToNearest: true);
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _saveSettings();
    }

    private void PlaceOnScreen(double? left, double? top, bool snapToNearest = false)
    {
        var (x, y) = PlacementResolver.Resolve(left, top, ActualWidth, ActualHeight, GetScreens(), snapToNearest: snapToNearest);
        if (Left != x) Left = x;
        if (Top != y) Top = y;
    }

    private IReadOnlyList<ScreenArea> GetScreens()
    {
        var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        return System.Windows.Forms.Screen.AllScreens.Select(s =>
        {
            var wa = s.WorkingArea;
            var tl = m.Transform(new Point(wa.Left, wa.Top));
            var br = m.Transform(new Point(wa.Right, wa.Bottom));
            return new ScreenArea(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y, s.Primary);
        }).ToList();
    }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);
}
