using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PingWidget.Core;

namespace PingWidget.Widget;

/// <summary>
/// The minimized widget: the first three hosts' last 15 seconds side by side, docked on the taskbar just left of the tray.
/// Click restores the full widget; drag slides it along the taskbar.
/// </summary>
public partial class TaskbarMiniWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _saveSettings;
    // The taskbar is topmost too and covers us whenever it is clicked: re-assert our z-order.
    private readonly DispatcherTimer _keepOnTop = new() { Interval = TimeSpan.FromSeconds(1) };

    public TaskbarMiniWindow(AppSettings settings, Action saveSettings)
    {
        InitializeComponent();
        _settings = settings;
        _saveSettings = saveSettings;
        _keepOnTop.Tick += (_, _) => BringAboveTaskbar();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { Dock(); _keepOnTop.Start(); }
            else _keepOnTop.Stop();
        };
    }

    public event EventHandler? RestoreRequested;

    /// <summary>Hosts drawn side by side (the first three).</summary>
    public IReadOnlyList<HostRowViewModel> Rows
    {
        set => Hosts.ItemsSource = value;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    protected override void OnClosed(EventArgs e)
    {
        _keepOnTop.Stop();
        base.OnClosed(e);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        double x = Left, y = Top;
        DragMove();
        if (Math.Abs(Left - x) < 3 && Math.Abs(Top - y) < 3)
        {
            Left = x;
            Top = y;
            RestoreRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        _settings.MiniLeft = Left;
        Dock();
        _settings.MiniLeft = Left; // clamped
        _saveSettings();
    }

    /// <summary>Puts the window on the taskbar (or the bottom-right screen corner if the taskbar is not horizontal).</summary>
    private void Dock()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var bar) || bar.Right - bar.Left <= bar.Bottom - bar.Top)
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 8;
            Top = wa.Bottom - Height - 8;
            return;
        }

        var m = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var tl = m.Transform(new Point(bar.Left, bar.Top));
        var br = m.Transform(new Point(bar.Right, bar.Bottom));
        double? trayLeft = null;
        var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        if (tray != IntPtr.Zero && GetWindowRect(tray, out var t) && t.Right > t.Left)
            trayLeft = m.Transform(new Point(t.Left, t.Top)).X;

        var area = new ScreenArea(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y, true);
        (Left, Top) = TaskbarPlacement.Resolve(area, trayLeft, Width, Height, _settings.MiniLeft);
        BringAboveTaskbar();
    }

    private void BringAboveTaskbar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_NOACTIVATE = 0x08000000;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern long GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern long SetWindowLongPtr(IntPtr hWnd, int nIndex, long dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
