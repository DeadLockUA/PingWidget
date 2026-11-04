using PingWidget.Core;

namespace PingWidget.Tests;

public class CrashLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "PingWidgetTests", Guid.NewGuid().ToString("N"));
    private string LogPath => Path.Combine(_dir, "error.log");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Write_AppendsExceptionWithTimestamp()
    {
        CrashLog.Write(new InvalidOperationException("first boom"), LogPath, new DateTime(2026, 11, 4, 12, 0, 0));
        CrashLog.Write(new InvalidOperationException("second boom"), LogPath, new DateTime(2026, 11, 4, 12, 0, 1));
        var text = File.ReadAllText(LogPath);
        Assert.Contains("2026-11-04 12:00:00", text);
        Assert.Contains("first boom", text);
        Assert.Contains("second boom", text);
    }

    [Fact]
    public void Write_LargeLog_IsTruncated()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(LogPath, new string('x', CrashLog.MaxBytes + 10));
        CrashLog.Write(new Exception("fresh"), LogPath, DateTime.UnixEpoch);
        var text = File.ReadAllText(LogPath);
        Assert.True(text.Length < CrashLog.MaxBytes);
        Assert.Contains("fresh", text);
    }

    [Fact]
    public void Write_UnwritablePath_DoesNotThrow() =>
        CrashLog.Write(new Exception("x"), Path.Combine(_dir, "\0bad"), DateTime.UnixEpoch);
}
