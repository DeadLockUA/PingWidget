using Microsoft.Win32;
using PingWidget.Core;

namespace PingWidget.Tests;

public class AutoStartTests
{
    private const string Name = "PingWidget.Test";

    [Fact]
    public void SetAndClear()
    {
        try
        {
            AutoStart.Set(true, @"C:\x\PingWidget.exe", Name);
            Assert.True(AutoStart.IsEnabled(Name));
            using (var key = Registry.CurrentUser.OpenSubKey(AutoStart.RunKeyPath))
                Assert.Equal("\"C:\\x\\PingWidget.exe\"", key!.GetValue(Name));

            AutoStart.Set(false, @"C:\x\PingWidget.exe", Name);
            Assert.False(AutoStart.IsEnabled(Name));
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStart.RunKeyPath, writable: true);
            key?.DeleteValue(Name, throwOnMissingValue: false);
        }
    }

    [Fact]
    public void ClearWhenAbsent_DoesNotThrow()
    {
        AutoStart.Set(false, @"C:\x\PingWidget.exe", Name);
        Assert.False(AutoStart.IsEnabled(Name));
    }
}
