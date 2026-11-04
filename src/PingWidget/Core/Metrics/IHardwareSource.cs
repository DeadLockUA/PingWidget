namespace PingWidget.Core.Metrics;

/// <summary>Reads the current hardware sensors. May throw; callers treat failures as "no data".</summary>
public interface IHardwareSource : IDisposable
{
    IReadOnlyList<SensorReading> ReadSensors();
}
