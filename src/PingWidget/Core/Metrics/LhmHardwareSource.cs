using LibreHardwareMonitor.Hardware;

namespace PingWidget.Core.Metrics;

/// <summary>
/// LibreHardwareMonitor adapter. Opening takes about a second, so construct it off the UI thread.
/// Without admin rights (or without the PawnIO driver) CPU temperature reads as 0; everything else works.
/// </summary>
public sealed class LhmHardwareSource : IHardwareSource
{
    private readonly Computer _computer;

    public LhmHardwareSource()
    {
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = true };
        _computer.Open();
    }

    public IReadOnlyList<SensorReading> ReadSensors()
    {
        var result = new List<SensorReading>();
        foreach (var hw in _computer.Hardware)
        {
            Collect(hw, result);
            foreach (var sub in hw.SubHardware) Collect(sub, result);
        }
        return result;
    }

    private static void Collect(IHardware hw, List<SensorReading> into)
    {
        hw.Update();
        var type = hw.HardwareType.ToString();
        foreach (var s in hw.Sensors)
            into.Add(new SensorReading(type, hw.Name, s.SensorType.ToString(), s.Name, s.Value));
    }

    public void Dispose() => _computer.Close();
}
