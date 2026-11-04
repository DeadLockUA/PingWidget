using System.IO;
using System.Text.Json;
using PingWidget.Core.Metrics;

namespace PingWidget.Core;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PingWidget", "settings.json");

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return AppSettings.Defaults();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
            if (settings is null) return AppSettings.Defaults();
            settings.Hosts = Sanitize(settings.Hosts);
            settings.MetricThresholds = (settings.MetricThresholds ?? new MetricThresholds()).Sanitized();
            return settings;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return AppSettings.Defaults();
        }
    }

    /// <summary>
    /// Makes a hand-edited host list safe to run: drops null/blank/duplicate entries, trims addresses,
    /// clamps thresholds, and falls back to the default hosts if nothing usable is left.
    /// </summary>
    private static List<HostConfig> Sanitize(List<HostConfig>? hosts)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<HostConfig>();
        foreach (var h in hosts ?? [])
        {
            var address = h?.Address?.Trim();
            if (string.IsNullOrEmpty(address) || !seen.Add(address)) continue;
            result.Add(new HostConfig(address, Math.Clamp(h!.ThresholdMs, HostConfig.MinThreshold, HostConfig.MaxThreshold)));
        }
        return result.Count > 0 ? result : AppSettings.DefaultHosts();
    }

    public static void Save(AppSettings settings, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
