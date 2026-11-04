using System.IO;
using System.Windows;
using PingWidget.Core;
using PingWidget.Settings;
using PingWidget.Tray;
using PingWidget.Widget;

namespace PingWidget;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppSettings _settings = AppSettings.Defaults();
    private PingMonitor? _monitor;
    private WidgetWindow? _widget;
    private TrayIconController? _tray;
    private SettingsWindow? _settingsWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, "PingWidget.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        _settings = SettingsStore.Load(SettingsStore.DefaultPath);
        _settings.StartWithWindows = AutoStart.IsEnabled();
        if (_settings.StartWithWindows) TrySetAutoStart(true); // refresh path in case the exe moved

        _monitor = new PingMonitor(new SystemPinger(), () => DateTime.Now);
        _monitor.SetHosts(_settings.Hosts);
        _monitor.Updated += OnMonitorUpdated;

        _widget = new WidgetWindow(_monitor, _settings, SaveSettings);

        _tray = new TrayIconController
        {
            ShowOnDesktop = _settings.ShowOnDesktop,
            StartWithWindows = _settings.StartWithWindows,
        };
        _tray.ShowOnDesktopToggled += (_, _) => ApplyShowOnDesktop(_tray.ShowOnDesktop, save: true);
        _tray.StartWithWindowsToggled += (_, _) => OnStartWithWindowsToggled();
        _tray.SettingsRequested += (_, _) => OpenSettings();
        _tray.ExitRequested += (_, _) => Shutdown();

        ApplyShowOnDesktop(_settings.ShowOnDesktop, save: false);
        _monitor.Start(TimeSpan.FromSeconds(1));
    }

    private void ApplyShowOnDesktop(bool show, bool save)
    {
        if (show) _widget?.Show();
        else _widget?.Hide();
        if (!save) return;
        _settings.ShowOnDesktop = show;
        SaveSettings();
    }

    private void OnStartWithWindowsToggled()
    {
        var wanted = _tray!.StartWithWindows;
        if (!TrySetAutoStart(wanted))
        {
            _tray.SetStartWithWindowsSilently(!wanted); // registry not writable: revert without re-entering
            return;
        }
        _settings.StartWithWindows = wanted;
        SaveSettings();
    }

    private static bool TrySetAutoStart(bool enabled)
    {
        try
        {
            AutoStart.Set(enabled, Environment.ProcessPath!);
            return true;
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings.Hosts);
        try
        {
            if (_settingsWindow.ShowDialog() == true && _settingsWindow.Result is { } hosts)
            {
                _settings.Hosts = hosts.ToList();
                SaveSettings();
                _monitor!.SetHosts(hosts);
                _widget?.RefreshRows();
            }
        }
        finally
        {
            _settingsWindow = null;
        }
    }

    private void OnMonitorUpdated(object? sender, EventArgs e)
    {
        var monitor = _monitor!;
        var healthy = monitor.Hosts.All(h =>
        {
            try { return !HealthEvaluator.IsUnhealthy(monitor.HistoryFor(h.Address).Snapshot(), h.ThresholdMs); }
            catch (KeyNotFoundException) { return true; }
        });
        Dispatcher.BeginInvoke(() => _tray?.SetHealthy(healthy));
    }

    private void SaveSettings()
    {
        try { SettingsStore.Save(_settings, SettingsStore.DefaultPath); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* keep running; settings stay in memory */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_monitor is not null)
        {
            _monitor.Updated -= OnMonitorUpdated;
            _monitor.Dispose();
            SaveSettings();
        }
        _tray?.Dispose();
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
