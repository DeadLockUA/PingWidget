using PingWidget.Core;
using PingWidget.Widget;

namespace PingWidget.Tests;

public class HostRowViewModelTests
{
    private static PingSample S(long? r) => new(DateTime.UnixEpoch, r);

    [Fact]
    public void LastAboveThreshold_ShowsMsAndRed()
    {
        var vm = new HostRowViewModel("8.8.8.8", 10) { Samples = [S(5), S(12)] };
        Assert.Equal("12 ms", vm.CurrentText);
        Assert.True(vm.IsAboveThreshold);
    }

    [Fact]
    public void LastLost_ShowsDashAndRed()
    {
        var vm = new HostRowViewModel("8.8.8.8", 10) { Samples = [S(5), S(null)] };
        Assert.Equal("—", vm.CurrentText);
        Assert.True(vm.IsAboveThreshold);
    }

    [Fact]
    public void NoSamples_ShowsDashNotRed()
    {
        var vm = new HostRowViewModel("8.8.8.8", 10);
        Assert.Equal("—", vm.CurrentText);
        Assert.False(vm.IsAboveThreshold);
    }

    [Fact]
    public void BelowThreshold_Green()
    {
        var vm = new HostRowViewModel("8.8.8.8", 100) { Samples = [S(20)] };
        Assert.Equal("20 ms", vm.CurrentText);
        Assert.False(vm.IsAboveThreshold);
    }

    [Fact]
    public void SettingSamples_RaisesPropertyChanged()
    {
        var vm = new HostRowViewModel("x", 10);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.Samples = [S(1)];
        Assert.Contains(nameof(HostRowViewModel.Samples), changed);
        Assert.Contains(nameof(HostRowViewModel.CurrentText), changed);
        Assert.Contains(nameof(HostRowViewModel.IsAboveThreshold), changed);
    }

    [Fact]
    public void ValuesProjectSamples()
    {
        var vm = new HostRowViewModel("x", 10);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.Samples = [S(5), S(null)];
        Assert.Equal(new double?[] { 5, null }, vm.Values);
        Assert.Contains(nameof(HostRowViewModel.Values), changed);
    }
}
