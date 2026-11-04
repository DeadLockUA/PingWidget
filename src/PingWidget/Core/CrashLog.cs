using System.IO;

namespace PingWidget.Core;

/// <summary>Appends unexpected exceptions to a small log file so a silent tray-app failure can be diagnosed.</summary>
public static class CrashLog
{
    public const int MaxBytes = 256 * 1024;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PingWidget", "error.log");

    /// <summary>Never throws: logging must not become a second failure.</summary>
    public static void Write(Exception exception, string path, DateTime now)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path) && new FileInfo(path).Length > MaxBytes) File.Delete(path);
            File.AppendAllText(path, $"[{now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Nothing sensible left to do.
        }
    }
}
