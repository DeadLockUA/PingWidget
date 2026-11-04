using System.ComponentModel;
using PingWidget.Core;

namespace PingWidget.Widget;

public sealed class HostRowViewModel : INotifyPropertyChanged
{
    private IReadOnlyList<PingSample> _samples = [];

    public HostRowViewModel(string address, int thresholdMs)
    {
        Address = address;
        ThresholdMs = thresholdMs;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Address { get; }
    public int ThresholdMs { get; }

    public IReadOnlyList<PingSample> Samples
    {
        get => _samples;
        set
        {
            _samples = value;
            Raise(nameof(Samples));
            Raise(nameof(CurrentText));
            Raise(nameof(IsAboveThreshold));
        }
    }

    public string CurrentText =>
        _samples.Count > 0 && _samples[^1].RoundtripMs is long ms ? $"{ms} ms" : "—";

    public bool IsAboveThreshold =>
        _samples.Count > 0 && (_samples[^1].IsLost || _samples[^1].RoundtripMs > ThresholdMs);

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
