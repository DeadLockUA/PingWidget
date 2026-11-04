using PingWidget.Core;

namespace PingWidget.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "PingWidgetTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private static void AssertDefaults(AppSettings s)
    {
        Assert.Equal(new[]
        {
            new HostConfig("192.168.1.1", 10),
            new HostConfig("192.168.0.1", 20),
            new HostConfig("8.8.8.8", 100),
        }, s.Hosts);
        Assert.True(s.ShowOnDesktop);
        Assert.False(s.StartWithWindows);
        Assert.Null(s.WindowLeft);
        Assert.Null(s.WindowTop);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults() => AssertDefaults(SettingsStore.Load(FilePath));

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{not json");
        AssertDefaults(SettingsStore.Load(FilePath));
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "");
        AssertDefaults(SettingsStore.Load(FilePath));
    }

    [Fact]
    public void Load_JsonNull_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "null");
        AssertDefaults(SettingsStore.Load(FilePath));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var s = new AppSettings
        {
            Hosts = [new HostConfig("1.1.1.1", 50)],
            ShowOnDesktop = false,
            WindowLeft = 1930.5,
            WindowTop = 40,
        };
        SettingsStore.Save(s, FilePath);
        var loaded = SettingsStore.Load(FilePath);
        Assert.Equal(s.Hosts, loaded.Hosts);
        Assert.False(loaded.ShowOnDesktop);
        Assert.Equal(1930.5, loaded.WindowLeft);
        Assert.Equal(40, loaded.WindowTop);
    }

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        SettingsStore.Save(AppSettings.Defaults(), FilePath);
        Assert.True(File.Exists(FilePath));
    }
}
