using PingWidget.Core.Metrics;

namespace PingWidget.Tests;

public class SensorPickerTests
{
    private static SensorReading S(string hwType, string hwName, string type, string name, float? v) => new(hwType, hwName, type, name, v);

    /// <summary>Sensors measured on the user's machine (Ryzen 5 5600X, RX 6900 XT, non-admin).</summary>
    private static List<SensorReading> UserMachine(float cpuTemp = 0) =>
    [
        S("Cpu", "AMD Ryzen 5 5600X", "Load", "CPU Core #1", 47.76f),
        S("Cpu", "AMD Ryzen 5 5600X", "Load", "CPU Total", 46.27f),
        S("Cpu", "AMD Ryzen 5 5600X", "Load", "CPU Core Max", 85.07f),
        S("Cpu", "AMD Ryzen 5 5600X", "Temperature", "Core (Tctl/Tdie)", cpuTemp),
        S("Memory", "Virtual Memory", "Data", "Memory Used", 29.44f),
        S("Memory", "Virtual Memory", "Data", "Memory Available", 5.73f),
        S("Memory", "Virtual Memory", "Load", "Memory", 83.70f),
        S("Memory", "Total Memory", "Data", "Memory Used", 18.50f),
        S("Memory", "Total Memory", "Data", "Memory Available", 13.42f),
        S("Memory", "Total Memory", "Load", "Memory", 57.95f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "SmallData", "GPU Memory Total", 16368f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "SmallData", "D3D Dedicated Memory Total", 16338.2f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "SmallData", "D3D Dedicated Memory Used", 3533.7f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "Load", "D3D 3D", 11.7f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "SmallData", "GPU Memory Used", 3533f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "Temperature", "GPU Core", 59f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "Temperature", "GPU Hot Spot", 67f),
        S("GpuAmd", "AMD Radeon RX 6900 XT", "Load", "GPU Core", 5f),
    ];

    [Fact]
    public void UserMachine_AllButCpuTemp()
    {
        var m = SensorPicker.Pick(UserMachine());
        Assert.Equal(46.27, m[MetricKind.CpuLoad].Value!.Value, 2);
        Assert.Null(m[MetricKind.CpuTemp].Value);
        Assert.Equal(57.95, m[MetricKind.RamUsed].Value!.Value, 2);
        Assert.Equal(18.50, m[MetricKind.RamUsed].UsedGb!.Value, 2);
        Assert.Equal(31.92, m[MetricKind.RamUsed].TotalGb!.Value, 2);
        Assert.Equal(5, m[MetricKind.GpuLoad].Value);
        Assert.Equal(59, m[MetricKind.GpuTemp].Value);
        Assert.Equal(3533 / 1024.0, m[MetricKind.VramUsed].UsedGb!.Value, 3);
        Assert.Equal(16368 / 1024.0, m[MetricKind.VramUsed].TotalGb!.Value, 3);
        Assert.Equal(21.58, m[MetricKind.VramUsed].Value!.Value, 2);
    }

    [Fact]
    public void AlwaysAllSixKeys() => Assert.Equal(6, SensorPicker.Pick([]).Count);

    [Fact]
    public void VirtualMemoryIgnored() =>
        Assert.Null(SensorPicker.Pick(UserMachine().Where(s => s.HardwareName != "Total Memory").ToList())[MetricKind.RamUsed].Value);

    [Fact]
    public void CpuTempPresentWhenAdmin() => Assert.Equal(54, SensorPicker.Pick(UserMachine(cpuTemp: 54))[MetricKind.CpuTemp].Value);

    [Fact]
    public void CpuTempNaN_Missing() => Assert.Null(SensorPicker.Pick(UserMachine(cpuTemp: float.NaN))[MetricKind.CpuTemp].Value);

    [Fact]
    public void CpuTempNull_Missing()
    {
        var list = UserMachine();
        list[3] = list[3] with { Value = null };
        Assert.Null(SensorPicker.Pick(list)[MetricKind.CpuTemp].Value);
    }

