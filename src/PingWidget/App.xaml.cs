using System.Windows;
using PingWidget.Core;
using PingWidget.Widget;

namespace PingWidget;

public partial class App : Application
{
    private PingMonitor? _monitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = SettingsStore.Load(SettingsStore.DefaultPath);
        _monitor = new PingMonitor(new SystemPinger(), () => DateTime.Now);
        _monitor.SetHosts(settings.Hosts);
        var window = new WidgetWindow(_monitor, settings, () => SettingsStore.Save(settings, SettingsStore.DefaultPath));
        window.Closed += (_, _) => Shutdown();
        window.Show();
        _monitor.Start(TimeSpan.FromSeconds(1));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose();
        base.OnExit(e);
    }
}
