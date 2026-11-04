using System.ComponentModel;
using PingWidget.Core.Metrics;

namespace PingWidget.Widget;

public sealed class MetricRowViewModel : INotifyPropertyChanged
{
    private IReadOnlyList<MetricReading> _readings = [];

    public MetricRowViewModel(MetricKind kind, string label, double threshold)
    {
        Kind = kind;
        Label = label;
        Threshold = threshold;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MetricKind Kind { get; }
    public string Label { get; }
    public double Threshold { get; }

    public IReadOnlyList<MetricReading> Readings
    {
        get => _readings;
        set
        {
            _readings = value;
            Raise(nameof(Readings));
            Raise(nameof(Values));
            Raise(nameof(CurrentText));
            Raise(nameof(IsAboveThreshold));
        }
    }

    public IReadOnlyList<double?> Values => _readings.Select(r => r.Value).ToArray();

    public string CurrentText => _readings.Count > 0 ? MetricFormatter.Format(Kind, _readings[^1]) : "—";

    public bool IsAboveThreshold => _readings.Count > 0 && _readings[^1].Value > Threshold;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
