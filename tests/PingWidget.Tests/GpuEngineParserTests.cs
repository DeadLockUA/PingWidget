using PingWidget.Core.Metrics;

namespace PingWidget.Tests;

public class GpuEngineParserTests
{
    private const string Dgpu = "luid_0x00000000_0x00015bc3_phys_0";
    private const string Igpu = "luid_0x00000000_0x00017623_phys_0";

    private static KeyValuePair<string, double> V(string name, double value) => new(name, value);

    [Fact]
    public void BusiestAdapter_IsTheOneWithMostDedicatedMemoryInUse() =>
        Assert.Equal(Dgpu, GpuEngineParser.BusiestAdapter([V(Igpu, 0), V(Dgpu, 2_899_992_576)]));

    [Fact]
    public void BusiestAdapter_NoData_IsNull() => Assert.Null(GpuEngineParser.BusiestAdapter([]));

    [Fact]
    public void LoadByPid_TakesTheBusiestEngine_LikeTaskManager()
    {
        var load = GpuEngineParser.LoadByPid(
            [V($"pid_10_{Dgpu}_eng_0_engtype_3d", 20), V($"pid_10_{Dgpu}_eng_3_engtype_copy", 5)], Dgpu);
        Assert.Equal(20, load[10]);
    }

    [Fact]
    public void LoadByPid_SumsRepeatedInstancesOfOneEngine()
    {
        var load = GpuEngineParser.LoadByPid(
            [V($"pid_10_{Dgpu}_eng_0_engtype_3d", 20), V($"pid_10_{Dgpu}_eng_0_engtype_3d", 15)], Dgpu);
        Assert.Equal(35, load[10]);
    }

    [Fact]
    public void LoadByPid_HandlesEngineTypeWithSpaces()
    {
        var load = GpuEngineParser.LoadByPid([V($"pid_2356_{Dgpu}_eng_10_engtype_high priority 3d", 4)], Dgpu);
        Assert.Equal(4, load[2356]);
    }

    [Fact]
    public void LoadByPid_IgnoresOtherAdapters()
    {
        var load = GpuEngineParser.LoadByPid([V($"pid_10_{Igpu}_eng_0_engtype_3d", 90)], Dgpu);
        Assert.Empty(load);
    }

    [Fact]
    public void LoadByPid_UnknownAdapter_CountsAllAdapters()
    {
        var load = GpuEngineParser.LoadByPid([V($"pid_10_{Igpu}_eng_0_engtype_3d", 90)], adapter: null);
        Assert.Equal(90, load[10]);
    }

    [Fact]
    public void LoadByPid_SkipsMalformedNames() =>
        Assert.Empty(GpuEngineParser.LoadByPid([V("_Total", 90)], adapter: null));
}
