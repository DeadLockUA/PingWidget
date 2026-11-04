using Microsoft.Win32;

namespace PingWidget.Core;

/// <summary>"Start with Windows" via the per-user Run key.</summary>
public static class AutoStart
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "PingWidget";

    public static bool IsEnabled(string valueName = DefaultValueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(valueName) is string s && s.Length > 0;
    }

    public static void Set(bool enabled, string exePath, string valueName = DefaultValueName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
            key.SetValue(valueName, $"\"{exePath}\"", RegistryValueKind.String);
        else
            key.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
