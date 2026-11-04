using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace PingWidget.Core.Metrics;

/// <summary>
/// LibreHardwareMonitor adapter for CPU and GPU; RAM comes from Windows (GlobalMemoryStatusEx).
/// Opening takes about a second, so construct it off the UI thread.
/// Without admin rights (or without the PawnIO driver) CPU temperature reads as 0; everything else works.
/// </summary>
public sealed class LhmHardwareSource : IHardwareSource
{
    private readonly Computer _computer;

    public LhmHardwareSource()
    {
        // Memory group stays off: with admin + PawnIO it probes the SMBus for DIMM sensors every second,
        // which we never show. RAM usage comes from Windows instead.
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMemoryEnabled = false };
        try { _computer.Open(); }
        catch
        {
            try { _computer.Close(); } catch { /* best effort */ }
            throw;
        }
    }

    internal bool MemoryGroupEnabled => _computer.IsMemoryEnabled;

    internal IEnumerable<ISensor> AllSensors =>
        _computer.Hardware.SelectMany(h => h.Sensors.Concat(h.SubHardware.SelectMany(s => s.Sensors)));

    public IReadOnlyList<SensorReading> ReadSensors()
    {
        var result = new List<SensorReading>();
        foreach (var hw in _computer.Hardware)
        {
            Collect(hw, result);
            foreach (var sub in hw.SubHardware) Collect(sub, result);
        }
        AddWindowsMemory(result);
        return result;
    }

    private static void Collect(IHardware hw, List<SensorReading> into)
    {
        hw.Update();
        var type = hw.HardwareType.ToString();
        foreach (var s in hw.Sensors)
        {
            // LHM keeps a 24 h value history per sensor by default; we never read it. Re-applied every tick
            // because some sensors (e.g. AMD Tctl) appear lazily.
            if (s.ValuesTimeWindow != TimeSpan.Zero) s.ValuesTimeWindow = TimeSpan.Zero;
            into.Add(new SensorReading(type, hw.Name, s.SensorType.ToString(), s.Name, s.Value));
        }
    }

    /// <summary>Emits RAM in the same shape LHM's memory group uses, so <see cref="SensorPicker"/> needs no special case.</summary>
    private static void AddWindowsMemory(List<SensorReading> into)
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0) return;
        const double Gb = 1024.0 * 1024 * 1024;
        var used = (status.TotalPhys - status.AvailPhys) / Gb;
        var available = status.AvailPhys / Gb;
        var percent = (status.TotalPhys - status.AvailPhys) * 100.0 / status.TotalPhys;
        into.Add(new SensorReading("Memory", "Total Memory", "Load", "Memory", (float)percent));
        into.Add(new SensorReading("Memory", "Total Memory", "Data", "Memory Used", (float)used));
        into.Add(new SensorReading("Memory", "Total Memory", "Data", "Memory Available", (float)available));
    }

    public void Dispose() => _computer.Close();

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
