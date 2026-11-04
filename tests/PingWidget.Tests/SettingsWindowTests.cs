using System.Runtime.ExceptionServices;
using PingWidget.Core;
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
        var w = new SettingsWindow(AppSettings.DefaultHosts());
        w.Rows.Add(new SettingsWindow.EditableHost { Address = " 1.1.1.1 ", Threshold = " 30 " });
        Assert.True(w.TryAccept());
        Assert.Equal(new HostConfig("1.1.1.1", 30), w.Result![^1]);
        Assert.Equal(4, w.Result.Count);
    });

    [Fact]
    public void Accept_NonNumericThreshold_ShowsErrorAndNoResult() => RunSta(() =>
    {
        var w = new SettingsWindow([new HostConfig("8.8.8.8", 100)]);
        w.Rows[0].Threshold = "abc";
        Assert.False(w.TryAccept());
        Assert.Null(w.Result);
        Assert.Equal("Порог должен быть от 1 до 10000 мс", w.ErrorText);
    });

    [Fact]
    public void Accept_AllRemoved_ShowsError() => RunSta(() =>
    {
        var w = new SettingsWindow([new HostConfig("8.8.8.8", 100)]);
        w.Rows.Clear();
        Assert.False(w.TryAccept());
        Assert.Equal("Добавьте хотя бы один адрес", w.ErrorText);
    });
}
