using System.IO;
using System.Text.Json;

namespace PingWidget.Core;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PingWidget", "settings.json");

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return AppSettings.Defaults();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
            if (settings is null) return AppSettings.Defaults();
            settings.Hosts ??= AppSettings.DefaultHosts();
            return settings;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return AppSettings.Defaults();
        }
    }

    public static void Save(AppSettings settings, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
