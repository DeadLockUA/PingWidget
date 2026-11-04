namespace PingWidget.Core;

/// <summary>Thread-safe fixed-size ring buffer of ping samples, oldest first.</summary>
public sealed class SampleHistory
{
    private readonly PingSample[] _buffer;
    private readonly object _lock = new();
    private int _start;
    private int _count;

    public SampleHistory(int capacity = 120)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _buffer = new PingSample[capacity];
    }

    public int Count { get { lock (_lock) return _count; } }

    public void Add(PingSample sample)
    {
        lock (_lock)
        {
            if (_count < _buffer.Length)
            {
                _buffer[(_start + _count) % _buffer.Length] = sample;
                _count++;
            }
            else
            {
                _buffer[_start] = sample;
                _start = (_start + 1) % _buffer.Length;
            }
        }
    }

    public IReadOnlyList<PingSample> Snapshot()
    {
        lock (_lock)
        {
            var result = new PingSample[_count];
            for (var i = 0; i < _count; i++) result[i] = _buffer[(_start + i) % _buffer.Length];
            return result;
        }
    }
}
