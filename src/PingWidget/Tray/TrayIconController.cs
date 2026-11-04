using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PingWidget.Tray;

/// <summary>Tray icon (green/red dot) with the app menu.</summary>
public sealed class TrayIconController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _showItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly Icon _green = MakeDot(Color.FromArgb(0x4c, 0xd9, 0x7b));
    private readonly Icon _red = MakeDot(Color.FromArgb(0xff, 0x4d, 0x4d));
    private bool? _healthy;
    private bool _silent;

    public TrayIconController()
    {
        _showItem = new ToolStripMenuItem("Показывать на рабочем столе") { CheckOnClick = true };
        _showItem.CheckedChanged += (_, _) => ShowOnDesktopToggled?.Invoke(this, EventArgs.Empty);
        var settingsItem = new ToolStripMenuItem("Настройки…");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        _autoStartItem = new ToolStripMenuItem("Запускать с Windows") { CheckOnClick = true };
        _autoStartItem.CheckedChanged += (_, _) =>
        {
            if (!_silent) StartWithWindowsToggled?.Invoke(this, EventArgs.Empty);
        };
        var exitItem = new ToolStripMenuItem("Выход");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([_showItem, settingsItem, _autoStartItem, new ToolStripSeparator(), exitItem]);

        _icon = new NotifyIcon { Text = "PingWidget", Icon = _green, ContextMenuStrip = menu, Visible = true };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowOnDesktop = !ShowOnDesktop;
        };
    }

    public event EventHandler? ShowOnDesktopToggled;
    public event EventHandler? StartWithWindowsToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    /// <summary>Setting this raises <see cref="ShowOnDesktopToggled"/> only when the value changes.</summary>
    public bool ShowOnDesktop
    {
        get => _showItem.Checked;
        set => _showItem.Checked = value;
    }

    public bool StartWithWindows
    {
        get => _autoStartItem.Checked;
        set => _autoStartItem.Checked = value;
    }

    /// <summary>Changes the checkbox without raising <see cref="StartWithWindowsToggled"/> (used to revert a failed change).</summary>
    public void SetStartWithWindowsSilently(bool value)
    {
        _silent = true;
        try { _autoStartItem.Checked = value; }
        finally { _silent = false; }
    }

    public void SetHealthy(bool healthy)
    {
        if (_healthy == healthy) return;
        _healthy = healthy;
        _icon.Icon = healthy ? _green : _red;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
        _green.Dispose();
        _red.Dispose();
    }

    private static Icon MakeDot(Color color)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 3, 3, 26, 26);
        }
        var handle = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
