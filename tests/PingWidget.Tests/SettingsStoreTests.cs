using PingWidget.Core;
using PingWidget.Core.Metrics;

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

    private AppSettings LoadJson(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, json);
        return SettingsStore.Load(FilePath);
    }

    [Fact]
    public void Load_NullHostEntry_Dropped() =>
        Assert.Equal([new HostConfig("1.1.1.1", 5)],
            LoadJson("""{"Hosts":[null,{"Address":"1.1.1.1","ThresholdMs":5}]}""").Hosts);

    [Fact]
    public void Load_LowercaseKeys_Accepted() =>
        Assert.Equal([new HostConfig("1.1.1.1", 5)],
            LoadJson("""{"hosts":[{"address":"1.1.1.1","thresholdMs":5}]}""").Hosts);

    [Fact]
    public void Load_MissingOrBlankAddress_Dropped() =>
        Assert.Equal([new HostConfig("8.8.8.8", 100)],
            LoadJson("""{"Hosts":[{"ThresholdMs":5},{"Address":"  ","ThresholdMs":5},{"Address":"8.8.8.8","ThresholdMs":100}]}""").Hosts);

    [Fact]
    public void Load_ThresholdClampedToValidRange() =>
        Assert.Equal([new HostConfig("a", 1), new HostConfig("b", 10000)],
            LoadJson("""{"Hosts":[{"Address":"a","ThresholdMs":0},{"Address":"b","ThresholdMs":99999}]}""").Hosts);

    [Fact]
    public void Load_DuplicateAddresses_KeepFirst() =>
        Assert.Equal([new HostConfig("a", 5)],
            LoadJson("""{"Hosts":[{"Address":"a","ThresholdMs":5},{"Address":" A ","ThresholdMs":7}]}""").Hosts);

    [Fact]
    public void Load_EmptyHostList_UsesDefaultHosts() =>
        Assert.Equal(AppSettings.DefaultHosts(), LoadJson("""{"Hosts":[],"ShowOnDesktop":false}""").Hosts);

    [Fact]
    public void Load_HostsNotAnArray_ReturnsDefaults() =>
        AssertDefaults(LoadJson("""{"Hosts":"oops"}"""));

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

    [Fact]
    public void Load_V1FileWithoutMetricThresholds_Defaults()
    {
        var t = LoadJson("""{"Hosts":[{"Address":"8.8.8.8","ThresholdMs":100}],"ShowOnDesktop":true}""").MetricThresholds;
        Assert.Equal(90, t.CpuLoad);
        Assert.Equal(80, t.CpuTemp);
        Assert.Equal(90, t.RamUsed);
        Assert.Equal(90, t.GpuLoad);
        Assert.Equal(90, t.VramUsed);
        Assert.Equal(80, t.GpuTemp);
    }

    [Fact]
    public void Load_NullMetricThresholds_Defaults() =>
        Assert.Equal(80, LoadJson("""{"MetricThresholds":null}""").MetricThresholds.GpuTemp);

    [Fact]
    public void Load_MetricThresholdsClamped()
    {
        var t = LoadJson("""{"MetricThresholds":{"CpuLoad":0,"CpuTemp":999,"RamUsed":150}}""").MetricThresholds;
        Assert.Equal(1, t.CpuLoad);
        Assert.Equal(150, t.CpuTemp);
        Assert.Equal(100, t.RamUsed);
        Assert.Equal(90, t.GpuLoad);
        Assert.Equal(80, t.GpuTemp);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsThresholds()
    {
        var s = AppSettings.Defaults();
        s.MetricThresholds.GpuTemp = 85;
        s.MetricThresholds.VramUsed = 70;
        SettingsStore.Save(s, FilePath);
        var t = SettingsStore.Load(FilePath).MetricThresholds;
        Assert.Equal(85, t.GpuTemp);
        Assert.Equal(70, t.VramUsed);
    }

    [Fact]
    public void Thresholds_OfKind()
    {
        var t = new MetricThresholds { GpuTemp = 85 };
        Assert.Equal(85, t.Of(MetricKind.GpuTemp));
        Assert.Equal(90, t.Of(MetricKind.CpuLoad));
        Assert.True(MetricThresholds.IsTemperature(MetricKind.CpuTemp));
        Assert.False(MetricThresholds.IsTemperature(MetricKind.VramUsed));
    }
}
