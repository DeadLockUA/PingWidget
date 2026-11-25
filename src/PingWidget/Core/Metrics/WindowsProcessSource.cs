using System.Runtime.InteropServices;

namespace PingWidget.Core.Metrics;

/// <summary>
/// Lists processes with NtQuerySystemInformation and per-process GPU load with performance counters.
/// Unlike opening each process, this also sees services and protected processes without admin rights.
/// </summary>
public sealed class WindowsProcessSource : IProcessSource
{
    private const int SystemProcessInformation = 5;
    private const uint StatusInfoLengthMismatch = 0xC0000004;
    private const int MaxBufferSize = 64 * 1024 * 1024;

    // SYSTEM_PROCESS_INFORMATION field offsets on x64.
    private const int NextEntryOffset = 0x00;
    private const int WorkingSetPrivateSize = 0x08;
    private const int UserTime = 0x28;
    private const int KernelTime = 0x30;
    private const int ImageNameLength = 0x38;
    private const int ImageNameBuffer = 0x40;
    private const int UniqueProcessId = 0x50;

    private readonly PdhGpuCounters _gpu = new();
    private int _bufferSize = 1024 * 1024;

    public WindowsProcessSource()
    {
        if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("Process offsets are x64 only.");
    }

    public ProcessSnapshot Read() => new(ReadProcesses(), _gpu.ReadLoadByPid());

    private List<ProcessSample> ReadProcesses()
    {
        while (true)
        {
            var buffer = Marshal.AllocHGlobal(_bufferSize);
            try
            {
                var status = NtQuerySystemInformation(SystemProcessInformation, buffer, _bufferSize, out _);
                if (status == 0) return Parse(buffer);
                if (status != StatusInfoLengthMismatch || _bufferSize >= MaxBufferSize)
                    throw new InvalidOperationException($"NtQuerySystemInformation failed: 0x{status:X8}");
                _bufferSize *= 2;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }

    private static List<ProcessSample> Parse(IntPtr entry)
    {
        var result = new List<ProcessSample>(512);
        while (true)
        {
            var pid = (int)Marshal.ReadInt64(entry, UniqueProcessId);
            int nameBytes = (ushort)Marshal.ReadInt16(entry, ImageNameLength);
            var namePtr = Marshal.ReadIntPtr(entry, ImageNameBuffer);
            // Pid 0 is the Idle process: its "CPU time" is the time nothing ran.
            if (pid != 0 && nameBytes > 0 && namePtr != IntPtr.Zero)
            {
                var name = Marshal.PtrToStringUni(namePtr, nameBytes / 2);
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
                result.Add(new ProcessSample(
                    pid,
                    name,
                    Marshal.ReadInt64(entry, UserTime) + Marshal.ReadInt64(entry, KernelTime),
                    Marshal.ReadInt64(entry, WorkingSetPrivateSize)));
            }

            var next = Marshal.ReadInt32(entry, NextEntryOffset);
            if (next == 0) return result;
            entry += next;
        }
    }

    public void Dispose() => _gpu.Dispose();

    [DllImport("ntdll.dll")]
    private static extern uint NtQuerySystemInformation(int infoClass, IntPtr buffer, int length, out int returnLength);
}

/// <summary>GPU load per process. Any failure means "no GPU data": the CPU and RAM names keep working.</summary>
internal sealed class PdhGpuCounters : IDisposable
{
    private const uint FormatDouble = 0x200;
    private const uint FormatNoCap100 = 0x8000;
    private const uint MoreData = 0x800007D2;
    private const int ItemSize = 24; // PDH_FMT_COUNTERVALUE_ITEM_W on x64
    private const int ItemStatusOffset = 8;
    private const int ItemValueOffset = 16;

    private readonly IntPtr _query;
    private readonly IntPtr _engines;
    private readonly IntPtr _adapterMemory;

    public PdhGpuCounters()
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0)
        {
            _query = IntPtr.Zero;
            return;
        }
        if (PdhAddEnglishCounterW(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _engines) != 0)
            _engines = IntPtr.Zero;
        if (PdhAddEnglishCounterW(_query, @"\GPU Adapter Memory(*)\Dedicated Usage", IntPtr.Zero, out _adapterMemory) != 0)
            _adapterMemory = IntPtr.Zero;
    }

    public IReadOnlyDictionary<int, double> ReadLoadByPid()
    {
        if (_query == IntPtr.Zero || _engines == IntPtr.Zero || PdhCollectQueryData(_query) != 0)
            return new Dictionary<int, double>();
        var adapter = _adapterMemory == IntPtr.Zero ? null : GpuEngineParser.BusiestAdapter(ReadArray(_adapterMemory));
        return GpuEngineParser.LoadByPid(ReadArray(_engines), adapter);
    }

    private static List<KeyValuePair<string, double>> ReadArray(IntPtr counter)
    {
        const uint format = FormatDouble | FormatNoCap100;
        var result = new List<KeyValuePair<string, double>>();
        uint size = 0;
        if (PdhGetFormattedCounterArrayW(counter, format, ref size, out _, IntPtr.Zero) != MoreData || size == 0)
            return result;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(counter, format, ref size, out var count, buffer) != 0) return result;
            for (var i = 0; i < count; i++)
            {
                var item = buffer + i * ItemSize;
                var status = Marshal.ReadInt32(item, ItemStatusOffset);
                if (status is not 0 and not 1) continue; // PDH_CSTATUS_VALID_DATA, PDH_CSTATUS_NEW_DATA
                var name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item));
                if (name is null) continue;
                result.Add(new(name, BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, ItemValueOffset))));
            }
            return result;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero) PdhCloseQuery(_query);
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
