namespace PingWidget.Core;

/// <summary>Ping samples of one host, oldest first.</summary>
public sealed class SampleHistory(int capacity = 120) : RingBuffer<PingSample>(capacity);
