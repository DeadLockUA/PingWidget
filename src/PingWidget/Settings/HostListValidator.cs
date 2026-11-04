using PingWidget.Core;

namespace PingWidget.Settings;

public static class HostListValidator
{
    public const int MinThreshold = HostConfig.MinThreshold;
    public const int MaxThreshold = HostConfig.MaxThreshold;

    /// <summary>Returns user-facing error messages; an empty list means the hosts are valid.</summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<HostConfig> hosts)
    {
        var errors = new List<string>();
        if (hosts.Count == 0)
        {
            errors.Add("Добавьте хотя бы один адрес");
            return errors;
        }

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in hosts)
        {
            var address = h.Address?.Trim() ?? "";
            if (address.Length == 0)
                AddOnce(errors, "Адрес не может быть пустым");
            else if (seen.TryGetValue(address, out var first))
                AddOnce(errors, $"Адрес {first} указан дважды");
            else
                seen[address] = address;

            if (h.ThresholdMs is < MinThreshold or > MaxThreshold)
                AddOnce(errors, $"Порог должен быть от {MinThreshold} до {MaxThreshold} мс");
        }
        return errors;
    }

    private static void AddOnce(List<string> errors, string message)
    {
        if (!errors.Contains(message)) errors.Add(message);
    }
}
