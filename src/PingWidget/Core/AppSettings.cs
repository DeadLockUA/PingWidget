namespace PingWidget.Core;

public sealed class AppSettings
{
    public List<HostConfig> Hosts { get; set; } = DefaultHosts();
    public bool ShowOnDesktop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }

    public static AppSettings Defaults() => new();

    public static List<HostConfig> DefaultHosts() =>
    [
        new("192.168.1.1", 10),
        new("192.168.0.1", 20),
        new("8.8.8.8", 100),
    ];
}
