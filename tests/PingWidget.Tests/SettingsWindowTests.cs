using System.Runtime.ExceptionServices;
using PingWidget.Core;
using PingWidget.Core.Metrics;
using PingWidget.Settings;

namespace PingWidget.Tests;

public class SettingsWindowTests
{
    private static void RunSta(Action action)
    {
        ExceptionDispatchInfo? error = null;
        var t = new Thread(() =>
        {
            try { action(); }
            catch (Exception e) { error = ExceptionDispatchInfo.Capture(e); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        error?.Throw();
    }

    [Fact]
    public void Accept_TrimsAndParses() => RunSta(() =>
    {
        var w = new SettingsWindow(AppSettings.DefaultHosts(), new MetricThresholds());
        w.Rows.Add(new SettingsWindow.EditableHost { Address = " 1.1.1.1 ", Threshold = " 30 " });
        Assert.True(w.TryAccept());
        Assert.Equal(new HostConfig("1.1.1.1", 30), w.Result![^1]);
        Assert.Equal(4, w.Result.Count);
    });

    [Fact]
    public void Accept_NonNumericThreshold_ShowsErrorAndNoResult() => RunSta(() =>
    {
        var w = new SettingsWindow([new HostConfig("8.8.8.8", 100)], new MetricThresholds());
        w.Rows[0].Threshold = "abc";
        Assert.False(w.TryAccept());
        Assert.Null(w.Result);
        Assert.Equal("Порог должен быть от 1 до 10000 мс", w.ErrorText);
    });

    [Fact]
    public void Accept_AllRemoved_ShowsError() => RunSta(() =>
    {
        var w = new SettingsWindow([new HostConfig("8.8.8.8", 100)], new MetricThresholds());
        w.Rows.Clear();
        Assert.False(w.TryAccept());
        Assert.Equal("Добавьте хотя бы один адрес", w.ErrorText);
    });

    [Fact]
    public void Accept_ThresholdsParsed() => RunSta(() =>
    {
        var w = new SettingsWindow(AppSettings.DefaultHosts(), new MetricThresholds());
        Assert.Equal("80", w.ThresholdTexts[MetricKind.GpuTemp]);
        w.ThresholdTexts[MetricKind.GpuTemp] = " 85 ";
        Assert.True(w.TryAccept());
        Assert.Equal(85, w.ResultThresholds!.GpuTemp);
        Assert.Equal(90, w.ResultThresholds.CpuLoad);
    });

    [Fact]
    public void Accept_BadThreshold_ShowsTempError() => RunSta(() =>
    {
        var w = new SettingsWindow(AppSettings.DefaultHosts(), new MetricThresholds());
        w.ThresholdTexts[MetricKind.CpuTemp] = "abc";
        Assert.False(w.TryAccept());
        Assert.Null(w.ResultThresholds);
        Assert.Contains("Порог температуры должен быть от 1 до 150 °C", w.ErrorText);
    });

    [Fact]
    public void Accept_HostAndThresholdErrors_BothShown() => RunSta(() =>
    {
        var w = new SettingsWindow([new HostConfig("8.8.8.8", 100)], new MetricThresholds());
        w.Rows[0].Threshold = "0";
        w.ThresholdTexts[MetricKind.RamUsed] = "200";
        Assert.False(w.TryAccept());
        Assert.Contains("Порог должен быть от 1 до 10000 мс", w.ErrorText);
        Assert.Contains("Порог нагрузки и памяти должен быть от 1 до 100 %", w.ErrorText);
    });
}