    [Fact]
    public void IntelCpuPackageTemp() =>
        Assert.Equal(61, SensorPicker.Pick([S("Cpu", "Intel", "Temperature", "CPU Package", 61f)])[MetricKind.CpuTemp].Value);

    [Fact]
    public void NvidiaNamesWork()
    {
        var list = UserMachine().Select(s => s.HardwareType == "GpuAmd" ? s with { HardwareType = "GpuNvidia" } : s).ToList();
        Assert.Equal(59, SensorPicker.Pick(list)[MetricKind.GpuTemp].Value);
    }

    [Fact]
    public void DiscretePreferredOverIntel()
    {
        List<SensorReading> list = [S("GpuIntel", "Intel UHD", "Load", "GPU Core", 77f), .. UserMachine()];
        Assert.Equal(5, SensorPicker.Pick(list)[MetricKind.GpuLoad].Value);
    }

    [Fact]
    public void IntelOnly_Used()
    {
        var m = SensorPicker.Pick([S("GpuIntel", "Intel UHD", "Load", "GPU Core", 12f)]);
        Assert.Equal(12, m[MetricKind.GpuLoad].Value);
        Assert.Null(m[MetricKind.VramUsed].Value);
        Assert.Null(m[MetricKind.GpuTemp].Value);
    }

    [Fact]
    public void NoGpu_AllGpuMissing()
    {
        var m = SensorPicker.Pick(UserMachine().Where(s => !s.HardwareType.StartsWith("Gpu")).ToList());
        Assert.Null(m[MetricKind.GpuLoad].Value);
        Assert.Null(m[MetricKind.VramUsed].Value);
        Assert.Null(m[MetricKind.GpuTemp].Value);
    }

    [Fact]
    public void VramFallbackToD3D()
    {
        var m = SensorPicker.Pick(UserMachine().Where(s => !s.Name.StartsWith("GPU Memory")).ToList());
        Assert.Equal(16338.2 / 1024, m[MetricKind.VramUsed].TotalGb!.Value, 2);
        Assert.Equal(3533.7 / 16338.2 * 100, m[MetricKind.VramUsed].Value!.Value, 1);
    }

    [Fact]
    public void ZeroTotal_Missing()
    {
        var list = UserMachine().Select(s => s.Name is "GPU Memory Total" or "D3D Dedicated Memory Total" ? s with { Value = 0 } : s).ToList();
        Assert.Null(SensorPicker.Pick(list)[MetricKind.VramUsed].Value);
    }

    [Fact]
    public void DiscreteByDedicatedMemory_WinsOverIntegratedListedFirst()
    {
        List<SensorReading> list =
        [
            S("GpuAmd", "AMD Radeon(TM) Graphics", "Load", "GPU Core", 3f),
            S("GpuAmd", "AMD Radeon(TM) Graphics", "SmallData", "GPU Memory Total", 512f),
            S("GpuAmd", "AMD Radeon(TM) Graphics", "SmallData", "GPU Memory Used", 400f),
            S("GpuNvidia", "NVIDIA GeForce RTX 4070", "Load", "GPU Core", 42f),
            S("GpuNvidia", "NVIDIA GeForce RTX 4070", "SmallData", "D3D Dedicated Memory Total", 8192f),
            S("GpuNvidia", "NVIDIA GeForce RTX 4070", "SmallData", "D3D Dedicated Memory Used", 1024f),
        ];
        var m = SensorPicker.Pick(list);
        Assert.Equal(42, m[MetricKind.GpuLoad].Value);
        Assert.Equal(8, m[MetricKind.VramUsed].TotalGb!.Value, 3);
    }

    [Fact]
    public void TdiePreferredOverTctl()
    {
        var m = SensorPicker.Pick(
        [
            S("Cpu", "AMD Ryzen 7 2700X", "Temperature", "Core (Tctl)", 70f),
            S("Cpu", "AMD Ryzen 7 2700X", "Temperature", "Core (Tdie)", 60f),
        ]);
        Assert.Equal(60, m[MetricKind.CpuTemp].Value);
    }
}
